using System.Text.RegularExpressions;

namespace Saga.Core.Ingest
{
    /// <summary>
    /// Winieta dziennika na pierwszej stronie aktu ogłoszonego („DZIENNIK USTAW / RZECZYPOSPOLITEJ POLSKIEJ /
    /// Warszawa, dnia 12 stycznia 2026 r. / Poz. 26") w wejściu bez części nagłówkowych — PDF i TXT. DOCX trzyma ją
    /// w nagłówku strony, który czytnik oznacza rolą <see cref="BlockRole.JournalHeader"/>; w PDF i TXT to zwykłe
    /// wiersze na początku treści. Bez tej roli przepadają dane ogłoszenia (dziennik, rocznik, pozycja, data
    /// ogłoszenia — a z nią daty wejścia w życie i prefiks eId), a wiersze winiety stają się treścią przed tytułem.
    ///
    /// Rozpoznanie jest wąskie (ADR-0088): tylko bloki na samym początku dokumentu, które razem dają winietę — nazwę
    /// dziennika, a po niej najwyżej drugi wiersz nazwy, miejsce i datę wydania numeru oraz pozycję. Podział winiety
    /// na bloki zależy od składu (PDF łamie duże litery nazwy, TXT bywa sklejony w jeden wiersz), więc liczy się
    /// tekst bloków złączony, a nie każdy blok z osobna. Pierwszy blok, który winiety nie przedłuża, ją kończy.
    /// </summary>
    internal static class JournalMasthead
    {
        /// <summary>Najwięcej bloków winiety — więcej znaczyłoby, że to już nie winieta.</summary>
        private const int MaxBlocks = 6;

        private static readonly Regex Masthead = new(
            @"^(?:DZIENNIK\s+USTAW|MONITOR\s+POLSKI)" +
            @"(?:\s+(?:DZIENNIK\s+URZĘDOWY\s+)?RZECZYPOSPOLITEJ\s+POLSKIEJ)?" +
            @"(?:\s+Warszawa,\s+dnia\s+\d{1,2}\s+\p{L}+\s+\d{4}\s*r\.?)?" +
            @"(?:\s+Poz\.\s*\d{1,6}\.?)?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static string Normalize(string text) => Whitespace.Replace(text, " ").Trim();

        private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

        /// <summary>Bloki z winietą jako jednym blokiem w roli <see cref="BlockRole.JournalHeader"/>; bez winiety — te same bloki.</summary>
        internal static IReadOnlyList<DocumentBlock> Mark(IReadOnlyList<DocumentBlock> blocks)
        {
            var length = 0;
            var joined = string.Empty;
            for (var i = 0; i < Math.Min(blocks.Count, MaxBlocks); i++)
            {
                joined = (joined + " " + blocks[i].Text).Trim();
                if (Masthead.IsMatch(joined))
                    length = i + 1;
                else if (length > 0 || i > 0)
                    break;
            }

            // Winieta wraca jednym blokiem: skład łamie nazwę dziennika na kilka wierszy („MONITOR" / „POLSKI"),
            // a nagłówek publikatora szuka nazwy, daty i pozycji w obrębie wiersza.
            return length == 0
                ? blocks
                : [blocks[0] with { Text = Normalize(JoinedUpTo(length)), Role = BlockRole.JournalHeader }, .. blocks.Skip(length)];

            string JoinedUpTo(int count) => string.Join(" ", blocks.Take(count).Select(block => block.Text.Trim()));
        }
    }
}
