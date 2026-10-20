using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Core.Services.Parsing;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Znak dodawany bez wskazania miejsca w tekście („w pkt 4 dodaje się przecinek i dodaje się pkt 5") — ostatniego słowa
    /// jednostki akt zmieniający nie podaje, więc zmiana jest instrukcją ręczną <c>dodaj-recznie</c> ze znakiem na jednostce
    /// (ADR-0104, odpowiedź autorów schematu z 2026-10-20 do pytania 10). Dotąd ginęła bez instrukcji.
    /// </summary>
    public class MarkAddedWithoutPlaceTests
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

        private static (string Target, string? Function, string Text)[] Instructions(XElement xml) => xml.Descendants()
            .Where(e => e.Attribute("element-eId") is not null && e.Name.LocalName != "punkt")
            .Select(e => (e.Attribute("element-eId")!.Value, e.Attribute("funkcja")?.Value,
                e.Name.LocalName == "cytat-w-tekscie" ? e.Value : string.Empty))
            .ToArray();

        [Theory]
        // Korpus: 31 dokumentów — „w ust. 2 w pkt 5 dodaje się przecinek i pkt 6 w brzmieniu:", „na końcu dodaje się średnik".
        [InlineData("1) w art. 4d w ust. 5 w pkt 4 dodaje się przecinek i dodaje się pkt 5 w brzmieniu:", ",")]
        [InlineData("1) w art. 4d w ust. 5 w pkt 4 na końcu dodaje się średnik i dodaje się pkt 5 w brzmieniu:", ";")]
        [InlineData("1) w art. 4d w ust. 5 w pkt 4 na końcu dodaje się przecinek i wyraz „lub” oraz dodaje się pkt 5 w brzmieniu:", ", lub")]
        public void MarkAddedWithoutAPlace_IsAManualAdditionOnTheUnit_NextToTheAddedUnit(string command, string mark)
        {
            var xml = Export(command, "„5) zakład ubezpieczeń.”.");

            Assert.Equal(
            [
                ("DU/2021/200__art_4d__ust_5__pkt_4", "dodaj-po", ""),
                ("DU/2021/200__art_4d__ust_5__pkt_4", "dodaj-recznie", mark),
            ], Instructions(xml).OrderBy(i => i.Function).ToArray());
        }

        [Fact]
        public void MarkAddedAfterNamedWords_StaysAnAutomaticChange()
        {
            // Miejsce wskazane wyrazami — zmiana automatyczna „Finansowego" → „Finansowego,".
            var xml = Export("1) w art. 4d w ust. 5 w pkt 4 po wyrazach „Finansowego” dodaje się przecinek.");

            var change = Assert.Single(xml.Descendants(), e => e.Name.LocalName == "zamien-tekst");
            Assert.Equal(("Finansowego", "Finansowego,"), (change.Attribute("stary-tekst")?.Value, change.Attribute("nowy-tekst")?.Value));
        }

        [Theory]
        [InlineData("w pkt 4 dodaje się przecinek i dodaje się pkt 5 w brzmieniu:", AmendmentCommandKind.AddSign)]
        [InlineData("w pkt 4 po wyrazach „banku” dodaje się przecinek;", AmendmentCommandKind.AddWords)]
        [InlineData("w pkt 4 kropkę zastępuje się przecinkiem i dodaje się pkt 5 w brzmieniu:", AmendmentCommandKind.ReplaceSign)]
        public void SignCommand_IsReadAsItsKind(string text, AmendmentCommandKind kind)
        {
            Assert.Equal(kind, Assert.Single(AmendmentCommandParser.TextChangeCommands(text)).Kind);
        }

        [SchemaPlFact]
        public void ManualMarkAddition_IsSchemaValid()
        {
            var xml = Export("1) w art. 4d w ust. 5 w pkt 4 dodaje się przecinek i dodaje się pkt 5 w brzmieniu:", "„5) zakład ubezpieczeń.”.");

            Assert.Empty(SchemaPl.Validator.ValidateActBody(xml.Elements().First()));
        }
    }
}
