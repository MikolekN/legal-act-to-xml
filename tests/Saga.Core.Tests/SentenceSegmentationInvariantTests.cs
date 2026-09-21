using Saga.Core.Services.Parsing;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Sprawdza niezmienniki z <see cref="SentenceSegmentationInvariants"/> na kształtach tekstu,
    /// które w korpusie wywołują najwięcej kłopotów. Przypadki dobrano z rzeczywistych dokumentów
    /// (ustawy, obwieszczenia, rozporządzenia), żeby zestaw działał także na czystym klonie,
    /// bez dostępu do niewersjonowanego katalogu documents/.
    ///
    /// Test NIE sprawdza, ile segmentów powstało ani gdzie przebiegła granica — to zadanie testów
    /// poszczególnych reguł. Tu chodzi wyłącznie o to, żeby żadna przyszła reguła nie uszkodziła
    /// treści przy okazji przesuwania granic.
    /// </summary>
    public class SentenceSegmentationInvariantTests
    {
        public static TheoryData<string, string> PathologicalShapes() => new()
        {
            { "pusty tekst", "" },
            { "same białe znaki", "   \t  " },
            { "jedno zdanie", "Przepis stosuje się odpowiednio." },
            { "dwa zdania", "Ustawa określa zasady i tryb postępowania. Przepisy stosuje się odpowiednio." },
            { "bez kropki na końcu", "Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia" },
            { "łańcuch oznaczeń w treści cytowanej", "„Art. 59a. 1. W roku 2024 niewykorzystane środki przechodzą na rok następny." },
            { "oznaczenie liczbą rzymską", "3.2a. Rejestracja operatorów systemów bezzałogowych" },
            { "formuła podpisu z inicjałem", "Prezydent Rzeczypospolitej Polskiej: A. Duda" },
            { "nagłówek wersalikami", "WYKAZ OBIEKTÓW, O KTÓRYCH MOWA W ART. 1A UST. 1 PKT 2 LIT. A USTAWY" },
            { "cytat zamknięty kropką i cudzysłowem", "Klauzula ma brzmienie: „Jestem świadomy odpowiedzialności karnej.”. Klauzula zastępuje pouczenie organu." },
            { "publikator i rocznik", "Zmiany ogłoszono w Dz. U. z 2024 r. poz. 1. Przepis stosuje się od stycznia." },
            { "odstęp nierozdzielający po skrócie", "Przepis art. 5 Kodeksu stosuje się wprost." },
            { "odstęp nierozdzielający po oznaczeniu", "„3a. W przypadku odbudowy stosuje się przepisy dotychczasowe." },
            { "skrót nazwy własnej", "Instytut Medycyny Wsi im. Witolda Chodźki prowadzi badania naukowe." },
            { "wielokrotny odstęp po kropce", "Zdanie pierwsze.  Zdanie drugie." },
            { "polskie litery na początku zdania", "Pierwsza część. Ąćęłńóśźż druga część." },
            { "kropka w liczbie", "Kwota wynosi 1.500.000 zł i podlega waloryzacji corocznie." },
            { "wyliczenie w jednym bloku", "Do wniosku dołącza się: 1) oświadczenie; 2) kopię decyzji; 3) potwierdzenie zapłaty." },
        };

        [Theory]
        [MemberData(nameof(PathologicalShapes))]
        public void SplitIntoSentences_ZachowujeNiezmienniki(string opis, string tekst)
        {
            // Nazwa przypadku jest tylko etykietą w raporcie — czytelność przy czerwonym teście.
            Assert.NotNull(opis);

            var segments = ParsingFactories.SplitIntoSentences(tekst);

            SentenceSegmentationInvariants.AssertHolds(tekst, segments);
        }

        [Fact]
        public void SplitIntoSentences_TekstZlozonyZSamychBialychZnakow_ZwracaPustaListe()
        {
            var segments = ParsingFactories.SplitIntoSentences("   ");

            Assert.Empty(segments);
        }
    }
}
