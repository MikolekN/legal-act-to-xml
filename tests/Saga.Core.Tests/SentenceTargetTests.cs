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

        // ============================================================
        // Zdanie-przedmiot w każdym miejscu, zdanie-miejsce przy swojej jednostce (ADR-0126)
        // ============================================================

        [Theory]
        // Przedmiotem jest zdanie — „art. 7" i „art. 8" to miejsca; zdanie stoi w każdym, z powtórzonym „w" czy bez.
        [InlineData("1) w art. 7 i 8 zdanie pierwsze otrzymuje brzmienie:", "DU/2021/200__art_7__zdn_1", "DU/2021/200__art_8__zdn_1")]
        [InlineData("1) w art. 7 i w art. 8 zdanie pierwsze otrzymuje brzmienie:", "DU/2021/200__art_7__zdn_1", "DU/2021/200__art_8__zdn_1")]
        [InlineData("1) w art. 7 ust. 2 i art. 8 ust. 3 zdanie pierwsze otrzymuje brzmienie:",
            "DU/2021/200__art_7__ust_2__zdn_1", "DU/2021/200__art_8__ust_3__zdn_1")]
        public void ReplacedSentence_StandsInEveryPlaceBeforeIt(string command, string first, string second)
        {
            var xml = Export(command, "„Opłatę wnosi się w terminie 14 dni.”;");

            Assert.Equal([(first, "zastap-tekst"), (second, "zastap-tekst")], Instructions(xml).Select(i => (i.Target, i.Function)));
        }

        [Theory]
        [InlineData("1) w art. 5 w ust. 2 i 3 zdanie drugie skreśla się;")]
        [InlineData("1) w art. 5 w ust. 2 i 3 uchyla się zdanie drugie;")]
        public void RepealedSentence_StandsInEveryPlaceBeforeIt(string command)
        {
            var xml = Export(command);

            Assert.Equal(["DU/2021/200__art_5__ust_2__zdn_2", "DU/2021/200__art_5__ust_3__zdn_2"], Instructions(xml).Select(i => i.Target));
        }

        [Fact]
        public void SentencesReplacedInSeveralPlaces_AreOneRangePerPlace()
        {
            var xml = Export("1) w art. 7 i 8 zdania pierwsze i drugie otrzymują brzmienie:",
                "„Opłatę wnosi się w terminie 14 dni. Opłaty nie zwraca się.”;");

            Assert.Equal([("DU/2021/200__art_7__zdn_1", "DU/2021/200__art_7__zdn_2"), ("DU/2021/200__art_8__zdn_1", "DU/2021/200__art_8__zdn_2")],
                Instructions(xml).Select(i => (i.Target, i.To)));
        }

        [Theory]
        // Zdanie-miejsce należy do jednostki, po której stoi (§ 59 ust. 2 ZTP); powtórzone „w zdaniu" wskazuje każde miejsce.
        // Korpus: Dz. U. 2026 poz. 607.
        [InlineData("1) w art. 5 w ust. 10 i w ust. 11 w zdaniu pierwszym wyrazy „14 dni” zastępuje się wyrazami „30 dni”;",
            "DU/2021/200__art_5__ust_10", "DU/2021/200__art_5__ust_11__zdn_1")]
        // Jednostka i zdanie jako dwa przedmioty uchylenia.
        [InlineData("1) w art. 5 uchyla się ust. 2 i ust. 3 zdanie drugie;", "DU/2021/200__art_5__ust_2", "DU/2021/200__art_5__ust_3__zdn_2")]
        // Korpus: 347576_3084408 art. 1 pkt 30 — jednostka głębsza po wyliczeniu należy do ostatniego miejsca.
        [InlineData("1) w art. 69 i art. 70 ust. 1 wyrazy „art. 10” zastępuje się wyrazami „art. 11”;",
            "DU/2021/200__art_69", "DU/2021/200__art_70__ust_1")]
        public void SentenceAsAPlace_StaysWithTheUnitItFollows(string command, string first, string second)
        {
            Assert.Equal([first, second], Instructions(Export(command)).Select(i => i.Target));
        }

        [Theory]
        // Zdanie w każdej jednostce przedziału: para eId na zdaniach objęłaby tekst między nimi — instrukcja ręczna na przedziale.
        [InlineData("1) w art. 7–9 zdanie pierwsze otrzymuje brzmienie:", "„Opłatę wnosi się w terminie 14 dni.”;",
            "DU/2021/200__art_7", "DU/2021/200__art_9", "zastap-recznie")]
        [InlineData("1) w art. 5 w ust. 2–4 w zdaniu pierwszym wyrazy „14 dni” zastępuje się wyrazami „30 dni”;", null,
            "DU/2021/200__art_5__ust_2", "DU/2021/200__art_5__ust_4", "zastap-recznie")]
        [InlineData("1) w art. 5 w ust. 2–4 skreśla się zdanie drugie;", null,
            "DU/2021/200__art_5__ust_2", "DU/2021/200__art_5__ust_4", "uchyl-recznie")]
        public void SentenceInEveryUnitOfARange_IsAManualInstructionOnTheRange(
            string command, string? quote, string from, string to, string function)
        {
            var xml = Export(quote is null ? [command] : [command, quote]);

            Assert.Equal([(from, to, function)], Instructions(xml).Select(i => (i.Target, i.To, i.Function)));
        }

        [Fact]
        public void SentenceNumberedUnderTheProvisionNamingIt_IsTheTarget()
        {
            // Korpus: 322102_3051914 (Dz. U. 2025 poz. 1367 art. 1 pkt 10) — numer zdania podają litery pod „w zdaniu:".
            var xml = Export(
                "1) w art. 12 w ust. 1 w zdaniu:",
                "a) pierwszym wyraz „Przetarg” zastępuje się wyrazami „Przetarg ustny”,",
                "b) drugim wyraz „przetarg” zastępuje się wyrazami „przetarg ustny”;");

            Assert.Equal([("zamien-tekst", "DU/2021/200__art_12__ust_1__zdn_1"), ("zamien-tekst", "DU/2021/200__art_12__ust_1__zdn_2")],
                Instructions(xml).Select(i => (i.Name, i.Target)));
        }

        [Theory]
        // Zdanie nazwane wyżej bez numeru — adres zdania nieznany: instrukcja ręczna na jednostce (ADR-0102).
        [InlineData("1) w art. 12 w ust. 1 w zdaniu ostatnim:")]
        [InlineData("1) w art. 12 w ust. 1 w zdaniu:")]
        public void SentenceNamedAboveWithoutANumber_IsManual(string place)
        {
            var xml = Export(place, "a) wyraz „Przetarg” zastępuje się wyrazami „Przetarg ustny”;");

            Assert.Equal([("DU/2021/200__art_12__ust_1", "zastap-recznie")], Instructions(xml).Select(i => (i.Target, i.Function)));
        }

        // ============================================================
        // Część wspólna jako miejsce zmiany (ADR-0127)
        // ============================================================

        [Fact]
        public void SentenceInTheCommonPart_IsCountedInTheCommonPart()
        {
            // Korpus: 310239_2768075; odpowiedź autorów schematu na pytanie 7.
            var xml = Export("1) w art. 15i w ust. 1 w części wspólnej w zdaniu drugim po wyrazach „123 zł” dodaje się wyraz „rocznie”;");

            Assert.Equal([("zamien-tekst", "DU/2021/200__art_15i__ust_1__czw__zdn_2", null, null)], Instructions(xml));
        }

        [Fact]
        public void SentenceRepealedInTheCommonPart_IsTheSentenceOfTheCommonPart()
        {
            var xml = Export("1) w art. 5 w ust. 1 w części wspólnej skreśla się zdanie drugie;");

            Assert.Equal([("uchyl", "DU/2021/200__art_5__ust_1__czw__zdn_2", null, null)], Instructions(xml));
        }

        [Fact]
        public void SignInTheCommonPartBeforeAnAddedPoint_ChangesTheCommonPart_AndAddsAfterThePoint()
        {
            // Korpus: 309531 — „w pkt 24 w części wspólnej kropkę zastępuje się średnikiem i dodaje się pkt 25".
            var xml = Export("1) w art. 4 w pkt 24 w części wspólnej kropkę zastępuje się średnikiem i dodaje się pkt 25 w brzmieniu:",
                "„25) pojazd najmowany.”;");

            Assert.Equal([("zamien-tekst", "DU/2021/200__art_4__pkt_24__czw", null, null), ("cytat-strukt", "DU/2021/200__art_4__pkt_24", null, "dodaj-po")],
                Instructions(xml));
        }

        [Fact]
        public void PlaceInTheInheritedUnit_LiesInEveryUnitOfTheProvisionAbove()
        {
            var xml = Export(
                "1) w art. 14 w ust. 1 i 2:",
                "a) w części wspólnej wyrazy „M” zastępuje się wyrazami „N”,",
                "b) w zdaniu drugim wyrazy „O” zastępuje się wyrazami „P”;");

            Assert.Equal(["DU/2021/200__art_14__ust_1__czw", "DU/2021/200__art_14__ust_2__czw",
                "DU/2021/200__art_14__ust_1__zdn_2", "DU/2021/200__art_14__ust_2__zdn_2"], Instructions(xml).Select(i => i.Target));
        }

        [Fact]
        public void CommonPartAsTheObject_StaysOneCommonPart()
        {
            var xml = Export("1) w art. 9 w ust. 3 część wspólna otrzymuje brzmienie:", "„– wymierza się karę.”;");

            Assert.Equal([("cytat-strukt", "DU/2021/200__art_9__ust_3__czw", null, "zastap")], Instructions(xml));
            Assert.DoesNotContain(xml.Descendants().Attributes(), attribute => attribute.Value.Contains("czw__czw"));
        }

        [Fact]
        public void CommonPartOfAnAnnexUnit_StaysAManualPlaceOnTheUnit()
        {
            // Eksport nie zna części wspólnej z eId pod jednostką załącznika — instrukcja ręczna na jednostce (ADR-0100).
            var xml = Export("1) w załączniku nr 1 do ustawy w ust. 1 w pkt 4 w części wspólnej wyrazy „A” zastępuje się wyrazami „B”.");

            Assert.Equal([("DU/2021/200__zal_1__ust_1__pkt_4", "zastap-recznie")], Instructions(xml).Select(i => (i.Target, i.Function)));
        }

        [SchemaPlFact]
        public void SentenceTargets_AreSchemaValid()
        {
            var xml = Export(
                "1) w art. 5 w ust. 2 zdanie drugie otrzymuje brzmienie:", "„Opłatę pobiera się raz w roku.”;",
                "2) w art. 7a w ust. 7 dodaje się zdanie trzecie w brzmieniu:", "„Opłaty nie zwraca się.”;",
                "3) w art. 132 w ust. 1 zdanie drugie i trzecie otrzymują brzmienie:", "„Opłatę pobiera się raz w roku. Opłaty nie zwraca się.”;",
                "4) w art. 7 i 8 zdanie pierwsze otrzymuje brzmienie:", "„Opłatę wnosi się w terminie 14 dni.”;",
                "5) w art. 9–11 zdanie pierwsze otrzymuje brzmienie:", "„Opłatę wnosi się w terminie 14 dni.”;",
                "6) w art. 12 w ust. 1 w zdaniu:",
                "a) pierwszym wyraz „Przetarg” zastępuje się wyrazami „Przetarg ustny”,",
                "b) drugim wyraz „przetarg” zastępuje się wyrazami „przetarg ustny”.");

            Assert.Empty(SchemaPl.Validator.ValidateActBody(xml.Elements().First()));
        }
    }
}
