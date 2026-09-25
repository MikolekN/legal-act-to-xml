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
            Assert.Equal("zachowuja dopuszczenie do uzytku szkolnego.", wrapUpPart.ContentText);
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
            Assert.Equal("podlega karze grzywny.", wrapUpPart.ContentText);
            Assert.All(paragraph.Points, point => Assert.Empty(point.Letters));
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
