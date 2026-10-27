using System.Xml.Linq;
using Saga.Core.Services;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Formatowanie znakowe w treści (ADR-0117): czytnik DOCX zapisuje zakresy formatowania obok tekstu bloku, a przebieg
    /// eksportu obejmuje nimi tekst jednostek — tekst zostaje bez zmian, rozpoznawanie go nie widzi.
    /// </summary>
    public class InlineFormattingTests
    {
        private static string Line(params string[] runs) => $"<w:p>{string.Concat(runs)}</w:p>";

        private static string Run(string text, string properties = "") =>
            $@"<w:r>{(properties.Length > 0 ? $"<w:rPr>{properties}</w:rPr>" : string.Empty)}<w:t xml:space=""preserve"">{text}</w:t></w:r>";

        private const string Subscript = @"<w:vertAlign w:val=""subscript""/>";

        private static XElement Export(params string[] articleLines)
        {
            var body = string.Concat(
                new[] { Line(Run("USTAWA")), Line(Run("z dnia 14 czerwca 2024 r.")), Line(Run("o ochronie powietrza")) }
                    .Concat(articleLines)
                    .Append(Line(Run("Art. 9. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia."))));
            using var stream = new MemoryStream(DocxFixtures.WithBodyXml(body));
            var document = LegalDocumentParser.Parse(stream, "ustawa.docx", new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
            return LegalDocumentConverter.Convert(document);
        }

        private static XElement FirstSentence(XElement xml) => xml.Descendants().First(e => e.Name.LocalName == "zdanie");

        [Fact]
        public void SubscriptInUnitText_StandsInSub_AndTheTextStaysTheSame()
        {
            // 71 dokumentów korpusu: „CO2", „PIi" — bez indeksu dolnego zmienna i jej indeks zlewają się w jedno.
            var sentence = FirstSentence(Export(Line(Run("Art. 1. Emisja CO"), Run("2", Subscript), Run(" i N"), Run("2", Subscript), Run("O nie przekracza normy."))));

            Assert.Equal("Emisja CO2 i N2O nie przekracza normy.", sentence.Value);
            Assert.Equal(["2", "2"], sentence.Elements().Where(e => e.Name.LocalName == "sub").Select(e => e.Value));
        }

        [Fact]
        public void SubscriptInsideBracketsPrintedInTheAct_StaysInSub()
        {
            // 329304_2949179: jednostka w nawiasie „[gCO2eq/MJ]" — nawias jest tekstem aktu, nie kanałem indeksu górnego.
            var sentence = FirstSentence(Export(Line(Run("Art. 1. Emisja wynosi 94 [gCO"), Run("2eq", Subscript), Run("/MJ]."))));

            Assert.Equal("Emisja wynosi 94 [gCO2eq/MJ].", sentence.Value);
            Assert.Equal("2eq", Assert.Single(sentence.Elements(), e => e.Name.LocalName == "sub").Value);
        }

        [Fact]
        public void SuperscriptRightAfterASubscript_IsStillASupElement()
        {
            // „σ₁²": grupa kanału „[2]" stoi w węźle za indeksem dolnym; wzorzec indeksu górnego widzi znak przed nią.
            var sentence = FirstSentence(Export(Line(Run("Art. 1. Wariancja x"), Run("1", Subscript), Run("2", @"<w:vertAlign w:val=""superscript""/>"), Run(" nie przekracza progu."))));

            Assert.Equal("Wariancja x12 nie przekracza progu.", sentence.Value);
            Assert.Equal(["sub", "sup"], sentence.Elements().Select(e => e.Name.LocalName));
        }

        [Fact]
        public void WordsRepeatedInAManualInstruction_TakeTheFormattingOfTheirOwnCommand()
        {
            // Instrukcja ręczna (zmiana w załączniku — ADR-0100) powtarza wyrazy komendy w cytat-w-tekscie; kopia bierze
            // formatowanie z komendy, w której stoi, a nie z następnej o tych samych wyrazach.
            var xml = Export(
                Line(Run("Art. 1. W ustawie z dnia 5 lipca 2018 r. o ochronie powietrza (Dz. U. poz. 20) wprowadza się następujące zmiany:")),
                Line(Run("1) w załączniku nr 1 w lp. 3 wyrazy „NOx” zastępuje się wyrazami „NO"), Run("2", Subscript), Run("”;")),
                Line(Run("2) w załączniku nr 1 w lp. 5 wyrazy „NOx” zastępuje się wyrazami „NO"), Run("2", Subscript), Run("”.")));

            var copies = xml.Descendants().Where(e => e.Name.LocalName == "cytat-w-tekscie")
                .SelectMany(citation => citation.Descendants().Where(e => e.Name.LocalName == "tekst" && e.Value == "NO2")).ToList();
            Assert.NotEmpty(copies);
            Assert.All(copies, copy => Assert.Equal("2", Assert.Single(copy.Elements(), e => e.Name.LocalName == "sub").Value));
        }

        [Fact]
        public void ShortTextRepeatedElsewhere_TakesTheFormattingOfItsOwnPlace()
        {
            // Ten sam krótki tekst stoi w akcie kilka razy; formatowanie przechodzi z miejsca, w którym stoi w kolejności
            // dokumentu, nie z pierwszego wystąpienia.
            var xml = Export(
                Line(Run("Art. 1. Stężenie C2 oznacza się metodą wzorcową.")),
                Line(Run("Art. 2. Stężenie C"), Run("2", Subscript), Run(" oznacza się metodą wzorcową.")));

            var sentences = xml.Descendants().Where(e => e.Name.LocalName == "zdanie").ToList();
            Assert.Empty(sentences[0].Elements());
            Assert.Equal("2", Assert.Single(sentences[1].Elements()).Value);
        }

        [Fact]
        public void ItalicAndBoldOfTheRun_StandInIAndB()
        {
            // Decyzja użytkownika (2026-10-27): kursywa i pogrubienie tam, gdzie źródło je ma — „de minimis" w stylu
            // znaku albo kursywą wprost, pogrubienie przebiegu. Przykład autorów DU 2026/252 pomija kursywę (pytanie 35).
            var sentence = FirstSentence(Export(Line(Run("Art. 1. Pomoc "), Run("de", "<w:i/>"), Run(" "), Run("minimis", "<w:i/>"),
                Run(" udziela się "), Run("wyłącznie", "<w:b/>"), Run(" na wniosek."))));

            Assert.Equal("Pomoc de minimis udziela się wyłącznie na wniosek.", sentence.Value);
            Assert.Equal([("i", "de minimis"), ("b", "wyłącznie")], sentence.Elements().Select(e => (e.Name.LocalName, e.Value)));
        }

        [Fact]
        public void BoldOfTheWholeParagraph_IsItsTypography_NotABElement()
        {
            // ADR-0118: pogrubienie całego akapitu nadane wprost (dokumenty spoza szablonu, nagłówek tabeli, przepis w nowym
            // brzmieniu tekstu jednolitego) jest typografią wiersza jak styl akapitu; wyróżnienie to część akapitu.
            var xml = Export(
                Line(Run("Art. 1. Brak zgody uważa się za odmowę.", "<w:b/>")),
                Line(Run("Art. 2. Termin wynosi "), Run("14 dni", "<w:b/>"), Run(".")));

            var sentences = xml.Descendants().Where(e => e.Name.LocalName == "zdanie").ToList();
            Assert.Empty(sentences[0].Elements());
            Assert.Equal("14 dni", Assert.Single(sentences[1].Elements(), e => e.Name.LocalName == "b").Value);
        }

        [Fact]
        public void TextStartingWithAnInlineElement_IsWrittenWithoutIndentation()
        {
            // Zapis z wcięciami wstawiał przed pierwszym elementem treści mieszanej nowy wiersz i wcięcie, które stawały się
            // częścią tekstu zdania; przykłady autorów schematu mają „<zdanie …><sup>".
            var xml = Export(Line(Run("Art. 1. "), Run("De minimis", "<w:i/>"), Run(" stosuje się do pomocy.")));

            var written = xml.ToString();
            Assert.Matches(@"<zdanie[^>]*><i>De minimis</i> stosuje się", written);
            Assert.Equal("De minimis stosuje się do pomocy.", FirstSentence(XElement.Parse(written)).Value);
        }

        [Fact]
        public void BoldAndItalicTogether_NestBoldOutside()
        {
            var sentence = FirstSentence(Export(Line(Run("Art. 1. Stosuje się zasadę "), Run("non bis in idem", "<w:b/><w:i/>"), Run(" w każdej sprawie."))));

            var bold = Assert.Single(sentence.Elements());
            Assert.Equal("b", bold.Name.LocalName);
            Assert.Equal(("i", "non bis in idem"), (Assert.Single(bold.Elements()).Name.LocalName, bold.Value));
        }

        [SchemaPlFact]
        public void SubscriptInUnitText_IsSchemaValid()
        {
            var xml = Export(Line(Run("Art. 1. Emisja CO"), Run("2", Subscript), Run(" nie przekracza normy.")));

            // Treść aktu bez nagłówka: fikstura nie ma nagłówka dziennika, więc metadane publikacji są puste.
            Assert.Empty(SchemaPl.Validator.ValidateActBody(xml.Elements().First()));
        }
    }
}
