using System.Text;
using Saga.Core.Services.UnitReferences;
using Saga.Model;

namespace Saga.Core.Services
{
    /// <summary>
    /// Serwis do zarządzania odniesieniami do aktów prawnych.
    /// Odpowiada za aktualizację referencji na podstawie treści encji.
    /// </summary>
    public class LegalReferenceService
    {
        /// <summary>Cele nowelizacji wskazane w treści encji („w art. 5 § 2 pkt 3 …").</summary>
        public IReadOnlyList<StructuralAmendmentReference> ReadAmendmentTargets(
            StructuralReference? context, string? contentText, bool sectionSignIsBasicUnit = false)
        {
            var targets = new List<StructuralAmendmentReference>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var address in UnitAddressReader.Read(contentText, context, sectionSignIsBasicUnit))
            {
                var start = WithoutSentence(address.Start);
                var end = address.End is { } last ? WithoutSentence(last) : null;
                if (end is not null && end.ToString() == start.ToString())
                    end = null;

                var target = new StructuralAmendmentReference { Structure = start, RangeEnd = end, RawText = contentText };
                if (seen.Add(target.ToString()))
                    targets.Add(target);
            }

            return targets;
        }

        private static StructuralReference WithoutSentence(StructuralReference reference)
        {
            var copy = reference.Clone();
            copy.Sentence = null;
            return copy;
        }

        /// <summary>
        /// Tworzy kontekst dla encji poprzez łączenie tekstów encji nadrzędnych.
        /// </summary>
        public string GetContext(BaseEntity entity)
        {
            var contextBuilder = new StringBuilder();
            var currentEntity = entity;

            while (currentEntity != null && !(currentEntity is Saga.Model.EditorialUnits.Article))
            {
                if (!string.IsNullOrEmpty(currentEntity.ContentText))
                {
                    contextBuilder.Insert(0, currentEntity.ContentText + " ");
                }
                currentEntity = currentEntity.Parent;
            }

            return contextBuilder.ToString().Trim();
        }
    }
}
