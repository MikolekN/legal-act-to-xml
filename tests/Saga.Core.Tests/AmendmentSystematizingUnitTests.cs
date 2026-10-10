using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Model;
using Saga.Model.SystematizingUnits;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Jednostki systematyzacyjne w treści nowelizacji („po rozdziale 9 dodaje się rozdział 9a
    /// w brzmieniu:") — kształt jak w przykładzie autorów schematu (akn4pl, DU/2026/26):
    /// cytat-strukt → rozdzial → naglowek-rozdzialu → oddzial-n → artykul.
    /// </summary>
    public class AmendmentSystematizingUnitTests
    {
        private const string ArticleStyle = "ARTartustawynprozporzdzenia";
        private const string PointStyle = "PKTpunkt";
        private const string QuotedArticle = "ZARTzmartartykuempunktem";
        private const string QuotedParagraph = "ZUSTzmustartykuempunktem";
        private const string QuotedChapter = "ZROZDZODDZOZNzmoznrozdzoddzartykuempunktem";
        private const string QuotedChapterHeading = "ZROZDZODDZPRZEDMzmprzedmrozdzoddzartykuempunktem";

        private static LegalDocument Parse(params (string Text, string Style)[] lines) =>
            LegalDocumentParser.ParseBlocks(lines.Select(line => new DocumentBlock { Text = line.Text, StyleId = line.Style }).ToList());

        private static LegalDocument InsertedChapter() => Parse(
            ("Art. 1. W ustawie z dnia 25 czerwca 1999 r. o świadczeniach (Dz. U. z 2025 r. poz. 350) wprowadza się następujące zmiany:", ArticleStyle),
            ("1) po rozdziale 9 dodaje się rozdział 9a w brzmieniu:", PointStyle),
            ("„Rozdział 9a", QuotedChapter),
            ("Orzekanie dla celów świadczeń", QuotedChapterHeading),
            ("Art. 85a. 1. Zadania są realizowane w jednostkach terenowych.", QuotedArticle),
            ("2. Fizjoterapeuta może wydawać orzeczenia.", QuotedParagraph),
            ("Art. 85b. Orzeczenie wydaje się na wniosek.”;", QuotedArticle),
            ("2) uchyla się art. 86.", PointStyle),
            ("Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.", ArticleStyle));

        private static Amendment AmendmentOfPoint(LegalDocument document, string point) =>
            document.Articles.First().Paragraphs[0].Points.Single(p => p.Number?.Value == point).Amendment!;

        [Fact]
        public void QuotedChapter_IsAUnitWithItsHeading_AndHoldsTheQuotedArticles()
        {
            var content = AmendmentOfPoint(InsertedChapter(), "1").Content!;

            var chapter = Assert.IsType<Chapter>(Assert.Single(content.SystematizingUnits));
            Assert.Equal("rozdz_9a", chapter.Id);
            Assert.Equal("Orzekanie dla celów świadczeń", chapter.Heading);
            Assert.Empty(content.Articles);
            Assert.Null(content.PlainText);

            var subchapter = Assert.Single(chapter.Subchapters);
            Assert.True(subchapter.IsImplicit);
            Assert.Equal(new[] { "art_85a", "art_85b" }, subchapter.Articles.Select(a => a.Id));
            Assert.Equal("art_85a__ust_2", subchapter.Articles[0].Paragraphs[1].Id);
        }

        [Fact]
        public void QuotedChapter_IsExportedInsideCytatStrukt_WithMinusNSubchapter()
        {
            var xml = LegalDocumentConverter.Convert(InsertedChapter());
            var citation = xml.Descendants().Single(e => e.Name.LocalName == "cytat-strukt");
            XNamespace ns = citation.Name.Namespace;

            var chapter = citation.Elements().Single();
            Assert.Equal("rozdzial", chapter.Name.LocalName);
            Assert.Equal("DU/2025/350__rozdz_9a", chapter.Attribute("eId")?.Value);
            Assert.Equal("9a", chapter.Element(ns + "naglowek-rozdzialu")?.Element(ns + "nr")?.Value);
            Assert.Equal("Orzekanie dla celów świadczeń", chapter.Element(ns + "naglowek-rozdzialu")?.Element(ns + "p")?.Value);

            var subchapter = chapter.Element(ns + "oddzial-n")!;
            Assert.Equal(new[] { "DU/2025/350__art_85a", "DU/2025/350__art_85b" },
                subchapter.Elements(ns + "artykul").Select(a => a.Attribute("eId")?.Value));
            Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName == "cytat-w-tekscie");
        }

        [Fact]
        public void ChapterCommands_TargetTheChapter_InsertAfterAndRepeal()
        {
            var document = Parse(
                ("Art. 1. W ustawie z dnia 25 czerwca 1999 r. o świadczeniach (Dz. U. z 2025 r. poz. 350) wprowadza się następujące zmiany:", ArticleStyle),
                ("1) w dziale II po rozdziale 9 dodaje się rozdział 9a w brzmieniu:", PointStyle),
                ("„Rozdział 9a", QuotedChapter),
                ("Orzekanie", QuotedChapterHeading),
                ("Art. 85a. Treść.”;", QuotedArticle),
                ("2) uchyla się rozdziały 10 i 11.", PointStyle),
                ("Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.", ArticleStyle));

            // Rozdział cytowany zaczyna się od działu, w którym go dodano (ADR-0021).
            var chapter = Assert.IsType<Chapter>(Assert.Single(AmendmentOfPoint(document, "1").Content!.SystematizingUnits));
            Assert.Equal("dzial_2__rozdz_9a", chapter.Id);

            var xml = LegalDocumentConverter.Convert(document);
            var citation = xml.Descendants().Single(e => e.Name.LocalName == "cytat-strukt");
            Assert.Equal("DU/2025/350__dzial_2__rozdz_9", citation.Attribute("element-eId")?.Value);
            Assert.Equal("dodaj-po", citation.Attribute("funkcja")?.Value);
            Assert.Equal(new[] { "DU/2025/350__rozdz_10", "DU/2025/350__rozdz_11" },
                xml.Descendants().Where(e => e.Name.LocalName == "uchyl").Select(e => e.Attribute("element-eId")?.Value));
        }

        [Fact]
        public void QuotedDesignationAndTitle_IsAChapterWithHeaderOnly()
        {
            // Ustawa 302620_2697714: „po art. 7 dodaje się oznaczenie i tytuł rozdziału w brzmieniu:".
            var document = Parse(
                ("Art. 1. W ustawie z dnia 7 lipca 2017 r. o konsultacjach (Dz. U. z 2023 r. poz. 248) wprowadza się następujące zmiany:", ArticleStyle),
                ("1) po art. 7 dodaje się oznaczenie i tytuł rozdziału w brzmieniu:", PointStyle),
                ("„Rozdział 2a", QuotedChapter),
                ("Konsultacje społeczne”;", QuotedChapterHeading),
                ("2) uchyla się art. 8.", PointStyle),
                ("Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.", ArticleStyle));

            var chapter = Assert.IsType<Chapter>(Assert.Single(AmendmentOfPoint(document, "1").Content!.SystematizingUnits));
            Assert.Equal("Konsultacje społeczne", chapter.Heading);
            Assert.Empty(chapter.Subchapters);
        }

        [Fact]
        public void QuotedDivisionWithChapters_NestsChaptersUnderTheDivision()
        {
            var document = Parse(
                ("Art. 1. W ustawie z dnia 9 marca 2017 r. o wymianie informacji (Dz. U. z 2023 r. poz. 1) wprowadza się następujące zmiany:", ArticleStyle),
                ("1) po dziale III dodaje się dział IIIa w brzmieniu:", PointStyle),
                ("„Dział IIIa", "ZTYTDZOZNzmozntytuudziauartykuempunktem"),
                ("Automatyczna wymiana informacji o sprzedawcach", "ZTYTDZPRZEDMzmprzedmtytuulubdziauartykuempunktem"),
                ("Rozdział 1", QuotedChapter),
                ("Przepisy ogólne", QuotedChapterHeading),
                ("Art. 75a. Treść.", QuotedArticle),
                ("Rozdział 2", QuotedChapter),
                ("Obowiązki", QuotedChapterHeading),
                ("Art. 75b. Treść.”;", QuotedArticle),
                ("2) uchyla się art. 76.", PointStyle),
                ("Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.", ArticleStyle));

            var division = Assert.IsType<Division>(Assert.Single(AmendmentOfPoint(document, "1").Content!.SystematizingUnits));
            Assert.Equal("dzial_3a", division.Id);
            Assert.Equal(new[] { "dzial_3a__rozdz_1", "dzial_3a__rozdz_2" }, division.Chapters.Select(c => c.Id));
            Assert.Equal("art_75b", division.Chapters[1].Subchapters.Single().Articles.Single().Id);
        }
    }
}
