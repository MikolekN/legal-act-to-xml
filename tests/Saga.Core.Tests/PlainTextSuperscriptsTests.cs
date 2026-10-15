using System.Text;
using Saga.Core.Ingest;
using Saga.Core.Services;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Indeks górny zwykłego tekstu na kanale „[…]": znaki indeksu Unicode i znaczniki odnośników przyklejone do słowa.
    /// </summary>
    public class PlainTextSuperscriptsTests
    {
        private static IReadOnlyList<string> Read(params string[] lines)
        {
            using var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(string.Join("\n", lines)));
            return new PlainTextBlockReader().ReadBlocks(stream).Select(block => block.Text).ToList();
        }

        [Fact]
        public void UnicodeSuperscripts_BecomeTheBracketChannel()
        {
            Assert.Equal(
                ["Art. 29[2]. Treść.", "o zmianie niektórych ustaw[1)] [2)]", "powierzchnia 1 m[2] lokalu"],
                Read("Art. 29². Treść.", "o zmianie niektórych ustaw¹⁾ ²⁾", "powierzchnia 1 m² lokalu"));
        }

        [Fact]
        public void ExplicitSuperscript_ContinuesTheNumberingSequence()
        {
            Assert.Equal(
                ["Art. 29. A.", "Art. 29[1]. B.", "Art. 29[2]. C.", "Art. 30. D."],
                Read("Art. 29. A.", "Art. 29¹. B.", "Art. 292. C.", "Art. 30. D."));
        }

        [Fact]
        public void FootnoteMarkersGluedToAWord_BecomeMarkers()
        {
            Assert.Equal(
                ["o zmianie ustawy o podatku oraz niektórych innych ustaw[1), 2)]",
                 "Art. 1. W ustawie (Dz. U. z 2024 r. poz. 361, z późn. zm.[2)]) wprowadza się zmiany:"],
                Read("o zmianie ustawy o podatku oraz niektórych innych ustaw1), 2)",
                    "Art. 1. W ustawie (Dz. U. z 2024 r. poz. 361, z późn. zm.2)) wprowadza się zmiany:"));
        }

        [Fact]
        public void MarkerAfterAnUppercaseWord_AndNonBreakingHyphen_AreWrittenAsInDocx()
        {
            Assert.Equal(
                ["MINISTRA SPRAW WEWNĘTRZNYCH I ADMINISTRACJI[1)]", "o społeczno-zawodowych organizacjach rolników"],
                Read("MINISTRA SPRAW WEWNĘTRZNYCH I ADMINISTRACJI1)", "o społeczno\u2011zawodowych organizacjach rolników"));
        }

        [Fact]
        public void FootnoteAnchorAtADesignation_BecomesAMarker_OrGoesWhenItHasNoNumber()
        {
            // Tekst jednolity: odnośnik przy oznaczeniu zmienionej jednostki; eksport tekstu klei jego numer
            // do oznaczenia albo zapisuje sam nawias.
            Assert.Equal(
                ["44b)[10)] przydatność do stosowania;", "1d.[22)] Podmiot pobiera opłaty.", "Art. 13mb.[44)] 1. Krajowymi podmiotami są:",
                 "Art. 5. 1.[22)] Treść.", "5. Podmiot składa wniosek.", "Art. 13ja. 1. Podmiot świadczy usługę.", "§ 4. Przepisów nie stosuje się."],
                Read("44b)10) przydatność do stosowania;", "1d.22) Podmiot pobiera opłaty.", "Art. 13mb.44) 1. Krajowymi podmiotami są:",
                    "Art. 5. 1.22) Treść.", "5.) Podmiot składa wniosek.", "Art. 13ja.) 1. Podmiot świadczy usługę.", "§ 4.) Przepisów nie stosuje się."));
        }

        [Fact]
        public void NumbersThatAreText_StayAsTheyAre()
        {
            // Jednostka miary z wykładnikiem w nawiasie, punkt wyliczenia, odesłanie — nie znaczniki.
            var lines = new[] { "objętość (w dm3) zbiornika", "emisja (CO2) spalin", "1) pierwszy punkt;", "(art. 5 ust. 1 pkt 2) oraz", "w latach 2024–2025)" };

            Assert.Equal(lines, Read(lines));
        }

        [Fact]
        public void TitleWithGluedMarkers_IsExportedWithoutThem()
        {
            var lines = new[] { "Ustawa", "z dnia 1 marca 2024 r.", "o zmianie niektórych ustaw1), 2)", "Art. 1. Treść.", "Art. 2. Treść." };
            using var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(string.Join("\n", lines)));
            var document = LegalDocumentParser.Parse(stream, "akt.txt", new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

            var title = LegalDocumentConverter.Convert(document).Descendants().First(e => e.Name.LocalName == "Tytul").Value;

            Assert.Equal("Ustawa z dnia 1 marca 2024 r. o zmianie niektórych ustaw", title);
        }
    }
}
