using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Sankcja karna i adres internetowy w treści (ADR-0119): rozpoznawane z brzmienia tekstu, oznaczane w eksporcie —
    /// rola części wspólnej albo element <c>sankcja-karna</c> jak w przykładzie autorów schematu (Dz. U. 2024 poz. 928).
    /// </summary>
    public class PenalSanctionAndLinkTests
    {
        private static XElement Export(params string[] lines)
        {
            var blocks = new List<DocumentBlock>
            {
                new() { Text = "DZIENNIK USTAW RZECZYPOSPOLITEJ POLSKIEJ", Role = BlockRole.JournalHeader },
                new() { Text = "Warszawa, dnia 24 czerwca 2024 r.", Role = BlockRole.JournalHeader },
                new() { Text = "Poz. 928", Role = BlockRole.JournalHeader },
            };
            blocks.AddRange(new[] { "USTAWA", "z dnia 14 czerwca 2024 r.", "o ochronie sygnalistów" }
                .Concat(lines)
                .Append("Art. 9. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.")
                .Select(line => new DocumentBlock { Text = line }));
            return LegalDocumentConverter.Convert(LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!);
        }

        private static List<XElement> Named(XElement xml, string name) => xml.Descendants().Where(e => e.Name.LocalName == name).ToList();

        [Fact]
        public void SanctionClosingTheSentence_StandsInSankcjaKarna_WithoutThePeriod()
        {
            var xml = Export("Art. 1. Kto ujawnia tożsamość sygnalisty, podlega grzywnie, karze ograniczenia wolności albo pozbawienia wolności do roku.");

            var sanction = Assert.Single(Named(xml, "sankcja-karna"));
            Assert.Equal("podlega grzywnie, karze ograniczenia wolności albo pozbawienia wolności do roku", sanction.Value);
            Assert.Equal(".", ((XText)sanction.NextNode!).Value);
        }

        [Fact]
        public void SanctionAfterAnEnumeration_IsTheRoleOfTheCommonPart()
        {
            // Sankcja w osobnym akapicie po punktach jest ich częścią wspólną: sankcja i domknięcie wyliczenia to dwie cechy
            // jednego akapitu — rola, nie osobny element.
            var xml = Export(
                "Art. 1. Kto:",
                "1) ujawnia tożsamość sygnalisty,",
                "2) utrudnia dokonanie zgłoszenia,",
                "– podlega karze grzywny.");

            var commonPart = Assert.Single(xml.Descendants(), e => e.Name.LocalName.StartsWith("czesc-wspolna") && e.Attribute("rola") is not null);
            Assert.Equal("sankcja-karna", commonPart.Attribute("rola")!.Value);
            Assert.Empty(Named(xml, "sankcja-karna"));
        }

        [Theory]
        [InlineData("Art. 1. Wniosek, o którym mowa w art. 2, podlega opłacie skarbowej.")]
        [InlineData("Art. 1. Funkcjonariusz, który naruszył obowiązek, podlega karze dyscyplinarnej.")]
        [InlineData("Art. 1. Sprawca, który podlega karze za ten czyn, nie może być członkiem zarządu.")]
        public void ClauseThatImposesNoPenalty_OrDoesNotCloseTheSentence_IsNotASanction(string line)
        {
            Assert.Empty(Named(Export(line), "sankcja-karna"));
        }

        [Theory]
        [InlineData("Art. 1. Wzór jest dostępny na stronie https://www.gov.pl/web/finanse.", "https://www.gov.pl/web/finanse", ".")]
        [InlineData("Art. 1. Komunikat ogłasza się pod adresem www.bip.gov.pl, w dniu jego wydania.", "www.bip.gov.pl", ", w dniu jego wydania.")]
        public void AddressWrittenInTheText_StandsInH_WithoutThePunctuationAfterIt(string line, string url, string tail)
        {
            var link = Assert.Single(Named(Export(line), "h"));
            Assert.Equal(url, link.Value);
            Assert.Equal(tail, ((XText)link.NextNode!).Value);
        }

        [SchemaPlFact]
        public void SanctionsAndAddresses_AreSchemaValid()
        {
            var xml = Export(
                "Art. 1. Kto ujawnia tożsamość sygnalisty, podlega grzywnie.",
                "Art. 2. Kto:",
                "1) ujawnia tożsamość sygnalisty,",
                "2) utrudnia dokonanie zgłoszenia,",
                "– podlega karze grzywny.",
                "Art. 3. Wzór jest dostępny na stronie https://www.gov.pl/web/finanse.");

            Assert.Empty(SchemaPl.Validator.Validate(xml));
        }
    }
}
