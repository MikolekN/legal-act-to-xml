using System.Text;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Strażnik SPRZĄTANIA, nie funkcji. Opisuje stan przejściowy: parser nie odczytuje jeszcze
    /// daty ogłoszenia z nagłówka aktu ogłoszonego, więc testy wejścia w życie podstawiają ją
    /// rusztowaniem (<see cref="AnnouncementDateScaffold"/>).
    ///
    /// Ten test ma spaść w dniu, w którym ekstrakcja zostanie zaimplementowana — i wtedy trzeba
    /// go USUNĄĆ razem z rusztowaniem, a testy wejścia w życie przepisać na dokumenty niosące
    /// prawdziwy nagłówek Dz.U. Czerwony tutaj nie znaczy „regres", tylko „rusztowanie stało się
    /// zbędne" (docs/backlog.md § 3b).
    /// </summary>
    public class AnnouncementDateExtractionTests
    {
        [Fact]
        public void AnnouncementDate_IsNotYetExtracted_ScaffoldStillNeeded()
        {
            // Nagłówek dokładnie w formie, w jakiej niesie datę ogłoszenia akt w Dz.U.
            string[] lines =
            {
                "DZIENNIK USTAW",
                "RZECZYPOSPOLITEJ POLSKIEJ",
                "Warszawa, dnia 12 stycznia 2026 r.",
                "Poz. 26",
                "USTAWA",
                "z dnia 5 marca 2025 r.",
                "o zmianie ustawy o rzeczach różnych",
                "Art. 1. Wprowadza się zmiany porządkowe.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
            };

            using var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(string.Join("\n", lines)));
            var document = LegalDocumentParser.Parse(stream, "ustawa.txt",
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document;

            Assert.NotNull(document);

            // Gdy ten warunek przestanie być prawdziwy, ekstrakcja istnieje — usuń ten plik
            // i AnnouncementDateScaffold, a wywołania AsAnnouncedOn zastąp dokumentami z nagłówkiem.
            Assert.Null(document!.AnnouncementDate);

            // Konsekwencja stanu przejściowego, warta pokazania wprost: bez daty ogłoszenia formuła
            // jest rozpoznana, ale nierozstrzygnięta, więc żadna jednostka nie dostaje daty.
            Assert.NotNull(document.EntryIntoForce);
            Assert.Null(document.EntryIntoForce!.ResolvedDate);
            Assert.NotEmpty(document.Articles);
            Assert.All(document.Articles, a => Assert.Equal(default, a.EffectiveDate));
        }
    }
}
