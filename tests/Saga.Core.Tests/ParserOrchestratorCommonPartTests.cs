using DocumentFormat.OpenXml.Wordprocessing;
using Saga.Core.Services.Parsing;
using Saga.Model;
using Saga.Model.SystematizingUnits;

namespace Saga.Core.Tests
{
    public class ParserOrchestratorCommonPartTests
    {
        private ParsingContext CreateContext()
        {
            var document = new LegalDocument
            {
                Type = LegalActType.Statute,
                SourceJournal = new JournalInfo { Year = 2024, Positions = { 1 } }
            };
            var subchapter = new Subchapter();
            return new ParsingContext(document, subchapter);
        }

        private Paragraph CreateParagraph(string text, string? styleId = null)
        {
            var paragraph = new Paragraph();
            var run = new Run(new Text(text));
            paragraph.Append(run);

            if (styleId != null)
            {
                paragraph.ParagraphProperties = new ParagraphProperties
                {
                    ParagraphStyleId = new ParagraphStyleId { Val = styleId }
                };
            }

            return paragraph;
        }

        [Fact]
        public void ProcessParagraph_WrapUpPointStyle_AttachesToParagraph()
        {
            var orchestrator = new ParserOrchestrator();
            var context = CreateContext();

            var article = CreateParagraph("Art. 1. Podreczniki dopuszczone do uzytku szkolnego do:",
                "ARTartustawynprozporzdzenia");
            var point1 = CreateParagraph("1) ksztalcenia w zawodzie,", "PKTpunkt");
            var point2 = CreateParagraph("2) ksztalcenia specjalnego,", "PKTpunkt");
            var point3 = CreateParagraph("3) ksztalcenia uczniow...", "PKTpunkt");
            var wrapUp = CreateParagraph("- zachowuja dopuszczenie do uzytku szkolnego.",
                "CZWSPPKTczwsplnapunktw");

            orchestrator.ProcessParagraph(article, context);
            orchestrator.ProcessParagraph(point1, context);
            orchestrator.ProcessParagraph(point2, context);
            orchestrator.ProcessParagraph(point3, context);
            orchestrator.ProcessParagraph(wrapUp, context);

            var paragraph = context.CurrentParagraph;
            Assert.NotNull(paragraph);

            var wrapUpPart = paragraph.CommonParts.Single(cp => cp.Type == CommonPartType.WrapUp);
            Assert.Equal("- zachowuja dopuszczenie do uzytku szkolnego.", wrapUpPart.ContentText);
        }

        [Fact]
        public void ProcessParagraph_StylelessDashAfterUnpunctuatedPoint_IsWrapUp()
        {
            // Bez stylu CZ_WSP_*: ostatni punkt bez znaku końcowego zapowiada część wspólną (§ 57 ust. 3 ZTP)
            // — w korpusie typowo sankcja „– podlega karze…" zamykająca wyliczenie czynów.
            var orchestrator = new ParserOrchestrator();
            var context = CreateContext();

            orchestrator.ProcessParagraph(CreateParagraph("Art. 1. Kto:"), context);
            orchestrator.ProcessParagraph(CreateParagraph("1) narusza obowiązek zgłoszenia,"), context);
            orchestrator.ProcessParagraph(CreateParagraph("2) utrudnia przeprowadzenie kontroli"), context);
            orchestrator.ProcessParagraph(CreateParagraph("– podlega karze grzywny."), context);

            var paragraph = context.CurrentParagraph;
            Assert.NotNull(paragraph);

            var wrapUpPart = paragraph.CommonParts.Single(cp => cp.Type == CommonPartType.WrapUp);
            Assert.Equal("– podlega karze grzywny.", wrapUpPart.ContentText);
            Assert.All(paragraph.Points, point => Assert.Empty(point.Letters));
        }

        [Fact]
        public void ProcessParagraph_StylelessDashAfterPointEndingWithYearAbbreviation_IsWrapUp()
        {
            // Kropka w „2026 r." należy do skrótu — punkt nie ma znaku końcowego (338414_3001786, art. 8).
            var orchestrator = new ParserOrchestrator();
            var context = CreateContext();

            orchestrator.ProcessParagraph(CreateParagraph("Art. 1. Do umów zawartych:"), context);
            orchestrator.ProcessParagraph(CreateParagraph("1) przed dniem 1 stycznia 2026 r.,"), context);
            orchestrator.ProcessParagraph(CreateParagraph("2) w następstwie postępowań wszczętych przed dniem 1 stycznia 2026 r."), context);
            orchestrator.ProcessParagraph(CreateParagraph("– stosuje się przepisy dotychczasowe."), context);

            var wrapUpPart = context.CurrentParagraph!.CommonParts.Single(cp => cp.Type == CommonPartType.WrapUp);
            Assert.Equal("– stosuje się przepisy dotychczasowe.", wrapUpPart.ContentText);
        }

