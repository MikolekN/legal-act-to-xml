#nullable enable
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Graphics.Operations;
using UglyToad.PdfPig.Graphics.Operations.ClippingPaths;
using UglyToad.PdfPig.Graphics.Operations.PathConstruction;
using UglyToad.PdfPig.Graphics.Operations.SpecialGraphicsState;
using UglyToad.PdfPig.Graphics.Operations.TextShowing;

namespace Saga.Core.Ingest.Pdf
{
    /// <summary>
    /// Litery widoczne na stronie: bez tych, które leżą poza obszarem przycięcia obowiązującym przy ich rysowaniu, i bez
    /// tych, które zakrył narysowany po nich biały prostokąt. Tak wydawca nakłada na stronę dokumentu własny nagłówek:
    /// treść oryginału przycina do pasa pod nim albo zakrywa jego nagłówek i pisze swój (M.P. 2026 poz. 1012). Tekst
    /// oryginału zostaje w warstwie tekstowej, choć na stronie go nie ma, a jego wiersze przeplatały się z wierszami
    /// wydawcy („ZałączZnaiłąk…”) i załączniki przepadały.
    /// </summary>
    /// <remarks>
    /// PdfPig nie przycina ani nie zakrywa liter. Numer <c>TextSequence</c> litery to numer operacji pokazującej tekst
    /// w strumieniu strony, więc stan rysowania śledzi się po operacjach (q/Q, cm, ścieżka zakończona W/W*, kolor
    /// wypełnienia) i przypisuje literom po tym numerze. Formularz (Do) z własnym tekstem przesuwa numerację — wtedy
    /// liczby się nie zgadzają i strona zostaje bez zmian. Obszarem jest prostokąt otaczający ścieżkę; ścieżka z krzywą
    /// ani nie przycina, ani nie zakrywa.
    /// </remarks>
    internal static class PdfVisibleText
    {
        /// <summary>Tolerancja granicy obszaru (punkty) — litera na samej krawędzi zostaje.</summary>
        private const double Tolerance = 1.0;

        /// <summary>Białe wypełnienie narysowane po <c>Shown</c> operacjach pokazujących tekst.</summary>
        private readonly record struct Cover(PdfRectangle Box, int Shown);

        internal static IReadOnlyList<Letter> Visible(Page page)
        {
            var letters = page.Letters;
            if (letters.Count == 0)
                return letters;

            var shown = Unclipped(page, letters);
            var visible = WithoutOverprinted(shown);
            return visible.Count == letters.Count ? letters : visible;
        }

        /// <summary>Litery w obszarze przycięcia i niezakryte białym prostokątem.</summary>
        private static IReadOnlyList<Letter> Unclipped(Page page, IReadOnlyList<Letter> letters)
        {
            var (clips, covers) = Trace(page.Operations);
            if (covers.Count == 0 && clips.All(clip => clip is null))
                return letters;

            var sequences = letters.Select(letter => letter.TextSequence).Distinct().Count();
            if (sequences != clips.Count || letters.Any(letter => letter.TextSequence < 1 || letter.TextSequence > clips.Count))
                return letters;

            var sheet = new PdfRectangle(0, 0, page.Width, page.Height);
            return letters.Where(letter =>
                    KeptAnyway(letter, sheet)
                    || ((clips[letter.TextSequence - 1] is not { } clip || Touches(letter, clip))
                        && !covers.Any(cover => letter.TextSequence <= cover.Shown && Inside(letter, cover.Box))))
                .ToList();
        }

        /// <summary>Udział liter ciągu, które zderzają się z tekstem narysowanym później, od którego ciąg jest nadrukowany.</summary>
        private const double OverprintShare = 0.25;

        /// <summary>Najmniej liter ciągu zderzonych z tekstem narysowanym później — pojedyncze zetknięcia nie wystarczą.</summary>
        private const int OverprintMinLetters = 5;

        /// <summary>Część mniejszej z dwóch liter, którą musi przykryć druga, żeby litery się zderzyły.</summary>
        private const double CollisionShare = 0.3;

        /// <summary>Skok linii bazowej (w rozmiarach pisma), od którego zaczyna się nowy ciąg — inny blok tekstu.</summary>
        private const double RunLineJump = 2;

