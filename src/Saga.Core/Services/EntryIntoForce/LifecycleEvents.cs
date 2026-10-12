using System.Globalization;
using Saga.Model;
using Saga.Model.EntryIntoForce;

namespace Saga.Core.Services.EntryIntoForce
{
    /// <summary>Rodzaj zdarzenia w cyklu życia aktu.</summary>
    internal enum LifecycleEventKind
    {
        EntryIntoForce,
        Retroactivity,

        /// <summary>Ujednolicenie: ogłoszenie tekstu jednolitego.</summary>
        Consolidation,

        /// <summary>Uchylenie jednostki — w tekście jednolitym zmiana, po której jednostka to „(uchylony)".</summary>
        Repeal,
    }

    /// <summary>
    /// Zdarzenie cyklu życia aktu: jedna data wejścia w życie albo mocy wstecznej wraz z jednostkami,
    /// których dotyczy wyjątkiem. Zdarzenie reguły ogólnej nie wymienia jednostek — obejmuje pozostałe.
    /// </summary>
    internal sealed class LifecycleEvent
    {
        internal required string Id { get; init; }
        internal required LifecycleEventKind Kind { get; init; }
        internal required DateTime Date { get; init; }

        /// <summary>
        /// Zdarzenie, którego dnia akt nie podaje („z dniem wdrożenia rozwiązań technicznych") — granica istnieje,
        /// dnia nie znamy.
        /// </summary>
        internal bool Undated { get; init; }

        /// <summary>Data wyliczona z formuły względnej („po upływie 14 dni od dnia ogłoszenia"), nie podana wprost.</summary>
        internal required bool FromFormula { get; init; }

        /// <summary>Formuła, z której wyliczono datę; null dla daty podanej wprost.</summary>
        internal string? Condition { get; init; }

        /// <summary>Czy to zdarzenie reguły ogólnej.</summary>
        internal bool IsGeneral { get; set; }

        /// <summary>Numer odnośnika tekstu jednolitego, z którego zdarzenie odczytano; null dla zdarzeń z klauzuli.</summary>
        internal string? EvidenceFootnote { get; init; }

        /// <summary>Akt, który zdarzenie sprawia, gdy jest nim inny akt niż ten dokument („DU/2023/818").</summary>
        internal string? CausingAct { get; init; }

        /// <summary>
        /// Przepisy aktu zmieniającego, które zdarzenie sprawiają, tak jak nazywają je odnośniki
        /// („art. 2 pkt 1 lit. a; art. 2 pkt 1 lit. c").
        /// </summary>
        internal string? CausingProvision { get; init; }

        /// <summary>Jednostki objęte zdarzeniem na mocy wyjątku.</summary>
        internal List<ScopedUnitReference> Targets { get; } = [];

        /// <summary>Jednostki klauzuli, w których zapisano wyjątki składające się na to zdarzenie.</summary>
        internal HashSet<string> SourceUnitEIds { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Jednostka, w której zdarzenie zapisano, gdy jest jedna (pozycja wyliczenia wyjątków). Null dla reguły
        /// ogólnej i dla zdarzenia złożonego z kilku pozycji — podstawą jest wtedy cała klauzula.
        /// </summary>
        internal string? SourceUnitEId => SourceUnitEIds.Count == 1 ? SourceUnitEIds.Single() : null;
    }

    /// <summary>
    /// Grupa czasowa: okres, w którym jednostka obowiązuje, liczony od zdarzenia wejścia w życie,
    /// poprzedzony stosowaniem z mocą wsteczną, jeśli jednostka ją ma. Jednostka wskazuje grupę
    /// atrybutem <c>okres</c>.
    /// </summary>
    internal sealed record TimeGroup(string Id, string? EntryEventId, string? RetroactiveEventId, string? ConsolidationEventId = null)
    {
        /// <summary>Zdarzenie zamykające okres obowiązywania — przedział półotwarty [od, do).</summary>
        internal string? UntilEventId { get; init; }
    }

