using Saga.Core.Ingest;
using Saga.Core.Services.Parsing;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Cytat nowelizacji otwarty i niedomknięty do końca dokumentu nie wchłania reszty aktu zmieniającego
    /// (ADR-0049): dokument buduje się ponownie, a cytat kończy się przed artykułem, który kontynuuje numerację
    /// aktu zmieniającego.
    /// </summary>
    public class UnclosedQuotationTests
    {
        private const string Command =
            "Art. 1. W ustawie z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) w art. 5 ust. 2 otrzymuje brzmienie:";

        private const string Unclosed = "„2. Opłatę wnosi się w terminie 14 dni.";

        private static readonly string[] Heading = ["USTAWA", "z dnia 14 czerwca 2024 r.", "o zmianie ustawy o opłatach"];

        private static List<DocumentBlock> Blocks(params string[] lines) =>
            Heading.Concat(lines).Select(line => new DocumentBlock { Text = line }).ToList();

        private static LegalDocument Parse(params string[] lines) =>
            LegalDocumentParser.Parse(Blocks(lines), new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

        private static string?[] Numbers(IEnumerable<Saga.Model.EditorialUnits.Article> articles) =>
            articles.Select(article => article.Number?.Value).ToArray();

        private static Amendment AmendmentOf(Saga.Model.EditorialUnits.Article article) =>
            article.Paragraphs.Single().Amendment!;

        [Fact]
        public void UnclosedQuotation_EndsBeforeTheNextArticleOfTheAmendingAct()
        {
            var document = Parse(
                Command,
                Unclosed,
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
                "Art. 3. Przepis końcowy.");

            // Przedtem art. 2 i 3 ustawy zmieniającej były artykułami cytowanymi, a ustawa miała jeden artykuł.
            Assert.Equal(new[] { "1", "2", "3" }, Numbers(document.Articles));
            var content = AmendmentOf(document.Articles.First()).Content!;
            Assert.Single(content.Paragraphs);
            Assert.Empty(content.Articles);
        }

        [Fact]
        public void UnclosedQuotation_KeepsTheWarningAboutItsBoundary()
        {
            var document = Parse(Command, Unclosed, "Art. 2. Ustawa wchodzi w życie z dniem ogłoszenia.");

            Assert.Contains(document.Articles.First().Paragraphs.Single().ValidationMessages,
                message => message.Level == ValidationLevel.Warning && message.Message.Contains("niedomknięty"));
        }

        [Fact]
        public void QuotedArticleWithTheNextNumber_StaysQuoted_WhenTheActsOwnArticleFollows()
        {
            // Cytat kilku artykułów zawiera numer kolejny względem artykułu z komendą; granicą jest dopiero
            // artykuł, po którym numeracja ustawy zmieniającej biegnie dalej.
            var document = Parse(
                "Art. 1. W ustawie z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) art. 1 i 2 otrzymują brzmienie:",
                "„Art. 1. Ustawa określa opłaty.",
                "Art. 2. Opłatę wnosi się w terminie 14 dni.",
                "Art. 2. W ustawie z dnia 3 marca 2022 r. o podatkach (Dz. U. poz. 300) uchyla się art. 7.",
                "Art. 3. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

            Assert.Equal(new[] { "1", "2", "3" }, Numbers(document.Articles));
            Assert.Equal(new[] { "1", "2" }, Numbers(AmendmentOf(document.Articles.First()).Content!.Articles));
            Assert.StartsWith("W ustawie z dnia 3 marca 2022 r.", document.Articles.ElementAt(1).Paragraphs.Single().ContentText);
        }

        [Fact]
        public void SameNumberInALaterQuotation_DoesNotMoveTheBoundary()
        {
            var document = Parse(
                Command,
                Unclosed,
                "Art. 2. W ustawie z dnia 3 marca 2022 r. o podatkach (Dz. U. poz. 300) art. 1 i 2 otrzymują brzmienie:",
                "„Art. 1. Ustawa określa podatki.",
                "Art. 2. Podatek płaci się w terminie 14 dni.”.",
                "Art. 3. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

            Assert.Equal(new[] { "1", "2", "3" }, Numbers(document.Articles));
            Assert.Empty(AmendmentOf(document.Articles.ElementAt(0)).Content!.Articles);
            Assert.Equal(new[] { "1", "2" }, Numbers(AmendmentOf(document.Articles.ElementAt(1)).Content!.Articles));
        }

        [Fact]
        public void TwoUnclosedQuotations_AreBothBounded()
        {
            var document = Parse(
                Command,
                Unclosed,
                "Art. 2. W ustawie z dnia 3 marca 2022 r. o podatkach (Dz. U. poz. 300) art. 7 otrzymuje brzmienie:",
                "„Art. 7. Podatek płaci się w terminie 14 dni.",
                "Art. 3. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
                "Prezydent Rzeczypospolitej Polskiej: A. Duda");

            Assert.Equal(new[] { "1", "2", "3" }, Numbers(document.Articles));
            Assert.Equal(new[] { "7" }, Numbers(AmendmentOf(document.Articles.ElementAt(1)).Content!.Articles));
            Assert.Equal("Duda", document.Signature?.Surname);
        }

        [Fact]
        public void SignatureAfterAnUnclosedQuotation_IsNotQuotedContent()
        {
            var document = Parse(Command, Unclosed, "Prezydent Rzeczypospolitej Polskiej: A. Duda");

            Assert.Equal("Duda", document.Signature?.Surname);
            Assert.Single(AmendmentOf(document.Articles.Single()).Content!.Paragraphs);
        }

        [Fact]
        public void ClosedQuotationOfSeveralArticles_IsLeftAsItIs()
        {
            var document = Parse(
                "Art. 1. W ustawie z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) art. 1 i 2 otrzymują brzmienie:",
                "„Art. 1. Ustawa określa opłaty.",
                "Art. 2. Opłatę wnosi się w terminie 14 dni.”.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

            Assert.Equal(new[] { "1", "2" }, Numbers(document.Articles));
            Assert.Equal(new[] { "1", "2" }, Numbers(AmendmentOf(document.Articles.First()).Content!.Articles));
        }

        [Fact]
        public void DocumentEndingInsideTheQuotation_KeepsWhatItQuotes()
        {
            // Nic nie wygląda na dalszy ciąg aktu zmieniającego — nie ma czego odcinać.
            var document = Parse(Command, Unclosed, "3. Opłaty nie zwraca się.");

            Assert.Equal(2, AmendmentOf(document.Articles.Single()).Content!.Paragraphs.Count);
        }

        [Fact]
        public void UnclosedQuotationInARegulation_EndsBeforeTheNextSection()
        {
            string[] lines =
            [
                "ROZPORZĄDZENIE",
                "MINISTRA FINANSÓW",
                "z dnia 11 maja 2017 r.",
                "zmieniające rozporządzenie w sprawie wykazu osób prawnych",
                "Na podstawie art. 12 ust. 3 ustawy z dnia 15 grudnia 2016 r. o Prokuratorii Generalnej Rzeczypospolitej Polskiej (Dz. U. poz. 2261) zarządza się, co następuje:",
                "§ 1. W rozporządzeniu Ministra Finansów z dnia 2 stycznia 2017 r. w sprawie wykazu osób prawnych (Dz. U. poz. 10) § 4 otrzymuje brzmienie:",
                "„§ 4. Wykaz ogłasza się w Biuletynie Informacji Publicznej.",
                "§ 2. Rozporządzenie wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
            ];
            var document = LegalDocumentParser.Parse(
                lines.Select(line => new DocumentBlock { Text = line }).ToList(),
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

            Assert.Equal(new[] { "1", "2" }, Numbers(document.Articles));
            Assert.Equal(new[] { "4" }, Numbers(AmendmentOf(document.Articles.First()).Content!.Articles));
        }

        [Fact]
        public void Observer_SeesOnlyThePassThatProducesTheResult()
        {
            var blocks = Blocks(Command, Unclosed, "Art. 2. Ustawa wchodzi w życie z dniem ogłoszenia.");
            var seen = new List<DocumentBlock>();

            var document = LegalDocumentParser.ParseBlocks(blocks, observeBlock: (block, _) => seen.Add(block));

            Assert.Equal(blocks.Count, seen.Count);
            Assert.Equal(new[] { "1", "2" }, Numbers(document.Articles));
        }

        // ============================================================
        // Ślad otwartego cytatu
        // ============================================================

        private static EntityNumber Number(int numeric, string letter = "") =>
            new() { NumericPart = numeric, LexicalPart = letter, Value = numeric + letter };

        [Theory]
        [InlineData(2, "", true)]
        // Litera oznacza jednostkę dodaną do aktu zmienianego — akt uchwalany numeruje artykuły po kolei.
        [InlineData(2, "a", false)]
        [InlineData(3, "", false)]
        [InlineData(1, "", false)]
        public void Follows_OnlyThePlainNextNumber(int numeric, string letter, bool expected)
        {
            Assert.Equal(expected, OpenQuotationTrail.Follows(Number(1), Number(numeric, letter)));
        }

        [Fact]
        public void FindBreak_SkipsTheCandidateFollowedByTheSameNumber()
        {
            var quoted = new DocumentBlock { Text = "Art. 2. cytowany" };
            var own = new DocumentBlock { Text = "Art. 2. własny" };
            var trail = new OpenQuotationTrail();
            trail.NoteArticle(quoted, Number(2), depth: 1);
            trail.NoteArticle(own, Number(2), depth: 1);
            trail.NoteArticle(new DocumentBlock { Text = "Art. 3." }, Number(3), depth: 1);

            Assert.Same(own, trail.FindBreak(Number(1)));
        }

        [Fact]
        public void FindBreak_IgnoresArticlesDeeperInQuotes_AndFallsBackToTheFirstCandidate()
        {
            var own = new DocumentBlock { Text = "Art. 2. własny" };
            var trail = new OpenQuotationTrail();
            trail.NoteArticle(own, Number(2), depth: 1);
            // Artykuł w cytacie otwartym za kandydatem nie należy do numeracji aktu zmieniającego.
            trail.NoteArticle(new DocumentBlock { Text = "Art. 9. cytowany" }, Number(9), depth: 2);
            trail.NoteArticle(new DocumentBlock { Text = "Art. 3." }, Number(3), depth: 1);
            Assert.Same(own, trail.FindBreak(Number(1)));

            // Numeracja aktu zmieniającego ma lukę: żaden kandydat nie jest pewny, zostaje pierwszy.
            var gap = new OpenQuotationTrail();
            gap.NoteArticle(own, Number(2), depth: 1);
            gap.NoteArticle(new DocumentBlock { Text = "Art. 4." }, Number(4), depth: 1);
            Assert.Same(own, gap.FindBreak(Number(1)));
        }

        [Fact]
        public void FindBreak_WithoutArticleCandidates_PointsAtTheSignature()
        {
            var signature = new DocumentBlock { Text = "Prezydent Rzeczypospolitej Polskiej: A. Duda" };
            var trail = new OpenQuotationTrail();
            trail.NoteArticle(new DocumentBlock { Text = "Art. 7. cytowany" }, Number(7), depth: 1);
            trail.NoteSignature(signature);

            Assert.Same(signature, trail.FindBreak(Number(1)));

            trail.Reset();
            Assert.Null(trail.FindBreak(Number(1)));
        }
    }
}
