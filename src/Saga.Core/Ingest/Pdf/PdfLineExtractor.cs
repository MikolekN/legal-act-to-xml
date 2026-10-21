#nullable enable
using System.Text;
using UglyToad.PdfPig.Content;

namespace Saga.Core.Ingest.Pdf
{
    /// <summary>
    /// Odtwarza wiersze tekstu z liter strony PDF (PdfPig): grupowanie po linii bazowej
    /// (oś Y PdfPig rośnie do góry — sortowanie malejąco), scalanie liter w tekst z wstawianiem
    /// spacji z odstępów oraz detekcja indeksu górnego → notacja [x] (§ 89 ust. 6 ZTP; łapie też
    /// odnośniki § 163). Kryteria superscriptu: rozmiar ≤ 0,75× dominanty wiersza ORAZ linia bazowa
    /// podniesiona o &gt; 0,2× rozmiaru dominanty względem bazy wiersza.
    /// </summary>
    internal static class PdfLineExtractor
    {
        private const double SuperscriptSizeRatio = 0.75;
        private const double SuperscriptRaiseRatio = 0.2;
        private const double LineClusterToleranceRatio = 0.7;

        // 0,2 em (nie 0,25): PDF-y bez glifów spacji (odstępy słów przez przesunięcia TJ) mają odstępy
        // międzywyrazowe 0,20-0,25 em przy wyjustowaniu; kerning międzyliterowy nie zbliża się do 0,2 em.
        private const double SpaceGapRatio = 0.2;

        public static List<PdfTextLine> ExtractLines(Page page) => ExtractLines(page, page.Letters);

        /// <summary>
        /// Wiersze z wybranych liter strony — tekst poza tabelami albo tekst jednej komórki. Treść złożona w poprzek strony
        /// przychodzi w układzie, w którym jest pozioma (<see cref="PdfRotation"/>); jej wysokość podaje <paramref name="frameHeight"/>.
        /// </summary>
        public static List<PdfTextLine> ExtractLines(Page page, IReadOnlyList<Letter> letters, double? frameHeight = null)
        {
            if (letters.Count == 0)
                return new List<PdfTextLine>();

            var clusters = ClusterIntoLines(WithoutOverprints(letters));
            var lines = new List<PdfTextLine>(clusters.Count);
            foreach (var cluster in clusters)
            {
                var line = BuildLine(cluster, page, frameHeight ?? page.Height);
                if (!string.IsNullOrWhiteSpace(line.Text))
                    lines.Add(line);
            }

            // Porządek czytania: od góry strony (Y malejąco).
            return lines.OrderByDescending(l => l.BaselineY).ToList();
        }

        /// <summary>
        /// Glif nadrukowany drugi raz w tym samym miejscu — skład Monitora Polskiego pogrubia tak oznaczenia i nagłówki
        /// („Poz. 948", „UCHWAŁA NR 195" dwa razy w tym samym punkcie). Drugi egzemplarz to ta sama litera, a nie kolejna:
        /// po sortowaniu wzdłuż wiersza wychodziło „994488". Pozycje porównuje się z dokładnością do 0,1 pt.
        /// </summary>
        private static IReadOnlyList<Letter> WithoutOverprints(IReadOnlyList<Letter> letters)
        {
            var seen = new HashSet<(string Value, long X, long Y)>();
            var kept = new List<Letter>(letters.Count);
            foreach (var letter in letters)
            {
                if (seen.Add((letter.Value, (long)Math.Round(letter.StartBaseLine.X * 10), (long)Math.Round(letter.StartBaseLine.Y * 10))))
                    kept.Add(letter);
            }

            return kept.Count == letters.Count ? letters : kept;
        }

        /// <summary>
        /// Grupuje litery w wiersze po Y linii bazowej z tolerancją względną do rozmiaru fontu —
        /// tolerancja musi wchłonąć litery indeksu górnego (podniesiona baza) do wiersza macierzystego.
        /// </summary>
        private static List<List<Letter>> ClusterIntoLines(IReadOnlyList<Letter> letters)
        {
            var sorted = letters.OrderByDescending(l => l.StartBaseLine.Y).ToList();
            var clusters = new List<List<Letter>>();
            List<Letter>? current = null;
            double currentBaseY = 0;

            foreach (var letter in sorted)
            {
                var tolerance = Math.Max(2.0, LineClusterToleranceRatio * Math.Max(letter.PointSize, 1.0));
                if (current == null || currentBaseY - letter.StartBaseLine.Y > tolerance)
                {
                    current = new List<Letter>();
                    clusters.Add(current);
                    currentBaseY = letter.StartBaseLine.Y;
                }
                current.Add(letter);
            }

            return clusters;
        }

