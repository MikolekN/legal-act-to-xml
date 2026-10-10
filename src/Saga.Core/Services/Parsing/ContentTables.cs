using Saga.Core.Ingest;
using Saga.Model;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Składa tabelę treści z bloków jednej tabeli źródłowej: wiersze i komórki według miejsca bloku
    /// (<see cref="DocumentBlock.TableCell"/>), akapity komórki w kolejności dokumentu.
    /// </summary>
    internal static class ContentTables
    {
        /// <param name="blocks">Kolejne bloki tej samej tabeli, także puste — pusta komórka też jest komórką.</param>
        internal static ContentTable FromBlocks(IEnumerable<DocumentBlock> blocks)
        {
            var table = new ContentTable();

            foreach (var row in blocks.Where(block => block.TableCell is not null).GroupBy(block => block.TableCell!.Row).OrderBy(row => row.Key))
            {
                var cells = row
                    .GroupBy(block => block.TableCell!.Column)
                    .OrderBy(cell => cell.Key)
                    // Dalszy ciąg komórki scalonej w pionie nie jest osobną komórką — niesie ją wiersz, w którym się zaczyna.
                    .Where(cell => !cell.First().TableCell!.ContinuesCellAbove)
                    .Select(cell => new ContentTableCell
                    {
                        ColumnSpan = cell.First().TableCell!.ColumnSpan,
                        RowSpan = cell.First().TableCell!.RowSpan,
                        Paragraphs = cell
                            .Select(block => block.Text.CollapseWhitespace().Trim())
                            .Where(text => text.Length > 0)
                            .ToList(),
                    })
                    .ToList();

                table.Rows.Add(new ContentTableRow { IsHeader = row.First().TableCell!.IsHeaderRow, Cells = cells });
            }

            return table;
        }
    }
}
