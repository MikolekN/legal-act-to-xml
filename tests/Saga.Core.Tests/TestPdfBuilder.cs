using System.Globalization;
using System.Text;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Minimalny generator PDF do fikstur testowych adaptera PDF (Etap 9) — bez zależności od plików
    /// fontów. Polskie znaki i cudzysłowy typograficzne realizuje przez Helveticę (Standard 14)
    /// z kodowaniem /Differences (nazwy glifów Adobe: aogonek, sacute, quotedblbase…), które
    /// ekstrakcja PdfPig mapuje z powrotem na Unicode. Pełna kontrola pozycji (x, y w punktach,
    /// oś Y rośnie DO GÓRY) i rozmiaru fontu — konieczna dla testów superscriptu/wcięć/artefaktów.
    /// </summary>
    internal sealed class TestPdfBuilder
    {
        /// <summary>
        /// Tekst w wierszu; <c>Rotated</c> — obrócony o ćwierć obrotu, biegnący w górę strony (treść w poprzek strony);
        /// <c>UpsideDown</c> — obrócony o pół obrotu, biegnący od prawej do lewej;
        /// <c>Clip</c> — prostokąt przycięcia (x, y, szerokość, wysokość), poza którym tekst nie jest widoczny.
        /// </summary>
        internal sealed record TextRun(string Text, double X, double Y, double Size, bool Rotated = false,
            (double X, double Y, double Width, double Height)? Clip = null, bool UpsideDown = false);

        /// <summary>Odcinek rysowany kreską — obramowanie tabeli.</summary>
        internal sealed record Rule(double X1, double Y1, double X2, double Y2);

        /// <summary>
        /// Wypełniony prostokąt rysowany przed tekstem (kawałek obramowania, białe tło komórki) albo biały po nim
        /// (zakrycie tekstu).
        /// </summary>
        internal sealed record Box(double X, double Y, double Width, double Height, bool White = false);

        private readonly List<List<TextRun>> _pages = new();
        private readonly List<List<Rule>> _rules = new();
        private readonly List<List<Box>> _boxes = new();
        private readonly List<List<Box>> _covers = new();
        private readonly Dictionary<char, (byte Code, string GlyphName)> _mapped = new();
        private byte _nextCode = 0x80;

        public double PageWidth { get; init; } = 595;   // A4 w punktach
        public double PageHeight { get; init; } = 842;

        private static readonly Dictionary<char, string> GlyphNames = new()
        {
            ['ą'] = "aogonek", ['ć'] = "cacute", ['ę'] = "eogonek", ['ł'] = "lslash", ['ń'] = "nacute",
            ['ó'] = "oacute", ['ś'] = "sacute", ['ź'] = "zacute", ['ż'] = "zdotaccent",
            ['Ą'] = "Aogonek", ['Ć'] = "Cacute", ['Ę'] = "Eogonek", ['Ł'] = "Lslash", ['Ń'] = "Nacute",
            ['Ó'] = "Oacute", ['Ś'] = "Sacute", ['Ź'] = "Zacute", ['Ż'] = "Zdotaccent",
            ['„'] = "quotedblbase", ['”'] = "quotedblright", ['“'] = "quotedblleft",
            ['–'] = "endash", ['—'] = "emdash", ['‒'] = "figuredash", ['§'] = "section", ['•'] = "bullet",
        };

        public TestPdfBuilder AddPage()
        {
            _pages.Add(new List<TextRun>());
            _rules.Add(new List<Rule>());
            _boxes.Add(new List<Box>());
            _covers.Add(new List<Box>());
            return this;
        }

        /// <summary>Rysuje odcinek na OSTATNIEJ stronie (obramowanie tabeli). Współrzędne w punktach; Y rośnie do góry.</summary>
        public TestPdfBuilder AddLine(double x1, double y1, double x2, double y2)
        {
            if (_pages.Count == 0)
                AddPage();
            _rules[^1].Add(new Rule(x1, y1, x2, y2));
            return this;
        }

        /// <summary>
        /// Wypełniony prostokąt na OSTATNIEJ stronie, rysowany przed tekstem — czarny: obramowanie składane kawałkami,
        /// biały: tło komórki.
        /// </summary>
        public TestPdfBuilder AddFilledBox(double x, double y, double width, double height, bool white = false)
        {
            if (_pages.Count == 0)
                AddPage();
            _boxes[^1].Add(new Box(x, y, width, height, white));
            return this;
        }

        /// <summary>Biały prostokąt na OSTATNIEJ stronie, rysowany PO tekście — zakrywa to, co pod nim.</summary>
        public TestPdfBuilder AddCover(double x, double y, double width, double height)
        {
            if (_pages.Count == 0)
                AddPage();
            _covers[^1].Add(new Box(x, y, width, height, White: true));
            return this;
        }

        /// <summary>Siatka tabeli: linie poziome na podanych Y i pionowe na podanych X, każda przez całą tabelę.</summary>
        public TestPdfBuilder AddGrid(double[] rows, double[] columns)
        {
            foreach (var y in rows)
                AddLine(columns[0], y, columns[^1], y);
            foreach (var x in columns)
                AddLine(x, rows[0], x, rows[^1]);
            return this;
        }

        /// <summary>Dodaje tekst na OSTATNIEJ stronie. Współrzędne w punktach; Y rośnie do góry.</summary>
        public TestPdfBuilder AddText(string text, double x, double y, double size = 11) => Add(new TextRun(text, x, y, size));

        /// <summary>
        /// Dodaje tekst obrócony o ćwierć obrotu (biegnie w górę strony od punktu x, y; wiersze następują w prawo) —
        /// tak skład stawia szeroką tabelę na stronie pionowej.
        /// </summary>
        public TestPdfBuilder AddRotatedText(string text, double x, double y, double size = 11) => Add(new TextRun(text, x, y, size, Rotated: true));

        /// <summary>Dodaje tekst odwrócony o pół obrotu: biegnie od punktu x, y w lewo, litery do góry nogami.</summary>
        public TestPdfBuilder AddUpsideDownText(string text, double x, double y, double size = 11) =>
            Add(new TextRun(text, x, y, size, UpsideDown: true));

        /// <summary>
        /// Dodaje tekst przycięty prostokątem (<c>q … re W n BT … ET Q</c>) — tak wydawca nakłada na stronę dokumentu
        /// własny nagłówek, a treść oryginału przycina do pasa pod nim.
        /// </summary>
        public TestPdfBuilder AddClippedText(string text, double x, double y, (double X, double Y, double Width, double Height) clip, double size = 11) =>
            Add(new TextRun(text, x, y, size, Clip: clip));

        private TestPdfBuilder Add(TextRun run)
        {
            if (_pages.Count == 0)
                AddPage();
            _pages[^1].Add(run);
            return this;
        }

        public byte[] Build()
        {
            // Najpierw przejdź wszystkie teksty, by zebrać mapowanie znaków spoza ASCII.
            foreach (var page in _pages)
                foreach (var run in page)
                    foreach (var c in run.Text)
                        MapChar(c);

            var objects = new List<string>();

            // 1: katalog, 2: drzewo stron, 3: font, 4: encoding; strony i strumienie dalej.
            int pageObjStart = 5;
            var pageRefs = new List<string>();
            for (int i = 0; i < _pages.Count; i++)
                pageRefs.Add($"{pageObjStart + i * 2} 0 R");

            objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
            objects.Add($"<< /Type /Pages /Kids [{string.Join(" ", pageRefs)}] /Count {_pages.Count} >>");
            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding 4 0 R >>");
            objects.Add(BuildEncodingObject());

            for (int i = 0; i < _pages.Count; i++)
            {
                int contentObj = pageObjStart + i * 2 + 1;
                objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {Pt(PageWidth)} {Pt(PageHeight)}] " +
                    $"/Resources << /Font << /F1 3 0 R >> >> /Contents {contentObj} 0 R >>");

                var content = BuildRules(_rules[i]) + BuildBoxes(_boxes[i]) + BuildContentStream(_pages[i])
                    + (_covers[i].Count > 0 ? "\n" + BuildBoxes(_covers[i]).TrimEnd('\n') : string.Empty);
                objects.Add($"<< /Length {content.Length} >>\nstream\n{content}\nendstream");
            }

            return AssembleFile(objects);
        }

        private void MapChar(char c)
        {
            if (c < 0x80 || _mapped.ContainsKey(c))
                return;
            if (!GlyphNames.TryGetValue(c, out var glyph))
                throw new InvalidOperationException($"TestPdfBuilder: brak mapowania glifu dla znaku '{c}' (U+{(int)c:X4}).");
            if (_nextCode == 0)
                throw new InvalidOperationException("TestPdfBuilder: wyczerpano kody 0x80-0xFF.");
            _mapped[c] = (_nextCode, glyph);
            _nextCode++;
        }

        private string BuildEncodingObject()
        {
            var sb = new StringBuilder("<< /Type /Encoding /BaseEncoding /WinAnsiEncoding /Differences [");
            foreach (var (_, (code, glyph)) in _mapped)
                sb.Append(CultureInfo.InvariantCulture, $" {code} /{glyph}");
            sb.Append(" ] >>");
            return sb.ToString();
        }

        private string BuildContentStream(List<TextRun> runs)
        {
            var sb = new StringBuilder();
            foreach (var run in runs)
            {
                if (run.Clip is { } clip)
                    sb.Append(CultureInfo.InvariantCulture, $"q {Pt(clip.X)} {Pt(clip.Y)} {Pt(clip.Width)} {Pt(clip.Height)} re W n ");
                sb.Append(CultureInfo.InvariantCulture,
                    $"BT /F1 {Pt(run.Size)} Tf {(run.Rotated ? "0 1 -1 0" : run.UpsideDown ? "-1 0 0 -1" : "1 0 0 1")} {Pt(run.X)} {Pt(run.Y)} Tm (");
                foreach (var c in run.Text)
                {
                    if (c == '(' || c == ')' || c == '\\')
                        sb.Append('\\').Append(c);
                    else if (c < 0x80)
                        sb.Append(c);
                    else
                        sb.Append('\\').Append(Convert.ToString(_mapped[c].Code, 8).PadLeft(3, '0'));
                }
                sb.Append(run.Clip is null ? ") Tj ET\n" : ") Tj ET Q\n");
            }
            return sb.ToString().TrimEnd('\n');
        }

        private static string BuildBoxes(List<Box> boxes)
        {
            var sb = new StringBuilder();
            foreach (var box in boxes)
                sb.Append(CultureInfo.InvariantCulture, $"{(box.White ? 1 : 0)} g {Pt(box.X)} {Pt(box.Y)} {Pt(box.Width)} {Pt(box.Height)} re f 0 g\n");
            return sb.ToString();
        }

        private static string BuildRules(List<Rule> rules)
        {
            var sb = new StringBuilder();
            foreach (var rule in rules)
                sb.Append(CultureInfo.InvariantCulture, $"0.5 w {Pt(rule.X1)} {Pt(rule.Y1)} m {Pt(rule.X2)} {Pt(rule.Y2)} l S\n");
            return sb.ToString();
        }

        private static string Pt(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        private static byte[] AssembleFile(List<string> objects)
        {
            var sb = new StringBuilder();
            sb.Append("%PDF-1.4\n");

            var offsets = new List<int>();
            for (int i = 0; i < objects.Count; i++)
            {
                offsets.Add(Encoding.Latin1.GetByteCount(sb.ToString()));
                sb.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
            }

            int xrefOffset = Encoding.Latin1.GetByteCount(sb.ToString());
            sb.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Count + 1}\n");
            sb.Append("0000000000 65535 f \n");
            foreach (var off in offsets)
                sb.Append(CultureInfo.InvariantCulture, $"{off:0000000000} 00000 n \n");
            sb.Append(CultureInfo.InvariantCulture,
                $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");

            return Encoding.Latin1.GetBytes(sb.ToString());
        }
    }
}
