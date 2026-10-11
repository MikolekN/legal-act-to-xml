using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Model;
using Saga.Model.EditorialUnits;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Akapit w stylu części wspólnej (CZ_WSP_*) bez myślnika na początku: styl i miejsce tuż za wyliczeniem
    /// wystarczają, żeby zamknął wyliczenie, a akapit, który niczego nie zamyka, zostaje dalszą treścią —
    /// przedtem takie akapity ginęły z modelu i eksportu.
    /// </summary>
    public class CommonPartWithoutDashTests
    {
        private const string PointsCommonPart = "CZWSPPKTczwsplnapunktw";
        private const string LettersCommonPart = "CZWSPLITczwsplnaliter";

        private static DocumentBlock Text(string text, string? style = null) => new() { Text = text, StyleId = style };

        private static LegalDocument Parse(params DocumentBlock[] blocks)
        {
            var all = new List<DocumentBlock>
            {
                new() { Text = "DZIENNIK USTAW RZECZYPOSPOLITEJ POLSKIEJ", Role = BlockRole.JournalHeader },
                new() { Text = "Warszawa, dnia 24 czerwca 2024 r.", Role = BlockRole.JournalHeader },
                new() { Text = "Poz. 928", Role = BlockRole.JournalHeader },
                Text("USTAWA"), Text("z dnia 14 czerwca 2024 r."), Text("o służbie"),
            };
            all.AddRange(blocks);
            return LegalDocumentParser.Parse(all, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
        }

        private static Paragraph FirstParagraph(LegalDocument document) => document.Articles.First().Paragraphs.First();

        [Fact]
        public void StyledParagraphAfterPoints_ClosesThem_WithoutTheDash()
        {
            // Korpus: 299131_2682739 — „… w przypadku: 1) urlopu bezpłatnego, … 5) nieusprawiedliwionej
            // nieobecności na służbie" i w stylu części wspólnej „ulega proporcjonalnemu obniżeniu …".
            var paragraph = FirstParagraph(Parse(
                Text("Art. 1. 1. Urlop wypoczynkowy w przypadku:"),
                Text("1) urlopu bezpłatnego,"),
                Text("2) nieusprawiedliwionej nieobecności na służbie"),
                Text("ulega proporcjonalnemu obniżeniu o ten okres.", PointsCommonPart),
                Text("2. Urlopu udziela przełożony.")));

            Assert.Equal(2, paragraph.Points.Count);
            var closing = Assert.Single(paragraph.CommonParts, part => part.Type == CommonPartType.WrapUp);
            Assert.Equal("ulega proporcjonalnemu obniżeniu o ten okres.", closing.ContentText);
            Assert.Contains(paragraph.ValidationMessages, message => message.Message.Contains("nie zaczyna się od myślnika"));
        }

        [Fact]
        public void StyledParagraphAfterLetters_ClosesThem_WithoutTheDash()
        {
            // Korpus: 299137_2675687 — „jak również ujawnianie mienia …" za literami punktu.
            var paragraph = FirstParagraph(Parse(
                Text("Art. 1. 1. Do zadań służby należy:"),
                Text("1) rozpoznawanie przestępstw:"),
                Text("a) przeciwko pokojowi,"),
                Text("b) przeciwko obronności,"),
                Text("jak również ujawnianie mienia zagrożonego przepadkiem;", LettersCommonPart),
                Text("2) współdziałanie z innymi organami.")));

            var point = paragraph.Points.First();
            Assert.Equal(2, point.Letters.Count);
            Assert.Equal("jak również ujawnianie mienia zagrożonego przepadkiem;",
                Assert.Single(point.CommonParts, part => part.Type == CommonPartType.WrapUp).ContentText);
            Assert.Equal(2, paragraph.Points.Count);
        }

        // ============================================================
        // Bez stylu (ADR-0046): mała litera tuż za pozycją wyliczenia
        // ============================================================

        [Theory]
        // Korpus: 275050_2461922 — pozycje zakończone przecinkami, część wspólna bez myślnika (starsze akty).
        [InlineData("2) niewykonania rekultywacji gruntów w wyznaczonym terminie,")]
        // § 57 ust. 3 ZTP: ostatnia pozycja bez znaku końcowego.
        [InlineData("2) niewykonania rekultywacji gruntów w wyznaczonym terminie")]
        public void LowercaseParagraphAfterAnItem_ClosesTheEnumeration_WithoutStyleOrDash(string lastItem)
        {
            var paragraph = FirstParagraph(Parse(
                Text("Art. 1. 1. W przypadku:"),
                Text("1) zaniechania użytkowania gruntów,"),
                Text(lastItem),
                Text("stosuje się przepisy niniejszej ustawy."),
                Text("2. Opłaty wnosi się na rachunek urzędu.")));

            Assert.Equal(2, paragraph.Points.Count);
            Assert.Equal("stosuje się przepisy niniejszej ustawy.",
                Assert.Single(paragraph.CommonParts, part => part.Type == CommonPartType.WrapUp).ContentText);
            Assert.All(paragraph.Points, point => Assert.Empty(point.Continuation));
        }

        [Theory]
        // Wielka litera: nowe zdanie, nie dalszy ciąg wprowadzenia.
        [InlineData("2) niewykonania rekultywacji gruntów,", "Przepisy stosuje się odpowiednio.")]
        // Pozycja zakończona średnikiem albo kropką: kontekst nie rozstrzyga, czyje zdanie ciągnie akapit.
        [InlineData("2) niewykonania rekultywacji gruntów;", "stosuje się przepisy niniejszej ustawy.")]
        [InlineData("2) niewykonania rekultywacji gruntów.", "stosuje się przepisy niniejszej ustawy.")]
        public void OtherParagraphsAfterAnItem_StayItsFurtherContent(string lastItem, string next)
        {
            var paragraph = FirstParagraph(Parse(
                Text("Art. 1. 1. W przypadku:"),
                Text("1) zaniechania użytkowania gruntów,"),
                Text(lastItem),
                Text(next),
                Text("2. Opłaty wnosi się na rachunek urzędu.")));

            Assert.DoesNotContain(paragraph.CommonParts, part => part.Type == CommonPartType.WrapUp);
            Assert.Equal(next, Assert.Single(paragraph.Points.Last().Continuation).Text);
        }

        [Fact]
        public void LowercaseParagraphAfterLetters_ClosesTheLetters()
        {
            var paragraph = FirstParagraph(Parse(
                Text("Art. 1. 1. Do zadań służby należy:"),
                Text("1) rozpoznawanie przestępstw:"),
                Text("a) przeciwko pokojowi,"),
                Text("b) przeciwko obronności,"),
                Text("jak również ujawnianie mienia zagrożonego przepadkiem;"),
                Text("2) współdziałanie z innymi organami.")));

            var point = paragraph.Points.First();
            Assert.Equal("jak również ujawnianie mienia zagrożonego przepadkiem;",
                Assert.Single(point.CommonParts, part => part.Type == CommonPartType.WrapUp).ContentText);
            Assert.Equal(2, paragraph.Points.Count);
        }

        [Fact]
        public void StyledParagraphThatClosesNothing_IsFurtherContent_NotDropped()
        {
            // Styl mówi „część wspólna punktów", ale ustęp nie ma punktów: akapit nie ginie.
            var paragraph = FirstParagraph(Parse(
                Text("Art. 1. 1. Urlop wypoczynkowy przysługuje raz w roku."),
                Text("W szczególnych przypadkach urlopu udziela się w częściach.", PointsCommonPart),
                Text("2. Urlopu udziela przełożony.")));

            Assert.Empty(paragraph.CommonParts);
            Assert.Equal("W szczególnych przypadkach urlopu udziela się w częściach.", Assert.Single(paragraph.Continuation).Text);
        }

        [Fact]
        public void SecondStyledParagraph_IsFurtherContentOfTheCommonPart()
        {
            // Korpus: 311012_2800574 art. 99a ust. 4 — tekst jednolity niesie dwa brzmienia części wspólnej,
            // każde ze swoim odnośnikiem; drugie stawało się tiretem pod ostatnim punktem albo ginęło.
            var paragraph = FirstParagraph(Parse(
                Text("Art. 1. 1. W przypadku:"),
                Text("1) nabycia suszu tytoniowego przez inny podmiot, lub"),
                Text("2) sprzedaży suszu tytoniowego innemu podmiotowi"),
                Text("– bez jego oznaczenia znakami akcyzy, stawka akcyzy wynosi 812,52 zł za każdy kilogram.", PointsCommonPart),
                Text("– bez jego oznaczenia znakami akcyzy, stawka akcyzy wynosi 1336,10 zł za każdy kilogram.", PointsCommonPart),
                Text("2. Akcyzę wpłaca się na rachunek urzędu.")));

            var closing = Assert.Single(paragraph.CommonParts, part => part.Type == CommonPartType.WrapUp);
            Assert.EndsWith("812,52 zł za każdy kilogram.", closing.ContentText);
            Assert.EndsWith("1336,10 zł za każdy kilogram.", Assert.Single(closing.Continuation).Text);
            Assert.All(paragraph.Points, point => Assert.Empty(point.Letters));
        }

        [Fact]
        public void Export_KeepsTheSecondWordingInsideTheCommonPart()
        {
            var xml = LegalDocumentConverter.Convert(Parse(
                Text("Art. 1. 1. W przypadku:"),
                Text("1) nabycia suszu tytoniowego przez inny podmiot, lub"),
                Text("2) sprzedaży suszu tytoniowego innemu podmiotowi"),
                Text("– bez jego oznaczenia znakami akcyzy, stawka akcyzy wynosi 812,52 zł za każdy kilogram.", PointsCommonPart),
                Text("– bez jego oznaczenia znakami akcyzy, stawka akcyzy wynosi 1336,10 zł za każdy kilogram.", PointsCommonPart),
                Text("2. Akcyzę wpłaca się na rachunek urzędu.")));

            var commonPart = xml.Descendants().Single(e => e.Name.LocalName == "czesc-wspolna-punkt");
            Assert.Equal(new[] { "p", "fragment" }, commonPart.Elements().Select(e => e.Name.LocalName));
            Assert.EndsWith("1336,10 zł za każdy kilogram.", commonPart.Elements().Last().Value);
            Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName.EndsWith("tiret", StringComparison.Ordinal));
        }

        [SchemaPlFact]
        public void ActWithCommonPartsWithoutDashes_IsSchemaValid_AsAWhole()
        {
            Assert.Empty(SchemaPl.Validator.Validate(LegalDocumentConverter.Convert(Parse(
                Text("Art. 1. 1. Urlop wypoczynkowy w przypadku:"),
                Text("1) urlopu bezpłatnego,"),
                Text("2) nieusprawiedliwionej nieobecności na służbie"),
                Text("ulega proporcjonalnemu obniżeniu o ten okres.", PointsCommonPart),
                Text("2. W przypadku:"),
                Text("1) nabycia suszu tytoniowego przez inny podmiot, lub"),
                Text("2) sprzedaży suszu tytoniowego innemu podmiotowi"),
                Text("– bez jego oznaczenia, stawka wynosi 812,52 zł.", PointsCommonPart),
                Text("– bez jego oznaczenia, stawka wynosi 1336,10 zł.", PointsCommonPart),
                Text("Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.")))));
        }
    }
}
