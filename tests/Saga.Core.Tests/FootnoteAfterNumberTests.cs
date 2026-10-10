using Saga.Core.Ingest;
using Saga.Core.Services.Classify;
using Saga.Core.Services.Parsing;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Znacznik odnośnika tuż za numerem jednostki — codzienność tekstów jednolitych („2.[9)] Do wniosku…").
    /// Numer kończy się na kropce, a znacznik zostaje na początku treści, jak każdy inny odnośnik.
    /// </summary>
    public class FootnoteAfterNumberTests
    {
        private static LegalDocument Parse(params string[] lines) =>
            LegalDocumentParser.ParseBlocks(lines.Select(text => new DocumentBlock { Text = text }).ToList());

        [Theory]
        // TJ 275050_2461922, 307519_2779687 i Kodeks cywilny (TJ 291207_2617005).
        [InlineData("2.[9)] Do wniosku powinna być dołączona mapa.", "2", "[9)] Do wniosku powinna być dołączona mapa.")]
        [InlineData("2a.[6)] Nie wymaga uzyskania zgody przeznaczenie gruntów.", "2a", "[6)] Nie wymaga uzyskania zgody przeznaczenie gruntów.")]
        [InlineData("§ 4.[9)] Przepisów § 1 i 3 nie stosuje się.", "4", "[9)] Przepisów § 1 i 3 nie stosuje się.")]
        [InlineData("5.[4)]Podmiot składa wniosek.", "5", "[4)]Podmiot składa wniosek.")]
        public void MarkerRightAfterTheDot_DoesNotHideTheNumber(string text, string number, string content)
        {
            Assert.Equal(ParagraphKind.Paragraph, new ParagraphClassifier().Classify(new ClassificationInput(text, null)).Kind);
            Assert.Equal(number, ParsingFactories.ParseParagraphNumber(text)?.Value);
            Assert.Equal(content, ParsingFactories.StripParagraphPrefix(text));
        }

        [Fact]
        public void NumberedSentenceWithoutSpace_IsStillNotAParagraph()
        {
            // Bez odstępu i bez znacznika to nie numer ustępu („2.5 mln", „3.Tekst").
            Assert.Null(ParsingFactories.ParseParagraphNumber("2.5 mln zł przeznacza się na cele ustawy."));
            Assert.Null(ParsingFactories.ParseParagraphNumber("3.[1] Tekst z indeksem, nie odnośnikiem."));
        }

        [Fact]
        public void MarkerAfterTheArticleDesignation_LeavesTheFirstParagraphNumbered()
        {
            var document = Parse(
                "Art. 13ja.[36)] 1. Podmiot świadczący usługę poboru opłat składa wniosek.",
                "2.[37)] Wniosek zawiera dane podmiotu.",
                "Art. 448.[8)] § 1. W razie naruszenia dobra osobistego sąd może przyznać zadośćuczynienie.",
                "§ 2.[9)] Przepis stosuje się odpowiednio.",
                "Art. 1.[2)] Ustawa określa środki ochrony.");

            var articles = document.Articles.ToDictionary(a => a.Number!.Value);

            var first = articles["13ja"].Paragraphs;
            Assert.Equal(new[] { "art_13ja__ust_1", "art_13ja__ust_2" }, first.Select(p => p.Id));
            Assert.Equal("[36)] Podmiot świadczący usługę poboru opłat składa wniosek.", first[0].ContentText);
            Assert.Equal("[37)] Wniosek zawiera dane podmiotu.", first[1].ContentText);

            Assert.Equal(new[] { "art_448__par_1", "art_448__par_2" }, articles["448"].Paragraphs.Select(p => p.Id));

            // Artykuł bez ustępów: znacznik zostaje w treści ustępu niejawnego.
            var single = Assert.Single(articles["1"].Paragraphs);
            Assert.True(single.IsImplicit);
            Assert.Equal("[2)] Ustawa określa środki ochrony.", single.ContentText);
        }
    }
}
