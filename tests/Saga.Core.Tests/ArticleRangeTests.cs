using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Zakres oznaczeń w tekście jednolitym („Art. 39–51. (pominięte)") jest jedną jednostką: <c>nr</c> zostaje pusty,
    /// granice niosą <c>nr-od</c> i <c>nr-do</c>, a adres ma pierwsza jednostka zakresu (ADR-0097).
    /// </summary>
    public class ArticleRangeTests
    {
        private static LegalDocument Parse(params string[] lines) =>
            LegalDocumentParser.ParseBlocks(lines.Select(text => new DocumentBlock { Text = text }).ToList());

        private static XElement Export(params string[] lines) => LegalDocumentConverter.Convert(Parse(lines));

        private static XElement ArticleNamed(XElement xml, string eId) =>
            xml.Descendants().Single(e => e.Name.LocalName == "artykul" && e.Attribute("eId")?.Value.Split("__")[^1] == eId);

        private static readonly string[] ConsolidatedText =
        [
            "Art. 200. Przepis obowiązujący.",
            "Art. 201–225. (pominięte)",
            "Art. 226. Ustawa wchodzi w życie z dniem 1 stycznia 2025 r.",
        ];

        [Fact]
        public void RangeOfArticles_IsOneArticle_WithBothEnds()
        {
            // Korpus: 275043_2474596 (Dz. U. 2024 poz. 177) — „Art. 201–225. (pominięte)".
            var range = Parse(ConsolidatedText).Articles.Single(article => article.Number?.Value == "201");

            Assert.Equal("225", range.NumberTo?.Value);
            Assert.Equal("(pominięte)", Assert.Single(range.Paragraphs).ContentText);
        }

        [Fact]
        public void RangeOfArticles_IsExportedWithAnEmptyNumber_AndTheAddressOfItsFirstUnit()
        {
            var range = ArticleNamed(Export(ConsolidatedText), "art_201");

            Assert.Equal(new[] { "nr", "nr-od", "nr-do" },
                range.Elements().Take(3).Select(element => element.Name.LocalName));
            Assert.Equal(new[] { "", "201", "225" }, range.Elements().Take(3).Select(element => element.Value));
            Assert.Equal("(pominięte)", range.Descendants().First(element => element.Name.LocalName == "zdanie").Value);
        }

        [Fact]
        public void ArticleWithOneDesignation_HasOnlyItsNumber()
        {
            var article = ArticleNamed(Export(ConsolidatedText), "art_226");

            Assert.Equal("226", article.Elements().First(element => element.Name.LocalName == "nr").Value);
            Assert.DoesNotContain(article.Elements(), element => element.Name.LocalName is "nr-od" or "nr-do");
        }

        [Theory]
        // Korpus: litera w końcu zakresu („Art. 37b–37ag. (uchylone)") i odnośnik tuż za kropką zakresu
        // (317037_2830281 — „Art. 266–280.[1)]" z objaśnieniem w odnośniku zamiast adnotacji).
        [InlineData("Art. 37b–37ag. (uchylone)", "37b", "37ag", "(uchylone)")]
        [InlineData("Art. 266–280.[1)]", "266", "280", "[1)]")]
        [InlineData("Art. 48‒50. (pominięte)", "48", "50", "(pominięte)")]
        [InlineData("Art. 41a–Art. 41i. (uchylone)", "41a", "41i", "(uchylone)")]
        public void RangeEnd_IsReadFromTheDesignation(string line, string first, string last, string content)
        {
            var range = Parse("Art. 1. Przepis.", line).Articles.Last();

            Assert.Equal(first, range.Number?.Value);
            Assert.Equal(last, range.NumberTo?.Value);
            Assert.Equal(content, range.Paragraphs.Single().ContentText);
        }

        [Theory]
        // Myślnik po kropce oznaczenia to treść artykułu, a koniec mniejszy niż początek — nie zakres.
        [InlineData("Art. 5. – 7 dni od dnia doręczenia.")]
        [InlineData("Art. 15–7. (pominięte)")]
        public void DashAfterTheDesignation_WithoutARangeEnd_IsContent(string line)
        {
            var article = Parse("Art. 1. Przepis.", line).Articles.Last();

            Assert.Null(article.NumberTo);
        }
    }
}
