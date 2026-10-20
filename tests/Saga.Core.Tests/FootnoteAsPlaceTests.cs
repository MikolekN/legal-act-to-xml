using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Odnośnik jako miejsce zmiany (ADR-0106, ustalenie z 2026-10-19 do pytania 24): akapity i punkty odnośnika nie mają
    /// eId, więc celem jest sam odnośnik (<c>odn_1</c>) z instrukcją ręczną — a nie jednostka aktu o tym samym numerze.
    /// </summary>
    public class FootnoteAsPlaceTests
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

        private static (string Name, string Target, string? Function)[] Instructions(XElement xml) => xml.Descendants()
            .Where(e => e.Attribute("element-eId") is not null && e.Name.LocalName is not ("punkt" or "ustep"))
            .Select(e => (e.Name.LocalName, e.Attribute("element-eId")!.Value, e.Attribute("funkcja")?.Value))
            .ToArray();

        [Fact]
        public void ParagraphAndPointOfAFootnote_AreManualInstructionsOnTheFootnote()
        {
            // Korpus: 354686_3141475 (Dz. U. 2025 poz. 1863) — „w odnośniku nr 1 do tytułu ustawy: a) w akapicie drugim w pkt 5
            // kropkę zastępuje się średnikiem i dodaje się pkt 6 …, b) akapit trzeci otrzymuje brzmienie:".
            var xml = Export(
                "1) w odnośniku nr 1 do tytułu ustawy:",
                "a) w akapicie drugim w pkt 5 kropkę zastępuje się średnikiem i dodaje się pkt 6 w brzmieniu:",
                "„6) dyrektywy 2024/1.”,",
                "b) akapit trzeci otrzymuje brzmienie:",
                "„Ustawa zmienia inne ustawy.”.");

            Assert.Equal(
            [
                ("cytat-w-tekscie", "DU/2021/200__odn_1", "zastap-recznie"),
                ("cytat-strukt", "DU/2021/200__odn_1", "dodaj-recznie"),
                ("cytat-w-tekscie", "DU/2021/200__odn_1", "zastap-recznie"),
            ], Instructions(xml));

            // Punkt odnośnika nie udaje punktu aktu (pkt_6) — stoi pod odnośnikiem.
            Assert.Equal("DU/2021/200__odn_1__pkt_6", xml.Descendants().Single(e => e.Name.LocalName == "punkt").Attribute("eId")?.Value);
        }

        [Fact]
        public void WordsChangedInTheWholeFootnote_StayAnAutomaticChange()
        {
            var xml = Export("1) w odnośniku nr 2 wyrazy „opłata” zastępuje się wyrazami „należność”.");

            Assert.Equal([("zamien-tekst", "DU/2021/200__odn_2", null)], Instructions(xml));
        }

        [Fact]
        public void FootnoteAsTheObjectOfTheCommand_StaysAQuotedFootnote()
        {
            // ADR-0059 — „odnośnik nr 3 otrzymuje brzmienie:" zmienia cały odnośnik.
            var xml = Export("1) odnośnik nr 3 otrzymuje brzmienie:", "„3) Zmiany ustawy zostały ogłoszone.”.");

            Assert.Equal([("cytat-strukt", "DU/2021/200__odn_3", "zastap")], Instructions(xml));
        }

        [SchemaPlFact]
        public void ChangesInsideAFootnote_AreSchemaValid()
        {
            var xml = Export(
                "1) w odnośniku nr 1 do tytułu ustawy akapit trzeci otrzymuje brzmienie:", "„Ustawa zmienia inne ustawy.”;",
                "2) w odnośniku nr 2 wyrazy „opłata” zastępuje się wyrazami „należność”.");

            Assert.Empty(SchemaPl.Validator.ValidateActBody(xml.Elements().First()));
        }
    }
}
