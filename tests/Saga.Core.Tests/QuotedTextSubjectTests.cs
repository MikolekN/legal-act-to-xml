using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Core.Services.Parsing;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Zdanie albo akapit jako przedmiot komendy (ADR-0079): treść cytowana jest tekstem — styl części wspólnej, którym
    /// szablon składa cytowane zdania, ani oznaczenie na początku cytowanego akapitu nie czynią z niej jednostek.
    /// </summary>
    public class QuotedTextSubjectTests
    {
        [Theory]
        // Korpus: 329091_3181227 — zmiany tekstu załącznika rozporządzenia.
        [InlineData("– w pkt 1.4.1 w akapicie pierwszym zdanie pierwsze otrzymuje brzmienie:")]
        [InlineData("– w pkt 3.2.15 akapit ósmy otrzymuje brzmienie:")]
        [InlineData("– w pkt 5.2.24 akapity czwarty i piąty otrzymują brzmienie:")]
        [InlineData("– po akapicie trzecim dodaje się akapity czwarty i piąty w brzmieniu:")]
        [InlineData("– – akapity od pierwszego do trzeciego otrzymują brzmienie:")]
        [InlineData("– w pkt 8.1 akapit dwudziesty piąty otrzymuje brzmienie:")]
        [InlineData("– dodaje się akapit w brzmieniu:")]
        [InlineData("a) w ust. 7 dodaje się zdanie trzecie w brzmieniu:")]
        [InlineData("Zdanie drugie otrzymuje brzmienie:")]
        public void SentenceOrParagraphBeforeTheCommand_IsATextSubject(string command)
        {
            Assert.True(QuotedTextSubject.Names(command));
        }

        [Theory]
        // „zdania" nie jest przedmiotem — przedmiotem jest „pkt 8 i 9".
        [InlineData("w § 1 w pkt 7 kropkę na końcu zdania zastępuje się średnikiem i dodaje się pkt 8 i 9 w brzmieniu:")]
        // Przedmiot mieszany: akapit i rysunek.
        [InlineData("– dodaje się akapit szósty i rysunek 6.3.2.10a w brzmieniu:")]
        // Miejscownik nie nazywa przedmiotu.
        [InlineData("– w akapicie drugim dodaje się pkt 4 w brzmieniu:")]
        [InlineData("w ust. 2 pkt 3 otrzymuje brzmienie:")]
        [InlineData("w ust. 2 zdanie pierwsze")]
        public void OtherSubjects_AreNotTextSubjects(string command)
        {
            Assert.False(QuotedTextSubject.Names(command));
        }

        private const string CommonPartQuoteStyle = "ZCZWSPPKTzmczciwsppktartykuempunktem";

        private static LegalDocument Parse(params DocumentBlock[] body)
        {
            DocumentBlock[] heading =
            [
                new() { Text = "USTAWA" },
                new() { Text = "z dnia 14 czerwca 2024 r." },
                new() { Text = "o zmianie ustawy o opłatach" },
                new() { Text = "Art. 1. W ustawie z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) wprowadza się następujące zmiany:", StyleId = "ARTartustawynprozporzdzenia" },
            ];
            DocumentBlock end = new() { Text = "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.", StyleId = "ARTartustawynprozporzdzenia" };
            return LegalDocumentParser.Parse([.. heading, .. body, end], new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
        }

        private static DocumentBlock Point(string text) => new() { Text = text, StyleId = "PKTpunkt" };

        private static DocumentBlock Quote(string text) => new() { Text = text, StyleId = CommonPartQuoteStyle };

        [Fact]
        public void QuotedSentenceInACommonPartStyle_IsText_NotACommonPart()
        {
            var document = Parse(
                Point("1) w art. 5 w ust. 7 zdanie pierwsze otrzymuje brzmienie:"),
                Quote("„Opłatę wnosi się w terminie 14 dni.”;"),
                Point("2) w art. 6 dodaje się ust. 3 w brzmieniu:"),
                new DocumentBlock { Text = "„3. Opłata jest dochodem gminy.”.", StyleId = "ZUSTzmustartykuempunktem" });

            var points = document.Articles.First().Paragraphs.Single().Points;
            var content = points[0].Amendment!.Content!;
            Assert.Equal("Opłatę wnosi się w terminie 14 dni.", content.PlainText);
            Assert.True(content.PlainTextIsSentences);
            Assert.Empty(content.CommonParts);

            // Komenda o jednostce nadal buduje jednostkę.
            Assert.Single(points[1].Amendment!.Content!.Paragraphs);
        }

        [Fact]
        public void QuotedParagraphsOpeningWithDesignations_AreText_NotUnits()
        {
            // Akapity tekstu załącznika bywają wyliczeniem („1) …") — przedmiotem komendy są akapity, nie punkty aktu.
            var document = Parse(
                Point("1) w załączniku w pkt 2.2.2 akapity piąty i szósty otrzymują brzmienie:"),
                Quote("„1) Znak stosuje się przed przejazdem.”,"),
                Quote("2) Znak umieszcza się po prawej stronie jezdni.”."));

            var content = document.Articles.First().Paragraphs.Single().Points[0].Amendment!.Content!;
            Assert.Empty(content.Points);
            Assert.Empty(content.CommonParts);
            Assert.Contains("1) Znak stosuje się przed przejazdem.", content.PlainText);
            Assert.Contains("2) Znak umieszcza się po prawej stronie jezdni.", content.PlainText);
        }

        [Fact]
        public void QuotedSentenceWithAnEnumeration_KeepsItsPoints()
        {
            // Korpus: 338285_3000542 — zdanie z wyliczeniem obejmuje jego pozycje, a te są punktami zmienianego ustępu.
            var document = Parse(
                Point("1) w art. 6 w ust. 2 zdanie drugie otrzymuje brzmienie:"),
                new DocumentBlock { Text = "„Jeżeli w tym okresie nauczyciel:", StyleId = "ZFRAGzmfragmentunpzdaniaartykuempunktem" },
                new DocumentBlock { Text = "1) był nauczycielem akademickim,", StyleId = "ZPKTzmpktartykuempunktem" },
                new DocumentBlock { Text = "2) zajmował stanowisko dyrektora", StyleId = "ZPKTzmpktartykuempunktem" },
                new DocumentBlock { Text = "– oceny dokonuje się za okres pracy w szkole.”.", StyleId = "ZCZWSPPKTzmczciwsppktartykuempunktem" });

            var content = document.Articles.First().Paragraphs.Single().Points[0].Amendment!.Content!;
            Assert.Equal(2, content.Points.Count);
        }

        [Fact]
        public void QuotedSentence_IsExportedAsText_WithoutAnInventedCommonPart()
        {
            var document = Parse(
                Point("1) w art. 5 w ust. 7 zdanie pierwsze otrzymuje brzmienie:"),
                Quote("„Opłatę wnosi się w terminie 14 dni.”."));

            var xml = LegalDocumentConverter.Convert(document);

            Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName.StartsWith("czesc-wspolna", StringComparison.Ordinal));
            Assert.Contains(xml.Descendants(), e => e.Name.LocalName == "cytat-w-tekscie" && e.Value.Contains("Opłatę wnosi się w terminie 14 dni."));
        }

        [SchemaPlFact]
        public void QuotedSentence_IsSchemaValid()
        {
            var document = Parse(
                Point("1) w art. 5 w ust. 7 zdanie pierwsze otrzymuje brzmienie:"),
                Quote("„Opłatę wnosi się w terminie 14 dni.”."));

            Assert.Empty(SchemaPl.Validator.ValidateActBody(LegalDocumentConverter.Convert(document).Elements().First()));
        }
    }
}
