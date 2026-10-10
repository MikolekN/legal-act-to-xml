using DocumentFormat.OpenXml;
using Word = DocumentFormat.OpenXml.Wordprocessing;

#nullable enable

namespace Saga.Core.Ingest
{
    /// <summary>
    /// Tabele dokumentu DOCX: dla akapitu w komórce — która to tabela, wiersz i kolumna siatki oraz scalenia.
    /// Tabela zagnieżdżona w komórce jest osobną tabelą; akapit należy do najbliższej.
    /// </summary>
    internal sealed class DocxTables
    {
        private readonly Dictionary<Word.TableCell, BlockTableCell> _cells;

        private DocxTables(Dictionary<Word.TableCell, BlockTableCell> cells) => _cells = cells;

        internal static DocxTables Read(Word.Document document)
        {
            var cells = new Dictionary<Word.TableCell, BlockTableCell>();
            var index = 0;
            foreach (var table in document.Descendants<Word.Table>())
                ReadTable(table, index++, cells);

            return new DocxTables(cells);
        }

        /// <summary>Komórka, w której stoi akapit; null poza tabelą.</summary>
        internal BlockTableCell? CellOf(Word.Paragraph paragraph) =>
            paragraph.Ancestors<Word.TableCell>().FirstOrDefault() is { } cell ? _cells.GetValueOrDefault(cell) : null;

        private static void ReadTable(Word.Table table, int tableIndex, Dictionary<Word.TableCell, BlockTableCell> cells)
        {
            var placed = new List<(Word.TableCell Element, BlockTableCell Cell, bool StartsMerge)>();

            // Wiersze i komórki tej tabeli — także owinięte kontrolką treści, ale bez tabel zagnieżdżonych.
            var rows = OwnDescendants<Word.TableRow, Word.Table>(table).ToList();
            for (var row = 0; row < rows.Count; row++)
            {
                var rowProperties = rows[row].TableRowProperties;
                var isHeader = rowProperties?.GetFirstChild<Word.TableHeader>() is { } header
                               && (header.Val is null || header.Val.Value != Word.OnOffOnlyValues.Off);
                var column = rowProperties?.GetFirstChild<Word.GridBefore>()?.Val?.Value ?? 0;

                foreach (var element in OwnDescendants<Word.TableCell, Word.TableRow>(rows[row]))
                {
                    var properties = element.TableCellProperties;
                    var span = Math.Max(1, properties?.GridSpan?.Val?.Value ?? 1);

                    // w:vMerge bez wartości kontynuuje scalenie; „restart" je zaczyna.
                    var merge = properties?.VerticalMerge;
                    var startsMerge = merge?.Val is { } value && value.Value == Word.MergedCellValues.Restart;

                    placed.Add((element, new BlockTableCell
                    {
                        Table = tableIndex,
                        Row = row,
                        Column = column,
                        ColumnSpan = span,
                        ContinuesCellAbove = merge is not null && !startsMerge,
                        IsHeaderRow = isHeader,
                    }, startsMerge));

                    column += span;
                }
            }

            // Wiersze z kontynuacją scalenia, osobno dla każdej kolumny siatki.
            var continued = placed
                .Where(p => p.Cell.ContinuesCellAbove)
                .GroupBy(p => p.Cell.Column)
                .ToDictionary(group => group.Key, group => group.Select(p => p.Cell.Row).ToHashSet());

            foreach (var (element, cell, startsMerge) in placed)
                cells[element] = startsMerge ? cell with { RowSpan = RowSpanOf(cell, continued) } : cell;
        }

        /// <summary>Wiersze zajęte przez komórkę zaczynającą scalenie: ona sama i nieprzerwany ciąg kontynuacji pod nią.</summary>
        private static int RowSpanOf(BlockTableCell start, Dictionary<int, HashSet<int>> continuedByColumn)
        {
            var span = 1;
            if (continuedByColumn.TryGetValue(start.Column, out var rows))
                while (rows.Contains(start.Row + span))
                    span++;

            return span;
        }

        /// <summary>Potomkowie typu <typeparamref name="T"/>, których najbliższym przodkiem typu <typeparamref name="TOwner"/> jest ten element.</summary>
        private static IEnumerable<T> OwnDescendants<T, TOwner>(TOwner owner)
            where T : OpenXmlElement
            where TOwner : OpenXmlElement =>
            owner.Descendants<T>().Where(descendant => descendant.Ancestors<TOwner>().First() == owner);
    }
}
