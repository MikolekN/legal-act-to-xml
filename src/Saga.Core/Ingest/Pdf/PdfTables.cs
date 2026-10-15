#nullable enable
using UglyToad.PdfPig.Content;

namespace Saga.Core.Ingest.Pdf
{
    /// <summary>
    /// Tabela strony PDF odczytana z obramowania: komórki siatki z literami, które w nich stoją.
    /// </summary>
    internal sealed class PdfTable
    {
        /// <summary>Krawędzie siatki: góra i dół (Y rośnie do góry), lewa i prawa.</summary>
        public required double Top { get; init; }

        public required double Bottom { get; init; }

        public required double Left { get; init; }

        public required double Right { get; init; }

        /// <summary>Położenie linii pionowych siatki — po nim poznaje się tabelę ciągnącą się na następnej stronie.</summary>
        public required IReadOnlyList<double> Columns { get; init; }

        /// <summary>Liczba wierszy siatki.</summary>
        public required int RowCount { get; init; }

        /// <summary>Komórki w kolejności wierszy, w wierszu — od lewej.</summary>
        public required IReadOnlyList<PdfTableCell> Cells { get; init; }
    }

    /// <summary>Komórka siatki tabeli PDF; scalona obejmuje kilka wierszy albo kolumn siatki.</summary>
    internal sealed class PdfTableCell
    {
        public required int Row { get; init; }

        public required int Column { get; init; }

        public int RowSpan { get; init; } = 1;

        public int ColumnSpan { get; init; } = 1;

        /// <summary>Krawędzie komórki: lewa, prawa, dół, góra (Y rośnie do góry).</summary>
        public required (double Left, double Right, double Bottom, double Top) Bounds { get; init; }

        public List<Letter> Letters { get; } = [];

        /// <summary>Wiersze tekstu komórki w porządku czytania — wypełnia je czytnik bloków.</summary>
        public List<PdfTextLine> Lines { get; } = [];

        internal bool Contains(double x, double y) =>
            x >= Bounds.Left && x <= Bounds.Right && y >= Bounds.Bottom && y <= Bounds.Top;
    }

    /// <summary>
    /// Tabele strony z obramowania rysowanego ścieżkami: odcinki poziome i pionowe, które się przecinają, tworzą siatkę;
    /// brak odcinka między sąsiednimi polami siatki to scalenie komórek. Tabela bez obramowania (układ kolumn wyrównany
    /// spacjami) siatki nie ma — zostaje wierszami tekstu.
    /// </summary>
    internal static class PdfTables
    {
        /// <summary>Tolerancja położenia odcinków i ich styku (punkty) — krawędzie komórek rysuje się z zaokrągleniem.</summary>
        private const double Tolerance = 1.5;

        /// <summary>Grubość, do której wypełniony prostokąt jest odcinkiem obramowania, a nie cieniowaniem komórki.</summary>
        private const double ThinStroke = 2.0;

        private readonly record struct Segment(bool Horizontal, double At, double From, double To);

        internal static List<PdfTable> Find(Page page)
        {
            var segments = Segments(page);
            var tables = new List<PdfTable>();
            foreach (var component in Components(segments))
            {
                var horizontals = component.Where(s => s.Horizontal).ToList();
                var verticals = component.Where(s => !s.Horizontal).ToList();
                var rows = Distinct(horizontals.Select(s => s.At)).OrderByDescending(y => y).ToList();
                var columns = Distinct(verticals.Select(s => s.At)).OrderBy(x => x).ToList();
                if (rows.Count < 2 || columns.Count < 2)
                    continue;

                tables.Add(Build(rows, columns, horizontals, verticals));
            }

            if (tables.Count == 0)
                return tables;

            foreach (var letter in page.Letters)
            {
                var x = (letter.StartBaseLine.X + letter.EndBaseLine.X) / 2;
                var y = letter.StartBaseLine.Y + 0.3 * letter.PointSize;
                var cell = tables.Select(table => CellAt(table, x, y)).FirstOrDefault(found => found is not null);
                cell?.Letters.Add(letter);
            }

            return tables;
        }

        /// <summary>Komórka, w której stoi punkt; null poza tabelą.</summary>
        private static PdfTableCell? CellAt(PdfTable table, double x, double y)
        {
            if (x < table.Left || x > table.Right || y > table.Top || y < table.Bottom)
                return null;

            return table.Cells.FirstOrDefault(cell => cell.Contains(x, y));
        }

