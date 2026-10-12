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

        /// <summary>
        /// Znacznik w każdej postaci spotykanej w aktach: nawias z co najmniej jednym numerem odnośnika —
        /// także z interpunkcją wciągniętą do indeksu górnego („[1),]", „[13).]") i z kilkoma numerami.
        /// </summary>
        internal const string ReferencePattern = @"\[[^\[\]]*\d+\)[^\[\]]*\]";

        internal static readonly Regex Reference = new(ReferencePattern, RegexOptions.Compiled);

        /// <summary>Znaczniki stojące jeden za drugim od wskazanego miejsca tekstu („.[7)][8)] ").</summary>
        internal static readonly Regex AttachedHere = new(@"\G(?:" + ReferencePattern + ")+", RegexOptions.Compiled);

        /// <summary>
        /// Zawartość nawiasu będąca samym znacznikiem: numery odnośników z nawiasami, rozdzielone i zakończone
        /// najwyżej interpunkcją („1)", „1), 2)", „13)."). Nawias kwadratowy z innym tekstem („[D_2022 x (A + B)/n)]"
        /// — wzór wpisany w treść) znacznikiem nie jest, choć pasuje do <see cref="Reference"/>.
        /// </summary>
        internal static readonly Regex Bare = new(
            @"^\s*\d+[a-z]?\)(?:\s*[,;]?\s*\d+[a-z]?\))*\s*[.,;]?\s*$", RegexOptions.Compiled);

        // „[1 15)]": indeks górny oznaczenia i odnośnik sąsiadujące w DOCX zlewają się w jeden nawias.
        private static readonly Regex Marker = new(
            @"\[(?:(?<superscript>\d+[a-z]*)\s+)?[^\[\]]*\d+\)[^\[\]]*\]", RegexOptions.Compiled);

        /// <summary>
        /// Tekst ze znacznikami zastąpionymi odstępami tej samej długości — pozycje w tekście zostają ważne.
        /// Indeks górny oznaczenia zlany ze znacznikiem („4[1 15)]") zostaje jako „4[1]".
        /// </summary>
        /// <summary>
        /// Tekst bez znaczników odnośników — do rozpoznawania wierszy, przy których odnośnik stoi na końcu
        /// („ROZPORZĄDZENIE MINISTRA ZDROWIA[1)]": odnośnik wyjaśnia właściwość ministra).
        /// </summary>
        internal static string Remove(string text) => Reference.Replace(text, string.Empty).TrimEnd();

        internal static string Blank(string text) => Marker.Replace(text, match =>
        {
            var superscript = match.Groups["superscript"];
            var kept = superscript.Success ? $"[{superscript.Value}]" : string.Empty;
            return kept + new string(' ', match.Length - kept.Length);
        });
    }
}