        [Theory]
        // § 56 ust. 3 ZTP: gdy wyliczenie punktów kończy część wspólna, punkty przed ostatnim kończą się przecinkiem.
        [InlineData(",", true)]
        [InlineData(";", false)]
        public void ProcessParagraph_StylelessDashAfterTheLettersOfTheLastPoint_ClosesThePoints_WhenPointsEndWithCommas(
            string pointEnding, bool closesThePoints)
        {
            // Korpus: myślnik za ostatnią literą ostatniego punktu — 88 części wspólnych punktów w 65 tekstach
            // jednolitych wychodziło bez stylów jako część wspólna liter (ADR-0074).
            var orchestrator = new ParserOrchestrator();
            var context = CreateContext();

            orchestrator.ProcessParagraph(CreateParagraph("Art. 1. 1. Zwolnienie przysługuje:"), context);
            orchestrator.ProcessParagraph(CreateParagraph("1) osobie, która ukończyła 26 lat" + pointEnding), context);
            orchestrator.ProcessParagraph(CreateParagraph("2) osobie, która:"), context);
            orchestrator.ProcessParagraph(CreateParagraph("a) prowadzi gospodarstwo rolne,"), context);
            orchestrator.ProcessParagraph(CreateParagraph("b) uzyskuje dochód z gospodarstwa"), context);
            orchestrator.ProcessParagraph(CreateParagraph("– jeżeli złożyła wniosek w terminie."), context);

            var paragraph = context.CurrentParagraph!;
            var lastPoint = paragraph.Points[^1];
            Assert.Equal(closesThePoints, paragraph.CommonParts.Any(cp => cp.Type == CommonPartType.WrapUp));
            Assert.Equal(!closesThePoints, lastPoint.CommonParts.Any(cp => cp.Type == CommonPartType.WrapUp));
            Assert.All(lastPoint.Letters, letter => Assert.Empty(letter.Tirets));
        }

        [Fact]
        public void ProcessParagraph_StylelessDashAfterTheLettersOfAMiddlePoint_StaysWithTheLetters()
        {
            // Po części wspólnej przychodzi kolejny punkt — zamykała więc litery, nie punkty (CommonPartPlacement).
            var orchestrator = new ParserOrchestrator();
            var context = CreateContext();

            orchestrator.ProcessParagraph(CreateParagraph("Art. 1. 1. Zwolnienie przysługuje:"), context);
            orchestrator.ProcessParagraph(CreateParagraph("1) osobie, która ukończyła 26 lat,"), context);
            orchestrator.ProcessParagraph(CreateParagraph("2) osobie, która:"), context);
            orchestrator.ProcessParagraph(CreateParagraph("a) prowadzi gospodarstwo rolne,"), context);
            orchestrator.ProcessParagraph(CreateParagraph("b) uzyskuje dochód z gospodarstwa"), context);
            orchestrator.ProcessParagraph(CreateParagraph("– jeżeli złożyła wniosek w terminie,"), context);
            orchestrator.ProcessParagraph(CreateParagraph("3) osobie niepełnosprawnej"), context);
            orchestrator.ProcessParagraph(CreateParagraph("– na jej wniosek."), context);

            var paragraph = context.CurrentParagraph!;
            Assert.Equal("– jeżeli złożyła wniosek w terminie,",
                Assert.Single(paragraph.Points[1].CommonParts, cp => cp.Type == CommonPartType.WrapUp).ContentText);
            Assert.Equal("– na jej wniosek.",
                Assert.Single(paragraph.CommonParts, cp => cp.Type == CommonPartType.WrapUp).ContentText);
        }

        [Fact]
        public void ProcessParagraph_StylelessDashAfterATiretEndingWithAConjunction_DoesNotClosePoints()
        {
            // Punkty kończą się przecinkiem (ADR-0074), ale ostatnie tiret kończy spójnik — myślnik to kolejne tiret.
            var orchestrator = new ParserOrchestrator();
            var context = CreateContext();

            orchestrator.ProcessParagraph(CreateParagraph("Art. 1. 1. Zwolnienie przysługuje:"), context);
            orchestrator.ProcessParagraph(CreateParagraph("1) osobie, która ukończyła 26 lat,"), context);
            orchestrator.ProcessParagraph(CreateParagraph("2) osobie, która:"), context);
            orchestrator.ProcessParagraph(CreateParagraph("a) prowadzi gospodarstwo:"), context);
            orchestrator.ProcessParagraph(CreateParagraph("– rolne lub"), context);
            orchestrator.ProcessParagraph(CreateParagraph("– leśne"), context);

            var paragraph = context.CurrentParagraph!;
            Assert.DoesNotContain(paragraph.CommonParts, cp => cp.Type == CommonPartType.WrapUp);
            Assert.Equal(2, paragraph.Points[1].Letters[0].Tirets.Count);
        }

        [Fact]
        public void ProcessParagraph_StylelessDashAfterPunctuatedPoint_IsNotWrapUp()
        {
            // Po elemencie zakończonym przecinkiem myślnik bywa w korpusie legendą wzoru („…wynosi 1,"
            // → „– wskaźnik…"), nie częścią wspólną — reguła kontekstowa go nie przejmuje.
            var orchestrator = new ParserOrchestrator();
            var context = CreateContext();

            orchestrator.ProcessParagraph(CreateParagraph("Art. 1. Kwotę oblicza się według wzoru, w którym:"), context);
            orchestrator.ProcessParagraph(CreateParagraph("1) wskaźnik przyjmuje się w wysokości 1,"), context);
            orchestrator.ProcessParagraph(CreateParagraph("– wartość nominalna przekazanych papierów,"), context);

            var paragraph = context.CurrentParagraph;
            Assert.NotNull(paragraph);
            Assert.DoesNotContain(paragraph.CommonParts, cp => cp.Type == CommonPartType.WrapUp);
        }
    }
}
