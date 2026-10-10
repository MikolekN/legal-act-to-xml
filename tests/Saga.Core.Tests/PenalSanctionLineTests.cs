using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Sankcja karna zapisana w osobnym akapicie („Kto …, || podlega grzywnie."). Jest dalszym ciągiem
    /// zdania przepisu — bez niej przepis karny nie ma kary.
    /// </summary>
    public class PenalSanctionLineTests
    {
        private const string SanctionStyle = "SKARNsankcjakarnawszczeglnociwKodeksiekarnym";

        private static LegalDocument Parse(params (string Text, string? Style)[] blocks) =>
            LegalDocumentParser.ParseBlocks(
                blocks.Select(block => new DocumentBlock { Text = block.Text, StyleId = block.Style }).ToList());

        [Fact]
        public void SanctionLine_ContinuesTheSentenceOfTheProvision()
        {
            // Ustawa o ochronie sygnalistów, art. 54.
            var document = Parse(
                ("Art. 54. 1. Kto, chcąc, aby inna osoba nie dokonała zgłoszenia, uniemożliwia jej to lub istotnie utrudnia,", null),
                ("podlega grzywnie, karze ograniczenia wolności albo pozbawienia wolności do roku.", SanctionStyle),
                ("2. Jeżeli sprawca czynu określonego w ust. 1 stosuje wobec innej osoby przemoc, groźbę bezprawną lub podstęp,", null),
                ("podlega karze pozbawienia wolności do lat 3.", SanctionStyle));

            var paragraphs = document.Articles.Single().Paragraphs;

            Assert.Equal(2, paragraphs.Count);
            Assert.Equal(
                "Kto, chcąc, aby inna osoba nie dokonała zgłoszenia, uniemożliwia jej to lub istotnie utrudnia, " +
                "podlega grzywnie, karze ograniczenia wolności albo pozbawienia wolności do roku.",
                paragraphs[0].ContentText);
            Assert.Single(paragraphs[0].TextSegments);
            Assert.EndsWith("podlega karze pozbawienia wolności do lat 3.", paragraphs[1].ContentText);
            Assert.DoesNotContain(paragraphs.SelectMany(p => p.ValidationMessages), m => m.Message.Contains("Pominięty akapit"));
        }

        [Fact]
        public void SanctionLine_IsRecognizedWithoutItsStyle_WhenTheProvisionIsUnfinished()
        {
            // TXT i PDF nie mają stylów: rozstrzyga początek „podlega" i przepis urwany na przecinku.
            var document = Parse(
                ("Art. 1. Kto utrudnia kontrolę,", null),
                ("podlega karze grzywny.", null),
                ("Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.", null));

            Assert.Equal("Kto utrudnia kontrolę, podlega karze grzywny.", document.Articles.First().Paragraphs.Single().ContentText);
        }

        [Fact]
        public void SanctionAfterTheDash_KeepsTheDash()
        {
            var document = Parse(
                ("Art. 1. Kto narusza zakaz określony w ustawie – Prawo prasowe", null),
                ("– podlega karze pozbawienia wolności od 6 miesięcy do lat 8.", SanctionStyle));

            var paragraph = document.Articles.Single().Paragraphs.Single();

            Assert.Equal(
                "Kto narusza zakaz określony w ustawie – Prawo prasowe – podlega karze pozbawienia wolności od 6 miesięcy do lat 8.",
                paragraph.ContentText);
            Assert.Empty(paragraph.Points);
        }

        [Fact]
        public void SanctionAfterAnEnumeration_IsItsCommonPart()
        {
            var document = Parse(
                ("Art. 1. Kto wbrew obowiązkowi:", null),
                ("1) nie prowadzi rejestru,", null),
                ("2) nie przechowuje wyników pomiarów w wymaganym okresie,", null),
                ("podlega karze grzywny.", SanctionStyle));

            var paragraph = document.Articles.Single().Paragraphs.Single();

            Assert.Equal(2, paragraph.Points.Count);
            Assert.Equal("podlega karze grzywny.",
                paragraph.CommonParts.Single(part => part.Type == CommonPartType.WrapUp).ContentText);

            var xml = LegalDocumentConverter.Convert(document);
            Assert.Equal("art_1__czw", xml.Descendants().Single(e => e.Name.LocalName == "czesc-wspolna-punkt").Attribute("eId")?.Value);
        }

        [Fact]
        public void QuotedSanctionLine_ContinuesTheQuotedProvision()
        {
            var document = Parse(
                ("Art. 1. W ustawie z dnia 6 czerwca 1997 r. – Kodeks karny (Dz. U. z 2025 r. poz. 383) po art. 224a dodaje się art. 224b w brzmieniu:", null),
                ("„Art. 224b. Kto niszczy, uszkadza, ukrywa lub usuwa znak graniczny,", "ZARTzmartartykuempunktem"),
                ("podlega karze grzywny do 720 stawek dziennych.”.", "ZSKARNzmsankcjikarnejwszczeglnociwKodeksiekarnym"),
                ("Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.", "ARTartustawynprozporzdzenia"));

            var quoted = document.Articles.First().Paragraphs.Single().Amendment!.Content!.Articles.Single();

            Assert.Equal(
                "Kto niszczy, uszkadza, ukrywa lub usuwa znak graniczny, podlega karze grzywny do 720 stawek dziennych.",
                quoted.Paragraphs.Single().ContentText);
        }

        [Theory]
        // Zdanie przepisu skończone — „podlega…" otwiera nowe zdanie, nie sankcję.
        [InlineData("Art. 1. Opłata jest dochodem budżetu państwa.", "Podlega ona zwrotowi w całości.")]
        // Wyliczenie tiretów: „– podlega …" po dwukropku to kolejny tiret.
        [InlineData("Art. 1. Zwolnieniu:", "– podlega przychód ze sprzedaży,")]
        public void OtherLinesStartingWithPodlega_AreNotSanctions(string provision, string next)
        {
            var document = Parse((provision, null), (next, null));

            Assert.DoesNotContain("odlega", document.Articles.Single().Paragraphs.First().TextSegments.First().Text);
        }
    }
}
