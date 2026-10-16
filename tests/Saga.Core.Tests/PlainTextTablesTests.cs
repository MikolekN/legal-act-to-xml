using System.Text;
using Saga.Core.Ingest;

namespace Saga.Core.Tests
{
    /// <summary>Tabela w eksporcie tekstu LibreOffice: komórki wiersza rozdzielone tabulatorem.</summary>
    public class PlainTextTablesTests
    {
        private static IReadOnlyList<DocumentBlock> Read(params string[] lines)
        {
            using var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(string.Join("\n", lines)));
            return new PlainTextBlockReader().ReadBlocks(stream);
        }

        private static IEnumerable<(int Row, int Column, string Text)> Cells(IEnumerable<DocumentBlock> blocks) =>
            blocks.Where(block => block.TableCell is not null).Select(block => (block.TableCell!.Row, block.TableCell.Column, block.Text));

        [Fact]
        public void TabSeparatedRows_AreTableCells()
        {
            var blocks = Read(
                "1) wiersz oznaczony kodem A 01 otrzymuje brzmienie:",
                "A 01\tNaruszenie, w wyniku którego czyn został zakwalifikowany jako przestępstwo\tart. 173\t15",
                "2) wiersz oznaczony kodem A 09 otrzymuje brzmienie:");

            Assert.Equal(
                [(0, 0, "A 01"), (0, 1, "Naruszenie, w wyniku którego czyn został zakwalifikowany jako przestępstwo"), (0, 2, "art. 173"), (0, 3, "15")],
                Cells(blocks));
            Assert.Contains(blocks, block => block.TableCell is null && block.Text.EndsWith("wiersz oznaczony kodem A 09 otrzymuje brzmienie:"));
        }

        [Fact]
        public void DesignationTab_StaysInTheCell_AndTheCellListContinuesOnNextLines()
        {
            // Tabulator za oznaczeniem („1)⇥…") nie rozdziela komórek; kolejne pozycje wyliczenia należą do ostatniej komórki.
            var blocks = Read(
                "Efekty kształcenia\tKryteria weryfikacji",
                "1)\tcharakteryzuje pojęcia bezpieczeństwa pracy\t1)\twymienia przepisy prawa",
                "2)\twyjaśnia znaczenie pojęć",
                "Art. 2. Rozporządzenie wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

            Assert.Equal(
                [(0, 0, "Efekty kształcenia"), (0, 1, "Kryteria weryfikacji"),
                 (1, 0, "1)\tcharakteryzuje pojęcia bezpieczeństwa pracy"), (1, 1, "1)\twymienia przepisy prawa"),
                 (1, 1, "2)\twyjaśnia znaczenie pojęć")],
                Cells(blocks));
            Assert.Contains(blocks, block => block.TableCell is null && block.Text.StartsWith("Art. 2."));
        }

        [Theory]
        [InlineData("OpłWyr\t–\topłatę wyrównawczą wyrażoną w złotych,")]
        [InlineData("2)\twiersz oznaczony kodem A 09 otrzymuje brzmienie:")]
        [InlineData("42a))\tpodmiot pobierający opłaty – podmiot pobierający opłaty za korzystanie z dróg;")]
        [InlineData("Art. 40. Dotychczasowe przepisy wydane na podstawie\tart. 14b ust. 3 zachowują moc.")]
        [InlineData("a)\tkaprala Służby Więziennej\t\t– 2 lata,\nb)\tstarszego kaprala Służby Więziennej\t\t– 2 lata,")]
        [InlineData("1)\t\tdo wytworzenia energii elektrycznej nie będą wykorzystywane:")]
        [InlineData("i)\tprzeprowadzania\tegzaminu na licencję maszynisty albo\tegzaminu na świadectwo maszynisty – nie może być wyższa")]
        public void LegendLine_DesignationTab_OrALoneTab_IsNotATable(string line)
        {
            Assert.DoesNotContain(Read(line, "Art. 41. Ustawa wchodzi w życie."), block => block.TableCell is not null);
        }
    }
}
