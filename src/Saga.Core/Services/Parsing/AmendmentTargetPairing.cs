using Saga.Model;

namespace Saga.Core.Services.Parsing
{
    /// <summary>Przypisuje jednostki treści nowelizacji jej celom.</summary>
    internal static class AmendmentTargetPairing
    {
        /// <summary>Jednostki redakcyjne najwyższego poziomu w treści nowelizacji, w kolejności eksportu.</summary>
        internal static IReadOnlyList<BaseEntity> ContentUnits(AmendmentContent? content)
        {
            if (content is null)
                return [];

            return Enumerable.Empty<BaseEntity>()
                .Concat(content.Articles ?? [])
                .Concat(content.Paragraphs ?? [])
                .Concat(content.Points ?? [])
                .Concat(content.Letters ?? [])
                .Concat(content.Tirets ?? [])
                .ToList();
        }

        internal static IReadOnlyList<IReadOnlyList<BaseEntity>>? Pair(
            IReadOnlyList<StructuralAmendmentReference> targets, IReadOnlyList<BaseEntity> units)
        {
            if (targets.Count == 0 || units.Count == 0)
                return null;

            if (PairByDesignation(targets, units) is { } paired)
                return paired;

            if (units.Count == targets.Count && targets.All(target => target.RangeEnd is null))
                return units.Select(unit => (IReadOnlyList<BaseEntity>)new[] { unit }).ToList();

            return null;
        }

        private static List<IReadOnlyList<BaseEntity>>? PairByDesignation(
            IReadOnlyList<StructuralAmendmentReference> targets, IReadOnlyList<BaseEntity> units)
        {
            var paired = new List<IReadOnlyList<BaseEntity>>();
            var next = 0;

            foreach (var target in targets)
            {
                if (next >= units.Count || NumberOf(units[next]) != Designation(target.Structure))
                    return null;

                var last = target.RangeEnd is { } end ? Designation(end) : NumberOf(units[next]);
                var from = next;
                while (next < units.Count && NumberOf(units[next]) != last)
                    next++;
                if (next == units.Count)
                    return null;

                next++;
                paired.Add(units.Skip(from).Take(next - from).ToList());
            }

            return next == units.Count ? paired : null;
        }

        private static string? NumberOf(BaseEntity unit) => unit.Number?.Value;

        /// <summary>Oznaczenie najgłębszej jednostki adresu („art_5__ust_2" → „2", tiret „1.2" → „2").</summary>
        private static string? Designation(StructuralReference reference) =>
            reference.Tiret?.Split('.')[^1] ?? reference.Letter ?? reference.Point ?? reference.Paragraph ?? reference.Article;
    }
}
