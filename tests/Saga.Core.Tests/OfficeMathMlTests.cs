using System.Xml.Linq;
using DocumentFormat.OpenXml.Wordprocessing;
using Saga.Core.Helpers;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Równanie Worda (<c>m:oMath</c>) jako MathML prezentacyjny — postać wzoru w schemacie (<c>wzor</c>).
    /// </summary>
    public class OfficeMathMlTests
    {
        private const string Namespaces =
            "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" " +
            "xmlns:m=\"http://schemas.openxmlformats.org/officeDocument/2006/math\"";

        /// <summary>MathML równania bez deklaracji przestrzeni nazw — sama budowa.</summary>
        private static string MathMl(string omml, bool display = false) =>
            Flat(Convert(omml, display));

        private static XElement Convert(string omml, bool display = false)
        {
            var paragraph = new Paragraph($"<w:p {Namespaces}><m:oMath>{Expand(omml)}</m:oMath></w:p>");
            return OfficeMathMl.ToMathMl(paragraph.Descendants().First(OfficeMathText.IsMath), display);
        }

        private static string Flat(XElement element)
        {
            var attributes = string.Concat(element.Attributes().Select(a => $" {a.Name.LocalName}=\"{a.Value}\""));
            var content = string.Concat(element.Nodes().Select(node => node is XElement child ? Flat(child) : ((XText)node).Value));
            return $"<{element.Name.LocalName}{attributes}>{content}</{element.Name.LocalName}>";
        }

        /// <summary>„{tekst}" w próbce to przebieg równania z tym tekstem.</summary>
        private static string Expand(string omml) =>
            System.Text.RegularExpressions.Regex.Replace(omml, @"\{([^}]*)\}", match => $"<m:r><m:t>{match.Groups[1].Value}</m:t></m:r>");

        [Fact]
        public void RunText_IsSplitIntoIdentifiersNumbersAndOperators()
        {
            // Kilka liter to jeden identyfikator; Word składa go kursywą, MathML domyślnie pismem prostym.
            Assert.Equal(
                "<math><mi mathvariant=\"italic\">Ioze</mi><mo>=</mo><mi>K</mi><mo>∙</mo><mn>100</mn><mo>%</mo></math>",
                MathMl("{Ioze=K∙100 %}"));
        }

        [Theory]
        // Odstęp i przecinek między cyframi należą do liczby.
        [InlineData("{1 724 188 910}", "<math><mn>1 724 188 910</mn></math>")]
        [InlineData("{0,20+A}", "<math><mn>0,20</mn><mo>+</mo><mi>A</mi></math>")]
        // Znak wyrównania układu równań nie jest treścią.
        [InlineData("{a&amp;=b}", "<math><mi>a</mi><mo>=</mo><mi>b</mi></math>")]
        public void Numbers_KeepTheirSeparators(string omml, string expected)
        {
            Assert.Equal(expected, MathMl(omml));
        }

        [Theory]
        // Krój przebiegu wychodzi tylko wtedy, gdy różni się od domyślnego MathML (jedna litera — kursywa).
        [InlineData("p", "E", "<math><mi mathvariant=\"normal\">E</mi></math>")]
        [InlineData("p", "WYD", "<math><mi>WYD</mi></math>")]
        [InlineData("b", "x", "<math><mi mathvariant=\"bold\">x</mi></math>")]
        public void IdentifierStyle_FollowsTheRun(string style, string text, string expected)
        {
            Assert.Equal(expected, MathMl($"<m:r><m:rPr><m:sty m:val=\"{style}\"/></m:rPr><m:t>{text}</m:t></m:r>"));
        }

        [Fact]
        public void NormalTextRun_IsText()
        {
            Assert.Equal(
                "<math><mtext>tys. zł</mtext></math>",
                MathMl("<m:r><m:rPr><m:nor/></m:rPr><m:t>tys. zł</m:t></m:r>"));
        }

        [Theory]
        [InlineData("<m:sSub><m:e>{E}</m:e><m:sub>{i}</m:sub></m:sSub>", "<math><msub><mi>E</mi><mi>i</mi></msub></math>")]
        [InlineData("<m:sSup><m:e>{x}</m:e><m:sup>{2}</m:sup></m:sSup>", "<math><msup><mi>x</mi><mn>2</mn></msup></math>")]
        [InlineData(
            "<m:sSubSup><m:e>{S}</m:e><m:sub>{2024}</m:sub><m:sup>{W}</m:sup></m:sSubSup>",
            "<math><msubsup><mi>S</mi><mn>2024</mn><mi>W</mi></msubsup></math>")]
        // Argument złożony z kilku żetonów obejmuje mrow — konstrukcja ma stałą liczbę miejsc.
        [InlineData(
            "<m:sSub><m:e>{W}</m:e><m:sub>{n-1}</m:sub></m:sSub>",
            "<math><msub><mi>W</mi><mrow><mi>n</mi><mo>-</mo><mn>1</mn></mrow></msub></math>")]
        public void Indices_BecomeScriptElements(string omml, string expected)
        {
            Assert.Equal(expected, MathMl(omml));
        }

        [Theory]
        [InlineData("<m:f><m:num>{a}</m:num><m:den>{b}</m:den></m:f>", "<math><mfrac><mi>a</mi><mi>b</mi></mfrac></math>")]
        [InlineData(
            "<m:f><m:fPr><m:type m:val=\"lin\"/></m:fPr><m:num>{a}</m:num><m:den>{b}</m:den></m:f>",
            "<math><mrow><mi>a</mi><mo>/</mo><mi>b</mi></mrow></math>")]
        [InlineData(
            "<m:f><m:fPr><m:type m:val=\"skw\"/></m:fPr><m:num>{a}</m:num><m:den>{b}</m:den></m:f>",
            "<math><mfrac bevelled=\"true\"><mi>a</mi><mi>b</mi></mfrac></math>")]
        public void Fraction_FollowsItsType(string omml, string expected)
        {
            Assert.Equal(expected, MathMl(omml));
        }

        [Theory]
        [InlineData(
            "<m:d><m:e>{a+b}</m:e></m:d>",
            "<math><mrow><mo fence=\"true\">(</mo><mi>a</mi><mo>+</mo><mi>b</mi><mo fence=\"true\">)</mo></mrow></math>")]
        // Klamra z jednej strony (układ przypadków): pusty znak zamykający nie wychodzi.
        [InlineData(
            "<m:d><m:dPr><m:begChr m:val=\"{\"/><m:endChr m:val=\"\"/></m:dPr><m:e>{a}</m:e></m:d>",
            "<math><mrow><mo fence=\"true\">{</mo><mi>a</mi></mrow></math>")]
        [InlineData(
            "<m:d><m:e>{a}</m:e><m:e>{b}</m:e></m:d>",
            "<math><mrow><mo fence=\"true\">(</mo><mi>a</mi><mo separator=\"true\">|</mo><mi>b</mi><mo fence=\"true\">)</mo></mrow></math>")]
        public void Delimiter_UsesItsOwnCharacters(string omml, string expected)
        {
            Assert.Equal(expected, MathMl(omml.Replace("m:val=\"{\"", "m:val=\"&#123;\"")));
        }

        [Theory]
        // Granice pod i nad znakiem (domyślnie) albo jako indeksy; ukryta granica nie wychodzi.
        [InlineData(
            "<m:nary><m:naryPr><m:chr m:val=\"∑\"/></m:naryPr><m:sub>{i}</m:sub><m:sup>{j}</m:sup><m:e>{P}</m:e></m:nary>",
            "<math><mrow><munderover><mo>∑</mo><mi>i</mi><mi>j</mi></munderover><mi>P</mi></mrow></math>")]
        [InlineData(
            "<m:nary><m:naryPr><m:chr m:val=\"∑\"/><m:limLoc m:val=\"subSup\"/></m:naryPr><m:sub>{i}</m:sub><m:sup>{j}</m:sup><m:e>{P}</m:e></m:nary>",
            "<math><mrow><msubsup><mo>∑</mo><mi>i</mi><mi>j</mi></msubsup><mi>P</mi></mrow></math>")]
        [InlineData(
            "<m:nary><m:naryPr><m:chr m:val=\"∑\"/><m:supHide m:val=\"1\"/></m:naryPr><m:sub>{t}</m:sub><m:sup/><m:e>{E}</m:e></m:nary>",
            "<math><mrow><munder><mo>∑</mo><mi>t</mi></munder><mi>E</mi></mrow></math>")]
        public void Operator_CarriesItsLimits(string omml, string expected)
        {
            Assert.Equal(expected, MathMl(omml));
        }

        [Theory]
        [InlineData(
            "<m:rad><m:radPr><m:degHide m:val=\"1\"/></m:radPr><m:deg/><m:e>{x+1}</m:e></m:rad>",
            "<math><msqrt><mi>x</mi><mo>+</mo><mn>1</mn></msqrt></math>")]
        [InlineData("<m:rad><m:deg>{8}</m:deg><m:e>{x}</m:e></m:rad>", "<math><mroot><mi>x</mi><mn>8</mn></mroot></math>")]
        [InlineData(
            "<m:acc><m:accPr><m:chr m:val=\"̅\"/></m:accPr><m:e>{x}</m:e></m:acc>",
            "<math><mover accent=\"true\"><mi>x</mi><mo>̅</mo></mover></math>")]
        [InlineData(
            "<m:m><m:mr><m:e>{a}</m:e><m:e>{b}</m:e></m:mr><m:mr><m:e>{c}</m:e><m:e>{d}</m:e></m:mr></m:m>",
            "<math><mtable><mtr><mtd><mi>a</mi></mtd><mtd><mi>b</mi></mtd></mtr><mtr><mtd><mi>c</mi></mtd><mtd><mi>d</mi></mtd></mtr></mtable></math>")]
        public void RootAccentAndMatrix_KeepTheirStructure(string omml, string expected)
        {
            Assert.Equal(expected, MathMl(omml));
        }

        [Fact]
        public void DisplayEquation_IsMarkedAsBlock()
        {
            Assert.Equal("<math display=\"block\"><mi>a</mi></math>", MathMl("{a}", display: true));
        }

        [SchemaPlFact]
        public void ConvertedEquation_IsValidMathMl()
        {
            // Korpus: 342785_3070463 — wzór z indeksami, ułamkiem, nawiasem, operatorem i pierwiastkiem.
            var math = Convert(
                "<m:sSub><m:e>{WYD}</m:e><m:sub>{n}</m:sub></m:sSub>{=}" +
                "<m:f><m:num><m:sSub><m:e>{DEF}</m:e><m:sub>{n-2}</m:sub></m:sSub></m:num><m:den>{100 %}</m:den></m:f>{∙}" +
                "<m:d><m:e>{a+b}</m:e></m:d>{+}" +
                "<m:nary><m:naryPr><m:chr m:val=\"∑\"/></m:naryPr><m:sub>{i=1}</m:sub><m:sup>{n}</m:sup><m:e>{P}</m:e></m:nary>{+}" +
                "<m:rad><m:deg>{8}</m:deg><m:e>{PKB}</m:e></m:rad>",
                display: true);

            Assert.Empty(SchemaPl.Validator.Validate(math));
        }
    }
}
