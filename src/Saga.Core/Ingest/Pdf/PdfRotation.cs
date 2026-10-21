#nullable enable
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace Saga.Core.Ingest.Pdf
{
    /// <summary>
    /// Treść złożona w poprzek strony: szeroka tabela załącznika na stronie pionowej, narysowana tekstem obróconym
    /// o ćwierć obrotu (bez obrotu samej strony — M.P. 2026 poz. 948). Litery takiej treści czyta się w układzie,
    /// w którym są poziome: obrót przenosi je razem z obramowaniem tabeli, a resztę adaptera zostawia bez zmian.
    /// </summary>
    internal static class PdfRotation
    {
        /// <summary>Udział liter obróconych, od którego strona ma treść w poprzek (pojedyncza komórka z pionowym napisem — nie).</summary>
        private const double TurnedShare = 0.5;

        /// <summary>Kierunek linii bazowej litery: +1 — w górę strony (obrót o 90°), −1 — w dół (o 270°), 0 — inny.</summary>
        private static int Quarter(Letter letter)
        {
            var dx = letter.EndBaseLine.X - letter.StartBaseLine.X;
            var dy = letter.EndBaseLine.Y - letter.StartBaseLine.Y;
            if (Math.Abs(dy) <= Math.Abs(dx) || Math.Abs(dy) < 1e-6)
                return 0;
            return dy > 0 ? 1 : -1;
        }

        /// <summary>
        /// Litery obrócone strony i kierunek ich obrotu, gdy stanowią większość liter strony; inaczej null — strona
        /// czyta się jak zwykle.
        /// </summary>
        internal static (List<Letter> Turned, int Direction)? Turned(IReadOnlyList<Letter> letters)
        {
            if (letters.Count == 0)
                return null;

            var up = letters.Where(letter => Quarter(letter) == 1).ToList();
            var down = letters.Where(letter => Quarter(letter) == -1).ToList();
            var (turned, direction) = up.Count >= down.Count ? (up, 1) : (down, -1);
            return turned.Count >= TurnedShare * letters.Count ? (turned, direction) : null;
        }

        /// <summary>
        /// Punkt w układzie treści obróconej: tekst biegnący w górę strony (+1) czyta się wzdłuż X′ = y, wiersze piętrzą
        /// się ku lewej krawędzi (Y′ = szerokość − x); biegnący w dół (−1) — X′ = wysokość − y, Y′ = x.
        /// </summary>
        internal static PdfPoint Point(PdfPoint point, int direction, double pageWidth, double pageHeight) =>
            direction > 0
                ? new PdfPoint(point.Y, pageWidth - point.X)
                : new PdfPoint(pageHeight - point.Y, point.X);

        /// <summary>Prostokąt w układzie treści obróconej (obrót o ćwierć obrotu zachowuje prostokąt osiowy).</summary>
        internal static PdfRectangle Rectangle(PdfRectangle box, int direction, double pageWidth, double pageHeight)
        {
            var a = Point(box.BottomLeft, direction, pageWidth, pageHeight);
            var b = Point(box.TopRight, direction, pageWidth, pageHeight);
            return new PdfRectangle(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
        }

        /// <summary>Litera przeniesiona do układu treści obróconej — dla wyodrębniania wierszy jest pozioma.</summary>
        internal static Letter Letter(Letter letter, int direction, double pageWidth, double pageHeight) =>
            new(letter.Value,
                Rectangle(letter.BoundingBox, direction, pageWidth, pageHeight),
                Rectangle(letter.GlyphRectangleLoose, direction, pageWidth, pageHeight),
                Point(letter.StartBaseLine, direction, pageWidth, pageHeight),
                Point(letter.EndBaseLine, direction, pageWidth, pageHeight),
                letter.Width,
                letter.FontSize,
                letter.FontDetails,
                letter.RenderingMode,
                letter.StrokeColor,
                letter.FillColor,
                letter.PointSize,
                letter.TextSequence);
    }
}
