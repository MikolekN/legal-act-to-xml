using System.Xml.Linq;
using DocumentFormat.OpenXml.Wordprocessing;
using Saga.Core.Helpers;
using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Wzór w osobnym akapicie (ADR-0038): akapit złożony z samego równania jest dalszą treścią jednostki,
    /// za którą stoi, z wyrażeniem w MathML obok zapisu liniowego; eksport pisze go jako <c>wzor-mat</c>.
    /// </summary>
    public class FormulaTests
    {
        private const string Namespaces =
            "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" " +
            "xmlns:m=\"http://schemas.openxmlformats.org/officeDocument/2006/math\"";

        private const string MathNamespace = "http://www.w3.org/1998/Math/MathML";

        private static Paragraph Word(string content) => new($"<w:p {Namespaces}>{content}</w:p>");

        private static string Equation(string text) => $"<m:oMath><m:r><m:t>{text}</m:t></m:r></m:oMath>";

        private static string Run(string text) => $"<w:r><w:t xml:space=\"preserve\">{text}</w:t></w:r>";

        private static DocumentBlock Text(string text, string? style = null) => new() { Text = text, StyleId = style };

        /// <summary>Blok-wzór, jaki oddaje czytnik DOCX: zapis liniowy i MathML.</summary>
        private static DocumentBlock Formula(string linear, string? style = null) => new()
        {
            Text = linear,
            StyleId = style,
            FormulaMathMl = $"<math xmlns=\"{MathNamespace}\" display=\"block\"><mi>K</mi><mo>=</mo><mi>A</mi><mo>∙</mo><mi>W</mi></math>",
        };

        private static LegalDocument Parse(params DocumentBlock[] blocks) =>
            LegalDocumentParser.Parse(blocks.ToList(), new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

        private static readonly DocumentBlock[] Heading =
        [
            Text("USTAWA"), Text("z dnia 14 czerwca 2024 r."), Text("o opłatach"),
        ];

        // ============================================================
        // Czytnik: który akapit jest wzorem
        // ============================================================

        [Fact]
        public void ParagraphWithOnlyAnEquation_IsAFormula()
        {
            var block = DocxBlockReader.ToBlock(Word($"<m:oMathPara>{Equation("K=A∙W")}</m:oMathPara>"), null);

            Assert.Equal("K=A∙W", block.Text);
            var math = XElement.Parse(block.FormulaMathMl!);
            Assert.Equal(MathNamespace, math.Name.NamespaceName);
            Assert.Equal("block", math.Attribute("display")!.Value);
            Assert.Equal(new[] { "mi", "mo", "mi", "mo", "mi" }, math.Elements().Select(e => e.Name.LocalName));
        }

        [Fact]
        public void PunctuationAfterTheEquation_BelongsToTheFormula()
        {
            // Korpus: 351826_3166873 — „C_s=C_0-PI/I ," z przecinkiem zdania, w którym wzór stoi.
            var block = DocxBlockReader.ToBlock(Word(Equation("a=b") + Run(" ,")), null);

            var math = XElement.Parse(block.FormulaMathMl!);
            Assert.Equal(",", math.Elements().Last().Value);
            Assert.Equal("mo", math.Elements().Last().Name.LocalName);
        }

        [Theory]
        // Wiersz legendy i zdanie z symbolem niosą tekst obok równania — to nie wzory.
        [InlineData(" – ilość energii elektrycznej")]
        [InlineData(", o której mowa w art. 5")]
        public void ParagraphWithTextBesideTheEquation_IsNotAFormula(string text)
        {
            Assert.Null(DocxBlockReader.ToBlock(Word(Equation("Ie") + Run(text)), null).FormulaMathMl);
        }

        [Fact]
        public void ParagraphWithoutEquations_IsNotAFormula()
        {
            Assert.Null(DocxBlockReader.ToBlock(Word(Run("Art. 1. Ustawa określa zasady.")), null).FormulaMathMl);
        }

        [Fact]
        public void SeveralEquationsInOneParagraph_AreLinesOfOneFormula()
        {
            var math = OfficeMathMl.FormulaOf(Word(Equation("a=b") + Equation("c=d")))!;

            var table = Assert.Single(math.Elements());
            Assert.Equal("mtable", table.Name.LocalName);
            Assert.Equal(new[] { "a=b", "c=d" }, table.Elements().Select(row => row.Value));
        }

        // ============================================================
        // Model i eksport: wzór jako dalsza treść jednostki
        // ============================================================

        private static DocumentBlock[] ActWithFormula(string? formulaStyle = null) =>
        [
            .. Heading,
            Text("Art. 1. 1. Wysokość kary oblicza się według wzoru:"),
            Formula("K=A∙W", formulaStyle),
            Text("gdzie poszczególne symbole oznaczają:"),
            Text("K – wysokość kary, wyrażoną w złotych,"),
            Text("2. Karę uiszcza się w terminie 14 dni."),
            Text("Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia."),
        ];

        [Fact]
        public void FormulaParagraph_ContinuesTheUnitItFollows_WithItsMathMl()
        {
            var paragraph = Parse(ActWithFormula()).Articles.First().Paragraphs[0];

            var blocks = paragraph.Continuation;
            Assert.Equal(new[] { "K=A∙W", "gdzie poszczególne symbole oznaczają:", "K – wysokość kary, wyrażoną w złotych," }, blocks.Select(b => b.Text));
            Assert.StartsWith("<math", blocks[0].Formula!.MathMl);
            Assert.All(blocks.Skip(1), block => Assert.Null(block.Formula));
        }

        [Theory]
        // Wzór złożony stylem jednostki (ustęp, punkt) nie ma oznaczenia — jednostką nie jest.
        [InlineData("USTustnpkodeksu")]
        [InlineData("PKTpunkt")]
        public void FormulaInAUnitStyle_IsNotAUnit(string style)
        {
            var article = Parse(ActWithFormula(style)).Articles.First();

            Assert.Equal(new[] { "1", "2" }, article.Paragraphs.Select(p => p.Number?.Value));
            Assert.Empty(article.Paragraphs[0].Points);
            Assert.NotNull(article.Paragraphs[0].Continuation[0].Formula);
        }

        [Fact]
        public void Formula_IsExportedAsWzorMat_AfterTheSentencesOfItsUnit()
        {
            var act = LegalDocumentConverter.Convert(Parse(ActWithFormula())).Elements().Single();
            var paragraph = act.Descendants().First(e => e.Name.LocalName == "ustep");

            Assert.Equal(new[] { "nr", "p", "wzor-mat", "fragment", "fragment" }, paragraph.Elements().Select(e => e.Name.LocalName));

            var formula = paragraph.Elements().Single(e => e.Name.LocalName == "wzor-mat");
            var math = formula.Elements().Single(e => e.Name.LocalName == "wzor").Elements().Single();
            Assert.Equal(MathNamespace, math.Name.NamespaceName);
            Assert.Equal("math", math.Name.LocalName);
            Assert.Equal("K=A∙W", math.Value);

            // Wzór nie zostaje drugi raz jako tekst.
            Assert.DoesNotContain(paragraph.Elements().Where(e => e.Name.LocalName == "fragment"), e => e.Value == "K=A∙W");
        }

        [SchemaPlFact]
        public void ActWithAFormula_IsSchemaValid_InItsBody()
        {
            Assert.Empty(SchemaPl.Validator.ValidateActBody(
                LegalDocumentConverter.Convert(Parse(ActWithFormula())).Elements().First()));
        }

        // ============================================================
        // Treść cytowana
        // ============================================================

        [Fact]
        public void FormulaInQuotedContent_ContinuesTheQuotedUnit()
        {
            // Korpus: 318581_2836177 — wzór w treści cytowanej.
            var document = Parse(
            [
                Text("USTAWA"), Text("z dnia 14 czerwca 2024 r."), Text("o zmianie ustawy o opłatach"),
                Text("Art. 1. W ustawie z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) art. 5 otrzymuje brzmienie:"),
                Text("„Art. 5. Wysokość kary oblicza się według wzoru:"),
                Formula("K=A∙W"),
                Text("gdzie K oznacza wysokość kary.”."),
                Text("Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia."),
            ]);

            var quoted = Assert.Single(document.Articles.First().Paragraphs[0].Amendment!.Content!.Articles);
            var blocks = quoted.Paragraphs[0].Continuation;
            Assert.Equal(new[] { "K=A∙W", "gdzie K oznacza wysokość kary." }, blocks.Select(b => b.Text));
            Assert.NotNull(blocks[0].Formula);

            var citation = LegalDocumentConverter.Convert(document).Descendants().Single(e => e.Name.LocalName == "cytat-strukt");
            Assert.Single(citation.Descendants(), e => e.Name.LocalName == "wzor-mat");
        }

        [Fact]
        public void QuotedFormulaInAUnitStyle_IsNotAUnit()
        {
            // W szczotce wzór cytowany bywa złożony stylem artykułu albo ustępu („Z/ART", „Z/LIT/UST") —
            // styl nie czyni z niego jednostki, bo wzór nie ma oznaczenia.
            var article = new Saga.Core.Services.Parsing.CollectedAmendmentParagraph(
                "Art. 5. Wysokość kary oblicza się według wzoru:", null, null);
            var formula = new Saga.Core.Services.Parsing.CollectedAmendmentParagraph(
                "K=A∙W", "ZUSTzmustartykuempunktem", Saga.Core.Helpers.AmendmentStyleDecoder.DecodeByStyleId("ZUSTzmustartykuempunktem"))
            {
                FormulaMathMl = Formula("K=A∙W").FormulaMathMl,
            };

            var content = new Saga.Core.Services.Parsing.Builders.AmendmentBuilder().Build(
                new Saga.Core.Services.Parsing.Builders.AmendmentBuildInput([article, formula], null, AmendmentOperationType.Modification));

            var paragraph = Assert.Single(Assert.Single(content.Articles).Paragraphs);
            Assert.NotNull(Assert.Single(paragraph.Continuation).Formula);
            Assert.Empty(content.Paragraphs);
        }
    }
}
