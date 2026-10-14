using System.Xml.Linq;
using Saga.Core.Services;
using Saga.Core.Services.Parsing;
using Kind = Saga.Core.Services.Parsing.FootnoteContent.Kind;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Wyliczenie w odnośniku (ADR-0078): akapity za pierwszym akapitem treści odnośnika to jego jednostki — punkty,
    /// litery, tirety, części wspólne i dalsze akapity — a nie kolejne zdania jednego akapitu.
    /// </summary>
    public class FootnoteEnumerationTests
    {
        private static Kind[] Kinds(IEnumerable<FootnoteContent.Unit> units) => units.Select(unit => unit.Kind).ToArray();

        [Fact]
        public void PointsUnderTheFirstParagraph_ArePointsOfTheFootnote()
        {
            var units = FootnoteContent.Read(["Niniejsza ustawa służy stosowaniu:", "1)\trozporządzenia 2021/784;", "2)\trozporządzenia 2022/2065."])!;

            Assert.Equal([Kind.Point, Kind.Point], Kinds(units));
            Assert.Equal(["1", "2"], units.Select(unit => unit.Number));
            Assert.Equal("rozporządzenia 2021/784;", units[0].Text);
        }

        [Fact]
        public void LettersAndTirets_StandUnderTheirPointAndLetter()
        {
            // Korpus: 309531_2761490 — „2) służy stosowaniu:" z literami.
            var units = FootnoteContent.Read(
            [
                "Niniejsza ustawa:",
                "1)\twdraża dyrektywę 2022/738;",
                "2)\tsłuży stosowaniu:",
                "a)\trozporządzenia 2020/1055,",
                "b)\trozporządzenia wykonawczego Komisji 2022/694:",
                "– w zakresie przewozu drogowego,",
                "– w zakresie dostępu do zawodu.",
            ])!;

            Assert.Equal([Kind.Point, Kind.Point], Kinds(units));
            var letters = units[1].Units;
            Assert.Equal(["a", "b"], letters.Select(letter => letter.Number));
            Assert.Equal([Kind.Tiret, Kind.Tiret], Kinds(letters[1].Units));
            Assert.Equal("w zakresie przewozu drogowego,", letters[1].Units[0].Text);
        }

        [Fact]
        public void DashAfterAnItemWithoutClosingMark_ClosesItsEnumerationWithACommonPart()
        {
            // Jak w treści aktu (ADR-0074): myślnik za pozycją bez znaku końcowego to część wspólna, nie kolejna pozycja.
            var units = FootnoteContent.Read(
            [
                "Niniejsza ustawa wdraża:",
                "1) dyrektywę 2019/1937:",
                "a) art. 1,",
                "b) art. 2",
                "– w zakresie ochrony sygnalistów,",
                "2) dyrektywę 2022/2555",
                "– w zakresie swojej regulacji.",
            ])!;

            Assert.Equal([Kind.Point, Kind.Point, Kind.CommonPart], Kinds(units));
            Assert.Equal([Kind.Letter, Kind.Letter, Kind.CommonPart], Kinds(units[0].Units));
            Assert.Equal("– w zakresie swojej regulacji.", units[2].Text);
        }

        [Fact]
        public void CapitalizedParagraphAfterTheEnumeration_IsAFurtherParagraph_NotACommonPart()
        {
            // Wiersz o ogłoszeniu aktów Unii stoi w szablonie w stylu części wspólnej, ale zaczyna nowe zdanie.
            var units = FootnoteContent.Read(
            [
                "Niniejsza ustawa służy stosowaniu:",
                "1) rozporządzenia 2021/784.",
                "Dane dotyczące ogłoszenia aktów prawa Unii Europejskiej dotyczą wydania specjalnego.",
            ])!;

            Assert.Equal([Kind.Point, Kind.Paragraph], Kinds(units));
        }

        [Fact]
        public void FurtherParagraphsWithoutEnumeration_AreParagraphsOfTheFootnote()
        {
            var units = FootnoteContent.Read(["Zmiany wymienionej ustawy zostały ogłoszone w Dz. U. poz. 1.", "Tekst jednolity ogłoszono osobno."])!;

            Assert.Equal([Kind.Paragraph], Kinds(units));
        }

        public static TheoryData<string[]> Inexpressible => new()
        {
            // Jeden akapit — nie ma czego dzielić.
            new[] { "Niniejsza ustawa wdraża dyrektywę." },
            // Litera bez punktu: schemat nie ma litery wprost w odnośniku.
            new[] { "Niniejsza ustawa wdraża:", "a) dyrektywę 2019/1937," },
            // Tiret wprost pod wprowadzeniem i pod punktem: tiret odnośnika stoi tylko w literze.
            new[] { "Niniejsza ustawa wdraża:", "– dyrektywę 2019/1937," },
            new[] { "Niniejsza ustawa:", "1) wykonuje postanowienia:", "– rozporządzenia 277/2012," },
            // Akapit od małej litery bez wyliczenia przed sobą ciągnie zdanie pierwszego akapitu.
            new[] { "Niniejsza ustawa wdraża dyrektywę", "w zakresie swojej regulacji." },
        };

        [Theory]
        [MemberData(nameof(Inexpressible))]
        public void ContentTheFootnoteUnitsCannotExpress_StaysOneParagraph(string[] paragraphs)
        {
            Assert.Null(FootnoteContent.Read(paragraphs));
        }

        // ============================================================
        // Eksport
        // ============================================================

        private static string FootnoteXml(params string[] paragraphs) =>
            @"<w:p><w:r><w:rPr><w:rStyle w:val=""IGindeksgrny""/></w:rPr><w:footnoteRef/></w:r>" +
            @"<w:r><w:rPr><w:rStyle w:val=""IGindeksgrny""/></w:rPr><w:t>)</w:t></w:r>" +
            $@"<w:r><w:tab/><w:t xml:space=""preserve"">{paragraphs[0]}</w:t></w:r></w:p>" +
            string.Concat(paragraphs.Skip(1).Select(text =>
                $@"<w:p><w:r><w:t xml:space=""preserve"">{text.Replace("\t", @"</w:t><w:tab/><w:t xml:space=""preserve"">")}</w:t></w:r></w:p>"));

        private static readonly string[] EnumeratingFootnote =
        [
            "Niniejsza ustawa:",
            "1)\twdraża dyrektywę 2022/738,",
            "2)\tsłuży stosowaniu:",
            "a)\trozporządzenia 2020/1055,",
            "b)\trozporządzenia wykonawczego Komisji 2022/694 w zakresie:",
            "– przewozu drogowego,",
            "– dostępu do zawodu",
            "– w brzmieniu ostatnich zmian,",
            "c)\trozporządzenia 2024/1",
            "– w zakresie objętym ustawą,",
            "3)\twykonuje decyzję 2023/5",
            "– w zakresie swojej regulacji.",
            "Dane dotyczące ogłoszenia aktów prawa Unii Europejskiej dotyczą wydania specjalnego.",
        ];

        private static XElement Export(params string[] footnoteParagraphs)
        {
            using var stream = new MemoryStream(DocxFixtures.WithFootnoteXml(
                [FootnoteXml(footnoteParagraphs)],
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o przewozie drogowym{1}",
                "Art. 1. Ustawa określa zasady przewozu drogowego.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia."));
            var document = LegalDocumentParser.Parse(stream, "ustawa.docx", new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
            return LegalDocumentConverter.Convert(document);
        }

        private static XElement Footnote(XElement xml) => xml.Descendants().Single(e => e.Name.LocalName == "odnosnik");

        private static string[] ChildNames(XElement element) => element.Elements().Select(e => e.Name.LocalName).ToArray();

        private static XElement Sentence(XElement unit) =>
            unit.Elements().Single(e => e.Name.LocalName == "p").Elements().First(e => e.Name.LocalName == "zdanie");

        [Fact]
        public void FootnoteWithAnEnumeration_IsExportedAsItsUnits()
        {
            var footnote = Footnote(Export(EnumeratingFootnote));

            Assert.Equal(["p", "punkt-odnosnik", "punkt-odnosnik", "punkt-odnosnik", "czesc-wspolna-punkt-odnosnik", "fragment-odnosnik"],
                ChildNames(footnote));

            var second = footnote.Elements().ElementAt(2);
            Assert.Equal(["nr", "p", "litera-odnosnik", "litera-odnosnik", "litera-odnosnik", "czesc-wspolna-litera-odnosnik"], ChildNames(second));
            Assert.Equal("2", second.Elements().First().Value);
            Assert.Equal("służy stosowaniu:", Sentence(second).Value);

            var letter = second.Elements().ElementAt(3);
            Assert.Equal(["nr", "p", "tiret-odnosnik", "tiret-odnosnik", "czesc-wspolna-tiret-odnosnik"], ChildNames(letter));
            Assert.Equal("przewozu drogowego,", Sentence(letter.Elements().ElementAt(2)).Value);
        }

        [Fact]
        public void IntroductionAndItsItems_AreFragmentsOfOneSentence()
        {
            var footnote = Footnote(Export(EnumeratingFootnote));

            // Wprowadzenie, punkty, ich litery i tirety oraz części wspólne to jedno zdanie, we fragmentach w kolejności
            // tekstu — jak wyliczenie w treści aktu. Dalszy akapit zaczyna nowe zdanie.
            var units = footnote.Elements().Where(e => e.Name.LocalName != "fragment-odnosnik").ToList();
            var sentences = units.Select(unit => unit.Name.LocalName == "p" ? unit.Elements().First() : Sentence(unit)).ToList();
            Assert.Single(sentences.Select(sentence => sentence.Attributes().Single(a => a.Name.LocalName == "zdGUID").Value).Distinct());
            Assert.Equal(["1", "2", "3", "11", "12"], sentences.Select(sentence => sentence.Attribute("fragment")?.Value));
        }

        [Fact]
        public void FootnoteWithoutAnExpressibleEnumeration_StaysOneParagraph()
        {
            var footnote = Footnote(Export("Niniejsza ustawa wdraża:", "– dyrektywę 2019/1937,", "– dyrektywę 2022/2555."));

            Assert.Equal(["p"], ChildNames(footnote));
            Assert.Equal(3, footnote.Elements().Single().Elements().Count());
        }

        [SchemaPlFact]
        public void FootnoteWithAnEnumeration_IsSchemaValid()
        {
            Assert.Empty(SchemaPl.Validator.ValidateActBody(Export(EnumeratingFootnote).Elements().First()));
        }
    }
}
