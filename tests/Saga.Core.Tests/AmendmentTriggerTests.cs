using Saga.Core.Ingest;
using Saga.Core.Services.Parsing;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Zwrot zmiany brzmienia zapowiada treść cytowaną w kolejnych akapitach tylko wtedy, gdy zostawia brzmienie
    /// otwarte (ADR-0045) — przepis, który podaje brzmienie w tym samym akapicie, nie jest komendą nowelizacyjną
    /// i nie może połknąć dalszej części aktu.
    /// </summary>
    public class AmendmentTriggerTests
    {
        private static DocumentBlock Text(string text) => new() { Text = text };

        private static LegalDocument Parse(params string[] lines) =>
            LegalDocumentParser.Parse(
                lines.Select(Text).ToList(), new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

        [Theory]
        // Komenda kończy się dwukropkiem; brzmienie stoi w kolejnych akapitach.
        [InlineData("2) w art. 5 ust. 2 otrzymuje brzmienie:", true)]
        [InlineData("po art. 5 dodaje się art. 5a w brzmieniu:", true)]
        // Cytat otwarty w tym samym akapicie i niedomknięty: brzmienie ciągnie się dalej.
        [InlineData("ust. 2 otrzymuje brzmienie: „2. Agencja jest państwową osobą prawną,", true)]
        // Brzmienie mieści się w akapicie — bez cudzysłowu albo w cudzysłowie domkniętym.
        [InlineData("5. W stosunkach z zagranicą Agencja posługuje się nazwą w brzmieniu: Polish Air Navigation Services Agency (PANSA).", false)]
        [InlineData("3. Farmaceuta składa ślubowanie w brzmieniu: „Ślubuję uroczyście sumiennie wykonywać swoje obowiązki.”.", false)]
        [InlineData("3) „Treść ustaleń na str. ........ otrzymuje brzmienie: ................”.", false)]
        [InlineData("1. Opłatę wnosi się w terminie 14 dni.", false)]
        public void ModificationPhrase_AnnouncesContent_OnlyWhenItLeavesTheWordingOpen(string text, bool expected)
        {
            Assert.Equal(expected, AmendmentFinalizer.LeavesWordingOpen(text));
        }

        [Fact]
        public void ProvisionGivingAWordingInline_DoesNotSwallowTheRestOfTheAct()
        {
            // Korpus: 341293_3030889 — bez stylów ten ustęp otwierał cytat, w którym ginęło 219 jednostek ustawy.
            var document = Parse(
                "USTAWA", "z dnia 8 grudnia 2006 r.", "o Polskiej Agencji Żeglugi Powietrznej",
                "Art. 1. 1. Tworzy się Polską Agencję Żeglugi Powietrznej.",
                "2. W stosunkach z zagranicą Agencja posługuje się nazwą w języku angielskim w brzmieniu: Polish Air Navigation Services Agency (PANSA).",
                "Art. 2. 1. Agencja podlega ministrowi właściwemu do spraw transportu.",
                "2. Agencja działa na podstawie ustawy.",
                "Art. 3. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

            Assert.Equal(new[] { "1", "2", "3" }, document.Articles.Select(article => article.Number?.Value));
            Assert.Equal(2, document.Articles.First().Paragraphs.Count);
            Assert.All(document.Articles.SelectMany(article => article.Paragraphs), paragraph => Assert.Null(paragraph.Amendment));
        }

        [Fact]
        public void QuotedFormulaWithTheWordsOfACommand_DoesNotSwallowTheRestOfTheAct()
        {
            // Korpus: 327653_2981670 § 17 ust. 8 — wzór adnotacji w protokole zawiera słowa „otrzymuje brzmienie:";
            // za nim ginęło dziesięć paragrafów i załącznik, także ze stylami.
            var document = Parse(
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o kontroli",
                "Art. 1. Zespół inspektorów dokonuje zmian w protokole w ten sposób, że dołącza do niego tekst w brzmieniu:",
                "1) „Ustalenia na str. ........ skreśla się.”;",
                "2) „Treść ustaleń na str. ........ otrzymuje brzmienie: ................”.",
                "Art. 2. Zespół inspektorów zajmuje stanowisko wobec zastrzeżeń na piśmie.",
                "Art. 3. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

            Assert.Equal(new[] { "1", "2", "3" }, document.Articles.Select(article => article.Number?.Value));
        }

        // ============================================================
        // Treść cytowana bez cudzysłowu otwierającego i bez stylów (ADR-0048)
        // ============================================================

        private const string Command =
            "Art. 1. W ustawie z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) w art. 5 ust. 2 otrzymuje brzmienie:";

        [Theory]
        // Cudzysłów otwierający zgubiony w źródle; zamykający jest.
        [InlineData("2. Opłatę wnosi się w terminie 14 dni.”.")]
        // Oba zgubione.
        [InlineData("2. Opłatę wnosi się w terminie 14 dni.")]
        public void QuotedContentWithoutAnOpeningQuote_EndsBeforeTheNextArticleOfTheAmendingAct(string quoted)
        {
            var document = Parse(
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o zmianie ustawy o opłatach",
                Command,
                quoted,
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

            // Przedtem art. 2 ustawy zmieniającej stawał się artykułem cytowanym, a ustawa miała jeden artykuł.
            Assert.Equal(new[] { "1", "2" }, document.Articles.Select(article => article.Number?.Value));
            var content = document.Articles.First().Paragraphs.Single().Amendment!.Content!;
            Assert.Single(content.Paragraphs);
            Assert.Empty(content.Articles);
        }

        [Fact]
        public void QuotedContentWithoutQuotes_EndsBeforeTheSignature()
        {
            var document = Parse(
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o zmianie ustawy o opłatach",
                Command,
                "2. Opłatę wnosi się w terminie 14 dni.",
                "Prezydent Rzeczypospolitej Polskiej: A. Duda");

            Assert.Equal("Duda", document.Signature?.Surname);
            Assert.Single(document.Articles.Single().Paragraphs.Single().Amendment!.Content!.Paragraphs);
        }

        [Fact]
        public void QuotedArticleWithAnotherNumber_StaysQuoted_WhenQuotesAreMissing()
        {
            // Artykuł cytowany ma numer z aktu zmienianego; treść kończy dopiero kolejny artykuł aktu zmieniającego.
            var document = Parse(
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o zmianie ustawy o opłatach",
                "Art. 1. W ustawie z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) po art. 15 dodaje się art. 15a i 15b w brzmieniu:",
                "Art. 15a. Opłatę wnosi się w terminie 14 dni.",
                "Art. 15b. Opłaty nie zwraca się.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

            Assert.Equal(new[] { "1", "2" }, document.Articles.Select(article => article.Number?.Value));
            Assert.Equal(2, document.Articles.First().Paragraphs.Single().Amendment!.Content!.Articles.Count);
        }

        [Theory]
        [InlineData("2. Opłatę wnosi się w terminie 14 dni.”.", true)]
        [InlineData("Opłatę wnosi minister, zwany dalej „ministrem”.", false)]
        [InlineData("2. Opłatę wnosi się w terminie 14 dni.", false)]
        [InlineData("„2. Opłatę wnosi się w terminie 14 dni.”.", false)]
        public void ClosingQuoteWithoutItsOpening_ClosesAQuotationWhoseOpeningIsMissing(string text, bool expected)
        {
            Assert.Equal(expected, QuoteBalanceTracker.ClosesUnopenedQuote(text));
        }

        [Fact]
        public void CommandEndingWithTheColon_StillTakesItsQuotedContent()
        {
            var document = Parse(
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o zmianie ustawy o opłatach",
                "Art. 1. W ustawie z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) w art. 5 ust. 2 otrzymuje brzmienie:",
                "„2. Opłatę wnosi się w terminie 14 dni.”.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

            Assert.Equal(new[] { "1", "2" }, document.Articles.Select(article => article.Number?.Value));
            var amendment = document.Articles.First().Paragraphs.Single().Amendment;
            Assert.NotNull(amendment);
            Assert.Single(amendment.Content!.Paragraphs);
        }
    }
}
