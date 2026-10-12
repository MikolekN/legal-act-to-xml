using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Core.Services.Parsing;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Obwieszczenie, które nie ogłasza aktu (ADR-0043) — stawki, wskaźniki, wolne stanowiska: cały dokument
    /// jest obwieszczeniem, z podstawą prawną, podpisem i własnymi załącznikami, pod korzeniem
    /// <c>obwieszczenie</c>.
    /// </summary>
    public class PlainAnnouncementTests
    {
        private static DocumentBlock Text(string text, string? style = null) => new() { Text = text, StyleId = style };

        private static readonly string[] Heading =
        [
            "OBWIESZCZENIE",
            "MINISTRA INFRASTRUKTURY",
            "z dnia 25 lipca 2025 r.",
            "w sprawie wysokości stawki opłaty legalizacyjnej obowiązującej od dnia 1 stycznia 2026 r.",
        ];

        private const string LegalBasis =
            "Na podstawie art. 190 ust. 9 ustawy z dnia 20 lipca 2017 r. – Prawo wodne (Dz. U. z 2025 r. poz. 960) " +
            "ogłasza się, że stawka opłaty legalizacyjnej wynosi 1125,64 zł.";

        private const string Signature = "Minister Infrastruktury: wz. P. Koperski";

        private static LegalDocument Parse(params string[] body)
        {
            var blocks = new List<DocumentBlock>
            {
                new() { Text = "MONITOR POLSKI", Role = BlockRole.JournalHeader },
                new() { Text = "Warszawa, dnia 30 lipca 2025 r.", Role = BlockRole.JournalHeader },
                new() { Text = "Poz. 702", Role = BlockRole.JournalHeader },
            };
            blocks.AddRange(Heading.Concat(body).Select(line => Text(line)));
            return LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
        }

        private static List<DocumentBlock> Blocks(params string[] lines) => lines.Select(line => Text(line)).ToList();

        private static List<XElement> Named(XElement xml, string name) =>
            xml.Descendants().Where(e => e.Name.LocalName == name).ToList();

        // ============================================================
        // Układ dokumentu
        // ============================================================

        [Fact]
        public void AnnouncementWithoutAnAnnex_IsAnAnnouncementAsAWhole()
        {
            var layout = AnnouncementBoundary.Describe(Blocks([.. Heading, LegalBasis, Signature]));

            Assert.True(layout.IsPlainAnnouncement);
            Assert.Equal(-1, layout.AnnexStart);
        }

        [Fact]
        public void AnnexThatIsNotAnAct_BelongsToTheAnnouncement()
        {
            // Korpus: 331278_3065180 — za wierszem „Załącznik do obwieszczenia …" stoi tytuł i tabela, nie akt.
            var layout = AnnouncementBoundary.Describe(Blocks(
            [
                .. Heading, LegalBasis, Signature,
                "Załącznik do obwieszczenia Ministra Infrastruktury z dnia 25 lipca 2025 r. (M.P. poz. 702)",
                "WYKAZ STAWEK",
                "1. Stawka podstawowa",
            ]));

            Assert.True(layout.IsPlainAnnouncement);
            Assert.Equal(6, layout.AnnexStart);
        }

        [Theory]
        // Rodzaj aktu tuż za wierszem załącznika — także w pisowni spoza wzorca.
        [InlineData("w sprawie ogłoszenia jednolitego tekstu ustawy o odbudowie", "USTAWA")]
        [InlineData("w sprawie ogłoszenia jednolitego tekstu rozporządzenia", "ROZPORZĄDZENIE MINISTRA FINANSÓW")]
        [InlineData("w sprawie wykazu", "ZARządzenie NR 249")]
        // Przedmiot obwieszczenia wystarcza, gdy akt nie zaczyna się rodzajem.
        [InlineData("w sprawie ogłoszenia jednolitego tekstu ustawy – Kodeks pracy", "Kodeks pracy")]
        public void AnnexThatIsAnAct_IsAnnouncedByTheAnnouncement(string subject, string firstLineOfTheAnnex)
        {
            var layout = AnnouncementBoundary.Describe(Blocks(
                "OBWIESZCZENIE", "MARSZAŁKA SEJMU RZECZYPOSPOLITEJ POLSKIEJ", "z dnia 6 sierpnia 2025 r.", subject,
                "1. Na podstawie art. 16 ust. 1 ustawy z dnia 20 lipca 2000 r. o ogłaszaniu aktów normatywnych ogłasza się tekst.",
                "Załącznik do obwieszczenia Marszałka Sejmu Rzeczypospolitej Polskiej z dnia 6 sierpnia 2025 r. (Dz. U. poz. 1094)",
                firstLineOfTheAnnex,
                "z dnia 11 sierpnia 2001 r."));

            Assert.True(layout.AnnouncesAct);
            Assert.False(layout.IsPlainAnnouncement);
            Assert.Equal(5, layout.AnnexStart);
        }

        [Fact]
        public void ShortConsolidatedRegulation_BuildsItsSections_FromTheHeadingOfTheAnnouncedAct()
        {
            // Korpus: 329550_2989608 — rozporządzenie o dwóch paragrafach; klasyfikator dokumentu nie widzi w nim
            // przewagi „§", a o jednostce podstawowej mówi nagłówek ogłaszanego aktu.
            string[] lines =
            [
                "OBWIESZCZENIE",
                "MINISTRA SPRAWIEDLIWOŚCI",
                "z dnia 4 sierpnia 2025 r.",
                "w sprawie ogłoszenia jednolitego tekstu rozporządzenia Ministra Sprawiedliwości w sprawie określenia stanowisk w Służbie Więziennej",
                "1. Na podstawie art. 16 ust. 3 ustawy z dnia 20 lipca 2000 r. o ogłaszaniu aktów normatywnych ogłasza się w załączniku jednolity tekst rozporządzenia.",
                "Minister Sprawiedliwości: W. Żurek",
                "Załącznik do obwieszczenia Ministra Sprawiedliwości z dnia 4 sierpnia 2025 r. (Dz. U. poz. 1102)",
                "ROZPORZĄDZENIE",
                "MINISTRA SPRAWIEDLIWOŚCI",
                "z dnia 12 stycznia 2011 r.",
                "w sprawie określenia stanowisk w Służbie Więziennej",
                "Na podstawie art. 26 ust. 2 ustawy z dnia 9 kwietnia 2010 r. o Służbie Więziennej (Dz. U. z 2024 r. poz. 1869) zarządza się, co następuje:",
                "§ 1. Wykaz stanowisk określa załącznik do rozporządzenia.",
                "§ 2. Rozporządzenie wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
            ];

            var layout = AnnouncementBoundary.Describe(Blocks(lines));
            Assert.Equal(LegalActType.Regulation, layout.AnnouncedKind);

            var document = LegalDocumentParser.Parse(Blocks(lines), new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
            Assert.Equal(new[] { "1", "2" }, document.Articles.Select(article => article.Number?.Value));
            Assert.All(document.Articles, article => Assert.True(article.IsSection));

            var root = LegalDocumentConverter.Convert(document).Elements().Single();
            Assert.Equal("obwieszczenie", root.Name.LocalName);
            Assert.Equal(2, Named(root, "paragraf").Count);
            Assert.Single(root.Elements(), e => e.Name.LocalName == "rozporzadzenie");
        }

        // Korpus: 340765_3073132 — obwieszczenie zbudowane z paragrafów, jak akt wykonawczy.
        private const string BasisOfSections =
            "Na podstawie art. 80zd ust. 3 ustawy z dnia 20 czerwca 1997 r. – Prawo o ruchu drogowym (Dz. U. z 2024 r. poz. 1251) " +
            "ogłasza się, co następuje:";

        private static readonly string[] Sections =
        [
            BasisOfSections,
            "§ 1. Od dnia 1 stycznia 2026 r. stawka opłaty za wydanie:",
            "1) decyzji o profesjonalnej rejestracji pojazdów nie może przekroczyć 147 zł;",
            "2) znaku legalizacyjnego nie może przekroczyć 22,50 zł.",
            "§ 2. Od dnia 1 stycznia 2026 r. stawka opłaty za wydanie blankietu wynosi 15 zł.",
            Signature,
        ];

        [Fact]
        public void AnnouncementBuiltOfSections_IsAnAnnouncementWithProvisions()
        {
            var layout = AnnouncementBoundary.Describe(Blocks([.. Heading, .. Sections]));

            Assert.True(layout.IsPlainAnnouncement);
            Assert.True(layout.HasProvisions);
            Assert.True(layout.HasSections);
        }

        [Fact]
        public void SectionsOfAnAnnouncement_AreItsBasicUnits()
        {
            var document = Parse(Sections);

            Assert.False(document.Announcement!.AnnouncesAct);
            Assert.Null(document.Announcement.Body);
            Assert.Equal(BasisOfSections, document.LegalBasis);
            Assert.Equal(new[] { "1", "2" }, document.Articles.Select(article => article.Number?.Value));
            Assert.All(document.Articles, article => Assert.True(article.IsSection));
            Assert.Equal(2, document.Articles.First().Paragraphs.Single().Points.Count);
            Assert.Equal("Koperski", document.Signature?.Surname);
        }

        [Fact]
        public void Export_PutsTheSectionsUnderTheAnnouncement()
        {
            var root = LegalDocumentConverter.Convert(Parse(Sections)).Elements().Single();

            Assert.Equal("obwieszczenie", root.Name.LocalName);
            Assert.Equal(
                new[] { "metadane", "rodzaj-aktu", "autor-aktu", "data", "tytul", "podstawa-prawna", "paragraf", "paragraf", "organ-podpisujacy" },
                root.Elements().Select(e => e.Name.LocalName));
            // Jednostka podstawowa ma w adresie skrót „art" także wtedy, gdy jest paragrafem (ADR-0034).
            Assert.Equal(
                new[] { "MP/2025/702__art_1", "MP/2025/702__art_2" },
                root.Elements().Where(e => e.Name.LocalName == "paragraf").Select(e => e.Attribute("eId")?.Value));
        }

        [Fact]
        public void AnnexOfAnAnnouncementWithSections_IsItsAnnex()
        {
            // Korpus: 345769_3066483 — paragrafy, podpis i załącznik z listą.
            var document = Parse(
            [
                .. Sections,
                "Załącznik do obwieszczenia Ministra Infrastruktury z dnia 25 lipca 2025 r. (M.P. poz. 702)",
                "Stawki opłat w poszczególnych regionach nie ulegają zmianie.",
            ]);

            Assert.Equal(2, document.Articles.Count());
            Assert.Single(document.Annexes);
        }

        [SchemaPlFact]
        public void AnnouncementWithSections_IsSchemaValid_AsAWhole()
        {
            Assert.Empty(SchemaPl.Validator.Validate(LegalDocumentConverter.Convert(Parse(Sections))));
        }

        [Theory]
        // Korpus: 329084_2934669 — organ małą literą w wierszu nagłówka; nagłówek poznaje się po miejscu.
        [InlineData("Obwieszczenie marszałka Sejmu Rzeczypospolitej Polskiej", true)]
        [InlineData("OBWIESZCZENIE MINISTRA FINANSÓW", true)]
        // Długa nazwa organu nie czyni nagłówka zdaniem.
        [InlineData("Obwieszczenie ministra właściwego do spraw budownictwa, planowania i zagospodarowania przestrzennego oraz mieszkalnictwa, ministra właściwego do spraw gospodarki oraz ministra właściwego do spraw klimatu", true)]
        // Zdanie zaczynające się tym słowem nagłówkiem nie jest.
        [InlineData("Obwieszczenie zostało wydane na podstawie odrębnych przepisów.", false)]
        public void FirstLineStartingWithTheKindWord_IsTheHeadingOfAnAnnouncement(string firstLine, bool expected)
        {
            var layout = AnnouncementBoundary.Describe(Blocks(
                firstLine, "z dnia 11 czerwca 2025 r.", "w sprawie wysokości stawki opłaty", LegalBasis, Signature));

            Assert.Equal(expected, layout.OpensWithAnnouncement);
        }

        [Fact]
        public void DocumentThatIsNotAnAnnouncement_HasNoAnnouncementLayout()
        {
            var layout = AnnouncementBoundary.Describe(Blocks(
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o opłatach", "Art. 1. Ustawa określa opłaty."));

            Assert.False(layout.OpensWithAnnouncement);
            Assert.False(layout.IsPlainAnnouncement);
            Assert.Equal(-1, layout.AnnexStart);
        }

        // ============================================================
        // Model
        // ============================================================

        [Fact]
        public void LegalBasisAndSignature_AreReadFromTheAnnouncement()
        {
            var document = Parse(LegalBasis, Signature);

            Assert.NotNull(document.Announcement);
            Assert.False(document.Announcement.AnnouncesAct);
            Assert.Equal(LegalBasis, document.LegalBasis);
            Assert.Equal("Koperski", document.Signature?.Surname);
            Assert.Empty(document.Announcement.Content);
            Assert.Empty(document.Articles);
        }

        // Korpus: 328975_2977599 — „… ogłasza się następujące wysokości stawek opłat:" i punkty.
        private const string Introduction =
            "Na podstawie art. 398 ust. 11 ustawy z dnia 20 lipca 2017 r. – Prawo wodne (Dz. U. z 2025 r. poz. 960) " +
            "ogłasza się następujące wysokości stawek opłat:";

        private static LegalDocument WithEnumeration() => Parse(
            Introduction,
            "1) stawka opłaty za przyjęcie zgłoszenia:",
            "a) wodnoprawnego wynosi 132,33 zł,",
            "b) uzupełniającego wynosi 66,16 zł;",
            "2) stawka opłaty za wydanie pozwolenia wynosi 330,07 zł.",
            Signature);

        [Fact]
        public void LegalBasisThatOpensAnEnumeration_IsTheTextOfTheProvision()
        {
            var document = WithEnumeration();

            // Wprowadzenie nie odrywa się od wyliczenia: podstawy prawnej osobno nie ma.
            Assert.Empty(document.LegalBasis);
            var body = document.Announcement!.Body!;
            Assert.Equal(Introduction, body.ContentText);
            Assert.Equal(new[] { "1", "2" }, body.Points.Select(point => point.Number?.Value));
            Assert.Equal(new[] { "a", "b" }, body.Points[0].Letters.Select(letter => letter.Number?.Value));
            Assert.Empty(document.Announcement.Content);
            Assert.Equal("Koperski", document.Signature?.Surname);
        }

        [Fact]
        public void ParagraphsAfterTheLegalBasis_AreOneProvisionWithoutADesignation()
        {
            // Korpus: 338747_3016710 — „… obwieszcza się, co następuje:", akapity, wyliczenie, akapit.
            const string basis =
                "Na podstawie art. 6a § 2a ustawy z dnia 25 lipca 2002 r. – Prawo o ustroju sądów administracyjnych " +
                "(Dz. U. z 2024 r. poz. 1267) obwieszcza się, co następuje:";
            var document = Parse(
                basis,
                "W Wojewódzkim Sądzie Administracyjnym w Gliwicach jest wolne jedno stanowisko asesorskie.",
                "Informuję, że w przedmiocie powołania do pełnienia urzędu stosuje się odpowiednio:",
                "1) dział II ustawy – Prawo o ustroju sądów powszechnych;",
                "2) rozporządzenie w sprawie badań kandydatów.",
                "Wzór karty zgłoszenia opublikowany został w Biuletynie Informacji Publicznej.",
                Signature);

            Assert.Equal(basis, document.LegalBasis);
            var body = document.Announcement!.Body!;
            Assert.Equal("W Wojewódzkim Sądzie Administracyjnym w Gliwicach jest wolne jedno stanowisko asesorskie.", body.ContentText);
            Assert.Equal(2, body.Points.Count);
            Assert.Contains(body.Continuation, block => block.Text!.StartsWith("Informuję, że", StringComparison.Ordinal));
        }

        [Fact]
        public void AnnouncementWithNumberedPoints_KeepsItsPoints()
        {
            // „1. …" to punkt obwieszczenia, jak w obwieszczeniu tekstu jednolitego — nie przepis bez oznaczenia.
            var document = Parse(
                "1. Na podstawie art. 5 ustawy z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) ogłasza się stawki.",
                "2. Stawki obowiązują od dnia 1 stycznia 2026 r.",
                Signature);

            Assert.Null(document.Announcement!.Body);
            Assert.Equal(new[] { "1", "2" }, document.Announcement.Points.Select(point => point.Number?.Value));
        }

        [Fact]
        public void AnnexesAnnouncedInThePlural_AreAnnexesOfTheAnnouncement()
        {
            // Korpus: 334940_3004869 — „Załączniki do obwieszczenia …" i „Załącznik nr 1", „Załącznik nr 2".
            var document = Parse(
                LegalBasis, Signature,
                "Załączniki do obwieszczenia Ministra Infrastruktury z dnia 25 lipca 2025 r. (M.P. poz. 702)",
                "Załącznik nr 1",
                "Stawki opłat w regionie wodnym Wisły nie ulegają zmianie.",
                "Załącznik nr 2",
                "Stawki opłat w regionie wodnym Odry nie ulegają zmianie.");

            Assert.Equal(new[] { "1", "2" }, document.Annexes.Select(annex => annex.Number));
            Assert.Null(document.Announcement!.Body);
            Assert.Equal(LegalBasis, document.LegalBasis);
        }

        [Fact]
        public void Export_WritesTheProvisionWithoutADesignation_WithUnitsAddressedFromTheAnnouncement()
        {
            var root = LegalDocumentConverter.Convert(WithEnumeration()).Elements().Single();

            Assert.Equal(
                new[] { "metadane", "rodzaj-aktu", "autor-aktu", "data", "tytul", "nie-ztp-artykul", "organ-podpisujacy" },
                root.Elements().Select(e => e.Name.LocalName));

            var provision = root.Elements().Single(e => e.Name.LocalName == "nie-ztp-artykul");
            Assert.Equal("jednostka-nieoznaczona", provision.Attribute("brak-eid")?.Value);
            Assert.Null(provision.Attribute("eId"));
            Assert.Equal(string.Empty, provision.Elements().First().Value);
            Assert.Equal("nr", provision.Elements().First().Name.LocalName);
            Assert.Equal(Introduction, provision.Elements().Single(e => e.Name.LocalName == "p").Value);

            // Schemat przyjmuje pod takim przepisem jednostki tylko w wariancie spoza ZTP; adres liczy się od obwieszczenia.
            var points = provision.Elements().Where(e => e.Name.LocalName == "nie-ztp-punkt").ToList();
            Assert.Equal(new[] { "MP/2025/702__pkt_1", "MP/2025/702__pkt_2" }, points.Select(point => point.Attribute("eId")?.Value));
            Assert.Equal(
                new[] { "MP/2025/702__pkt_1__lit_a", "MP/2025/702__pkt_1__lit_b" },
                points[0].Elements().Where(e => e.Name.LocalName == "nie-ztp-litera").Select(letter => letter.Attribute("eId")?.Value));
        }

        // Treść, której potok nie złoży w jeden przepis: cytat wieloakapitowy po zwrocie zmiany brzmienia
        // (obwieszczenie o sprostowaniu błędów) otwiera nowelizację.
        private static LegalDocument ThatCannotBeStructured() => Parse(
            "Na podstawie art. 18b ustawy z dnia 14 kwietnia 2000 r. o umowach międzynarodowych (Dz. U. z 2020 r. poz. 127) " +
            "obwieszcza się, że w tekście umowy:",
            "1) przepis 1.8.6.2.1 otrzymuje brzmienie:",
            "„Jeżeli właściwa władza zatwierdza jednostkę inspekcyjną, to akredytacja powinna być wyraźna.",
            "Akredytacja powinna wyraźnie obejmować działania związane z zatwierdzeniem.”;",
            "2) w przepisie 2.2.9.3 zamiast wyrazu „dopuszczenia” powinien być wyraz „zatwierdzenia”.",
            Signature);

        [Fact]
        public void ContentThatCannotBeStructured_StaysInTheModelAsItsParagraphs()
        {
            var document = ThatCannotBeStructured();

            Assert.Null(document.Announcement!.Body);
            Assert.Equal(5, document.Announcement.Content.Count);
            Assert.Equal("Koperski", document.Signature?.Surname);
        }

        [Fact]
        public void Export_WritesContentThatCannotBeStructured_AsFlatContent_NotDroppingIt()
        {
            var root = LegalDocumentConverter.Convert(ThatCannotBeStructured()).Elements().Single();

            var provision = root.Elements().Single(e => e.Name.LocalName == "nie-ztp-artykul");
            Assert.Equal("jednostka-nieoznaczona", provision.Attribute("brak-eid")?.Value);
            Assert.StartsWith("Na podstawie art. 18b", provision.Elements().Single(e => e.Name.LocalName == "p").Value);

            // Struktury się nie zgaduje: pozostałe akapity są dalszą treścią, w kolejności dokumentu.
            var further = provision.Elements().Where(e => e.Name.LocalName == "fragment").Select(e => e.Value).ToList();
            Assert.Equal(4, further.Count);
            Assert.StartsWith("1) przepis 1.8.6.2.1 otrzymuje brzmienie:", further[0]);
            Assert.StartsWith("2) w przepisie 2.2.9.3", further[3]);
            Assert.DoesNotContain(provision.Descendants(), e => e.Attribute("eId") is not null);
        }

        [SchemaPlFact]
        public void AnnouncementWithContentThatCannotBeStructured_IsSchemaValid_AsAWhole()
        {
            Assert.Empty(SchemaPl.Validator.Validate(LegalDocumentConverter.Convert(ThatCannotBeStructured())));
        }

        [SchemaPlFact]
        public void AnnouncementWithAProvision_IsSchemaValid_AsAWhole()
        {
            Assert.Empty(SchemaPl.Validator.Validate(LegalDocumentConverter.Convert(WithEnumeration())));
        }

        [Fact]
        public void AnnouncementIssuedJointly_KeepsEverySignature()
        {
            var document = Parse(LegalBasis, "Minister Infrastruktury: wz. P. Koperski", "Minister Finansów i Gospodarki: A. Domański");

            Assert.Equal("Koperski", document.Signature?.Surname);
            Assert.Equal("Domański", Assert.Single(document.CoSignatures).Surname);
        }

        [Fact]
        public void AnnexOfTheAnnouncement_IsItsAnnex()
        {
            var document = Parse(
                LegalBasis, Signature,
                "Załącznik do obwieszczenia Ministra Infrastruktury z dnia 25 lipca 2025 r. (M.P. poz. 702)",
                "Stawki opłat w poszczególnych regionach wodnych nie ulegają zmianie.");

            var annex = Assert.Single(document.Annexes);
            Assert.StartsWith("Załącznik do obwieszczenia Ministra Infrastruktury", Assert.Single(annex.AttachedTo));
            Assert.Contains(annex.Content, block => block.Text == "Stawki opłat w poszczególnych regionach wodnych nie ulegają zmianie.");
            Assert.Empty(document.Articles);
        }

        // ============================================================
        // Eksport
        // ============================================================

        [Fact]
        public void Export_HasTheAnnouncementAsTheRoot()
        {
            var xml = LegalDocumentConverter.Convert(Parse(LegalBasis, Signature));
            var root = xml.Elements().Single();

            Assert.Equal("obwieszczenie", root.Name.LocalName);
            Assert.Equal(
                new[] { "metadane", "rodzaj-aktu", "autor-aktu", "data", "tytul", "podstawa-prawna", "organ-podpisujacy" },
                root.Elements().Select(e => e.Name.LocalName));
            Assert.Equal("OBWIESZCZENIE", Named(xml, "rodzaj-aktu").Single().Value);
            Assert.Equal("MINISTRA INFRASTRUKTURY", Named(xml, "autor-aktu").Single().Value);
            Assert.Equal(LegalBasis, Named(xml, "podstawa-prawna").Single().Value);
            Assert.Equal("OBW", Named(xml, "Rodzaj").Single().Value);
        }

        [Fact]
        public void Export_NamesTheIssuerFromTheSignature()
        {
            var authority = Assert.Single(Named(LegalDocumentConverter.Convert(Parse(LegalBasis, Signature)), "Organ"));

            Assert.Equal("wydajacy", authority.Attribute("rola")?.Value);
            Assert.Equal("Minister Infrastruktury", authority.Elements().Single(e => e.Name.LocalName == "Nazwa").Value);
        }

        [Fact]
        public void Export_PutsTheAnnexUnderTheAnnouncement()
        {
            var xml = LegalDocumentConverter.Convert(Parse(
                LegalBasis, Signature,
                "Załącznik do obwieszczenia Ministra Infrastruktury z dnia 25 lipca 2025 r. (M.P. poz. 702)",
                "Stawki opłat w poszczególnych regionach wodnych nie ulegają zmianie."));

            var annex = Named(xml, "zalacznik").Single();
            Assert.Equal("obwieszczenie", annex.Ancestors().Last(e => e.Name.LocalName is "obwieszczenie" or "ustawa").Name.LocalName);
            Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName == "ustawa");
        }

        [SchemaPlFact]
        public void PlainAnnouncement_IsSchemaValid_AsAWhole()
        {
            Assert.Empty(SchemaPl.Validator.Validate(LegalDocumentConverter.Convert(Parse(LegalBasis, Signature))));
            Assert.Empty(SchemaPl.Validator.Validate(LegalDocumentConverter.Convert(Parse(
                LegalBasis, Signature,
                "Załącznik do obwieszczenia Ministra Infrastruktury z dnia 25 lipca 2025 r. (M.P. poz. 702)",
                "Stawki opłat w poszczególnych regionach wodnych nie ulegają zmianie."))));
        }
    }
}
