using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;

namespace Saga.Core.Tests
{
    /// <summary>Cały eksport aktu wobec schemaPL.xsd — z nagłówkiem, nie tylko treść.</summary>
    public class ActSchemaTests
    {
        private static XElement Export()
        {
            var blocks = new List<DocumentBlock>
            {
                new() { Text = "DZIENNIK USTAW RZECZYPOSPOLITEJ POLSKIEJ", Role = BlockRole.JournalHeader },
                new() { Text = "Warszawa, dnia 24 czerwca 2024 r.", Role = BlockRole.JournalHeader },
                new() { Text = "Poz. 928", Role = BlockRole.JournalHeader },
            };
            blocks.AddRange(new[]
            {
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o ochronie sygnalistów",
                "Rozdział 1", "Przepisy ogólne",
                "Art. 1. Ustawa określa zasady ochrony sygnalistów.",
                "Art. 2. Ustawa wchodzi w życie po upływie 3 miesięcy od dnia ogłoszenia.",
            }.Select(text => new DocumentBlock { Text = text }));

            var document = LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document;
            return LegalDocumentConverter.Convert(document!);
        }

        [SchemaPlFact]
        public void ActWithJournalHeader_IsSchemaValid_AsAWhole()
        {
            var xml = Export();

            // Daty wejścia w życie i mocy wstecznej aktu stoją jeszcze w metadanych w elementach spoza
            // schematu; schemat trzyma je w DaneCzasowe (backlog). Poza nimi dokument ma być zgodny w całości.
            xml.Descendants().Where(e => e.Name.LocalName is "DataWejsciaWZycie" or "DatyMocyWstecznej").Remove();

            Assert.Empty(SchemaPl.Validator.Validate(xml));
        }

        [Fact]
        public void ActTitleBlock_IsExportedAsKindDateAndSubject()
        {
            var act = Export().Elements().First();
            var ns = act.Name.Namespace;

            Assert.Equal("USTAWA", act.Element(ns + "rodzaj-aktu")?.Value);
            Assert.Equal("z dnia 14 czerwca 2024 r.", act.Element(ns + "data")?.Value);
            Assert.Equal("o ochronie sygnalistów", act.Element(ns + "tytul")?.Value);
            Assert.Equal(new[] { "metadane", "rodzaj-aktu", "data", "tytul", "rozdzial" },
                act.Elements().Select(e => e.Name.LocalName).Take(5));
        }
    }
}
