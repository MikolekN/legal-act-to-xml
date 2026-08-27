using Saga.Model;

namespace Saga.Core.Services.EntryIntoForce
{
    internal sealed class EntryIntoForceExceptionMap
    {
        private readonly Dictionary<BaseEntity, DateTime?> _units = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<TextSegment, DateTime?> _segments = new(ReferenceEqualityComparer.Instance);
        private readonly List<string> _conflicts = [];

        /// <summary>Wyjątki wskazujące tę samą jednostkę różnymi datami — pierwszy wpis wygrywa.</summary>
        internal IReadOnlyList<string> Conflicts => _conflicts;

        internal void MarkUnit(BaseEntity unit, DateTime? date)
        {
            if (_units.TryGetValue(unit, out var existing))
            {
                if (existing != date)
                    _conflicts.Add($"{unit.Id}: {Describe(existing)} vs {Describe(date)}");
                return;
            }

            _units[unit] = date;
        }

        internal void MarkSegment(TextSegment segment, DateTime? date)
        {
            if (_segments.TryGetValue(segment, out var existing))
            {
                if (existing != date)
                    _conflicts.Add($"zdanie {segment.Order}: {Describe(existing)} vs {Describe(date)}");
                return;
            }

            _segments[segment] = date;
        }

        internal bool TryGetUnit(BaseEntity unit, out DateTime? date) => _units.TryGetValue(unit, out date);

        internal bool TryGetSegment(TextSegment segment, out DateTime? date) =>
            _segments.TryGetValue(segment, out date);

        private static string Describe(DateTime? date) =>
            date is { } value ? value.ToString("yyyy-MM-dd") : "data nierozstrzygnięta";
    }
}
