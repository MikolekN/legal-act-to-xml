using Saga.Core.Services.Parsing;

namespace Saga.Core.Tests
{
    /// <summary>
    ///
    /// Każdy test asercjami opisuje ZAMIAR (jak ma być), a nie stan faktyczny. Luka, która
    /// jeszcze nie jest domknięta, dostaje [Fact(Skip = "…")] z powodem samowystarczalnym
    /// (bez odsyłania do niewersjonowanych dokumentów) — domknięcie polega na zdjęciu Skip,
    /// bez dotykania asercji. Test utrwalający BŁĄD asercją zaświeciłby się na czerwono
    /// dokładnie wtedy, gdy regułę naprawiono.
    ///
    /// Kontrprzykłady są aktywne zawsze: pominięty nie strzegłby niczego właśnie wtedy,
    /// gdy powstaje weto.
    /// </summary>
    public class SentenceSplittingKnownGapsTests
    {
        [Fact]
        public void SplitIntoSentences_SamodzielneOznaczenieJednostki_NieJestGranica()
        {
            // „Art. 59a. 1." w treści cytowanej nowelizacji to oznaczenie, nie zdanie — należy
            // do zdania, które po nim następuje.
            var text = "„Art. 59a. 1. W roku 2024 niewykorzystane środki przechodzą na rok następny.";

            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Single(segments);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }

        [Fact]
        public void SplitIntoSentences_OznaczenieNaKoncuZdania_PozostajeGranica()
        {
            // Kontrprzykład chroniący przed zbyt szerokim wetem grupy A: tu "art. 4a." kończy zdanie
            // treści, więc podział jest POPRAWNY i żadne weto nie może go skasować.
            var text = "Wnioskodawca składa korektę wniosku, o którym mowa w art. 4a. Minister właściwy do spraw energii przekazuje dane.";

            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Equal(2, segments.Count);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }

        [Fact]
        public void SplitIntoSentences_OznaczenieBezCudzyslowu_PozostajeGranica()
        {
            // Weto grupy A obejmuje wyłącznie treść cytowaną. Samotny numer na początku treści
            // jednostki BEZ cudzysłowu to w korpusie zawsze defekt wyżej w potoku (niezdjęty
            // prefiks ustępu, nierozpoznany numer punktu „5.6.") — granica ma go uwidaczniać
            // w pomiarze, a nie maskować.
            var text = "5.6. Przekroczenie tygodniowego wymiaru czasu pracy podlega karze.";

            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Equal(2, segments.Count);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }

        [Fact]
        public void SplitIntoSentences_InicjalImienia_NieJestGranica()
        {
            // Formuła podpisu: inicjał po dwukropku, przed nazwiskiem.
            var text = "Prezydent Rzeczypospolitej Polskiej: A. Duda";

            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Single(segments);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }

        [Theory]
        [InlineData("Szpital Kliniczny im. J. Babińskiego prowadzi rejestr pacjentów.")]
        [InlineData("Opinię sporządził prof. A. Nowak z zespołem biegłych.")]
        [InlineData("Marszałek Sejmu: J. K. Nowak")]
        public void SplitIntoSentences_InicjalPoTytuleLubInicjale_NieJestGranica(string text)
        {
            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Single(segments);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }

        [Theory]
        [InlineData("Stawki określa tabela A. Opłatę pobiera się z góry.")]
        [InlineData("Rejestr prowadzi Krajowy Depozyt Papierów Wartościowych S.A. Przepis ust. 2 stosuje się odpowiednio.")]
        public void SplitIntoSentences_WielkaLiteraBezKontekstuOsoby_PozostajeGranica(string text)
        {
            // Kontrprzykład dla grupy B: sam kształt „wielka litera + kropka + wyraz kapitalizowany"
            // nie wystarcza — „A." zamyka zdanie o tabeli, „S.A." nazwę spółki (z korpusu), żadne
            // nie jest inicjałem.
            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Equal(2, segments.Count);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }

        [Fact]
        public void SplitIntoSentences_TytulPrzedNazwiskiem_NieJestGranica()
        {
            var text = "Dr. Jan Kowalski jest lekarzem.";

            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Single(segments);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }

        [Fact]
        public void SplitIntoSentences_NaglowekWersalikami_NieJestGranica()
        {
            // W tekście pisanym kapitalikami warunek "po kropce stoi wielka litera" jest spełniony
            // zawsze, więc nie niesie żadnej informacji.
            var text = "WYKAZ OBIEKTÓW, O KTÓRYCH MOWA W ART. 1A UST. 1 PKT 2 LIT. A USTAWY";

            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Single(segments);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }

        [Fact]
        public void SplitIntoSentences_CudzyslowZamykajacy_JestGranica()
        {
            // Granica przeoczona: po kropce stoi cudzysłów zamykający, więc kształt "kropka + spacja"
            // nie występuje, choć zdanie się kończy.
            var text = "Ślubuję sumiennie wykonywać powierzone mi obowiązki.” Ślubowanie może być złożone z dodaniem zdania.";

            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Equal(2, segments.Count);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }

        [Fact]
        public void SplitIntoSentences_PublikatorNaKoncuZdania_JestGranica()
        {
            // Kropka po „Dz. U." kończy jednocześnie skrót i zdanie, gdy dalej stoi zwykły wyraz.
            var text = "Zmiany ogłoszono w Dz. U. Przepis stosuje się od stycznia.";

            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Equal(2, segments.Count);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }

        [Theory]
        [InlineData("Ustawa została ogłoszona w Dz. U. Nr 12, poz. 136 i obowiązuje od stycznia.")]
        [InlineData("Dyrektywę ogłoszono w Dz. U. UE L 119 i wdrożono ją w terminie.")]
        public void SplitIntoSentences_PublikatorZKontynuacja_NieJestGranica(string text)
        {
            // Kontrprzykład: po „Dz. U." stoi dalszy ciąg oznaczenia („Nr", „UE L"),
            // a nie nowe zdanie.
            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Single(segments);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }
    }
}
