using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Zmiana wyrazów w różnych formach („w różnej liczbie i różnym przypadku … użytymi w odpowiedniej liczbie
    /// i odpowiednim przypadku wyrazami") to instrukcja ręczna na każdy cel (ADR-0103): formy stoją tylko w akcie
    /// zmienianym, a instrukcja z formą podstawową trafiłaby co najwyżej w mianownik.
    /// </summary>
    public class InflectedWordChangeTests
    {
        private static XElement Export(params string[] provisions)
        {
            string[] lines =
            [
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o zmianie ustawy o opłatach",
                "Art. 1. W ustawie z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) wprowadza się następujące zmiany:",
                .. provisions,
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
            ];
            return LegalDocumentConverter.Convert(LegalDocumentParser.Parse(
                lines.Select(line => new DocumentBlock { Text = line }).ToList(),
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!);
        }

        [Theory]
        // Przykład autorów schematu (DU/2026/252 art. 1 pkt 42) i korpus (275887_3019095).
        [InlineData("1) użyte w art. 40 i w art. 64, w różnej liczbie i różnym przypadku, wyrazy „sektorowy zespół cyberbezpieczeństwa” zastępuje się użytymi w odpowiedniej liczbie i odpowiednim przypadku wyrazami „CSIRT sektorowy”.")]
        [InlineData("1) użyte w art. 40 oraz w art. 64, w różnym przypadku, wyrazy „sektorowy zespół cyberbezpieczeństwa” zastępuje się użytymi w odpowiednim przypadku wyrazami „CSIRT sektorowy”.")]
        public void InflectedWords_AreAManualInstructionPerTarget_WithTheNewWords(string command)
        {
            var xml = Export(command);

            Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName == "zamien-tekst");
            var manual = xml.Descendants().Where(e => e.Attribute("funkcja")?.Value == "zastap-recznie").ToList();
            Assert.Equal(["DU/2021/200__art_40", "DU/2021/200__art_64"], manual.Select(e => e.Attribute("element-eId")?.Value));
            Assert.All(manual, e => Assert.Equal("CSIRT sektorowy", e.Value));
        }

        [Fact]
        public void InflectedWords_InARangeOfUnits_KeepTheRangeEnd()
        {
            // M.P. 2026 poz. 1014: „w art. 75a w ust. 1–3, w różnym przypadku, wyrazy …" — cel ma własny adres, więc
            // przedział zostaje jak w instrukcji automatycznej (ADR-0016); kasuje go tylko adres zastępczy (ADR-0100).
            var xml = Export("1) użyte w art. 40 w ust. 1–3 oraz w art. 64, w różnym przypadku, wyrazy „zespół” zastępuje się użytymi w odpowiednim przypadku wyrazami „CSIRT”.");

            var manual = xml.Descendants().Where(e => e.Attribute("funkcja")?.Value == "zastap-recznie").ToList();
            Assert.Equal([("DU/2021/200__art_40__ust_1", "DU/2021/200__art_40__ust_3"), ("DU/2021/200__art_64", null)],
                manual.Select(e => (e.Attribute("element-eId")?.Value, e.Attribute("element-do-eId")?.Value)));
        }

        [Fact]
        public void WordsInOneForm_StayAnAutomaticChange()
        {
            var xml = Export("1) w art. 40 wyrazy „zespół” zastępuje się wyrazami „CSIRT”.");

            Assert.Equal("DU/2021/200__art_40", xml.Descendants().Single(e => e.Name.LocalName == "zamien-tekst").Attribute("element-eId")?.Value);
        }

        [SchemaPlFact]
        public void ManualInstructionsForInflectedWords_AreSchemaValid()
        {
            var xml = Export("1) użyte w art. 40 i w art. 64, w różnym przypadku, wyrazy „zespół” zastępuje się użytymi w odpowiednim przypadku wyrazami „CSIRT”.");

            Assert.Empty(SchemaPl.Validator.ValidateActBody(xml.Elements().First()));
        }
    }
}
