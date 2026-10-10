using System.Text.RegularExpressions;

namespace Saga.Core.Services
{
    /// <summary>
    /// Znacznik odnośnika w kanale tekstowym: indeks górny zakończony nawiasem — „[9)]", „[2), 3)]".
    /// Stoi w treści w miejscu odwołania; indeks górny oznaczenia („125[1]") nawiasu nie ma.
    /// </summary>
    internal static class FootnoteMarkers
    {
        /// <summary>Wzorzec znacznika do wstawiania w inne wyrażenia.</summary>
        internal const string Pattern = @"\[[^\]]*\)\]";

        // „[1 15)]": indeks górny oznaczenia i odnośnik sąsiadujące w DOCX zlewają się w jeden nawias.
        private static readonly Regex Marker = new(
            @"\[(?:(?<superscript>\d+[a-z]*)\s+)?[^\]]*\)\]", RegexOptions.Compiled);

        /// <summary>
        /// Tekst ze znacznikami zastąpionymi odstępami tej samej długości — pozycje w tekście zostają ważne.
        /// Indeks górny oznaczenia zlany ze znacznikiem („4[1 15)]") zostaje jako „4[1]".
        /// </summary>
        internal static string Blank(string text) => Marker.Replace(text, match =>
        {
            var superscript = match.Groups["superscript"];
            var kept = superscript.Success ? $"[{superscript.Value}]" : string.Empty;
            return kept + new string(' ', match.Length - kept.Length);
        });
    }
}
