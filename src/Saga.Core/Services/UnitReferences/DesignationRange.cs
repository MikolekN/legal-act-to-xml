using Saga.Core.Helpers;

namespace Saga.Core.Services.UnitReferences
{
    /// <summary>
    /// Rozwija przedziały oznaczeń jednostek („art. 85c–85j", „pkt 19-21", „lit. a–c").
    /// </summary>
    internal static class DesignationRange
    {
        /// <summary>Górny limit rozwinięcia. Zabezpiecza przed absurdalnym przedziałem.</summary>
        private const int MaxExpansion = 99;

        /// <summary>
        /// Rozwija przedział od-do. Obsługuje trzy kształty oznaczeń:
        /// czysto liczbowy („19"–„21"), liczbowo-literowy o tej samej części liczbowej („85c"–„85j")
        /// oraz czysto literowy („a"–„c" lub „aa"–„ac").
        /// Zwraca null dla przedziału niejednoznacznego (np. „5a"–„7b").
        /// </summary>
        internal static IReadOnlyList<string>? Expand(string? from, string? to)
        {
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
                return null;

            var a = from.Trim();
            var b = to.Trim();

            // Przedział liczebnikowy („tiret czwarte–szóste")
            if (PolishNumerals.TryParseOrdinal(a, out var ordFrom) &&
                PolishNumerals.TryParseOrdinal(b, out var ordTo))
                return ExpandNumeric(ordFrom, ordTo);

            var (numA, suffixA) = SplitNumberAndCharacter(a);
            var (numB, suffixB) = SplitNumberAndCharacter(b);

            // Przedział liczbowy (np. „19"–„21")
            if (numA.HasValue && numB.HasValue && suffixA.Length == 0 && suffixB.Length == 0)
                return ExpandNumeric(numA.Value, numB.Value);

            // Przedział liczbowo-literowy
            if (numA.HasValue && numB.HasValue && numA.Value == numB.Value &&
                suffixA.Length > 0 && suffixB.Length > 0)
            {
                var letters = ExpandAlphabetic(suffixA, suffixB);
                if (letters is null)
                    return null;

                var result = new List<string>(letters.Count);
                foreach (var letter in letters)
                    result.Add($"{numA.Value}{letter}");
                return result;
            }

            // Przedział literowy
            if (!numA.HasValue && !numB.HasValue)
                return ExpandAlphabetic(a, b);

            return null;
        }

        /// <summary>Rozbija oznaczenie na część liczbową i literową („85c" → (85, "c"), „a" → (null, "a")).</summary>
        private static (int? Number, string Suffix) SplitNumberAndCharacter(string designation)
        {
            int i = 0;
            while (i < designation.Length && char.IsDigit(designation[i])) i++;

            if (i == 0)
                return (null, designation);

            return int.TryParse(designation[..i], out var number)
                ? (number, designation[i..])
                : (null, designation);
        }

        private static IReadOnlyList<string>? ExpandNumeric(int from, int to)
        {
            if (to <= from || to - from + 1 > MaxExpansion)
                return null;

            var result = new List<string>(to - from + 1);
            for (var n = from; n <= to; n++)
                result.Add(n.ToString());
            return result;
        }

        /// <summary>
        /// Rozwija przedział literowy w porządku: a, b, …, z, aa, ab, … (bez polskich znaków).
        /// </summary>
        private static IReadOnlyList<string>? ExpandAlphabetic(string from, string to)
        {
            var a = from.ToLowerInvariant();
            var b = to.ToLowerInvariant();

            if (a.Length == 0 || b.Length == 0 || a.Length != b.Length || !IsBasicLatin(a) || !IsBasicLatin(b))
                return null;

            if (string.CompareOrdinal(a, b) >= 0)
                return null;

            var result = new List<string>();
            var current = a;
            while (true)
            {
                result.Add(current);
                if (string.Equals(current, b, StringComparison.Ordinal))
                    break;

                var next = Increment(current);
                if (next is null || next.Length != b.Length)
                    return null;
                current = next;
            }

            return result;
        }

        private static bool IsBasicLatin(string value)
        {
            foreach (var c in value)
                if (c is < 'a' or > 'z')
                    return false;
            return true;
        }

        /// <summary>Następnik oznaczenia literowego („a"→„b", „az"→„ba").</summary>
        private static string? Increment(string value)
        {
            var chars = value.ToCharArray();
            for (var i = chars.Length - 1; i >= 0; i--)
            {
                if (chars[i] < 'z')
                {
                    chars[i]++;
                    return new string(chars);
                }
                chars[i] = 'a';
            }
            return null;
        }
    }
}
