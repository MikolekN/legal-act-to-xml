using Saga.Core.Services;
using Saga.Model.EditorialUnits;

namespace Saga.Core.Tests
{
    public class JournalReferenceServiceTests
    {
        private readonly JournalReferenceService _service = new();

        [Fact]
        public void MissingYear_IsTheYearInTheDateOfTheCitedAct()
        {
            // § 158 ust. 7 ZTP: rocznik pomija się, gdy akt ogłoszono w roku wymienionym w jego dacie.
            var article = new Article
            {
                ContentText = "Art. 1. W ustawie z dnia 7 lipca 2023 r. o zmianie ustawy o planowaniu (Dz. U. poz. 1688 " +
                              "oraz z 2024 r. poz. 1907) wprowadza się następujące zmiany:"
            };

            _service.ParseJournalReferences(article);

            var journal = Assert.Single(article.Journals);
            Assert.Equal(2023, journal.Year);
            Assert.Equal(new[] { 1688 }, journal.Positions);
        }

        [Fact]
        public void MissingYear_DoesNotComeFromTheAmendingActOrTheCalendar()
        {
            // Data wejścia w życie przepisu zmieniającego nie mówi nic o roczniku aktu zmienianego.
            var article = new Article
            {
                EffectiveDate = new DateTime(2026, 4, 13),
                ContentText = "Art. 1. W ustawie z dnia 1 stycznia 2022 r. (Dz. U. poz. 5)"
            };

            _service.ParseJournalReferences(article);

            Assert.Equal(2022, Assert.Single(article.Journals).Year);
        }

        [Fact]
        public void YearWithoutZ_IsReadLikeTheZtpForm()
        {
            // 334479_2972864: „(Dz. U. 2025 r. poz. 881)" — literówka źródła, brak „z" przed rocznikiem.
            var article = new Article
            {
                ContentText = "Art. 4. W ustawie z dnia 7 września 1991 r. o systemie oświaty (Dz. U. 2025 r. poz. 881) w art. 11 po ust. 1 dodaje się ust. 1a w brzmieniu:"
            };

            _service.ParseJournalReferences(article);

            var journal = Assert.Single(article.Journals);
            Assert.Equal(2025, journal.Year);
            Assert.Equal(new[] { 881 }, journal.Positions);
        }

        [Fact]
        public void MissingYear_WithoutAnActDate_LeavesTheJournalOut()
        {
            // Rocznika się nie zgaduje: publikator bez roku i bez daty aktu nie daje identyfikatora ELI.
            var article = new Article
            {
                ContentText = "Art. 1. W ustawie z dnia … r. o planowaniu (Dz. U. poz. 5) wprowadza się następujące zmiany:"
            };

            _service.ParseJournalReferences(article);

            Assert.Empty(article.Journals);
        }

        [Theory]
        // Dz. U. 2026 poz. 1306 (tekst jednolity z przepisami zmieniającymi z 2014 r.): publikator sprzed 2012 r. z numerem
        // dziennika. Pozycja jest unikalna w roczniku — numer nie wchodzi do identyfikatora.
        [InlineData("Art. 19. W ustawie z dnia 6 kwietnia 1990 r. o Policji (Dz. U. z 2011 r. Nr 287, poz. 1687, ze zm.) " +
                    "w art. 20 wprowadza się następujące zmiany:", 2011, new[] { 1687 })]
        [InlineData("Art. 20. W ustawie z dnia 6 czerwca 1997 r. – Kodeks postępowania karnego (Dz. U. Nr 89, poz. 555, " +
                    "z późn. zm.) wprowadza się następujące zmiany:", 1997, new[] { 555 })]
        // Kolejny numer tego samego rocznika to dalsza pozycja na liście.
        [InlineData("Art. 21. W ustawie z dnia 1 stycznia 2010 r. o zmianie (Dz. U. z 2011 r. Nr 217, poz. 1280 i Nr 230, poz. 1371) " +
                    "uchyla się art. 2.", 2011, new[] { 1280, 1371 })]
        public void JournalWithIssueNumber_IsReadByYearAndPosition(string text, int year, int[] positions)
        {
            var article = new Article { ContentText = text };

            _service.ParseJournalReferences(article);

            var journal = Assert.Single(article.Journals);
            Assert.Equal(year, journal.Year);
            Assert.Equal(positions, journal.Positions);
        }

