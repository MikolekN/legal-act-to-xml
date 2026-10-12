using Saga.Core.Services.Parsing;

namespace Saga.Core.Tests
{
    public class SentenceBoundaryShapeTests
    {
        [Theory]
        // Rok zapisany „R." to ten sam skrót co „r.".
        [InlineData("USTAWY Z DNIA 18 PAŹDZIERNIKA 2006 R. O UJAWNIANIU INFORMACJI O DOKUMENTACH")]
        [InlineData("Ustawa z dnia 18 października 2006 r. O ujawnianiu informacji o dokumentach")]
        public void SplitIntoSentences_RokNiezaleznieOdWielkosciLiter_NieJestGranica(string text)
        {
            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Single(segments);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }

        [Theory]
        [InlineData('”')]
        [InlineData('"')]
        public void SplitIntoSentences_CytatDomknietyCudzyslowem_TniePoCudzyslowie(char closing)
        {
            var text = $"Ślubuję rzetelnie i sumiennie wykonywać powierzone mi obowiązki.{closing} "
                + "Ślubowanie może być złożone z dodaniem zdania końcowego.";

            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Equal(2, segments.Count);
            Assert.EndsWith($".{closing}", segments[0].Text, StringComparison.Ordinal);
            Assert.StartsWith("Ślubowanie", segments[1].Text, StringComparison.Ordinal);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }

        [Theory]
        // Cudzysłów domykający treść nowelizacji, po którym idzie średnik albo przecinek,
        // nie jest granicą — brzmienie jest częścią zdania wprowadzającego.
        [InlineData("W art. 5 dodaje się ust. 3 w brzmieniu: „Przepis stosuje się odpowiednio.”; pozostałe zmiany bez zmian.")]
        [InlineData("W art. 5 dodaje się ust. 3 w brzmieniu: „Przepis stosuje się odpowiednio.”, a w art. 6 uchyla się ust. 2.")]
        public void SplitIntoSentences_CudzyslowPrzedSrednikiem_NieJestGranica(string text)
        {
            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Single(segments);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }

        [Theory]
        // Korpus: 275050_2461922 — odnośnik o zmianie zdania stoi zaraz za jego kropką.
        [InlineData("Termin na wyrażenie zgody wynosi 60 dni od dnia otrzymania wniosku.[7)] Brak wyrażenia zgody uznaje się za odmowę.", "wniosku.[7)]")]
        [InlineData("Pisma doręcza się za pokwitowaniem odbioru.[26)][27)] Odbierający potwierdza odbiór.", "odbioru.[26)][27)]")]
        [InlineData("Pisma doręcza się za pokwitowaniem odbioru.[1), 2)] Odbierający potwierdza odbiór.", "odbioru.[1), 2)]")]
        [InlineData("Ślubuję sumiennie wykonywać obowiązki.”[3)] Ślubowanie składa się przed radą.", "obowiązki.”[3)]")]
        public void SplitIntoSentences_ZnacznikOdnosnikaZaKropka_NalezyDoZdaniaKtoreZamyka(string text, string endOfFirst)
        {
            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Equal(2, segments.Count);
            Assert.EndsWith(endOfFirst, segments[0].Text, StringComparison.Ordinal);
            Assert.DoesNotContain("[", segments[1].Text);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }

        [Theory]
        // Znacznik odsunięty odstępem otwiera następne zdanie — tak stoi przy oznaczeniu jednostki.
        [InlineData("Pisma doręcza się za pokwitowaniem odbioru. [7)] Odbierający potwierdza odbiór.")]
        // Indeks górny bez nawiasu zamykającego nie jest znacznikiem odnośnika.
        [InlineData("Powierzchnia wynosi 5 m.[2] Do obliczeń przyjmuje się wartość średnią.")]
        public void SplitIntoSentences_ZnacznikNiePrzyklejonyAlboNieOdnosnik_NieTworzyGranicy(string text)
        {
            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Single(segments);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }

        [Theory]
        // Weta lewego kontekstu (rok, skrót, publikator) rozstrzygają tak samo ze znacznikiem i bez niego.
        [InlineData("Przepis stosuje się od dnia 1 lipca 2024 r.[7)] Do postępowań wszczętych wcześniej stosuje się przepisy dotychczasowe.")]
        [InlineData("Wniosek składa się zgodnie z art.[7)] Do wniosku dołącza się oświadczenie.")]
        [InlineData("Zmiany ogłoszono w Dz. U.[7)] Do ustawy dodano załącznik.")]
        public void SplitIntoSentences_ZnacznikOdnosnikaZaKropka_NieZnosiWetLewegoKontekstu(string text)
        {
            var withoutMarker = Saga.Core.Services.FootnoteMarkers.Reference.Replace(text, string.Empty);

            Assert.Equal(
                ParsingFactories.SplitIntoSentences(withoutMarker).Count,
                ParsingFactories.SplitIntoSentences(text).Count);
            SentenceSegmentationInvariants.AssertHolds(text, ParsingFactories.SplitIntoSentences(text));
        }

        [Theory]
        [InlineData("Czy posiada Pani (Pan) zaległości podatkowe? Proszę zakreślić właściwe pole.")]
        [InlineData("Uwaga! Wniosek niekompletny pozostawia się bez rozpoznania.")]
        [InlineData("Czy dotyczy to art. 5 ust. 2? Przepis stosuje się odpowiednio.")]
        public void SplitIntoSentences_ZnakZapytaniaIWykrzyknik_KonczaZdanie(string text)
        {
            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Equal(2, segments.Count);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }
    }
}
