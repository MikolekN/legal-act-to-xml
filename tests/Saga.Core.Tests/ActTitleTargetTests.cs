using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Tytuł aktu zmienianego jako cel komendy (ADR-0101): <c>eId</c> go nie adresuje, więc instrukcja jest ręczna
    /// (<c>*-recznie</c>), a jej adresem jest sam akt — tak zalecają autorzy schematu (2026-10-18, pytanie 6).
    /// </summary>
    public class ActTitleTargetTests
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

        private static (string Target, string? Function)[] Instructions(XElement xml) => xml.Descendants()
            .Where(e => e.Attribute("element-eId") is not null)
            .Select(e => (e.Attribute("element-eId")!.Value, e.Attribute("funkcja")?.Value))
            .ToArray();

        [Theory]
        // Korpus: 316836_2952324 („tytuł rozporządzenia otrzymuje brzmienie:").
        [InlineData("1) tytuł ustawy otrzymuje brzmienie:", "„o opłatach i należnościach”.", "zastap-recznie")]
        // Korpus: 314966_2806096, 336451_2986123 — odnośnik dodany do tytułu, pierwszy w akcie.
        [InlineData("1) do tytułu ustawy dodaje się odnośnik nr 1 w brzmieniu:", "„1) Niniejsza ustawa wdraża dyrektywę.”.", "dodaj-recznie")]
        [InlineData("1) dodaje się odnośnik nr 1 do tytułu ustawy w brzmieniu:", "„1) Niniejsza ustawa wdraża dyrektywę.”.", "dodaj-recznie")]
        public void CommandAboutTheTitle_IsAManualInstructionOnTheAct(string command, string quote, string function)
        {
            Assert.Equal([("DU/2021/200", function)], Instructions(Export(command, quote)));
        }

        [Theory]
        // Korpus: 336454_3062257 — sam „tytuł" w wyliczeniu zmian aktu, bez nazwy aktu.
        [InlineData("1) tytuł otrzymuje brzmienie:", "„o opłatach i należnościach”;")]
        // Korpus: 351432_3114915 — dodanie odnośnika do tytułu za innym zwrotem.
        [InlineData("1) odnośnik nr 1 do tytułu ustawy oznacza się jako odnośnik nr 2 do tytułu ustawy i do tytułu ustawy dodaje się odnośnik nr 1 w brzmieniu:",
            "„1) Niniejsza ustawa wdraża dyrektywę.”;")]
        public void TitleWithoutTheNameOfTheAct_IsTheTitleOfTheAct(string command, string quote)
        {
            Assert.Equal("DU/2021/200", Assert.Single(Instructions(Export(command, quote))).Target);
        }

        [Fact]
        public void BareTitleUnderAUnit_IsNotTheTitleOfTheAct()
        {
            // Pod „w dziale II:" sam „tytuł" to tytuł działu, nie aktu.
            var instructions = Instructions(Export("1) w dziale II:", "a) tytuł otrzymuje brzmienie:", "„Opłaty lokalne”;"));

            Assert.DoesNotContain(instructions, instruction => instruction.Target == "DU/2021/200");
        }

        [Fact]
        public void TitleAmongThePlacesOfAWordChange_IsATargetNextToTheUnits()
        {
            // Roadmapa, kandydat 15 (korpus: 310979_2778577): tytuł wymieniony wśród miejsc zmiany wyrazów nie był osobnym celem.
            var instructions = Instructions(Export(
                "1) w tytule ustawy, w art. 1 i w art. 5 wyraz „opłata” zastępuje się wyrazem „należność”;"));

            // Jednostki — zamiana automatyczna; tytuł nie ma eId, więc ręcznie na akcie (ADR-0101), za nimi.
            Assert.Equal(
                [("DU/2021/200__art_1", null), ("DU/2021/200__art_5", null), ("DU/2021/200", "zastap-recznie")],
                instructions);
        }

        [Fact]
        public void WordsChangedInTheTitle_AreAManualInstructionWithTheWords()
        {
            // Korpus: 301399_2685961 — automatyczna zmiana z adresem aktu objęłaby cały akt.
            var xml = Export("1) w tytule ustawy w ogólnym określeniu przedmiotu ustawy skreśla się wyrazy „uczniów i”.");

            Assert.Equal([("DU/2021/200", "uchyl-recznie")], Instructions(xml));
            Assert.Equal("uczniów i", xml.Descendants().Single(e => e.Name.LocalName == "cytat-w-tekscie").Value);
        }

        [Fact]
        public void TitleNamedRightAfterTheActFormula_IsTheTarget()
        {
            // Akt zmieniający z jednym przepisem: formuła aktu zmienianego stoi przed tytułem.
            var xml = LegalDocumentConverter.Convert(LegalDocumentParser.Parse(new[]
            {
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o zmianie ustawy o podatkach",
                "Art. 1. W ustawie z dnia 5 marca 2020 r. o podatkach (Dz. U. poz. 300) tytuł ustawy otrzymuje brzmienie:",
                "„o podatkach lokalnych”.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
            }.Select(line => new DocumentBlock { Text = line }).ToList(), new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!);

            Assert.Equal([("DU/2020/300", "zastap-recznie")], Instructions(xml));
        }

        [Theory]
        // Odnośnik do tytułu jako miejsce — to nie zmiana w tytule.
        [InlineData("w odnośniku nr 1 do tytułu ustawy akapit drugi otrzymuje brzmienie:")]
        // Tytuł jednostki systematyzacyjnej („tytuł rozdziału 2"), nie aktu.
        [InlineData("tytuł rozdziału 2 otrzymuje brzmienie:")]
        public void OtherTitles_AreNotTheActTitle(string text)
        {
            Assert.Null(LegalReferenceService.ActTitleTarget(text));
        }

        [SchemaPlFact]
        public void ManualInstructionsOnTheTitle_AreSchemaValid()
        {
            var xml = Export(
                "1) tytuł ustawy otrzymuje brzmienie:", "„o opłatach i należnościach”;",
                "2) w tytule ustawy skreśla się wyrazy „i należnościach”.");

            Assert.Empty(SchemaPl.Validator.ValidateActBody(xml.Elements().First()));
        }
    }
}
