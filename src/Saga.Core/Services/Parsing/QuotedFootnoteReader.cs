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
            $@"^\s*(?:\[\s*(?<quote>[{QuotationMarks.OpeningOrStraight}])?\s*(?<no>\d+[a-z]*)?\)\s*\]|(?<quote>[{QuotationMarks.OpeningOrStraight}])?(?<no>\d+[a-z]*)\))\s*",
            RegexOptions.Compiled);

        /// <summary>Odnośnik wymieniony w treści jednostki obwieszczenia („nie obejmuje odnośnika nr 2 oraz § 2 …").</summary>
        private static readonly Regex Named = new(
            @"(?<![\p{L}])odnośnik(?:a|u|iem)?\s+nr\s+(?<no>\d+[a-z]*)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Akapit zaczynający się oznaczeniem artykułu albo paragrafu — przepis, nie dalsza treść odnośnika.</summary>
        private static readonly Regex Provision = new(@"^\s*(?:Art\.|§)\s*\d", RegexOptions.Compiled);

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
        /// Przytoczony odnośnik („Podany … tekst jednolity nie obejmuje odnośnika nr 2 oraz § 2 …, które stanowią:
        /// „²⁾ Niniejsze rozporządzenie …""): treść jednostki wymienia odnośnik, a przytoczenie zaczyna się jego
        /// znacznikiem. Null, gdy znacznik nie pasuje do żadnego wymienionego odnośnika albo w przytoczeniu stoi przepis.
        /// <paramref name="opensWithQuote"/> mówi, czy przy znaczniku stał cudzysłów otwierający przytoczenie.
        /// </summary>
        internal static Footnote? Cited(string ownerText, IReadOnlyList<CollectedAmendmentParagraph> paragraphs, out bool opensWithQuote)
        {
            opensWithQuote = false;
            var named = Named.Matches(ownerText).Select(match => match.Groups["no"].Value).ToHashSet();
            if (named.Count == 0 || paragraphs.Count == 0 || paragraphs.Any(paragraph => paragraph.Table is not null)
                || LeadingMarker.Match(paragraphs[0].Text) is not { Success: true } marker
                || paragraphs.Skip(1).Any(paragraph => Provision.IsMatch(paragraph.Text)))
                return null;

            var number = marker.Groups["no"].Success ? marker.Groups["no"].Value : named.Count == 1 ? named.Single() : null;
            return number is not null && named.Contains(number)
                ? Build(new Command(number, Adds: false), paragraphs, out opensWithQuote)
                : null;
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
