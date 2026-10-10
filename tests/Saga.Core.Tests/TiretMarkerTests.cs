using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Core.Services.Classify;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Znacznik tiretu jako źródło głębokości: zagnieżdżenie stoi w treści („– –"), więc nie wymaga stylu
    /// ani wcięcia, a znacznik schodzi z treści w całości.
    /// </summary>
    public class TiretMarkerTests
    {
        private static LegalDocument Parse(params string[] lines) =>
            LegalDocumentParser.ParseBlocks(lines.Select(line => new DocumentBlock { Text = line }).ToList());

        [Theory]
        [InlineData("– krajowych", 1)]
        [InlineData("- krajowych", 1)]
        [InlineData("–krajowych", 1)]
        [InlineData("– – centralnych", 2)]
        [InlineData("– – – gminnych", 3)]
        [InlineData("„– – centralnych", 2)]
        [InlineData("– -5 °C", 1)]
        [InlineData("krajowych", 0)]
        public void Depth_IsTheNumberOfDashesOfTheMarker(string text, int expected)
        {
            Assert.Equal(expected, TiretMarker.Depth(text));
        }

        [Theory]
        [InlineData("– krajowych", "krajowych")]
        [InlineData("– – centralnych,", "centralnych,")]
        [InlineData("– –\tcentralnych,", "centralnych,")]
        [InlineData("– – – gminnych", "gminnych")]
        [InlineData("– -5 °C", "-5 °C")]
        public void Strip_RemovesTheWholeMarker(string text, string expected)
        {
            Assert.Equal(expected, TiretMarker.Strip(text));
        }

        [Fact]
        public void DoubleTiret_IsNestedByItsMarker_WithoutStylesOrLayout()
        {
            var document = Parse(
                "Art. 1. Przepisy stosuje się do:",
                "1) podmiotów:",
                "a) publicznych:",
                "– krajowych:",
                "– – centralnych,",
                "– – terenowych,",
                "– zagranicznych,",
                "b) prywatnych;",
                "2) osób fizycznych.");

            var letter = document.Articles.First().Paragraphs.Single().Points[0].Letters[0];

            Assert.Equal(2, letter.Tirets.Count);
            Assert.Equal(new[] { "centralnych,", "terenowych," }, letter.Tirets[0].Tirets.Select(t => t.ContentText));
            Assert.Empty(letter.Tirets[1].Tirets);

            var xml = LegalDocumentConverter.Convert(document);
            var nested = xml.Descendants().Where(e => e.Name.LocalName == "podw-tiret").ToList();
            Assert.Equal(
                new[] { "art_1__pkt_1__lit_a__tir_1__dwtir_1", "art_1__pkt_1__lit_a__tir_1__dwtir_2" },
                nested.Select(e => e.Attribute("eId")!.Value));
        }

        [Fact]
        public void QuotedDoubleTiret_IsNestedByItsMarker()
        {
            var document = Parse(
                "Art. 1. W ustawie z dnia 5 lipca 2018 r. o krajowym systemie cyberbezpieczeństwa (Dz. U. z 2026 r. poz. 20) w art. 5 pkt 3 otrzymuje brzmienie:",
                "„3) podmiotów:",
                "a) publicznych:",
                "– krajowych:",
                "– – centralnych,",
                "– – terenowych,",
                "– zagranicznych,",
                "b) prywatnych;”.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

            var quoted = document.Articles.First().Paragraphs.Single().Amendment!.Content!.Points.Single();
            var letter = quoted.Letters[0];

            Assert.Equal(new[] { "krajowych:", "zagranicznych," }, letter.Tirets.Select(t => t.ContentText));
            Assert.Equal(new[] { "centralnych,", "terenowych," }, letter.Tirets[0].Tirets.Select(t => t.ContentText));

            var xml = LegalDocumentConverter.Convert(document);
            Assert.Equal(
                new[] { "DU/2026/20__art_5__pkt_3__lit_a__tir_1__dwtir_1", "DU/2026/20__art_5__pkt_3__lit_a__tir_1__dwtir_2" },
                xml.Descendants().Where(e => e.Name.LocalName == "podw-tiret").Select(e => e.Attribute("eId")!.Value));
        }
    }
}
