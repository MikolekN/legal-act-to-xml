using Saga.Core.Services.Parsing;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Odrzucanie „RRRR r." jest warunkowe.
    /// Sama obecność daty nie rozstrzyga o granicy; rozstrzyga jej pozycja w zdaniu.
    /// </summary>
    public class YearBoundaryTests
    {
        [Theory]
        [InlineData("W 2024 r. Bank Gospodarstwa Krajowego dokonuje dodatkowej wyceny aktywów.")]
        [InlineData("Od 2027 r. Centrum Egzaminów Medycznych będzie otrzymywać dotację celową.")]
        [InlineData("Do dnia 1 marca 2027 r. Krajowa Rada Biegłych Rewidentów podejmuje uchwałę.")]
        [InlineData("W terminie do dnia 30 maja 2025 r. Prezes Urzędu Regulacji Energetyki kalkuluje taryfę.")]
        [InlineData("Od dnia 1 lipca 2005 r. Ochotnicze Hufce Pracy stają się jednostką budżetową.")]
        [InlineData("W 2020 r. i 2021 r. Bank Gospodarstwa Krajowego prowadził rachunek pomocniczy.")]
        [InlineData("Od dnia 1 marca 2023 r. do dnia 31 grudnia 2023 r. Prezes Funduszu wydaje decyzje.")]
        public void SplitIntoSentences_DataOtwieraZdanie_NieJestGranica(string text)
        {
            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Single(segments);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }

        [Theory]
        [InlineData("Wnioski składa się w terminie do dnia 31 grudnia 2024 r. Nie pobiera się odsetek za opóźnienie.")]
        [InlineData("Dotyczy to projektów służących usuwaniu skutków powodzi z września 2024 r. W takim przypadku przepisu art. 44 nie stosuje się.")]
        [InlineData("Zaległości reguluje się w terminie do dnia 31 maja 2024 r. Zarządca rozliczeń prowadzi ewidencję wpłat.")]
        [InlineData("Uprawnienie przysługuje za okres do dnia 30 czerwca 2013 r. Drugim okresem sprawozdawczym jest rok następny.")]
        public void SplitIntoSentences_DataZamykaTresc_JestGranica(string text)
        {
            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Equal(2, segments.Count);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }

        [Theory]
        [InlineData("Przepisy ustawy z dnia 18 października 2006 r. O ujawnianiu informacji o dokumentach stosuje się odpowiednio.")]
        [InlineData("OŚWIADCZENIE W ROZUMIENIU USTAWY Z DNIA 18 PAŹDZIERNIKA 2006 R. O UJAWNIANIU INFORMACJI O DOKUMENTACH")]
        [InlineData("Pomoc zatwierdzono decyzją Komisji Europejskiej z dnia 13 lipca 2012 r. C(2012) 4609 final.")]
        public void SplitIntoSentences_DataWOznaczeniuAktu_NieJestGranica(string text)
        {
            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Single(segments);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }
    }
}
