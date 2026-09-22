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
    }
}
