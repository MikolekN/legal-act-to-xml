using Saga.Model;
using Saga.Model.EditorialUnits;
using Saga.Model.SystematizingUnits;

namespace Saga.Core.Services.EntryIntoForce
{
    /// <summary>Przejście po drzewie dokumentu w kolejności dzieci jednostek.</summary>
    internal static class EditorialUnitWalker
    {
        /// <summary>Bezpośrednie dzieci jednostki.</summary>
        internal static IEnumerable<BaseEntity> Children(BaseEntity unit, bool includeAmendmentContent)
        {
            foreach (var child in StructuralChildren(unit))
                yield return child;

            if (!includeAmendmentContent)
                yield break;

            if (unit is not IHasAmendments { Amendment.Content: { } content })
                yield break;

            foreach (var systematizing in content.SystematizingUnits) yield return systematizing;
            foreach (var article in content.Articles) yield return article;
            foreach (var paragraph in content.Paragraphs) yield return paragraph;
            foreach (var point in content.Points) yield return point;
            foreach (var letter in content.Letters) yield return letter;
            foreach (var tiret in content.Tirets) yield return tiret;
            foreach (var commonPart in content.CommonParts) yield return commonPart;
        }

        /// <summary>Jednostka i całe jej poddrzewo.</summary>
        internal static IEnumerable<BaseEntity> SelfAndDescendants(BaseEntity root, bool includeAmendmentContent)
        {
            yield return root;

            foreach (var child in Children(root, includeAmendmentContent))
                foreach (var descendant in SelfAndDescendants(child, includeAmendmentContent))
                    yield return descendant;
        }

        private static IEnumerable<BaseEntity> StructuralChildren(BaseEntity unit) => unit switch
        {
            ISystematizingUnit => SystematizingHierarchy.Children(unit),
            Article article => article.Paragraphs,

            Paragraph paragraph => Concat(paragraph.CommonParts, paragraph.Points),
            Point point => Concat(point.CommonParts, point.Letters),
            Letter letter => Concat(letter.CommonParts, letter.Tirets),

            Tiret tiret => tiret.Tirets,
            _ => [],
        };

        private static IEnumerable<BaseEntity> Concat(
            IEnumerable<BaseEntity> commonParts, IEnumerable<BaseEntity> enumerated)
        {
            foreach (var commonPart in commonParts) yield return commonPart;
            foreach (var item in enumerated) yield return item;
        }
    }
}
