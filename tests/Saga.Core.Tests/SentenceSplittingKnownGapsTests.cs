using Saga.Core.Services.Parsing;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Znane braki reguły podziału na zdania — po jednym teście na grupę poprawek
    /// z docs/internal/wiedza-segmentacja-zdan.md §13.
    ///
    /// Każdy test asercjami opisuje ZAMIAR (jak ma być), a nie stan faktyczny, i jest pominięty
    /// z powodem wskazującym grupę. Dzięki temu:
    /// - backlog jest widoczny wprost w raporcie z testów, a nie tylko w dokumencie,
    /// - domknięcie grupy polega na zdjęciu atrybutu Skip, bez dotykania asercji,
    /// - naprawa reguły nie wygląda w CI jak regresja.
    ///
    /// Poprzednio część tej wiedzy siedziała w teście, który asercją utrwalał BŁĄD
    /// ("Rzeczywistość: jest 2 segmenty") — taki test zaświeciłby się na czerwono dokładnie
    /// wtedy, gdy reguła zostanie naprawiona.
    ///
    /// Liczby przy grupach to zmierzona liczba błędnych granic na pełnym korpusie
    /// (2 320 dokumentów, 36 506 granic — z czego 5 962 błędnych).
    /// </summary>
    public class SentenceSplittingKnownGapsTests
    {
        [Fact(Skip = "Grupa A (samodzielne oznaczenie jednostki, 2 884 błędnych granic) — patrz §13 dokumentu wiedzy.")]
        public void SplitIntoSentences_SamodzielneOznaczenieJednostki_NieJestGranica()
        {
            // „Art. 59a. 1." to oznaczenie, nie zdanie. Dziś powstają z tego trzy segmenty,
            // z których dwa pierwsze są bezwartościowe i przesuwają Order reszty jednostki.
            var text = "„Art. 59a. 1. W roku 2024 niewykorzystane środki przechodzą na rok następny.";

            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Single(segments);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }

        [Fact(Skip = "Grupa A (samodzielne oznaczenie jednostki) — wariant z odesłaniem, musi POZOSTAĆ podzielony.")]
        public void SplitIntoSentences_OznaczenieNaKoncuZdania_PozostajeGranica()
        {
            // Kontrprzykład chroniący przed zbyt szerokim wetem: tu "art. 4a." kończy zdanie
            // treści, więc podział jest POPRAWNY i grupa A nie może go skasować.
            var text = "Wnioskodawca składa korektę wniosku, o którym mowa w art. 4a. Minister właściwy do spraw energii przekazuje dane.";

            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Equal(2, segments.Count);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }

        [Fact(Skip = "Grupa B (inicjały i tytuły osobowe, 2 279 błędnych granic) — patrz §13 dokumentu wiedzy.")]
        public void SplitIntoSentences_InicjalImienia_NieJestGranica()
        {
            // Formuła podpisu zamyka praktycznie każdy akt — dziś tniemy w środku imienia i nazwiska.
            var text = "Prezydent Rzeczypospolitej Polskiej: A. Duda";

            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Single(segments);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }

        [Fact(Skip = "Grupa B (inicjały i tytuły osobowe) — tytuł przed nazwiskiem.")]
        public void SplitIntoSentences_TytulPrzedNazwiskiem_NieJestGranica()
        {
            var text = "Dr. Jan Kowalski jest lekarzem.";

            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Single(segments);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }

        [Fact(Skip = "Grupa C (skróty rzeczowe i adresowe, 799 błędnych granic) — patrz §13 dokumentu wiedzy.")]
        public void SplitIntoSentences_SkrotNazwyWlasnej_NieJestGranica()
        {
            // Czołówkę skrótów w korpusie tworzą nazwy własne i adresy ("m.", "im.", "ul.", "ds."),
            // a nie skróty prawnicze.
            var text = "Instytut Medycyny Wsi im. Witolda Chodźki prowadzi badania naukowe.";

            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Single(segments);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }

        [Fact(Skip = "Grupa D (wersaliki, 98 błędnych granic) — patrz §13 dokumentu wiedzy.")]
        public void SplitIntoSentences_NaglowekWersalikami_NieJestGranica()
        {
            // W tekście pisanym kapitalikami warunek "po kropce stoi wielka litera" jest spełniony
            // zawsze, więc nie niesie żadnej informacji.
            var text = "WYKAZ OBIEKTÓW, O KTÓRYCH MOWA W ART. 1A UST. 1 PKT 2 LIT. A USTAWY";

            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Single(segments);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }

        [Fact(Skip = "Grupa D (interpunkcja granicy, 5 przeoczonych granic) — patrz §13 dokumentu wiedzy.")]
        public void SplitIntoSentences_CudzyslowZamykajacy_JestGranica()
        {
            // Granica przeoczona: po kropce stoi cudzysłów zamykający, więc kształt "kropka + spacja"
            // nie występuje, choć zdanie się kończy.
            var text = "Ślubuję sumiennie wykonywać powierzone mi obowiązki.” Ślubowanie może być złożone z dodaniem zdania.";

            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Equal(2, segments.Count);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }

        [Fact(Skip = "Grupa E (weto warunkowe, 65 przeoczonych granic) — wymaga wcześniejszego domknięcia grup A i C.")]
        public void SplitIntoSentences_PublikatorNaKoncuZdania_JestGranica()
        {
            // Weto na publikator jest dziś bezwarunkowe i patrzy wyłącznie w lewo, więc gubi
            // prawdziwą granicę, gdy zdanie kończy się oznaczeniem dziennika.
            var text = "Zmiany ogłoszono w Dz. U. Przepis stosuje się od stycznia.";

            var segments = ParsingFactories.SplitIntoSentences(text);

            Assert.Equal(2, segments.Count);
            SentenceSegmentationInvariants.AssertHolds(text, segments);
        }
    }
}
