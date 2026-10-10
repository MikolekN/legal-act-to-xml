using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Jednostka bez <c>eId</c> nazywa powód jego braku (<c>brak-eid</c>) — schemat traktuje brak adresu
    /// jako decyzję, nie przeoczenie. Dotyczy jedynego ustępu artykułu i jednostki, której oznaczenia
    /// nie odczytano: ta nie wnosi segmentu do adresu, a jej dzieci adresują się względem przodka (ADR-0024).
    /// </summary>
    public class MissingEIdReasonTests
    {
        private static XElement Export(params string[] lines) => LegalDocumentConverter.Convert(
            LegalDocumentParser.ParseBlocks(lines.Select(text => new DocumentBlock { Text = text }).ToList()));

        private static List<XElement> Named(XElement xml, string name) =>
            xml.Descendants().Where(e => e.Name.LocalName == name).ToList();

        private static string? Attr(XElement element, string name) => element.Attribute(name)?.Value;

        [Fact]
        public void OnlyParagraphOfAnArticle_NamesTheReason()
        {
            // Jak w przykładach autorów schematu: <ustep-n brak-eid="jednostka-nieoznaczona">.
            var xml = Export(
                "Art. 1. Ustawa określa zasady ochrony sygnalistów.",
                "Art. 2. 1. Przepis z ustępami.",
                "2. Drugi ustęp.");

            var only = Assert.Single(Named(xml, "ustep-n"));
            Assert.Null(Attr(only, "eId"));
            Assert.Equal("jednostka-nieoznaczona", Attr(only, "brak-eid"));

            // Jednostka z adresem powodu nie niesie — atrybuty wykluczają się.
            Assert.All(Named(xml, "ustep"), unit =>
            {
                Assert.NotNull(Attr(unit, "eId"));
                Assert.Null(Attr(unit, "brak-eid"));
            });
        }

        // Akapit w stylu ustępu, ale bez numeru — legenda wzoru, sankcja w osobnym wierszu.
        private static XElement ExportWithUnnumberedParagraph() => LegalDocumentConverter.Convert(
            LegalDocumentParser.ParseBlocks(
            [
                new() { Text = "Art. 1. 1. Opłatę oblicza się według wzoru." },
                new() { Text = "w którym poszczególne symbole oznaczają:", StyleId = "UST" },
                new() { Text = "1) K - kwotę pożyczki;" },
                new() { Text = "2) R - liczbę dni w roku." },
                new() { Text = "2. Opłaty nie pobiera się od osób fizycznych." },
            ]));

        [Fact]
        public void UnitWithoutADesignation_HasNoEId_AndNamesTheReason()
        {
            var xml = ExportWithUnnumberedParagraph();
            var paragraphs = Named(xml, "artykul").Single().Elements().Where(e => e.Name.LocalName != "nr").ToList();

            Assert.Equal(new[] { "ustep", "nie-ztp-ustep", "ustep" }, paragraphs.Select(e => e.Name.LocalName));

            var unnumbered = paragraphs[1];
            Assert.Null(Attr(unnumbered, "eId"));
            Assert.Equal("jednostka-nieoznaczona", Attr(unnumbered, "brak-eid"));
            Assert.Equal(string.Empty, unnumbered.Elements().First(e => e.Name.LocalName == "nr").Value);
            Assert.StartsWith("w którym poszczególne symbole", unnumbered.Elements().First(e => e.Name.LocalName == "p").Value);

            Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName == "nr" && e.Value == "UNKNOWN");
        }

        [Fact]
        public void UnitsBelowIt_AreAddressedFromTheNearestDesignatedAncestor()
        {
            var xml = ExportWithUnnumberedParagraph();

            // Bez pustego segmentu „ust": adres punktu to adres artykułu i jego własne oznaczenie.
            Assert.Equal(new[] { "art_1__pkt_1", "art_1__pkt_2" },
                Named(xml, "nie-ztp-punkt").Select(point => Attr(point, "eId")));
            Assert.All(xml.Descendants().Select(e => Attr(e, "eId")).Where(eId => eId is not null),
                eId => Assert.DoesNotMatch(@"(^|__)[a-z]+(__|$)", eId!));
        }

        [Fact]
        public void UnnumberedParagraphBesideCodeParagraphs_IsOneOfThem()
        {
            // Kodeks cywilny, art. 263 w tekście jednolitym: pierwszy akapit bez „§ 1.", dalej „§ 2. (uchylony)".
            var xml = Export(
                "Art. 263. Roszczenie właściciela przeciwko użytkownikowi przedawnia się z upływem roku.",
                "§ 2. (uchylony)");

            var paragraphs = Named(xml, "artykul").Single().Elements().Where(e => e.Name.LocalName != "nr").ToList();

            Assert.Equal(new[] { "nie-ztp-paragraf-kodeksu", "paragraf-kodeksu" }, paragraphs.Select(e => e.Name.LocalName));
            Assert.Equal("jednostka-nieoznaczona", Attr(paragraphs[0], "brak-eid"));
            Assert.Equal("art_263__par_2", Attr(paragraphs[1], "eId"));
        }

        [SchemaPlFact]
        public void UnitsWithoutADesignation_AreSchemaValid()
        {
            Assert.Empty(SchemaPl.Validator.ValidateActBody(ExportWithUnnumberedParagraph().Elements().First()));

            var code = Export(
                "Rozdział 1",
                "Użytkowanie",
                "Art. 263. Roszczenie właściciela przeciwko użytkownikowi przedawnia się z upływem roku.",
                "§ 2. (uchylony)");
            Assert.Empty(SchemaPl.Validator.ValidateActBody(code.Elements().First()));
        }
    }
}
