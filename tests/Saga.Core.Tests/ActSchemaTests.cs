using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;

namespace Saga.Core.Tests
{
    /// <summary>Cały eksport aktu wobec schemaPL.xsd — z nagłówkiem, nie tylko treść.</summary>
    public class ActSchemaTests
    {
        private static XElement Export() => Export(
            "USTAWA", "z dnia 14 czerwca 2024 r.", "o ochronie sygnalistów",
            "Rozdział 1", "Przepisy ogólne",
            "Art. 1. Ustawa określa zasady ochrony sygnalistów.",
            "Art. 2. Ustawa wchodzi w życie po upływie 3 miesięcy od dnia ogłoszenia.");

        private static XElement Export(params string[] lines)
        {
            var blocks = new List<DocumentBlock>
            {
                new() { Text = "DZIENNIK USTAW RZECZYPOSPOLITEJ POLSKIEJ", Role = BlockRole.JournalHeader },
                new() { Text = "Warszawa, dnia 24 czerwca 2024 r.", Role = BlockRole.JournalHeader },
                new() { Text = "Poz. 928", Role = BlockRole.JournalHeader },
            };
            blocks.AddRange(lines.Select(text => new DocumentBlock { Text = text }));

            var document = LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document;
            return LegalDocumentConverter.Convert(document!);
        }

        /// <summary>
        /// Problemy całego dokumentu. Daty wejścia w życie i mocy wstecznej aktu stoją jeszcze w metadanych
        /// w elementach spoza schematu (schemat trzyma je w DaneCzasowe, backlog) — poza nimi dokument ma być
        /// zgodny w całości.
        /// </summary>
        private static IReadOnlyList<string> Problems(XElement xml)
        {
            xml.Descendants().Where(e => e.Name.LocalName is "DataWejsciaWZycie" or "DatyMocyWstecznej").Remove();
            return SchemaPl.Validator.Validate(xml);
        }

        [SchemaPlFact]
        public void ActWithJournalHeader_IsSchemaValid_AsAWhole()
        {
            Assert.Empty(Problems(Export()));
        }

        [SchemaPlFact]
        public void AmendingAct_IsSchemaValid_AsAWhole()
        {
            var xml = Export(
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o zmianie ustawy o systemie ubezpieczeń społecznych",
                "Art. 1. W ustawie z dnia 13 października 1998 r. o systemie ubezpieczeń społecznych (Dz. U. z 2025 r. poz. 350) wprowadza się następujące zmiany:",
                "1) w art. 68 ust. 1 pkt 5 otrzymuje brzmienie:",
                "„5) orzekanie o niezdolności do pracy;”;",
                "2) w art. 69 wyrazy „organ rentowy” zastępuje się wyrazami „Zakład”;",
                "3) uchyla się art. 70;",
                "4) po rozdziale 9 dodaje się rozdział 9a w brzmieniu:",
                "„Rozdział 9a",
                "Orzekanie dla celów świadczeń",
                "Art. 85a. Zadania realizuje Zakład.”.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

            Assert.Empty(Problems(xml));
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
