using DocumentFormat.OpenXml.Wordprocessing;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Równanie Worda (<c>m:oMath</c>) w tekście akapitu: zapis liniowy w miejscu, w którym równanie stoi.
    /// Bez tego wzór i symbol otwierający wiersz legendy znikały z odczytanego tekstu.
    /// </summary>
    public class OfficeMathTextTests
    {
        private const string Namespaces =
            "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" " +
            "xmlns:m=\"http://schemas.openxmlformats.org/officeDocument/2006/math\"";

        private static string Text(string content) =>
            new Paragraph($"<w:p {Namespaces}>{content}</w:p>").GetFullText();

        private static string Math(string content) => Text($"<m:oMath>{content}</m:oMath>");

        private static string R(string text) => $"<m:r><m:t>{text}</m:t></m:r>";

        private static string W(string text) => $"<w:r><w:t xml:space=\"preserve\">{text}</w:t></w:r>";

        [Fact]
        public void PlainEquation_IsReadAsItsCharacters()
        {
            // Korpus: 318581_2836177.
            Assert.Equal(
                "Ioze=(Ie∙Uoze)+Ib",
                Math(R("Ioze=") + "<m:d><m:dPr/><m:e>" + R("Ie∙Uoze") + "</m:e></m:d>" + R("+Ib")));
        }

        [Theory]
        [InlineData("<m:sSub><m:e>{E}</m:e><m:sub>{i}</m:sub></m:sSub>", "E_i")]
        [InlineData("<m:sSup><m:e>{x}</m:e><m:sup>{2}</m:sup></m:sSup>", "x^2")]
        [InlineData("<m:sSubSup><m:e>{SPW}</m:e><m:sub>{2024}</m:sub><m:sup>{W}</m:sup></m:sSubSup>", "SPW_2024^W")]
        // Argument złożony stoi w nawiasie; argument już w nawiasie nie dostaje drugiego.
        [InlineData("<m:sSub><m:e>{WYD}</m:e><m:sub>{n-1}</m:sub></m:sSub>", "WYD_(n-1)")]
        [InlineData("<m:sSub><m:e>{Eb}</m:e><m:sub><m:d><m:e>{t}</m:e></m:d></m:sub></m:sSub>", "Eb_(t)")]
        public void Indices_AreMarkedWithUnderscoreAndCaret(string omml, string expected)
        {
            Assert.Equal(expected, Math(Expand(omml)));
        }

        [Theory]
        [InlineData("<m:f><m:num>{PI}</m:num><m:den>{I}</m:den></m:f>", "PI/I")]
        [InlineData("<m:f><m:num>{a+b}</m:num><m:den>{100 %}</m:den></m:f>", "(a+b)/(100 %)")]
        public void Fraction_IsNumeratorOverDenominator(string omml, string expected)
        {
            Assert.Equal(expected, Math(Expand(omml)));
        }

        [Theory]
        // Znaki nawiasu z właściwości; brak właściwości to nawias okrągły, pusty znak — brak nawiasu.
        [InlineData("<m:d><m:dPr><m:begChr m:val=\"[\"/><m:endChr m:val=\"]\"/></m:dPr><m:e>{a}</m:e></m:d>", "[a]")]
        [InlineData("<m:d><m:dPr><m:begChr m:val=\"\"/><m:endChr m:val=\"\"/></m:dPr><m:e>{a}</m:e></m:d>", "a")]
        [InlineData("<m:d><m:e>{a}</m:e><m:e>{b}</m:e></m:d>", "(a|b)")]
        public void Delimiter_UsesItsOwnCharacters(string omml, string expected)
        {
            Assert.Equal(expected, Math(Expand(omml)));
        }

        [Theory]
        [InlineData(
            "<m:nary><m:naryPr><m:chr m:val=\"∑\"/></m:naryPr><m:sub>{i=u}</m:sub><m:sup>{j}</m:sup><m:e>{PI}</m:e></m:nary>",
            "∑_(i=u)^jPI")]
        // Ukryta granica nie zostawia znaku.
        [InlineData(
            "<m:nary><m:naryPr><m:chr m:val=\"∑\"/><m:supHide m:val=\"1\"/></m:naryPr><m:sub>{t∈T}</m:sub><m:sup/><m:e>{E}</m:e></m:nary>",
            "∑_(t∈T)E")]
        [InlineData("<m:rad><m:radPr><m:degHide m:val=\"1\"/></m:radPr><m:deg/><m:e>{x}</m:e></m:rad>", "√x")]
        [InlineData("<m:rad><m:deg>{8}</m:deg><m:e>{PKB}</m:e></m:rad>", "√(8&PKB)")]
        public void OperatorsAndRoots_KeepTheirLimitsAndDegree(string omml, string expected)
        {
            Assert.Equal(expected, Math(Expand(omml)));
        }

        [Fact]
        public void EquationInsideASentence_StandsWhereItIsWritten()
        {
            // Korpus: 299130_2697165 — bez równania zdanie brzmiało „kwoty wydatków , o której mowa".
            var text = Text(
                W("do obliczenia kwoty wydatków ") +
                "<m:oMath><m:sSub><m:e>" + R("WYD") + "</m:e><m:sub>" + R("n") + "</m:sub></m:sSub></m:oMath>" +
                W(", o której mowa w art. 112aa"));

            Assert.Equal("do obliczenia kwoty wydatków WYD_n, o której mowa w art. 112aa", text);
        }

        [Fact]
        public void SymbolOpeningALegendLine_IsNotLost()
        {
            var text = Text("<m:oMath>" + R("Ie") + "</m:oMath>" + W(" – ilość energii elektrycznej"));

            Assert.Equal("Ie – ilość energii elektrycznej", text);
        }

        [Fact]
        public void EquationInItsOwnParagraph_IsTheTextOfTheParagraph()
        {
            Assert.Equal("a=b", Text("<m:oMathPara><m:oMath>" + R("a=b") + "</m:oMath></m:oMathPara>"));
        }

        [Fact]
        public void ParagraphWithoutEquations_ReadsAsBefore()
        {
            Assert.Equal("Art. 1. Ustawa określa zasady.", Text(W("Art. 1. ") + W("Ustawa określa zasady.")));
        }

        /// <summary>„{tekst}" w próbce to przebieg równania z tym tekstem.</summary>
        private static string Expand(string omml) =>
            System.Text.RegularExpressions.Regex.Replace(omml, @"\{([^}]*)\}", match => R(match.Groups[1].Value));
    }
}
