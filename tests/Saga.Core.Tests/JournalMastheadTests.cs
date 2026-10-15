using System.Text;
using Saga.Core.Ingest;
using Saga.Core.Ingest.Pdf;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Winieta dziennika („DZIENNIK USTAW / RZECZYPOSPOLITEJ POLSKIEJ / Warszawa, dnia … / Poz. …") w PDF i TXT:
    /// bez nagłówka strony DOCX stoi na początku treści i ma dać te same dane ogłoszenia co nagłówek.
    /// </summary>
    public class JournalMastheadTests
    {
        private static readonly string[] Masthead =
        [
            "DZIENNIK USTAW",
            "RZECZYPOSPOLITEJ POLSKIEJ",
            "Warszawa, dnia 12 stycznia 2026 r.",
            "Poz. 26",
        ];

        private static readonly string[] Act =
        [
            "USTAWA",
            "z dnia 18 grudnia 2025 r.",
            "o zmianie ustawy o systemie ubezpieczeń społecznych",
            "Art. 1. W ustawie z dnia 13 października 1998 r. o systemie ubezpieczeń społecznych wprowadza się zmiany.",
            "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
        ];

        private static IReadOnlyList<DocumentBlock> ReadText(IEnumerable<string> lines)
        {
            using var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(string.Join("\n", lines)));
            return new PlainTextBlockReader().ReadBlocks(stream);
        }

        private static LegalDocument ParseText(IEnumerable<string> lines)
        {
            using var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(string.Join("\n", lines)));
            var result = LegalDocumentParser.Parse(stream, "akt.txt", new ParseOptions { Policy = ParsePolicy.AlwaysParse });
            Assert.NotNull(result.Document);
            return result.Document!;
        }

        [Fact]
        public void Text_MastheadAtTheStart_IsJournalHeader()
        {
            var blocks = ReadText(Masthead.Concat(Act));

            Assert.All(blocks.Take(4), block => Assert.Equal(BlockRole.JournalHeader, block.Role));
            Assert.All(blocks.Skip(4), block => Assert.Equal(BlockRole.Body, block.Role));
        }

        [Fact]
        public void Text_PublisherAndRepublicInOneLine_IsJournalHeader()
        {
            var blocks = ReadText(new[] { "DZIENNIK USTAW RZECZYPOSPOLITEJ POLSKIEJ", "Warszawa, dnia 12 stycznia 2026 r.", "Poz. 26" }.Concat(Act));

            Assert.Equal(3, blocks.Count(block => block.Role == BlockRole.JournalHeader));
            Assert.Equal("USTAWA", blocks.First(block => block.Role == BlockRole.Body).Text);
        }

        [Fact]
        public void Text_MonitorPolskiBrokenAcrossLines_IsJournalHeader()
        {
            // Skład PDF łamie dużą nazwę dziennika: „MONITOR" / „POLSKI DZIENNIK URZĘDOWY …", data i pozycja w jednym wierszu.
            var blocks = ReadText(new[] { "MONITOR", "POLSKI DZIENNIK URZĘDOWY RZECZYPOSPOLITEJ POLSKIEJ", "Warszawa, dnia 4 sierpnia 2025 r. Poz. 719" }
                .Concat(Act));

            Assert.Equal(3, blocks.Count(block => block.Role == BlockRole.JournalHeader));
            Assert.Equal("USTAWA", blocks.First(block => block.Role == BlockRole.Body).Text);
        }

        [Fact]
        public void Text_MastheadLinesWithoutPublisher_StayBody()
        {
            // Bez nazwy dziennika na początku „Warszawa, dnia …" i „Poz. …" nie są winietą.
            var blocks = ReadText(new[] { "Warszawa, dnia 12 stycznia 2026 r.", "Poz. 26" }.Concat(Act));

            Assert.All(blocks, block => Assert.Equal(BlockRole.Body, block.Role));
        }

        [Fact]
        public void Text_PublisherLineInsideTheAct_StaysBody()
        {
            // Winieta stoi tylko na początku dokumentu — ten sam wiersz dalej jest treścią.
            var blocks = ReadText(Act.Concat(Masthead));

            Assert.All(blocks, block => Assert.Equal(BlockRole.Body, block.Role));
        }

        [Fact]
        public void Text_Masthead_GivesTheAnnouncementData()
        {
            var document = ParseText(Masthead.Concat(Act));

            Assert.Equal(PublisherType.DziennikUstaw, document.SourceJournal.Publisher);
            Assert.Equal(2026, document.SourceJournal.Year);
            Assert.Equal([26], document.SourceJournal.Positions);
            Assert.Equal(new DateTime(2026, 1, 12), document.AnnouncementDate);
            Assert.Equal("USTAWA", document.KindLine);
        }

        [Fact]
        public void Pdf_MastheadAtTheTopOfTheFirstPage_IsJournalHeader()
        {
            var builder = new TestPdfBuilder().AddPage()
                .AddText("DZIENNIK USTAW", 142, 757, 40)
                .AddText("RZECZYPOSPOLITEJ POLSKIEJ", 142, 716, 24)
                .AddText("Warszawa, dnia 12 stycznia 2026 r.", 199, 666, 14)
                .AddText("Poz. 26", 277, 638, 14);
            // Interlinia treści jak w Dzienniku Ustaw (ok. 14 pt) — odstępy winiety są od niej kilkakrotnie większe.
            var y = 586.0;
            foreach (var line in Act.Concat(Enumerable.Range(3, 8).Select(n => $"Art. {n}. Przepis wypełniający stronę.")))
            {
                builder.AddText(line, 57, y);
                y -= 14;
            }

            using var stream = new MemoryStream(builder.Build());
            var blocks = new PdfBlockReader().ReadBlocks(stream);

            Assert.Equal(Masthead, blocks.Where(block => block.Role == BlockRole.JournalHeader).Select(block => block.Text));
            Assert.StartsWith("USTAWA", blocks.First(block => block.Role == BlockRole.Body).Text);
        }
    }
}
