using System.Text;
using System.Text.RegularExpressions;
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
            // Odnośnik jako miejsce zmiany („w odnośniku nr 1 do tytułu ustawy:", „w odnośniku nr 2 wyrazy …"): jego akapity
            // i punkty nie mają eId, więc celem jest sam odnośnik — także dla przepisów pod nim (ustalenie z 2026-10-19
            // do pytania 24, ADR-0106).
            if (context?.Footnote is { } inherited)
                return [new StructuralAmendmentReference { Structure = new StructuralReference { Footnote = inherited }, RawText = contentText }];
            if (contentText is not null && FootnotePlace.Match(ManualTargets.WithoutActFormula(contentText)) is { Success: true } place)
                return [new StructuralAmendmentReference { Structure = new StructuralReference { Footnote = place.Groups["number"].Value }, RawText = contentText }];

            var targets = new List<StructuralAmendmentReference>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            // Zdanie nazwane z numerem zostaje w adresie: uchylane (ADR-0070), zastępowane i zmieniane w tekście
            // („w ust. 3 w zdaniu pierwszym wyrazy …") — cel w całej jednostce zmieniłby też zdania, których komenda nie
            // wymienia (ADR-0102). Zdanie bez jednostki („w opisie koszuli skreśla się zdanie szóste") niczego nie adresuje.
            foreach (var address in UnitAddressReader.Read(contentText, context, sectionSignIsBasicUnit))
            {
                var keepsSentence = NamesAUnit(address.Start);
                var start = keepsSentence ? address.Start : WithoutSentence(address.Start);
                var end = address.End is not { } last ? null : keepsSentence ? last : WithoutSentence(last);
                if (end is not null && Identity(end) == Identity(start))
                    end = null;

                var target = new StructuralAmendmentReference { Structure = start, RangeEnd = end, RawText = contentText };
                if (seen.Add($"{Identity(start)}–{(end is null ? "" : Identity(end))}"))
                    targets.Add(target);
            }

            // Tytuł aktu zmienianego jako miejsce komendy — bez jednostki, którą adresuje eId (ADR-0101). Sam „tytuł"
            // („1) tytuł otrzymuje brzmienie:" w wyliczeniu zmian aktu) jest tytułem aktu tylko wtedy, gdy przepisy nadrzędne
            // nie wskazują jednostki — pod „w dziale II:" byłby tytułem działu.
            if (targets.Count == 0 && ActTitleTarget(contentText, bare: context is null) is { } title)
                targets.Add(title);

            // Tytuł aktu wśród kilku miejsc zmiany („wyraz „X” użyty w tytule ustawy, w art. 1 i w art. 5 …") jest celem
            // obok jednostek — przed nimi, w kolejności z przepisu.
            else if (targets.Count > 0 && contentText is not null
                     && TitleAmongPlaces.IsMatch(UnitAddressReader.WithoutQuotedText(contentText)))
                targets.Insert(0, ActTitleReference(contentText));

            return targets;
        }

        /// <summary>Odnośnik aktu zmienianego jako miejsce zmiany: „w odnośniku nr 1 (do tytułu ustawy)".</summary>
        private static readonly Regex FootnotePlace = new(
            @"^w\s+odnośniku\s+nr\s+(?<number>\d+[a-z]*)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Cel „tytuł aktu zmienianego", gdy przepis zaczyna się od niego; null, gdy go nie nazywa (ADR-0101).
        /// <paramref name="bare"/>: wystarczy sam „tytuł" bez nazwy aktu — gdy żaden przepis nadrzędny nie wskazuje jednostki.
        /// </summary>
        internal static StructuralAmendmentReference? ActTitleTarget(string? contentText, bool bare = false)
        {
            if (contentText is null)
                return null;

            var text = ManualTargets.WithoutActFormula(contentText);
            return ActTitlePlace.IsMatch(text) || (bare && BareActTitlePlace.IsMatch(text)) ? ActTitleReference(contentText) : null;
        }

        /// <summary>Cel „tytuł aktu zmienianego" — adresem jest sam akt, instrukcja jest ręczna (ADR-0101).</summary>
        internal static StructuralAmendmentReference ActTitleReference(string? rawText) =>
            new() { Structure = new StructuralReference { ActTitle = true }, RawText = rawText };

        /// <summary>Tytuł aktu jako jedno z miejsc zmiany: „… użyty w tytule ustawy, w art. 1 …".</summary>
        private static readonly Regex TitleAmongPlaces = new(
            @"(?<![\p{L}])w\s+tytule\s+(?:ustawy|rozporządzenia|uchwały|zarządzenia|kodeksu)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>Sam „tytuł" jako przedmiot albo miejsce komendy: „tytuł otrzymuje brzmienie:", „w tytule wyrazy …".</summary>
        private static readonly Regex BareActTitlePlace = new(
            @"^(?:w\s+)?tytu(?:ł|le)\s+(?:otrzymuje\s+brzmienie|(?:po\s+)?wyraz|skreśla\s+się|dodaje\s+się)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Przepis zaczyna się tytułem aktu: „tytuł ustawy otrzymuje brzmienie:", „w tytule ustawy ogólne określenie
        /// przedmiotu …", „do tytułu ustawy dodaje się odnośnik …", „dodaje się odnośnik nr 1 do tytułu ustawy". Odnośnik
        /// do tytułu jako miejsce („w odnośniku nr 1 do tytułu ustawy") miejscem w tytule nie jest.
        /// </summary>
        private static readonly Regex ActTitlePlace = new(
            @"^(?:dodaje\s+się\s+odnośnik(?:\s+nr\s+\d+)?\s+)?(?:(?:w|do)\s+)?tytu(?:ł|łu|le)\s+(?:ustawy|rozporządzenia|uchwały|zarządzenia|obwieszczenia|kodeksu)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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