        [Theory]
        // Pierwsza wypisana pozycja to akt zmieniany: dalsze są jego zmianami, także ogłoszonymi wcześniej w tym roczniku
        // (Dz. U. 2026 poz. 638), albo innym aktem przywołanym dalej w przepisie (korpus 335351).
        [InlineData("Art. 1. W ustawie z dnia 6 czerwca 1997 r. – Kodeks postępowania karnego (Dz. U. z 2026 r. poz. 490 i 421) " +
                    "w art. 25 w § 1 pkt 2 otrzymuje brzmienie:", "DU/2026/490")]
        [InlineData("§ 1. W rozporządzeniu Ministra Edukacji i Nauki z dnia 4 grudnia 2023 r. zmieniającym rozporządzenie (Dz. U. poz. 2653) " +
                    "w § 5 wyrazy „określonym w komunikacie wydanym na podstawie art. 74 ustawy z dnia 26 maja 2023 r. o aplikacji mObywatel " +
                    "(Dz. U. poz. 1234).” zastępuje się wyrazami „określonym w komunikacie”.", "DU/2023/2653")]
        public void AmendedAct_IsTheFirstCitedPosition(string text, string eli)
        {
            var article = new Article { ContentText = text, IsSection = text.StartsWith('§') };

            _service.ParseJournalReferences(article);

            Assert.Equal(eli, article.Journals[0].GetELIStrings().First());
        }

        [Fact]
        public void ParseJournalReferences_MergesPositionsForSameYear()
        {
            var article = new Article
            {
                EffectiveDate = new DateTime(2020, 1, 1),
                ContentText = "Art. 2. W ustawie z dnia 1 stycznia 2020 r. (Dz. U. z 2020 r. poz. 10 i 12; Dz. U. z 2020 r. poz. 12, 15)"
            };

            _service.ParseJournalReferences(article);

            Assert.Single(article.Journals);
            var journal = article.Journals[0];
            Assert.Equal(2020, journal.Year);
            Assert.Equal(new[] { 10, 12, 15 }, journal.Positions);
        }

        [Fact]
        public void ParseJournalReferences_CapturesMultipleYears()
        {
            var article = new Article
            {
                EffectiveDate = new DateTime(2019, 1, 1),
                ContentText = "Art. 3. W ustawie z dnia 1 stycznia 2019 r. (Dz. U. z 2020 r. poz. 10, 12; Dz. U. z 2021 r. poz. 3)"
            };

            _service.ParseJournalReferences(article);

            Assert.Equal(2, article.Journals.Count);
            Assert.Contains(article.Journals, j => j.Year == 2020 && j.Positions.SequenceEqual(new[] { 10, 12 }));
            Assert.Contains(article.Journals, j => j.Year == 2021 && j.Positions.SequenceEqual(new[] { 3 }));
        }

        [Fact]
        public void ParseJournalReferences_UsesLongestSourceString()
        {
            var article = new Article
            {
                EffectiveDate = new DateTime(2020, 1, 1),
                ContentText = "Art. 4. W ustawie z dnia 1 stycznia 2020 r. (Dz. U. z 2020 r. poz. 10; Dz. U. z 2020 r. poz. 10 i 12)"
            };

            _service.ParseJournalReferences(article);

            var journal = Assert.Single(article.Journals);
            Assert.Equal("Dz. U. z 2020 r. poz. 10 i 12", journal.SourceString);
        }

        [Fact]
        public void AmendingFormula_MayNameTheStatuteBeforeItsDate()
        {
            // Ustawa budżetowa: między „ustawie" a datą stoi jej określenie.
            var article = new Article
            {
                ContentText = "Art. 1. W ustawie budżetowej na rok 2024 z dnia 18 stycznia 2024 r. (Dz. U. poz. 122) " +
                              "wprowadza się następujące zmiany:"
            };

            _service.ParseJournalReferences(article);

            var journal = Assert.Single(article.Journals);
            Assert.Equal(2024, journal.Year);
            Assert.Equal(new[] { 122 }, journal.Positions);
        }

        [Theory]
        // Rozporządzenie zmieniające: między „rozporządzeniu" a datą stoi organ wydający, także długi.
        [InlineData("§ 1. W rozporządzeniu Rady Ministrów z dnia 3 grudnia 2015 r. w sprawie Pełnomocnika Rządu " +
                    "(Dz. U. z 2021 r. poz. 562 oraz z 2023 r. poz. 2620) wprowadza się następujące zmiany:", 2021, 562)]
        [InlineData("§ 1. W rozporządzeniu Ministrów Infrastruktury oraz Spraw Wewnętrznych i Administracji z dnia " +
                    "31 lipca 2002 r. w sprawie znaków i sygnałów drogowych (Dz. U. z 2019 r. poz. 2310) w § 5 uchyla się ust. 2.", 2019, 2310)]
        // § 158 ust. 7 ZTP: rocznik pominięty — rok z daty rozporządzenia.
        [InlineData("§ 1. W rozporządzeniu Ministra Finansów z dnia 1 lutego 2021 r. w sprawie wzoru zgłoszenia " +
                    "(Dz. U. poz. 200) wprowadza się następujące zmiany:", 2021, 200)]
        public void AmendingFormula_OfARegulation_NamesItsIssuerBeforeTheDate(string text, int year, int position)
        {
            var section = new Article { ContentText = text, IsSection = true };

            _service.ParseJournalReferences(section);

            var journal = Assert.Single(section.Journals);
            Assert.Equal(year, journal.Year);
            Assert.Equal(new[] { position }, journal.Positions);
        }

