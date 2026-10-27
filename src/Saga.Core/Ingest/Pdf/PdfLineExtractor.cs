#nullable enable
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace Saga.Core.Ingest.Pdf
{
    /// <summary>
    /// Odtwarza wiersze tekstu z liter strony PDF (PdfPig): grupowanie po linii bazowej
    /// (oś Y PdfPig rośnie do góry — sortowanie malejąco), scalanie liter w tekst z wstawianiem
    /// spacji z odstępów oraz detekcja indeksu górnego → notacja [x] (§ 89 ust. 6 ZTP; łapie też
    /// odnośniki § 163). Kryteria superscriptu: rozmiar ≤ 0,75× dominanty wiersza ORAZ linia bazowa
    /// podniesiona o &gt; 0,2× rozmiaru dominanty względem bazy wiersza; cyfra — także ≤ 0,85× przy podniesieniu &gt; 0,35×.
    /// </summary>
    internal static class PdfLineExtractor
    {
        private const double SuperscriptSizeRatio = 0.75;
        private const double SuperscriptRaiseRatio = 0.2;

        /// <summary>
        /// Cyfra złożona fontem bliższym treści (≤ 0,85×), ale wyraźnie podniesiona (&gt; 0,35×), też jest indeksem górnym —
        /// tak bywa złożone oznaczenie odnośnika we wzorze załącznika („2" 7,9 pt przy treści 9,8 pt, Dz. U. 2026 poz. 1292).
        /// Tylko cyfra: mniejszy tekst na innej linii bazowej (sąsiednia komórka tabeli) nie staje się indeksem.
        /// </summary>
        private const double RaisedDigitSizeRatio = 0.85;
        private const double RaisedDigitRaiseRatio = 0.35;
        private const double LineClusterToleranceRatio = 0.7;

        /// <summary>
        /// Indeks górny złożony z nawiasami („9[1]” w Dz. U. 2026 poz. 1340 — nawiasy też są podniesione i mniejsze):
        /// notacja [x] obejmuje go drugi raz. Zostaje jedna para, jak przy indeksie bez nawiasów.
        /// </summary>
        private static readonly Regex DoubledSuperscriptPattern = new(@"\[\[([^\[\]]*)\]\]", RegexOptions.Compiled);

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

            var height = frameHeight ?? page.Height;
            var lines = new List<PdfTextLine>();
            foreach (var group in letters.GroupBy(DirectionOf))
            {
                if (group.Key == WritingDirection.Forward)
                {
                    lines.AddRange(Built(page, group.ToList(), height).Select(built => built.Line));
                    continue;
                }

                lines.AddRange(TurnedLines(page, group.ToList(), group.Key, height));
            }

            // Porządek czytania: od góry strony (Y malejąco).
            return lines.OrderByDescending(l => l.BaselineY).ToList();
        }

        /// <summary>Kierunek linii bazowej litery względem układu, w którym czyta się wiersze.</summary>
        private enum WritingDirection
        {
            Forward,
            Up,
            Down,
            Backward,
        }

        private static WritingDirection DirectionOf(Letter letter)
        {
            var (dx, dy) = (letter.EndBaseLine.X - letter.StartBaseLine.X, letter.EndBaseLine.Y - letter.StartBaseLine.Y);
            if (Math.Abs(dy) > Math.Abs(dx))
                return dy > 0 ? WritingDirection.Up : WritingDirection.Down;
            return dx < 0 ? WritingDirection.Backward : WritingDirection.Forward;
        }

        /// <summary>
        /// Wiersze z liter pisanych pod innym kątem niż reszta układu — napis pionowy w tabeli albo odwrócony do góry
        /// nogami (w tabeli w poprzek strony M.P. 2026 poz. 1017 napisy „OBSZAR 1 Personel ochrony ludności" biegną
        /// od prawej do lewej). Litery czyta się w układzie, w którym są poziome, a wiersz stoi w miejscu napisu: na jego
        /// lewej krawędzi i górze. Wcześniej każda litera była osobnym wierszem albo wiersz wychodził wspak („i c ś o n …").
        /// </summary>
        private static IEnumerable<PdfTextLine> TurnedLines(Page page, List<Letter> letters, WritingDirection direction, double height)
        {
            Func<PdfPoint, PdfPoint> toForward = direction switch
            {
                WritingDirection.Up => point => new PdfPoint(point.Y, -point.X),
                WritingDirection.Down => point => new PdfPoint(-point.Y, point.X),
                _ => point => new PdfPoint(-point.X, -point.Y),
            };

            var originals = new Dictionary<Letter, Letter>(ReferenceEqualityComparer.Instance);
            var turned = letters.Select(letter =>
            {
                var forward = PdfRotation.Transformed(letter, toForward);
                originals[forward] = letter;
                return forward;
            }).ToList();

            // Wiersze jednego napisu (odstęp nie większy niż półtorej wysokości pisma) to jeden wiersz w porządku czytania:
            // w układzie strony kolejność wierszy napisu odwróconego albo pionowego byłaby inna niż przy czytaniu.
            foreach (var run in Runs(Built(page, turned, height).ToList()))
            {
                var source = run.SelectMany(built => built.Cluster).Select(letter => originals[letter]).ToList();
                var xs = source.SelectMany(letter => new[] { letter.StartBaseLine.X, letter.EndBaseLine.X }).ToList();
                var ys = source.SelectMany(letter => new[] { letter.StartBaseLine.Y, letter.EndBaseLine.Y }).ToList();
                yield return run[0].Line with
                {
                    Text = Joined(run.Select(built => built.Line.Text)),
                    StartX = xs.Min(),
                    BaselineY = ys.Max(),
                };
            }
        }

        /// <summary>Kolejne wiersze napisu: odstęp linii bazowych nie większy niż półtorej wysokości pisma.</summary>
        private static IEnumerable<List<(List<Letter> Cluster, PdfTextLine Line)>> Runs(List<(List<Letter> Cluster, PdfTextLine Line)> lines)
        {
            var run = new List<(List<Letter> Cluster, PdfTextLine Line)>();
            foreach (var built in lines)
            {
                if (run.Count > 0 && run[^1].Line.BaselineY - built.Line.BaselineY > RunLineGapRatio * Math.Max(built.Line.DominantSize, 1.0))
                {
                    yield return run;
                    run = [];
                }

                run.Add(built);
            }

            if (run.Count > 0)
                yield return run;
        }

        private const double RunLineGapRatio = 1.5;

        /// <summary>Wiersze napisu sklejone odstępem; dywiz po literze na końcu wiersza łączy wyraz.</summary>
        private static string Joined(IEnumerable<string> lines)
        {
            var text = new StringBuilder();
            foreach (var line in lines.Select(line => line.Trim()))
            {
                if (text.Length > 1 && text[^1] == '-' && char.IsLetter(text[^2]) && line.Length > 0 && char.IsLower(line[0]))
                    text.Length--;
                else if (text.Length > 0)
                    text.Append(' ');
                text.Append(line);
            }

            return text.ToString();
        }

        /// <summary>Wiersze z liter jednego kierunku, poziomych w układzie, w którym się je czyta — razem z literami wiersza.</summary>
        private static IEnumerable<(List<Letter> Cluster, PdfTextLine Line)> Built(Page page, IReadOnlyList<Letter> letters, double height)
        {
            var spaceGlyphs = UsesSpaceGlyphs(letters);
            foreach (var cluster in ClusterIntoLines(WithoutOverprints(letters)))
            {
                var line = BuildLine(cluster, page, height, spaceGlyphs);
                if (!string.IsNullOrWhiteSpace(line.Text))
                    yield return (cluster, line);
            }
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

        private static PdfTextLine BuildLine(List<Letter> cluster, Page page, double pageHeight, bool spaceGlyphs)
        {
            // Rozmiar tekstu podstawowego i jego baza (dominanta Y liter tego rozmiaru; remis — NIŻSZE Y, bo superscript
            // ma bazę podniesioną).
            var dominantSize = BaseSize(cluster);
            var baselineY = Mode(
                cluster.Where(l => Math.Round(l.PointSize, 1) == dominantSize).Select(l => Math.Round(l.StartBaseLine.Y, 1)),
                preferHigher: false);

            var ordered = cluster.OrderBy(l => l.StartBaseLine.X).ToList();
            var raisedDigits = RaisedDigits(ordered, dominantSize, baselineY);
            var letterSpaced = spaceGlyphs ? LetterSpacedLetters(ordered, dominantSize) : [];
            var sb = new StringBuilder();
            bool inSuperscript = false;
            Letter? prev = null;
            Letter? lastInk = null;
            double maxGap = 0;

            for (var i = 0; i < ordered.Count; i++)
            {
                var letter = ordered[i];
                // Odstęp nie należy do indeksu górnego, nawet złożony jego rozmiarem (spacja za „4e³" w składzie
                // LibreOffice): w kanale dawałby „art. 4e[3 ]w[ ]brzmieniu", a komenda nowelizacji przestawała pasować.
                if (string.IsNullOrWhiteSpace(letter.Value))
                {
                    if (IsStraySpace(letter, lastInk, ordered.Skip(i + 1).FirstOrDefault(next => !string.IsNullOrWhiteSpace(next.Value))))
                        continue;


                    if (inSuperscript)
                    {
                        sb.Append(']');
                        inSuperscript = false;
                    }

                    AppendSpace(sb);
                    prev = letter;
                    continue;
                }

                if (lastInk != null)
                    maxGap = Math.Max(maxGap, letter.StartBaseLine.X - lastInk.EndBaseLine.X);
                lastInk = letter;

                bool isSuper = dominantSize > 0
                    && ((letter.PointSize <= SuperscriptSizeRatio * dominantSize
                            && letter.StartBaseLine.Y - baselineY > SuperscriptRaiseRatio * dominantSize)
                        || raisedDigits.Contains(letter));

                if (prev != null)
                {
                    var gap = letter.StartBaseLine.X - prev.EndBaseLine.X;
                    if (gap > SpaceGapRatio * Math.Max(dominantSize, 1.0) && !letterSpaced.Contains(letter))
                    {
                        if (inSuperscript)
                        {
                            sb.Append(']');
                            inSuperscript = false;
                        }
                        AppendSpace(sb);
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
                Text = DoubledSuperscriptPattern.Replace(sb.ToString(), "[$1]"),
                PageNumber = page.Number,
                StartX = ordered[0].StartBaseLine.X,
                BaselineY = baselineY,
                DominantSize = dominantSize,
                PageHeight = pageHeight,
                MaxGap = maxGap,
            };
        }

        /// <summary>
        /// Litery tekstu rozstrzelonego, przed którymi luka nie jest odstępem między wyrazami: „Opis:" złożony ze stałym
        /// odstępem międzyliterowym 0,2 em (Dz. U. 2026 poz. 1188), „Z a ł ą c z n i k i  d o …" w PDF-ach z edytora. Na
        /// stronie, która odstępy wyrazów zapisuje glifem spacji, ciąg co najmniej <see cref="MinLetterSpacedGaps"/> luk
        /// równych sobie między literami bez glifu pomiędzy to rozstrzelenie; krótszy ciąg z tą samą luką w tym wierszu
        /// („d o" za „Załączniki") — też. Między wyrazami tekstu rozstrzelonego stoi glif spacji, więc wyrazy zostają osobno.
        /// </summary>
        private static HashSet<Letter> LetterSpacedLetters(List<Letter> ordered, double dominantSize)
        {
            var letterSpaced = new HashSet<Letter>(ReferenceEqualityComparer.Instance);
            if (dominantSize <= 0)
                return letterSpaced;

            var runs = new List<(int From, int To, double Min, double Max)>();
            for (var i = 0; i < ordered.Count; i++)
            {
                var end = i;
                while (end + 1 < ordered.Count && IsSpacedLetterPair(ordered[end], ordered[end + 1], dominantSize))
                    end++;
                if (end == i)
                    continue;

                var gaps = Enumerable.Range(i, end - i).Select(k => ordered[k + 1].StartBaseLine.X - ordered[k].EndBaseLine.X).ToList();
                runs.Add((i, end, gaps.Min(), gaps.Max()));
                i = end;
            }

            var spread = LetterSpacingSpreadRatio * dominantSize;
            var uniform = runs.Where(run => run.Max - run.Min <= spread).ToList();
            var established = uniform.Where(run => run.To - run.From >= MinLetterSpacedGaps).ToList();
            foreach (var run in uniform)
            {
                var spaced = established.Any(other => Math.Max(other.Max, run.Max) - Math.Min(other.Min, run.Min) <= spread);
                if (!spaced)
                    continue;
                for (var k = run.From + 1; k <= run.To; k++)
                    letterSpaced.Add(ordered[k]);
            }

            return letterSpaced;
        }

        /// <summary>
        /// Dwie litery z luką ponad progiem odstępu, ale nie szerszą niż rozstrzelenie bywa. Znak przestankowy też należy
        /// do rozstrzelenia — odstęp międzyliterowy stoi i za ostatnią literą („Opis:" dawało „Opis :").
        /// </summary>
        private static bool IsSpacedLetterPair(Letter first, Letter second, double dominantSize)
        {
            if (!IsLetterOrPunctuation(first) || !IsLetterOrPunctuation(second))
                return false;
            var gap = second.StartBaseLine.X - first.EndBaseLine.X;
            return gap > SpaceGapRatio * dominantSize && gap <= MaxLetterSpacingRatio * dominantSize;
        }

        private static bool IsLetterOrPunctuation(Letter letter) =>
            letter.Value.Length > 0 && (char.IsLetter(letter.Value[0]) || char.IsPunctuation(letter.Value[0]));

        /// <summary>
        /// Litery zapisujące odstępy wyrazów glifem spacji — co najmniej <see cref="SpaceGlyphShare"/> liter to spacje.
        /// W PDF-ie bez glifów spacji odstęp wyrazu to sama luka i bywa nie szerszy niż rozstrzelenie, więc tam luki
        /// zostają odstępami.
        /// </summary>
        private static bool UsesSpaceGlyphs(IReadOnlyList<Letter> letters) =>
            letters.Count(letter => string.IsNullOrWhiteSpace(letter.Value)) >= Math.Max(MinSpaceGlyphs, SpaceGlyphShare * letters.Count);

        /// <summary>
        /// Glif spacji z innej linii bazowej niż litery po obu jego stronach, które nie zostawiają mu miejsca — wciągnięty
        /// do wiersza tolerancją grupowania (spacja za „poziom sieci" w komórce obok, 2,5 pt niżej — M.P. 2026 poz. 343)
        /// wpadał po sortowaniu w środek wyrazu: „8 5 %", „N owiny", „1 1. Miejscowość". Spacja na linii bazowej sąsiadów
        /// zostaje odstępem nawet bez miejsca: w ściśniętej wąskiej komórce odstęp wyrazów bywa węższy niż pół spacji
        /// („przez podmiot", Dz. U. 2026 poz. 1310), a „10)pracownik" złożone bez luki ma spację w strumieniu treści.
        /// </summary>
        private static bool IsStraySpace(Letter space, Letter? before, Letter? after)
        {
            if (before == null || after == null)
                return false;
            var shift = StrayBaselineRatio * Math.Max(space.PointSize, 1.0);
            return Math.Abs(space.StartBaseLine.Y - before.StartBaseLine.Y) > shift
                && Math.Abs(space.StartBaseLine.Y - after.StartBaseLine.Y) > shift
                && after.StartBaseLine.X - before.EndBaseLine.X < StraySpaceRoomRatio * space.Width;
        }

        private const double StrayBaselineRatio = 0.05;
        private const double StraySpaceRoomRatio = 0.5;
        private const int MinLetterSpacedGaps = 3;
        private const double LetterSpacingSpreadRatio = 0.06;
        private const double MaxLetterSpacingRatio = 0.8;
        private const double SpaceGlyphShare = 0.05;
        private const int MinSpaceGlyphs = 3;

        /// <summary>
        /// Jeden odstęp między wyrazami. W wierszu justowanym odstęp bywa szerszy niż znak spacji, który go zapisuje
        /// („Progress  in  International" w odnośniku M.P. 2026 poz. 672): za spacją stoi jeszcze luka większa niż
        /// <see cref="SpaceGapRatio"/> — to wciąż ten sam odstęp, nie drugi.
        /// </summary>
        private static void AppendSpace(StringBuilder sb)
        {
            if (sb.Length > 0 && sb[^1] == ' ')
                return;
            sb.Append(' ');
        }

        /// <summary>
        /// Cyfry indeksu górnego złożone pismem bliższym tekstowi (<see cref="RaisedDigitSizeRatio"/>). Ciąg cyfr, za którym
        /// stoi nawias albo kropka („3)" we wzorze złożonym na kilku liniach bazowych — Dz. U. 2025 poz. 878), jest numerem
        /// wyliczenia, nie indeksem.
        /// </summary>
        private static HashSet<Letter> RaisedDigits(List<Letter> ordered, double dominantSize, double baselineY)
        {
            var result = new HashSet<Letter>();
            bool IsRaisedDigit(Letter letter) => dominantSize > 0
                && char.IsDigit(letter.Value[0])
                && letter.PointSize <= RaisedDigitSizeRatio * dominantSize
                && letter.StartBaseLine.Y - baselineY > RaisedDigitRaiseRatio * dominantSize;

            for (var i = 0; i < ordered.Count; i++)
            {
                if (!IsRaisedDigit(ordered[i]))
                    continue;

                var end = i;
                while (end < ordered.Count && IsRaisedDigit(ordered[end]))
                    end++;

                if (end >= ordered.Count || ordered[end].Value is not (")" or "."))
                    result.UnionWith(ordered.Skip(i).Take(end - i));
                i = end - 1;
            }

            return result;
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
