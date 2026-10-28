using System.Text.RegularExpressions;
using Saga.Core.Services.Classify.Document;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Wielkość liter wierszy strefy tytułowej w eksporcie (ADR-0130). Styl rodzaju i organu składa je wersalikami,
    /// więc litery wpisane w źródle („ustawa”, „USTAWA”, „RozporządzeniE”) nic nie znaczą: rodzaj wychodzi jak w
    /// przykładach autorów schematu i w ISAP („Ustawa”, „Zarządzenie nr 272”), a organ — każde słowo wielką literą
    /// poza spójnikami i przyimkami („Ministra Spraw Wewnętrznych i Administracji”).
    /// </summary>
    internal static class ActHeadingText
    {
        private static readonly Regex Word = new(@"\p{L}+", RegexOptions.Compiled);
        private static readonly Regex RomanNumeral = new(@"^[IVXLCDM]+$", RegexOptions.Compiled);
        private const string Vowels = "aąeęioóuy";

        // Słowa, które w nazwie organu zostają małą literą.
        private static readonly HashSet<string> LowercaseInOrgan = new(StringComparer.OrdinalIgnoreCase)
        {
            "i", "oraz", "a", "w", "we", "z", "ze", "do", "na", "o", "od", "dla", "przy", "po", "nr",
        };

        /// <summary>
        /// Rodzaj aktu („USTAWA” → „Ustawa”, „UCHWAŁA NR XII/123/2025” → „Uchwała nr XII/123/2025”): małe litery
        /// i wielka pierwsza; liczba rzymska oznaczenia zostaje.
        /// </summary>
        internal static string Kind(string kind) =>
            CapitalizeFirst(Word.Replace(kind.Trim(), word =>
                RomanNumeral.IsMatch(word.Value) && word.Index > 0 ? word.Value : word.Value.ToLowerInvariant()));

        /// <summary>
        /// Organ wydający („MINISTRA FINANSÓW”, „MINISTRA FINANSÓW i gospodarki” → „Ministra Finansów i Gospodarki”):
        /// każde słowo wielką literą poza spójnikami, przyimkami i „spraw” po „do” („Komitetu do spraw Pożytku
        /// Publicznego”), jak w tytułach ISAP. Litery wpisane pod wersalikami stylu nic nie znaczą, także mieszane.
        /// </summary>
        internal static string Organ(string organ)
        {
            var first = true;
            var previous = string.Empty;
            return Word.Replace(organ.Trim(), word =>
            {
                var lower = word.Value.ToLowerInvariant();
                var keepLower = !first && (LowercaseInOrgan.Contains(lower) || (lower == "spraw" && previous == "do"));
                first = false;
                previous = lower;
                // Słowo wersalikami bez samogłoski to skrót („NBP”, „RP”) — zostaje; „WSI” jest słowem.
                if (word.Length >= 2 && word.Value.All(char.IsUpper) && !lower.Any(Vowels.Contains))
                    return word.Value;
                return keepLower ? lower : CapitalizeFirst(lower);
            });
        }

        /// <summary>
        /// Wiersz rodzaju, w którym może stać też organ („ROZPORZĄDZENIE MINISTRA FINANSÓW” → „Rozporządzenie Ministra
        /// Finansów”).
        /// </summary>
        internal static string Heading(string kindLine)
        {
            var kind = KindOf(kindLine);
            var organ = kindLine.Trim()[kind.Length..].Trim();
            return organ.Length == 0 ? Kind(kind) : Kind(kind) + " " + Organ(organ);
        }

        /// <summary>
        /// Rodzaj aktu bez organu stojącego w tym samym wierszu („ROZPORZĄDZENIE MINISTRA FINANSÓW" →
        /// „ROZPORZĄDZENIE") — organ wychodzi wtedy osobno, w <c>autor-aktu</c>.
        /// </summary>
        internal static string KindWithoutIssuer(string kindLine, string issuerLine) =>
            issuerLine.Length > 0 && ZtpPatterns.KindWithIssuerPattern.Match(kindLine) is { Success: true } sameLine
                ? sameLine.Groups["kind"].Value
                : kindLine;

        /// <summary>
        /// Tytuł aktu w metadanych („USTAWA z dnia …” → „Ustawa z dnia …”, „ROZPORZĄDZENIE MINISTRA FINANSÓW z dnia …”
        /// → „Rozporządzenie Ministra Finansów z dnia …”): część przed datą to rodzaj i organ, reszta zostaje.
        /// </summary>
        internal static string Title(string title, string kindLine, string dateLine)
        {
            var dateAt = dateLine.Length > 0 ? title.IndexOf(dateLine, StringComparison.Ordinal) : -1;
            if (kindLine.Length == 0 || dateAt <= 0 || !title.StartsWith(kindLine.Trim(), StringComparison.Ordinal))
                return CapitalizeFirst(title);

            var kind = KindOf(kindLine);
            var organ = title[kind.Length..dateAt].Trim();
            var heading = organ.Length == 0 ? Kind(kind) : Kind(kind) + " " + Organ(organ);
            return heading + " " + title[dateAt..];
        }

        // Słowo rodzaju z oznaczeniem („ZARZĄDZENIE NR 53”), bez organu z tego samego wiersza.
        private static string KindOf(string kindLine) =>
            ZtpPatterns.KindWithIssuerPattern.Match(kindLine.Trim()) is { Success: true } sameLine
                ? sameLine.Groups["kind"].Value
                : kindLine.Trim();

        private static string CapitalizeFirst(string text) =>
            text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
    }
}
