using System.Text.RegularExpressions;

namespace Saga.Core.Ingest
{
    /// <summary>
    /// Ręczne przejście do nowego wiersza w akapicie (<c>w:br</c>) w TXT. Czytnik DOCX oddaje je odstępem i akapit zostaje
    /// jeden; eksport tekstu pisze w tym miejscu koniec wiersza, więc dalszy ciąg zdania stawał się osobnym blokiem —
    /// komenda nowelizacji („W rozporządzeniu … w sprawie szcze-" / „gółowego … wprowadza się następujące zmiany:")
    /// przestawała być komendą, a akt zmieniany tracił adres.
    ///
    /// Wiersz dołącza do poprzedniego, gdy zaczyna się małą literą, nie jest oznaczeniem jednostki, a poprzedni:
    /// <list type="bullet">
    /// <item>kończy się dywizem po literze („szcze-", „nie-") — wyraz przerwany w pół, akapit tak się nie kończy;</item>
    /// <item>albo jest wierszem przepisu (artykuł, paragraf, ustęp) urwanym bez znaku końcowego.</item>
    /// </list>
    /// Wiersz za pozycją wyliczenia (punkt, litera, tiret) zostaje osobny: akapit małą literą za pozycją bez znaku
    /// końcowego to część wspólna bez myślnika (ADR-0046), a tekst sam tego nie rozstrzyga.
    /// </summary>
    internal static class PlainTextLineBreaks
    {
        private static readonly Regex BrokenWord = new(@"\p{L}-\s*$", RegexOptions.Compiled);

        private static readonly Regex Provision = new(@"^„?(?:Art\.\s*\S+|§\s*\d+\S*\.|\d+[a-z]*\.\s)", RegexOptions.Compiled);

        private static readonly Regex EnumerationItem = new(@"^„?(?:\d+[a-z]*\)|[a-z]{1,3}\)|[–‒-])", RegexOptions.Compiled);

        private static readonly Regex OpenEnd = new(@"[\p{L}\d]\s*$", RegexOptions.Compiled);

        private static readonly Regex Designation = new(@"^(?:\d+[a-z]*[.)]|[a-z]{1,3}\)|[–‒-]|Art\.|§)", RegexOptions.Compiled);

        internal static IReadOnlyList<DocumentBlock> Join(IReadOnlyList<DocumentBlock> blocks)
        {
            var result = new List<DocumentBlock>(blocks.Count);
            foreach (var block in blocks)
            {
                if (result.Count > 0 && block.TableCell is null && result[^1].TableCell is null && Continues(result[^1].Text, block.Text))
                {
                    result[^1] = result[^1] with { Text = result[^1].Text.TrimEnd() + " " + block.Text.TrimStart() };
                    continue;
                }

                result.Add(block);
            }

            return result;
        }

        private static bool Continues(string previous, string line)
        {
            var next = line.TrimStart();
            if (next.Length == 0 || !char.IsLower(next[0]) || Designation.IsMatch(next))
                return false;

            return BrokenWord.IsMatch(previous)
                || (Provision.IsMatch(previous) && !EnumerationItem.IsMatch(previous) && OpenEnd.IsMatch(previous));
        }
    }
}
