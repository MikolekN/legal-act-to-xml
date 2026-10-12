using Saga.Core.Services;
using Saga.Core.Services.Parsing;
using Saga.Model;
using Saga.Model.EditorialUnits;
using Saga.Model.SystematizingUnits;

namespace Saga.Core.Tests
{
    public class LegalReferenceServiceTests
    {
        private readonly LegalReferenceService _service = new();

        /// <summary>Jedyny cel wskazany w treści.</summary>
        private StructuralAmendmentReference SingleTarget(
            string text, StructuralReference? context = null, bool sectionSignIsBasicUnit = false) =>
            Assert.Single(_service.ReadAmendmentTargets(context, text, sectionSignIsBasicUnit));

        private string[] Targets(string text, StructuralReference? context = null) =>
            _service.ReadAmendmentTargets(context, text).Select(t => t.ToString()).ToArray();

        [Fact]
        public void ReadAmendmentTargets_DetectsArticle()
        {
            Assert.Equal("5", SingleTarget("w art. 5 dodaje się pkt 3").Structure.Article);
        }

        [Fact]
        public void ReadAmendmentTargets_DetectsParagraph()
        {
            Assert.Equal("2", SingleTarget("w ust. 2 pkt 1 otrzymuje brzmienie").Structure.Paragraph);
        }

        // ============================================================
        // Paragraf kodeksu — segment „par", nie „ust" (ADR-0014)
        // ============================================================

        [Fact]
        public void ReadAmendmentTargets_CodeParagraphAfterArticle_UsesPar()
        {
            // Korpus: 306701_2736647.
            Assert.Equal("art_53__par_30c", SingleTarget("w art. 53 § 30c otrzymuje brzmienie:").ToString());
        }

        [Fact]
        public void ReadAmendmentTargets_CodeParagraphUnderInheritedArticle_UsesPar()
        {
            // Artykuł przychodzi z kontekstu rodzica, „§" z komendy — wciąż paragraf kodeksu.
            var target = SingleTarget(
                "w § 2b wyrazy „art. 14a” zastępuje się wyrazami „art. 14a § 1”;", new StructuralReference { Article = "12" });

            Assert.Equal("art_12__par_2b", target.ToString());
        }

        [Fact]
        public void ReadAmendmentTargets_SectionSignOnlyInQuote_IsNotATarget()
        {
            // Korpus: 361298_3194530 — „§" występuje wyłącznie w cytowanym brzmieniu,
            // więc celem jest cały artykuł, a nie jego paragraf.
            var target = SingleTarget(
                "w art. 593 wyrazy „art. 586-592” zastępuje się wyrazami „art. 586-591 i art. 592 § 2 i 3”;");

            Assert.Equal("art_593", target.ToString());
        }

        [Fact]
        public void ReadAmendmentTargets_SectionSignWithoutArticle_InSectionBasedAct_IsTheMainUnit()
        {
            // W akcie opartym na paragrafach (rozporządzenie) samodzielny „§" to jednostka główna.
            var target = SingleTarget("w § 5 ust. 2 otrzymuje brzmienie:", sectionSignIsBasicUnit: true);

            Assert.Equal("art_5__ust_2", target.ToString());
            Assert.False(target.Structure.ParagraphIsCodeParagraph);
        }

        [Fact]
        public void ReadAmendmentTargets_SectionAddedAfterASection_TargetsTheSectionBefore()
        {
            // Korpus: 321948_2991133 — dodawane paragrafy są przedmiotem komendy, nie paragrafami kodeksu pod celem.
            var target = SingleTarget("po § 3 dodaje się § 3a i § 3b w brzmieniu:", sectionSignIsBasicUnit: true);

            Assert.Equal("art_3", target.ToString());
        }

        [Fact]
        public void ReadAmendmentTargets_EnumeratedSections_AreSeparateTargets()
        {
            // Korpus: 289896_2764903 i 335954_3006176.
            Assert.Equal(new[] { "art_6", "art_7" },
                _service.ReadAmendmentTargets(null, "§ 6 i § 7 otrzymują brzmienie:", sectionSignIsBasicUnit: true)
                    .Select(target => target.ToString()));

            Assert.Equal(new[] { "art_6a", "art_12", "art_14__ust_1", "art_14a__ust_1" },
                _service.ReadAmendmentTargets(
                        null,
                        "w § 6a, w § 12, w § 14 w ust. 1 i w § 14a w ust. 1 wyrazy „W latach” zastępuje się wyrazami „Do dnia”;",
                        sectionSignIsBasicUnit: true)
                    .Select(target => target.ToString()));
        }

        [Fact]
        public void ReadAmendmentTargets_SectionSignAfterExplicitArticle_InSectionBasedAct_StaysACodeParagraph()
        {
            var target = SingleTarget("w art. 5 § 2 otrzymuje brzmienie:", sectionSignIsBasicUnit: true);

            Assert.Equal("art_5__par_2", target.ToString());
        }

