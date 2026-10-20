using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Core.Services.UnitReferences;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Komenda w załączniku aktu zmienianego (ADR-0080): „w załączniku nr 1 …" to miejsce nad całą treścią aktu,
    /// a jednostki pod nim są jednostkami załącznika (zal_1__ust_2__pkt_57) — nie jednostkami aktu.
    /// </summary>
    public class AnnexCommandTargetTests
    {
        private static string[] Read(string text, StructuralReference? context = null) =>
            UnitAddressReader.Read(text, context).Select(address => address.Start.ToString()).ToArray();

        [Fact]
        public void UnitsAfterTheAnnex_AreUnitsOfTheAnnex()
        {
            // Korpus: 303357_3003029.
            Assert.Equal(["zal_1__ust_2__pkt_57"], Read("w załączniku nr 1 w ust. 2 po pkt 57 dodaje się pkt 57a w brzmieniu:"));
            Assert.Equal(["zal_2"], Read("w załączniku nr 2 do rozporządzenia:"));
            Assert.Equal(["zal_3"], Read("W rozporządzeniu Ministra Zdrowia z dnia 5 maja 2023 r. (Dz. U. poz. 79) w załączniku nr 3 wprowadza się następujące zmiany:"));
        }

        [Fact]
        public void PathUnderAnAnnex_ContinuesInTheAnnex()
        {
            var annex = new StructuralReference { Annex = "1" };

            Assert.Equal(["zal_1__pkt_4"], Read("po pkt 4 dodaje się pkt 4a w brzmieniu:", annex));

            // Podział treści załącznika („w części 1") i jego numeracja wielopoziomowa („pkt 1.4.1") nie są jednostkami
            // aktu: adresem zostaje załącznik, a przepisy pod nim dziedziczą miejsce w jego treści — jednostki pod nim
            // nie są jednostkami załącznika.
            var part = UnitAddressReader.Read("w części 1 „Warunki techniczne”:", annex).Single().Start;
            Assert.Equal("zal_1", part.ToString());
            Assert.True(part.InAnnexContent);
            Assert.Equal(["zal_1"], Read("po pkt 4 dodaje się pkt 4a w brzmieniu:", part));
            Assert.Equal(["zal_1"], Read("w pkt 1.4.1 w akapicie pierwszym zdanie pierwsze otrzymuje brzmienie:", annex));
        }

        [Fact]
        public void LocationInsideTheAnnexContent_StopsTheAddressAtTheDeepestKnownUnit()
        {
            // Korpus: 348987_3152607 — załącznik z paragrafami; paragrafu aktu w załączniku nie ma, więc „ust. 2 pkt 9"
            // nie jest ustępem załącznika.
            Assert.Equal(["zal_36"], Read("w § 10 w ust. 2 pkt 9 otrzymuje brzmienie:", new StructuralReference { Annex = "36" }));

            // Korpus: 310431_2770522 — zdanie w objaśnieniach wzoru ustępu załącznika: adresem jest ten ustęp.
            Assert.Equal(["zal_2__ust_6"], Read("w ust. 6 w objaśnieniach do wzoru dodaje się zdanie trzecie w brzmieniu:",
                new StructuralReference { Annex = "2" }));
            Assert.Equal(["zal_6"], Read("w tabeli nr 1:", new StructuralReference { Annex = "6" }));
        }

        [Fact]
        public void AnnexWithoutNumber_IsTheOnlyAnnex_AddressedAsTheFirst()
        {
            // Komenda zakłada, że załącznik jest jedyny, więc i pierwszy — autorzy schematu, 2026-10-18 (ADR-0099).
            Assert.Equal(["zal_1__pkt_4"], Read("w załączniku do rozporządzenia po pkt 4 dodaje się pkt 4a w brzmieniu:"));
        }

        [Theory]
        // Wzmianka o załączniku nie jest miejscem komendy.
        [InlineData("w art. 5 ust. 2 wyrazy, o których mowa w załączniku nr 1, skreśla się", "art_5__ust_2")]
        [InlineData("w art. 5 w ust. 2 dodaje się pkt 3 w brzmieniu określonym w załączniku nr 2", "art_5__ust_2")]
        // Załącznik aktu zmieniającego.
        [InlineData("w art. 5 ust. 2 otrzymuje brzmienie w załączniku do niniejszej ustawy", "art_5__ust_2")]
        public void MentionOfAnAnnex_IsNotTheLocationOfTheCommand(string text, string expected)
        {
            Assert.Equal([expected], Read(text));
        }

        [Theory]
        [InlineData("pkt 1.4.1")]
        [InlineData("lp. 1.54")]
        [InlineData("pkt 12.5")]
        [InlineData("pkt B.W15")]
        public void MultiLevelNumbering_IsNotANumericDesignation(string text)
        {
            Assert.DoesNotContain(UnitReferenceTokenizer.Tokenize(text),
                token => token.Kind == UnitTokenKind.Designation && char.IsDigit(token.Text[0]));
        }

        // ============================================================
        // Nowelizacja i eksport
        // ============================================================

        private static XElement Export(params string[] provisions)
        {
            string[] lines =
            [
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o zmianie ustawy o opłatach",
                "Art. 1. W ustawie z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) wprowadza się następujące zmiany:",
                .. provisions,
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
            ];
            var document = LegalDocumentParser.Parse(
                lines.Select(line => new DocumentBlock { Text = line }).ToList(),
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
            return LegalDocumentConverter.Convert(document);
        }

        private static string?[] Targets(XElement xml) => xml.Descendants()
            .Select(e => e.Attributes().FirstOrDefault(a => a.Name.LocalName == "element-eId")?.Value)
            .OfType<string>().ToArray();

        [Fact]
        public void UnitAddedInAnAnnex_IsAddressedInThatAnnex()
        {
            var xml = Export(
                "1) w załączniku nr 1 do ustawy po pkt 4 dodaje się pkt 4a w brzmieniu:",
                "„4a) jezioro Gopło;”.");

            Assert.Equal(["DU/2021/200__zal_1__pkt_4"], Targets(xml));
            Assert.Contains(xml.Descendants(), e => e.Name.LocalName == "punkt" && e.Attribute("eId")?.Value == "DU/2021/200__zal_1__pkt_4a");
        }

        [Fact]
        public void UnitsEnumeratedInsideTheAnnexContent_StaySeparateTargets()
        {
            // „uchyla się pkt 1 i 2" w tabeli załącznika — dwa cele o tym samym adresie, nie jeden.
            var targets = UnitAddressReader.Read("w tabeli nr 1 uchyla się pkt 1 i 2", new StructuralReference { Annex = "3" });

            Assert.Equal(["zal_3", "zal_3"], targets.Select(target => target.Start.ToString()));
            Assert.Equal(["1", "2"], targets.Select(target => target.Start.Point));

            // Korpus: 347307_3175441 — „§ 107 i § 108" regulaminu stanowiącego załącznik: dwa cele.
            Assert.Equal(2, UnitAddressReader.Read("§ 107 i § 108 otrzymują brzmienie:", new StructuralReference { Annex = "1" }, sectionSignIsBasicUnit: true).Count);
        }

        [Fact]
        public void WordsChangedInAUnitOfTheAnnex_AreAddressedToThatUnit()
        {
            // Korpus: 301530_3044668.
            var xml = Export(
                "1) w załączniku nr 1 do ustawy:",
                "a) w pkt 5 po wyrazach „zakładów górniczych” dodaje się wyrazy „oraz zakładów”,",
                "b) w pkt 6 w części wspólnej skreśla się wyraz „odtworzone”.");

            Assert.Equal(["DU/2021/200__zal_1__pkt_5", "DU/2021/200__zal_1__pkt_6"], Targets(xml));
        }

        [Fact]
        public void UnitReplacedInsideTheAnnexContent_HasNoAddress()
        {
            // Punkt tabeli załącznika — adres załącznika objąłby cały załącznik.
            var xml = Export(
                "1) w załączniku nr 1 do ustawy w tabeli nr 2 pkt 3 otrzymuje brzmienie:",
                "„3) jezioro Gopło;”.");

            Assert.Equal(["UNKNOWN"], Targets(xml));
        }

        [Fact]
        public void TextChangedInsideAnAnnex_IsAddressedToTheAnnex_ButARepealThereIsNot()
        {
            var xml = Export(
                "1) w załączniku nr 1 do ustawy w pkt 1.4.1 zdanie pierwsze otrzymuje brzmienie:",
                "„Napisy wykonuje się literami.”;",
                "2) w załączniku nr 2 do ustawy skreśla się lp. 7.");

            // Zastąpienie tekstu w załączniku — adresem jest załącznik; uchylenie wiersza, którego adresu nie ma,
            // nie wskazuje całego załącznika.
            Assert.Equal(["DU/2021/200__zal_1", "UNKNOWN"], Targets(xml));
        }

        [SchemaPlFact]
        public void CommandsInAnAnnex_AreSchemaValid()
        {
            var xml = Export(
                "1) w załączniku nr 1 do ustawy po pkt 4 dodaje się pkt 4a w brzmieniu:",
                "„4a) jezioro Gopło;”,",
                "2) w załączniku nr 2 do ustawy w pkt 1.4.1 zdanie pierwsze otrzymuje brzmienie:",
                "„Napisy wykonuje się literami.”.");

            Assert.Empty(SchemaPl.Validator.ValidateActBody(xml.Elements().First()));
        }
    }
}
