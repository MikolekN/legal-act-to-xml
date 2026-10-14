using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Core.Services.Parsing;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Nowelizacja zagnieżdżona (ADR-0082): cytowany przepis, który sam jest komendą („4) po art. 28a dodaje się art. 28b
    /// w brzmieniu:"), dostaje własną nowelizację z cytatem, który po nim stoi — jego akapity nie są jednostkami cytatu
    /// zewnętrznego.
    /// </summary>
    public class NestedAmendmentTests
    {
        private static CollectedAmendmentParagraph Line(string text) => new(text, null, null);

        [Fact]
        public void QuoteOpenedAfterACommand_IsNestedInTheQuotedProvision()
        {
            var items = NestedQuotes.Split(
            [
                Line("4) po art. 28a dodaje się art. 28b w brzmieniu:"),
                Line("„Art. 28b. 1. Przewoźnik zgłasza przewóz."),
                Line("2. Zgłoszenie zawiera dane przewoźnika.”;"),
                Line("5) w art. 30 uchyla się ust. 2;"),
            ]);

            Assert.Equal(["4) po art. 28a dodaje się art. 28b w brzmieniu:", "5) w art. 30 uchyla się ust. 2;"],
                items.Select(item => item.Paragraph.Text));
            Assert.Equal(2, items[0].Nested.Count);
            Assert.Empty(items[1].Nested);
        }

        [Fact]
        public void NestedQuoteClosedInItsOnlyParagraph_IsNestedToo()
        {
            var items = NestedQuotes.Split(
            [
                Line("1) w art. 34 dodaje się lit. d w brzmieniu:"),
                Line("„d) umowy, o której mowa w art. 34a ust. 1;”;"),
                Line("2) uchyla się art. 35;"),
            ]);

            Assert.Equal(2, items.Count);
            Assert.Single(items[0].Nested);
        }

        [Theory]
        // Korpus: 349921_3119995 — dwukropek pominięty albo zastąpiony średnikiem, zgubione „w".
        [InlineData("– pkt 1 otrzymuje brzmienie")]
        [InlineData("2) w art. 21 ust. 1 otrzymuje brzmienie;")]
        [InlineData("Art. 99. W ustawie z dnia 8 maja 1997 r. o poręczeniach (Dz. U. z 2022 r. poz. 445) w art. 34g w ust. 3 po pkt 1a dodaje się pkt 1b brzmieniu:")]
        [InlineData("2) po art. 5 dodaje się art. 5a w następującym brzmieniu:")]
        public void CommandWithAMistakeInItsEnding_StillOpensTheNestedQuote(string command)
        {
            var items = NestedQuotes.Split([Line(command), Line("„1) podjęciu uchwały o przyjęciu programu;”;")]);

            Assert.Single(Assert.Single(items).Nested);
        }

        [Fact]
        public void QuotedWordsAndAQuoteWithoutACommand_AreNotNested()
        {
            var items = NestedQuotes.Split(
            [
                Line("1) w art. 12 wyrazy „art. 40” zastępuje się wyrazami „art. 41”;"),
                Line("2) art. 13 otrzymuje brzmienie: „Art. 13. Ślubowanie brzmi:"),
                Line("„Ślubuję uroczyście.”.”;"),
            ]);

            // Cudzysłów otwarty w środku punktu 2 domyka się w następnym akapicie — to dalszy wiersz punktu.
            Assert.Equal(2, items.Count);
            Assert.All(items, item => Assert.Empty(item.Nested));
            Assert.Equal(["„Ślubuję uroczyście.”.”;"], items[1].Continued.Select(line => line.Text));
        }

        [Fact]
        public void LinesOfAQuoteOpenedInsideAParagraph_ContinueThatParagraph()
        {
            // Korpus: 355525_3152691 — nowe wyrazy na kilka wierszy.
            var items = NestedQuotes.Split(
            [
                Line("a) w ust. 1 wyrazy „30 czerwca” zastępuje się wyrazami „ , a w przypadku dodatku za 2024 r.:"),
                Line("1) do dnia 15 lipca – w 2024 r.;"),
                Line("2) do dnia 15 czerwca – w latach 2025–2026 r”,"),
                Line("b) uchyla się ust. 1a."),
            ]);

            Assert.Equal(2, items.Count);
            Assert.Equal(2, items[0].Continued.Count);
            Assert.Empty(items[1].Continued);
        }

        [Fact]
        public void TableOpeningAQuoteAfterACommand_IsANestedQuote()
        {
            static DocumentBlock Cell(int column, string text) =>
                new() { Text = text, TableCell = new BlockTableCell { Table = 0, Row = 0, Column = column } };
            var table = ContentTables.FromBlocks([Cell(0, "„2a"), Cell(1, "Naruszenie obowiązku"), Cell(2, "1500”;")]);

            var items = NestedQuotes.Split(
            [
                Line("18) w załączniku nr 1 po lp. 2 dodaje się lp. 2a w brzmieniu:"),
                new CollectedAmendmentParagraph(string.Empty, null, null) { Table = table },
                Line("19) uchyla się art. 7."),
            ]);

            Assert.Equal(2, items.Count);
            Assert.NotNull(Assert.Single(items[0].Nested).Table);
        }

        [Fact]
        public void QuoteLeftOpenToTheEnd_TakesNoLines()
        {
            // Literówka: cudzysłów niedomknięty do końca treści nie wciąga dalszych jednostek.
            var items = NestedQuotes.Split(
            [
                Line("a) w ust. 1 wyrazy „30 czerwca zastępuje się wyrazami „15 lipca”,"),
                Line("b) uchyla się ust. 1a."),
            ]);

            Assert.Equal(2, items.Count);
            Assert.All(items, item => Assert.Empty(item.Continued));
        }

        // ============================================================
        // Parsowanie i eksport
        // ============================================================

        private static LegalDocument Parse(params string[] provisions)
        {
            string[] lines =
            [
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o zmianie niektórych ustaw",
                .. provisions,
                "Art. 9. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
            ];
            return LegalDocumentParser.Parse(
                lines.Select(line => new DocumentBlock { Text = line }).ToList(),
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
        }

        private static readonly string[] AmendmentOfAnAmendingProvision =
        [
            "Art. 1. W ustawie z dnia 28 lipca 2023 r. o delegowaniu kierowców (Dz. U. poz. 1523) wprowadza się następujące zmiany:",
            "1) w art. 39 pkt 4 otrzymuje brzmienie:",
            "„4) po art. 28a dodaje się art. 28b w brzmieniu:",
            "„Art. 28b. 1. Przewoźnik zgłasza przewóz.",
            "2. Zgłoszenie zawiera dane przewoźnika.”;”;",
            "2) uchyla się art. 40.",
        ];

        [Fact]
        public void QuotedProvisionThatIsACommand_HasItsOwnAmendment()
        {
            var points = Parse(AmendmentOfAnAmendingProvision).Articles.First().Paragraphs.Single().Points;

            var quoted = Assert.Single(points[0].Amendment!.Content!.Points);
            Assert.Equal("4", quoted.Number!.Value);
            Assert.Empty(points[0].Amendment!.Content!.Articles);

            var nested = quoted.Amendment!;
            Assert.Equal(AmendmentOperationType.Insertion, nested.OperationType);
            Assert.Equal("art_28a", nested.Targets.Single().Structure.ToString());
            var article = Assert.Single(nested.Content!.Articles);
            Assert.Equal("28b", article.Number!.Value);
            Assert.Equal(2, article.Paragraphs.Count);

            // Przepis za cytatem zostaje przepisem aktu.
            Assert.Equal(AmendmentOperationType.Repeal, points[1].Amendment!.OperationType);
        }

        [Fact]
        public void NestedAmendment_IsExportedInsideTheQuotedProvision()
        {
            var xml = LegalDocumentConverter.Convert(Parse(AmendmentOfAnAmendingProvision));

            var outer = xml.Descendants().First(e => e.Name.LocalName == "cytat-strukt");
            Assert.Equal("DU/2023/1523__art_39__pkt_4", outer.Attribute("element-eId")?.Value);
            var quotedPoint = Assert.Single(outer.Elements());
            Assert.Equal("punkt", quotedPoint.Name.LocalName);

            var nested = Assert.Single(quotedPoint.Descendants(), e => e.Name.LocalName == "cytat-strukt");
            Assert.Equal("dodaj-po", nested.Attribute("funkcja")?.Value);
            Assert.Equal("artykul", Assert.Single(nested.Elements()).Name.LocalName);
        }

        [Fact]
        public void NestedAmendment_TakesTheActNamedByTheQuotedArticle()
        {
            // Korpus: 354691_3138944.
            var points = Parse(
                "Art. 1. W ustawie z dnia 7 lipca 2023 r. o zmianie ustawy o finansach publicznych (Dz. U. poz. 1500) wprowadza się następujące zmiany:",
                "1) art. 6 otrzymuje brzmienie:",
                "„Art. 6. W ustawie z dnia 27 sierpnia 2009 r. o finansach publicznych (Dz. U. z 2025 r. poz. 1483) wprowadza się następujące zmiany:",
                "1) w art. 34 w ust. 1 w pkt 5 dodaje się lit. d w brzmieniu:",
                "„d) umowy, o której mowa w art. 34a ust. 1;”;”;",
                "2) uchyla się art. 7.").Articles.First().Paragraphs.Single().Points;

            var quotedArticle = Assert.Single(points[0].Amendment!.Content!.Articles);
            var nested = quotedArticle.Paragraphs.Single().Points.Single().Amendment!;
            Assert.Equal(2025, nested.TargetLegalAct.Year);
            Assert.Equal([1483], nested.TargetLegalAct.Positions);
            Assert.Equal("art_34__ust_1__pkt_5", nested.Targets.Single().Structure.ToString());
            Assert.Equal("d", Assert.Single(nested.Content!.Letters).Number!.Value);
        }

        // Korpus: 309528_2762939 — ustawa zmienia przepis ustawy zmieniającej, który zmienia wyrazy i uchyla ustęp.
        private static readonly string[] QuotedCommandsWithoutQuotes =
        [
            "Art. 1. W ustawie z dnia 12 kwietnia 2024 r. o zmianie ustawy o rachunkowości (Dz. U. poz. 619) art. 2 otrzymuje brzmienie:",
            "„Art. 2. W ustawie z dnia 20 sierpnia 1997 r. o Krajowym Rejestrze Sądowym (Dz. U. z 2024 r. poz. 979) wprowadza się następujące zmiany:",
            "1) w art. 12 w ust. 4 wyrazy „art. 40 pkt 2–5c” zastępuje się wyrazami „art. 40 pkt 2–5d”;",
            "2) w art. 20 uchyla się ust. 3.”.",
        ];

        [Fact]
        public void QuotedProvisionsThatChangeWordsOrRepeal_HaveTheirOwnAmendments()
        {
            var quoted = Parse(QuotedCommandsWithoutQuotes).Articles.First().Paragraphs.Single().Amendment!.Content!.Articles.Single();
            var points = quoted.Paragraphs.Single().Points;

            var words = points[0].Amendment!;
            Assert.Equal(AmendmentOperationType.Modification, words.OperationType);
            var change = Assert.Single(words.TextChanges);
            Assert.Equal(("art. 40 pkt 2–5c", "art. 40 pkt 2–5d"), (change.OldText, change.NewText));
            Assert.Equal("art_12__ust_4", words.Targets.Single().Structure.ToString());
            Assert.Equal([979], words.TargetLegalAct.Positions);

            var repeal = points[1].Amendment!;
            Assert.Equal(AmendmentOperationType.Repeal, repeal.OperationType);
            Assert.Equal("art_20__ust_3", repeal.Targets.Single().Structure.ToString());
        }

        [Fact]
        public void QuotedProseWithoutACommandLocation_HasNoAmendment()
        {
            // Treść zwykłego aktu w cytacie — słowa „zastępuje się" nie czynią z niej komendy.
            var points = Parse(
                "Art. 1. W ustawie z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) wprowadza się następujące zmiany:",
                "1) art. 5 otrzymuje brzmienie:",
                "„Art. 5. Opłatę za wyrazy „pilne” zastępuje się opłatą dodatkową.”.").Articles.First().Paragraphs.Single().Points;

            var quoted = points[0].Amendment!.Content!.Articles.Single();
            Assert.All(quoted.Paragraphs, paragraph => Assert.Null(paragraph.Amendment));
        }

        // Korpus: 355525_3152691 — cytowany przepis zmienia wyrazy na nowe, rozpisane na kilka wierszy.
        private static readonly string[] QuotedWordsOverSeveralLines =
        [
            "Art. 1. W ustawie z dnia 7 lipca 2023 r. o zmianie ustawy o dodatku (Dz. U. poz. 1500) art. 6 otrzymuje brzmienie:",
            "„Art. 6. W ustawie z dnia 17 grudnia 2021 r. o dodatku osłonowym (Dz. U. z 2023 r. poz. 759) wprowadza się następujące zmiany:",
            "1) w art. 4a:",
            "a) w ust. 1 wyrazy „30 czerwca” zastępuje się wyrazami „ , a w przypadku dodatku za 2024 r.:",
            "1) do dnia 15 lipca – w 2024 r.;",
            "2) do dnia 15 czerwca – w latach 2025–2026 r”,",
            "b) uchyla się ust. 1a;",
            "2) w art. 15 wyrazy „2024 r.” zastępuje się wyrazami „2025 r.”.”.",
        ];

        [Fact]
        public void LinesOfNewWords_AreTheTextOfTheQuotedProvision_NotItsUnits()
        {
            var quoted = Parse(QuotedWordsOverSeveralLines).Articles.First().Paragraphs.Single().Amendment!.Content!.Articles.Single();
            var points = quoted.Paragraphs.Single().Points;
            var letters = points[0].Letters;

            Assert.Equal(["1", "2"], points.Select(point => point.Number!.Value));
            Assert.Equal(["a", "b"], letters.Select(letter => letter.Number!.Value));
            Assert.Equal(["1) do dnia 15 lipca – w 2024 r.;", "2) do dnia 15 czerwca – w latach 2025–2026 r”,"],
                letters[0].Continuation.Select(block => block.Text));
        }

        [SchemaPlFact]
        public void QuotedWordsOverSeveralLines_AreSchemaValid()
        {
            Assert.Empty(SchemaPl.Validator.ValidateActBody(
                LegalDocumentConverter.Convert(Parse(QuotedWordsOverSeveralLines)).Elements().First()));
        }

        [SchemaPlFact]
        public void QuotedCommandsWithoutQuotes_AreSchemaValid()
        {
            Assert.Empty(SchemaPl.Validator.ValidateActBody(
                LegalDocumentConverter.Convert(Parse(QuotedCommandsWithoutQuotes)).Elements().First()));
        }

        [SchemaPlFact]
        public void NestedAmendment_IsSchemaValid()
        {
            Assert.Empty(SchemaPl.Validator.ValidateActBody(
                LegalDocumentConverter.Convert(Parse(AmendmentOfAnAmendingProvision)).Elements().First()));
        }
    }
}
