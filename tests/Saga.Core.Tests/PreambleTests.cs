using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Preambuła ustawy: akapity między tytułem a pierwszą jednostką trafiają do <see cref="LegalDocument.Preamble"/>
    /// i wychodzą jako <c>preambula</c> — przedtem ginęły jako „akapity sprzed pierwszego artykułu".
    /// </summary>
    public class PreambleTests
    {
        private static DocumentBlock Text(string text, string? style = null) => new() { Text = text, StyleId = style };

        private static LegalDocument Parse(params string[] lines)
        {
            var blocks = new List<DocumentBlock>
            {
                new() { Text = "DZIENNIK USTAW RZECZYPOSPOLITEJ POLSKIEJ", Role = BlockRole.JournalHeader },
                new() { Text = "Warszawa, dnia 24 czerwca 2024 r.", Role = BlockRole.JournalHeader },
                new() { Text = "Poz. 928", Role = BlockRole.JournalHeader },
            };
            blocks.AddRange(lines.Select(line => Text(line)));
            return LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
        }

        private static List<XElement> Named(XElement xml, string name) =>
            xml.Descendants().Where(e => e.Name.LocalName == name).ToList();

        // Korpus: 302303_2701435 — ustawa o języku polskim.
        private static readonly string[] StatuteWithPreamble =
        [
            "USTAWA",
            "z dnia 7 października 1999 r.",
            "o języku polskim",
            "Parlament Rzeczypospolitej Polskiej:",
            "– zważywszy, że język polski stanowi podstawowy element narodowej tożsamości,",
            "– uznając konieczność ochrony tożsamości narodowej w procesie globalizacji,",
            "uchwala niniejszą ustawę.",
            "Rozdział 1",
            "Przepisy ogólne",
            "Art. 1. Przepisy ustawy dotyczą ochrony języka polskiego.",
            "Art. 2. Ustawa wchodzi w życie po upływie 6 miesięcy od dnia ogłoszenia.",
        ];

        [Fact]
        public void ParagraphsBetweenTheTitleAndTheFirstUnit_AreThePreamble()
        {
            var document = Parse(StatuteWithPreamble);

            Assert.Equal(
                new[]
                {
                    "Parlament Rzeczypospolitej Polskiej:",
                    "– zważywszy, że język polski stanowi podstawowy element narodowej tożsamości,",
                    "– uznając konieczność ochrony tożsamości narodowej w procesie globalizacji,",
                    "uchwala niniejszą ustawę.",
                },
                document.Preamble);
            Assert.Equal("o języku polskim", document.Subject);
            Assert.Equal(new[] { "1", "2" }, document.Articles.Select(article => article.Number?.Value));
        }

        [Fact]
        public void Export_PutsThePreambleRightAfterTheTitle_OneParagraphEach()
        {
            var root = LegalDocumentConverter.Convert(Parse(StatuteWithPreamble)).Elements().Single();

            Assert.Equal(
                new[] { "metadane", "rodzaj-aktu", "data", "tytul", "preambula" },
                root.Elements().Take(5).Select(e => e.Name.LocalName));

            var preamble = Named(root, "preambula").Single();
            Assert.Null(preamble.Attribute("eId"));
            Assert.Equal(new[] { "p", "p", "p", "p" }, preamble.Elements().Select(e => e.Name.LocalName));
            Assert.Equal("uchwala niniejszą ustawę.", preamble.Elements().Last().Value);
            Assert.All(preamble.Elements(), paragraph => Assert.Contains(paragraph.Elements(), e => e.Name.LocalName == "zdanie"));
        }

        [Fact]
        public void StatuteWithoutAPreamble_HasNone()
        {
            var document = Parse(
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o opłatach",
                "Rozdział 1", "Przepisy ogólne",
                "Art. 1. Ustawa określa opłaty.");

            Assert.Empty(document.Preamble);
            Assert.Empty(Named(LegalDocumentConverter.Convert(document), "preambula"));
        }

        [Fact]
        public void TextAfterTheFirstHeading_IsNotThePreamble()
        {
            // Za oznaczeniem rozdziału stoi jego tytuł i treść aktu — preambuła skończyła się wcześniej.
            var document = Parse(
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o opłatach",
                "W trosce o finanse publiczne stanowi się, co następuje:",
                "Rozdział 1", "Przepisy ogólne",
                "Akapit, który nie jest ani tytułem, ani przepisem.",
                "Art. 1. Ustawa określa opłaty.");

            Assert.Equal(new[] { "W trosce o finanse publiczne stanowi się, co następuje:" }, document.Preamble);
        }

        [Fact]
        public void LegalBasisOfARegulation_IsNotAPreamble()
        {
            var document = Parse(
                "ROZPORZĄDZENIE", "MINISTRA FINANSÓW", "z dnia 14 czerwca 2024 r.", "w sprawie wzoru zgłoszenia",
                "Na podstawie art. 5 ustawy z dnia 1 lutego 2021 r. o zgłoszeniach (Dz. U. poz. 200) zarządza się, co następuje:",
                "§ 1. Rozporządzenie określa wzór zgłoszenia.",
                "§ 2. Rozporządzenie wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

            Assert.Empty(document.Preamble);
            Assert.StartsWith("Na podstawie art. 5", document.LegalBasis);
        }

        [Theory]
        // Adnotacja tekstu jednolitego o uchylonym nagłówku stoi w miejscu rozdziału, nie preambuły.
        [InlineData("(oznaczenie rozdziału 1 i tytuł – uchylone)")]
        // Akapit z numerem preambułą nie jest.
        [InlineData("1. Na podstawie art. 16 ust. 1 ustawy z dnia 20 lipca 2000 r. o ogłaszaniu aktów normatywnych ogłasza się tekst.")]
        public void OtherLinesBeforeTheFirstArticle_AreNotThePreamble(string line)
        {
            var document = Parse(
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o opłatach",
                line,
                "Art. 1. Ustawa określa opłaty.");

            Assert.Empty(document.Preamble);
        }

        [Fact]
        public void PreambleOfAConsolidatedStatute_StandsInTheAnnouncedAct()
        {
            // Korpus: 275043_2474596 — tekst jednolity ustawy z preambułą.
            var xml = LegalDocumentConverter.Convert(Parse(
                "OBWIESZCZENIE",
                "MARSZAŁKA SEJMU RZECZYPOSPOLITEJ POLSKIEJ",
                "z dnia 6 sierpnia 2025 r.",
                "w sprawie ogłoszenia jednolitego tekstu ustawy o wspieraniu rodziny",
                "1. Na podstawie art. 16 ust. 1 ustawy z dnia 20 lipca 2000 r. o ogłaszaniu aktów normatywnych ogłasza się jednolity tekst ustawy.",
                "Marszałek Sejmu: S. Hołownia",
                "Załącznik do obwieszczenia Marszałka Sejmu Rzeczypospolitej Polskiej z dnia 6 sierpnia 2025 r. (Dz. U. poz. 1094)",
                "USTAWA",
                "z dnia 9 czerwca 2011 r.",
                "o wspieraniu rodziny",
                "Dla dobra dzieci, które potrzebują szczególnej ochrony i pomocy ze strony dorosłych,",
                "uchwala się, co następuje:",
                "Art. 1. Ustawa określa zasady wspierania rodziny.",
                "Art. 2. Ustawa wchodzi w życie z dniem 1 stycznia 2012 r."));

            var act = xml.Elements().Single().Elements().Single(e => e.Name.LocalName == "ustawa");
            Assert.Equal(
                new[] { "zalacznik-do", "rodzaj-aktu", "data", "tytul", "preambula", "artykul", "artykul" },
                act.Elements().Select(e => e.Name.LocalName));
            Assert.Equal(2, act.Elements().Single(e => e.Name.LocalName == "preambula").Elements().Count());
        }

        [SchemaPlFact]
        public void StatuteWithAPreamble_IsSchemaValid_AsAWhole()
        {
            Assert.Empty(SchemaPl.Validator.Validate(LegalDocumentConverter.Convert(Parse(StatuteWithPreamble))));
        }
    }
}
