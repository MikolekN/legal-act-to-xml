using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Dalsza treść akapitu jednostki: akapit bez oznaczenia i tabela stojące w przepisie należą do jednostki,
    /// za którą stoją — nie giną i nie udają jednostek.
    /// </summary>
    public class UnitContinuationTests
    {
        private static LegalDocument Parse(params string[] lines) =>
            LegalDocumentParser.Parse(
                lines.Select(text => new DocumentBlock { Text = text }).ToList(),
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

        private static string[] ChildNames(XElement element) =>
            element.Elements().Select(e => e.Name.LocalName).ToArray();

        private static XElement Single(XElement xml, string name) =>
            xml.Descendants().Single(e => e.Name.LocalName == name);

        // ============================================================
        // Akapit bez oznaczenia
        // ============================================================

        [Fact]
        public void ParagraphWithoutADesignation_ContinuesTheUnitItFollows()
        {
            var document = Parse(
                "Art. 1. 1. Wysokość opłaty oblicza się według wzoru:",
                "O = S × P",
                "gdzie poszczególne symbole oznaczają:",
                "2. Opłatę wnosi się w terminie 14 dni.");

            var paragraphs = document.Articles.Single().Paragraphs;

            Assert.Equal(2, paragraphs.Count);
            Assert.Equal("Wysokość opłaty oblicza się według wzoru:", paragraphs[0].ContentText);
            Assert.Equal(
                new[] { "O = S × P", "gdzie poszczególne symbole oznaczają:" },
                paragraphs[0].Continuation.Select(block => block.Text));
            Assert.Empty(paragraphs[1].Continuation);
        }

        [Fact]
        public void Continuation_BelongsToTheDeepestOpenUnit()
        {
            var document = Parse(
                "Art. 1. Opłata zależy od:",
                "1) powierzchni lokalu:",
                "a) mieszkalnego,",
                "(z wyłączeniem pomieszczeń przynależnych)",
                "b) użytkowego;",
                "2) okresu najmu.");

            var letters = document.Articles.Single().Paragraphs.Single().Points[0].Letters;

            Assert.Equal(
                new[] { "(z wyłączeniem pomieszczeń przynależnych)" },
                letters[0].Continuation.Select(block => block.Text));
            Assert.Empty(letters[1].Continuation);
        }

        [Fact]
        public void Continuation_IsExportedAsFragments_BetweenTheSentenceAndTheEnumeration()
        {
            var document = Parse(
                "Art. 1. 1. Wysokość opłaty oblicza się według wzoru:",
                "O = S × P[2]",
                "gdzie poszczególne symbole oznaczają:",
                "1) O – opłatę;",
                "2) S – stawkę.",
                "2. Opłatę wnosi się w terminie 14 dni.");

            var first = LegalDocumentConverter.Convert(document).Descendants().First(e => e.Name.LocalName == "ustep");

            // Schemat: zdania, potem dalsza treść akapitu, potem wyliczenie.
            Assert.Equal(new[] { "nr", "p", "fragment", "fragment", "punkt", "punkt" }, ChildNames(first));

            var fragments = first.Elements().Where(e => e.Name.LocalName == "fragment").ToList();
            Assert.Equal("gdzie poszczególne symbole oznaczają:", fragments[1].Value);
            // Indeks górny w dalszej treści jest elementem, jak w zdaniu.
            Assert.Equal("sup", fragments[0].Elements().Single().Name.LocalName);
        }

        [Fact]
        public void HeadingOfAnotherAct_IsNotAContinuationOfTheLastProvision()
        {
            // Tekst jednolity: za przepisami obwieszczenia zaczyna się akt — jego strefa tytułowa nie jest
            // dalszą treścią ostatniego przepisu obwieszczenia.
            var document = Parse(
                "Art. 1. Ogłasza się jednolity tekst ustawy.",
                "Załącznik do obwieszczenia Marszałka Sejmu Rzeczypospolitej Polskiej z dnia 5 maja 2025 r. (Dz. U. poz. 700)",
                "USTAWA",
                "z dnia 5 maja 2020 r.");

            Assert.Empty(document.Articles.Single().Paragraphs.Single().Continuation);
        }

        [SchemaPlFact]
        public void UnitWithContinuation_IsSchemaValid_InTheActBody()
        {
            var document = Parse(
                "Art. 1. 1. Wysokość opłaty oblicza się według wzoru:",
                "O = S × P",
                "gdzie poszczególne symbole oznaczają:",
                "1) O – opłatę;",
                "2) S – stawkę.",
                "2. Opłatę wnosi się w terminie 14 dni.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

            Assert.Empty(SchemaPl.Validator.ValidateActBody(LegalDocumentConverter.Convert(document).Elements().First()));
        }

        // ============================================================
        // Tabela w treści przepisu
        // ============================================================

        private static string P(string text) => $"<w:p><w:r><w:t>{text}</w:t></w:r></w:p>";

        private static string Cell(string text) => $"<w:tc><w:tcPr/>{P(text)}</w:tc>";

        private static string Row(params string[] cells) => $"<w:tr>{string.Concat(cells)}</w:tr>";

        private static LegalDocument ParseWithTable()
        {
            // Komórki wyglądają jak jednostki („1.", „1)") — w tabeli nimi nie są.
            var body =
                P("USTAWA") + P("z dnia 14 czerwca 2024 r.") + P("o opłatach lotniczych") +
                P("Art. 1. Stawki opłat określa tabela:") +
                "<w:tbl>" +
                    $"<w:tr><w:trPr><w:tblHeader/></w:trPr>{Cell("Lp.")}{Cell("Czynność")}{Cell("Opłata")}</w:tr>" +
                    Row(Cell("1."), Cell("Rozpatrzenie wniosku o uznanie certyfikatu"), Cell("1500")) +
                    Row(Cell("2."), Cell("1) wydanie potwierdzenia odbioru"), Cell("500")) +
                "</w:tbl>" +
                P("Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

            using var stream = new MemoryStream(DocxFixtures.WithBodyXml(body));
            return LegalDocumentParser.Parse(stream, "ustawa.docx", new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
        }

        [Fact]
        public void TableInAProvision_IsItsContinuation_NotASeriesOfUnits()
        {
            var document = ParseWithTable();

            Assert.Equal(new[] { "art_1", "art_2" }, document.Articles.Select(a => a.Id));

            var paragraph = document.Articles.First().Paragraphs.Single();
            Assert.Empty(paragraph.Points);

            var table = Assert.Single(paragraph.Continuation).Table!;
            Assert.Equal(new[] { true, false, false }, table.Rows.Select(row => row.IsHeader));
            Assert.Equal(
                new[] { "2.", "1) wydanie potwierdzenia odbioru", "500" },
                table.Rows[2].Cells.Select(cell => cell.Paragraphs.Single()));
        }

        [Fact]
        public void TableInAProvision_IsExportedAfterItsSentence()
        {
            var article = LegalDocumentConverter.Convert(ParseWithTable()).Descendants().First(e => e.Name.LocalName == "artykul");
            var paragraph = article.Elements().Single(e => e.Name.LocalName.StartsWith("ustep"));

            Assert.Equal(new[] { "p", "tabela" }, ChildNames(paragraph));
            Assert.Equal(3, Single(paragraph, "tbody").Elements().Count());
        }

        [Fact]
        public void TableStartedInsideAnAmendment_IsNotSplitBetweenTwoPaths()
        {
            // Wiersz tabeli cytowany w nowelizacji: pierwszą komórkę bierze nowelizacja, więc reszta tabeli
            // nie może stać się osobną, niepełną tabelą przepisu zmieniającego.
            var body =
                P("USTAWA") + P("z dnia 14 czerwca 2024 r.") + P("o zmianie ustawy o transporcie drogowym") +
                P("Art. 1. W ustawie z dnia 6 września 2001 r. o transporcie drogowym (Dz. U. z 2024 r. poz. 1539) " +
                  "w załączniku nr 1 w tabeli po lp. 2a dodaje się lp. 2b w brzmieniu:") +
                "<w:tbl>" + Row(Cell("2b."), Cell("Wykonywanie przewozu z naruszeniem obowiązku"), Cell("2000")) + "</w:tbl>" +
                P("Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

            using var stream = new MemoryStream(DocxFixtures.WithBodyXml(body));
            var document = LegalDocumentParser.Parse(stream, "ustawa.docx", new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

            var units = document.Articles.SelectMany(article => article.Paragraphs).ToList();
            Assert.DoesNotContain(units.SelectMany(unit => unit.Continuation), block => block.Table is not null);
        }

        [SchemaPlFact]
        public void ProvisionWithATable_IsSchemaValid_InTheActBody()
        {
            Assert.Empty(SchemaPl.Validator.ValidateActBody(LegalDocumentConverter.Convert(ParseWithTable()).Elements().First()));
        }
    }
}