        private static PdfTextLine BuildLine(List<Letter> cluster, Page page, double pageHeight)
        {
            // Rozmiar tekstu podstawowego i jego baza (dominanta Y liter tego rozmiaru; remis — NIŻSZE Y, bo superscript
            // ma bazę podniesioną).
            var dominantSize = BaseSize(cluster);
            var baselineY = Mode(
                cluster.Where(l => Math.Round(l.PointSize, 1) == dominantSize).Select(l => Math.Round(l.StartBaseLine.Y, 1)),
                preferHigher: false);

            var ordered = cluster.OrderBy(l => l.StartBaseLine.X).ToList();
            var sb = new StringBuilder();
            bool inSuperscript = false;
            Letter? prev = null;
            Letter? lastInk = null;
            double maxGap = 0;

            foreach (var letter in ordered)
            {
                // Odstęp nie należy do indeksu górnego, nawet złożony jego rozmiarem (spacja za „4e³" w składzie
                // LibreOffice): w kanale dawałby „art. 4e[3 ]w[ ]brzmieniu", a komenda nowelizacji przestawała pasować.
                if (string.IsNullOrWhiteSpace(letter.Value))
                {
                    if (inSuperscript)
                    {
                        sb.Append(']');
                        inSuperscript = false;
                    }

                    sb.Append(' ');
                    prev = letter;
                    continue;
                }

                if (lastInk != null)
                    maxGap = Math.Max(maxGap, letter.StartBaseLine.X - lastInk.EndBaseLine.X);
                lastInk = letter;

                bool isSuper = dominantSize > 0
                    && letter.PointSize <= SuperscriptSizeRatio * dominantSize
                    && letter.StartBaseLine.Y - baselineY > SuperscriptRaiseRatio * dominantSize;

                if (prev != null)
                {
                    var gap = letter.StartBaseLine.X - prev.EndBaseLine.X;
                    if (gap > SpaceGapRatio * Math.Max(dominantSize, 1.0))
                    {
                        if (inSuperscript)
                        {
                            sb.Append(']');
                            inSuperscript = false;
                        }
                        sb.Append(' ');
                    }
                }

                if (isSuper && !inSuperscript)
                {
                    sb.Append('[');
                    inSuperscript = true;
                }
                else if (!isSuper && inSuperscript)
                {
                    sb.Append(']');
                    inSuperscript = false;
                }

                sb.Append(letter.Value);
                prev = letter;
            }

            if (inSuperscript)
                sb.Append(']');

            return new PdfTextLine
            {
                Text = sb.ToString(),
                PageNumber = page.Number,
                StartX = ordered[0].StartBaseLine.X,
                BaselineY = baselineY,
                DominantSize = dominantSize,
                PageHeight = pageHeight,
                MaxGap = maxGap,
            };
        }

        /// <summary>Dominanta (moda) wartości; kierunek rozstrzygania remisów podaje wywołujący.</summary>
        /// <summary>
        /// Rozmiar tekstu podstawowego wiersza: największy rozmiar, którym złożono co najmniej ćwierć liter. W krótkim
        /// wierszu (komórka tabeli „2" z odwołaniem „0)" w indeksie górnym) liter indeksu bywa więcej niż liter tekstu —
        /// dominanta brałaby rozmiar indeksu za podstawowy i gubiła kanał [x]; w zwykłym wierszu to ten sam rozmiar co dominanta.
        /// </summary>
        private static double BaseSize(List<Letter> cluster)
        {
            var sizes = cluster.GroupBy(l => Math.Round(l.PointSize, 1)).ToList();
            var threshold = cluster.Count / 4.0;
            return sizes.Where(size => size.Count() >= threshold).Max(size => size.Key);
        }

        private static double Mode(IEnumerable<double> values, bool preferHigher)
        {
            var groups = values.GroupBy(v => v).OrderByDescending(g => g.Count());
            var ordered = preferHigher
                ? groups.ThenByDescending(g => g.Key)
                : groups.ThenBy(g => g.Key);
            return ordered.First().Key;
        }
    }
}
