using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Core.Services.Parsing;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Obwieszczenie w sprawie ogłoszenia tekstu jednolitego: treść sprzed wiersza „Załącznik do obwieszczenia …"
    /// należy do obwieszczenia, a nie do aktu, który ono ogłasza.
    /// </summary>
    public class AnnouncementTests
    {
        private static DocumentBlock Block(string text, string? style = null) => new() { Text = text, StyleId = style };

        // Układ szczotki RCL (336586_2987956): nagłówek, punkty obwieszczenia z podpunktami, przytoczony przepis,
        // podpis, akt.
        private static readonly string[] ConsolidatedText =
        [
            "Obwieszczenie",
            "Marszałka Sejmu Rzeczypospolitej Polskiej",
            "z dnia 6 sierpnia 2025 r.",
            "w sprawie ogłoszenia jednolitego tekstu ustawy o odbudowie obiektów budowlanych",
            "1. Na podstawie art. 16 ust. 1 ustawy z dnia 20 lipca 2000 r. o ogłaszaniu aktów normatywnych ogłasza się w załączniku do niniejszego obwieszczenia jednolity tekst ustawy z dnia 11 sierpnia 2001 r. o odbudowie obiektów budowlanych, z uwzględnieniem zmian wprowadzonych:",
            "1) ustawą z dnia 1 października 2024 r. o zmianie ustawy o odbudowie (Dz. U. poz. 1473),",
            "2) ustawą z dnia 9 maja 2025 r. o zmianie ustawy o odbudowie (Dz. U. poz. 680)",
            "oraz zmian wynikających z przepisów ogłoszonych przed dniem 6 sierpnia 2025 r.",
            "2. Podany w załączniku do niniejszego obwieszczenia tekst jednolity ustawy nie obejmuje art. 57 ustawy z dnia 1 października 2024 r. o zmianie ustawy o odbudowie (Dz. U. poz. 1473), który stanowi:",
            "„Art. 57. Przepisy ustawy zmienianej w art. 10 stosuje się również do odbudowy.”",
            "Marszałek Sejmu: S. Hołownia",
            "Załącznik do obwieszczenia Marszałka Sejmu Rzeczypospolitej Polskiej z dnia 6 sierpnia 2025 r. (Dz. U. poz. 1094)",
            "USTAWA",
            "z dnia 11 sierpnia 2001 r.",
            "o odbudowie obiektów budowlanych",
            "Art. 1. Ustawa reguluje zasady odbudowy.",
            "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
        ];

        private const int ActStart = 11;
        private const int ActSubjectLine = 14;

        private static LegalDocument Parse(IEnumerable<string> lines) =>
            LegalDocumentParser.Parse(
                lines.Select(line => Block(line)).ToList(),
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

        // ============================================================
        // Granica obwieszczenia i ogłaszanego aktu
        // ============================================================

        [Fact]
        public void ContentBeforeTheAnnexedAct_BelongsToTheAnnouncement()
        {
            var document = Parse(ConsolidatedText);

            // Przytoczony „Art. 57." nie jest artykułem ogłaszanego aktu.
            Assert.Equal(new[] { "1", "2" }, document.Articles.Select(a => a.Number?.Value));
            Assert.NotNull(document.Announcement);
        }

        [Fact]
        public void AnnouncementHeading_StaysTheHeadingOfTheDocument()
        {
            var document = Parse(ConsolidatedText);

            Assert.Equal("Obwieszczenie", document.KindLine);
            Assert.Equal("Marszałka Sejmu Rzeczypospolitej Polskiej", document.IssuerLine);
            Assert.Equal("z dnia 6 sierpnia 2025 r.", document.DateLine);
            Assert.Equal(new DateTime(2025, 8, 6), document.ActDate);
            Assert.StartsWith("w sprawie ogłoszenia jednolitego tekstu", document.Subject);
        }

        [Fact]
        public void ActAnnouncedDirectly_HasNoAnnouncement()
        {
            var document = Parse(ConsolidatedText.Skip(ActStart + 1));

            Assert.Null(document.Announcement);
            Assert.Equal(2, document.Articles.Count());
        }

        [Fact]
        public void AnnouncementWithoutAnAnnexedAct_IsLeftToTheActPipeline()
        {
            // Obwieszczenie innego rodzaju (bez „Załącznik do obwieszczenia") nie ma granicy — nic się nie zmienia.
            Assert.Equal(-1, AnnouncementBoundary.Find(ConsolidatedText.Take(ActStart).Select(line => Block(line)).ToList()));
            Assert.Equal(ActStart, AnnouncementBoundary.Find(ConsolidatedText.Select(line => Block(line)).ToList()));
        }

        // ============================================================
        // Jednostki obwieszczenia
        // ============================================================

        [Fact]
        public void AnnouncementPoints_HoldSubPointsAndTheirCommonPart()
        {
            var announcement = Parse(ConsolidatedText).Announcement!;

            Assert.Empty(announcement.Content);
            Assert.Equal(new[] { "pkt_1", "pkt_2" }, announcement.Points.Select(p => p.Id));

            var first = announcement.Points[0];
            Assert.StartsWith("Na podstawie art. 16", first.ContentText);
            Assert.Equal(new[] { "pkt_1__ppkt_1", "pkt_1__ppkt_2" }, first.Points.Select(p => p.Id));
            Assert.StartsWith("ustawą z dnia 1 października 2024 r.", first.Points[0].ContentText);

            var commonPart = Assert.Single(first.CommonParts, part => part.Type == CommonPartType.WrapUp);
            Assert.StartsWith("oraz zmian wynikających", commonPart.ContentText);
        }

        // ============================================================
        // Przytoczone przepisy (ADR-0036)
        // ============================================================

        private const int QuotedLine = 9;

        /// <summary>Wzorcowe obwieszczenie z innym przytoczeniem w punkcie 2.</summary>
        private static List<string> WithQuotation(params string[] quoted)
        {
            var lines = ConsolidatedText.ToList();
            lines.RemoveAt(QuotedLine);
            lines.InsertRange(QuotedLine, quoted);
            return lines;
        }

        [Fact]
        public void QuotedArticle_IsAQuotationOfThePointItStandsAfter()
        {
            var second = Parse(ConsolidatedText).Announcement!.Points[1];

            Assert.Empty(second.Points);
            var quotation = Assert.Single(second.Continuation).Quotation!;
            var article = Assert.Single(quotation.Content.Articles);
            Assert.Equal("57", article.Number?.Value);
            Assert.Equal("Przepisy ustawy zmienianej w art. 10 stosuje się również do odbudowy.", article.Paragraphs[0].ContentText);

            // Akt przytaczany wskazuje treść punktu: „art. 57 ustawy z dnia 1 października 2024 r. … (Dz. U. poz. 1473)".
            Assert.Equal(new[] { "DU/2024/1473" }, quotation.SourceAct!.GetELIStrings());
        }

        [Fact]
        public void QuotedArticleOfSeveralParagraphs_IsOneQuotation()
        {
            // Przytoczenie otwarte cudzysłowem ciągnie się do jego domknięcia — „1)" w środku nie jest podpunktem.
            var second = Parse(WithQuotation("„Art. 57. Przepisy stosuje się do:", "1) odbudowy,", "2) remontu.”"))
                .Announcement!.Points[1];

            Assert.Empty(second.Points);
            var article = Assert.Single(Assert.Single(second.Continuation).Quotation!.Content.Articles);
            Assert.Equal(new[] { "1", "2" }, article.Paragraphs[0].Points.Select(point => point.Number?.Value));
        }

        [Fact]
        public void SeveralQuotedArticles_AreOneQuotation()
        {
            var second = Parse(WithQuotation(
                    "„Art. 57. 1. Przepisy stosuje się do odbudowy.",
                    "2. Przepisów nie stosuje się do remontu.",
                    "Art. 58. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.”"))
                .Announcement!.Points[1];

            var articles = Assert.Single(second.Continuation).Quotation!.Content.Articles;
            Assert.Equal(new[] { "57", "58" }, articles.Select(article => article.Number?.Value));
            Assert.Equal(2, articles[0].Paragraphs.Count);
        }

        [Fact]
        public void QuoteOpenedAfterTheArticleDesignation_IsAQuotationOfThatArticle()
        {
            // Korpus: 299130_2697165 — przytoczony jest ust. 1, więc cudzysłów stoi za oznaczeniem artykułu
            // („Art. 78. „1. …"). Przedtem akapit zostawał tekstem, a punkty ustępu stawały się podpunktami
            // obwieszczenia o tych samych adresach co jego własne (ADR-0075).
            var second = Parse(WithQuotation(
                    "Art. 57. „1. Uznaje się, że od dnia 19 stycznia 2023 r.:",
                    "1) programy mogą być dofinansowane;",
                    "2) wojewoda może przyznawać dofinansowanie.”"))
                .Announcement!.Points[1];

            Assert.Empty(second.Points);
            var article = Assert.Single(Assert.Single(second.Continuation).Quotation!.Content.Articles);
            Assert.Equal("57", article.Number?.Value);
            var paragraph = Assert.Single(article.Paragraphs);
            Assert.Equal("1", paragraph.Number?.Value);
            Assert.Equal(new[] { "1", "2" }, paragraph.Points.Select(point => point.Number?.Value));
        }

        [Fact]
        public void QuoteOpenedAfterTheArticleDesignation_LeavesTheDesignationOutsideTheQuotation()
        {
            // Przytoczony jest ustęp, nie artykuł: oznaczenie artykułu jest tekstem zdania punktu, a ustęp ma adres
            // w swoim artykule (ADR-0113).
            var quotation = Assert.Single(Parse(WithQuotation("Art. 57. „2. Przepisy stosuje się do odbudowy.”"))
                .Announcement!.Points[1].Continuation).Quotation!;

            Assert.Equal("Art. 57.", quotation.ArticleDesignation);
            var paragraph = Assert.Single(Assert.Single(quotation.Content.Articles).Paragraphs);
            Assert.Equal("art_57__ust_2", paragraph.Id);
        }

        [Theory]
        // Cudzysłów przed oznaczeniem — przytoczony jest cały artykuł.
        [InlineData("„Art. 57. 2. Przepisy stosuje się do odbudowy.”")]
        // Za cudzysłowem nie ma oznaczenia ustępu — przytoczona jest cała treść artykułu.
        [InlineData("Art. 57. „Przepisy stosuje się do odbudowy.”")]
        public void QuotationOfAWholeArticle_HasNoDesignationOutsideIt(string quoted)
        {
            var quotation = Assert.Single(Parse(WithQuotation(quoted)).Announcement!.Points[1].Continuation).Quotation!;

            Assert.Null(quotation.ArticleDesignation);
            Assert.Equal("57", Assert.Single(quotation.Content.Articles).Number?.Value);
        }

        [Theory]
        // Przytoczony ustęp wymagałby adresu z treści punktu („art. 5 ust. 3 ustawy …").
        [InlineData("„3. W przypadku gdy osoba posiada zaświadczenie, przepisu nie stosuje się.”")]
        // Akapit poza jednostkami — przytoczenia nie da się zbudować w całości.
        [InlineData("„Na dzień ogłoszenia obwieszczenia działem kieruje Minister Finansów.”")]
        public void QuotationThatIsNotWholeArticles_StaysText(string quoted)
        {
            var block = Assert.Single(Parse(WithQuotation(quoted)).Announcement!.Points[1].Continuation);

            Assert.Null(block.Quotation);
            Assert.Equal(quoted, block.Text);
        }

        [Fact]
        public void QuotationWithAParagraphBeforeItsUnits_StaysText()
        {
            // Akapit przed pierwszą jednostką nie należy do żadnej z nich — przytoczenia nie da się zbudować w całości.
            string[] quoted = ["„Uwaga wstępna bez oznaczenia jednostki.", "Art. 57. Przepisy stosuje się do odbudowy.”"];

            var blocks = Parse(WithQuotation(quoted)).Announcement!.Points[1].Continuation;

            Assert.All(blocks, block => Assert.Null(block.Quotation));
            Assert.Equal(quoted, blocks.Select(block => block.Text));
        }

        [Fact]
        public void UnmarkedParagraphInAQuotedArticle_ContinuesItsUnit()
        {
            // Akapit bez oznaczenia za jednostką jest jej dalszą treścią (ADR-0029), także w przytoczeniu.
            var lines = WithQuotation("„Art. 57. Kwotę oblicza się według wzoru:", "K=A∙W", "gdzie K oznacza wysokość kary.”");

            var article = Assert.Single(Assert.Single(Parse(lines).Announcement!.Points[1].Continuation).Quotation!.Content.Articles);
            Assert.Equal(new[] { "K=A∙W", "gdzie K oznacza wysokość kary." }, article.Paragraphs[0].Continuation.Select(block => block.Text));

            var quoted = LegalDocumentConverter.Convert(Parse(lines)).Descendants().Single(e => e.Name.LocalName == "tresc-strukt");
            Assert.Equal(
                new[] { "K=A∙W", "gdzie K oznacza wysokość kary." },
                quoted.Descendants().Where(e => e.Name.LocalName == "fragment").Select(e => e.Value));
        }

        // Korpus: 349921_3119995 — przytoczone przepisy zmieniające z własnymi cytatami.
        private static readonly string[] QuotedAmendingProvision =
        [
            "„Art. 54. W ustawie z dnia 7 września 1991 r. o systemie oświaty (Dz. U. z 2019 r. poz. 1481) w art. 90m ust. 2 otrzymuje brzmienie:",
            "„2. Rada gminy może upoważnić kierownika ośrodka pomocy społecznej do prowadzenia postępowania.”.",
            "Art. 55. W ustawie z dnia 8 marca 1990 r. o samorządzie gminnym (Dz. U. z 2022 r. poz. 559) wprowadza się następujące zmiany:",
            "1) w art. 10f ust. 5 otrzymuje brzmienie:",
            "„5. Strategia rozwoju gminy podlega aktualizacji.”;",
            "2) w art. 74 uchyla się ust. 3.”",
        ];

        [Fact]
        public void QuotedAmendingProvisionWithItsOwnQuotation_HasItAsItsAmendment()
        {
            // Cytowany w przepisie zmieniającym „2. …" nie jest ustępem przytaczanego artykułu, tylko treścią zmiany,
            // którą ten przepis wprowadza (ADR-0082, ADR-0084).
            var quotation = Assert.Single(Parse(WithQuotation(QuotedAmendingProvision)).Announcement!.Points[1].Continuation).Quotation!;

            Assert.Equal(["54", "55"], quotation.Content.Articles.Select(article => article.Number!.Value));
            var first = Assert.Single(quotation.Content.Articles[0].Paragraphs);
            Assert.Equal("art_90m__ust_2", first.Amendment!.Targets.Single().Structure.ToString());
            Assert.Equal([1481], first.Amendment.TargetLegalAct.Positions);
            Assert.Equal("2", Assert.Single(first.Amendment.Content!.Paragraphs).Number!.Value);

            var points = quotation.Content.Articles[1].Paragraphs.Single().Points;
            Assert.Equal(AmendmentOperationType.Modification, points[0].Amendment!.OperationType);
            Assert.Equal(AmendmentOperationType.Repeal, points[1].Amendment!.OperationType);
        }

        [Fact]
        public void QuotedAmendingProvision_IsExportedWithItsAmendments_InTheQuotation()
        {
            var quoted = LegalDocumentConverter.Convert(Parse(WithQuotation(QuotedAmendingProvision)))
                .Descendants().Single(e => e.Name.LocalName == "tresc-strukt");

            // Przytoczony artykuł ma adres w akcie, z którego pochodzi; jego cytat — w akcie, który zmienia.
            Assert.Equal("DU/2024/1473__art_54", quoted.Elements().First().Attribute("eId")!.Value);
            var nested = quoted.Descendants().First(e => e.Name.LocalName == "cytat-strukt");
            Assert.Equal("DU/2019/1481__art_90m__ust_2", nested.Attribute("element-eId")!.Value);
            Assert.DoesNotContain(quoted.Descendants(), e => e.Name.LocalName == "fragment");
        }

        [SchemaPlFact]
        public void QuotedAmendingProvision_IsSchemaValid_InItsBody()
        {
            Assert.Empty(SchemaPl.Validator.ValidateActBody(
                LegalDocumentConverter.Convert(Parse(WithQuotation(QuotedAmendingProvision))).Elements().First()));
        }

        // ============================================================
        // Tabela w przytoczeniu
        // ============================================================

        private static DocumentBlock Cell(int row, int column, string text) =>
            new() { Text = text, TableCell = new BlockTableCell { Table = 0, Row = row, Column = column } };

        /// <summary>Wzorcowe obwieszczenie z przytoczeniem z bloków (z komórkami tabeli) w punkcie 2.</summary>
        private static LegalDocument ParseWithQuotation(params DocumentBlock[] quoted)
        {
            var blocks = ConsolidatedText.Select(line => Block(line)).ToList();
            blocks.RemoveAt(QuotedLine);
            blocks.InsertRange(QuotedLine, quoted);
            return LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
        }

        // Korpus: 329080_2931155 — przytoczony przepis zmieniający dodaje wiersz tabeli załącznika.
        private static readonly DocumentBlock[] QuotedTableRow =
        [
            Block("„Art. 54. W ustawie z dnia 6 września 2001 r. o transporcie drogowym (Dz. U. z 2022 r. poz. 2201) w załączniku nr 1 po lp. 2 dodaje się lp. 2a w brzmieniu:"),
            Cell(0, 0, "„2a"), Cell(0, 1, "Naruszenie obowiązku delegowania kierowcy"), Cell(0, 2, "1500”;"),
            Block("Art. 55. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.”"),
        ];

        [Fact]
        public void TableQuotedByAQuotedProvision_IsTheContentOfItsAmendment()
        {
            var quotation = Assert.Single(ParseWithQuotation(QuotedTableRow).Announcement!.Points[1].Continuation).Quotation!;

            Assert.Equal(["54", "55"], quotation.Content.Articles.Select(article => article.Number!.Value));
            var amendment = quotation.Content.Articles[0].Paragraphs.Single().Amendment!;
            var table = Assert.Single(amendment.Content!.Tables);
            Assert.Equal("2a", table.Rows[0].Cells[0].Paragraphs.Single());
            Assert.Equal([2201], amendment.TargetLegalAct.Positions);
        }

        [Fact]
        public void TableInsideAQuotedArticle_ContinuesItsUnit()
        {
            var quotation = Assert.Single(ParseWithQuotation(
                Block("„Art. 54. Opłaty wynoszą:"),
                Cell(0, 0, "Opłata A"), Cell(0, 1, "100 zł"),
                Block("Art. 55. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.”")).Announcement!.Points[1].Continuation).Quotation!;

            var paragraph = quotation.Content.Articles[0].Paragraphs.Single();
            Assert.Null(paragraph.Amendment);
            Assert.NotNull(Assert.Single(paragraph.Continuation).Table);
        }

        [Fact]
        public void QuotationEndingWithATable_StaysTextWithItsQuotationMarks_AndClosesInTheTable()
        {
            var document = ParseWithQuotation(
                Block("„Art. 54. Opłaty wynoszą:"),
                Cell(0, 0, "Opłata A"), Cell(0, 1, "100 zł”."));

            var blocks = document.Announcement!.Points[1].Continuation;
            Assert.Equal("„Art. 54. Opłaty wynoszą:", blocks[0].Text);
            Assert.Equal("100 zł”.", blocks[1].Table!.Rows[0].Cells[1].Paragraphs.Single());

            // Cudzysłów domknięty w komórce zamyka przytoczenie — podpis za nim jest podpisem.
            Assert.Equal(2, blocks.Count);
            Assert.Equal("Hołownia", document.Signature!.Surname);
        }

        [SchemaPlFact]
        public void QuotationWithATable_IsSchemaValid_InItsBody()
        {
            Assert.Empty(SchemaPl.Validator.ValidateActBody(
                LegalDocumentConverter.Convert(ParseWithQuotation(QuotedTableRow)).Elements().First()));
        }

        [Fact]
        public void QuotationWithAQuoteThatNoCommandIntroduces_StaysText()
        {
            // Cytat w przytoczonym artykule bez komendy przed sobą (rota ślubowania) nie jest jego jednostką ani zmianą.
            string[] quoted = ["„Art. 54. Ślubowanie ma następujące brzmienie:", "„Ślubuję uroczyście.”.”"];

            var blocks = Parse(WithQuotation(quoted)).Announcement!.Points[1].Continuation;

            Assert.All(blocks, block => Assert.Null(block.Quotation));
            Assert.Equal(quoted, blocks.Select(block => block.Text));
        }

        [Fact]
        public void QuotationUnderAPointNamingTwoActs_HasNoSourceAct()
        {
            // Dwa publikatory w treści punktu — nie wiadomo, z którego aktu pochodzi przytoczenie.
            var lines = ConsolidatedText.ToList();
            lines[QuotedLine - 1] = "2. Podany w załączniku tekst jednolity nie obejmuje art. 57 ustawy z dnia 1 października 2024 r. " +
                "o zmianie ustawy o odbudowie (Dz. U. poz. 1473) oraz art. 3 ustawy z dnia 9 maja 2025 r. (Dz. U. poz. 680), które stanowią:";

            var quotation = Assert.Single(Parse(lines).Announcement!.Points[1].Continuation).Quotation!;

            Assert.Null(quotation.SourceAct);
            var exported = LegalDocumentConverter.Convert(Parse(lines)).Descendants().Single(e => e.Name.LocalName == "tresc-strukt");
            Assert.Equal("art_57", exported.Elements().Single().Attribute("eId")!.Value);
        }

        // ============================================================
        // Przytoczony odnośnik (ADR-0107)
        // ============================================================

        // Korpus: 334858_3048802 — tekst jednolity nie obejmuje odnośnika nr 2 i paragrafów aktu zmieniającego; odnośnik
        // przytoczony przed nimi, ze znacznikiem indeksem górnym i wyliczeniem.
        private static List<string> WithQuotedFootnote(string footnoteNumber, string firstLine)
        {
            var lines = WithQuotation(
                firstLine,
                "1) rozporządzenia (UE) 2017/373 z dnia 1 marca 2017 r.,",
                "2) rozporządzenia (UE) 2023/1768 z dnia 14 lipca 2023 r.”",
                "„Art. 57. Przepisy ustawy zmienianej w art. 10 stosuje się również do odbudowy.”");
            lines[QuotedLine - 1] = $"2. Podany w załączniku do niniejszego obwieszczenia tekst jednolity ustawy nie obejmuje odnośnika nr {footnoteNumber} " +
                "oraz art. 57 ustawy z dnia 1 października 2024 r. o zmianie ustawy o odbudowie (Dz. U. poz. 1473), które stanowią:";
            return lines;
        }

        [Theory]
        [InlineData("„[2)] Niniejsza ustawa służy stosowaniu:")]
        [InlineData("[„2)] Niniejsza ustawa służy stosowaniu:")]
        public void QuotedFootnoteNamedByThePoint_IsAQuotationOfThatFootnote(string firstLine)
        {
            var blocks = Parse(WithQuotedFootnote("2", firstLine)).Announcement!.Points[1].Continuation;

            Assert.Equal(2, blocks.Count);
            var footnote = blocks[0].Quotation!.Content.Footnote!;
            Assert.Equal("2", footnote.Symbol);
            Assert.Equal("Niniejsza ustawa służy stosowaniu:", footnote.Paragraphs[0]);
            Assert.Equal(3, footnote.Paragraphs.Count);
            Assert.Equal(new[] { "DU/2024/1473" }, blocks[0].Quotation!.SourceAct!.GetELIStrings());

            // Przytoczony artykuł za odnośnikiem zostaje osobnym przytoczeniem.
            Assert.Equal("57", Assert.Single(blocks[1].Quotation!.Content.Articles).Number?.Value);
        }

        [Fact]
        public void QuotedFootnote_IsExportedAsAQuotedFootnoteOfTheQuotedAct()
        {
            var point = LegalDocumentConverter.Convert(Parse(WithQuotedFootnote("2", "„[2)] Niniejsza ustawa służy stosowaniu:")))
                .Descendants().Single(e => e.Name.LocalName == "punkt-OTJ" && e.Attribute("eId")!.Value.EndsWith("pkt_2"));

            var quotations = point.Descendants().Where(e => e.Name.LocalName == "tresc-strukt").ToList();
            Assert.Equal(2, quotations.Count);
            var footnote = Assert.Single(quotations[0].Elements());
            Assert.Equal("odnosnik-cyt", footnote.Name.LocalName);
            Assert.Equal("DU/2024/1473__odn_2", footnote.Attribute("eId")!.Value);
            Assert.Equal("2)", footnote.Descendants().First(e => e.Name.LocalName == "sup").Value);
            Assert.Equal(2, footnote.Elements().Count(e => e.Name.LocalName == "punkt-odnosnik"));
            Assert.DoesNotContain(point.Descendants(), e => e.Name.LocalName == "fragment");
        }

        [SchemaPlFact]
        public void QuotedFootnote_IsSchemaValid_InItsBody()
        {
            Assert.Empty(SchemaPl.Validator.ValidateActBody(
                LegalDocumentConverter.Convert(Parse(WithQuotedFootnote("2", "„[2)] Niniejsza ustawa służy stosowaniu:"))).Elements().First()));
        }

        [Fact]
        public void QuotationOpeningWithTheMarkerOfAnotherFootnote_StaysText()
        {
            // Punkt wymienia odnośnik nr 3, a przytoczenie zaczyna się znacznikiem „2)" — nie wiadomo, czym jest.
            var blocks = Parse(WithQuotedFootnote("3", "„[2)] Niniejsza ustawa służy stosowaniu:")).Announcement!.Points[1].Continuation;

            Assert.Equal("„[2)] Niniejsza ustawa służy stosowaniu:", blocks[0].Text);
            Assert.Null(blocks[0].Quotation);
            Assert.NotNull(blocks[^1].Quotation);
        }

        [Fact]
        public void QuotationOpeningWithAPoint_StaysText_WhenThePointNamesNoFootnote()
        {
            var lines = WithQuotation("„2) rozporządzenia (UE) 2017/373 z dnia 1 marca 2017 r.”");

            var blocks = Parse(lines).Announcement!.Points[1].Continuation;

            Assert.Null(Assert.Single(blocks).Quotation);
        }

        [Fact]
        public void SignatureUnderTheAnnouncement_IsTheSignatureOfTheDocument()
        {
            var signature = Parse(ConsolidatedText).Signature!;

            Assert.Equal("Marszałek Sejmu", signature.Authority);
            Assert.Equal("Hołownia", signature.Surname);
        }

        // ============================================================
        // Korzeń „obwieszczenie" z aktem osadzonym (ADR-0031)
        // ============================================================

        [Fact]
        public void AnnouncedAct_KeepsItsOwnHeading()
        {
            var announcement = Parse(ConsolidatedText).Announcement!;

            Assert.StartsWith("Załącznik do obwieszczenia Marszałka Sejmu", announcement.AttachedTo);
            Assert.Equal("USTAWA", announcement.ActKindLine);
            Assert.Equal("z dnia 11 sierpnia 2001 r.", announcement.ActDateLine);
            Assert.Equal("o odbudowie obiektów budowlanych", announcement.ActSubject);
        }

        private static string[] ChildNames(XElement element) =>
            element.Elements().Select(child => child.Name.LocalName).ToArray();

        private static XElement Child(XElement element, string name) =>
            element.Elements().First(child => child.Name.LocalName == name);

        [Fact]
        public void ConsolidatedStatute_IsExportedUnderTheAnnouncement()
        {
            var xml = LegalDocumentConverter.Convert(Parse(ConsolidatedText));

            var announcement = Assert.Single(xml.Elements());
            Assert.Equal("obwieszczenie", announcement.Name.LocalName);
            Assert.Equal(
                new[] { "metadane", "rodzaj-aktu", "autor-aktu", "data", "tytul", "punkt-OTJ", "punkt-OTJ", "organ-podpisujacy", "ustawa" },
                ChildNames(announcement));
            Assert.Equal("Obwieszczenie", Child(announcement, "rodzaj-aktu").Value);
            Assert.Equal("Marszałka Sejmu Rzeczypospolitej Polskiej", Child(announcement, "autor-aktu").Value);

            // Akt ogłaszany w załączniku ma własną strefę tytułową i tylko własne artykuły.
            var act = Child(announcement, "ustawa");
            Assert.Equal(new[] { "zalacznik-do", "rodzaj-aktu", "data", "tytul", "artykul", "artykul" }, ChildNames(act));
            Assert.Equal("Ustawa", Child(act, "rodzaj-aktu").Value);
            Assert.Equal("o odbudowie obiektów budowlanych", Child(act, "tytul").Value);
        }

        [Fact]
        public void AnnouncementPoints_AreExportedWithAddressesOfTheAnnouncement()
        {
            var announcement = LegalDocumentConverter.Convert(Parse(ConsolidatedText)).Elements().Single();

            var first = Child(announcement, "punkt-OTJ");
            Assert.Equal(new[] { "nr", "p", "podpunkt-OTJ", "podpunkt-OTJ", "czesc-wspolna-podpunkt-OTJ" }, ChildNames(first));
            // Fikstura nie ma nagłówka dziennika — prefiks aktu daje wiersz „Załącznik do obwieszczenia … (Dz. U. poz. 1094)".
            Assert.Equal("DU/2025/1094__pkt_1", first.Attribute("eId")!.Value);
            Assert.Equal(
                new[] { "DU/2025/1094__pkt_1__ppkt_1", "DU/2025/1094__pkt_1__ppkt_2", "DU/2025/1094__pkt_1__czw" },
                first.Elements().Skip(2).Select(e => e.Attribute("eId")!.Value));

            // Przytoczony przepis stoi w zdaniu punktu, nie obok niego.
            var second = announcement.Elements().Where(e => e.Name.LocalName == "punkt-OTJ").ElementAt(1);
            Assert.Equal(new[] { "nr", "p" }, ChildNames(second));

            // Jednostki obwieszczenia nie są przepisami aktu — nie niosą dat.
            Assert.DoesNotContain(
                announcement.Elements().Where(e => e.Name.LocalName == "punkt-OTJ").DescendantsAndSelf(),
                e => e.Attribute("wejscie-w-zycie") != null || e.Attribute("okres") != null);
        }

        [Fact]
        public void QuotedArticle_IsExportedInTheSentence_AddressedInItsOwnAct()
        {
            var announcement = LegalDocumentConverter.Convert(
                    Parse(WithQuotation("„Art. 57. Przepisy stosuje się do:", "1) odbudowy,", "2) remontu.”;")))
                .Elements().Single();

            var sentence = announcement.Elements().Where(e => e.Name.LocalName == "punkt-OTJ").ElementAt(1)
                .Descendants().First(e => e.Name.LocalName == "zdanie");
            var quoted = Assert.Single(sentence.Elements());

            Assert.Equal("tresc-strukt", quoted.Name.LocalName);
            Assert.Equal("oba", quoted.Attribute("cudzyslowy")!.Value);
            Assert.EndsWith("który stanowi:", sentence.Nodes().OfType<XText>().First().Value);

            // Interpunkcja za cudzysłowem należy do zdania punktu.
            Assert.Equal(";", sentence.Nodes().OfType<XText>().Last().Value);

            // Adres w akcie przytaczanym, nie w obwieszczeniu ani w ogłaszanym akcie.
            Assert.Equal(
                new[] { "DU/2024/1473__art_57", "DU/2024/1473__art_57__pkt_1", "DU/2024/1473__art_57__pkt_2" },
                quoted.Descendants().Select(e => e.Attribute("eId")?.Value).OfType<string>());
        }

        [Fact]
        public void QuotedParagraphsOfAnArticle_AreExportedInASentenceFragmentAfterItsDesignation()
        {
            var announcement = LegalDocumentConverter.Convert(Parse(WithQuotation(
                    "Art. 57. „2. Przepisy stosuje się do odbudowy.",
                    "3. Przepisów nie stosuje się do remontu.”;")))
                .Elements().Single();

            var fragments = announcement.Elements().Where(e => e.Name.LocalName == "punkt-OTJ").ElementAt(1)
                .Elements().Single(e => e.Name.LocalName == "p").Elements().ToList();
            Assert.Equal(2, fragments.Count);
            var (introduction, designation) = (fragments[0], fragments[1]);

            // Zdanie punktu kończy się wprowadzeniem; dalszy fragment tego samego zdania niesie oznaczenie artykułu.
            Assert.EndsWith("który stanowi:", introduction.Value);
            Assert.Equal(introduction.Attribute("nr")!.Value, designation.Attribute("nr")!.Value);
            Assert.Equal(introduction.Attributes().Single(a => a.Name.LocalName == "zdGUID").Value,
                designation.Attributes().Single(a => a.Name.LocalName == "zdGUID").Value);
            Assert.Equal((int)introduction.Attribute("fragment")! + 1, (int)designation.Attribute("fragment")!);
            Assert.Equal("true", designation.Attributes().Single(a => a.Name.LocalName == "znastepnym").Value);
            Assert.Equal("Art. 57.", designation.Nodes().OfType<XText>().First().Value);
            Assert.Equal(";", designation.Nodes().OfType<XText>().Last().Value);

            // W cudzysłowie same ustępy, z numerami i adresami w swoim artykule.
            var quoted = Assert.Single(designation.Elements());
            Assert.Equal("tresc-strukt", quoted.Name.LocalName);
            Assert.Equal(new[] { "ustep", "ustep" }, quoted.Elements().Select(e => e.Name.LocalName));
            Assert.Equal(new[] { "DU/2024/1473__art_57__ust_2", "DU/2024/1473__art_57__ust_3" },
                quoted.Elements().Select(e => e.Attribute("eId")!.Value));
            Assert.Equal(new[] { "2", "3" }, quoted.Elements().Select(e => e.Elements().First().Value));
        }

        [SchemaPlFact]
        public void QuotedParagraphOfAnArticle_IsSchemaValid_ExceptForTheAnnouncedTypographyOfTheSentence()
        {
            // Atrybut tpg:znastepnym przy zdaniu dopiero wejdzie do schematu (pytanie 27, ADR-0113) — poza nim zgodny.
            var problems = SchemaPl.Validator.ValidateActBody(LegalDocumentConverter.Convert(
                Parse(WithQuotation("Art. 57. „2. Przepisy stosuje się do odbudowy.”"))).Elements().First());

            Assert.All(problems, problem => Assert.Contains("znastepnym", problem));
        }

        [SchemaPlFact]
        public void AnnouncementWithQuotedArticles_IsSchemaValid_InItsBody()
        {
            var lines = WithQuotation(
                "„Art. 57. 1. Przepisy stosuje się do:",
                "1) odbudowy,",
                "2) remontu.",
                "2. Przepisów nie stosuje się do rozbiórki.",
                "Art. 58. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.”");

            Assert.Empty(SchemaPl.Validator.ValidateActBody(LegalDocumentConverter.Convert(Parse(lines)).Elements().First()));
        }

        [Fact]
        public void ActAnnouncedDirectly_KeepsTheActAsTheRoot()
        {
            var xml = LegalDocumentConverter.Convert(Parse(ConsolidatedText.Skip(ActStart + 1)));

            Assert.Equal("ustawa", Assert.Single(xml.Elements()).Name.LocalName);
        }

        [Fact]
        public void AnnouncedActWhoseKindIsNotNamed_IsAnotherLegalAct_AndTheAnnouncementStays()
        {
            // Obwieszczenie zapowiada tekst jednolity, ale załącznik nie zaczyna się nagłówkiem rodzaju aktu.
            // Obwieszczenie nie znika przez to z eksportu: jego punkty i podpis wychodzą, a to, co ogłasza,
            // stoi pod elementem aktu bez nazwanego rodzaju.
            var lines = ConsolidatedText.Take(ActStart + 1).Concat(
            [
                "WYKAZ ZAREJESTROWANYCH KANCELARII",
                "(według stanu na dzień 31 grudnia 2024 r.)",
            ]);

            var xml = LegalDocumentConverter.Convert(Parse(lines));

            var root = Assert.Single(xml.Elements());
            Assert.Equal("obwieszczenie", root.Name.LocalName);
            Assert.Equal(2, root.Elements().Count(e => e.Name.LocalName == "punkt-OTJ"));
            Assert.Single(root.Elements(), e => e.Name.LocalName == "organ-podpisujacy");
            var act = Assert.Single(root.Elements(), e => e.Name.LocalName == "inny-akt-prawny");
            Assert.Equal("zalacznik-do", act.Elements().First().Name.LocalName);
            Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName == "ustawa");
        }

        [Fact]
        public void AnnouncedResolutionBuiltOfArticles_IsAResolution_NotAStatute()
        {
            // Korpus: 349310_3127449 — Regulamin Senatu: uchwała zbudowana z artykułów. Schemat przewiduje
            // w uchwale paragrafy, ale o elemencie rozstrzyga rodzaj aktu; przedtem obwieszczenie znikało,
            // a uchwała wychodziła jako ustawa.
            string[] lines =
            [
                "OBWIESZCZENIE",
                "MARSZAŁKA SENATU RZECZYPOSPOLITEJ POLSKIEJ",
                "z dnia 20 listopada 2025 r.",
                "w sprawie ogłoszenia jednolitego tekstu uchwały Senatu Rzeczypospolitej Polskiej – Regulamin Senatu",
                "1. Na podstawie art. 16 ust. 3 pkt 1 ustawy z dnia 20 lipca 2000 r. o ogłaszaniu aktów normatywnych ogłasza się jednolity tekst uchwały.",
                "Marszałek Senatu: M. Kidawa-Błońska",
                "Załącznik do obwieszczenia Marszałka Senatu Rzeczypospolitej Polskiej z dnia 20 listopada 2025 r. (M.P. poz. 1251)",
                "UCHWAŁA",
                "SENATU RZECZYPOSPOLITEJ POLSKIEJ",
                "z dnia 23 listopada 1990 r.",
                "Regulamin Senatu",
                "Art. 1. Senat działa na podstawie Konstytucji.",
                "Art. 2. Senat obraduje na posiedzeniach.",
            ];

            var document = Parse(lines);
            var root = Assert.Single(LegalDocumentConverter.Convert(document).Elements());

            Assert.Equal("obwieszczenie", root.Name.LocalName);
            Assert.Single(root.Elements(), e => e.Name.LocalName == "punkt-OTJ");
            var act = Assert.Single(root.Elements(), e => e.Name.LocalName == "uchwala");
            Assert.Equal(2, act.Elements().Count(e => e.Name.LocalName == "artykul"));
            Assert.All(document.Articles, article => Assert.False(article.IsSection));
        }

        [SchemaPlFact]
        public void ConsolidatedStatute_IsSchemaValid_InItsBody()
        {
            var xml = LegalDocumentConverter.Convert(Parse(ConsolidatedText));

            Assert.Empty(SchemaPl.Validator.ValidateActBody(xml.Elements().First()));
        }

        // ============================================================
        // Kodeks
        // ============================================================

        private static readonly string[] Code =
        [
            "USTAWA",
            "z dnia 6 czerwca 1997 r.",
            "Kodeks karny",
            "Rozdział I",
            "Zasady odpowiedzialności karnej",
            "Art. 1. § 1. Odpowiedzialności karnej podlega ten tylko, kto popełnia czyn zabroniony.",
            "§ 2. Nie stanowi przestępstwa czyn zabroniony, którego społeczna szkodliwość jest znikoma.",
            "Art. 2. Odpowiedzialności karnej podlega ten tylko, na kim ciążył prawny obowiązek.",
        ];

        [Fact]
        public void ConsolidatedCode_IsEmbeddedAsACode()
        {
            var lines = ConsolidatedText.Take(ActStart + 1).Concat(Code);

            var announcement = LegalDocumentConverter.Convert(Parse(lines)).Elements().Single();

            Assert.Equal("obwieszczenie", announcement.Name.LocalName);
            var act = Assert.Single(announcement.Elements(), e => e.Name.LocalName == "kodeks");
            Assert.Equal("Kodeks karny", Child(act, "tytul").Value);
            Assert.DoesNotContain(announcement.Elements(), e => e.Name.LocalName == "ustawa");
        }

        [Fact]
        public void CodeAnnouncedDirectly_HasTheCodeRoot()
        {
            var xml = LegalDocumentConverter.Convert(Parse(Code));

            Assert.Equal("kodeks", Assert.Single(xml.Elements()).Name.LocalName);
        }

        [Fact]
        public void CodeWithArticlesOutsideSystematizingUnits_KeepsTheStatuteElement()
        {
            // Artykuł wprost w kodeksie ma w schemacie inną budowę; taki akt (w praktyce: wycinek) zostaje ustawą.
            var xml = LegalDocumentConverter.Convert(Parse(Code.Where(line => line != "Rozdział I" && line != "Zasady odpowiedzialności karnej")));

            Assert.Equal("ustawa", Assert.Single(xml.Elements()).Name.LocalName);
        }

        [Fact]
        public void StatuteAmendingACode_IsAnOrdinaryStatute()
        {
            var lines = Code.ToArray();
            lines[2] = "o zmianie ustawy – Kodeks karny";

            var xml = LegalDocumentConverter.Convert(Parse(lines));

            Assert.Equal("ustawa", Assert.Single(xml.Elements()).Name.LocalName);
        }

        [SchemaPlFact]
        public void Code_IsSchemaValid_InItsBody_DirectlyAndConsolidated()
        {
            Assert.Empty(SchemaPl.Validator.ValidateActBody(LegalDocumentConverter.Convert(Parse(Code)).Elements().First()));
            Assert.Empty(SchemaPl.Validator.ValidateActBody(
                LegalDocumentConverter.Convert(Parse(ConsolidatedText.Take(ActStart + 1).Concat(Code))).Elements().First()));
        }

        // ============================================================
        // Dane czasowe tekstu jednolitego (ADR-0033)
        // ============================================================

        private static XElement ExportWithJournalHeader() => ExportWithJournalHeader(ConsolidatedText, out _);

        private static XElement ExportWithJournalHeader(IEnumerable<string> lines, out LegalDocument document)
        {
            var blocks = new List<DocumentBlock>
            {
                new() { Text = "DZIENNIK USTAW RZECZYPOSPOLITEJ POLSKIEJ", Role = BlockRole.JournalHeader },
                new() { Text = "Warszawa, dnia 11 sierpnia 2025 r.", Role = BlockRole.JournalHeader },
                new() { Text = "Poz. 1094", Role = BlockRole.JournalHeader },
            };
            blocks.AddRange(lines.Select(line => Block(line)));
            document = LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
            return LegalDocumentConverter.Convert(document);
        }

        // Wzór obwieszczenia z § 104 ZTP: punkt 1 podaje dzień stanu prawnego tekstu jednolitego.
        private static string[] WithLegalStateDay(string day)
        {
            var lines = ConsolidatedText.ToArray();
            lines[4] = "1. Na podstawie art. 16 ust. 1 ustawy z dnia 20 lipca 2000 r. o ogłaszaniu aktów normatywnych ogłasza się "
                + "w załączniku do niniejszego obwieszczenia jednolity tekst ustawy z dnia 11 sierpnia 2001 r. o odbudowie obiektów "
                + $"budowlanych (Dz. U. z 2024 r. poz. 1), z uwzględnieniem stanu prawnego na dzień {day} oraz zmian wprowadzonych:";
            return lines;
        }

        private static XElement? Metadata(XElement xml, string name) =>
            xml.Descendants().SingleOrDefault(e => e.Name.LocalName == name);

        [Fact]
        public void ConsolidatedTextVersion_IsValidFromTheConsolidationEvent()
        {
            // Jak w przykładzie autorów schematu (DU20261261_obw_tj.xml): wersja obowiązuje od ujednolicenia.
            var xml = ExportWithJournalHeader();

            var validity = Metadata(xml, "DataObowiazywaniaWersji")!;
            Assert.Equal("zdarz_ujednolicenie_2025-08-11_DU-2025-1094_domyslne", validity.Elements().Single(e => e.Name.LocalName == "Od").Value);

            // Obwieszczenie według wcześniejszego wzoru nie podaje dnia stanu prawnego — nie zgadujemy go.
            Assert.Null(Metadata(xml, "DataStanuPrawnegoTJ"));
        }

        [Fact]
        public void LegalStateDay_StatedInTheAnnouncement_IsTheLegalStateDate()
        {
            var xml = ExportWithJournalHeader(WithLegalStateDay("25 lipca 2025 r."), out var document);

            Assert.Equal(new DateTime(2025, 7, 25), document.Announcement!.LegalStateDate);
            Assert.Equal("2025-07-25", Metadata(xml, "DataStanuPrawnegoTJ")!.Value);

            // Kolejność ze schematu: wersja, potem stan prawny.
            var names = Metadata(xml, "DataObowiazywaniaWersji")!.Parent!.Elements().Select(e => e.Name.LocalName).ToList();
            Assert.Equal(names.IndexOf("DataObowiazywaniaWersji") + 1, names.IndexOf("DataStanuPrawnegoTJ"));
        }

        [Fact]
        public void LegalStateDay_LeftBlankInTheAnnouncement_IsNotADate()
        {
            // Korpus: 347307_3175441 — wzór z wykropkowanym dniem.
            var xml = ExportWithJournalHeader(WithLegalStateDay("…"), out var document);

            Assert.Null(document.Announcement!.LegalStateDate);
            Assert.Null(Metadata(xml, "DataStanuPrawnegoTJ"));
        }

        [SchemaPlFact]
        public void ConsolidatedStatute_WithLegalStateDay_IsSchemaValid()
        {
            Assert.Empty(SchemaPl.Validator.Validate(ExportWithJournalHeader(WithLegalStateDay("25 lipca 2025 r."), out _)));
        }

        [Fact]
        public void ConsolidatedText_HasOneConsolidationEvent_AndNoUnitDates()
        {
            var xml = ExportWithJournalHeader();
            const string eventId = "zdarz_ujednolicenie_2025-08-11_DU-2025-1094_domyslne";

            // „Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia" w art. 2 mówi o ogłoszeniu ustawy,
            // nie obwieszczenia — z daty dokumentu nie wolno wyliczać wejścia w życie.
            var lifecycleEvent = Assert.Single(xml.Descendants(), e => e.Name.LocalName == "Zdarzenie");
            Assert.Equal(eventId, lifecycleEvent.Attribute("id")!.Value);
            Assert.Equal("ujednolicenie", lifecycleEvent.Attribute("rodzaj")!.Value);
            Assert.Equal("2025-08-11", lifecycleEvent.Attribute("data")!.Value);

            var group = Assert.Single(xml.Descendants(), e => e.Name.LocalName == "GrupaCzasowa");
            Assert.Equal($"okr_ujednolicenie.{eventId}", group.Attribute("id")!.Value);
            Assert.Equal("true", group.Attribute("glowna")!.Value);
            var interval = Assert.Single(group.Elements());
            Assert.Equal("ujednolicenie", interval.Attribute("rodzaj")!.Value);
            Assert.Equal(eventId, interval.Attribute("od")!.Value);

            Assert.DoesNotContain(xml.Descendants(), e => e.Attribute("wejscie-w-zycie") != null || e.Attribute("okres") != null);
        }

        [SchemaPlFact]
        public void ConsolidatedStatute_IsSchemaValid_AsAWhole()
        {
            Assert.Empty(SchemaPl.Validator.Validate(ExportWithJournalHeader()));
        }

        [Theory]
        [InlineData("o Policji")]
        [InlineData("o Państwowej Straży Pożarnej[1)]")]
        [InlineData("Kodeks cywilny[1)]")]
        [InlineData("Prawo restrukturyzacyjne")]
        public void AnnouncedActSubject_MayStartWithAProperName(string subject)
        {
            var lines = ConsolidatedText.ToArray();
            lines[ActSubjectLine] = subject;

            Assert.Equal(subject, Parse(lines).Announcement!.ActSubject);
        }
    }
}
