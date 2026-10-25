using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Akt oparty na paragrafach (rozporządzenie, uchwała, zarządzenie — § 124 ZTP): samodzielny „§" jest
    /// jednostką podstawową, tak jak „Art." w ustawie (ADR-0034).
    /// </summary>
    public class RegulationTests
    {
        private static readonly string[] Regulation =
        [
            "ROZPORZĄDZENIE",
            "MINISTRA FINANSÓW",
            "z dnia 11 maja 2017 r.",
            "w sprawie wykazu osób prawnych zastępowanych przez Prokuratorię Generalną",
            "Na podstawie art. 12 ust. 3 ustawy z dnia 15 grudnia 2016 r. o Prokuratorii Generalnej Rzeczypospolitej Polskiej (Dz. U. poz. 2261) zarządza się, co następuje:",
            "§ 1. Rozporządzenie określa wykaz osób prawnych zastępowanych przez Prokuratorię Generalną.",
            "§ 2. 1. Wykaz obejmuje:",
            "1) spółki, w których Skarb Państwa posiada wszystkie akcje;",
            "2) fundacje, których jedynym fundatorem jest Skarb Państwa.",
            "2. Wykaz ogłasza się w Biuletynie Informacji Publicznej.",
            "§ 3. Rozporządzenie wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
        ];

        private static LegalDocument Parse(IEnumerable<string> lines) =>
            LegalDocumentParser.Parse(
                lines.Select(line => new DocumentBlock { Text = line }).ToList(),
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

        [Fact]
        public void SectionSign_IsTheBasicUnitOfARegulation()
        {
            var document = Parse(Regulation);

            var units = document.Articles.ToList();
            Assert.Equal(new[] { "1", "2", "3" }, units.Select(unit => unit.Number?.Value));
            Assert.All(units, unit => Assert.True(unit.IsSection));
            Assert.Equal(new[] { "art_1", "art_2", "art_3" }, units.Select(unit => unit.Id));
        }

        [Fact]
        public void ParagraphsAndPointsOfASection_AreBuiltLikeUnderAnArticle()
        {
            var second = Parse(Regulation).Articles.ElementAt(1);

            Assert.Equal(new[] { "art_2__ust_1", "art_2__ust_2" }, second.Paragraphs.Select(p => p.Id));
            Assert.Equal(new[] { "art_2__ust_1__pkt_1", "art_2__ust_1__pkt_2" }, second.Paragraphs[0].Points.Select(p => p.Id));
            Assert.Equal("Wykaz ogłasza się w Biuletynie Informacji Publicznej.", second.Paragraphs[1].ContentText);

            // Ustęp nie jest paragrafem kodeksu — „§" należy tu do jednostki nadrzędnej.
            Assert.All(second.Paragraphs, paragraph => Assert.NotEqual(Saga.Model.EditorialUnits.Paragraph.CodeParagraphRole, paragraph.Role));
        }

        [Fact]
        public void HeadingOfARegulation_CarriesItsIssuerAndLegalBasis()
        {
            var document = Parse(Regulation);

            Assert.Equal("ROZPORZĄDZENIE", document.KindLine);
            Assert.Equal("MINISTRA FINANSÓW", document.IssuerLine);
            Assert.Equal("z dnia 11 maja 2017 r.", document.DateLine);
            Assert.StartsWith("w sprawie wykazu osób prawnych", document.Subject);
            Assert.StartsWith("Na podstawie art. 12 ust. 3", document.LegalBasis);
            Assert.EndsWith("zarządza się, co następuje:", document.LegalBasis);
        }

        [Theory]
        // Dz. U. 2026 poz. 555: rozporządzenie czterech ministrów — organ „Ministrów: Spraw …, Obrony …" z dwukropkiem;
        // adapter PDF składa nagłówek w jeden wiersz pisany jak nazwa własna, z odwołaniami do odnośników.
        [InlineData("Rozporządzenie Ministrów: Spraw Wewnętrznych i Administracji[1)], Obrony Narodowej, Finansów i Gospodarki[2)] oraz Sprawiedliwości")]
        [InlineData("ROZPORZĄDZENIE", "MINISTRÓW: SPRAW WEWNĘTRZNYCH I ADMINISTRACJI, OBRONY NARODOWEJ, FINANSÓW I GOSPODARKI ORAZ SPRAWIEDLIWOŚCI")]
        public void HeadingOfAJointRegulation_WithAColonAfterMinisters_IsItsTitleZone(params string[] heading)
        {
            var document = Parse(heading.Concat(
            [
                "z dnia 17 kwietnia 2026 r.",
                "zmieniające rozporządzenie w sprawie pojazdów specjalnych",
                "Na podstawie art. 66 ust. 7 ustawy z dnia 20 czerwca 1997 r. – Prawo o ruchu drogowym (Dz. U. z 2024 r. poz. 1251) zarządza się, co następuje:",
                "§ 1. Rozporządzenie wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
            ]));

            Assert.Equal("z dnia 17 kwietnia 2026 r.", document.DateLine);
            Assert.Equal("zmieniające rozporządzenie w sprawie pojazdów specjalnych", document.Subject);
            Assert.Contains("Sprawiedliwości", document.IssuerLine, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith("Na podstawie art. 66 ust. 7", document.LegalBasis);
        }

        [Fact]
        public void SectionSignInAStatute_StaysACodeParagraph()
        {
            // W ustawie „§" pod artykułem to paragraf kodeksu — rodzaj aktu rozstrzyga, nie sam znak.
            var document = Parse(
            [
                "USTAWA",
                "z dnia 6 czerwca 1997 r.",
                "Kodeks karny",
                "Art. 1. § 1. Odpowiedzialności karnej podlega ten tylko, kto popełnia czyn zabroniony.",
                "§ 2. Nie stanowi przestępstwa czyn zabroniony, którego społeczna szkodliwość jest znikoma.",
                "Art. 2. Odpowiedzialności karnej podlega ten tylko, na kim ciążył obowiązek.",
            ]);

            var units = document.Articles.ToList();
            Assert.Equal(new[] { "1", "2" }, units.Select(unit => unit.Number?.Value));
            Assert.All(units, unit => Assert.False(unit.IsSection));
            Assert.Equal(new[] { "art_1__par_1", "art_1__par_2" }, units[0].Paragraphs.Select(p => p.Id));
        }

        // ============================================================
        // Eksport: korzeń według rodzaju aktu, jednostka „paragraf"
        // ============================================================

        private static string[] ChildNames(XElement element) =>
            element.Elements().Select(child => child.Name.LocalName).ToArray();

        private static XElement Export(IEnumerable<string> lines, bool withJournalHeader = false)
        {
            var blocks = new List<DocumentBlock>();
            if (withJournalHeader)
            {
                blocks.Add(new() { Text = "DZIENNIK USTAW RZECZYPOSPOLITEJ POLSKIEJ", Role = BlockRole.JournalHeader });
                blocks.Add(new() { Text = "Warszawa, dnia 16 maja 2017 r.", Role = BlockRole.JournalHeader });
                blocks.Add(new() { Text = "Poz. 950", Role = BlockRole.JournalHeader });
            }
            blocks.AddRange(lines.Select(line => new DocumentBlock { Text = line }));
            var document = LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
            return LegalDocumentConverter.Convert(document).Elements().Single();
        }

        [Fact]
        public void Regulation_IsExportedUnderItsOwnRoot_WithSections()
        {
            var act = Export(Regulation);

            Assert.Equal("rozporzadzenie", act.Name.LocalName);
            Assert.Equal(
                new[] { "metadane", "rodzaj-aktu", "autor-aktu", "data", "tytul", "podstawa-prawna", "paragraf", "paragraf", "paragraf" },
                ChildNames(act));
            Assert.Equal("MINISTRA FINANSÓW", act.Elements().Single(e => e.Name.LocalName == "autor-aktu").Value);
            Assert.StartsWith("Na podstawie art. 12 ust. 3", act.Elements().Single(e => e.Name.LocalName == "podstawa-prawna").Value);

            var sections = act.Elements().Where(e => e.Name.LocalName == "paragraf").ToList();
            Assert.Equal(new[] { "art_1", "art_2", "art_3" }, sections.Select(e => e.Attribute("eId")!.Value));
            Assert.Equal(new[] { "nr", "ustep", "ustep" }, ChildNames(sections[1]));
            Assert.DoesNotContain(act.Descendants(), e => e.Name.LocalName == "artykul");
        }

        [Fact]
        public void Resolution_HasItsOwnRoot()
        {
            var act = Export(
            [
                "UCHWAŁA NR 113",
                "RADY MINISTRÓW",
                "z dnia 25 lipca 2017 r.",
                "w sprawie ustanowienia programu wieloletniego",
                "Na podstawie art. 136 ust. 2 ustawy z dnia 27 sierpnia 2009 r. o finansach publicznych (Dz. U. z 2016 r. poz. 1870) Rada Ministrów uchwala, co następuje:",
                "§ 1. Ustanawia się program wieloletni.",
                "§ 2. Uchwała wchodzi w życie z dniem podjęcia.",
            ]);

            Assert.Equal("uchwala", act.Name.LocalName);
            Assert.Equal(2, act.Elements().Count(e => e.Name.LocalName == "paragraf"));
        }

        [Fact]
        public void Resolution_WithoutAStatutoryBasis_KeepsItsEnactmentClauseAsTheLegalBasis()
        {
            // M.P. 2026 poz. 617: zamiast „Na podstawie art. …" cel i formuła — akapit przepadał, a uchwała bez
            // podstawy prawnej nie była zgodna ze schematem.
            var act = Export(
            [
                "UCHWAŁA NR 142",
                "RADY MINISTRÓW",
                "z dnia 16 czerwca 2026 r.",
                "w sprawie podjęcia działań w celu utworzenia stałej bazy",
                "W celu stworzenia warunków do podjęcia działań, Rada Ministrów uchwala, co następuje:",
                "§ 1. Upoważnia się Ministra Obrony Narodowej do podjęcia działań.",
                "§ 2. Uchwała wchodzi w życie z dniem podjęcia.",
            ]);

            var basis = act.Elements().Single(e => e.Name.LocalName == "podstawa-prawna");
            Assert.Equal("W celu stworzenia warunków do podjęcia działań, Rada Ministrów uchwala, co następuje:", basis.Value);
            Assert.Equal(2, act.Elements().Count(e => e.Name.LocalName == "paragraf"));
        }

        [Fact]
        public void Resolution_InForceTheDayAfterItsAdoption_HasThatEvent()
        {
            // M.P. 2026 poz. 349: uchwała Sejmu z dnia 27 marca 2026 r. — „Art. 2. Uchwała wchodzi w życie z dniem
            // następującym po dniu podjęcia." Bez tej formuły akt nie miał żadnego zdarzenia wejścia w życie.
            var act = Export(
            [
                "UCHWAŁA",
                "SEJMU RZECZYPOSPOLITEJ POLSKIEJ",
                "z dnia 27 marca 2026 r.",
                "w sprawie zmiany Regulaminu Sejmu Rzeczypospolitej Polskiej",
                "Art. 1. Ustanawia się regulamin.",
                "Art. 2. Uchwała wchodzi w życie z dniem następującym po dniu podjęcia.",
            ]);

            var events = act.Descendants().Where(e => e.Name.LocalName == "Zdarzenie").ToList();
            var entry = Assert.Single(events, e => e.Attribute("rodzaj")?.Value == "wejscie-w-zycie");
            Assert.Equal("2026-03-28", entry.Attribute("data")?.Value);
        }

        [Fact]
        public void Resolution_WithItsOrganInTitleCase_HasItsTitleZone()
        {
            // M.P. 2026 poz. 948: adapter PDF składa nagłówek w jeden wiersz pisany jak nazwa własna (ADR-0090) — wzorzec
            // nagłówka uchwały znał tylko organ wersalikami, więc strefa tytułowa przepadała (pusty tytuł, organ UNKNOWN).
            var act = Export(
            [
                "Uchwała nr 195 Rady Ministrów",
                "z dnia 25 września 2026 r.",
                "zmieniająca uchwałę w sprawie ustanowienia programu wieloletniego",
                "Na podstawie art. 136 ust. 2 ustawy z dnia 27 sierpnia 2009 r. o finansach publicznych (Dz. U. z 2025 r. poz. 1483) Rada Ministrów uchwala, co następuje:",
                "§ 1. Ustanawia się program wieloletni.",
                "§ 2. Uchwała wchodzi w życie z dniem następującym po dniu ogłoszenia.",
            ]);

            Assert.Equal("uchwala", act.Name.LocalName);
            Assert.Equal("Rady Ministrów", act.Elements().Single(e => e.Name.LocalName == "autor-aktu").Value);
            Assert.Equal("zmieniająca uchwałę w sprawie ustanowienia programu wieloletniego",
                act.Elements().Single(e => e.Name.LocalName == "tytul").Value);
        }

        [Fact]
        public void Statute_KeepsItsRootAndArticles()
        {
            var act = Export(
            [
                "USTAWA", "z dnia 11 sierpnia 2001 r.", "o odbudowie obiektów budowlanych",
                "Art. 1. Ustawa reguluje zasady odbudowy.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
            ]);

            Assert.Equal("ustawa", act.Name.LocalName);
            Assert.Equal(new[] { "metadane", "rodzaj-aktu", "data", "tytul", "artykul", "artykul" }, ChildNames(act));
        }

        [SchemaPlFact]
        public void Regulation_IsSchemaValid_InItsBody()
        {
            Assert.Empty(SchemaPl.Validator.ValidateActBody(Export(Regulation)));
        }

        [SchemaPlFact]
        public void Regulation_IsSchemaValid_AsAWhole()
        {
            var act = Export(Regulation, withJournalHeader: true);

            Assert.Empty(SchemaPl.Validator.Validate(act.Parent!));
        }

        // ============================================================
        // Organ wydający w strefie tytułowej
        // ============================================================

        [Theory]
        [InlineData("Ministra Finansów[1)]")]
        [InlineData("MINISTRa transportu, budownictwa i gospodarki morskiej[1)]")]
        [InlineData("Prezesa RAdy Ministrów")]
        public void IssuerLine_IsRecognisedByItsPlaceInTheHeading(string issuer)
        {
            // Szczotki RCL zapisują organ zwykłą pisownią i z odnośnikiem; wcześniej taki wiersz zamykał strefę
            // tytułową, więc ginęły też data i tytuł aktu.
            var lines = Regulation.ToArray();
            lines[0] = "Rozporządzenie";
            lines[1] = issuer;

            var document = Parse(lines);

            Assert.Equal(issuer, document.IssuerLine);
            Assert.Equal("z dnia 11 maja 2017 r.", document.DateLine);
            Assert.Equal(new DateTime(2017, 5, 11), document.ActDate);
            Assert.StartsWith("w sprawie wykazu osób prawnych", document.Subject);
        }

        [Fact]
        public void IssuerInTheKindLine_IsExportedApartFromTheKind()
        {
            var lines = Regulation.Skip(2).Prepend("ROZPORZĄDZENIE MINISTRA FINANSÓW").ToArray();

            var act = Export(lines);

            Assert.Equal("ROZPORZĄDZENIE", act.Elements().Single(e => e.Name.LocalName == "rodzaj-aktu").Value);
            Assert.Equal("MINISTRA FINANSÓW", act.Elements().Single(e => e.Name.LocalName == "autor-aktu").Value);
        }

        [Fact]
        public void StatuteHeading_HasNoIssuer()
        {
            var document = Parse(
            [
                "USTAWA", "z dnia 11 sierpnia 2001 r.", "o odbudowie obiektów budowlanych",
                "Art. 1. Ustawa reguluje zasady odbudowy.",
            ]);

            Assert.Equal(string.Empty, document.IssuerLine);
            Assert.Equal("o odbudowie obiektów budowlanych", document.Subject);
        }

        // ============================================================
        // Załączniki aktu wykonawczego
        // ============================================================

        private static IEnumerable<string> WithAnnex(params string[] annex) =>
            Regulation.Append("Minister Finansów: A. Domański").Concat(annex);

        [Theory]
        // Między rodzajem aktu a datą stoi organ, a w uchwale i zarządzeniu także numer.
        [InlineData("Załącznik do rozporządzenia Ministra Finansów z dnia 11 maja 2017 r. (Dz. U. poz. 950)")]
        [InlineData("Załącznik do rozporządzenia Ministra Rodziny, Pracy i Polityki Społecznej z dnia 11 maja 2017 r. (Dz. U. poz. 950)")]
        [InlineData("Załącznik do uchwały nr 113 Rady Ministrów z dnia 25 lipca 2017 r. (M.P. poz. 994)")]
        [InlineData("Załącznik do zarządzenia nr 81 Prezesa Rady Ministrów z dnia 24 lipca 2017 r. (M.P. poz. 756)")]
        public void AnnexHeaderOfASectionBasedAct_NamesItsIssuer(string header)
        {
            var document = Parse(WithAnnex(header, "WYKAZ OSÓB PRAWNYCH", "Spółka Alfa", "Fundacja Beta"));

            var annex = Assert.Single(document.Annexes);
            Assert.Equal(header, Assert.Single(annex.AttachedTo));

            // Pierwszy wiersz o budowie tytułu jest tytułem załącznika także bez stylu i wyśrodkowania.
            Assert.Equal("WYKAZ OSÓB PRAWNYCH", annex.Title);
            Assert.Equal(new[] { "Spółka Alfa", "Fundacja Beta" }, annex.Content.Select(block => block.Text));

            // Treść załącznika nie trafia do ostatniego paragrafu.
            var last = document.Articles.Last();
            Assert.Equal("3", last.Number?.Value);
            Assert.Empty(last.Paragraphs.SelectMany(paragraph => paragraph.Continuation));
            Assert.Single(last.Paragraphs);
        }

        [Fact]
        public void AnnexesOfARegulation_UnderACommonHeading_StartAtTheirDesignations()
        {
            var document = Parse(WithAnnex(
                "Załączniki do rozporządzenia Ministra Finansów z dnia 11 maja 2017 r. (Dz. U. poz. 950)",
                "Załącznik nr 1",
                "WYKAZ SPÓŁEK",
                "Spółka Alfa",
                "Załącznik nr 2",
                "WYKAZ FUNDACJI",
                "Fundacja Beta"));

            Assert.Equal(new[] { "1", "2" }, document.Annexes.Select(annex => annex.Number));
            Assert.Equal(new[] { "Spółka Alfa", "Fundacja Beta" }, document.Annexes.Select(annex => annex.Content.Last().Text));
        }

        [Fact]
        public void SentenceAboutAnAnnex_IsNotAnAnnexHeader()
        {
            // Bez daty aktu wiersz nie wskazuje załącznika — to treść przepisu.
            var lines = Regulation.ToList();
            lines.Insert(lines.Count - 1, "Załącznik do rozporządzenia określa wzór wniosku.");

            var document = Parse(lines);

            Assert.Empty(document.Annexes);
            Assert.Equal(new[] { "1", "2", "3" }, document.Articles.Select(unit => unit.Number?.Value));
        }

        [Fact]
        public void AnnexOfARegulation_IsExportedAfterTheSignature()
        {
            var act = Export(WithAnnex(
                "Załącznik do rozporządzenia Ministra Finansów z dnia 11 maja 2017 r. (Dz. U. poz. 950)",
                "WYKAZ OSÓB PRAWNYCH",
                "Spółka Alfa"));

            Assert.Equal(
                new[] { "paragraf", "paragraf", "paragraf", "organ-podpisujacy", "zalacznik" },
                ChildNames(act).SkipWhile(name => name != "paragraf"));
            var annex = act.Elements().Single(e => e.Name.LocalName == "zalacznik");
            Assert.Equal(new[] { "zalacznik-do", "tytul-zalacznika", "segment-zalacznika" }, ChildNames(annex));
            Assert.StartsWith("Załącznik do rozporządzenia Ministra Finansów", annex.Elements().First().Value);
        }

        [SchemaPlFact]
        public void RegulationWithAnAnnex_IsSchemaValid_AsAWhole()
        {
            var act = Export(
                WithAnnex(
                    "Załącznik do rozporządzenia Ministra Finansów z dnia 11 maja 2017 r. (Dz. U. poz. 950)",
                    "WYKAZ OSÓB PRAWNYCH",
                    "Spółka Alfa"),
                withJournalHeader: true);

            Assert.Empty(SchemaPl.Validator.Validate(act.Parent!));
        }

        // ============================================================
        // Cytowany paragraf w rozporządzeniu zmieniającym
        // ============================================================

        private static readonly string[] AmendingRegulation =
        [
            "ROZPORZĄDZENIE",
            "MINISTRA FINANSÓW",
            "z dnia 5 marca 2025 r.",
            "zmieniające rozporządzenie w sprawie wzoru zgłoszenia",
            "Na podstawie art. 12 ust. 3 ustawy z dnia 1 stycznia 2020 r. o zgłoszeniach (Dz. U. poz. 1) zarządza się, co następuje:",
            "§ 1. W rozporządzeniu Ministra Finansów z dnia 1 lutego 2021 r. w sprawie wzoru zgłoszenia (Dz. U. poz. 200) wprowadza się następujące zmiany:",
            "1) § 5 otrzymuje brzmienie:",
            "„§ 5. 1. Zgłoszenie składa się w postaci elektronicznej.",
            "2. Zgłoszenie zawiera:",
            "1) imię i nazwisko;",
            "2) adres.”;",
            "2) § 8 otrzymuje brzmienie:",
            "„§ 8. Organ potwierdza przyjęcie zgłoszenia.”.",
            "§ 2. Rozporządzenie wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
        ];

        [Fact]
        public void QuotedSection_IsTheBasicUnit_WithItsParagraphs()
        {
            var points = Parse(AmendingRegulation).Articles.First().Paragraphs[0].Points;

            var quoted = Assert.Single(points[0].Amendment!.Content!.Articles);
            Assert.True(quoted.IsSection);
            Assert.Equal("5", quoted.Number?.Value);
            Assert.Equal(new[] { "1", "2" }, quoted.Paragraphs.Select(p => p.Number?.Value));
            Assert.Equal(2, quoted.Paragraphs[1].Points.Count);
            Assert.All(quoted.Paragraphs, p => Assert.NotEqual(Saga.Model.EditorialUnits.Paragraph.CodeParagraphRole, p.Role));
            Assert.Empty(points[0].Amendment!.Content!.Paragraphs);

            // Paragraf bez ustępów: treść w ustępie niejawnym, jak w artykule.
            var single = Assert.Single(points[1].Amendment!.Content!.Articles);
            Assert.True(single.IsSection);
            Assert.True(Assert.Single(single.Paragraphs).IsImplicit);
        }

        [Fact]
        public void QuotedSection_IsExportedAsASection()
        {
            var quotes = Export(AmendingRegulation).Descendants().Where(e => e.Name.LocalName == "cytat-strukt").ToList();

            Assert.Equal(2, quotes.Count);
            Assert.All(quotes, quote => Assert.Equal(new[] { "paragraf" }, ChildNames(quote)));
            Assert.Equal(new[] { "nr", "ustep", "ustep" }, ChildNames(quotes[0].Elements().Single()));
            Assert.DoesNotContain(quotes.Descendants(), e => e.Name.LocalName.Contains("paragraf-kodeksu"));
        }

        [Fact]
        public void QuotedSectionInAStatute_StaysACodeParagraph()
        {
            // Ustawa zmieniająca kodeks cytuje „§ 2." jako paragraf kodeksu — rozstrzyga rodzaj aktu zmieniającego.
            var document = Parse(
            [
                "USTAWA",
                "z dnia 5 marca 2025 r.",
                "o zmianie ustawy – Kodeks karny",
                "Art. 1. W ustawie z dnia 6 czerwca 1997 r. – Kodeks karny (Dz. U. z 2024 r. poz. 17) w art. 1 § 2 otrzymuje brzmienie:",
                "„§ 2. Nie stanowi przestępstwa czyn zabroniony, którego społeczna szkodliwość jest znikoma.”.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
            ]);

            var content = document.Articles.First().Paragraphs[0].Amendment!.Content!;

            Assert.Empty(content.Articles);
            Assert.Equal(Saga.Model.EditorialUnits.Paragraph.CodeParagraphRole, Assert.Single(content.Paragraphs).Role);
        }

        [Fact]
        public void SectionsAddedAfterASection_AreOneQuotationAfterThatSection()
        {
            var lines = AmendingRegulation.ToList();
            var index = lines.IndexOf("2) § 8 otrzymuje brzmienie:");
            lines[index] = "2) po § 7 dodaje się § 7a i § 7b w brzmieniu:";
            lines[index + 1] = "„§ 7a. Organ potwierdza przyjęcie zgłoszenia.";
            lines.Insert(index + 2, "§ 7b. Potwierdzenie doręcza się niezwłocznie.”.");

            var amendment = Parse(lines).Articles.First().Paragraphs[0].Points[1].Amendment!;

            Assert.Equal(new[] { "art_7" }, amendment.Targets.Select(target => target.ToString()));
            Assert.Equal(new[] { "7a", "7b" }, amendment.Content!.Articles.Select(unit => unit.Number?.Value));
        }

        [Fact]
        public void QuotedSection_IsAddressedInTheAmendedRegulation()
        {
            // „W rozporządzeniu … (Dz. U. poz. 200)" wskazuje akt zmieniany — jego publikator jest prefiksem
            // celu i cytowanej jednostki, nie publikator rozporządzenia zmieniającego (ADR-0027).
            var amendment = Parse(AmendingRegulation).Articles.First().Paragraphs[0].Points[0].Amendment!;

            Assert.Equal(2021, amendment.TargetLegalAct.Year);
            Assert.Equal(new[] { 200 }, amendment.TargetLegalAct.Positions);

            var quote = Export(AmendingRegulation, withJournalHeader: true)
                .Descendants().First(e => e.Name.LocalName == "cytat-strukt");

            Assert.Equal("DU/2021/200__art_5", quote.Attribute("element-eId")?.Value);
            Assert.Equal("DU/2021/200__art_5", quote.Elements().Single().Attribute("eId")?.Value);
            Assert.EndsWith("/DU/2021/200", quote.Attributes().Single(a => a.Name.LocalName == "akt").Value);
        }

        [SchemaPlFact]
        public void AmendingRegulation_IsSchemaValid_InItsBody()
        {
            Assert.Empty(SchemaPl.Validator.ValidateActBody(Export(AmendingRegulation)));
        }

        // ============================================================
        // Tekst jednolity rozporządzenia
        // ============================================================

        private static readonly string[] ConsolidatedRegulation =
        [
            "Obwieszczenie",
            "Prezesa Rady Ministrów",
            "z dnia 6 sierpnia 2025 r.",
            "w sprawie ogłoszenia jednolitego tekstu rozporządzenia Ministra Finansów w sprawie wykazu osób prawnych",
            "1. Na podstawie art. 16 ust. 3 ustawy z dnia 20 lipca 2000 r. o ogłaszaniu aktów normatywnych ogłasza się w załączniku do niniejszego obwieszczenia jednolity tekst rozporządzenia Ministra Finansów z dnia 11 maja 2017 r. w sprawie wykazu osób prawnych.",
            "Prezes Rady Ministrów: D. Tusk",
            "Załącznik do obwieszczenia Prezesa Rady Ministrów z dnia 6 sierpnia 2025 r. (Dz. U. poz. 1100)",
            .. Regulation,
        ];

        [Fact]
        public void ConsolidatedRegulation_IsEmbeddedUnderTheAnnouncement()
        {
            var announcement = Export(ConsolidatedRegulation);

            Assert.Equal("obwieszczenie", announcement.Name.LocalName);
            var act = Assert.Single(announcement.Elements(), e => e.Name.LocalName == "rozporzadzenie");
            Assert.Equal(
                new[] { "zalacznik-do", "rodzaj-aktu", "autor-aktu", "data", "tytul", "podstawa-prawna", "paragraf", "paragraf", "paragraf" },
                ChildNames(act));
            Assert.Equal("MINISTRA FINANSÓW", act.Elements().Single(e => e.Name.LocalName == "autor-aktu").Value);

            // Nagłówek dokumentu pozostaje nagłówkiem obwieszczenia.
            Assert.Equal("Prezesa Rady Ministrów", announcement.Elements().Single(e => e.Name.LocalName == "autor-aktu").Value);
        }

        [Theory]
        // Korpus: 303866_2776495, 298226_2711116, 318274_2881159 — nagłówek aktu w załączniku pisany inaczej niż wzorzec.
        [InlineData("rozporządzenie", "Ministra Finansów", "rozporzadzenie")]
        [InlineData("ZARządzenie NR 249", "prezesa rady ministrów", "zarzadzenie")]
        [InlineData("zaRZĄDZENIE nr 53", "Prezesa RADY MINISTRÓW", "zarzadzenie")]
        public void ConsolidatedActWithAKindLineInUnusualSpelling_IsEmbeddedByItsKind(string kindLine, string issuer, string element)
        {
            var lines = ConsolidatedRegulation.ToList();
            var kind = lines.IndexOf("ROZPORZĄDZENIE");
            lines[kind] = kindLine;
            lines[kind + 1] = issuer;

            var announcement = Export(lines);

            Assert.Equal("obwieszczenie", announcement.Name.LocalName);
            var act = Assert.Single(announcement.Elements(), e => e.Name.LocalName == element);
            Assert.Equal(kindLine, act.Elements().Single(e => e.Name.LocalName == "rodzaj-aktu").Value);
            Assert.Equal(issuer, act.Elements().Single(e => e.Name.LocalName == "autor-aktu").Value);
            Assert.Equal(3, act.Elements().Count(e => e.Name.LocalName == "paragraf"));
        }

        [Fact]
        public void IssuerOnTheKindLineInTitleCase_IsExportedApartFromTheKind()
        {
            var lines = Regulation.ToList();
            lines[0] = "Rozporządzenie Ministra Finansów i Gospodarki";
            lines.RemoveAt(1);

            var act = Export(lines);

            Assert.Equal("Rozporządzenie", act.Elements().Single(e => e.Name.LocalName == "rodzaj-aktu").Value);
            Assert.Equal("Ministra Finansów i Gospodarki", act.Elements().Single(e => e.Name.LocalName == "autor-aktu").Value);
            Assert.Equal(new[] { "rodzaj-aktu", "autor-aktu", "data", "tytul", "podstawa-prawna" }, ChildNames(act).Skip(1).Take(5));
        }

        [Fact]
        public void SectionQuotedInTheAnnouncement_IsTheBasicUnitOfTheQuotedRegulation()
        {
            // Obwieszczenie ogłasza rozporządzenie, więc przytoczone „§ 2." to paragraf, nie paragraf kodeksu.
            var lines = ConsolidatedRegulation.ToList();
            var signature = lines.IndexOf("Prezes Rady Ministrów: D. Tusk");
            lines.InsertRange(signature,
            [
                "2. Podany w załączniku do niniejszego obwieszczenia tekst jednolity rozporządzenia nie obejmuje § 2 rozporządzenia " +
                    "Ministra Finansów z dnia 3 marca 2025 r. zmieniającego rozporządzenie w sprawie wykazu osób prawnych (Dz. U. poz. 310), który stanowi:",
                "„§ 2. 1. Rozporządzenie wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
                "2. Przepis § 1 stosuje się od dnia 1 stycznia 2026 r.”",
            ]);

            var quoted = Export(lines).Descendants().Single(e => e.Name.LocalName == "tresc-strukt");

            var section = Assert.Single(quoted.Elements());
            Assert.Equal("paragraf", section.Name.LocalName);
            Assert.Equal("DU/2025/310__art_2", section.Attribute("eId")!.Value);
            Assert.Equal(new[] { "nr", "ustep", "ustep" }, ChildNames(section));
        }

        [Fact]
        public void SectionQuotedFromMonitorPolski_IsAddressedInThatJournal()
        {
            var lines = ConsolidatedRegulation.ToList();
            var signature = lines.IndexOf("Prezes Rady Ministrów: D. Tusk");
            lines.InsertRange(signature,
            [
                "2. Podany w załączniku do niniejszego obwieszczenia tekst jednolity nie obejmuje § 2 zarządzenia nr 81 " +
                    "Prezesa Rady Ministrów z dnia 24 lipca 2017 r. zmieniającego zarządzenie w sprawie Komitetu (M.P. poz. 756), który stanowi:",
                "„§ 2. Zarządzenie wchodzi w życie z dniem ogłoszenia.”",
            ]);

            var quoted = Export(lines).Descendants().Single(e => e.Name.LocalName == "tresc-strukt");

            Assert.Equal("MP/2017/756__art_2", Assert.Single(quoted.Elements()).Attribute("eId")!.Value);
        }

        [SchemaPlFact]
        public void ConsolidatedRegulation_IsSchemaValid_InItsBody()
        {
            Assert.Empty(SchemaPl.Validator.ValidateActBody(Export(ConsolidatedRegulation)));
        }
    }
}
