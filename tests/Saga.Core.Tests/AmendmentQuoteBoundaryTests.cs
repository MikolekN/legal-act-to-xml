using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Core.Services.Parsing;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Graniczne cudzysłowy cytatu nowelizacyjnego (§ 94 ZTP): znak otwierający „ z początku
    /// pierwszego akapitu i zamykający ” z końca ostatniego NIE należą do treści zmienianej
    /// jednostki — opisują cytat. Parser je zdejmuje (<c>AmendmentStateManager.TrimQuoteBoundaries</c>),
    /// a konwerter odtwarza w atrybucie <c>cudzyslowy</c> (otwierajacy, zamykajacy, oba,
    /// brak — blok środkowy).
    ///
    /// Rozróżnienie kluczowe dla poprawności: cudzysłów WEWNĘTRZNY („zwany dalej „PCOC””) jest
    /// częścią treści i musi przetrwać; zdejmowana jest wyłącznie granica.
    /// </summary>
    public class AmendmentQuoteBoundaryTests
    {
        // ============================================================
        // TrimQuoteBoundaries — reguła zdejmowania granicy
        // ============================================================

        private static AmendmentCollector CollectorWith(params string[] paragraphs)
        {
            var collector = new AmendmentCollector();
            foreach (var text in paragraphs)
                collector.AddParagraph(text, null);
            return collector;
        }

        [Fact]
        public void TrimQuoteBoundaries_SingleParagraph_StripsBothQuotesAndReportsPunctuation()
        {
            var collector = CollectorWith("„Art. 5. Nowe brzmienie artykułu.”,");

            var quotes = AmendmentStateManager.TrimQuoteBoundaries(collector);

            Assert.True(quotes.HasOpening);
            Assert.True(quotes.HasClosing);
            Assert.Equal(",", quotes.Punctuation);
            Assert.Equal("Art. 5. Nowe brzmienie artykułu.", collector.Paragraphs[0].Text);
        }

        [Fact]
        public void TrimQuoteBoundaries_MultipleParagraphs_TouchesOnlyFirstAndLast()
        {
            var collector = CollectorWith(
                "„Art. 5. Prawo podlega ograniczeniu:",
                "1) w zakresie tajemnic chronionych;",
                "2) ze względu na prywatność.”;");

            var quotes = AmendmentStateManager.TrimQuoteBoundaries(collector);

            Assert.True(quotes.HasOpening);
            Assert.True(quotes.HasClosing);
            Assert.Equal(";", quotes.Punctuation);
            Assert.Equal("Art. 5. Prawo podlega ograniczeniu:", collector.Paragraphs[0].Text);
            Assert.Equal("1) w zakresie tajemnic chronionych;", collector.Paragraphs[1].Text);
            Assert.Equal("2) ze względu na prywatność.", collector.Paragraphs[2].Text);
        }

        [Fact]
        public void TrimQuoteBoundaries_InnerQuote_SurvivesBoundaryRemoval()
        {
            // Cudzysłów wewnętrzny należy do treści — zdejmowana jest tylko granica.
            var collector = CollectorWith("„4) plan, zwany dalej „Krajowym planem”.”,");

            var quotes = AmendmentStateManager.TrimQuoteBoundaries(collector);

            Assert.True(quotes.HasClosing);
            Assert.Equal(",", quotes.Punctuation);
            Assert.Equal("4) plan, zwany dalej „Krajowym planem”.", collector.Paragraphs[0].Text);
        }

        [Fact]
        public void TrimQuoteBoundaries_StraightQuotes_AreRecognised()
        {
            // Kanał TXT/PDF (i część edytorów) daje proste " zamiast pary „ ”.
            var collector = CollectorWith("\"Art. 5. Nowe brzmienie.\";");

            var quotes = AmendmentStateManager.TrimQuoteBoundaries(collector);

            Assert.True(quotes.HasOpening);
            Assert.True(quotes.HasClosing);
            Assert.Equal(";", quotes.Punctuation);
            Assert.Equal("Art. 5. Nowe brzmienie.", collector.Paragraphs[0].Text);
        }

        [Fact]
        public void TrimQuoteBoundaries_NoClosingQuote_ReportsOpeningOnly_AndKeepsText()
        {
            // Cytat niedomknięty (literówka/OCR) — nie zgadujemy granicy, tekst zostaje.
            var collector = CollectorWith("„Art. 5. Brzmienie bez domknięcia.");

            var quotes = AmendmentStateManager.TrimQuoteBoundaries(collector);

            Assert.True(quotes.HasOpening);
            Assert.False(quotes.HasClosing);
            Assert.Null(quotes.Punctuation);
            Assert.Equal("Art. 5. Brzmienie bez domknięcia.", collector.Paragraphs[0].Text);
        }

        [Fact]
        public void TrimQuoteBoundaries_WithoutQuotes_LeavesTextUntouched()
        {
            var collector = CollectorWith("Art. 5. Treść bez cudzysłowów.");

            var quotes = AmendmentStateManager.TrimQuoteBoundaries(collector);

            Assert.False(quotes.HasOpening);
            Assert.False(quotes.HasClosing);
            Assert.Null(quotes.Punctuation);
            Assert.Equal("Art. 5. Treść bez cudzysłowów.", collector.Paragraphs[0].Text);
        }

        [Fact]
        public void TrimQuoteBoundaries_ClosingQuoteWithoutPunctuation_ReportsNullPunctuation()
        {
            var collector = CollectorWith("„Art. 5. Nowe brzmienie.”");

            var quotes = AmendmentStateManager.TrimQuoteBoundaries(collector);

            Assert.True(quotes.HasClosing);
            Assert.Null(quotes.Punctuation);
            Assert.Equal("Art. 5. Nowe brzmienie.", collector.Paragraphs[0].Text);
        }

        [Fact]
        public void TrimQuoteBoundaries_EmptyCollector_ReportsNoBoundaries()
        {
            var quotes = AmendmentStateManager.TrimQuoteBoundaries(new AmendmentCollector());

            Assert.False(quotes.HasOpening);
            Assert.False(quotes.HasClosing);
            Assert.Null(quotes.Punctuation);
        }

        // ============================================================
        // Potok: granica nie trafia do treści jednostki
        // ============================================================

        private static (ParserOrchestrator orch, ParsingContext ctx) NewPipeline()
        {
            var document = new LegalDocument { Type = LegalActType.Statute };
            var subchapter = document.RootPart.Books[0].Titles[0].Divisions[0].Chapters[0].Subchapters[0];
            return (new ParserOrchestrator(), new ParsingContext(document, subchapter));
        }

        private static DocumentBlock Blk(string text) => new()
        {
            Text = text,
            Source = new BlockSourceLocation { BlockIndex = null },
        };

        private static void Feed(ParserOrchestrator orch, ParsingContext ctx, params string[] lines)
        {
            foreach (var line in lines)
                orch.ProcessBlock(Blk(line), ctx);
        }

        private static Amendment ParseSingleAmendment(params string[] quotedLines)
        {
            var (orch, ctx) = NewPipeline();
            var lines = new List<string>
            {
                "Art. 1. W ustawie wprowadza się następujące zmiany:",
                "1) art. 5 otrzymuje brzmienie:",
            };
            lines.AddRange(quotedLines);
            Feed(orch, ctx, lines.ToArray());
            orch.Finalize(ctx);

            var amendment = ctx.Document.Articles.First().Paragraphs[0].Points.Single().Amendment;
            Assert.NotNull(amendment);
            return amendment!;
        }

        [Fact]
        public void ParsedAmendment_RecordsQuoteBoundaries_AndKeepsThemOutOfContent()
        {
            var amendment = ParseSingleAmendment("„Art. 5. Nowe brzmienie artykułu.”;");

            Assert.True(amendment.HasOpeningQuote);
            Assert.True(amendment.HasClosingQuote);
            Assert.Equal(";", amendment.TrailingPunctuation);

            var quoted = amendment.Content!.Articles.Single();
            var text = quoted.Paragraphs[0].ContentText ?? string.Empty;
            Assert.DoesNotContain("”", text);
            Assert.Equal("Nowe brzmienie artykułu.", text);
        }

        [Fact]
        public void ParsedAmendment_InnerQuote_SurvivesInContent()
        {
            var amendment = ParseSingleAmendment(
                "„Art. 5. Plan działania, zwany dalej „Krajowym planem”.”;");

            var quoted = amendment.Content!.Articles.Single();
            var text = quoted.Paragraphs[0].ContentText ?? string.Empty;
            Assert.Equal("Plan działania, zwany dalej „Krajowym planem”.", text);
        }

        // ============================================================
        // Eksport: atrybut 'cudzyslowy'
        // ============================================================

        private static XDocument ConvertSingleAmendment(params string[] quotedLines)
        {
            var (orch, ctx) = NewPipeline();
            var lines = new List<string>
            {
                "Art. 1. W ustawie wprowadza się następujące zmiany:",
                "1) art. 5 otrzymuje brzmienie:",
            };
            lines.AddRange(quotedLines);
            Feed(orch, ctx, lines.ToArray());
            orch.Finalize(ctx);

            return new XDocument(LegalDocumentConverter.Convert(ctx.Document));
        }

        private static List<XElement> Citations(XDocument doc) =>
            doc.Descendants()
               .Where(e => e.Name.LocalName is "cytat-strukt" or "cytat-w-tekscie")
               .ToList();

        [Fact]
        public void SingleBlockQuote_GetsBothQuotationBits()
        {
            var doc = ConvertSingleAmendment("„Art. 5. Nowe brzmienie artykułu.”;");

            var citation = Assert.Single(Citations(doc));
            // Blok otwiera i zamyka cytat.
            Assert.Equal("oba", citation.Attribute("cudzyslowy")?.Value);
        }

        [Fact]
        public void TrailingPunctuation_MovesOutOfCitation_ToTheSentence()
        {
            var doc = ConvertSingleAmendment("„Art. 5. Nowe brzmienie artykułu.”;");

            var citation = Assert.Single(Citations(doc));
            var body = string.Concat(citation.DescendantNodes().OfType<XText>().Select(t => t.Value));

            // Średnik należy do zdania nowelizującego, nie do cytowanej treści.
            Assert.DoesNotContain("”", body);
            Assert.Equal(";", (citation.NextNode as XText)?.Value);
        }

        [Fact]
        public void UnclosedQuote_DoesNotClaimClosingBit()
        {
            var doc = ConvertSingleAmendment("„Art. 5. Brzmienie bez domknięcia.");

            var citation = Assert.Single(Citations(doc));
            // Otwiera, nie zamyka — granica pozostaje niepewna i XML tego nie ukrywa.
            Assert.Equal("otwierajacy", citation.Attribute("cudzyslowy")?.Value);
        }

        // ============================================================
        // Cytat otwarty a styl ustawy matki
        // ============================================================

        [Theory]
        [InlineData(null)]
        [InlineData("ZLITPKTzmpktliter")]
        [InlineData("PKTpunkt")]
        public void Collector_SeesAQuoteLeftOpen_WhateverTheStyle(string? style)
        {
            var collector = new AmendmentCollector();
            collector.AddParagraph("„Wariant D", style);
            collector.AddParagraph("1. Wykształcenie – co najmniej średnie.", style);
            Assert.True(collector.LeavesQuoteOpen);

            collector.AddParagraph("2. Staż pracy.”,", style);
            Assert.False(collector.LeavesQuoteOpen);
        }

        [Fact]
        public void Collector_WithoutAnOpeningQuote_LeavesNothingOpen()
        {
            var collector = new AmendmentCollector();
            collector.AddParagraph("Art. 5. Opłata wynosi „100 zł", "ZARTzmartartykuempunktem");

            Assert.False(collector.LeavesQuoteOpen);
        }

        private static DocumentBlock Styled(string text, string? style) => new() { Text = text, StyleId = style };

        private static LegalDocument ParseRegulation(params DocumentBlock[] body)
        {
            DocumentBlock[] blocks =
            [
                Styled("ROZPORZĄDZENIE", null), Styled("MINISTRA INFRASTRUKTURY", null), Styled("z dnia 13 stycznia 2026 r.", null),
                Styled("zmieniające rozporządzenie w sprawie pracowników kolejowych", null),
                Styled("Na podstawie art. 22d ust. 3 ustawy z dnia 28 marca 2003 r. o transporcie kolejowym (Dz. U. z 2025 r. poz. 1234) zarządza się, co następuje:", null),
                .. body,
                Styled("§ 2. Rozporządzenie wchodzi w życie po upływie 14 dni od dnia ogłoszenia.", "ARTartustawynprozporzdzenia"),
            ];
            return LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
        }

        [Fact]
        public void LinesOfAnOpenQuoteInTheStyleOfTheAct_AreQuotedContent()
        {
            // Korpus: 338265_3166784 — cytat po komendzie tiretu złożony stylem punktu aktu.
            var document = ParseRegulation(
                Styled("§ 1. W rozporządzeniu Ministra Infrastruktury z dnia 11 stycznia 2021 r. w sprawie pracowników (Dz. U. z 2024 r. poz. 780) wprowadza się następujące zmiany:", "ARTartustawynprozporzdzenia"),
                Styled("1) uchyla się § 39;", "PKTpunkt"),
                Styled("2) w załączniku nr 1 do rozporządzenia:", "PKTpunkt"),
                Styled("a) w pkt 1:", "LITlitera"),
                Styled("– w ppkt I dodaje się wariant D w brzmieniu:", "TIRtiret"),
                Styled("„Wariant D", "PKTpunkt"),
                Styled("1. Wykształcenie – co najmniej średnie.", "PKTpunkt"),
                Styled("2. Staż pracy – odbycie przygotowania zawodowego.”,", "PKTpunkt"),
                Styled("– w ppkt II uchyla się pkt 3,", "TIRtiret"),
                Styled("b) uchyla się pkt 2.", "LITlitera"));

            // Paragraf aktu zmieniającego ma swoje punkty, a nie ustępy z cytatu.
            var section = document.Articles.First();
            var paragraph = Assert.Single(section.Paragraphs);
            Assert.True(paragraph.IsImplicit);
            Assert.Equal(["1", "2"], paragraph.Points.Select(point => point.Number!.Value));

            var letters = paragraph.Points[1].Letters;
            Assert.Equal(["a", "b"], letters.Select(letter => letter.Number!.Value));
            Assert.Equal(2, letters[0].Tirets.Count);
            Assert.Contains(letters[0].Tirets[0].Amendment!.Content!.Paragraphs, quoted => quoted.Number!.Value == "2");
        }

        [Fact]
        public void QuoteLeftOpenByATypo_EndsAtTheNextUnitOfTheAct()
        {
            // Literówka: cytat bez cudzysłowu zamykającego. Kolejny punkt aktu zmieniającego — bez własnej komendy —
            // nie staje się treścią cytowaną.
            var document = ParseRegulation(
                Styled("§ 1. W rozporządzeniu Ministra Infrastruktury z dnia 11 stycznia 2021 r. w sprawie pracowników (Dz. U. z 2024 r. poz. 780) wprowadza się następujące zmiany:", "ARTartustawynprozporzdzenia"),
                Styled("1) uchyla się § 39;", "PKTpunkt"),
                Styled("2) § 5 otrzymuje brzmienie:", "PKTpunkt"),
                Styled("„§ 5. Opłatę pobiera się raz w roku.;", "ZARTzmartartykuempunktem"),
                Styled("3) w § 7:", "PKTpunkt"),
                Styled("a) ust. 1 otrzymuje brzmienie:", "LITlitera"),
                Styled("„1. Opłata wynosi 100 zł.”;", "ZLITUSTzmustliter"),
                Styled("b) uchyla się ust. 2.", "LITlitera"));

            var points = document.Articles.First().Paragraphs.Single().Points;
            Assert.Equal(["1", "2", "3"], points.Select(point => point.Number!.Value));
            Assert.Equal(["a", "b"], points[2].Letters.Select(letter => letter.Number!.Value));
        }
    }
}
