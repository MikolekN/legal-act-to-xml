using System.Globalization;
using System.Text.RegularExpressions;

namespace Saga.Core.Ingest
{
    /// <summary>
    /// Tabele w eksporcie tekstu LibreOffice. Wiersz tabeli to jeden wiersz pliku z komórkami rozdzielonymi tabulatorem
    /// („A 01⇥Naruszenie …⇥art. 173"); ten sam tabulator stoi jednak także za oznaczeniem jednostki („1)⇥tekst"), więc
    /// tabulator za oznaczeniem łączy się z tekstem i rozdziela komórki dopiero ten, który został. Dalsze pozycje
    /// wyliczenia w komórce stoją w kolejnych wierszach pliku („2)⇥wyjaśnia …") — należą do ostatniej komórki, dopóki
    /// ciągną jej numerację. Granic tabeli plik nie zapisuje, dlatego tabela zaczyna się tylko wierszem z komórkami,
    /// a wiersz tabeli bez sąsiada musi mieć co najmniej trzy komórki (pojedynczy tabulator w zdaniu tabelą nie jest);
    /// wiersz legendy wzoru („OpłWyr⇥–⇥opłatę …") zostaje tekstem.
    /// </summary>
    internal static class PlainTextTables
    {
        private const int LoneRowMinCells = 3;

        /// <summary>
        /// Oznaczenie jednostki, za którym LibreOffice stawia tabulator — część komórki, nie granica komórek; także
        /// z przyklejonym znacznikiem odnośnika albo indeksem górnym („42a))", „6b)83)", „Art. 5.1)").
        /// </summary>
        private static readonly Regex DesignationToken = new(
            @"^„?(?:(?:\d{1,3}[a-z]{0,3}|[a-z]{1,3}|[IVXLC]{1,6}|[A-Z])[.)]|[–‒-](?:\s*[–‒-])*|§\s*\d+[a-z]*\.|Art\.\s*\d+[a-z]*\.)\S{0,10}$",
            RegexOptions.Compiled);

        /// <summary>Wiersz legendy wzoru: krótki symbol, myślnik, objaśnienie („OpłWyr⇥–⇥opłatę …", „KAG –⇥sumę …").</summary>
        private static readonly Regex LegendLine = new(@"^„?[^\t]{1,25}?(?:\t\s*[–‒-]\s*\t|\s[–‒-]\s*\t)", RegexOptions.Compiled);

        /// <summary>Pozycja wyliczenia zapisana z tabulatorem: numer z nawiasem albo tiret.</summary>
        private static readonly Regex ItemLine = new(@"^„?(?:(?<number>\d{1,3})[a-z]{0,3}\)|(?<dash>[–‒-]))\t", RegexOptions.Compiled);

        internal static IReadOnlyList<DocumentBlock> Mark(IReadOnlyList<DocumentBlock> blocks)
        {
            var result = new List<DocumentBlock>(blocks.Count);
            var table = 0;
            var i = 0;
            while (i < blocks.Count)
            {
                if (Cells(blocks[i].Text) is not { } first)
                {
                    result.Add(blocks[i++]);
                    continue;
                }

                // Wiersze tabeli: każdy wiersz z komórkami zaczyna wiersz, pozycje wyliczenia dopisują się do ostatniej komórki.
                var rows = new List<List<List<DocumentBlock>>> { Row(blocks[i], first) };
                var j = i + 1;
                while (j < blocks.Count)
                {
                    if (Cells(blocks[j].Text) is { } cells)
                        rows.Add(Row(blocks[j], cells));
                    else if (ContinuesLastCell(rows[^1][^1], blocks[j].Text))
                        rows[^1][^1].Add(blocks[j]);
                    else
                        break;
                    j++;
                }

                // Wiersz bez sąsiadów: co najmniej trzy komórki i nie przepis z oznaczeniem rozstawiony tabulatorami.
                if (rows.Count == 1 && (rows[0].Count < LoneRowMinCells || DesignationToken.IsMatch(rows[0][0][0].Text.Split('\t')[0])))
                {
                    result.AddRange(blocks.Skip(i).Take(j - i));
                    i = j;
                    continue;
                }

                for (var row = 0; row < rows.Count; row++)
                    for (var column = 0; column < rows[row].Count; column++)
                    {
                        var place = new BlockTableCell { Table = table, Row = row, Column = column };
                        result.AddRange(rows[row][column].Select(block => block with { TableCell = place }));
                    }

                table++;
                i = j;
            }

            return result;
        }

        /// <summary>Komórki wiersza tabeli; null, gdy wiersz nie ma tabulatora rozdzielającego komórki.</summary>
        private static List<string>? Cells(string text)
        {
            if (!text.Contains('\t') || LegendLine.IsMatch(text))
                return null;

            var cells = new List<string>();
            var tokens = text.Split('\t');
            for (var k = 0; k < tokens.Length; k++)
            {
                var token = tokens[k].Trim();
                if (DesignationToken.IsMatch(token) && k + 1 < tokens.Length)
                {
                    // Oznaczenie łączy się z tekstem także przez kilka tabulatorów („1)⇥⇥do wytworzenia …").
                    while (k + 2 < tokens.Length && tokens[k + 1].Trim().Length == 0)
                        k++;
                    cells.Add(token + "\t" + tokens[++k].Trim());
                    continue;
                }

                cells.Add(token);
            }

            // Tabulator wyrównujący wartość w wierszu tekstu („a) kaprala⇥⇥– 2 lata,") to nie granica komórek:
            // dalsza „komórka" zaczyna się myślnikiem ze spacją.
            if (cells.Skip(1).Any(cell => AlignedValue.IsMatch(cell)))
                return null;

            return cells.Count >= 2 ? cells : null;
        }

        private static readonly Regex AlignedValue = new(@"^[–‒-]\s", RegexOptions.Compiled);

        private static List<List<DocumentBlock>> Row(DocumentBlock line, List<string> cells) =>
            cells.Select(cell => new List<DocumentBlock> { line with { Text = cell } }).ToList();

        /// <summary>Dalsza pozycja wyliczenia ostatniej komórki: kolejny numer albo kolejny tiret.</summary>
        private static bool ContinuesLastCell(List<DocumentBlock> cell, string text)
        {
            var item = ItemLine.Match(text);
            var last = ItemLine.Match(cell[^1].Text);
            if (!item.Success || !last.Success)
                return false;

            if (item.Groups["dash"].Success)
                return last.Groups["dash"].Success;

            return last.Groups["number"].Success
                && int.Parse(item.Groups["number"].Value, CultureInfo.InvariantCulture)
                    == int.Parse(last.Groups["number"].Value, CultureInfo.InvariantCulture) + 1;
        }
    }
}
