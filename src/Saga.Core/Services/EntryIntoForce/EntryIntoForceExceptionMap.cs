using Saga.Model;

namespace Saga.Core.Services.EntryIntoForce
{
    /// <summary>
    /// Rozstrzygnięcia wyjątków przypięte do encji (klucz po tożsamości, bo eId nie jest w akcie
    /// unikalny).
    ///
    /// Data wejścia w życie i moc wsteczna są trzymane w OSOBNYCH kanałach, bo wyjątek nie musi
    /// nadpisywać obu składników naraz. Wyjątek „z wyjątkiem art. 2, który wchodzi w życie …"
    /// nadpisuje obie: brak mocy wstecznej w takim wyjątku ZDEJMUJE moc wsteczną reguły ogólnej,
    /// bo wskazana jednostka jest z tej reguły wyłączona w całości.
    /// </summary>
    internal sealed class EntryIntoForceExceptionMap
    {
        private const string UnresolvedEntry = "data nierozstrzygnięta";
        private const string NoRetroactivity = "brak mocy wstecznej";

        private readonly Dictionary<BaseEntity, DateTime?> _units = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<TextSegment, DateTime?> _segments = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<BaseEntity, DateTime?> _unitRetroactivity = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<TextSegment, DateTime?> _segmentRetroactivity = new(ReferenceEqualityComparer.Instance);
        private readonly List<string> _conflicts = [];

        /// <summary>Wyjątki wskazujące tę samą jednostkę różnymi datami — pierwszy wpis wygrywa.</summary>
        internal IReadOnlyList<string> Conflicts => _conflicts;

        /// <summary>Wyjątek nadający jednostce własną datę wejścia w życie.</summary>
        internal void MarkUnit(BaseEntity unit, EntryDates dates)
        {
            Mark(_units, unit, dates.Entry, unit.Id, UnresolvedEntry);
            MarkUnitRetroactivity(unit, dates.RetroactiveFrom);
        }

        /// <summary>
        /// Wyjątek nadający jednostce samą moc wsteczną („…, z tym że art. 2 z mocą od dnia …").
        /// </summary>
        internal void MarkUnitRetroactivity(BaseEntity unit, DateTime? retroactiveFrom) =>
            Mark(_unitRetroactivity, unit, retroactiveFrom, $"{unit.Id} (moc wsteczna)", NoRetroactivity);

        internal void MarkSegment(TextSegment segment, EntryDates dates)
        {
            Mark(_segments, segment, dates.Entry, $"zdanie {segment.Order}", UnresolvedEntry);
            MarkSegmentRetroactivity(segment, dates.RetroactiveFrom);
        }

        internal void MarkSegmentRetroactivity(TextSegment segment, DateTime? retroactiveFrom) =>
            Mark(_segmentRetroactivity, segment, retroactiveFrom,
                $"zdanie {segment.Order} (moc wsteczna)", NoRetroactivity);

        internal bool TryGetUnit(BaseEntity unit, out DateTime? date) => _units.TryGetValue(unit, out date);

        internal bool TryGetUnitRetroactivity(BaseEntity unit, out DateTime? date) =>
            _unitRetroactivity.TryGetValue(unit, out date);

        internal bool TryGetSegment(TextSegment segment, out DateTime? date) =>
            _segments.TryGetValue(segment, out date);

        internal bool TryGetSegmentRetroactivity(TextSegment segment, out DateTime? date) =>
            _segmentRetroactivity.TryGetValue(segment, out date);

        private void Mark<TKey>(
            Dictionary<TKey, DateTime?> channel, TKey key, DateTime? date, string description, string absent)
            where TKey : notnull
        {
            if (channel.TryGetValue(key, out var existing))
            {
                if (existing != date)
                    _conflicts.Add($"{description}: {Describe(existing, absent)} vs {Describe(date, absent)}");
                return;
            }

            channel[key] = date;
        }

        private static string Describe(DateTime? date, string absent) =>
            date is { } value ? value.ToString("yyyy-MM-dd") : absent;
    }
}
