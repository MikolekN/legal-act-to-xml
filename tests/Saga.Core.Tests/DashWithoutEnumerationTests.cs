using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Model;
using Saga.Model.EditorialUnits;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Myślnik pod ustępem bez otwartego wyliczenia (ADR-0042): bez wprowadzenia zakończonego dwukropkiem
    /// i bez stylu tiretu akapit zaczynający się od myślnika jest dalszym ciągiem przepisu, nie tiretem pod
    /// dostawionym punktem i literą.
    /// </summary>
    public class DashWithoutEnumerationTests
    {
        private const string MathNamespace = "http://www.w3.org/1998/Math/MathML";

        private static DocumentBlock Text(string text, string? style = null) => new() { Text = text, StyleId = style };

        private static DocumentBlock Formula(string linear) => new()
        {
            Text = linear,
            FormulaMathMl = $"<math xmlns=\"{MathNamespace}\" display=\"block\"><mi>S</mi><mo>=</mo><mi>W</mi><mo>∙</mo><mi>C</mi></math>",
        };

        private static LegalDocument Parse(params DocumentBlock[] blocks)
        {
            var all = new List<DocumentBlock>
            {
                new() { Text = "DZIENNIK USTAW RZECZYPOSPOLITEJ POLSKIEJ", Role = BlockRole.JournalHeader },
                new() { Text = "Warszawa, dnia 24 czerwca 2024 r.", Role = BlockRole.JournalHeader },
                new() { Text = "Poz. 928", Role = BlockRole.JournalHeader },
                Text("USTAWA"), Text("z dnia 14 czerwca 2024 r."), Text("o opłatach"),
            };
            all.AddRange(blocks);
            return LegalDocumentParser.Parse(all, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
        }

        private static Paragraph FirstParagraph(LegalDocument document) => document.Articles.First().Paragraphs.First();

        private static int TiretCount(Paragraph paragraph) =>
            paragraph.Points.Sum(point => point.Letters.Sum(letter => letter.Tirets.Count));

        [Theory]
        // Korpus: 277432_2495860 art. 13k ust. 2d — ta sama konstrukcja co w ust. 2c, tylko bez stylu sankcji.
        [InlineData("USTustnpkodeksu")]
        [InlineData("LITlitera")]
        [InlineData(null)]
        public void DashAfterAnUnfinishedSentence_FinishesIt(string? style)
        {
            var document = Parse(
                Text("Art. 1. 1. Za niedołączenie dokumentów, o których mowa w art. 5 ust. 7"),
                Text("– wymierza się karę pieniężną w wysokości 1000 zł.", style),
                Text("2. Karę uiszcza się w terminie 14 dni."));

            var paragraph = FirstParagraph(document);
            Assert.Equal(
                "Za niedołączenie dokumentów, o których mowa w art. 5 ust. 7 – wymierza się karę pieniężną w wysokości 1000 zł.",
                paragraph.ContentText);
            Assert.Empty(paragraph.Points);
            Assert.Empty(paragraph.Continuation);
            Assert.Equal(2, document.Articles.First().Paragraphs.Count);
        }

        [Fact]
        public void DashAfterAnUnfinishedSentence_LeavesTheParagraphAnOrdinaryUnit()
        {
            var act = LegalDocumentConverter.Convert(Parse(
                Text("Art. 1. 1. Za niedołączenie dokumentów, o których mowa w art. 5 ust. 7"),
                Text("– wymierza się karę pieniężną w wysokości 1000 zł."),
                Text("2. Karę uiszcza się w terminie 14 dni.")));

            // Przedtem: nie-ztp-ustep z tiretem pod dostawionym punktem i literą.
            Assert.DoesNotContain(act.Descendants(), e => e.Name.LocalName.StartsWith("nie-ztp-", StringComparison.Ordinal));
            Assert.DoesNotContain(act.Descendants(), e => e.Name.LocalName == "tiret");
            Assert.Equal(2, act.Descendants().Count(e => e.Name.LocalName == "ustep"));
        }

        [Fact]
        public void DashesAfterAnIntroductionWithAColon_AreTirets()
        {
            // Korpus: 322147_2895738 art. 52 — wyliczenie myślnikami wprost w ustępie; wprowadzenie kończy dwukropek.
            var paragraph = FirstParagraph(Parse(
                Text("Art. 1. 1. Mianowanie następuje po przesłużeniu w dotychczasowym stopniu:"),
                Text("– posterunkowego – 1 roku,"),
                Text("– sierżanta – 2 lat.")));

            Assert.Equal(2, TiretCount(paragraph));
            Assert.Equal("Mianowanie następuje po przesłużeniu w dotychczasowym stopniu:", paragraph.ContentText);
        }

        [Fact]
        public void DashInTiretStyle_IsATiretAlsoWithoutTheColon()
        {
            // Styl tiretu i myślnik to dwa zgodne sygnały — brak dwukropka jest wtedy usterką wprowadzenia.
            var paragraph = FirstParagraph(Parse(
                Text("Art. 1. 1. Zabezpieczenia dokonuje się, jeżeli stwierdzono"),
                Text("– brak płynności finansowej zobowiązanego,", "TIRtiret"),
                Text("– wyprzedaż majątku.", "TIRtiret")));

            Assert.Equal(2, TiretCount(paragraph));
        }

        [Fact]
        public void DashAfterAFormula_IntroducesItsLegend()
        {
            // Korpus: 315433_2813775 art. 31a ust. 2 — „– gdzie poszczególne symbole oznaczają:" w stylu litery.
            var paragraph = FirstParagraph(Parse(
                Text("Art. 1. 1. Wysokość składki ustala się zgodnie ze wzorem:"),
                Formula("S=W∙C,"),
                Text("– gdzie poszczególne symbole oznaczają:", "LITlitera"),
                Text("S – wysokość składki,"),
                Text("W – wolumen gazu.")));

            Assert.Empty(paragraph.Points);
            var formula = Assert.Single(paragraph.Continuation).Formula!;
            Assert.EndsWith("gdzie poszczególne symbole oznaczają:", formula.LegendIntroduction);
            Assert.Equal(new[] { "S", "W" }, formula.Legend.Select(symbol => symbol.Symbol));
        }

        [Fact]
        public void DashAfterAFormulaInAPoint_IntroducesItsLegend()
        {
            // Korpus: 300744_2694324 art. 12b ust. 1c — wzór stoi w punkcie, a „– gdzie …" otwierało pod nim tiret,
            // w którym legenda rozpadała się na fragmenty.
            var paragraph = FirstParagraph(Parse(
                Text("Art. 1. 1. Wysokość składki ustala się:"),
                Text("1) zgodnie ze wzorem:"),
                Formula("S=W∙C"),
                Text("– gdzie poszczególne symbole oznaczają:"),
                Text("S – wysokość składki,"),
                Text("W – wolumen gazu."),
                Text("2. Składkę uiszcza się w terminie 14 dni.")));

            var point = Assert.Single(paragraph.Points);
            Assert.Empty(point.Letters);
            var formula = Assert.Single(point.Continuation).Formula!;
            Assert.Equal("– gdzie poszczególne symbole oznaczają:", formula.LegendIntroduction);
            Assert.Equal(new[] { "S", "W" }, formula.Legend.Select(symbol => symbol.Symbol));
        }

        [Fact]
        public void DashAfterALegendEntryWithAColon_OpensTirets()
        {
            // Korpus: 357032_3162287 — „Sw – oznacza:" i dwa myślniki: wprowadzeniem jest wiersz legendy.
            var paragraph = FirstParagraph(Parse(
                Text("Art. 1. 1. Wysokość składki ustala się zgodnie ze wzorem:"),
                Formula("S=W∙C,"),
                Text("gdzie:"),
                Text("W – oznacza:"),
                Text("– roczną stopę procentową z pierwszego dnia roboczego,"),
                Text("– roczną stopę procentową obligacji.")));

            Assert.Equal(2, TiretCount(paragraph));
        }

        [Fact]
        public void DashAfterAFinishedSentence_IsFurtherContent_NotGluedToTheSentence()
        {
            var paragraph = FirstParagraph(Parse(
                Text("Art. 1. 1. Opłatę pobiera się raz w roku."),
                Text("– z wyjątkiem roku, w którym złożono wniosek."),
                Text("2. Opłaty nie zwraca się.")));

            Assert.Equal("Opłatę pobiera się raz w roku.", paragraph.ContentText);
            Assert.Empty(paragraph.Points);
            Assert.Equal("– z wyjątkiem roku, w którym złożono wniosek.", Assert.Single(paragraph.Continuation).Text);
        }

        [Fact]
        public void DashAfterAPointWithoutAClosingSign_IsStillTheCommonPart()
        {
            // Reguła dotyczy ustępu bez wyliczenia; po punkcie obowiązuje część wspólna (§ 57 ust. 3 ZTP).
            var paragraph = FirstParagraph(Parse(
                Text("Art. 1. 1. Kto:"),
                Text("1) narusza zakaz,"),
                Text("2) nie wykonuje obowiązku"),
                Text("– podlega karze grzywny.")));

            Assert.Equal(2, paragraph.Points.Count);
            Assert.Equal(0, TiretCount(paragraph));
            Assert.Contains(paragraph.CommonParts, part => part.ContentText.Contains("podlega karze grzywny"));
        }

        [SchemaPlFact]
        public void ActWithADashFinishingTheSentence_IsSchemaValid_AsAWhole()
        {
            Assert.Empty(SchemaPl.Validator.Validate(LegalDocumentConverter.Convert(Parse(
                Text("Art. 1. 1. Za niedołączenie dokumentów, o których mowa w art. 5 ust. 7"),
                Text("– wymierza się karę pieniężną w wysokości 1000 zł."),
                Text("2. Wysokość składki ustala się zgodnie ze wzorem:"),
                Formula("S=W∙C,"),
                Text("– gdzie poszczególne symbole oznaczają:"),
                Text("S – wysokość składki,"),
                Text("W – wolumen gazu."),
                Text("Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.")))));
        }
    }
}