        /// <summary>
        /// Tekst nadrukowany na innym tekście bez zakrycia go: wydawca pisze nagłówek załącznika na nagłówku oryginału
        /// (Dz. U. 2026 poz. 1341 — „Załącznik do rozporządzenia … (Dz. U. poz. …)" 12,5 pt pod „… (Dz. U. poz. 1341)" 8 pt).
        /// Na stronie nie da się odczytać żadnego, a w warstwie tekstowej ich wiersze przeplatały się, więc załącznik
        /// przepadał. Widać tekst narysowany później: ciąg kolejnych operacji pokazujących tekst jednym pismem znika, gdy
        /// zderza się z ciągiem narysowanym po nim innym pismem — co najmniej <see cref="OverprintMinLetters"/> liter
        /// i <see cref="OverprintShare"/> jego liter. Litera powtórzona tym samym pismem (pogrubienie nadrukiem) i indeks
        /// górny obok tekstu tego warunku nie spełniają.
        /// </summary>
        private static IReadOnlyList<Letter> WithoutOverprinted(IReadOnlyList<Letter> letters)
        {
            var runs = new List<List<Letter>>();
            foreach (var letter in letters.OrderBy(l => l.TextSequence))
            {
                // Ciąg to jeden blok tekstu: kolejne operacje tym samym pismem bez skoku o więcej niż dwa wiersze.
                var last = runs.Count > 0 ? runs[^1] : null;
                if (last is not null && letter.TextSequence - last[^1].TextSequence <= 1
                    && Math.Abs(letter.PointSize - last[^1].PointSize) < 0.5
                    && Math.Abs(letter.StartBaseLine.Y - last[^1].StartBaseLine.Y) <= RunLineJump * letter.PointSize)
                    last.Add(letter);
                else
                    runs.Add([letter]);
            }

            if (runs.Count < 2)
                return letters;

            var boxes = runs.Select(Bounds).ToList();
            var hidden = new HashSet<Letter>();
            for (var i = 0; i < runs.Count; i++)
            {
                var inked = runs[i].Where(l => !string.IsNullOrWhiteSpace(l.Value)).ToList();
                if (inked.Count < OverprintMinLetters)
                    continue;

                var collided = new HashSet<Letter>();
                for (var j = i + 1; j < runs.Count; j++)
                {
                    if (Math.Abs(runs[j][0].PointSize - runs[i][0].PointSize) < 0.5 || !Overlap(boxes[i], boxes[j]))
                        continue;
                    foreach (var later in runs[j].Where(l => !string.IsNullOrWhiteSpace(l.Value)))
                        foreach (var earlier in inked)
                            if (Collide(earlier, later))
                                collided.Add(earlier);
                }

                if (collided.Count >= OverprintMinLetters && collided.Count >= OverprintShare * inked.Count)
                    hidden.UnionWith(runs[i]);
            }

            return hidden.Count == 0 ? letters : letters.Where(letter => !hidden.Contains(letter)).ToList();
        }

        private static PdfRectangle Bounds(List<Letter> run) => new(
            run.Min(l => l.BoundingBox.Left), run.Min(l => l.BoundingBox.Bottom),
            run.Max(l => l.BoundingBox.Right), run.Max(l => l.BoundingBox.Top));

        private static bool Overlap(PdfRectangle a, PdfRectangle b) =>
            a.Left < b.Right && b.Left < a.Right && a.Bottom < b.Top && b.Bottom < a.Top;

        private static bool Collide(Letter a, Letter b)
        {
            var (x, y) = (a.BoundingBox, b.BoundingBox);
            var width = Math.Min(x.Right, y.Right) - Math.Max(x.Left, y.Left);
            var height = Math.Min(x.Top, y.Top) - Math.Max(x.Bottom, y.Bottom);
            if (width <= 0 || height <= 0)
                return false;

            var smaller = Math.Min(x.Width * x.Height, y.Width * y.Height);
            return smaller > 0 && width * height >= CollisionShare * smaller;
        }

        /// <summary>
        /// Litera choć częściowo w obszarze przycięcia — wiersz na krawędzi komórki ma widoczną górę liter (PDF z edytora:
        /// „drzewnego (przypadek 3a)” leży środkiem 0,1 pt pod krawędzią, ale nie całą wysokością).
        /// </summary>
        private static bool Touches(Letter letter, PdfRectangle area)
        {
            var core = Core(letter);
            return core.Right >= area.Left - Tolerance && core.Left <= area.Right + Tolerance
                && core.Top >= area.Bottom - Tolerance && core.Bottom <= area.Top + Tolerance;
        }

        /// <summary>
        /// Rdzeń litery: od linii bazowej na pół wysokości pisma, wzdłuż kierunku pisma. Prostokąt otaczający z PdfPig
        /// sięga głęboko pod linię bazową (całe pudełko fontu), więc wiersz nagłówka nad przycięciem „dotykał” obszaru.
        /// </summary>
        private static PdfRectangle Core(Letter letter)
        {
            var (start, end) = (letter.StartBaseLine, letter.EndBaseLine);
            var (dx, dy) = (end.X - start.X, end.Y - start.Y);
            var length = Math.Sqrt(dx * dx + dy * dy);
            (dx, dy) = length > 1e-6 ? (dx / length, dy / length) : (1, 0);
            var (upX, upY) = (-dy * CoreHeight * letter.PointSize, dx * CoreHeight * letter.PointSize);
            double[] xs = [start.X, end.X, start.X + upX, end.X + upX], ys = [start.Y, end.Y, start.Y + upY, end.Y + upY];
            return new PdfRectangle(xs.Min(), ys.Min(), xs.Max(), ys.Max());
        }

        /// <summary>Wysokość rdzenia litery jako część rozmiaru pisma (mniej więcej wysokość małych liter).</summary>
        private const double CoreHeight = 0.5;

        /// <summary>
        /// Litera poza stroną to treść wychodząca za krawędź (wzór szerszy niż strona w PDF-ie z edytora) — nie nakrywa
        /// jej nagłówek wydawcy, więc zostaje, jak dotąd.
        /// </summary>
        private static bool KeptAnyway(Letter letter, PdfRectangle page) => !Inside(letter, page);

