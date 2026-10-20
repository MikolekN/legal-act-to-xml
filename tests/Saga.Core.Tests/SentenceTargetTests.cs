using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Zdanie jako cel komendy (ADR-0102): komenda, która podaje numer zdania, wskazuje <c>…__zdn_N</c>; zdanie dodawane —
    /// <c>dodaj-po</c> na zdaniu poprzednim; sąsiednie zdania zastąpione jednym cytatem — zakres; zdanie bez numeru —
    /// instrukcja ręczna na jednostce (ustalenie z 2026-10-19 do pytania 7).
    /// </summary>
    public class SentenceTargetTests
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

        private static (string Name, string Target, string? To, string? Function)[] Instructions(XElement xml) => xml.Descendants()
            .Where(e => e.Attribute("element-eId") is not null)
            .Select(e => (e.Name.LocalName, e.Attribute("element-eId")!.Value, e.Attribute("element-do-eId")?.Value, e.Attribute("funkcja")?.Value))
            .ToArray();

        [Fact]
        public void ReplacedSentence_IsTheTarget()
        {
            // Przykład autorów schematu: „w art. 126 zdanie pierwsze otrzymuje brzmienie" → …__art_126__zdn_1.
            var xml = Export("1) w art. 5 w ust. 2 zdanie drugie otrzymuje brzmienie:", "„Opłatę pobiera się raz w roku.”.");

            Assert.Equal([("cytat-w-tekscie", "DU/2021/200__art_5__ust_2__zdn_2", null, "zastap-tekst")], Instructions(xml));
        }

        [Fact]
        public void WordsChangedInASentence_AreChangedInThatSentence()
        {
            // Korpus: 308909_2756164 — wyrazy stoją w obu zdaniach ustępu, a komenda zmienia je tylko w pierwszym.
            var xml = Export("1) w art. 8 w ust. 3c w zdaniu pierwszym po wyrazach „ust. 3b pkt 1 lub 2,” dodaje się wyrazy „stosowana do dnia 30 czerwca 2025 r.”.");

            Assert.Equal([("zamien-tekst", "DU/2021/200__art_8__ust_3c__zdn_1", null, null)], Instructions(xml));
        }

        [Fact]
        public void AddedSentence_StandsAfterTheSentenceBeforeIt()
        {
            var xml = Export("1) w art. 7a w ust. 7 dodaje się zdanie trzecie w brzmieniu:", "„Opłaty nie zwraca się.”.");

            Assert.Equal([("cytat-w-tekscie", "DU/2021/200__art_7a__ust_7__zdn_2", null, "dodaj-po")], Instructions(xml));
        }

        [Fact]
        public void AdjacentSentencesReplacedByOneQuotation_AreOneRange()
        {
            // Korpus: 338277_3001765 — „w § 1 zdanie drugie i trzecie otrzymują brzmienie:".
            var xml = Export("1) w art. 132 w ust. 1 zdanie drugie i trzecie otrzymują brzmienie:", "„Opłatę pobiera się raz w roku. Opłaty nie zwraca się.”.");

            Assert.Equal(
                [("cytat-w-tekscie", "DU/2021/200__art_132__ust_1__zdn_2", "DU/2021/200__art_132__ust_1__zdn_3", "zastap-tekst")],
                Instructions(xml));
        }

        [Fact]
        public void WordsChangedInNonAdjacentSentences_AreOneInstructionPerSentence()
        {
            // Korpus: 308873_2757497 — „w ust. 1 w zdaniu pierwszym i trzecim oraz w ust. 2 i 4 wyrazy …".
            var xml = Export("1) w art. 85b w ust. 1 w zdaniu pierwszym i trzecim oraz w ust. 2 i 4 wyrazy „31 grudnia 2025 r.” zastępuje się wyrazami „31 grudnia 2026 r.”.");

            Assert.Equal(
                ["DU/2021/200__art_85b__ust_1__zdn_1", "DU/2021/200__art_85b__ust_1__zdn_3", "DU/2021/200__art_85b__ust_2", "DU/2021/200__art_85b__ust_4"],
                Instructions(xml).Select(i => i.Target));
        }

        [Theory]
        // Zdanie bez numeru — jego adresu komenda nie daje: instrukcja ręczna na jednostce.
        [InlineData("1) w art. 5 w ust. 2 w zdaniu ostatnim wyrazy „raz” zastępuje się wyrazami „dwa razy”.", null, "zastap-recznie")]
        [InlineData("1) w art. 5 w ust. 2 dodaje się zdanie w brzmieniu:", "„Opłaty nie zwraca się.”.", "dodaj-recznie")]
        public void SentenceWithoutANumber_IsAManualInstructionOnTheUnit(string command, string? quote, string function)
        {
            var xml = Export(quote is null ? [command] : [command, quote]);

            Assert.Equal([("DU/2021/200__art_5__ust_2", function)], Instructions(xml).Select(i => (i.Target, i.Function)));
        }

        [SchemaPlFact]
        public void SentenceTargets_AreSchemaValid()
        {
            var xml = Export(
                "1) w art. 5 w ust. 2 zdanie drugie otrzymuje brzmienie:", "„Opłatę pobiera się raz w roku.”;",
                "2) w art. 7a w ust. 7 dodaje się zdanie trzecie w brzmieniu:", "„Opłaty nie zwraca się.”;",
                "3) w art. 132 w ust. 1 zdanie drugie i trzecie otrzymują brzmienie:", "„Opłatę pobiera się raz w roku. Opłaty nie zwraca się.”.");

            Assert.Empty(SchemaPl.Validator.ValidateActBody(xml.Elements().First()));
        }
    }
}