        [Fact]
        public void ReadAmendmentTargets_SectionSignWithoutArticle_InStatute_IsNotGuessed()
        {
            // Korpus: 311010_2798032. W ustawie „§" to paragraf kodeksu, a bez artykułu adres jest niepełny —
            // artykułu się nie zgaduje (wcześniej „w § 2" dawało fałszywy cel art_2).
            var targets = _service.ReadAmendmentTargets(null,
                "Jeżeli wniosek o zabezpieczenie w formie określonej w § 2 złożono po ustanowieniu zabezpieczenia " +
                "w trybie przepisów o postępowaniu egzekucyjnym w administracji");

            Assert.Empty(targets);
        }

        [Fact]
        public void ReadAmendmentTargets_OrdinaryParagraph_StaysUst()
        {
            Assert.Equal("art_5__ust_2__pkt_1", SingleTarget("w art. 5 ust. 2 pkt 1 otrzymuje brzmienie:").ToString());
        }

        [Fact]
        public void ReadAmendmentTargets_DetectsPoint()
        {
            Assert.Equal("3a", SingleTarget("po pkt 3a dodaje się pkt 3b").Structure.Point);
        }

        [Fact]
        public void ReadAmendmentTargets_DetectsLetter()
        {
            Assert.Equal("b", SingleTarget("w lit. b tiret drugie").Structure.Letter);
        }

        [Fact]
        public void ReadAmendmentTargets_DetectsMultipleLevels()
        {
            var target = SingleTarget("w art. 10 w ust. 3 pkt 2 otrzymuje brzmienie").Structure;

            Assert.Equal("10", target.Article);
            Assert.Equal("3", target.Paragraph);
            Assert.Equal("2", target.Point);
        }

        [Fact]
        public void ReadAmendmentTargets_ExplicitArticleInCommand_OverridesContext()
        {
            // Kontekst (artykuł z komendy nadrzędnej) uzupełnia tylko to, czego komenda nie mówi —
            // jawne „w art. 5" w adresie komendy wskazuje cel wprost (§ 89 ust. 2 ZTP).
            var target = SingleTarget("w art. 5 ust. 2 uchyla się pkt 3", new StructuralReference { Article = "7" });

            Assert.Equal("art_5__ust_2__pkt_3", target.ToString());
        }

        [Fact]
        public void ReadAmendmentTargets_CrossReferenceAfterAddress_DoesNotOverrideContext()
        {
            // „o którym mowa w art. 5" to odesłanie w prozie, nie kolejny cel: artykuł dołącza proza,
            // nie separator wyliczenia, więc adres kończy się na ustępie, a artykuł z kontekstu zostaje.
            var target = SingleTarget(
                "w ust. 2, o którym mowa w art. 5, dodaje się pkt 3", new StructuralReference { Article = "7" });

            Assert.Equal("art_7__ust_2", target.ToString());
        }

        [Fact]
        public void ReadAmendmentTargets_NoMatch_ReadsNothing()
        {
            Assert.Empty(_service.ReadAmendmentTargets(null, "Przepis stosuje się odpowiednio."));
        }

        // ============================================================
        // Wyliczenie i przedział celów (ADR-0016)
        // ============================================================

        [Fact]
        public void ReadAmendmentTargets_Enumeration_GivesATargetPerUnit()
        {
            // Korpus: „w art. 27 uchyla się pkt 2, 4 i 5;".
            Assert.Equal(
                new[] { "art_27__pkt_2", "art_27__pkt_4", "art_27__pkt_5" },
                Targets("w art. 27 uchyla się pkt 2, 4 i 5;"));
        }

        [Fact]
        public void ReadAmendmentTargets_Range_IsOneTargetWithAnEnd()
        {
            // Korpus: „ust. 8-10 otrzymują brzmienie:" pod art. 23 z komendy nadrzędnej.
            var target = SingleTarget("ust. 8-10 otrzymują brzmienie:", new StructuralReference { Article = "23" });

            Assert.Equal("art_23__ust_8", target.Structure.ToString());
            Assert.Equal("art_23__ust_10", target.RangeEnd?.ToString());
            Assert.Equal("art_23__ust_8–art_23__ust_10", target.ToString());
        }

        [Theory]
        [InlineData("w art. 5 w ust. 2 zdanie drugie i trzecie otrzymują brzmienie:")]
        [InlineData("w art. 5 w ust. 2 zdania drugie–czwarte otrzymują brzmienie:")]
        public void ReadAmendmentTargets_Sentences_CollapseIntoTheirUnit(string text)
        {
            // Cel nowelizacji to jednostka redakcyjna, bez zdania — zdania jednego ustępu to jeden cel.
            var target = SingleTarget(text);

            Assert.Equal("art_5__ust_2", target.ToString());
            Assert.Null(target.RangeEnd);
        }

        [Fact]
        public void GetContext_BuildsParentChainText()
        {
            var article = new Article { ContentText = "Art. 5." };
            var paragraph = new Paragraph { Parent = article, ContentText = "1. Tekst ustępu" };
            var point = new Point { Parent = paragraph, ContentText = "1) tekst punktu" };

            var context = _service.GetContext(point);

            Assert.Contains("Tekst ustępu", context);
            Assert.Contains("tekst punktu", context);
            // Article jest granicą - nie jest włączany
            Assert.DoesNotContain("Art. 5", context);
        }
    }

