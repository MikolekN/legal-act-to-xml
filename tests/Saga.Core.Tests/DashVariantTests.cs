using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Core.Services.Classify;
using Saga.Core.Services.UnitReferences;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Warianty myślnika z jednego źródła (ADR-0040): tiret, część wspólna, przedział oznaczeń i legenda wzoru
    /// rozpoznają każdy myślnik, którym piszą szczotki — nie tylko dywiz i półpauzę. Znaki zapisane kodami.
    /// </summary>
    public class DashVariantTests
    {
        public static TheoryData<string> Variants() =>
            new(Dashes.Characters.Select(dash => dash.ToString()));

        private static LegalDocument Parse(params string[] lines) =>
            LegalDocumentParser.ParseBlocks(lines.Select(text => new DocumentBlock { Text = text }).ToList());

        [Theory]
        [MemberData(nameof(Variants))]
        public void EveryDash_OpensATiret_AndIsTakenOffItsText(string dash)
        {
            Assert.True(ParagraphClassifier.IsTiretByText($"{dash} treść tiretu,"));
            Assert.Equal(1, TiretMarker.Depth($"{dash} treść tiretu,"));
            Assert.Equal(2, TiretMarker.Depth($"{dash} {dash} treść podwójnego tiretu,"));
            Assert.Equal("treść tiretu,", TiretMarker.Strip($"{dash} treść tiretu,"));
        }

        [Fact]
        public void TiretWithAFootnoteMarkerRightAfterTheDash_IsATiret()
        {
            // Korpus: 336403_3088135 — „–⁽*⁾ 12 miesięcy dla DME …": odnośnik o brzmieniu tiretu stoi zaraz za myślnikiem.
            var document = Parse(
                "Art. 1. 1. Warunkiem eksploatacji jest:",
                "1) wykonywanie kontroli:",
                "a) okresowych, wykonywanych nie rzadziej niż co:",
                "– 6 miesięcy dla ILS,",
                "–[2)] 12 miesięcy dla DME,",
                "b) doraźnych.");

            var letters = document.Articles.Single().Paragraphs.Single().Points.Single().Letters;
            Assert.Equal(new[] { "a", "b" }, letters.Select(letter => letter.Number?.Value));
            Assert.Equal(new[] { "6 miesięcy dla ILS,", "[2)] 12 miesięcy dla DME," }, letters[0].Tirets.Select(tiret => tiret.ContentText));
        }

        [Fact]
        public void TiretWrittenWithAFigureDash_IsRecognisedWithoutAStyle()
        {
            // Korpus: 323280_2941714 — tirety pisane „\u2012" (U+2012) wychodziły jako dalsza treść litery.
            var document = Parse(
                "Art. 1. 1. Zwolnienie stosuje się do:",
                "1) wyrobów:",
                "a) przeznaczonych do celów:",
                "\u2012 żeglugi,",
                "\u2012 rybołówstwa;");

            var letter = document.Articles.Single().Paragraphs.Single().Points.Single().Letters.Single();
            Assert.Equal(new[] { "żeglugi,", "rybołówstwa;" }, letter.Tirets.Select(tiret => tiret.ContentText));
        }

        [Fact]
        public void ClosingCommonPartWrittenWithAFigureDash_IsACommonPart()
        {
            // Korpus: 277432_2495860 — część wspólna zaczynająca się od „\u2012" ginęła z eksportu.
            var document = Parse(
                "Art. 1. 1. Droga jest drogą publiczną, jeżeli:",
                "1) została zaliczona do kategorii dróg,",
                "2) jest ogólnodostępna",
                "\u2012 i nie jest drogą wewnętrzną.");

            var paragraph = document.Articles.Single().Paragraphs.Single();
            Assert.Equal(2, paragraph.Points.Count);
            Assert.Contains(paragraph.CommonParts, part => part.Type == CommonPartType.WrapUp && part.ContentText.Contains("i nie jest drogą wewnętrzną."));
        }

        [Theory]
        [InlineData("art. 1\u20123", "art_1–art_3")]
        [InlineData("art. 1\u20143", "art_1–art_3")]
        [InlineData("art. 1-3", "art_1–art_3")]
        public void RangeOfDesignations_AcceptsEveryDash(string text, string expected)
        {
            var address = Assert.Single(UnitAddressReader.Read(text));

            Assert.Equal(expected, $"{address.Start}–{address.End}");
        }
    }
}