    /// <summary>
    /// Zdarzenia cyklu życia aktu odczytane z klauzuli wejścia w życie (ADR-0022): jedno na każdą różną
    /// datę wejścia w życie i jedno na każdą datę mocy wstecznej. Wyjątki o tej samej dacie i pochodzeniu
    /// trafiają do jednego zdarzenia.
    /// </summary>
    internal static class LifecycleEvents
    {
        internal static IReadOnlyList<LifecycleEvent> Of(LegalDocument document)
        {
            var info = document.EntryIntoForce;
            var journal = JournalKey(document.SourceJournal);

            // Tekst jednolity ma zdarzenie ujednolicenia — w dniu ogłoszenia obwieszczenia.
            if (document.Announcement is { IsConsolidatedText: true })
            {
                var consolidated = new List<LifecycleEvent>();
                if (document.AnnouncementDate is { } announced)
                {
                    consolidated.Add(new LifecycleEvent
                    {
                        Id = EventId(LifecycleEventKind.Consolidation, announced, journal, fromFormula: true),
                        Kind = LifecycleEventKind.Consolidation,
                        Date = announced,
                        FromFormula = true,
                        IsGeneral = true,
                    });
                }

                // Zmiany, na które czekają jednostki drukowane w dwóch wersjach (ADR-0060) — w kolejności
                // dokumentu, więc dowodem zdarzenia z kilku odnośników jest pierwszy z nich. Zdarzenie zbiera
                // zwykle kilka przepisów aktu zmieniającego; podstawą są wszystkie, w kolejności dokumentu (ADR-0063).
                consolidated.AddRange(document.Articles
                    .SelectMany(article => EditorialUnitWalker.SelfAndDescendants(article, includeAmendmentContent: false))
                    .SelectMany(unit => unit is IHasTextSegments sentences
                        ? sentences.TextSegments.Select(sentence => sentence.Version).Prepend(unit.Version)
                        : [unit.Version])
                    .OfType<WordingVersion>()
                    .Where(version => version.ChangeFootnote.Length > 0)
                    .Distinct()
                    .GroupBy(version => ChangeEvent(version, journal, provision: null).Id)
                    .Select(changes => ChangeEvent(
                        changes.First(),
                        journal,
                        string.Join("; ", changes.Select(version => version.ChangeProvision).OfType<string>().Distinct())))
                    .OrderBy(change => change.Undated).ThenBy(change => change.Date));
                return consolidated;
            }

            var events = new Dictionary<string, LifecycleEvent>(StringComparer.Ordinal);

            if (document.EffectiveDate != default)
                Get(LifecycleEventKind.EntryIntoForce, document.EffectiveDate, info?.Formula).IsGeneral = true;

            if (document.RetroactiveFrom != default)
                Get(LifecycleEventKind.Retroactivity, document.RetroactiveFrom, formula: null).IsGeneral = true;

            foreach (var partial in info?.Partials ?? [])
            {
                if (partial.Kind == PartialEntryIntoForceKind.EntryDate && partial.ResolvedDate is { } entry)
                    AddTargets(Get(LifecycleEventKind.EntryIntoForce, entry, partial.Formula), partial);

                if (partial.RetroactiveFrom is { } retroactive)
                    AddTargets(Get(LifecycleEventKind.Retroactivity, retroactive, formula: null), partial);
            }

            return events.Values.OrderBy(e => e.Date).ThenBy(e => e.Kind).ToList();

            LifecycleEvent Get(LifecycleEventKind kind, DateTime date, EntryIntoForceFormula? formula)
            {
                // Bez rozłożonej formuły data jest po prostu dana — jak odczytana wprost.
                var fromFormula = formula is { IsResolvable: true } && formula.Kind != EntryIntoForceFormulaKind.OnSpecificDate;
                var id = EventId(kind, date, journal, fromFormula);

                if (!events.TryGetValue(id, out var existing))
                {
                    existing = new LifecycleEvent
                    {
                        Id = id,
                        Kind = kind,
                        Date = date,
                        FromFormula = fromFormula,
                        Condition = fromFormula ? formula!.RawText.Trim() : null,
                    };
                    events.Add(id, existing);
                }
                return existing;
            }
        }

        /// <summary>
        /// Grupa czasowa jednostki o podanych datach albo null, gdy żadna z dat nie ma zdarzenia.
        /// Identyfikator jak w przykładach autorów schematu: „okr_obowiazywanie.&lt;zdarzenie&gt;.x" (bez końca).
        /// </summary>
        internal static TimeGroup? GroupFor(IReadOnlyList<LifecycleEvent> events, DateTime entry, DateTime retroactive)
        {
            if (events is [{ Kind: LifecycleEventKind.Consolidation } consolidation, ..])
                return entry == default && retroactive == default ? ConsolidationGroup(consolidation) : null;

            var entryEvent = EventOn(events, LifecycleEventKind.EntryIntoForce, entry);
            var retroactiveEvent = EventOn(events, LifecycleEventKind.Retroactivity, retroactive);

            return (entryEvent, retroactiveEvent) switch
            {
                (null, null) => null,
                ({ } e, null) => new TimeGroup($"okr_obowiazywanie.{e.Id}.x", e.Id, null),
                (null, { } r) => new TimeGroup($"okr_moc-wsteczna.{r.Id}.x", null, r.Id),
                ({ } e, { } r) => new TimeGroup($"okr_moc-wsteczna.{r.Id}.obowiazywanie.{e.Id}.x", e.Id, r.Id),
            };
        }

