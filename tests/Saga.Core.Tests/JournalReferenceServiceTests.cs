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
