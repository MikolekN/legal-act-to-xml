using System.Text.RegularExpressions;
using Saga.Model;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Cudzysłowy graniczne cytatu stojące w komórkach cytowanej tabeli (ADR-0050): otwierający w pierwszej
    /// niepustej komórce, zamykający — z interpunkcją komendy — w ostatniej. Znak należy do cytatu, nie do tabeli,
    /// więc jest z niej zdejmowany tak samo jak z pierwszego i ostatniego akapitu treści cytowanej.
    ///
    /// Redaktorzy stawiają go zwykle w osobnej kolumnie, pustej poza tą jedną komórką. Kolumna, w której nie było
    /// nic oprócz znaku, kolumną tabeli nie jest i znika razem z nim.
    /// </summary>
    internal static class QuotedTableBoundaries
    {
        private static readonly Regex OpeningQuote = new(
            $@"^\s*[{QuotationMarks.OpeningOrStraight}]", RegexOptions.Compiled);

        private static readonly Regex ClosingQuote = new(
            $@"[{QuotationMarks.ClosingOrStraight}]\s*(?<punct>[,;.])?\s*$", RegexOptions.Compiled);

        /// <summary>Zdejmuje cudzysłów otwierający cytat z pierwszej niepustej komórki tabeli; false, gdy go tam nie ma.</summary>
        internal static bool TakeOpening(ContentTable table)
        {
            if (FirstText(table) is not var (cell, index))
                return false;

            var text = cell.Paragraphs[index];
            // Cudzysłów sparowany w tej samej komórce („„spółka akcyjna" i inne") otwiera wyrażenie, nie cytat.
            if (!OpeningQuote.IsMatch(text) || Balance(text) <= 0)
                return false;

            Replace(cell, index, OpeningQuote.Replace(text, string.Empty, 1));
            if (ReferenceEquals(cell, table.Rows[0].Cells[0]))
                DropEdgeColumnIfEmpty(table, first: true);
            return true;
        }

        /// <summary>
        /// Zdejmuje cudzysłów zamykający cytat z ostatniej niepustej komórki tabeli. Zwraca interpunkcję stojącą
        /// za nim (pustą, gdy jej nie ma) albo null, gdy cudzysłowu zamykającego tam nie ma.
        /// </summary>
        internal static string? TakeClosing(ContentTable table)
        {
            if (LastText(table) is not var (cell, index))
                return null;

            var text = cell.Paragraphs[index];
            var match = ClosingQuote.Match(text);
            if (!match.Success || Balance(text) >= 0)
                return null;

            Replace(cell, index, text[..match.Index].TrimEnd());
            if (ReferenceEquals(cell, table.Rows[^1].Cells[^1]))
                DropEdgeColumnIfEmpty(table, first: false);
            return match.Groups["punct"].Value;
        }

        /// <summary>
        /// Przewaga cudzysłowów otwierających nad zamykającymi w tekście: dodatnia — tekst zostawia cudzysłów
        /// otwarty, ujemna — zamyka cudzysłów otwarty wcześniej. Prosty cudzysłów jest dwuznaczny: pary są
        /// obojętne, a nieparzysty liczy się zgodnie z miejscem, w którym stoi (na początku albo na końcu).
        /// </summary>
        private static int Balance(string text)
        {
            var balance = 0;
            var straight = 0;
            foreach (var character in text)
            {
                if (character == QuotationMarks.Opening)
                    balance++;
                else if (QuotationMarks.IsClosing(character))
                    balance--;
                else if (character == QuotationMarks.Straight)
                    straight++;
            }

            if (straight % 2 == 1)
                balance += text.TrimStart().StartsWith(QuotationMarks.Straight) ? 1 : -1;

            return balance;
        }

        private static (ContentTableCell Cell, int Index)? FirstText(ContentTable table)
        {
            foreach (var cell in table.Rows.SelectMany(row => row.Cells))
            {
                var index = cell.Paragraphs.FindIndex(paragraph => !string.IsNullOrWhiteSpace(paragraph));
                if (index >= 0)
                    return (cell, index);
            }

            return null;
        }

        private static (ContentTableCell Cell, int Index)? LastText(ContentTable table)
        {
            foreach (var cell in table.Rows.SelectMany(row => row.Cells).Reverse())
            {
                var index = cell.Paragraphs.FindLastIndex(paragraph => !string.IsNullOrWhiteSpace(paragraph));
                if (index >= 0)
                    return (cell, index);
            }

            return null;
        }

        private static void Replace(ContentTableCell cell, int index, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                cell.Paragraphs.RemoveAt(index);
            else
                cell.Paragraphs[index] = text;
        }

        /// <summary>
        /// Usuwa skrajną kolumnę, jeśli po zdjęciu cudzysłowu nie zostało w niej nic: w każdym wierszu skrajna
        /// komórka jest pusta i niescalona. Tabela o jednej kolumnie zostaje.
        /// </summary>
        private static void DropEdgeColumnIfEmpty(ContentTable table, bool first)
        {
            ContentTableCell Edge(ContentTableRow row) => first ? row.Cells[0] : row.Cells[^1];

            if (table.Rows.Any(row => row.Cells.Count < 2 || !IsEmptyPlainCell(Edge(row))))
                return;

            foreach (var row in table.Rows)
                row.Cells.RemoveAt(first ? 0 : row.Cells.Count - 1);
        }

        private static bool IsEmptyPlainCell(ContentTableCell cell) =>
            cell.ColumnSpan == 1 && cell.RowSpan == 1 && cell.Paragraphs.All(string.IsNullOrWhiteSpace);
    }
}
