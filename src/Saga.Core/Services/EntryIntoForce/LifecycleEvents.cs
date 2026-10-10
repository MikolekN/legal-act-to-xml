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

        /// <summary>Data wyliczona z formuły względnej („po upływie 14 dni od dnia ogłoszenia"), nie podana wprost.</summary>
        internal required bool FromFormula { get; init; }

        /// <summary>Formuła, z której wyliczono datę; null dla daty podanej wprost.</summary>
        internal string? Condition { get; init; }

        /// <summary>Czy to zdarzenie reguły ogólnej.</summary>
        internal bool IsGeneral { get; set; }

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
    internal sealed record TimeGroup(string Id, string? EntryEventId, string? RetroactiveEventId);

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

        /// <summary>Zdarzenie danego rodzaju w danym dniu; przy dwóch pochodzeniach tej samej daty — reguły ogólnej.</summary>
        private static LifecycleEvent? EventOn(IReadOnlyList<LifecycleEvent> events, LifecycleEventKind kind, DateTime date) =>
            date == default
                ? null
                : events.Where(e => e.Kind == kind && e.Date == date).OrderByDescending(e => e.IsGeneral).FirstOrDefault();

        /// <summary>Identyfikator zdarzenia jak w przykładach autorów schematu: „zdarz_wejscie_2024-09-25_DU-2024-928_domyslne".</summary>
        internal static string EventId(LifecycleEventKind kind, DateTime date, string? journal, bool fromFormula)
        {
            var parts = new List<string>
            {
                kind == LifecycleEventKind.EntryIntoForce ? "zdarz_wejscie" : "zdarz_moc-wsteczna",
                date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
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
