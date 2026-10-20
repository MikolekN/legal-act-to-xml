using System.Text.RegularExpressions;
using Saga.Model;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Komenda, której przedmiotem jest odnośnik aktu („odnośnik nr 1 do tytułu ustawy otrzymuje brzmienie:",
    /// „do tytułu ustawy dodaje się odnośnik nr 2 w brzmieniu:") — treść cytowana jest wtedy odnośnikiem, nie
    /// jednostkami, choć jej pierwszy akapit zaczyna się jak punkt („1) Niniejsza ustawa …") (ADR-0059).
    /// Odnośnik w załączniku ma adres pod załącznikiem i tu nie pasuje.
    /// </summary>
    internal static class QuotedFootnoteReader
    {
        private static readonly Regex Replaced = new(
            $@"(?<![\p{{L}}])odnośnik\s+nr\s+(?<no>\d+[a-z]*)\b[^:{QuotationMarks.Any}]*?otrzymuje\s+brzmienie\s*:\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex Added = new(
            $@"dodaje\s+się\s+(?:przecinek\s+(?:i|oraz)\s+)?odnośnik\s+nr\s+(?<no>\d+[a-z]*)\b[^:{QuotationMarks.Any}]*?w\s+brzmieniu\s*:\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex InAnAnnex = new(@"załącznik", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Znacznik odnośnika na początku jego treści: „1)", także zapisany indeksem górnym („[1)]"). Cudzysłów
        /// otwierający cytat bywa złożony indeksem górnym razem ze znacznikiem („[„2)] Niniejsza ustawa …"),
        /// a numer w indeksie górnym — nieobecny w tekście, gdy źródło wstawia go polem („[)] Niniejsza ustawa …").
        /// </summary>
        private static readonly Regex LeadingMarker = new(
            $@"^\s*(?:\[\s*(?<quote>[{QuotationMarks.OpeningOrStraight}])?\s*(?:\d+[a-z]*)?\)\s*\]|(?<quote>[{QuotationMarks.OpeningOrStraight}])?\d+[a-z]*\))\s*",
            RegexOptions.Compiled);

        /// <summary>Odnośnik wskazany komendą i to, czy jest dodawany.</summary>
        internal sealed record Command(string Number, bool Adds);

        internal static Command? Read(string? commandText)
        {
            if (string.IsNullOrWhiteSpace(commandText))
                return null;

            var text = commandText.TrimEnd();
            var match = Added.Match(text);
            var adds = match.Success;
            if (!adds)
                match = Replaced.Match(text);

            return match.Success && !InAnAnnex.IsMatch(text[..match.Index])
                ? new Command(match.Groups["no"].Value, adds)
                : null;
        }

        /// <summary>
        /// Treść cytowana jako odnośnik: akapity bez własnego znacznika na początku. <paramref name="opensWithQuote"/>
        /// mówi, czy przy znaczniku stał cudzysłów otwierający cytat.
        /// </summary>
        internal static Footnote Build(Command command, IReadOnlyList<CollectedAmendmentParagraph> paragraphs, out bool opensWithQuote)
        {
            var texts = paragraphs
                .Where(paragraph => paragraph.Table is null && !string.IsNullOrWhiteSpace(paragraph.Text))
                .Select(paragraph => paragraph.Text.Trim())
                .ToList();

            opensWithQuote = false;
            if (texts.Count > 0 && LeadingMarker.Match(texts[0]) is { Success: true } marker)
            {
                opensWithQuote = marker.Groups["quote"].Success;
                texts[0] = texts[0][marker.Length..];
            }

            return new Footnote { Symbol = command.Number, Paragraphs = texts };
        }

        /// <summary>
        /// Cel komendy: odnośnik zmieniany albo — przy dodaniu — poprzedzający dodawany (odnośniki numeruje się
        /// kolejno: „nr 2" staje po „nr 1"). Null, gdy dodawany jest pierwszy.
        /// </summary>
        internal static StructuralAmendmentReference? Target(Command command, string? rawText)
        {
            var number = command.Number;
            if (command.Adds)
            {
                // Pierwszy odnośnik nie ma poprzednika; dodany do tytułu aktu („do tytułu ustawy dodaje się odnośnik nr 1")
                // wskazuje tytuł — instrukcją ręczną z adresem aktu (ADR-0101).
                if (!int.TryParse(number, out var value) || value < 2)
                    return LegalReferenceService.ActTitleTarget(rawText);
                number = (value - 1).ToString();
            }

            return new StructuralAmendmentReference { Structure = new StructuralReference { Footnote = number }, RawText = rawText };
        }
    }
}