        /// <summary>
        /// Zmiana jednostki tekstu jednolitego: jej wejście w życie albo — gdy jednostkę uchyla — uchylenie.
        /// Zdarzenie należy do aktu, który zmianę wprowadza (ADR-0063): zmiany tego samego rodzaju, dnia i aktu to
        /// jedno zdarzenie; zmiana bez dnia ma w miejscu daty rozróżnik — numer odnośnika, w którym stoi jej termin.
        /// Gdy aktu zmieniającego nie nazwano publikatorem, w polu aktu zostaje dokument, w którym zdarzenie odczytano.
        /// </summary>
        private static LifecycleEvent ChangeEvent(WordingVersion version, string? journal, string? provision)
        {
            var evidence = Evidence(version);
            var kind = version.ChangeRepeals ? LifecycleEventKind.Repeal : LifecycleEventKind.EntryIntoForce;
            var stamp = version.ChangeDate is { } day
                ? day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : $"odn-{evidence}";

            return new LifecycleEvent
            {
                Id = EventId(kind, stamp, version.ChangeAct?.Replace('/', '-') ?? journal, fromFormula: false),
                Kind = kind,
                Date = version.ChangeDate ?? default,
                Undated = version.ChangeDate is null,
                FromFormula = false,
                Condition = version.ChangeDate is null ? version.ChangeTerm : null,
                EvidenceFootnote = evidence,
                CausingAct = version.ChangeAct,
                CausingProvision = provision,
            };
        }

        /// <summary>
        /// Odnośnik, z którego zdarzenie odczytano: ten, w którym stoi jego termin — także wtedy, gdy to odnośnik
        /// nazywający akt zmieniający, a nie odnośnik samej zmiany.
        /// </summary>
        private static string Evidence(WordingVersion version) =>
            version.TermFootnote.Length > 0 ? version.TermFootnote : version.ChangeFootnote;

        /// <summary>
        /// Grupa czasowa wersji jednostki tekstu jednolitego: przyszła obowiązuje OD zdarzenia zmiany, obowiązująca
        /// — DO tego samego zdarzenia („x" w miejscu granicy otwartej). Null, gdy wersji nie powiązano ze zmianą.
        /// </summary>
        internal static TimeGroup? GroupFor(WordingVersion version, LegalDocument document)
        {
            if (version.ChangeFootnote.Length == 0)
                return null;

            var change = ChangeEvent(version, JournalKey(document.SourceJournal), provision: null).Id;
            return version.Kind == WordingVersionKind.Future
                ? new TimeGroup($"okr_obowiazywanie.{change}.x", change, null)
                : new TimeGroup($"okr_obowiazywanie.x.{change}", null, null) { UntilEventId = change };
        }

        /// <summary>Grupa tekstu jednolitego: „okr_ujednolicenie.&lt;zdarzenie&gt;", jak w przykładzie autorów schematu.</summary>
        private static TimeGroup ConsolidationGroup(LifecycleEvent consolidation) =>
            new($"okr_ujednolicenie.{consolidation.Id}", null, null, consolidation.Id);

        /// <summary>Zdarzenie danego rodzaju w danym dniu; przy dwóch pochodzeniach tej samej daty — reguły ogólnej.</summary>
        private static LifecycleEvent? EventOn(IReadOnlyList<LifecycleEvent> events, LifecycleEventKind kind, DateTime date) =>
            date == default
                ? null
                : events.Where(e => e.Kind == kind && e.Date == date).OrderByDescending(e => e.IsGeneral).FirstOrDefault();

        /// <summary>Identyfikator zdarzenia jak w przykładach autorów schematu: „zdarz_wejscie_2024-09-25_DU-2024-928_domyslne".</summary>
        internal static string EventId(LifecycleEventKind kind, DateTime date, string? journal, bool fromFormula) =>
            EventId(kind, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), journal, fromFormula);

        /// <summary>Identyfikator zdarzenia o podanym stemplu: dacie albo rozróżniku zdarzenia bez dnia.</summary>
        private static string EventId(LifecycleEventKind kind, string stamp, string? journal, bool fromFormula)
        {
            var parts = new List<string>
            {
                kind switch
                {
                    LifecycleEventKind.EntryIntoForce => "zdarz_wejscie",
                    LifecycleEventKind.Consolidation => "zdarz_ujednolicenie",
                    LifecycleEventKind.Repeal => "zdarz_uchylenie",
                    _ => "zdarz_moc-wsteczna",
                },
                stamp,
            };
            if (journal is not null)
                parts.Add(journal);
            parts.Add(fromFormula ? "domyslne" : "odczytane");
            return string.Join('_', parts);
        }

        /// <summary>Wyjątek o dacie reguły ogólnej nic do niej nie dodaje — ogólna obejmuje jego jednostki i tak.</summary>
        private static void AddTargets(LifecycleEvent target, PartialEntryIntoForce partial)
        {
            if (target.IsGeneral)
                return;

            target.Targets.AddRange(partial.Targets);
            if (partial.SourceUnitEId is { } source)
                target.SourceUnitEIds.Add(source);
        }

        private static string? JournalKey(JournalInfo journal) =>
            journal.Year is { } year && journal.Positions.Count > 0
                ? $"{journal.PublisherSymbol()}-{year}-{journal.Positions[0]}"
                : null;
    }
}
