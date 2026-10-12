using System.Xml.Linq;
using Saga.Core.Helpers;
using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Core.Services.Classify;
using Saga.Core.Services.Parsing;
using Saga.Core.Services.Parsing.Builders;
using Saga.Model;
using Saga.Model.EditorialUnits;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Paragraf kodeksu (§ 55 ust. 3 ZTP): w kodeksach jednostką niższą od artykułu jest paragraf,
    /// zapisywany jak ustęp, tylko z prefiksem „§". Musi trafić do elementu <c>paragraf-kodeksu</c>
    /// i mieć eId z prefiksem <c>par</c>, a sam znak „§" nie może zostać w treści.
    ///
    /// Bez tego „§ 1.", „§ 2." … w jednym artykule zapadały się na wspólny, bezliczbowy eId
    /// (<c>art_2__ust</c>), bo numer nie był wydobywany — stąd asercje na unikalność eId.
    /// </summary>
    public class CodeParagraphTests
    {
        // ============================================================
        // Rozpoznanie i nadanie roli
        // ============================================================

        [Theory]
        [InlineData("§ 1. Do rozpoznawania spraw cywilnych powołane są sądy.")]
        [InlineData("§ 1a. (uchylony)")]
        [InlineData("§ 12. Treść paragrafu.")]
        [InlineData("§1. Bez spacji po znaku paragrafu.")]
        public void ParagraphPattern_RecognisesCodeParagraph(string text)
        {
            Assert.True(ParagraphClassifier.IsParagraphByText(text));
        }

        [Fact]
        public void ParseParagraphNumber_ExtractsNumberFromCodeParagraph()
        {
            Assert.Equal("1", ParsingFactories.ParseParagraphNumber("§ 1. Treść.")?.Value);
            Assert.Equal("1a", ParsingFactories.ParseParagraphNumber("§ 1a. Treść.")?.Value);
        }

        [Fact]
        public void StripParagraphPrefix_RemovesTheParagraphMark()
        {
            Assert.Equal("Treść przepisu.", ParsingFactories.StripParagraphPrefix("§ 1. Treść przepisu."));
        }

        [Fact]
        public void ApplyCodeParagraphRole_SetsRoleEIdPrefixAndLabel()
        {
            var paragraph = new Paragraph();
            ParsingFactories.ApplyCodeParagraphRole(paragraph, "§ 1. Treść.");

            Assert.Equal(Paragraph.CodeParagraphRole, paragraph.Role);
            Assert.Equal("par", paragraph.EIdPrefix);
            Assert.Equal("§", paragraph.DisplayLabel);
        }

        [Fact]
        public void ApplyCodeParagraphRole_LeavesOrdinaryParagraphUntouched()
        {
            // Zwykły ustęp („1. …") NIE jest paragrafem kodeksu — domyślne ust/ust. zostają.
            var paragraph = new Paragraph();
            ParsingFactories.ApplyCodeParagraphRole(paragraph, "1. Zwykły ustęp.");

            Assert.Null(paragraph.Role);
            Assert.Equal("ust", paragraph.EIdPrefix);
        }

        // ============================================================
        // Potok: artykuł kodeksu z kilkoma paragrafami
        // ============================================================

        private static (ParserOrchestrator orch, ParsingContext ctx) NewPipeline()
        {
            var document = new LegalDocument { Type = LegalActType.Statute };
            var subchapter = document.RootPart.Books[0].Titles[0].Divisions[0].Chapters[0].Subchapters[0];
            return (new ParserOrchestrator(), new ParsingContext(document, subchapter));
        }

        private static DocumentBlock Blk(string text) => new()
        {
            Text = text,
            Source = new BlockSourceLocation { BlockIndex = null },
        };

        private static ParsingContext ParseCodeArticle()
        {
            var (orch, ctx) = NewPipeline();
            foreach (var line in new[]
            {
                "Art. 2. § 1. Do rozpoznawania spraw cywilnych powołane są sądy powszechne.",
                "§ 1a. (uchylony)",
                "§ 2. Nie są rozpoznawane sprawy przekazane sądom szczególnym.",
            })
            {
                orch.ProcessBlock(Blk(line), ctx);
            }
            orch.Finalize(ctx);
            return ctx;
        }

        [Fact]
        public void CodeArticle_AllParagraphsCarryCodeRole_AndUniqueEIds()
        {
            var ctx = ParseCodeArticle();

            var article = Assert.Single(ctx.Document.Articles);
            Assert.Equal(3, article.Paragraphs.Count);
            Assert.All(article.Paragraphs, p => Assert.Equal(Paragraph.CodeParagraphRole, p.Role));

            Assert.Equal(
                new[] { "art_2__par_1", "art_2__par_1a", "art_2__par_2" },
                article.Paragraphs.Select(p => p.Id).ToArray());
        }

        [Fact]
        public void CodeArticle_ParagraphMarkIsStrippedFromContent()
        {
            var ctx = ParseCodeArticle();
            var article = ctx.Document.Articles.Single();

            Assert.All(article.Paragraphs, p => Assert.DoesNotContain("§", p.ContentText ?? string.Empty));
            Assert.Equal("Do rozpoznawania spraw cywilnych powołane są sądy powszechne.",
                article.Paragraphs[0].ContentText);
            Assert.Equal("(uchylony)", article.Paragraphs[1].ContentText);
        }

        [Fact]
        public void CurrentPosition_AfterCodeParagraph_UsesPar()
        {
            var ctx = ParseCodeArticle();

            Assert.Equal("art_2__par_2", ctx.CurrentStructuralReference.ToString());
        }

        [Fact]
        public void AmendmentTargetInCodeParagraph_IsExportedWithParElementEId()
        {
            // Korpus: 319698_2841367 — „w art. 56b § 2 otrzymuje brzmienie:". Adres celu ma nazywać
            // jednostkę tak samo jak jej eId w akcie zmienianym (ADR-0014).
            var (orch, ctx) = NewPipeline();
            foreach (var line in new[]
            {
                "Art. 1. W ustawie z dnia 10 września 1999 r. – Kodeks karny skarbowy wprowadza się następujące zmiany:",
                "1) w art. 56b § 2 otrzymuje brzmienie:",
                "„§ 2. Nowe brzmienie paragrafu.”;",
            })
            {
                orch.ProcessBlock(Blk(line), ctx);
            }
            orch.Finalize(ctx);

            var doc = new XDocument(LegalDocumentConverter.Convert(ctx.Document));
            var citation = Assert.Single(doc.Descendants(), e => e.Name.LocalName == "cytat-strukt");

            Assert.Equal("art_56b__par_2", citation.Attribute("element-eId")?.Value);
        }

        [Fact]
        public void OrdinaryArticle_StillProducesUstep_NotCodeParagraph()
        {
            // Strażnik przed nadgorliwością: akt bez „§" nie może nagle stać się kodeksem.
            var (orch, ctx) = NewPipeline();
            orch.ProcessBlock(Blk("Art. 3. 1. Pierwszy ustęp."), ctx);
            orch.ProcessBlock(Blk("2. Drugi ustęp."), ctx);
            orch.Finalize(ctx);

            var article = ctx.Document.Articles.Single();
            Assert.Equal(2, article.Paragraphs.Count);
            Assert.All(article.Paragraphs, p => Assert.Null(p.Role));
            Assert.Equal(new[] { "art_3__ust_1", "art_3__ust_2" },
                article.Paragraphs.Select(p => p.Id).ToArray());
        }

        // ============================================================
        // Eksport XML
        // ============================================================

        [Fact]
        public void CodeParagraph_IsExportedAsParagrafKodeksu()
        {
            var ctx = ParseCodeArticle();
            var doc = new XDocument(LegalDocumentConverter.Convert(ctx.Document));

            var names = doc.Descendants()
                .Where(e => e.Attribute("eId") != null && e.Attribute("eId")!.Value.Contains("par_"))
                .Select(e => e.Name.LocalName)
                .ToList();

            Assert.Equal(3, names.Count);
            Assert.All(names, n => Assert.Equal("paragraf-kodeksu", n));
            Assert.DoesNotContain(doc.Descendants(), e => e.Name.LocalName == "paragraf-kodeksu-n");
        }

        // ============================================================
        // Treść nowelizacji — styl kontra tekst
        // ============================================================

        [Fact]
        public void AmendmentContent_CodeParagraphs_AreParagraphsNotArticles()
        {
            // Regresja: przy priorytecie stylu kolejne „§ 2."/„§ 3." pod stylem artykułowym
            // stawały się osobnymi artykułami bez numeru. Treść ma pierwszeństwo nad stylem.
            var (orch, ctx) = NewPipeline();
            foreach (var line in new[]
            {
                "Art. 1. W ustawie wprowadza się następujące zmiany:",
                "1) art. 299i otrzymuje brzmienie:",
                "„Art. 299i. § 1. Szef Krajowej Administracji Skarbowej udostępnia dane.",
                "§ 2. Dane, o których mowa w § 1, obejmują roczne zatrudnienie.",
                "§ 3. Sposób udostępniania danych określają porozumienia.”;",
            })
            {
                orch.ProcessBlock(Blk(line), ctx);
            }
            orch.Finalize(ctx);

            var amendment = ctx.Document.Articles.First().Paragraphs[0].Points.Single().Amendment;
            Assert.NotNull(amendment);

            var quoted = Assert.Single(amendment!.Content!.Articles);
            Assert.Equal("299i", quoted.Number?.Value);
            Assert.Equal(3, quoted.Paragraphs.Count);
            Assert.All(quoted.Paragraphs, p => Assert.Equal(Paragraph.CodeParagraphRole, p.Role));
            Assert.Equal(
                new[] { "art_299i__par_1", "art_299i__par_2", "art_299i__par_3" },
                quoted.Paragraphs.Select(p => p.Id).ToArray());
        }

        /// <summary>Styl Word o TargetKind = Article (jednostka redakcyjna).</summary>
        private const string ArticleStyleId = "ZARTzmartartykuempunktem";

        /// <summary>Styl Word o TargetKind = Fragment (NIE jednostka redakcyjna).</summary>
        private const string FragmentStyleId = "ZFRAGzmfragmentunpzdaniaartykuempunktem";

        private static AmendmentEntityKind Classify(string text, string styleId) =>
            AmendmentBuilder.ClassifyAmendmentParagraph(new CollectedAmendmentParagraph(
                text, styleId, AmendmentStyleDecoder.DecodeByStyleId(styleId)));

        [Fact]
        public void StyleAndTextConflict_BothEditorialUnits_PrefersText()
        {
            // Styl mówi „artykuł", treść mówi „§ 2." → wygrywa treść.
            Assert.Equal(AmendmentTargetKind.Article,
                AmendmentStyleDecoder.DecodeByStyleId(ArticleStyleId)!.TargetKind);

            Assert.Equal(AmendmentEntityKind.Paragraph, Classify("§ 2. Treść paragrafu.", ArticleStyleId));
        }

        [Fact]
        public void StyleAndTextAgree_KeepsTheSharedVerdict()
        {
            Assert.Equal(AmendmentEntityKind.Article, Classify("Art. 5. Treść artykułu.", ArticleStyleId));
        }

        private static AmendmentContent BuildStyled(params (string Text, string StyleId)[] paragraphs) =>
            new AmendmentBuilder().Build(new AmendmentBuildInput(
                paragraphs.Select(p => new CollectedAmendmentParagraph(
                    p.Text, p.StyleId, AmendmentStyleDecoder.DecodeByStyleId(p.StyleId))).ToList(),
                null,
                AmendmentOperationType.Modification));

        [Fact]
        public void StyleOverriddenByText_IsRecordedOnTheBuiltEntity()
        {
            // Decyzja naprawcza (CLAUDE.md: „Walidacja") — encja musi nieść ValidationMessage,
            // tak jak w ścieżce ustawy matki przy konflikcie styl/treść.
            var content = BuildStyled(
                ("Art. 299i. § 1. Pierwszy paragraf.", ArticleStyleId),
                ("§ 2. Drugi paragraf.", ArticleStyleId));

            var article = Assert.Single(content.Articles);
            Assert.Empty(article.Paragraphs[0].ValidationMessages);

            var message = Assert.Single(article.Paragraphs[1].ValidationMessages);
            Assert.Equal(ValidationLevel.Warning, message.Level);
            Assert.Contains("styl=Article", message.Message);
            Assert.Contains("rozstrzygnięto: Paragraph", message.Message);
        }

        private const string ParagraphStyleId = "ZUSTzmustartykuempunktem";
        private const string PointStyleId = "ZPKTzmpktartykuempunktem";
        private const string LetterStyleId = "ZLITzmlitartykuempunktem";

        public static TheoryData<string, string, AmendmentEntityKind> OverrideCases => new()
        {
            // treść, styl (inna jednostka redakcyjna), oczekiwany rodzaj z treści
            { "Art. 7. Treść artykułu.", ParagraphStyleId, AmendmentEntityKind.Article },
            { "1) treść punktu;", ParagraphStyleId, AmendmentEntityKind.Point },
            { "a) treść litery;", PointStyleId, AmendmentEntityKind.Letter },
            { "– treść tiretu;", LetterStyleId, AmendmentEntityKind.Tiret },
        };

        [Theory]
        [MemberData(nameof(OverrideCases))]
        public void StyleOverride_IsRecordedOnTheEntity_ForEveryEditorialKind(
            string text, string styleId, AmendmentEntityKind expected)
        {
            // Najpierw upewniamy się, że to naprawdę konflikt — inaczej test przeszedłby pusto.
            Assert.Equal(expected, Classify(text, styleId));

            var flagged = AllEntities(BuildStyled((text, styleId)))
                .Where(e => e.ValidationMessages.Any(m => m.Message.Contains($"rozstrzygnięto: {expected}")))
                .ToList();

            Assert.Single(flagged);
        }

        [Fact]
        public void AmendmentArticleWithoutTail_CodeParagraphReusesImplicitFirstParagraph()
        {
            var content = BuildStyled(
                ("Art. 5.", ArticleStyleId),
                ("§ 1. Pierwszy paragraf.", ArticleStyleId),
                ("§ 2. Drugi paragraf.", ArticleStyleId));

            var article = Assert.Single(content.Articles);
            Assert.All(article.Paragraphs, p => Assert.Equal(Paragraph.CodeParagraphRole, p.Role));
            Assert.Equal(new[] { "art_5__par_1", "art_5__par_2" }, article.Paragraphs.Select(p => p.Id).ToArray());
        }

        private static IEnumerable<BaseEntity> AllEntities(AmendmentContent c) =>
            c.Articles.SelectMany(a => Walk(a))
                .Concat(c.Paragraphs.SelectMany(p => Walk(p)))
                .Concat(c.Points.SelectMany(p => Walk(p)))
                .Concat(c.Letters.SelectMany(l => Walk(l)))
                .Concat(c.Tirets.SelectMany(t => Walk(t)));

        private static IEnumerable<BaseEntity> Walk(Article a) => new BaseEntity[] { a }.Concat(a.Paragraphs.SelectMany(p => Walk(p)));
        private static IEnumerable<BaseEntity> Walk(Paragraph p) => new BaseEntity[] { p }.Concat(p.Points.SelectMany(x => Walk(x)));
        private static IEnumerable<BaseEntity> Walk(Point p) => new BaseEntity[] { p }.Concat(p.Letters.SelectMany(l => Walk(l)));
        private static IEnumerable<BaseEntity> Walk(Letter l) => new BaseEntity[] { l }.Concat(l.Tirets.SelectMany(t => Walk(t)));
        private static IEnumerable<BaseEntity> Walk(Tiret t) => new BaseEntity[] { t }.Concat(t.Tirets.SelectMany(x => Walk(x)));

        [Fact]
        public void NonEditorialStyle_WinsOverText()
        {
            // Z/FRAG niesie informację, której regex nie ma (zmiana fragmentu zdania) —
            // tu styl MUSI wygrać, inaczej fragment zostałby wzięty za ustęp.
            Assert.Equal(AmendmentEntityKind.PlainText, Classify("2. Fragment zdania.", FragmentStyleId));
        }

        // ============================================================
        // Element artykułu z paragrafami: „artykul-kodeksu" w oddziale
        // ============================================================

        private static XElement ExportCode(params string[] lines) => LegalDocumentConverter.Convert(
            LegalDocumentParser.ParseBlocks(lines.Select(text => new DocumentBlock { Text = text }).ToList()));

        private static string ElementOf(XElement xml, string eId) =>
            xml.Descendants().Single(e => e.Attribute("eId")?.Value == eId).Name.LocalName;

        [Fact]
        public void ArticleWithCodeParagraphs_InAChapter_IsACodeArticle()
        {
            var xml = ExportCode(
                "Rozdział 1",
                "Przepisy ogólne",
                "Art. 1. § 1. Kodeks reguluje stosunki cywilnoprawne.",
                "§ 2. Przepisy stosuje się odpowiednio.",
                "Art. 2. Prawo podmiotowe podlega ochronie.",
                "Art. 3. 1. Przepis z ustępami.",
                "2. Drugi ustęp.");

            // Schemat: w oddziale artykuł z „§" to „artykul-kodeksu", artykuł z ustępami — „artykul".
            Assert.Equal("artykul-kodeksu", ElementOf(xml, "art_1"));
            Assert.Equal("oddzial-n", xml.Descendants().Single(e => e.Attribute("eId")?.Value == "art_1").Parent!.Name.LocalName);
            Assert.Equal("artykul", ElementOf(xml, "art_2"));
            Assert.Equal("artykul", ElementOf(xml, "art_3"));
        }

        [Fact]
        public void ArticleWithCodeParagraphs_OutsideAChapter_StaysAnArticle()
        {
            // Wprost w akcie i na szczycie cytatu element nazywa się „artykul" — rodzaj rozstrzyga typ rodzica.
            var xml = ExportCode(
                "Art. 1. W ustawie z dnia 23 kwietnia 1964 r. - Kodeks cywilny (Dz. U. z 2024 r. poz. 1061) art. 5 otrzymuje brzmienie:",
                "„Art. 5. § 1. Nie można czynić ze swego prawa użytku sprzecznego z jego przeznaczeniem.",
                "§ 2. Takie działanie nie jest uważane za wykonywanie prawa.”.",
                "Art. 2. § 1. Przepis przejściowy.",
                "§ 2. Drugi paragraf.");

            Assert.Equal("artykul", ElementOf(xml, "DU/2024/1061__art_5"));
            Assert.Equal("cytat-strukt",
                xml.Descendants().Single(e => e.Attribute("eId")?.Value == "DU/2024/1061__art_5").Parent!.Name.LocalName);
            Assert.Equal("artykul", ElementOf(xml, "art_2"));
        }

        [Fact]
        public void QuotedChapterOfACode_HoldsCodeArticles()
        {
            var xml = ExportCode(
                "Art. 1. W ustawie z dnia 5 stycznia 2011 r. - Kodeks wyborczy (Dz. U. z 2025 r. poz. 365) po rozdziale 9 dodaje się rozdział 9a w brzmieniu:",
                "„Rozdział 9a",
                "Głosowanie korespondencyjne",
                "Art. 53a. § 1. Wyborca może głosować korespondencyjnie.",
                "§ 2. Głosowanie korespondencyjne jest wyłączone w obwodach odrębnych.",
                "Art. 53b. § 1. Zamiar głosowania zgłasza się komisarzowi.",
                "§ 2. Zgłoszenie zawiera dane wyborcy.”.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

            var quoted = xml.Descendants().Single(e => e.Name.LocalName == "cytat-strukt");
            Assert.Equal(new[] { "artykul-kodeksu", "artykul-kodeksu" },
                quoted.Descendants().Where(e => e.Name.LocalName.StartsWith("artykul")).Select(e => e.Name.LocalName));
        }

        [SchemaPlFact]
        public void CodeArticlesInChapters_AreSchemaValid()
        {
            var xml = ExportCode(
                "Rozdział 1",
                "Przepisy ogólne",
                "Art. 1. § 1. Kodeks reguluje stosunki cywilnoprawne.",
                "§ 2. Przepisy stosuje się odpowiednio.",
                "Art. 2. Prawo podmiotowe podlega ochronie.",
                "Art. 3. W ustawie z dnia 5 stycznia 2011 r. - Kodeks wyborczy (Dz. U. z 2025 r. poz. 365) po rozdziale 9 dodaje się rozdział 9a w brzmieniu:",
                "„Rozdział 9a",
                "Głosowanie korespondencyjne",
                "Art. 53a. § 1. Wyborca może głosować korespondencyjnie.",
                "§ 2. Głosowanie korespondencyjne jest wyłączone w obwodach odrębnych.”.");

            var act = xml.Elements().First();
            Assert.Empty(SchemaPl.Validator.ValidateActBody(act));
        }

        [Fact]
        public void SuperscriptInSentenceText_IsASupElement()
        {
            // Przykłady autorów schematu: „w art. 477<sup>9</sup> § 3<sup>1</sup> ustawy…", a w nr i eId „477[9]".
            var xml = LegalDocumentConverter.Convert(LegalDocumentParser.ParseBlocks(new[]
            {
                new DocumentBlock { Text = "Art. 1. W przypadku określonym w art. 477[9] § 3[1] ustawy sąd odrzuca odwołanie." },
                new DocumentBlock { Text = "Art. 2. Powierzchnia użytkowa nie przekracza 25 m[2], a odnośnik [1)] nie ma przypisu." },
            }));

            var sentences = xml.Descendants().Where(e => e.Name.LocalName == "zdanie").ToList();

            Assert.Equal(new[] { "9", "1" }, sentences[0].Elements().Select(e => e.Value));
            Assert.All(sentences[0].Elements(), e => Assert.Equal("sup", e.Name.LocalName));
            Assert.Equal("W przypadku określonym w art. 4779 § 31 ustawy sąd odrzuca odwołanie.", sentences[0].Value);

            // Jednostka miary też ma indeks górny; znacznik odnośnika spoza rejestru wychodzi tak samo, z nawiasem
            // należącym do znacznika (ADR-0047).
            Assert.Equal(new[] { "2", "1)" }, sentences[1].Elements().Select(e => e.Value));
            Assert.All(sentences[1].Elements(), e => Assert.Equal("sup", e.Name.LocalName));
            Assert.DoesNotContain("[", sentences[1].Value);
        }

        [Fact]
        public void SuperscriptedArticleAndCodeParagraph_KeepTheSuperscriptInEIdAndNr()
        {
            // Jak w przykładzie autorów schematu: artykul eId="…art_477[9]__par_2[1]", nr="2[1]".
            var document = LegalDocumentParser.ParseBlocks(new[]
            {
                new DocumentBlock { Text = "Art. 477[9]. § 1. Treść paragrafu pierwszego." },
                new DocumentBlock { Text = "§ 2[1]. Treść paragrafu dodanego." },
            });

            var xml = LegalDocumentConverter.Convert(document);
            var ns = xml.Name.Namespace;
            var article = xml.Descendants(ns + "artykul").Single();
            var added = article.Elements(ns + "paragraf-kodeksu").Last();

            Assert.Equal("art_477[9]", article.Attribute("eId")?.Value);
            Assert.Equal("477[9]", article.Element(ns + "nr")?.Value);
            Assert.Equal("art_477[9]__par_2[1]", added.Attribute("eId")?.Value);
            Assert.Equal("2[1]", added.Element(ns + "nr")?.Value);
        }
    }
}
