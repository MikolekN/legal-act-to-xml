using Saga.Core.Services.Parsing;

namespace Saga.Core.Tests
{
    /// <summary>Kropka należąca do skrótu nie kończy zdania.</summary>
    public class AbbreviationBoundaryTests
    {
        [Theory]
        [InlineData("ds.", "Rozporządzenie o utworzeniu Agencji Unii Europejskiej ds. Bezpieczeństwa Lotniczego weszło w życie.")]
        [InlineData("im.", "Absolwenci Krajowej Szkoły Administracji Publicznej im. Prezydenta Rzeczypospolitej Polskiej Lecha Kaczyńskiego.")]
        [InlineData("m.st.", "Opinię wydaje prezydent m.st. Warszawy w terminie 30 dni.")]
        [InlineData("art.", "Z wyjątkiem przypadków określonych w art. V ust. 1 Konwencji stosuje się przepisy dotychczasowe.")]
        [InlineData("poz.", "W części Rachunek zysków i strat poz. A i B otrzymują brzmienie:")]
        [InlineData("lit.", "Wytyczne, o których mowa w lit. A części 2 załącznika III, stosuje się odpowiednio.")]
        [InlineData("prof.", "Szpital Kliniczny im. prof. Witolda Orłowskiego prowadzi działalność leczniczą.")]
        [InlineData("ul.", "Siedziba mieści się w Warszawie przy ul. Czerniakowskiej 231.")]
        [InlineData("ust.", "Wymóg z ust. Drugiego stosuje się do wniosków złożonych po dniu wejścia w życie.")]
        [InlineData("pkt.", "Warunek z pkt. Pierwszego uznaje się za spełniony z mocy prawa.")]
        [InlineData("rozdz.", "Przepisy rozdz. Trzeciego stosuje się od dnia ogłoszenia.")]
        [InlineData("oddz.", "Zadania oddz. Wojewódzkiego określa statut jednostki.")]
        [InlineData("zd.", "Zgodnie z art. 5 zd. Drugie organ wydaje decyzję niezwłocznie.")]
        [InlineData("str.", "Wykaz zamieszczono na str. XIV załącznika do rozporządzenia.")]
        [InlineData("dr.", "Szpital im. dr. Józefa Babińskiego prowadzi działalność leczniczą.")]
        [InlineData("ks.", "Kapelanem jednostki jest ks. Piotr Nowak wyznaczony przez kurię.")]
        [InlineData("gen.", "Dowódcą jest gen. Anna Kowalska powołana przez Prezydenta.")]
        [InlineData("al.", "Siedziba mieści się przy al. Jerozolimskich 87 w Warszawie.")]
        [InlineData("pl.", "Zgromadzenie odbywa się na pl. Zamkowym w godzinach popołudniowych.")]
        [InlineData("cz.", "Wysokość wynagrodzenia wskazano w cz. I pkt 18 załącznika.")]
        [InlineData("dz.", "Ustawę ogłoszono w Dz. Urzędowym Ministra Zdrowia.")]
        [InlineData("np.", "Kategorię zdrowia określono w innej służbie, np. W wojsku albo w Policji.")]
        public void SplitIntoSentences_Skrot_NieJestGranica(string skrot, string text)
        {
            Assert.NotNull(skrot);

            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Single(segments);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }

        [Theory]
        [InlineData("rejestr / str.", "Organ prowadzi rejestr. Wpisu dokonuje się na wniosek strony.")]
        [InlineData("kadr / dr.", "Minister zatwierdza plan kadr. Zmiany wymagają zgody ministra.")]
        [InlineData("materiał / al.", "Wykonawca dostarcza materiał. Odbiór następuje protokolarnie.")]
        public void SplitIntoSentences_WyrazZakonczonyJakSkrot_PozostajeGranica(string opis, string text)
        {
            Assert.NotNull(opis);

            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Equal(2, segments.Count);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }

        [Theory]
        [InlineData("Poz. A i B otrzymują brzmienie określone w załączniku.")]
        [InlineData("Ul. Czerniakowska zostaje wyłączona z ruchu.")]
        public void SplitIntoSentences_SkrotKapitalizowany_NieJestGranica(string text)
        {
            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Single(segments);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }
    }
}