        private static bool Inside(Letter letter, PdfRectangle area)
        {
            var core = Core(letter);
            var (x, y) = ((core.Left + core.Right) / 2, (core.Bottom + core.Top) / 2);
            return x >= area.Left - Tolerance && x <= area.Right + Tolerance
                && y >= area.Bottom - Tolerance && y <= area.Top + Tolerance;
        }

        /// <summary>
        /// Obszar przycięcia przy każdej operacji pokazującej tekst, w kolejności strumienia (null — bez przycięcia),
        /// i białe wypełnienia z liczbą operacji pokazujących tekst przed nimi.
        /// </summary>
        private static (List<PdfRectangle?> Clips, List<Cover> Covers) Trace(IReadOnlyList<IGraphicsStateOperation> operations)
        {
            var clips = new List<PdfRectangle?>();
            var covers = new List<Cover>();
            var stack = new Stack<(double[] Matrix, PdfRectangle? Clip, bool White)>();
            double[] matrix = [1, 0, 0, 1, 0, 0];
            PdfRectangle? clip = null;
            var white = false;
            var path = new List<PdfPoint>();
            bool clipping = false, curved = false;

            void Add(double x, double y) =>
                path.Add(new PdfPoint(matrix[0] * x + matrix[2] * y + matrix[4], matrix[1] * x + matrix[3] * y + matrix[5]));

            foreach (var operation in operations)
            {
                switch (operation)
                {
                    case Push:
                        stack.Push((matrix, clip, white));
                        break;
                    case Pop when stack.Count > 0:
                        (matrix, clip, white) = stack.Pop();
                        break;
                    case ModifyCurrentTransformationMatrix { Value: { Length: 6 } m }:
                        matrix =
                        [
                            m[0] * matrix[0] + m[1] * matrix[2], m[0] * matrix[1] + m[1] * matrix[3],
                            m[2] * matrix[0] + m[3] * matrix[2], m[2] * matrix[1] + m[3] * matrix[3],
                            m[4] * matrix[0] + m[5] * matrix[2] + matrix[4], m[4] * matrix[1] + m[5] * matrix[3] + matrix[5],
                        ];
                        break;
                    case SetNonStrokeColorDeviceGray gray:
                        white = gray.Gray >= 1;
                        break;
                    case SetNonStrokeColorDeviceRgb rgb:
                        white = rgb.R >= 1 && rgb.G >= 1 && rgb.B >= 1;
                        break;
                    case SetNonStrokeColorDeviceCmyk cmyk:
                        white = cmyk.C <= 0 && cmyk.M <= 0 && cmyk.Y <= 0 && cmyk.K <= 0;
                        break;
                    case { Operator: "sc" or "scn" or "cs" }:
                        white = false;
                        break;
                    case AppendRectangle rectangle:
                        Add(rectangle.LowerLeftX, rectangle.LowerLeftY);
                        Add(rectangle.LowerLeftX + rectangle.Width, rectangle.LowerLeftY);
                        Add(rectangle.LowerLeftX, rectangle.LowerLeftY + rectangle.Height);
                        Add(rectangle.LowerLeftX + rectangle.Width, rectangle.LowerLeftY + rectangle.Height);
                        break;
                    case BeginNewSubpath start:
                        Add(start.X, start.Y);
                        break;
                    case AppendStraightLineSegment line:
                        Add(line.X, line.Y);
                        break;
                    case { Operator: "c" or "v" or "y" }:
                        curved = true;
                        break;
                    case ModifyClippingByNonZeroWindingIntersect or ModifyClippingByEvenOddIntersect:
                        clipping = true;
                        break;
                    case { Operator: "n" or "S" or "s" or "f" or "F" or "f*" or "B" or "B*" or "b" or "b*" }:
                        if (!curved && path.Count > 0)
                        {
                            var bounds = new PdfRectangle(path.Min(p => p.X), path.Min(p => p.Y), path.Max(p => p.X), path.Max(p => p.Y));
                            if (clipping)
                                clip = clip is { } current ? Intersection(current, bounds) : bounds;
                            if (white && operation.Operator is not ("n" or "S" or "s"))
                                covers.Add(new Cover(clip is { } visible ? Intersection(visible, bounds) : bounds, clips.Count));
                        }

                        path.Clear();
                        clipping = curved = false;
                        break;
                    case ShowText or ShowTextsWithPositioning or MoveToNextLineShowText or MoveToNextLineShowTextWithSpacing:
                        clips.Add(clip);
                        break;
                }
            }

            return (clips, covers);
        }

        private static PdfRectangle Intersection(PdfRectangle a, PdfRectangle b)
        {
            double left = Math.Max(a.Left, b.Left), bottom = Math.Max(a.Bottom, b.Bottom);
            double right = Math.Max(left, Math.Min(a.Right, b.Right)), top = Math.Max(bottom, Math.Min(a.Top, b.Top));
            return new PdfRectangle(left, bottom, right, top);
        }
    }
}