        private static List<Segment> Segments(Page page)
        {
            var segments = new List<Segment>();
            foreach (var path in page.Paths)
            {
                if (path.IsClipping || (!path.IsStroked && !path.IsFilled))
                    continue;

                foreach (var subpath in path)
                {
                    if (subpath.GetBoundingRectangle() is not { } box)
                        continue;

                    if (box.Height <= ThinStroke && box.Width > ThinStroke)
                        segments.Add(new Segment(true, (box.Bottom + box.Top) / 2, box.Left, box.Right));
                    else if (box.Width <= ThinStroke && box.Height > ThinStroke)
                        segments.Add(new Segment(false, (box.Left + box.Right) / 2, box.Bottom, box.Top));
                    else if (path.IsStroked && subpath.IsDrawnAsRectangle)
                    {
                        // Prostokąt obrysowany — cztery krawędzie komórki.
                        segments.Add(new Segment(true, box.Top, box.Left, box.Right));
                        segments.Add(new Segment(true, box.Bottom, box.Left, box.Right));
                        segments.Add(new Segment(false, box.Left, box.Bottom, box.Top));
                        segments.Add(new Segment(false, box.Right, box.Bottom, box.Top));
                    }
                }
            }

            return segments;
        }

        /// <summary>Grupy odcinków połączonych przecięciami — każda to obramowanie jednej tabeli.</summary>
        private static List<List<Segment>> Components(List<Segment> segments)
        {
            var parent = Enumerable.Range(0, segments.Count).ToArray();
            int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);

            for (var i = 0; i < segments.Count; i++)
                for (var j = i + 1; j < segments.Count; j++)
                    if (segments[i].Horizontal != segments[j].Horizontal && Cross(segments[i], segments[j]))
                        parent[Find(i)] = Find(j);

            return Enumerable.Range(0, segments.Count)
                .GroupBy(Find)
                .Select(group => group.Select(i => segments[i]).ToList())
                .ToList();
        }

        private static bool Cross(Segment a, Segment b)
        {
            var (horizontal, vertical) = a.Horizontal ? (a, b) : (b, a);
            return vertical.At >= horizontal.From - Tolerance && vertical.At <= horizontal.To + Tolerance
                && horizontal.At >= vertical.From - Tolerance && horizontal.At <= vertical.To + Tolerance;
        }

        private static List<double> Distinct(IEnumerable<double> positions)
        {
            var result = new List<double>();
            foreach (var position in positions.OrderBy(p => p))
                if (result.Count == 0 || position - result[^1] > Tolerance)
                    result.Add(position);

            return result;
        }

        /// <summary>
        /// Siatka z linii wierszy i kolumn; pola bez odcinka między sobą łączą się w jedną komórkę (scalenie w poziomie
        /// albo w pionie). Odcinek musi przechodzić przez środek granicy pola.
        /// </summary>
        private static PdfTable Build(List<double> rows, List<double> columns, List<Segment> horizontals, List<Segment> verticals)
        {
            int rowCount = rows.Count - 1, columnCount = columns.Count - 1;
            var parent = Enumerable.Range(0, rowCount * columnCount).ToArray();
            int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);
            void Union(int a, int b) => parent[Find(a)] = Find(b);

            for (var r = 0; r < rowCount; r++)
            {
                var middleY = (rows[r] + rows[r + 1]) / 2;
                for (var c = 0; c < columnCount; c++)
                {
                    var middleX = (columns[c] + columns[c + 1]) / 2;
                    if (c + 1 < columnCount && !verticals.Any(s => Math.Abs(s.At - columns[c + 1]) <= Tolerance
                            && s.From - Tolerance <= middleY && middleY <= s.To + Tolerance))
                        Union(r * columnCount + c, r * columnCount + c + 1);

                    if (r + 1 < rowCount && !horizontals.Any(s => Math.Abs(s.At - rows[r + 1]) <= Tolerance
                            && s.From - Tolerance <= middleX && middleX <= s.To + Tolerance))
                        Union(r * columnCount + c, (r + 1) * columnCount + c);
                }
            }

            var cells = Enumerable.Range(0, rowCount * columnCount)
                .GroupBy(Find)
                .Select(group =>
                {
                    int top = group.Min(i => i / columnCount), bottom = group.Max(i => i / columnCount);
                    int left = group.Min(i => i % columnCount), right = group.Max(i => i % columnCount);
                    return new PdfTableCell
                    {
                        Row = top,
                        Column = left,
                        RowSpan = bottom - top + 1,
                        ColumnSpan = right - left + 1,
                        Bounds = (columns[left], columns[right + 1], rows[bottom + 1], rows[top]),
                    };
                })
                .OrderBy(cell => cell.Row).ThenBy(cell => cell.Column)
                .ToList();

            return new PdfTable
            {
                Top = rows[0],
                Bottom = rows[^1],
                Left = columns[0],
                Right = columns[^1],
                Columns = columns,
                RowCount = rowCount,
                Cells = cells,
            };
        }
    }
}
