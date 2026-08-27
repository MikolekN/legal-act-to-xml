using Saga.Model;
using Saga.Model.EditorialUnits;

namespace Saga.Core.Services.EntryIntoForce
{
    /// <summary>
    /// Odnajduje jednostkę wskazaną ścieżką i zapisuje jej datę wejścia w życie.
    /// </summary>
    internal static class EntryIntoForceTargetResolver
    {
        /// <summary>Znajduje encję wskazaną przez ścieżkę.</summary>
        internal static BaseEntity? Resolve(LegalDocument document, StructuralReference reference)
        {
            if (reference.Article is null)
                return null;

            var article = document.Articles.FirstOrDefault(a => Matches(a.Number?.Value, reference.Article));
            return article is null ? null : DescendFromArticle(article, reference);
        }

        /// <summary>
        /// Odnajduje jednostkę wskazaną zakresem („w zakresie art. 85a ust. 1") w cytowanej treści.
        /// </summary>
        internal static BaseEntity? ResolveInAmendment(BaseEntity host, StructuralReference scope)
        {
            if (host is not IHasAmendments owner || owner.Amendment?.Content is not { } content)
                return null;

            if (scope.Article is not null &&
                content.Articles.FirstOrDefault(a => Matches(a.Number?.Value, scope.Article)) is { } article)
                return DescendFromArticle(article, scope);

            if (scope.Paragraph is not null &&
                content.Paragraphs.FirstOrDefault(p => Matches(p.Number?.Value, scope.Paragraph)) is { } paragraph)
                return DescendFromParagraph(paragraph, scope);

            if (scope.Point is not null &&
                content.Points.FirstOrDefault(p => Matches(p.Number?.Value, scope.Point)) is { } point)
                return DescendFromPoint(point, scope);

            if (scope.Letter is not null &&
                content.Letters.FirstOrDefault(l => Matches(l.Number?.Value, scope.Letter)) is { } letter)
                return DescendFromLetter(letter, scope);

            if (scope.Tiret is not null && ResolveTiret(content.Tirets, scope.Tiret) is { } tiret)
                return DescendFromTiret(tiret, scope);

            return null;
        }

        private static BaseEntity? DescendFromArticle(Article article, StructuralReference reference)
        {
            if (IsLeaf(reference, below: UnitDepth.Article))
                return Terminal(article, reference);

            var paragraph = reference.Paragraph is not null
                ? article.Paragraphs.FirstOrDefault(p => Matches(p.Number?.Value, reference.Paragraph))
                : article.Paragraphs.FirstOrDefault(p => p.IsImplicit) ?? article.Paragraphs.FirstOrDefault();

            return paragraph is null ? null : DescendFromParagraph(paragraph, reference);
        }

        private static BaseEntity? DescendFromParagraph(Paragraph paragraph, StructuralReference reference)
        {
            if (IsLeaf(reference, below: UnitDepth.Paragraph))
                return Terminal(paragraph, reference);

            if (reference.Point is null)
                return null;   // litera albo tiret bez punktu nie istnieje w hierarchii modelu

            var point = paragraph.Points.FirstOrDefault(p => Matches(p.Number?.Value, reference.Point));
            return point is null ? null : DescendFromPoint(point, reference);
        }

        private static BaseEntity? DescendFromPoint(Point point, StructuralReference reference)
        {
            if (IsLeaf(reference, below: UnitDepth.Point))
                return Terminal(point, reference);

            if (reference.Letter is null)
                return null;

            var letter = point.Letters.FirstOrDefault(l => Matches(l.Number?.Value, reference.Letter));
            return letter is null ? null : DescendFromLetter(letter, reference);
        }

        private static BaseEntity? DescendFromLetter(Letter letter, StructuralReference reference)
        {
            if (IsLeaf(reference, below: UnitDepth.Letter))
                return Terminal(letter, reference);

            var tiret = ResolveTiret(letter.Tirets, reference.Tiret!);
            return tiret is null ? null : DescendFromTiret(tiret, reference);
        }

        private static BaseEntity? DescendFromTiret(Tiret tiret, StructuralReference reference) =>
            Terminal(tiret, reference);

        private enum UnitDepth { Article, Paragraph, Point, Letter }

        private static bool IsLeaf(StructuralReference reference, UnitDepth below) => below switch
        {
            UnitDepth.Article => reference.Paragraph is null && reference.Point is null &&
                                 reference.Letter is null && reference.Tiret is null && reference.Sentence is null,
            UnitDepth.Paragraph => reference.Point is null && reference.Letter is null && reference.Tiret is null,
            UnitDepth.Point => reference.Letter is null && reference.Tiret is null,
            _ => reference.Tiret is null,
        };

        private static BaseEntity? Terminal(BaseEntity unit, StructuralReference reference) =>
            reference.Sentence is null ? unit : ResolveSentence(unit, reference);

        private static BaseEntity? ResolveSentence(BaseEntity owner, StructuralReference reference) =>
            owner is IHasTextSegments segments &&
            int.TryParse(reference.Sentence, out var order) &&
            segments.TextSegments.Any(s => s.Order == order)
                ? owner
                : null;

        /// <summary>
        /// Schodzi ścieżką kropkową tiretu („1.2" = tiret pierwszy, w nim podwójny tiret drugi).
        /// </summary>
        private static Tiret? ResolveTiret(IReadOnlyList<Tiret> roots, string path)
        {
            var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                return null;

            var current = roots.FirstOrDefault(t => Matches(t.Number?.Value, segments[0]));
            for (var i = 1; i < segments.Length && current is not null; i++)
                current = current.Tirets.FirstOrDefault(t => Matches(t.Number?.Value, segments[i]));

            return current;
        }

        internal static TextSegment? ResolveSegment(BaseEntity entity, StructuralReference reference) =>
            reference.Sentence is not null &&
            entity is IHasTextSegments segments &&
            int.TryParse(reference.Sentence, out var order)
                ? segments.TextSegments.FirstOrDefault(s => s.Order == order)
                : null;

        /// <summary>Porównanie oznaczeń jednostek.</summary>
        private static bool Matches(string? modelValue, string? referenceValue)
        {
            if (modelValue is null || referenceValue is null)
                return false;

            return string.Equals(
                modelValue.Trim().TrimEnd(')', '.'),
                referenceValue.Trim().TrimEnd(')', '.'),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
