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

            var block = Assert.Single(paragraph.Continuation);
            Assert.Equal("K=A∙W", block.Text);
            Assert.StartsWith("<math", block.Formula!.MathMl);
        }

        // ============================================================
        // Legenda wzoru
        // ============================================================

        [Fact]
        public void LinesAfterTheFormula_AreItsLegend()
        {
            var formula = Parse(ActWithFormula()).Articles.First().Paragraphs[0].Continuation[0].Formula!;

            Assert.Equal("gdzie poszczególne symbole oznaczają:", formula.LegendIntroduction);
            var symbol = Assert.Single(formula.Legend);
            Assert.Equal("K", symbol.Symbol);
            Assert.Equal("wysokość kary, wyrażoną w złotych,", symbol.Explanation);
            Assert.Null(symbol.SymbolMathMl);
        }

        [Theory]
        [InlineData("gdzie:")]
        [InlineData("w którym poszczególne symbole oznaczają:")]
        [InlineData("przy czym:")]
        [InlineData("poszczególne symbole oznaczają:")]
        public void LegendIntroduction_IsASentenceAnnouncingTheSymbols(string introduction)
        {
            var blocks = ActWithFormula();
            blocks[5] = Text(introduction);

            var formula = Parse(blocks).Articles.First().Paragraphs[0].Continuation[0].Formula!;

            Assert.Equal(introduction, formula.LegendIntroduction);
            Assert.Single(formula.Legend);
        }

        [Fact]
        public void SymbolWrittenAsAnEquation_KeepsItsMathMl()
        {
            // Korpus: 318581_2836177 — wiersz legendy zaczyna się równaniem „I_e", za nim myślnik i objaśnienie.
            var blocks = ActWithFormula();
            blocks[6] = new DocumentBlock
            {
                Text = "I_e \t–\tilość energii elektrycznej,",
                LeadingSymbol = new BlockSymbol("I_e", $"<math xmlns=\"{MathNamespace}\"><msub><mi>I</mi><mi>e</mi></msub></math>"),
            };

            var symbol = Assert.Single(Parse(blocks).Articles.First().Paragraphs[0].Continuation[0].Formula!.Legend);

            Assert.Equal("I_e", symbol.Symbol);
            Assert.Equal("ilość energii elektrycznej,", symbol.Explanation);
            Assert.Contains("<msub>", symbol.SymbolMathMl);
        }

        [Fact]
        public void LineThatIsNotALegend_ClosesIt()
        {
            // Zdanie za legendą jest zwykłą dalszą treścią; wiersz „X – …" po nim nie wraca do legendy.
            var blocks = ActWithFormula().ToList();
            blocks.InsertRange(7,
            [
                Text("Kwotę zaokrągla się do pełnych złotych."),
                Text("W – współczynnik stosowany w latach następnych,"),
            ]);

            var continuation = Parse(blocks.ToArray()).Articles.First().Paragraphs[0].Continuation;

            Assert.Single(continuation[0].Formula!.Legend);
            Assert.Equal(
                new[] { "Kwotę zaokrągla się do pełnych złotych.", "W – współczynnik stosowany w latach następnych," },
                continuation.Skip(1).Select(block => block.Text));
        }

        [Fact]
        public void SentenceAfterTheFormulaWithoutAColon_IsNotALegend()
        {
            // Korpus: 296403_2650204 — „gdzie ZEE oznacza …." to zdanie, nie wprowadzenie do opisów.
            var blocks = ActWithFormula();
            blocks[5] = Text("gdzie K oznacza wysokość kary w złotych.");
            blocks[6] = Text("2. Karę uiszcza się w terminie 14 dni.");

            var continuation = Parse(blocks.Take(7).ToArray()).Articles.First().Paragraphs[0].Continuation;

            Assert.Null(continuation[0].Formula!.LegendIntroduction);
            Assert.Empty(continuation[0].Formula!.Legend);
            Assert.Equal("gdzie K oznacza wysokość kary w złotych.", continuation[1].Text);
        }

        [Fact]
        public void EquationOpeningAParagraphWithText_IsItsLeadingSymbol()
        {
            var legendLine = DocxBlockReader.ToBlock(Word(Equation("Ie") + Run(" – ilość energii elektrycznej")), null);
            Assert.Equal("Ie", legendLine.LeadingSymbol!.Text);
            Assert.StartsWith("<math", legendLine.LeadingSymbol.MathMl);

            // Tekst przed równaniem albo sam wzór — akapit nie zaczyna się symbolem.
            Assert.Null(DocxBlockReader.ToBlock(Word(Run("kwoty ") + Equation("W") + Run(", o której mowa")), null).LeadingSymbol);
            Assert.Null(DocxBlockReader.ToBlock(Word(Equation("a=b")), null).LeadingSymbol);
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

            Assert.Equal(new[] { "nr", "p", "wzor-mat" }, paragraph.Elements().Select(e => e.Name.LocalName));

            var formula = paragraph.Elements().Single(e => e.Name.LocalName == "wzor-mat");
            var math = formula.Elements().Single(e => e.Name.LocalName == "wzor").Elements().Single();
            Assert.Equal(MathNamespace, math.Name.NamespaceName);
            Assert.Equal("math", math.Name.LocalName);
            Assert.Equal("K=A∙W", math.Value);

            // Wzór ani jego legenda nie zostają drugi raz jako tekst.
            Assert.DoesNotContain(paragraph.Descendants(), e => e.Name.LocalName == "fragment");
        }

        [Fact]
        public void Legend_IsExportedInsideTheFormula()
        {
            var blocks = ActWithFormula().ToList();
            blocks.Insert(7, new DocumentBlock
            {
                Text = "A – współczynnik równy 0,20.",
                LeadingSymbol = new BlockSymbol("A", $"<math xmlns=\"{MathNamespace}\"><mi>A</mi></math>"),
            });

            var formula = LegalDocumentConverter.Convert(Parse(blocks.ToArray())).Descendants().Single(e => e.Name.LocalName == "wzor-mat");

            Assert.Equal(new[] { "wzor", "wprowadzenie-do-opisu", "opis", "opis" }, formula.Elements().Select(e => e.Name.LocalName));
            Assert.Equal("gdzie poszczególne symbole oznaczają:", formula.Elements().ElementAt(1).Value);

            // Symbol zapisany tekstem i symbol będący równaniem; objaśnienie podzielone na zdania.
            var typed = formula.Elements().ElementAt(2);
            Assert.Equal(new[] { "symbol-txt", "objasnienie" }, typed.Elements().Select(e => e.Name.LocalName));
            Assert.Equal("K", typed.Elements().First().Value);
            Assert.Equal("wysokość kary, wyrażoną w złotych,", typed.Descendants().Single(e => e.Name.LocalName == "zdanie").Value);

            var equation = formula.Elements().ElementAt(3);
            Assert.Equal(new[] { "symbol-mat", "objasnienie" }, equation.Elements().Select(e => e.Name.LocalName));
            Assert.Equal("math", equation.Elements().First().Elements().Single().Name.LocalName);
        }

        [SchemaPlFact]
        public void FormulaWithALegend_IsSchemaValid_InItsBody()
        {
            var blocks = ActWithFormula().ToList();
            blocks.Insert(7, new DocumentBlock
            {
                Text = "A – współczynnik równy 0,20.",
                LeadingSymbol = new BlockSymbol("A", $"<math xmlns=\"{MathNamespace}\"><mi>A</mi></math>"),
            });

            Assert.Empty(SchemaPl.Validator.ValidateActBody(
                LegalDocumentConverter.Convert(Parse(blocks.ToArray())).Elements().First()));
        }

        // Korpus: 305231_2733472 — „1)" w osobnym akapicie, za nim wzór i jego legenda, „albo", „2)" i drugi wzór.
        private static DocumentBlock[] PointsMadeOfFormulas() =>
        [
            .. Heading,
            Text("Art. 1. 1. Premię oblicza się zgodnie ze wzorami:", "ARTartustawynprozporzdzenia"),
            Text("1)", "PKTpunkt"),
            Formula("K=A∙W"),
            Text("albo", "PKTpunkt"),
            Text("2)", "PKTpunkt"),
            Formula("K=A∙W"),
            Text("2. Premię wypłaca się co miesiąc.", "USTustnpkodeksu"),
            Text("Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.", "ARTartustawynprozporzdzenia"),
        ];

        [Fact]
        public void PointWhoseParagraphHasOnlyItsDesignation_KeepsAnEmptyParagraph_BeforeItsFormula()
        {
            var points = LegalDocumentConverter.Convert(Parse(PointsMadeOfFormulas())).Descendants()
                .Where(e => e.Name.LocalName == "punkt").ToList();

            Assert.Equal(2, points.Count);
            Assert.All(points, point => Assert.Equal(["nr", "p", "wzor-mat"],
                point.Elements().Select(e => e.Name.LocalName).Take(3)));
            Assert.Equal(string.Empty, points[0].Elements().ElementAt(1).Value);
        }

        [SchemaPlFact]
        public void PointsMadeOfFormulas_AreSchemaValid_InTheirBody()
        {
            Assert.Empty(SchemaPl.Validator.ValidateActBody(
                LegalDocumentConverter.Convert(Parse(PointsMadeOfFormulas())).Elements().First()));
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
