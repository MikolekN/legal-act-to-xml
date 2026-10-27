#nullable enable

using Saga.Model;

namespace Saga.Core.Ingest
{
    /// <summary>
    /// Przeniesienie zakresów formatowania (<see cref="InlineSpan"/>) z tekstu na jego przepisanie — numer odnośnika
    /// zamieniony na oznaczenie z aktu (<c>DocxFootnotes.WithBareReferencesAsPrinted</c>). Wspólny początek i koniec obu
    /// tekstów odpowiadają sobie wprost; środek wyrównuje najdłuższy wspólny podciąg. Znak bez odpowiednika nie niesie
    /// formatowania; zakres, z którego nie zostaje nic, odpada.
    /// </summary>
    internal static class SpanAlignment
    {
        internal static IReadOnlyList<InlineSpan> Remap(string from, string to, IReadOnlyList<InlineSpan> spans)
        {
            if (spans.Count == 0 || from == to)
                return spans;

            var map = Map(from, to);
            var result = new List<InlineSpan>(spans.Count);
            foreach (var span in spans)
            {
                int first = -1, last = -1;
                for (var i = span.Start; i < span.End && i < map.Length; i++)
                {
                    if (map[i] < 0)
                        continue;
                    if (first < 0)
                        first = map[i];
                    last = map[i];
                }

                if (first >= 0)
                    result.Add(span with { Start = first, Length = last + 1 - first });
            }

            return result;
        }

        /// <summary>Pozycja każdego znaku <paramref name="from"/> w <paramref name="to"/>; -1 — znak bez odpowiednika.</summary>
        internal static int[] Map(string from, string to)
        {
            var map = new int[from.Length];
            Array.Fill(map, -1);

            var prefix = 0;
            while (prefix < from.Length && prefix < to.Length && Same(from[prefix], to[prefix]))
            {
                map[prefix] = prefix;
                prefix++;
            }

            var suffix = 0;
            while (suffix < from.Length - prefix && suffix < to.Length - prefix
                && Same(from[from.Length - 1 - suffix], to[to.Length - 1 - suffix]))
            {
                map[from.Length - 1 - suffix] = to.Length - 1 - suffix;
                suffix++;
            }

            var n = from.Length - prefix - suffix;
            var m = to.Length - prefix - suffix;
            if (n == 0 || m == 0)
                return map;
            if ((long)n * m > MaxMiddleCells)
            {
                Greedy(from, to, prefix, n, m, map);
                return map;
            }

            // Najdłuższy wspólny podciąg środka (programowanie dynamiczne od końca), potem odczyt par od początku.
            var lengths = new int[n + 1, m + 1];
            for (var i = n - 1; i >= 0; i--)
                for (var j = m - 1; j >= 0; j--)
                    lengths[i, j] = Same(from[prefix + i], to[prefix + j])
                        ? lengths[i + 1, j + 1] + 1
                        : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);

            for (int i = 0, j = 0; i < n && j < m;)
            {
                if (Same(from[prefix + i], to[prefix + j]))
                {
                    map[prefix + i] = prefix + j;
                    i++;
                    j++;
                }
                else if (lengths[i + 1, j] >= lengths[i, j + 1])
                {
                    i++;
                }
                else
                {
                    j++;
                }
            }

            return map;
        }

        /// <summary>
        /// Długi środek prawie równych tekstów (znaczniki odnośników zmienione w kilku miejscach długiego bloku PDF):
        /// znaki równe odpowiadają sobie wprost, a po różnicy szuka się najkrótszego przeskoku w jednym z tekstów, za którym
        /// zgadza się kilka kolejnych znaków.
        /// </summary>
        private static void Greedy(string from, string to, int offset, int n, int m, int[] map)
        {
            int i = 0, j = 0;
            while (i < n && j < m)
            {
                if (Same(from[offset + i], to[offset + j]))
                {
                    map[offset + i] = offset + j;
                    i++;
                    j++;
                    continue;
                }

                var skipped = false;
                for (var k = 1; k <= Resync && !skipped; k++)
                {
                    if (Agree(from, offset + i + k, to, offset + j))
                    {
                        i += k;
                        skipped = true;
                    }
                    else if (Agree(from, offset + i, to, offset + j + k))
                    {
                        j += k;
                        skipped = true;
                    }
                }

                if (!skipped)
                {
                    i++;
                    j++;
                }
            }
        }

        // Zmiana wielkości liter (tytuł wersalikami zapisany zdaniowo — PdfBlockReader.WithTitleCase) to ten sam znak.
        private static bool Same(char a, char b) => a == b || char.ToLowerInvariant(a) == char.ToLowerInvariant(b);

        private static bool Agree(string a, int ai, string b, int bi)
        {
            for (var k = 0; k < Anchor; k++)
                if (ai + k >= a.Length || bi + k >= b.Length || !Same(a[ai + k], b[bi + k]))
                    return false;
            return true;
        }

        // Środek do kilku tysięcy znaków wyrównuje najdłuższy wspólny podciąg; dłuższy — wyrównanie zachłanne.
        private const long MaxMiddleCells = 4_000_000;
        private const int Resync = 12;
        private const int Anchor = 4;
    }
}
