using Saga.Core.Helpers;
using Saga.Core.Ingest;
using Saga.Core.Services.Classify;
using Saga.Core.Services.Parsing;
using Saga.Core.Services.Parsing.Builders;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Akapit złożony stylem jednostki, ale bez oznaczenia w treści (ADR-0039): jest dalszą treścią jednostki,
    /// za którą stoi. Jednostką bez odczytanego oznaczenia (ADR-0024) zostaje akapit z oznaczeniem spoza
    /// wzorca i akapit, przed którym nie ma jednostki do kontynuowania.
    /// </summary>
    public class UndesignatedUnitTests
    {
        private static DocumentBlock Block(string text, string? style = null) => new() { Text = text, StyleId = style };

        private static LegalDocument Parse(params DocumentBlock[] blocks) => LegalDocumentParser.ParseBlocks(blocks.ToList());

        // ============================================================
        // Co wygląda na oznaczenie
        // ============================================================

        [Theory]
        [InlineData("1. Treść ustępu.")]
        [InlineData("1a) treść punktu;")]
        [InlineData("a) treść litery,")]
        [InlineData("– treść tiretu,")]
        // Spoza wzorców klasyfikatora, a jednak oznaczenie.
        [InlineData("A) wyliczenie wersalikiem,")]
        [InlineData("l) litera w miejscu punktu,")]
        [InlineData("ł[1]) litera wstawiona za „ł)” z indeksem górnym,")]
        [InlineData("3-5. (uchylone)")]
        [InlineData("3.8. Zajęcia dodatkowe")]
        [InlineData("2.1 wykorzystuje umiejętności cyfrowe;")]
        [InlineData("IV. Postanowienia końcowe")]
        [InlineData("‒ tiret pisany myślnikiem cyfrowym,")]
        [InlineData("–[35)] ukaraną za wykroczenie,")]
        [InlineData("1)[2)] treść z odnośnikiem;")]
        [InlineData("„2. Treść cytowana.")]
        public void TextOpeningWithADesignation_LooksDesignated(string text) =>
            Assert.True(UnitDesignation.LooksDesignated(text));

        [Theory]
        [InlineData("w którym poszczególne symbole oznaczają:")]
        [InlineData("Wychowanie fizyczne służy wszechstronnemu rozwojowi słuchaczy.")]
        [InlineData("albo")]
        [InlineData("2025 r. jest rokiem rozliczeniowym.")]
        [InlineData("15019 Wspieranie polskiego eksportu")]
        [InlineData("* Dotyczy aparatów z wkładem chłodzącym.")]
        public void ProseOrALooseNumber_DoesNotLookDesignated(string text) =>
            Assert.False(UnitDesignation.LooksDesignated(text));

        // ============================================================
        // Treść aktu
        // ============================================================

        [Theory]
        [InlineData("UST")]
        [InlineData("PKT")]
        [InlineData("LIT")]
        [InlineData("TIR")]
        public void ParagraphInAUnitStyleWithoutADesignation_ContinuesTheDeepestOpenUnit(string style)
        {
            var document = Parse(
                Block("Art. 1. 1. Przepis wprowadzający:"),
                Block("1) pierwszy punkt;"),
                Block("zdanie bez oznaczenia jednostki", style),
                Block("2) drugi punkt."));

            var paragraph = document.Articles.Single().Paragraphs.Single();
            Assert.Equal(new[] { "1", "2" }, paragraph.Points.Select(point => point.Number?.Value));
            Assert.Equal("zdanie bez oznaczenia jednostki", Assert.Single(paragraph.Points[0].Continuation).Text);
        }

        [Fact]
        public void PolishLettersWithASuperscript_InTheLetterStyle_AreLetters()
        {
            // Korpus: 317038_2843903 — „ł[1])" i „ł[2])" za „ł)": wzorzec litery zna tylko „[a-z]", a kształt
            // oznaczenia nie znał indeksu za literą, więc pierwsza wychodziła częścią wspólną przerywającą
            // wyliczenie, a druga dalszą treścią litery „ł".
            var document = Parse(
                Block("Art. 4. Płatnikami składek są:"),
                Block("1) w odniesieniu do osób:", "PKT"),
                Block("l) bezrobotnych – starosta,", "LIT"),
                Block("ł) pobierających zasiłek – powiatowy urząd pracy,", "LIT"),
                Block("ł[1]) pobierających świadczenie integracyjne – centrum integracji społecznej,", "LIT"),
                Block("ł[2]) odbywających studia podyplomowe – powiatowy urząd pracy,", "LIT"),
                Block("m) pozostałych – Zakład.", "LIT"));

            var point = document.Articles.Single().Paragraphs.Single().Points.Single();
            Assert.Equal(new[] { "lit_l", "lit_ł", "lit_ł[1]", "lit_ł[2]", "lit_m" },
                point.Letters.Select(letter => letter.Id![(letter.Id!.LastIndexOf("__", StringComparison.Ordinal) + 2)..]));
            Assert.DoesNotContain(point.CommonParts, part => part.Type == CommonPartType.WrapUp);
            Assert.All(point.Letters, letter => Assert.Empty(letter.Continuation));
        }

        [Fact]
        public void TiretWrittenWithAFigureDash_StaysATiret()
        {
            // Korpus: 329862_3033439 — „‒" (U+2012) nie mieści się we wzorcu tiretu; rozpoznaje go styl.
            var document = Parse(
                Block("Art. 1. W ustawie wprowadza się następujące zmiany:"),
                Block("1) w art. 5:"),
                Block("a) w ust. 2:"),
                Block("‒ wprowadzenie do wyliczenia otrzymuje nowe znaczenie,", "TIR"),
                Block("‒ pkt 3 traci znaczenie,", "TIR"));

            var letter = document.Articles.Single().Paragraphs.Single().Points.Single().Letters.Single();
            Assert.Equal(2, letter.Tirets.Count);
            Assert.Empty(letter.Continuation);
        }

        [Fact]
        public void ParagraphStyleAfterABareArticleLine_IsTheContentOfItsFirstParagraph()
        {
            // „Art. 5." w osobnym wierszu: pusty ustęp niejawny czeka na treść — to nie jest dalsza treść.
            var document = Parse(
                Block("Art. 5."),
                Block("Organ prowadzi rejestr.", "UST"));

            var paragraph = Assert.Single(document.Articles.Single().Paragraphs);
            Assert.Equal("Organ prowadzi rejestr.", paragraph.ContentText);
            Assert.Empty(paragraph.Continuation);
        }

        // ============================================================
        // Treść cytowana
        // ============================================================

        private static CollectedAmendmentParagraph Quoted(string text, string? style = null) =>
            new(text, style, AmendmentStyleDecoder.DecodeByStyleId(style));

        private static AmendmentContent Build(params CollectedAmendmentParagraph[] paragraphs) =>
            new AmendmentBuilder().Build(new AmendmentBuildInput(paragraphs, null, AmendmentOperationType.Modification));

        [Fact]
        public void QuotedParagraphInAUnitStyleWithoutADesignation_ContinuesTheQuotedUnit()
        {
            // Korpus: 324346_2978884 — akapity prozy w stylu punktu za cytowaną jednostką.
            var content = Build(
                Quoted("2. Przedmiot realizuje następujące cele:"),
                Quoted("Wychowanie fizyczne służy wszechstronnemu rozwojowi słuchaczy.", "ZPKTzmpktartykuempunktem"),
                Quoted("Zajęcia odbywają się w grupach.", "ZPKTzmpktartykuempunktem"));

            var paragraph = Assert.Single(content.Paragraphs);
            Assert.Empty(paragraph.Points);
            Assert.Equal(
                new[] { "Wychowanie fizyczne służy wszechstronnemu rozwojowi słuchaczy.", "Zajęcia odbywają się w grupach." },
                paragraph.Continuation.Select(block => block.Text));
        }

        [Fact]
        public void QuotedParagraphOpeningTheQuotation_StaysAUnit()
        {
            // Bez jednostki przed sobą akapit otwiera cytat: cytat strukturalny nie staje się cytatem tekstu.
            var content = Build(Quoted("Wychowanie fizyczne", "ZPKTzmpktartykuempunktem"));

            Assert.Single(content.Points);
            Assert.Null(content.PlainText);
        }

        [Fact]
        public void QuotedItemWithAMultiLevelNumber_StaysAUnit()
        {
            // Korpus: 343572_3168483 — „pkt 2.1 i 2.2 otrzymują brzmienie": pozycje są rodzeństwem.
            var content = Build(
                Quoted("2.1 wykorzystuje umiejętności cyfrowe;", "ZPKTzmpktartykuempunktem"),
                Quoted("2.2 porównuje własne zasoby z wymaganiami rynku pracy;", "ZPKTzmpktartykuempunktem"));

            Assert.Equal(2, content.Points.Count);
        }

        // ============================================================
        // Akapity w stylu zmiany pod przepisem, który niczego nie zmienia
        // ============================================================

        [Fact]
        public void LinesInAmendmentStyle_UnderAProvisionThatAmendsNothing_AreItsFurtherContent_InOrder()
        {
            // Korpus: 352078_3158919 — wzór i jego objaśnienie złożone stylem zmiany, rozdzielone akapitem w stylu
            // ustępu. Przepis niczego nie zmienia, więc to nie nowelizacja, tylko dalsza treść ustępu (ADR-0071) —
            // w kolejności z aktu.
            var document = Parse(
                Block("Art. 1. 1. Wysokość rekompensaty ustala się zgodnie z następującym wzorem:", "ARTartustawynprozporzdzenia"),
                Block("R = (KF – KT) + KP", "ZFRAGzmfragmentunpzdaniaartykuempunktem"),
                Block("gdzie:", "USTustnpkodeksu"),
                Block("KF – oznacza koszt faktyczny,", "ZFRAGzmfragmentunpzdaniaartykuempunktem"),
                Block("2. Rekompensatę wypłaca się co miesiąc.", "USTustnpkodeksu"));

            var first = document.Articles.Single().Paragraphs[0];
            Assert.Null(first.Amendment);
            Assert.Equal(["R = (KF – KT) + KP", "gdzie:", "KF – oznacza koszt faktyczny,"], first.Continuation.Select(block => block.Text));
            Assert.Equal("2", document.Articles.Single().Paragraphs[1].Number!.Value);
        }
    }
}
