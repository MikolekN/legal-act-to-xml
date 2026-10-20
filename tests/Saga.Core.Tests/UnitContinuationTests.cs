using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Core.Services.Parsing;
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

        // ============================================================
        // Tabela w treści nowelizacji
        // ============================================================

        private static string Styled(string text, string style) =>
            $"<w:p><w:pPr><w:pStyle w:val=\"{style}\"/></w:pPr><w:r><w:t>{text}</w:t></w:r></w:p>";

        private static LegalDocument ParseBody(string body)
        {
            using var stream = new MemoryStream(DocxFixtures.WithBodyXml(body));
            return LegalDocumentParser.Parse(stream, "ustawa.docx", new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
        }

        // Wiersz tabeli dodawany nowelizacją: tabela stoi tuż po komendzie, bez cudzysłowów.
        private static LegalDocument ParseWithQuotedRow() => ParseBody(
            P("USTAWA") + P("z dnia 14 czerwca 2024 r.") + P("o zmianie ustawy o transporcie drogowym") +
            P("Art. 1. W ustawie z dnia 6 września 2001 r. o transporcie drogowym (Dz. U. z 2024 r. poz. 1539) " +
              "w załączniku nr 1 wprowadza się następujące zmiany:") +
            P("1) w tabeli po lp. 2a dodaje się lp. 2b w brzmieniu:") +
            "<w:tbl>" + Row(Cell("2b."), Cell("Wykonywanie przewozu z naruszeniem obowiązku"), Cell("2000")) + "</w:tbl>" +
            P("2) uchyla się lp. 7.") +
            P("Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia."));

        [Fact]
        public void TableAfterACommand_IsTheQuotedContentOfTheAmendment()
        {
            var document = ParseWithQuotedRow();
            var points = document.Articles.First().Paragraphs.Single().Points;

            // Tabela jest całą treścią cytowaną: zamyka ją jej koniec, więc „2)" jest już następnym punktem aktu.
            Assert.Equal(new[] { "art_1__pkt_1", "art_1__pkt_2" }, points.Select(p => p.Id));

            var content = points[0].Amendment!.Content!;
            var table = Assert.Single(content.Tables);
            Assert.Equal(
                new[] { "2b.", "Wykonywanie przewozu z naruszeniem obowiązku", "2000" },
                Assert.Single(table.Rows).Cells.Select(cell => cell.Paragraphs.Single()));

            // Komórki nie stały się ani jednostkami cytatu, ani tekstem, ani dalszą treścią przepisu zmieniającego.
            Assert.Empty(content.Paragraphs);
            Assert.Null(content.PlainText);
            Assert.Empty(points[0].Continuation);
        }

        [Fact]
        public void QuotedTable_IsExportedInsideAStructuralCitation()
        {
            var xml = LegalDocumentConverter.Convert(ParseWithQuotedRow());

            var citation = Single(xml, "cytat-strukt");
            Assert.Equal(new[] { "tabela" }, ChildNames(citation));
            Assert.Equal("brak", citation.Attribute("cudzyslowy")?.Value);
            Assert.Equal("zdanie", citation.Parent!.Name.LocalName);
            Assert.DoesNotContain(citation.Parent.Elements(), e => e.Name.LocalName == "cytat-w-tekscie");
        }

        [Fact]
        public void QuotedTable_OfACommandWithSeveralTargets_IsExportedOnce()
        {
            // „ust. 26 i 27 otrzymują brzmienie:" i tabela: wierszy nie da się rozdać między cele, więc tabela
            // stoi w jednym cytacie — nie może zniknąć.
            var document = ParseBody(
                P("USTAWA") + P("z dnia 14 czerwca 2024 r.") + P("o zmianie ustawy o opłacie skarbowej") +
                P("Art. 1. W ustawie z dnia 16 listopada 2006 r. o opłacie skarbowej (Dz. U. z 2025 r. poz. 1154) " +
                  "w załączniku ust. 26 i 27 otrzymują brzmienie:") +
                "<w:tbl>" + Row(Cell("26."), Cell("Nadanie obywatelstwa"), Cell("277 zł")) +
                            Row(Cell("27."), Cell("Przywrócenie obywatelstwa"), Cell("1669 zł")) + "</w:tbl>" +
                P("Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia."));

            var xml = LegalDocumentConverter.Convert(document);

            var citation = Single(xml, "cytat-strukt");
            Assert.Equal(2, Single(citation, "tbody").Elements().Count());
            Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName == "cytat-w-tekscie");
        }

        [Fact]
        public void TableInsideQuotedUnits_ContinuesTheQuotedUnit()
        {
            // Tabela w środku cytowanego artykułu należy do jednostki, za którą stoi; cytat trwa dalej.
            var document = ParseBody(
                P("USTAWA") + P("z dnia 14 czerwca 2024 r.") + P("o zmianie ustawy o dochodach jednostek samorządu") +
                Styled("Art. 1. W ustawie z dnia 1 października 2024 r. o dochodach jednostek samorządu terytorialnego " +
                       "(Dz. U. poz. 1572) po art. 78 dodaje się art. 78a w brzmieniu:", "ARTartustawynprozporzdzenia") +
                Styled("„Art. 78a. 1. W celu obliczenia przeliczeniowej liczby mieszkańców przyjmuje się:", "ZARTzmartartykuempunktem") +
                "<w:tbl>" + Row(Cell("gminy"), Cell("administracja publiczna"), Cell("0,25")) + "</w:tbl>" +
                Styled("2. Wartości determinant ustala się na dzień 31 maja 2025 r.”.", "ZUSTzmustartykuempunktem") +
                Styled("Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.", "ARTartustawynprozporzdzenia"));

            var content = document.Articles.First().Paragraphs.Single().Amendment!.Content!;
            var quoted = Assert.Single(content.Articles);

            Assert.Empty(content.Tables);
            Assert.Equal(new[] { "art_78a__ust_1", "art_78a__ust_2" }, quoted.Paragraphs.Select(p => p.Id));
            Assert.NotNull(Assert.Single(quoted.Paragraphs[0].Continuation).Table);
            Assert.Equal(new[] { "art_1", "art_2" }, document.Articles.Select(a => a.Id));
        }

        // ============================================================
        // Cudzysłowy cytowanej tabeli w jej komórkach — bez stylów
        // ============================================================

        private const string ChangesInAnAnnex =
            "Art. 1. W ustawie z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) w załączniku wprowadza się następujące zmiany:";

        private const string EntryIntoForce = "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.";

        private static string Act(params string[] body) =>
            P("USTAWA") + P("z dnia 14 czerwca 2024 r.") + P("o zmianie ustawy o opłatach") + string.Concat(body);

        private static string Table(params string[] rows) => "<w:tbl>" + string.Concat(rows) + "</w:tbl>";

        [Theory]
        // Cytowaną tabelę zamyka cudzysłów w dodatkowej komórce jej ostatniego wiersza, razem z interpunkcją komendy.
        [InlineData("”,")]
        [InlineData("”;")]
        public void QuotationClosedInTheLastCellOfATable_EndsAtTheTable(string closing)
        {
            var document = ParseBody(Act(
                P(ChangesInAnAnnex),
                P("1) po tabeli 1 dodaje się tabelę 1a w brzmieniu:"),
                P("„Tabela 1a. Stawki opłat"),
                Table(Row(Cell("Bielizna"), Cell("282"), Cell("")), Row(Cell("Ogółem"), Cell("1 307"), Cell(closing))),
                P("2) tytuł tabeli 4 otrzymuje brzmienie:"),
                P("„Tabela 4. Stawki dla komendanta”."),
                P(EntryIntoForce)));

            // Przedtem tabela nie liczyła się w bilansie cudzysłowów: cytat się nie domykał i „2)" było treścią cytowaną.
            var points = document.Articles.First().Paragraphs.Single().Points;
            Assert.Equal(new[] { "art_1__pkt_1", "art_1__pkt_2" }, points.Select(p => p.Id));
            Assert.Equal(new[] { "art_1", "art_2" }, document.Articles.Select(a => a.Id));

            var content = points[0].Amendment!.Content!;
            Assert.Single(content.Tables);
            Assert.Equal("Tabela 1a. Stawki opłat", content.PlainText);
            Assert.Equal("Tabela 4. Stawki dla komendanta", points[1].Amendment!.Content!.PlainText);
        }

        [Fact]
        public void QuotationClosedInTheLastCellWithAFullStop_EndsWithoutAWarning()
        {
            var document = ParseBody(Act(
                P(ChangesInAnAnnex),
                P("1) po tabeli 1 dodaje się tabelę 1a w brzmieniu:"),
                P("„Tabela 1a. Stawki opłat"),
                Table(Row(Cell("Ogółem"), Cell("1 307"), Cell("”."))),
                P(EntryIntoForce)));

            Assert.Equal(new[] { "art_1", "art_2" }, document.Articles.Select(a => a.Id));

            // Cytat domknął cudzysłów tabeli — nie trzeba go było odtwarzać ponownym przebiegiem (ADR-0049).
            var point = document.Articles.First().Paragraphs.Single().Points.Single();
            Assert.Single(point.Amendment!.Content!.Tables);
            Assert.DoesNotContain(point.ValidationMessages, message => message.Message.Contains("niedomknięty"));
        }

        // Cudzysłów otwierający stoi w pierwszej komórce tabeli, a zamyka go objaśnienie pod tabelą.
        private static LegalDocument ParseTableWithANote() => ParseBody(Act(
            P(ChangesInAnAnnex),
            P("1) w pkt 2 tabela otrzymuje brzmienie:"),
            Table(Row(Cell("„"), Cell("Lp."), Cell("Temat")), Row(Cell(""), Cell("1"), Cell("Szkolenie*"))),
            P("* Jedna godzina równa się 60 minutom.”,"),
            P("2) uchyla się pkt 7."),
            P(EntryIntoForce)));

        [Fact]
        public void TableOpeningAQuotation_IsFollowedByTheNoteThatClosesIt()
        {
            var document = ParseTableWithANote();

            var points = document.Articles.First().Paragraphs.Single().Points;
            Assert.Equal(new[] { "art_1__pkt_1", "art_1__pkt_2" }, points.Select(p => p.Id));

            // Przedtem cytat kończył się z tabelą, a objaśnienie stawało się dalszą treścią przepisu zmieniającego.
            // Akapit za tabelą cytowaną wprost jest objaśnieniem pod tą tabelą, nie osobnym tekstem cytowanym.
            var content = points[0].Amendment!.Content!;
            Assert.Equal(new[] { "* Jedna godzina równa się 60 minutom." }, content.Tables.Single().Description);
            Assert.Null(content.PlainText);
            Assert.Empty(points[0].Continuation);
            Assert.Equal(",", points[0].Amendment!.TrailingPunctuation);
        }

        [Fact]
        public void NoteUnderAQuotedTable_IsExportedAsItsDescription()
        {
            var xml = LegalDocumentConverter.Convert(ParseTableWithANote());

            // Jeden cytat strukturalny: tabela, a w niej — za wierszami — objaśnienie.
            var citation = Single(xml, "cytat-strukt");
            var table = citation.Elements().Single();
            Assert.Equal(new[] { "tbody", "opis-tabeli" }, ChildNames(table));
            Assert.Equal("* Jedna godzina równa się 60 minutom.", table.Elements().Last().Value);
            Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName == "cytat-w-tekscie");

            // Interpunkcja komendy stoi za cytatem, w zdaniu przepisu zmieniającego.
            Assert.EndsWith(",", citation.Parent!.Value);
        }

        // ============================================================
        // Cudzysłów graniczny cytatu w komórce tabeli należy do cytatu, nie do tabeli
        // ============================================================

        private static string[][] CellTexts(ContentTable table) =>
            table.Rows.Select(row => row.Cells.Select(cell => string.Join(" ¶ ", cell.Paragraphs)).ToArray()).ToArray();

        [Fact]
        public void ClosingQuotationMarkInItsOwnColumn_IsTakenOffTheTableWithTheColumn()
        {
            var document = ParseBody(Act(
                P(ChangesInAnAnnex),
                P("1) po tabeli 1 dodaje się tabelę 1a w brzmieniu:"),
                P("„Tabela 1a. Stawki opłat"),
                Table(Row(Cell("Bielizna"), Cell("282"), Cell("")), Row(Cell("Ogółem"), Cell("1 307"), Cell("”,"))),
                P("2) uchyla się lp. 7."),
                P(EntryIntoForce)));

            var amendment = document.Articles.First().Paragraphs.Single().Points[0].Amendment!;

            // Kolumna, w której stał wyłącznie cudzysłów, nie jest kolumną tabeli.
            Assert.Equal(
                new[] { new[] { "Bielizna", "282" }, new[] { "Ogółem", "1 307" } },
                CellTexts(amendment.Content!.Tables.Single()));
            Assert.True(amendment.HasOpeningQuote);
            Assert.True(amendment.HasClosingQuote);
            Assert.Equal(",", amendment.TrailingPunctuation);
        }

        [Fact]
        public void OpeningQuotationMarkInItsOwnColumn_IsTakenOffTheTableWithTheColumn()
        {
            var amendment = ParseTableWithANote().Articles.First().Paragraphs.Single().Points[0].Amendment!;

            Assert.Equal(
                new[] { new[] { "Lp.", "Temat" }, new[] { "1", "Szkolenie*" } },
                CellTexts(amendment.Content!.Tables.Single()));
            Assert.True(amendment.HasOpeningQuote);
            Assert.True(amendment.HasClosingQuote);
        }

        [Fact]
        public void QuotationMarksBesideTextInTheCells_AreTakenOffTheText()
        {
            var document = ParseBody(Act(
                P(ChangesInAnAnnex),
                P("1) w pkt 2 tabela otrzymuje brzmienie:"),
                Table(Row(Cell("„Lp."), Cell("Temat")), Row(Cell("1"), Cell("Szkolenie”;"))),
                P("2) uchyla się pkt 7."),
                P(EntryIntoForce)));

            var amendment = document.Articles.First().Paragraphs.Single().Points[0].Amendment!;

            Assert.Equal(
                new[] { new[] { "Lp.", "Temat" }, new[] { "1", "Szkolenie" } },
                CellTexts(amendment.Content!.Tables.Single()));
            Assert.True(amendment.HasOpeningQuote);
            Assert.True(amendment.HasClosingQuote);
            Assert.Equal(";", amendment.TrailingPunctuation);

            var citation = Single(LegalDocumentConverter.Convert(document), "cytat-strukt");
            Assert.Equal("oba", citation.Attribute("cudzyslowy")?.Value);
            Assert.EndsWith(";", citation.Parent!.Value);
        }

        [Fact]
        public void PairedQuotationInTheLastCell_StaysInTheTable()
        {
            // Wyrażenie w cudzysłowie zamknięte w tej samej komórce nie jest granicą cytatu.
            var document = ParseBody(Act(
                P(ChangesInAnAnnex),
                P("1) w tabeli po lp. 2a dodaje się lp. 2b w brzmieniu:"),
                Table(Row(Cell("2b."), Cell("forma prawna „spółka akcyjna”"))),
                P("2) uchyla się lp. 7."),
                P(EntryIntoForce)));

            var amendment = document.Articles.First().Paragraphs.Single().Points[0].Amendment!;

            Assert.Equal(
                new[] { new[] { "2b.", "forma prawna „spółka akcyjna”" } },
                CellTexts(amendment.Content!.Tables.Single()));
            Assert.False(amendment.HasOpeningQuote);
            Assert.False(amendment.HasClosingQuote);
        }

        private static ContentTable TableOf(params ContentTableCell[][] rows) =>
            new() { Rows = rows.Select(cells => new ContentTableRow { Cells = [.. cells] }).ToList() };

        private static ContentTableCell CellOf(string text, int rowSpan = 1) =>
            new() { RowSpan = rowSpan, Paragraphs = text.Length == 0 ? [] : [text] };

        [Fact]
        public void ColumnOfTheQuotationMark_Stays_WhenItHoldsAnythingElse()
        {
            // Skrajna kolumna ma treść w innym wierszu — jest kolumną tabeli; znika tylko cudzysłów.
            var table = TableOf(
                [CellOf("Bielizna"), CellOf("uwaga")],
                [CellOf("Ogółem"), CellOf("”.")]);

            Assert.Equal(".", QuotedTableBoundaries.TakeClosing(table));
            Assert.Equal(new[] { new[] { "Bielizna", "uwaga" }, new[] { "Ogółem", "" } }, CellTexts(table));
        }

        [Fact]
        public void ColumnOfTheQuotationMark_Stays_WhenItsCellIsMerged()
        {
            var table = TableOf(
                [CellOf("„", rowSpan: 2), CellOf("Lp.")],
                [CellOf("1")]);

            Assert.True(QuotedTableBoundaries.TakeOpening(table));
            Assert.Equal(new[] { new[] { "", "Lp." }, new[] { "1" } }, CellTexts(table));
        }

        [Fact]
        public void TableWithoutBoundaryMarks_IsLeftAsItIs()
        {
            var table = TableOf([CellOf("Lp."), CellOf("Temat")], [CellOf("1"), CellOf("Szkolenie")]);

            Assert.False(QuotedTableBoundaries.TakeOpening(table));
            Assert.Null(QuotedTableBoundaries.TakeClosing(table));
            Assert.Equal(new[] { new[] { "Lp.", "Temat" }, new[] { "1", "Szkolenie" } }, CellTexts(table));
        }

        [SchemaPlFact]
        public void QuotedTableWithADescription_IsSchemaValid_InTheActBody()
        {
            Assert.Empty(SchemaPl.Validator.ValidateActBody(LegalDocumentConverter.Convert(ParseTableWithANote()).Elements().First()));
        }

        [Fact]
        public void PairedQuotationInACell_DoesNotCloseTheQuotation()
        {
            var document = ParseBody(Act(
                P(ChangesInAnAnnex),
                P("1) po tabeli 1 dodaje się tabelę 1a w brzmieniu:"),
                P("„Tabela 1a. Formy prawne"),
                Table(Row(Cell("1"), Cell("„spółka akcyjna”"))),
                P("Objaśnienie: forma prawna według rejestru.”,"),
                P("2) uchyla się lp. 7."),
                P(EntryIntoForce)));

            var points = document.Articles.First().Paragraphs.Single().Points;
            Assert.Equal(new[] { "art_1__pkt_1", "art_1__pkt_2" }, points.Select(p => p.Id));
            var content = points[0].Amendment!.Content!;
            Assert.Equal("Tabela 1a. Formy prawne", content.PlainText);
            Assert.Equal(new[] { "Objaśnienie: forma prawna według rejestru." }, content.Tables.Single().Description);
        }

        [Fact]
        public void TableThatOpensAndClosesItsQuotation_IsTheWholeQuotedContent()
        {
            var document = ParseBody(Act(
                P(ChangesInAnAnnex),
                P("1) w pkt 2 tabela otrzymuje brzmienie:"),
                Table(Row(Cell("„Lp."), Cell("Temat")), Row(Cell("1"), Cell("Szkolenie”;"))),
                P("2) uchyla się pkt 7."),
                P(EntryIntoForce)));

            var points = document.Articles.First().Paragraphs.Single().Points;
            Assert.Equal(new[] { "art_1__pkt_1", "art_1__pkt_2" }, points.Select(p => p.Id));
            Assert.Single(points[0].Amendment!.Content!.Tables);
            Assert.Null(points[1].Amendment?.Content);
        }

        [SchemaPlFact]
        public void AmendmentWithAQuotedTable_IsSchemaValid_InTheActBody()
        {
            Assert.Empty(SchemaPl.Validator.ValidateActBody(LegalDocumentConverter.Convert(ParseWithQuotedRow()).Elements().First()));
        }

        [SchemaPlFact]
        public void ProvisionWithATable_IsSchemaValid_InTheActBody()
        {
            Assert.Empty(SchemaPl.Validator.ValidateActBody(LegalDocumentConverter.Convert(ParseWithTable()).Elements().First()));
        }
    }
}
