using System.Text;
using Saga.Core.Services.Parsing;
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

            // Zdanie zostaje w adresie tylko wtedy, gdy samo jest uchylane („skreśla się zdanie drugie"): instrukcja
            // uchylenia nie niesie treści, więc bez niego wskazywałaby całą jednostkę (ADR-0070). Zmiana brzmienia
            // zdania niesie je w treści cytowanej, a zmiana w tekście dotyczy jednostki. Zdanie bez jednostki
            // („w opisie koszuli skreśla się zdanie szóste") niczego nie adresuje.
            var sentenceIsRepealed = contentText is not null && AmendmentCommandParser.RepealsSentence(contentText);

            foreach (var address in UnitAddressReader.Read(contentText, context, sectionSignIsBasicUnit))
            {
                var keepsSentence = sentenceIsRepealed && NamesAUnit(address.Start);
                var start = keepsSentence ? address.Start : WithoutSentence(address.Start);
                var end = address.End is not { } last ? null : keepsSentence ? last : WithoutSentence(last);
                if (end is not null && Identity(end) == Identity(start))
                    end = null;

                var target = new StructuralAmendmentReference { Structure = start, RangeEnd = end, RawText = contentText };
                if (seen.Add($"{Identity(start)}–{(end is null ? "" : Identity(end))}"))
                    targets.Add(target);
            }

            return targets;
        }

        // Tożsamość celu po oznaczeniach z tekstu, nie po napisie eId — postać eId może się zmieniać
        // (ADR-0018, ADR-0019), a „art. 5 i 5[1]" to zawsze dwa cele.
        private static string Identity(StructuralReference reference) =>
            string.Join('|', reference.Systematizing, reference.Annex, reference.Article, reference.Paragraph, reference.ParagraphIsCodeParagraph,
                reference.Point, reference.Letter, reference.Tiret, reference.Sentence);

        private static bool NamesAUnit(StructuralReference reference) =>
            (reference.Systematizing ?? reference.Annex ?? reference.Article ?? reference.Paragraph ?? reference.Point ?? reference.Letter
                ?? reference.Tiret) is not null;

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
