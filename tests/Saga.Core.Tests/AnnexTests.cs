using Saga.Core.Ingest;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Załączniki aktu w modelu: treść za nagłówkiem „Załącznik(i) do ustawy z dnia …" nie należy do
    /// struktury aktu, ale nie ginie — trafia do <see cref="LegalDocument.Annexes"/>.
    /// </summary>
    public class AnnexTests
    {
        private const string AttachedToStyle = "TEKSTZacznikido";
        private const string DesignationStyle = "OZNZACZNIKAwskazanienrzacznika";
        private const string TitleStyle = "TYTZALtytuzacznika";

        private static readonly string[] Act =
        [
            "USTAWA", "z dnia 23 stycznia 2026 r.", "o krajowym systemie cyberbezpieczeństwa",
            "Art. 1. Sektory określają załączniki do ustawy.",
            "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
        ];

        private static DocumentBlock Block(string text, string? style = null, BlockAlignment? alignment = null) => new()
        {
            Text = text,
            StyleId = style,
            Layout = alignment is null ? null : new BlockLayoutInfo { Alignment = alignment },
        };

        private static LegalDocument Parse(params DocumentBlock[] annexBlocks) =>
            LegalDocumentParser.Parse(
                [.. Act.Select(text => Block(text)), .. annexBlocks],
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

        [Fact]
        public void Annexes_UnderACommonHeading_StartAtTheirDesignations()
        {
            // Układ przykładu autorów schematu (DU/2026/252): wskazanie aktu raz, potem „Załącznik nr N" i tytuł.
            var document = Parse(
                Block("Załączniki do ustawy z dnia 23 stycznia 2026 r.", AttachedToStyle),
                Block("(Dz. U. poz. 252)", AttachedToStyle),
                Block("Załącznik nr 1", DesignationStyle),
                Block("SEKTORY KLUCZOWE", TitleStyle),
                Block("Energia"),
                Block("Załącznik nr 2", DesignationStyle),
                Block("SEKTORY WAŻNE", TitleStyle),
                Block("Usługi pocztowe"),
                Block("Produkcja"));

            Assert.Equal(new[] { "zal_1", "zal_2" }, document.Annexes.Select(a => a.Id));
            Assert.Equal(new[] { "1", "2" }, document.Annexes.Select(a => a.Number));
            Assert.Equal(new[] { "Załącznik nr 1", "Załącznik nr 2" }, document.Annexes.Select(a => a.Heading));
            Assert.Equal(new[] { "SEKTORY KLUCZOWE", "SEKTORY WAŻNE" }, document.Annexes.Select(a => a.Title));

            // Wskazanie aktu stoi raz i niesie je pierwszy załącznik.
            Assert.Equal(new[] { "Załączniki do ustawy z dnia 23 stycznia 2026 r.", "(Dz. U. poz. 252)" }, document.Annexes[0].AttachedTo);
            Assert.Empty(document.Annexes[1].AttachedTo);

            Assert.Equal(new[] { "Energia" }, document.Annexes[0].Content.Select(b => b.Text));
            Assert.Equal(new[] { "Usługi pocztowe", "Produkcja" }, document.Annexes[1].Content.Select(b => b.Text));
        }

        [Fact]
        public void SingleAnnex_HasNoNumber_AndNoEId()
        {
            var document = Parse(
                Block("Załącznik do ustawy z dnia 23 stycznia 2026 r. (Dz. U. poz. 252)"),
                // Tytuł bez własnego stylu: rozstrzyga wyśrodkowanie.
                Block("WYKAZ SEKTORÓW", style: "ARTartustawynprozporzdzenia", alignment: BlockAlignment.Center),
                Block("Energia"));

            var annex = Assert.Single(document.Annexes);
            Assert.Null(annex.Number);
            Assert.Null(annex.Heading);
            Assert.Equal(string.Empty, annex.Id);
            Assert.Equal(new[] { "Załącznik do ustawy z dnia 23 stycznia 2026 r. (Dz. U. poz. 252)" }, annex.AttachedTo);
            Assert.Equal("WYKAZ SEKTORÓW", annex.Title);
            Assert.Equal(new[] { "Energia" }, annex.Content.Select(b => b.Text));
        }

        [Fact]
        public void CenteredLinesAfterTheTitle_AreContent()
        {
            // Załącznik z rysunkami: podpisy są wyśrodkowane jak tytuł, ale tytułem jest tylko pierwszy wiersz.
            var document = Parse(
                Block("Załącznik do ustawy z dnia 23 stycznia 2026 r."),
                Block("SZTANDAR JEDNOSTKI WOJSKOWEJ", alignment: BlockAlignment.Center),
                Block("strona główna", alignment: BlockAlignment.Center),
                Block("strona odwrotna", alignment: BlockAlignment.Center));

            var annex = Assert.Single(document.Annexes);
            Assert.Equal("SZTANDAR JEDNOSTKI WOJSKOWEJ", annex.Title);
            Assert.Equal(new[] { "strona główna", "strona odwrotna" }, annex.Content.Select(b => b.Text));
        }

        [Fact]
        public void NoteUnderTheHeading_IsContent_NotAnotherHeadingLine()
        {
            // Tekst jednolity: „(pominięty)" stoi w stylu wskazania aktu, ale wskazaniem nie jest.
            var document = Parse(
                Block("Załącznik do ustawy z dnia 13 kwietnia 2016 r. (Dz. U. z 2025 r. poz. 568)", AttachedToStyle),
                Block("(pominięty)", AttachedToStyle));

            var annex = Assert.Single(document.Annexes);
            Assert.Single(annex.AttachedTo);
            Assert.Equal(new[] { "(pominięty)" }, annex.Content.Select(b => b.Text));
        }

        [Theory]
        [InlineData("Załącznik nr 4a", "4a")]
        [InlineData("Załącznik nr 3[2)]", "3")]
        [InlineData("Załącznik nr 2 (uchylony)", "2")]
        public void Designation_MayCarryALetterAFootnoteMarkerOrANote(string heading, string number)
        {
            var document = Parse(
                Block("Załączniki do ustawy z dnia 23 stycznia 2026 r."),
                Block(heading));

            var annex = Assert.Single(document.Annexes);
            Assert.Equal(number, annex.Number);
            Assert.Equal(heading, annex.Heading);
        }

        [Fact]
        public void SentenceStartingLikeADesignation_IsContent()
        {
            var document = Parse(
                Block("Załączniki do ustawy z dnia 23 stycznia 2026 r."),
                Block("Załącznik nr 1"),
                Block("WZÓR WNIOSKU", TitleStyle),
                Block("Załącznik nr 2 określa wzór oświadczenia składanego wraz z wnioskiem."));

            var annex = Assert.Single(document.Annexes);
            Assert.Equal(
                new[] { "Załącznik nr 2 określa wzór oświadczenia składanego wraz z wnioskiem." },
                annex.Content.Select(b => b.Text));
        }

        [Fact]
        public void AnnexContent_StaysOutOfTheActStructure()
        {
            // Wyliczenie w załączniku wygląda jak jednostki aktu — nie może dokleić się do ostatniego artykułu.
            var document = Parse(
                Block("Załącznik do ustawy z dnia 23 stycznia 2026 r."),
                Block("WYMOGI", TitleStyle),
                Block("1) inwentaryzację produktów;", "PKTpunkt"),
                Block("2) kontrolowanie wersji.", "PKTpunkt"));

            Assert.Equal(new[] { "art_1", "art_2" }, document.Articles.Select(a => a.Id));
            Assert.Empty(document.Articles.Last().Paragraphs.Single().Points);
            Assert.Equal(
                new[] { "1) inwentaryzację produktów;", "2) kontrolowanie wersji." },
                Assert.Single(document.Annexes).Content.Select(b => b.Text));
        }

        [Fact]
        public void ActWithoutAnAnnex_HasNone()
        {
            Assert.Empty(Parse().Annexes);
        }

        // ============================================================
        // Tabela w załączniku
        // ============================================================

        private static string P(string text, string paragraphProperties = "") =>
            $"<w:p><w:pPr>{paragraphProperties}</w:pPr><w:r><w:t>{text}</w:t></w:r></w:p>";

        private static string Cell(string text, string properties = "") => $"<w:tc><w:tcPr>{properties}</w:tcPr>{P(text)}</w:tc>";

        private static string Row(params string[] cells) => $"<w:tr>{string.Concat(cells)}</w:tr>";

        [Fact]
        public void AnnexTable_KeepsRowsCellsAndMerges()
        {
            var body = string.Concat(Act.Select(line => P(line))) +
                P("Załącznik do ustawy z dnia 23 stycznia 2026 r.") +
                P("SEKTORY KLUCZOWE", @"<w:jc w:val=""center""/>") +
                "<w:tbl>" +
                    $"<w:tr><w:trPr><w:tblHeader/></w:trPr>{Cell("Sektor")}{Cell("Podsektor")}</w:tr>" +
                    Row(Cell("Energia", @"<w:vMerge w:val=""restart""/>"), Cell("gaz")) +
                    Row(Cell("", "<w:vMerge/>"), Cell("ropa")) +
                    Row(Cell("Transport", @"<w:gridSpan w:val=""2""/>")) +
                "</w:tbl>" +
                P("Objaśnienie pod tabelą.");

            using var stream = new MemoryStream(DocxFixtures.WithBodyXml(body));
            var document = LegalDocumentParser.Parse(stream, "ustawa.docx",
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

            var annex = Assert.Single(document.Annexes);
            Assert.Equal("SEKTORY KLUCZOWE", annex.Title);

            // Treść w kolejności dokumentu: tabela, potem akapit.
            Assert.Equal(2, annex.Content.Count);
            Assert.Equal("Objaśnienie pod tabelą.", annex.Content[1].Text);

            var table = annex.Content[0].Table!;
            Assert.Equal(new[] { true, false, false, false }, table.Rows.Select(r => r.IsHeader));
            Assert.Equal(new[] { "Sektor", "Podsektor" }, table.Rows[0].Cells.Select(c => c.Paragraphs.Single()));

            // Komórka scalona w pionie stoi w wierszu, w którym się zaczyna; wiersz pod nią ma o jedną mniej.
            Assert.Equal(new[] { 2, 1 }, table.Rows[1].Cells.Select(c => c.RowSpan));
            Assert.Equal(new[] { "ropa" }, table.Rows[2].Cells.Select(c => c.Paragraphs.Single()));
            Assert.Equal(2, Assert.Single(table.Rows[3].Cells).ColumnSpan);
        }
    }
}
