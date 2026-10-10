using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Część wspólna, po której wyliczenie biegnie dalej. Dołączona jako zamknięcie wyliczenia okazuje się
    /// czymś innym dopiero przy kolejnej pozycji: zamyka wyliczenie niższe (styl wskazał o poziom za wysoko)
    /// albo przerywa wyliczenie gospodarza (§ 57 ZTP tego nie przewiduje).
    /// </summary>
    public class CommonPartPlacementTests
    {
        private const string PointsCommonPart = "CZWSPPKTczwsplnapunktw";

        private static LegalDocument Parse(params (string Text, string? Style)[] blocks) =>
            LegalDocumentParser.ParseBlocks(
                blocks.Select(block => new DocumentBlock { Text = block.Text, StyleId = block.Style }).ToList());

        private static List<string> Children(XElement xml, string eId) =>
            xml.Descendants().Single(e => e.Attribute("eId")?.Value == eId)
                .Elements().Select(e => e.Name.LocalName).ToList();

        // Publiczne szkoły (301399_2685961): punkty 1–5, „– zwane dalej „szkołami";", punkt 6.
        private static LegalDocument Interrupted() => Parse(
            ("Art. 1. 1. Jednostkami systemu oświaty są:", null),
            ("1) publiczne przedszkola,", null),
            ("2) publiczne szkoły podstawowe", null),
            ("– zwane dalej „szkołami”;", PointsCommonPart),
            ("3) publiczne poradnie psychologiczno-pedagogiczne.", null),
            ("2. Przepis stosuje się odpowiednio.", null));

        [Fact]
        public void CommonPartFollowedByAnotherPoint_InterruptsTheEnumeration()
        {
            var paragraph = Interrupted().Articles.Single().Paragraphs[0];

            Assert.Equal(3, paragraph.Points.Count);
            var part = Assert.Single(paragraph.CommonParts, p => p.Type == CommonPartType.WrapUp);
            Assert.Equal(2, part.InterruptsAfter);
            Assert.Equal("– zwane dalej „szkołami”;", part.ContentText);
        }

        [Fact]
        public void InterruptingCommonPart_IsExportedWhereItStands_WithoutAnEId()
        {
            var xml = LegalDocumentConverter.Convert(Interrupted());

            Assert.Equal(new[] { "nr", "p", "punkt", "punkt", "nie-ztp-czesc-wspolna-punkt", "punkt" },
                Children(xml, "art_1__ust_1"));

            var part = xml.Descendants().Single(e => e.Name.LocalName == "nie-ztp-czesc-wspolna-punkt");
            Assert.Null(part.Attribute("eId"));
            Assert.Equal("nie-ztp-wielokrotna-czesc-wspolna", part.Attribute("brak-eid")?.Value);
        }

        [SchemaPlFact]
        public void InterruptingCommonPart_IsSchemaValid()
        {
            Assert.Empty(SchemaPl.Validator.ValidateActBody(LegalDocumentConverter.Convert(Interrupted()).Elements().First()));
        }

        [Fact]
        public void CommonPartRightAfterLetters_ClosesThem_WhenAnotherPointFollows()
        {
            // Styl mówi „część wspólna punktów", ale stoi za literami punktu 1, a po niej idzie punkt 2.
            var document = Parse(
                ("Art. 1. 1. Certyfikat wydaje się:", null),
                ("1) pracownikom medycznym oraz osobom wykonującym:", null),
                ("a) zawód medyczny,", null),
                ("b) czynności pomocnicze", null),
                ("– upoważnionym do wystawiania zaświadczeń;", PointsCommonPart),
                ("2) lekarzom konsultantom.", null),
                ("2. Przepis stosuje się odpowiednio.", null));

            var paragraph = document.Articles.Single().Paragraphs[0];
            Assert.DoesNotContain(paragraph.CommonParts, p => p.Type == CommonPartType.WrapUp);

            var part = Assert.Single(paragraph.Points[0].CommonParts, p => p.Type == CommonPartType.WrapUp);
            Assert.Null(part.InterruptsAfter);
            Assert.Equal("art_1__ust_1__pkt_1__czw", part.Id);

            var xml = LegalDocumentConverter.Convert(document);
            Assert.Equal(new[] { "nr", "p", "litera", "litera", "czesc-wspolna-litera" }, Children(xml, "art_1__ust_1__pkt_1"));
        }

        [Fact]
        public void CommonPartAfterTheLastPoint_StaysTheClosingPartOfThePoints()
        {
            // Ostatni punkt ma litery, a dalej nie ma już punktu — część wspólna zamyka punkty, jak mówi styl.
            var document = Parse(
                ("Art. 1. 1. Certyfikat wydaje się:", null),
                ("1) lekarzom konsultantom,", null),
                ("2) osobom wykonującym:", null),
                ("a) zawód medyczny,", null),
                ("b) czynności pomocnicze", null),
                ("– upoważnionym do wystawiania zaświadczeń.", PointsCommonPart),
                ("2. Przepis stosuje się odpowiednio.", null));

            var paragraph = document.Articles.Single().Paragraphs[0];
            var part = Assert.Single(paragraph.CommonParts, p => p.Type == CommonPartType.WrapUp);
            Assert.Null(part.InterruptsAfter);
            Assert.Equal("art_1__ust_1__czw", part.Id);
        }

        [Fact]
        public void QuotedCommonPart_FollowsTheSameRule()
        {
            // Przykład autorów schematu (DU/2026/26): cytowany ust. 1, część wspólna liter w stylu części wspólnej punktów.
            var document = Parse(
                ("Art. 1. W ustawie z dnia 13 października 1998 r. o systemie ubezpieczeń społecznych (Dz. U. z 2025 r. poz. 350) w art. 68b ust. 1 otrzymuje brzmienie:", null),
                ("„1. Certyfikat wydaje się:", "ZUSTzmustartykuempunktem"),
                ("1) pracownikom medycznym oraz osobom wykonującym:", "ZPKTzmpktartykuempunktem"),
                ("a) zawód medyczny,", "ZLITwPKTzmlitwpktartykuempunktem"),
                ("b) czynności pomocnicze", "ZLITwPKTzmlitwpktartykuempunktem"),
                ("– upoważnionym do wystawiania zaświadczeń;", "ZCZWSPPKTzmczciwsppktartykuempunktem"),
                ("2) lekarzom konsultantom.”.", "ZPKTzmpktartykuempunktem"),
                ("Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.", null));

            var xml = LegalDocumentConverter.Convert(document);
            var part = xml.Descendants().Single(e => e.Name.LocalName.StartsWith("czesc-wspolna"));

            Assert.Equal("czesc-wspolna-litera", part.Name.LocalName);
            Assert.Equal("art_68b__ust_1__pkt_1__czw", part.Attribute("eId")?.Value);
        }

        // ============================================================
        // Część wspólna jako jedyna treść nowelizacji
        // ============================================================

        [Fact]
        public void AddedCommonPart_IsAStructuralCitation_AddressedUnderTheTarget()
        {
            // Przykład autorów schematu (DU/2026/252, art. 13 pkt 1 lit. a tiret drugie).
            var document = Parse(
                ("Art. 13. W ustawie z dnia 21 lipca 2006 r. o nadzorze nad rynkiem finansowym (Dz. U. z 2025 r. poz. 640) w art. 18zg w ust. 2 dodaje się część wspólną w brzmieniu:", null),
                ("„– w rozumieniu art. 5 ust. 1 i 2 ustawy o krajowym systemie cyberbezpieczeństwa.”.", "ZCZWSPPKTzmczciwsppktartykuempunktem"),
                ("Art. 14. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.", "ARTartustawynprozporzdzenia"));

            var xml = LegalDocumentConverter.Convert(document);

            // Dokument ze stylami: cytat kończy się na akapicie w stylie artykułu, jak w aktach ogłoszonych.
            Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName == "cytat-w-tekscie");
            var citation = xml.Descendants().Single(e => e.Name.LocalName == "cytat-strukt");
            Assert.Equal("art_18zg__ust_2", citation.Attribute("element-eId")?.Value);
            Assert.Equal("dodaj-w", citation.Attribute("funkcja")?.Value);

            var part = Assert.Single(citation.Elements());
            Assert.Equal("czesc-wspolna-punkt", part.Name.LocalName);
            Assert.Equal("art_18zg__ust_2__czw", part.Attribute("eId")?.Value);
        }

        [SchemaPlFact]
        public void AddedCommonPart_IsSchemaValid()
        {
            var document = Parse(
                ("Art. 13. W ustawie z dnia 21 lipca 2006 r. o nadzorze nad rynkiem finansowym (Dz. U. z 2025 r. poz. 640) w art. 18zg w ust. 2 dodaje się część wspólną w brzmieniu:", null),
                ("„– w rozumieniu art. 5 ust. 1 i 2 ustawy o krajowym systemie cyberbezpieczeństwa.”.", "ZCZWSPPKTzmczciwsppktartykuempunktem"),
                ("Art. 14. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.", "ARTartustawynprozporzdzenia"));

            Assert.Empty(SchemaPl.Validator.ValidateActBody(LegalDocumentConverter.Convert(document).Elements().First()));
        }
    }
}
