using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Core.Services.Converters;
using Saga.Core.Services.Parsing;
using Saga.Model;
using Saga.Model.EditorialUnits;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Kilka komend w osobnych zdaniach jednej jednostki (ADR-0122): każde zdanie z komendą to osobna nowelizacja z celami
    /// ze swojego tekstu, a jej instrukcja stoi w zdaniu, które ją zapisuje. Komendy przed ostatnią muszą kończyć się
    /// w swoim zdaniu (uchylenie, zmiana w tekście) — inaczej jednostka zostaje jedną komendą.
    /// </summary>
    public class SentenceCommandsTests
    {
        private const string EntryIntoForce = "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.";

        // Wyliczenie ma co najmniej dwa punkty — inaczej punkt jest spoza ZTP; komendy bada pierwszy.
        private const string NextPoint = "2) art. 140 uchyla się.";

        private static LegalDocument Parse(string entryIntoForce, params string[] provisions)
        {
            string[] lines =
            [
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o zmianie ustawy o opłatach",
                "Art. 1. W ustawie z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) wprowadza się następujące zmiany:",
                .. provisions,
                NextPoint,
                entryIntoForce,
            ];
            return LegalDocumentParser.Parse(
                lines.Select(line => new DocumentBlock { Text = line }).ToList(),
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
        }

        private static XElement Export(params string[] provisions) => LegalDocumentConverter.Convert(Parse(EntryIntoForce, provisions));

        /// <summary>Instrukcje w kolejnych zdaniach jednostki: „nazwa element-eId" na każde zdanie.</summary>
        private static string[][] InstructionsPerSentence(XElement unit) => unit
            .Elements().Single(e => e.Name.LocalName == "p")
            .Elements().Where(e => e.Name.LocalName == "zdanie")
            .Select(sentence => sentence.Elements()
                .Where(e => e.Attribute("element-eId") is not null)
                .Select(e => $"{e.Name.LocalName} {e.Attribute("element-eId")!.Value}")
                .ToArray())
            .ToArray();

        private static List<XElement> Units(XElement xml, string name) =>
            xml.Descendants().Where(e => e.Name.LocalName == name).ToList();

        [Fact]
        public void RepealAndReplacedSentence_AreTwoCommands_EachInItsSentence()
        {
            var xml = Export(
                "1) art. 127 uchyla się. W art. 128 zdanie drugie otrzymuje brzmienie:",
                "„Opłaty nie pobiera się od osób zwolnionych.”;");

            var point = Units(xml, "punkt")[0];
            Assert.Equal(
                [["uchyl DU/2021/200__art_127"], ["cytat-w-tekscie DU/2021/200__art_128__zdn_2"]],
                InstructionsPerSentence(point));

            // Treść cytowana należy do komendy drugiego zdania — nie zostaje fragmentem za tekstem punktu.
            var quoted = point.Descendants().Single(e => e.Name.LocalName == "cytat-w-tekscie");
            Assert.Equal("Opłaty nie pobiera się od osób zwolnionych.", quoted.Elements().Single(e => e.Name.LocalName == "zdanie").Value);
            Assert.Equal("zastap-tekst", quoted.Attribute("funkcja")?.Value);
            Assert.DoesNotContain(point.Elements(), e => e.Name.LocalName == "fragment");
        }

        [Fact]
        public void TextChangeThenRepeal_AreTwoCommands()
        {
            var xml = Export("1) w art. 127 wyrazy „14 dni” zastępuje się wyrazami „30 dni”. Art. 128 uchyla się;");

            Assert.Equal(
                [["zamien-tekst DU/2021/200__art_127"], ["uchyl DU/2021/200__art_128"]],
                InstructionsPerSentence(Units(xml, "punkt")[0]));
        }

        [Fact]
        public void EveryEarlierSentence_IsACommandOfItsOwn()
        {
            var xml = Export(
                "1) art. 136 uchyla się. Art. 137 uchyla się. Po art. 137 dodaje się art. 137a w brzmieniu:",
                "„Art. 137a. Opłaty nie pobiera się.”;");

            Assert.Equal(
                [["uchyl DU/2021/200__art_136"], ["uchyl DU/2021/200__art_137"], ["cytat-strukt DU/2021/200__art_137"]],
                InstructionsPerSentence(Units(xml, "punkt")[0]));
        }

        [Fact]
        public void SentenceCommandsUnderAPlace_TakeTheirTargetsFromIt()
        {
            var xml = Export(
                "1) w art. 131:",
                "a) ust. 1 uchyla się. W ust. 2 zdanie pierwsze otrzymuje brzmienie:",
                "„Opłatę wnosi się w terminie 14 dni.”,",
                "b) ust. 3 uchyla się;");

            Assert.Equal(
                [["uchyl DU/2021/200__art_131__ust_1"], ["cytat-w-tekscie DU/2021/200__art_131__ust_2__zdn_1"]],
                InstructionsPerSentence(Units(xml, "litera")[0]));
        }

        [Fact]
        public void NextSentence_IsReadInThePlaceOfThePreviousOne()
        {
            var xml = Export(
                "1) w art. 5 ust. 2 uchyla się. Ust. 3 otrzymuje brzmienie:",
                "„3. Opłaty nie pobiera się.”;");

            Assert.Equal(
                [["uchyl DU/2021/200__art_5__ust_2"], ["cytat-strukt DU/2021/200__art_5__ust_3"]],
                InstructionsPerSentence(Units(xml, "punkt")[0]));
        }

        [Fact]
        public void UnitsUnderTheLastCommand_TakeItsPlace()
        {
            var xml = Export(
                "1) art. 132 uchyla się. W art. 133 wprowadza się następujące zmiany:",
                "a) ust. 1 uchyla się,",
                "b) ust. 2 uchyla się;");

            Assert.Equal([["uchyl DU/2021/200__art_132"], []], InstructionsPerSentence(Units(xml, "punkt")[0]));
            Assert.Equal(
                [[["uchyl DU/2021/200__art_133__ust_1"]], [["uchyl DU/2021/200__art_133__ust_2"]]],
                Units(xml, "litera").Select(InstructionsPerSentence));
        }

        [Fact]
        public void SentenceBreakInsideQuotedWords_DoesNotEndTheCommand()
        {
            // Podział eksportu tnie zdanie także w cudzysłowie („Opłata. Tak”) — komenda kończy się dopiero za nim.
            var xml = Export("1) w art. 134 wyrazy „Opłata. Tak” zastępuje się wyrazami „Nie”. Art. 135 uchyla się;");

            Assert.Equal(
                [[], ["zamien-tekst DU/2021/200__art_134"], ["uchyl DU/2021/200__art_135"]],
                InstructionsPerSentence(Units(xml, "punkt")[0]));
        }

        [Fact]
        public void EarlierCommand_EntersIntoForceWithItsSentence()
        {
            var document = Parse(
                "Art. 2. Ustawa wchodzi w życie z dniem 1 lipca 2026 r., z wyjątkiem art. 1 pkt 1 zdanie pierwsze, które wchodzi " +
                "w życie z dniem 1 stycznia 2027 r.",
                "1) art. 127 uchyla się. Art. 128 uchyla się;");

            var point = document.Articles.First().Paragraphs[0].Points[0];
            Assert.Equal(new DateTime(2027, 1, 1), Assert.Single(point.EarlierSentenceAmendments).EffectiveDate);
            Assert.Equal(new DateTime(2026, 7, 1), point.Amendment!.EffectiveDate);
        }

        [Fact]
        public void CanonicalSnapshot_ListsEarlierCommandsWithTheirSentence()
        {
            var document = Parse(EntryIntoForce, "1) art. 127 uchyla się. Art. 128 uchyla się;");

            var amendments = XDocument.Parse(CanonicalDtoXmlSerializer.Serialize(document)).Descendants("amendment").ToList();
            Assert.Equal(["1", null, null], amendments.Select(amendment => amendment.Attribute("sentence")?.Value));
            Assert.Equal(["art_127", "art_128", "art_140"], amendments.Select(amendment => amendment.Element("target")!.Value));
        }

        [Fact]
        public void Read_GivesEachCommandWithItsSentenceNumber()
        {
            var point = new Point { ContentText = "uchyla się art. 127. W art. 128 zdanie drugie otrzymuje brzmienie:" };

            Assert.Equal(
                [("uchyla się art. 127.", 1), ("W art. 128 zdanie drugie otrzymuje brzmienie:", 2)],
                SentenceCommands.Read(point)!.Select(command => (command.Text, command.Sentence)));
        }

        [Theory]
        // Komenda pierwszego zdania podaje brzmienie w cudzysłowie — nie kończy się zmianą bez treści.
        [InlineData("art. 5 otrzymuje brzmienie: „Art. 5. Opłatę wnosi się raz.”. Uchyla się art. 7.")]
        // Jedna komenda i zdanie bez komendy.
        [InlineData("uchyla się art. 127. Przepis stosuje się odpowiednio.")]
        // Czasowniki w zwykłym znaczeniu poza artykułem zmieniającym: decyzję uchyla się, dokumenty się dołącza.
        [InlineData("Decyzję uchyla się, jeżeli strona o to wnosi. Do wniosku dodaje się dokumenty.")]
        public void Read_KeepsTheUnitOneCommand_UnlessEveryEarlierCommandEndsInItsSentence(string text)
        {
            Assert.Null(SentenceCommands.Read(new Point { ContentText = text }));
        }
    }
}