        [Theory]
        // M.P. 2026 poz. 948: uchwała zmieniająca uchwałę — formuła „W uchwale …" nie otwierała odczytu publikatora, więc
        // komendy wskazywały jako akt zmieniany samą uchwałę zmieniającą. Zarządzenie zmieniające ma tę samą formułę.
        [InlineData("§ 1. W uchwale nr 103 Rady Ministrów z dnia 20 czerwca 2023 r. w sprawie ustanowienia programu wieloletniego " +
                    "(M.P. z 2026 r. poz. 249) wprowadza się następujące zmiany:", "MP/2026/249")]
        [InlineData("§ 1. W zarządzeniu nr 81 Prezesa Rady Ministrów z dnia 24 lipca 2017 r. w sprawie Rady do spraw Cyfryzacji " +
                    "(M.P. z 2018 r. poz. 756) § 2 otrzymuje brzmienie:", "MP/2018/756")]
        public void AmendingFormula_OfAResolutionOrAnOrder_ReadsItsJournal(string text, string eli)
        {
            var section = new Article { ContentText = text, IsSection = true };

            _service.ParseJournalReferences(section);

            Assert.Equal(new[] { eli }, Assert.Single(section.Journals).GetELIStrings());
        }

        [Theory]
        // Monitor Polski: uchwały i zarządzenia; rocznik pominięty to rok z daty aktu (§ 158 ust. 7 ZTP).
        [InlineData("§ 2 uchwały nr 151 Rady Ministrów z dnia 22 sierpnia 2023 r. zmieniającej uchwałę w sprawie programu (M.P. poz. 1029), który stanowi:", "MP/2023/1029")]
        [InlineData("§ 2 zarządzenia nr 81 Prezesa Rady Ministrów z dnia 24 lipca 2017 r. (M. P. z 2018 r. poz. 756), który stanowi:", "MP/2018/756")]
        [InlineData("art. 19 ustawy z dnia 21 listopada 2025 r. o zmianie ustawy (Dz. U. poz. 1792), który stanowi:", "DU/2025/1792")]
        [InlineData("art. 20 ustawy z dnia 6 czerwca 1997 r. – Kodeks karny (Dz. U. Nr 88, poz. 553, z późn. zm.), który stanowi:", "DU/1997/553")]
        public void ReadSingleAct_ReadsThePublisherWithTheJournal(string text, string eli)
        {
            Assert.Equal(new[] { eli }, JournalReferenceService.ReadSingleAct(text)!.GetELIStrings());
        }

        [Fact]
        public void ReadSingleAct_TextNamingTwoActs_HasNoSingleAct()
        {
            // Ten sam rocznik i pozycja w dwóch dziennikach to dwa różne akty.
            Assert.Null(JournalReferenceService.ReadSingleAct(
                "uchwały z dnia 3 marca 2023 r. (M.P. poz. 200) oraz ustawy z dnia 3 marca 2023 r. (Dz. U. poz. 200)"));
            Assert.Null(JournalReferenceService.ReadSingleAct("przepisu, który stanowi:"));
        }

        [Fact]
        public void SectionOnlyCitingARegulation_IsNotAmending()
        {
            var section = new Article
            {
                IsSection = true,
                ContentText = "§ 4. W rozporządzeniu, o którym mowa w § 3 (Dz. U. z 2022 r. poz. 902), określa się wzór wniosku.",
            };

            _service.ParseJournalReferences(section);

            Assert.Empty(section.Journals);
        }

        [Theory]
        // Przepis, który ustawę tylko przywołuje, nie jest przepisem zmieniającym.
        [InlineData("Art. 5. W ustawie, o której mowa w art. 1 (Dz. U. z 2022 r. poz. 902), nie stosuje się art. 4.")]
        [InlineData("Art. 6. W roku 2026 rezerwa, o której mowa w art. 34 ust. 1 ustawy z dnia 1 października 2024 r. (Dz. U. poz. 1572), wynosi 0.")]
        public void ArticleOnlyCitingAStatute_IsNotAmending(string text)
        {
            var article = new Article { ContentText = text };

            _service.ParseJournalReferences(article);

            Assert.Empty(article.Journals);
        }

        [Fact]
        public void ParseJournalReferences_IgnoresNonAmendingArticles()
        {
            var article = new Article
            {
                EffectiveDate = new DateTime(2024, 1, 1),
                ContentText = "Art. 72. W przypadku wnioskow (Dz. U. z 2022 r. poz. 902)"
            };

            _service.ParseJournalReferences(article);

            Assert.Empty(article.Journals);
        }
    }
}
