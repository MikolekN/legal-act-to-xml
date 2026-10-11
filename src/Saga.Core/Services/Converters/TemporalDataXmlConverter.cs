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

            foreach (var group in groups)
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

            return element;
        }

        private static XElement EventElement(LifecycleEvent lifecycleEvent, LegalDocument document)
        {
            var element = Element(XmlConverterConstants.Metadata.Temporal.Event,
                new XAttribute(XmlConverterConstants.Metadata.Temporal.Id, lifecycleEvent.Id),
                new XAttribute(XmlConverterConstants.Metadata.Temporal.Kind, lifecycleEvent.Kind == LifecycleEventKind.EntryIntoForce
                    ? XmlConverterConstants.Metadata.Temporal.KindEntryIntoForce
                    : XmlConverterConstants.Metadata.Temporal.KindRetroactivity),
                new XAttribute(XmlConverterConstants.Metadata.Temporal.Origin, lifecycleEvent.FromFormula
                    ? XmlConverterConstants.Metadata.Temporal.OriginComputed
                    : XmlConverterConstants.Metadata.Temporal.OriginRead),
                new XAttribute(XmlConverterConstants.Metadata.Temporal.Date,
                    lifecycleEvent.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));

            if (lifecycleEvent.Condition is { Length: > 0 } condition)
                element.Add(new XAttribute(XmlConverterConstants.Metadata.Temporal.Condition, condition));

            // Podstawą i dowodem zdarzenia jest jednostka, która je zapisuje: pozycja wyliczenia wyjątków
            // („art. 43 pkt 2"), a dla reguły ogólnej i zdarzenia z kilku pozycji — klauzula. Klauzulę niesie bywa
            // część wspólna wyliczenia („art_3__intro") — dowodem jest wtedy najbliższa jednostka z oznaczeniem.
            var stated = lifecycleEvent.SourceUnitEId ?? document.EntryIntoForce?.SourceUnitEId;
            if (stated is not null && WellFormedPrefix.Match(stated) is { Success: true, Value: var source })
            {
                if (Citation(source) is { } citation)
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

        /// <summary>Powołanie jednostki z jej eId („art_43__pkt_1" → „art. 43 pkt 1"); null dla poziomu spoza tabeli.</summary>
        private static string? Citation(string eId)
        {
            var parts = new List<string>();
            foreach (var segment in eId.Split(EIdSegment.Separator))
            {
                var split = segment.IndexOf('_');
                var label = segment[..split] switch
                {
                    "art" => "art.",
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
