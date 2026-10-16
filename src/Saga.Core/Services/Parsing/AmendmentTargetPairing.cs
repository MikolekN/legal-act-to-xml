using Saga.Model;
using Saga.Model.EditorialUnits;
using Saga.Model.SystematizingUnits;

namespace Saga.Core.Services.Parsing
{
    /// <summary>Przypisuje jednostki treści nowelizacji jej celom.</summary>
    internal static class AmendmentTargetPairing
    {
        /// <summary>Jednostki najwyższego poziomu w treści nowelizacji, w kolejności eksportu.</summary>
        internal static IReadOnlyList<BaseEntity> ContentUnits(AmendmentContent? content)
        {
            if (content is null)
                return [];

            return Enumerable.Empty<BaseEntity>()
                .Concat(content.SystematizingUnits ?? [])
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

        /// <summary>
        /// Zakotwicza jednostki cytatu w akcie zmienianym: punkt dodawany „w art. 68 ust. 1" ma eId
        /// art_68__ust_1__pkt_5 — adres celu powyżej poziomu jednostki i jej własna ścieżka. Jednostki dzieli
        /// między cele tak jak eksport; bez rozstrzygnięcia — wszystkie pod pierwszym celem.
        /// </summary>
        internal static void AnchorContentUnits(Amendment amendment)
        {
            var units = ContentUnits(amendment.Content);
            var targets = amendment.Targets ?? [];
            if (targets.Count == 0)
                return;

            // Część wspólna cytowana bez gospodarza („dodaje się część wspólną w brzmieniu:", „w pkt 9 wprowadzenie do
            // wyliczenia otrzymuje brzmienie:") stoi pod całym adresem celu — gospodarzem jest jednostka wskazana komendą
            // (art_18zg__ust_2 → art_18zg__ust_2__czw). Gdy cytat niesie obok niej pozycje jej wyliczenia („w ust. 1 pkt 2
            // i część wspólna otrzymują brzmienie:"), cel wskazuje pozycję, a gospodarzem jest jednostka nad nią
            // (art_12__ust_1__czw).
            foreach (var part in amendment.Content?.CommonParts ?? [])
                if (part.Parent is null)
                    part.AddressAnchor = (QuotesItemsOf(amendment.Content!, part.ListItemType)
                        ? HostOf(targets[0].Structure, part.ListItemType)
                        : targets[0].Structure).ToString();

            if (units.Count == 0)
                return;

            var paired = Pair(targets, units) ?? [units];
            for (var i = 0; i < paired.Count; i++)
                foreach (var unit in paired[i])
                    unit.AddressAnchor = AnchorFor(targets[i].Structure, unit);
        }

        private static bool QuotesItemsOf(AmendmentContent content, UnitType? listItemType) => listItemType switch
        {
            UnitType.Point => content.Points.Count > 0,
            UnitType.Letter => content.Letters.Count > 0,
            UnitType.Tiret => content.Tirets.Count > 0,
            _ => false,
        };

        /// <summary>
        /// Adres jednostki, która trzyma wyliczenie danego rodzaju: cel ucięty powyżej pozycji wyliczenia (punkty —
        /// do ustępu, litery — do punktu, tirety — do litery). Bez rodzaju pozycji — cały cel.
        /// </summary>
        private static StructuralReference HostOf(StructuralReference target, UnitType? listItemType)
        {
            var host = target.Clone();
            host.Sentence = null;
            switch (listItemType)
            {
                case UnitType.Point:
                    host.Point = host.Letter = host.Tiret = null;
                    break;
                case UnitType.Letter:
                    host.Letter = host.Tiret = null;
                    break;
                case UnitType.Tiret:
                    host.Tiret = null;
                    break;
                default:
                    return target;
            }

            return host;
        }

        private static string AnchorFor(StructuralReference target, BaseEntity unit)
        {
            // Rozdział dodawany „w dziale II" → dzial_2__rozdz_9a: ścieżka celu powyżej rangi jednostki.
            if (unit is ISystematizingUnit)
                return EIdSegment.Join((target.Systematizing ?? string.Empty)
                    .Split(EIdSegment.Separator, StringSplitOptions.RemoveEmptyEntries)
                    .Where(segment => SystematizingHierarchy.LevelOfPrefix(segment.Split('_')[0]) is { } level
                        && SystematizingHierarchy.Rank(level) < SystematizingHierarchy.Rank(unit)));

            var level = unit switch
            {
                Paragraph => 1,
                Point => 2,
                Letter => 3,
                Tiret => 4,
                _ => 0,
            };
            if (level == 0)
                return string.Empty;

            // Jednostka dodawana w załączniku stoi pod jego adresem (zal_1__ust_2__pkt_57a, ADR-0080).
            return new StructuralReference
            {
                Annex = target.Annex,
                Article = target.Article,
                Paragraph = level > 1 ? target.Paragraph : null,
                ParagraphIsCodeParagraph = level > 1 && target.ParagraphIsCodeParagraph,
                Point = level > 2 ? target.Point : null,
                Letter = level > 3 ? target.Letter : null,
            }.ToString();
        }

        private static string? NumberOf(BaseEntity unit) => unit.Number?.Value;

        /// <summary>Oznaczenie najgłębszej jednostki adresu („art_5__ust_2" → „2", tiret „1.2" → „2").</summary>
        private static string? Designation(StructuralReference reference) =>
            reference.Tiret?.Split('.')[^1] ?? reference.Letter ?? reference.Point ?? reference.Paragraph ?? reference.Article;
    }
}
