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
        public void AnnouncementBuiltOfSections_KeepsTheActPipeline()
        {
            // Korpus: 340765_3073132 — obwieszczenie z paragrafami; jednostki buduje potok aktu.
            var layout = AnnouncementBoundary.Describe(Blocks(
                [.. Heading, LegalBasis, "§ 1. Stawka wynosi 10 zł.", "§ 2. Obwieszczenie wchodzi w życie z dniem ogłoszenia.", Signature]));

            Assert.True(layout.OpensWithAnnouncement);
            Assert.True(layout.HasProvisions);
            Assert.False(layout.IsPlainAnnouncement);
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
