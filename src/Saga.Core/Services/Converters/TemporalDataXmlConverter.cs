using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Saga.Core.Services.EntryIntoForce;
using Saga.Model;
using Saga.Model.EntryIntoForce;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Dane czasowe aktu w metadanych (ADR-0022): cykl życia jako zdarzenia wejścia w życie i mocy
    /// wstecznej. Zdarzenie wyjątku wymienia jednostki, których dotyczy; zdarzenie reguły ogólnej — żadnej.
    /// </summary>
    internal static class TemporalDataXmlConverter
    {
        private const string EIdSegmentPattern = @"[a-z]{2,5}_[A-Za-z0-9]+(?:\[[^\]]*\])?";

        /// <summary>eId, który przejdzie wzorzec schematu — każdy segment ma oznaczenie.</summary>
        private static readonly Regex WellFormedEId = new(
            $"^{EIdSegmentPattern}(?:__{EIdSegmentPattern})*$", RegexOptions.Compiled);

        /// <summary>Najdłuższy początek eId złożony z segmentów z oznaczeniem („art_3__intro" → „art_3").</summary>
        private static readonly Regex WellFormedPrefix = new(
            $"^{EIdSegmentPattern}(?:__{EIdSegmentPattern})*(?=__|$)", RegexOptions.Compiled);

        /// <summary>
        /// Od kiedy obowiązuje wersja aktu zapisana w dokumencie: tekst jednolity — od zdarzenia ujednolicenia, jak
        /// w przykładzie autorów schematu (ADR-0072). Null dla aktu, który nie jest tekstem jednolitym, i dla
        /// tekstu jednolitego bez dnia ogłoszenia.
        /// </summary>
        internal static XElement? VersionValidity(LegalDocument document)
        {
            if (document.Announcement is not { IsConsolidatedText: true }
                || LifecycleEvents.Of(document) is not [{ Kind: LifecycleEventKind.Consolidation } consolidation, ..])
            {
                return null;
            }

            return Element(XmlConverterConstants.Metadata.VersionValidityElement,
                Element(XmlConverterConstants.Metadata.VersionValidFrom, consolidation.Id));
        }

        /// <summary>Element <c>DaneCzasowe</c> albo null, gdy akt nie ma żadnej rozstrzygniętej daty.</summary>
        internal static XElement? ToXml(LegalDocument document)
        {
            var events = LifecycleEvents.Of(document);
            if (events.Count == 0)
                return null;

            var cycle = Element(XmlConverterConstants.Metadata.Temporal.ActLifecycle);
            foreach (var lifecycleEvent in events)
                cycle.Add(EventElement(lifecycleEvent, document));

            return Element(XmlConverterConstants.Metadata.Temporal.Element, cycle);
        }

        /// <summary>
        /// Nadaje jednostkom, zdaniom i częściom wspólnym atrybut <c>okres</c> i dopisuje do
        /// <c>DaneCzasowe</c> grupy czasowe, na które wskazują. Główna jest grupa reguły ogólnej.
        /// </summary>
        internal static void AssignPeriods(XElement act, LegalDocument document)
        {
            XNamespace ns = XmlConverterConstants.Namespace;
            var temporal = act.Element(ns + XmlConverterConstants.Metadata.Element)?.Element(ns + XmlConverterConstants.Metadata.Temporal.Element);
            if (temporal is null)
                return;

            var events = LifecycleEvents.Of(document);
            var main = LifecycleEvents.GroupFor(events, document.EffectiveDate, document.RetroactiveFrom);
            var groups = new List<TimeGroup>();
            if (main is not null)
                groups.Add(main);

            foreach (var element in act.Descendants())
            {
                // Wersja czasowa jednostki: okres ograniczony zdarzeniem zmiany, przed projekcjami na daty.
                if (element.Annotation<WordingVersion>() is { } version && !IsAnnexContent(element)
                    && LifecycleEvents.GroupFor(version, document) is { } versionGroup)
                {
                    if (!groups.Contains(versionGroup))
                        groups.Add(versionGroup);

                    var projections = element.Attributes()
                        .Where(a => a.Name.LocalName is XmlConverterConstants.Contents.EntryIntoForce
                            or XmlConverterConstants.Contents.InForceUntil or XmlConverterConstants.Contents.UnitState)
                        .ToList();
                    projections.ForEach(a => a.Remove());
                    element.Add(new XAttribute(XmlConverterConstants.Metadata.Temporal.Period, versionGroup.Id), projections);
                    continue;
                }

                if (element.Annotation<UnitDates>() is not { } dates
                    || LifecycleEvents.GroupFor(events, dates.Entry, dates.Retroactive) is not { } group)
                    continue;

                // Treść załącznika niesie datę, ale nie odwołanie do grupy czasowej — jak w przykładzie autorów
                // schematu. Jednostka pod treścią dowolną nie ma deklaracji, więc jej „okres" nie ma dla
                // walidatora wartości i odwołanie zostawałoby niespełnione.
                if (IsAnnexContent(element))
                    continue;

                if (!groups.Contains(group))
                    groups.Add(group);

                // „okres" przed „wejscie-w-zycie", jak w przykładach autorów schematu.
                var entry = element.Attribute(XmlConverterConstants.Contents.EntryIntoForce);
                entry?.Remove();
                element.Add(new XAttribute(XmlConverterConstants.Metadata.Temporal.Period, group.Id), entry);
            }

            // Grupy w kolejności chronologicznej, jak zdarzenia w CyklZyciaAktu i w przykładach autorów schematu —
            // według zdarzenia, od którego biegnie okres (okres bez początku — według zdarzenia, które go zamyka);
            // przy remisie zostaje kolejność pierwszego użycia.
            var position = events.Select((lifecycleEvent, index) => (lifecycleEvent.Id, index))
                .GroupBy(pair => pair.Id)
                .ToDictionary(same => same.Key, same => same.First().index);
            int Start(TimeGroup group) =>
                (group is { RetroactiveEventId: null, EntryEventId: null, ConsolidationEventId: null }
                    ? new[] { group.UntilEventId }
                    : new[] { group.RetroactiveEventId, group.EntryEventId, group.ConsolidationEventId })
                    .Where(id => id is not null && position.ContainsKey(id))
                    .Select(id => position[id!])
                    .DefaultIfEmpty(int.MaxValue)
                    .Min();

            foreach (var group in groups.OrderBy(Start))
                temporal.Add(GroupElement(group, isMain: group == main));
        }

        private static bool IsAnnexContent(XElement element) =>
            element.Ancestors().Any(ancestor => ancestor.Name.LocalName == XmlConverterConstants.Attachments.Segment);

        private static XElement GroupElement(TimeGroup group, bool isMain)
        {
            var element = Element(XmlConverterConstants.Metadata.Temporal.Group,
                new XAttribute(XmlConverterConstants.Metadata.Temporal.Id, group.Id),
                new XAttribute(XmlConverterConstants.Metadata.Temporal.GroupIsMain, isMain));

            if (group.RetroactiveEventId is { } retroactive)
            {
                var interval = Element(XmlConverterConstants.Metadata.Temporal.Interval,
                    new XAttribute(XmlConverterConstants.Metadata.Temporal.Kind, XmlConverterConstants.Metadata.Temporal.KindRetroactivity),
                    new XAttribute(XmlConverterConstants.Metadata.Temporal.IntervalFrom, retroactive));
                if (group.EntryEventId is { } until)
                    interval.Add(new XAttribute(XmlConverterConstants.Metadata.Temporal.IntervalTo, until));
                element.Add(interval);
            }

            if (group.EntryEventId is { } entry)
                element.Add(Element(XmlConverterConstants.Metadata.Temporal.Interval,
                    new XAttribute(XmlConverterConstants.Metadata.Temporal.Kind, XmlConverterConstants.Metadata.Temporal.IntervalInForce),
                    new XAttribute(XmlConverterConstants.Metadata.Temporal.IntervalFrom, entry)));

            // Brzmienie obowiązujące do zdarzenia: początek otwarty, koniec wskazuje zdarzenie zmiany.
            if (group.UntilEventId is { } closing)
                element.Add(Element(XmlConverterConstants.Metadata.Temporal.Interval,
                    new XAttribute(XmlConverterConstants.Metadata.Temporal.Kind, XmlConverterConstants.Metadata.Temporal.IntervalInForce),
                    new XAttribute(XmlConverterConstants.Metadata.Temporal.IntervalTo, closing)));

            if (group.ConsolidationEventId is { } consolidation)
                element.Add(Element(XmlConverterConstants.Metadata.Temporal.Interval,
                    new XAttribute(XmlConverterConstants.Metadata.Temporal.Kind, XmlConverterConstants.Metadata.Temporal.KindConsolidation),
                    new XAttribute(XmlConverterConstants.Metadata.Temporal.IntervalFrom, consolidation)));

            return element;
        }

        private static XElement EventElement(LifecycleEvent lifecycleEvent, LegalDocument document)
        {
            var element = Element(XmlConverterConstants.Metadata.Temporal.Event,
                new XAttribute(XmlConverterConstants.Metadata.Temporal.Id, lifecycleEvent.Id),
                new XAttribute(XmlConverterConstants.Metadata.Temporal.Kind, lifecycleEvent.Kind switch
                {
                    LifecycleEventKind.EntryIntoForce => XmlConverterConstants.Metadata.Temporal.KindEntryIntoForce,
                    LifecycleEventKind.Consolidation => XmlConverterConstants.Metadata.Temporal.KindConsolidation,
                    LifecycleEventKind.Repeal => XmlConverterConstants.Metadata.Temporal.KindRepeal,
                    _ => XmlConverterConstants.Metadata.Temporal.KindRetroactivity,
                }),
                new XAttribute(XmlConverterConstants.Metadata.Temporal.Origin, lifecycleEvent.FromFormula
                    ? XmlConverterConstants.Metadata.Temporal.OriginComputed
                    : XmlConverterConstants.Metadata.Temporal.OriginRead));

            // Zdarzenie bez dnia zostaje bez daty: granica okresu istnieje, dnia akt nie podaje.
            if (!lifecycleEvent.Undated)
                element.Add(new XAttribute(XmlConverterConstants.Metadata.Temporal.Date,
                    lifecycleEvent.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));

            if (lifecycleEvent.Condition is { Length: > 0 } condition)
                element.Add(new XAttribute(XmlConverterConstants.Metadata.Temporal.Condition, condition));

            // Zdarzenie odczytane z odnośników tekstu jednolitego: dowodem jest odnośnik, w którym stoi jego
            // termin, podstawą — przepisy, które zmianę wprowadzają, a źródłem — akt, do którego należą (ADR-0063).
            if (lifecycleEvent.EvidenceFootnote is { } footnote)
            {
                if (lifecycleEvent.CausingProvision is { Length: > 0 } provision)
                    element.Add(new XAttribute(XmlConverterConstants.Metadata.Temporal.Basis, provision));
                if (lifecycleEvent.CausingAct is { } act)
                    element.Add(new XAttribute(XmlConverterConstants.Metadata.Temporal.Source, XmlConverterConstants.ELIUrl + "eli/" + act));
                element.Add(new XAttribute(XmlConverterConstants.Metadata.Temporal.Evidence,
                    $"{StructuralReference.FootnotePrefix}_{footnote}"));
                return element;
            }

            // Podstawą i dowodem zdarzenia jest jednostka, która je zapisuje: pozycja wyliczenia wyjątków
            // („art. 43 pkt 2"), a dla reguły ogólnej i zdarzenia z kilku pozycji — klauzula. Klauzulę niesie bywa
            // część wspólna wyliczenia („art_3__intro") — dowodem jest wtedy najbliższa jednostka z oznaczeniem.
            var stated = lifecycleEvent.SourceUnitEId ?? document.EntryIntoForce?.SourceUnitEId;
            if (stated is not null && WellFormedPrefix.Match(stated) is { Success: true, Value: var source })
            {
                if (Citation(source, IsSectionUnit(source, document)) is { } citation)
                    element.Add(new XAttribute(XmlConverterConstants.Metadata.Temporal.Basis, citation));
                if (document.SourceJournal.GetELIStrings().FirstOrDefault() is { } eli)
                    element.Add(new XAttribute(XmlConverterConstants.Metadata.Temporal.Source, XmlConverterConstants.ELIUrl + "eli/" + eli));
                element.Add(new XAttribute(XmlConverterConstants.Metadata.Temporal.Evidence, source));
            }

            foreach (var target in lifecycleEvent.Targets)
                if (UnitElement(target, document) is { } unit)
                    element.Add(unit);

            return element;
        }

        /// <summary>Jednostka objęta zdarzeniem, z zakresem („w zakresie art. 85a ust. 1"), jeśli wyjątek go zawęża.</summary>
        private static XElement? UnitElement(ScopedUnitReference target, LegalDocument document)
        {
            var eId = target.Structure.ToString();
            if (!WellFormedEId.IsMatch(eId))
                return null;

            var unit = Element(XmlConverterConstants.Metadata.Temporal.Unit,
                new XAttribute(XmlConverterConstants.Contents.eId, eId));

            var host = target.ScopeStructures.Count > 0 ? EntryIntoForceTargetResolver.Resolve(document, target.Structure) : null;
            foreach (var scope in target.ScopeStructures.Select(s => ScopeAddress(host, s)).Where(s => WellFormedEId.IsMatch(s)))
                unit.Add(Element(XmlConverterConstants.Metadata.Temporal.UnitInScope,
                    new XAttribute(XmlConverterConstants.Contents.eId, scope)));

            if (target.ScopeKind == ScopeKind.Prose && target.ScopeRawText is { Length: > 0 } prose)
                unit.Add(Element(XmlConverterConstants.Metadata.Temporal.UnitInScope,
                    Element(XmlConverterConstants.Metadata.Temporal.SubjectScope,
                        new XAttribute(XmlConverterConstants.Metadata.Temporal.SubjectScopeText, prose))));

            return unit;
        }

        /// <summary>
        /// Adres jednostki z zakresu. Klauzula zapisuje go względem przepisu zmieniającego („art. 13 pkt 2
        /// lit. b w zakresie ust. 1d"), a jednostka stoi w akcie zmienianym — adresem jest więc eId jednostki
        /// cytowanej („art_17__ust_1d"), a gdy przepis jej nie cytuje (uchylenie, zmiana wyrazów) — cel jego
        /// komendy. Zakres, którego przepis nie wskazuje, zostaje ścieżką z klauzuli.
        /// </summary>
        private static string ScopeAddress(BaseEntity? host, StructuralReference scope)
        {
            var written = scope.ToString();

            if (host is not null && EntryIntoForceTargetResolver.ResolveInAmendment(host, scope) is { Id: { Length: > 0 } quoted })
                return scope.Sentence is null
                    ? quoted
                    : EIdSegment.Join([quoted, new StructuralReference { Sentence = scope.Sentence }.ToString()]);

            var amendment = host is null ? null : EntryIntoForceTargetResolver.AmendmentOf(host);
            var targets = amendment?.Targets.Select(t => t.Structure.ToString()) ?? [];
            return targets.FirstOrDefault(target =>
                       target == written || target.EndsWith(EIdSegment.Separator + written, StringComparison.Ordinal))
                   ?? written;
        }

        /// <summary>
        /// Jednostka podstawowa eId jest paragrafem aktu opartego na paragrafach: w adresie ma segment <c>art</c>
        /// (ADR-0034), w powołaniu — „§" („§ 3. Rozporządzenie wchodzi w życie …" — Dz. U. 2026 poz. 661).
        /// </summary>
        private static bool IsSectionUnit(string eId, LegalDocument document)
        {
            var basic = eId.Split(EIdSegment.Separator)[0];
            return document.Articles.Any(article => article.IsSection && article.Id == basic);
        }

        /// <summary>Powołanie jednostki z jej eId („art_43__pkt_1" → „art. 43 pkt 1"); null dla poziomu spoza tabeli.</summary>
        private static string? Citation(string eId, bool sectionBased)
        {
            var parts = new List<string>();
            foreach (var segment in eId.Split(EIdSegment.Separator))
            {
                var split = segment.IndexOf('_');
                var label = segment[..split] switch
                {
                    "art" => sectionBased && parts.Count == 0 ? "§" : "art.",
                    "ust" => "ust.",
                    "par" => "§",
                    "pkt" => "pkt",
                    "lit" => "lit.",
                    _ => null,
                };
                if (label is null)
                    return null;
                parts.Add($"{label} {segment[(split + 1)..]}");
            }
            return string.Join(' ', parts);
        }
    }
}