    public class OrchestratorAmendmentIntegrationTests
    {
        [Fact]
        public void ProcessParagraph_UpdatesStructuralReference_ForArticle()
        {
            var document = new LegalDocument();
            var subchapter = new Subchapter();
            var context = new ParsingContext(document, subchapter);
            var orchestrator = new ParserOrchestrator();

            var paragraph = CreateWordParagraph("Art. 5. Treść artykułu");
            orchestrator.ProcessParagraph(paragraph, context);

            Assert.Equal("5", context.CurrentStructuralReference.Article);
        }

        [Fact]
        public void ProcessParagraph_UpdatesStructuralReference_ForParagraph()
        {
            var document = new LegalDocument();
            var subchapter = new Subchapter();
            var context = new ParsingContext(document, subchapter);
            var orchestrator = new ParserOrchestrator();

            orchestrator.ProcessParagraph(CreateWordParagraph("Art. 3. 1. Pierwszy ustęp"), context);
            orchestrator.ProcessParagraph(CreateWordParagraph("2. Drugi ustęp"), context);

            Assert.Equal("3", context.CurrentStructuralReference.Article);
            Assert.Equal("2", context.CurrentStructuralReference.Paragraph);
        }

        [Fact]
        public void ProcessParagraph_UpdatesStructuralReference_ForPoint()
        {
            var document = new LegalDocument();
            var subchapter = new Subchapter();
            var context = new ParsingContext(document, subchapter);
            var orchestrator = new ParserOrchestrator();

            orchestrator.ProcessParagraph(CreateWordParagraph("Art. 1. 1. Ustęp pierwszy"), context);
            orchestrator.ProcessParagraph(CreateWordParagraph("1) punkt pierwszy"), context);

            Assert.Equal("1", context.CurrentStructuralReference.Article);
            Assert.Equal("1", context.CurrentStructuralReference.Paragraph);
            Assert.Equal("1", context.CurrentStructuralReference.Point);
        }

        [Fact]
        public void ProcessParagraph_NewArticle_ResetsChildReferences()
        {
            var document = new LegalDocument();
            var subchapter = new Subchapter();
            var context = new ParsingContext(document, subchapter);
            var orchestrator = new ParserOrchestrator();

            orchestrator.ProcessParagraph(CreateWordParagraph("Art. 1. 1. Ustęp"), context);
            orchestrator.ProcessParagraph(CreateWordParagraph("1) punkt"), context);

            // Nowy artykuł - reset dzieci
            orchestrator.ProcessParagraph(CreateWordParagraph("Art. 2. Treść"), context);

            Assert.Equal("2", context.CurrentStructuralReference.Article);
            Assert.Null(context.CurrentStructuralReference.Point);
        }

        [Fact]
        public void ProcessParagraph_DetectsAmendmentTargets_InParagraphContent()
        {
            var document = new LegalDocument();
            var subchapter = new Subchapter();
            var context = new ParsingContext(document, subchapter);
            var orchestrator = new ParserOrchestrator();

            orchestrator.ProcessParagraph(
                CreateWordParagraph("Art. 1. 1. W art. 10 ust. 3 dodaje się pkt 5"), context);

            // Paragraf z treścią nowelizacyjną powinien mieć wykryty cel
            Assert.NotNull(context.CurrentParagraph);
            Assert.True(context.DetectedAmendmentTargets.ContainsKey(context.CurrentParagraph.Guid));

            var target = Assert.Single(context.DetectedAmendmentTargets[context.CurrentParagraph.Guid]);
            Assert.Equal("10", target.Structure.Article);
            Assert.Equal("3", target.Structure.Paragraph);
        }

        [Fact]
        public void ProcessParagraph_NoAmendmentTargets_ForNormalContent()
        {
            var document = new LegalDocument();
            var subchapter = new Subchapter();
            var context = new ParsingContext(document, subchapter);
            var orchestrator = new ParserOrchestrator();

            orchestrator.ProcessParagraph(
                CreateWordParagraph("Art. 1. 1. Przepis stosuje się odpowiednio."), context);

            Assert.NotNull(context.CurrentParagraph);
            Assert.False(context.DetectedAmendmentTargets.ContainsKey(context.CurrentParagraph.Guid));
        }

        /// <summary>
        /// Tworzy minimalny akapit Wordowy z podanym tekstem (do testów).
        /// </summary>
        private static DocumentFormat.OpenXml.Wordprocessing.Paragraph CreateWordParagraph(string text)
        {
            var paragraph = new DocumentFormat.OpenXml.Wordprocessing.Paragraph();
            var run = new DocumentFormat.OpenXml.Wordprocessing.Run();
            run.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Text(text));
            paragraph.AppendChild(run);
            return paragraph;
        }
    }
}
