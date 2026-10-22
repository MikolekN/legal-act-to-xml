using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Załączniki aktu w modelu: treść za nagłówkiem „Załącznik(i) do ustawy z dnia …" nie należy do
    /// struktury aktu, ale nie ginie — trafia do <see cref="LegalDocument.Annexes"/>.
    /// </summary>
    public class AnnexTests
    {
        private const string AttachedToStyle = "TEKSTZacznikido";
        private const string DesignationStyle = "OZNZACZNIKAwskazanienrzacznika";
        private const string TitleStyle = "TYTZALtytuzacznika";

        private static readonly string[] Act =
        [
            "USTAWA", "z dnia 23 stycznia 2026 r.", "o krajowym systemie cyberbezpieczeństwa",
            "Art. 1. Sektory określają załączniki do ustawy.",
            "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
        ];

        private static DocumentBlock Block(string text, string? style = null, BlockAlignment? alignment = null) => new()
        {
            Text = text,
            StyleId = style,
            Layout = alignment is null ? null : new BlockLayoutInfo { Alignment = alignment },
        };

        private static LegalDocument Parse(params DocumentBlock[] annexBlocks) =>
            LegalDocumentParser.Parse(
                [.. Act.Select(text => Block(text)), .. annexBlocks],
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

        [Fact]
        public void DesignationPrintedTwice_IsOneAnnex()
        {
            // Dz. U. 2026 poz. 1320: dziennik złożył „Załącznik nr 1" nad wzorem wniosku, który ma własne oznaczenie —
            // dwa załączniki o tym samym numerze (powtórzony zal_1) zamiast jednego.
            var document = Parse(
                Block("Załączniki do rozporządzenia Ministra Klimatu i Środowiska z dnia 8 października 2026 r."),
                Block("(Dz. U. poz. 1320)"),
                Block("Załącznik nr 1"),
                Block("Załącznik nr 1"),
                Block("WZÓR"),
                Block("Imię i nazwisko wnioskodawcy"),
                Block("Załącznik nr 2"),
                Block("Załącznik nr 2"),
                Block("NIEZAGOSPODAROWANE ZŁOŻA"),
                Block("Czeklin"));

            Assert.Equal(new[] { "zal_1", "zal_2" }, document.Annexes.Select(a => a.Id));
        }

        [Fact]
        public void Annexes_UnderACommonHeading_StartAtTheirDesignations()
        {
            // Układ przykładu autorów schematu (DU/2026/252): wskazanie aktu raz, potem „Załącznik nr N" i tytuł.
            var document = Parse(
                Block("Załączniki do ustawy z dnia 23 stycznia 2026 r.", AttachedToStyle),
                Block("(Dz. U. poz. 252)", AttachedToStyle),
                Block("Załącznik nr 1", DesignationStyle),
                Block("SEKTORY KLUCZOWE", TitleStyle),
                Block("Energia"),
                Block("Załącznik nr 2", DesignationStyle),
                Block("SEKTORY WAŻNE", TitleStyle),
                Block("Usługi pocztowe"),
                Block("Produkcja"));

            Assert.Equal(new[] { "zal_1", "zal_2" }, document.Annexes.Select(a => a.Id));
            Assert.Equal(new[] { "1", "2" }, document.Annexes.Select(a => a.Number));
            Assert.Equal(new[] { "Załącznik nr 1", "Załącznik nr 2" }, document.Annexes.Select(a => a.Heading));
            Assert.Equal(new[] { "SEKTORY KLUCZOWE", "SEKTORY WAŻNE" }, document.Annexes.Select(a => a.Title));

            // Wskazanie aktu stoi raz i niesie je pierwszy załącznik.
            Assert.Equal(new[] { "Załączniki do ustawy z dnia 23 stycznia 2026 r.", "(Dz. U. poz. 252)" }, document.Annexes[0].AttachedTo);
            Assert.Empty(document.Annexes[1].AttachedTo);

            Assert.Equal(new[] { "Energia" }, document.Annexes[0].Content.Select(b => b.Text));
            Assert.Equal(new[] { "Usługi pocztowe", "Produkcja" }, document.Annexes[1].Content.Select(b => b.Text));
        }

        [Fact]
        public void SingleAnnex_HasNoNumber_AndIsAddressedAsTheFirst()
        {
            var document = Parse(
                Block("Załącznik do ustawy z dnia 23 stycznia 2026 r. (Dz. U. poz. 252)"),
                // Tytuł bez własnego stylu: rozstrzyga wyśrodkowanie.
                Block("WYKAZ SEKTORÓW", style: "ARTartustawynprozporzdzenia", alignment: BlockAlignment.Center),
                Block("Energia"));

            var annex = Assert.Single(document.Annexes);
            Assert.Null(annex.Number);
            Assert.Null(annex.Heading);
            Assert.Equal("zal_1", annex.Id);
            Assert.Equal(new[] { "Załącznik do ustawy z dnia 23 stycznia 2026 r. (Dz. U. poz. 252)" }, annex.AttachedTo);
            Assert.Equal("WYKAZ SEKTORÓW", annex.Title);
            Assert.Equal(new[] { "Energia" }, annex.Content.Select(b => b.Text));
        }

        [Fact]
        public void FirstLineWithTheFormOfATitle_IsTheTitle_WithoutStyleOrCentering()
        {
            // Korpus: 292961_2706155 — dokument bez szablonu nie niesie stylu tytułu ani wyśrodkowania;
            // o tytule rozstrzyga miejsce (pierwszy wiersz) i budowa, jak przy tytule rozdziału.
            var document = Parse(
                Block("Załączniki do ustawy z dnia 23 stycznia 2026 r. (Dz. U. poz. 252)"),
                Block("Załącznik nr 1"),
                Block("DROGI KRAJOWE, NA KTÓRYCH POBIERA SIĘ OPŁATĘ ELEKTRONICZNĄ"),
                Block("1) autostrada A1 na odcinku Toruń – Gorzyczki;"),
                Block("2) autostrada A2 na odcinkach:"),
                Block("a) Modła Królewska – węzeł Konotopa,"),
                Block("b) węzeł Lubelska – węzeł Kałuszyn."));

            var annex = Assert.Single(document.Annexes);
            Assert.Equal("DROGI KRAJOWE, NA KTÓRYCH POBIERA SIĘ OPŁATĘ ELEKTRONICZNĄ", annex.Title);

            // Tytuł nie jest już akapitem treści, więc treść jest w całości wyliczeniem i staje się jednostkami.
            Assert.All(annex.Content, block => Assert.NotNull(block.Unit));
            var points = annex.Content
                .Select(block => block.Unit).OfType<Saga.Model.EditorialUnits.Paragraph>()
                .SelectMany(paragraph => paragraph.Points).ToList();
            Assert.Equal(new[] { "zal_1__pkt_1", "zal_1__pkt_2" }, points.Select(point => point.Id));
            Assert.Equal(2, points[1].Letters.Count);
        }

        [Theory]
        // Zdanie, wprowadzenie do wyliczenia i pozycja wyliczenia tytułem nie są — zostają treścią.
        [InlineData("Wykaz obejmuje sektory, o których mowa w art. 5.")]
        [InlineData("Ilekroć w załączniku jest mowa o:")]
        [InlineData("1) energia;")]
        [InlineData("wykaz sektorów")]
        public void FirstLineWithoutTheFormOfATitle_IsContent(string firstLine)
        {
            var document = Parse(
                Block("Załącznik do ustawy z dnia 23 stycznia 2026 r. (Dz. U. poz. 252)"),
                Block(firstLine),
                Block("Energia"));

            var annex = Assert.Single(document.Annexes);
            Assert.Null(annex.Title);
            Assert.Equal(firstLine, annex.Content.First().Text ?? annex.Content.First().Unit?.ContentText);
        }

        [Fact]
        public void CenteredLinesAfterTheTitle_AreContent()
        {
            // Załącznik z rysunkami: podpisy są wyśrodkowane jak tytuł, ale tytułem jest tylko pierwszy wiersz.
            var document = Parse(
                Block("Załącznik do ustawy z dnia 23 stycznia 2026 r."),
                Block("SZTANDAR JEDNOSTKI WOJSKOWEJ", alignment: BlockAlignment.Center),
                Block("strona główna", alignment: BlockAlignment.Center),
                Block("strona odwrotna", alignment: BlockAlignment.Center));

            var annex = Assert.Single(document.Annexes);
            Assert.Equal("SZTANDAR JEDNOSTKI WOJSKOWEJ", annex.Title);
            Assert.Equal(new[] { "strona główna", "strona odwrotna" }, annex.Content.Select(b => b.Text));
        }

        [Fact]
        public void NoteUnderTheHeading_IsContent_NotAnotherHeadingLine()
        {
            // Tekst jednolity: „(pominięty)" stoi w stylu wskazania aktu, ale wskazaniem nie jest.
            var document = Parse(
                Block("Załącznik do ustawy z dnia 13 kwietnia 2016 r. (Dz. U. z 2025 r. poz. 568)", AttachedToStyle),
                Block("(pominięty)", AttachedToStyle));

            var annex = Assert.Single(document.Annexes);
            Assert.Single(annex.AttachedTo);
            Assert.Equal(new[] { "(pominięty)" }, annex.Content.Select(b => b.Text));
        }

        [Theory]
        [InlineData("Załącznik nr 4a", "4a")]
        [InlineData("Załącznik nr 3[2)]", "3")]
        public void Designation_MayCarryALetterOrAFootnoteMarker(string heading, string number)
        {
            var document = Parse(
                Block("Załączniki do ustawy z dnia 23 stycznia 2026 r."),
                Block(heading));

            var annex = Assert.Single(document.Annexes);
            Assert.Equal(number, annex.Number);
            Assert.Equal(heading, annex.Heading);
        }

        [Fact]
        public void NoteBesideTheDesignation_IsTheContentOfTheAnnex()
        {
            // Tekst jednolity: załącznik uchylony nie ma nic poza dopiskiem przy oznaczeniu.
            var document = Parse(
                Block("Załączniki do ustawy z dnia 23 stycznia 2026 r."),
                Block("Załącznik nr 2 (uchylony)"));

            var annex = Assert.Single(document.Annexes);
            Assert.Equal("2", annex.Number);
            Assert.Equal("Załącznik nr 2", annex.Heading);
            Assert.Equal(new[] { "(uchylony)" }, annex.Content.Select(b => b.Text));
        }

        [Fact]
        public void SentenceStartingLikeADesignation_IsContent()
        {
            var document = Parse(
                Block("Załączniki do ustawy z dnia 23 stycznia 2026 r."),
                Block("Załącznik nr 1"),
                Block("WZÓR WNIOSKU", TitleStyle),
                Block("Załącznik nr 2 określa wzór oświadczenia składanego wraz z wnioskiem."));

            var annex = Assert.Single(document.Annexes);
            Assert.Equal(
                new[] { "Załącznik nr 2 określa wzór oświadczenia składanego wraz z wnioskiem." },
                annex.Content.Select(b => b.Text));
        }

        [Fact]
        public void AnnexContent_StaysOutOfTheActStructure()
        {
            // Wyliczenie w załączniku wygląda jak jednostki aktu — nie może dokleić się do ostatniego artykułu.
            var document = Parse(
                Block("Załącznik do ustawy z dnia 23 stycznia 2026 r."),
                Block("WYMOGI", TitleStyle),
                Block("1) inwentaryzację produktów;", "PKTpunkt"),
                Block("2) kontrolowanie wersji.", "PKTpunkt"));

            Assert.Equal(new[] { "art_1", "art_2" }, document.Articles.Select(a => a.Id));
            Assert.Empty(document.Articles.Last().Paragraphs.Single().Points);
            Assert.Equal(
                new[] { "inwentaryzację produktów;", "kontrolowanie wersji." },
                Assert.Single(document.Annexes).Content.SelectMany(b => b.Unit!.Points).Select(point => point.ContentText));
        }

        [Fact]
        public void ActWithoutAnAnnex_HasNone()
        {
            Assert.Empty(Parse().Annexes);
        }

        // ============================================================
        // Wyliczenie w załączniku jako jednostki
        // ============================================================

        // Układ przykładu autorów schematu (DU/2026/252, załącznik nr 4).
        private static LegalDocument ParseWithEnumeration(params DocumentBlock[] extra) => Parse(
        [
            Block("Załączniki do ustawy z dnia 23 stycznia 2026 r.", AttachedToStyle),
            Block("Załącznik nr 4", DesignationStyle),
            Block("WYMOGI DLA SYSTEMU ZARZĄDZANIA", TitleStyle),
            Block("I. System zarządzania bezpieczeństwem informacji obejmuje co najmniej:"),
            Block("1) inwentaryzację produktów ICT;", "PKTpunkt"),
            Block("2) zapewnienie ochrony informacji w zakresie:", "PKTpunkt"),
            Block("a) ochrony fizycznej miejsc,", "LITlitera"),
            Block("b) ochrony wykorzystującej oprogramowanie;", "LITlitera"),
            Block("II. Podmiot zapewnia:"),
            Block("1) szkolenia;", "PKTpunkt"),
            Block("2) audyty.", "PKTpunkt"),
            .. extra,
        ]);

        [Fact]
        public void EnumerationInAnAnnex_BecomesUnitsAddressedFromTheAnnex()
        {
            var annex = Assert.Single(ParseWithEnumeration().Annexes);

            var units = annex.Content.Select(block => block.Unit!).ToList();
            Assert.Equal(new[] { "zal_4__ust_I", "zal_4__ust_II" }, units.Select(u => u.Id));
            Assert.Equal(new[] { "I", "II" }, units.Select(u => u.Number!.Value));
            Assert.Equal("System zarządzania bezpieczeństwem informacji obejmuje co najmniej:", units[0].ContentText);

            Assert.Equal(new[] { "zal_4__ust_I__pkt_1", "zal_4__ust_I__pkt_2" }, units[0].Points.Select(p => p.Id));
            Assert.Equal(
                new[] { "zal_4__ust_I__pkt_2__lit_a", "zal_4__ust_I__pkt_2__lit_b" },
                units[0].Points[1].Letters.Select(l => l.Id));

            // Numeracja punktów zaczyna się w każdym ustępie od nowa — adresy się nie powtarzają.
            Assert.Equal(new[] { "zal_4__ust_II__pkt_1", "zal_4__ust_II__pkt_2" }, units[1].Points.Select(p => p.Id));
        }

        [Fact]
        public void AnnexWithTextOutsideTheEnumeration_KeepsItsParagraphs()
        {
            // Treść nie jest w całości wyliczeniem: nic nie zamieniamy, żeby nic nie zginęło.
            var annex = Assert.Single(ParseWithEnumeration(Block("Objaśnienia do wymogów zawiera odrębny dokument.")).Annexes);

            Assert.All(annex.Content, block => Assert.Null(block.Unit));
            Assert.Equal(9, annex.Content.Count);
            Assert.Equal("I. System zarządzania bezpieczeństwem informacji obejmuje co najmniej:", annex.Content[0].Text);
        }

        [Fact]
        public void EnumerationInTheOnlyUnnumberedAnnex_IsAddressedFromZal1()
        {
            // Jedyny załącznik bez numeru ma adres zal_1, więc jego wyliczenie staje się jednostkami pod nim (ADR-0099).
            var document = Parse(
                Block("Załącznik do ustawy z dnia 23 stycznia 2026 r."),
                Block("WYMOGI", TitleStyle),
                Block("1) inwentaryzację produktów;", "PKTpunkt"),
                Block("2) kontrolowanie wersji.", "PKTpunkt"));

            var units = Assert.Single(document.Annexes).Content.Select(block => block.Unit).ToList();
            Assert.All(units, unit => Assert.NotNull(unit));
            Assert.Equal(new[] { "zal_1__pkt_1", "zal_1__pkt_2" }, units.SelectMany(unit => unit!.Points).Select(point => point.Id));
        }

        [Fact]
        public void EnumerationInAnUnnumberedAnnex_NextToNumberedOnes_StaysAsParagraphs()
        {
            // Załącznik bez adresu — „pkt_1" wyglądałby jak punkt samego aktu.
            var document = Parse(
                Block("Załącznik do ustawy z dnia 23 stycznia 2026 r."),
                Block("1) inwentaryzację produktów;", "PKTpunkt"),
                Block("2) kontrolowanie wersji.", "PKTpunkt"),
                Block("Załącznik nr 2", DesignationStyle),
                Block("Transport"));

            Assert.All(document.Annexes[0].Content, block => Assert.Null(block.Unit));
        }

        // Korpus: 292961_2706155 — objaśnienie gwiazdki z tytułu stoi za wyliczeniem, w szablonie stylem części wspólnej.
        private static DocumentBlock[] AnnexWithAnAsteriskNote(string? noteStyle) =>
        [
            Block("Załączniki do ustawy z dnia 23 stycznia 2026 r.", AttachedToStyle),
            Block("Załącznik nr 1", DesignationStyle),
            Block("DROGI KRAJOWE KLASY A I S*[)], NA KTÓRYCH POBIERA SIĘ OPŁATĘ", TitleStyle),
            Block("1) autostrada A1 na odcinku Toruń – Gorzyczki;", "PKTpunkt"),
            Block("2) autostrada A2 na odcinkach:", "PKTpunkt"),
            Block("a) Modła Królewska – Konotopa,", "LITlitera"),
            Block("b) Lubelska – Kałuszyn.", "LITlitera"),
            Block("*[)]\tZgodnie z § 11 pkt 1 rozporządzenia w sprawie dróg publicznych.", noteStyle),
        ];

        [Theory]
        [InlineData("CZWSPPKTczwsplnapunktw")]
        [InlineData(null)]
        public void AsteriskNoteAfterTheEnumeration_StaysText_AfterTheUnits(string? noteStyle)
        {
            var annex = Assert.Single(Parse(AnnexWithAnAsteriskNote(noteStyle)).Annexes);

            Assert.Equal(2, annex.Content.Count);
            Assert.Equal(["1", "2"], annex.Content[0].Unit!.Points.Select(point => point.Number!.Value));
            Assert.DoesNotContain(annex.Content[0].Unit!.CommonParts, part => part.Type == CommonPartType.WrapUp);
            Assert.StartsWith("*[)]", annex.Content[1].Text);
        }

        [Fact]
        public void AsteriskMarkers_AreExportedAsSuperscripts_NotInTheTextChannelNotation()
        {
            var xml = LegalDocumentConverter.Convert(Parse(AnnexWithAnAsteriskNote(null)));
            var annex = Assert.Single(Named(xml, "zalacznik"));

            // „S*[)]" w tytule i „*[)]" przed objaśnieniem: gwiazdka zwykła, nawias w indeksie górnym.
            Assert.DoesNotContain(annex.DescendantNodes().OfType<XText>(), text => text.Value.Contains("[)]"));
            Assert.Equal([")", ")"], Named(annex, "sup").Select(sup => sup.Value));
        }

        [SchemaPlFact]
        public void AnnexWithAnAsteriskNote_IsSchemaValid_InItsBody()
        {
            var xml = LegalDocumentConverter.Convert(Parse(AnnexWithAnAsteriskNote(null)));

            Assert.Empty(SchemaPl.Validator.ValidateActBody(xml.Elements().First()));
        }

        [Fact]
        public void AnnexUnits_AreExportedInTheSegment_WithTheDateOfTheAct()
        {
            var blocks = new List<DocumentBlock>
            {
                new() { Text = "DZIENNIK USTAW RZECZYPOSPOLITEJ POLSKIEJ", Role = BlockRole.JournalHeader },
                new() { Text = "Warszawa, dnia 2 marca 2026 r.", Role = BlockRole.JournalHeader },
                new() { Text = "Poz. 252", Role = BlockRole.JournalHeader },
            };
            blocks.AddRange(Act.Select(text => Block(text)));
            blocks.AddRange(
            [
                Block("Załączniki do ustawy z dnia 23 stycznia 2026 r."),
                Block("Załącznik nr 4"),
                Block("I. System zarządzania obejmuje co najmniej:"),
                Block("1) inwentaryzację produktów ICT;", "PKTpunkt"),
                Block("2) kontrolowanie wersji.", "PKTpunkt"),
                Block("II. Wymogi uznaje się za spełnione."),
            ]);
            var document = LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

            var segment = Assert.Single(Named(LegalDocumentConverter.Convert(document), "segment-zalacznika"));

            // Oznaczenie rzymskie jest spoza ZTP; punkty pod nim to zwykłe punkty.
            var paragraphs = segment.Elements().ToList();
            Assert.Equal(new[] { "nie-ztp-ustep", "nie-ztp-ustep" }, paragraphs.Select(p => p.Name.LocalName));
            Assert.Equal(new[] { "DU/2026/252__zal_4__ust_I", "DU/2026/252__zal_4__ust_II" }, paragraphs.Select(p => Attr(p, "eId")));
            Assert.Equal("I", paragraphs[0].Elements().First(e => e.Name.LocalName == "nr").Value);

            var points = Named(paragraphs[0], "punkt");
            Assert.Equal(
                new[] { "DU/2026/252__zal_4__ust_I__pkt_1", "DU/2026/252__zal_4__ust_I__pkt_2" },
                points.Select(p => Attr(p, "eId")));

            // Jednostki załącznika wchodzą w życie z aktem (ogłoszenie 2 marca 2026 r., 14 dni).
            Assert.All(paragraphs.Concat(points), unit => Assert.Equal("2026-03-17", Attr(unit, "wejscie-w-zycie")));

            // Niosą datę, ale nie odwołanie do grupy czasowej — jak w przykładzie autorów schematu;
            // jednostki aktu mają jedno i drugie.
            Assert.DoesNotContain(segment.DescendantsAndSelf(), element => element.Attribute("okres") != null);
            Assert.All(Named(segment.AncestorsAndSelf().Last(), "artykul"), article => Assert.NotNull(article.Attribute("okres")));
        }

        [SchemaPlFact]
        public void AnnexUnits_AreSchemaValid_InTheActBody()
        {
            var xml = LegalDocumentConverter.Convert(ParseWithEnumeration());

            Assert.Empty(SchemaPl.Validator.ValidateActBody(xml.Elements().First()));
        }

        // ============================================================
        // Tabela w załączniku
        // ============================================================

        private static string P(string text, string paragraphProperties = "") =>
            $"<w:p><w:pPr>{paragraphProperties}</w:pPr><w:r><w:t>{text}</w:t></w:r></w:p>";

        private static string Cell(string text, string properties = "") => $"<w:tc><w:tcPr>{properties}</w:tcPr>{P(text)}</w:tc>";

        private static string Row(params string[] cells) => $"<w:tr>{string.Concat(cells)}</w:tr>";

        [Fact]
        public void AnnexTable_KeepsRowsCellsAndMerges()
        {
            var body = string.Concat(Act.Select(line => P(line))) +
                P("Załącznik do ustawy z dnia 23 stycznia 2026 r.") +
                P("SEKTORY KLUCZOWE", @"<w:jc w:val=""center""/>") +
                "<w:tbl>" +
                    $"<w:tr><w:trPr><w:tblHeader/></w:trPr>{Cell("Sektor")}{Cell("Podsektor")}</w:tr>" +
                    Row(Cell("Energia", @"<w:vMerge w:val=""restart""/>"), Cell("gaz")) +
                    Row(Cell("", "<w:vMerge/>"), Cell("ropa")) +
                    Row(Cell("Transport", @"<w:gridSpan w:val=""2""/>")) +
                "</w:tbl>" +
                P("Objaśnienie pod tabelą.");

            using var stream = new MemoryStream(DocxFixtures.WithBodyXml(body));
            var document = LegalDocumentParser.Parse(stream, "ustawa.docx",
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

            var annex = Assert.Single(document.Annexes);
            Assert.Equal("SEKTORY KLUCZOWE", annex.Title);

            // Treść w kolejności dokumentu: tabela, potem akapit.
            Assert.Equal(2, annex.Content.Count);
            Assert.Equal("Objaśnienie pod tabelą.", annex.Content[1].Text);

            var table = annex.Content[0].Table!;
            Assert.Equal(new[] { true, false, false, false }, table.Rows.Select(r => r.IsHeader));
            Assert.Equal(new[] { "Sektor", "Podsektor" }, table.Rows[0].Cells.Select(c => c.Paragraphs.Single()));

            // Komórka scalona w pionie stoi w wierszu, w którym się zaczyna; wiersz pod nią ma o jedną mniej.
            Assert.Equal(new[] { 2, 1 }, table.Rows[1].Cells.Select(c => c.RowSpan));
            Assert.Equal(new[] { "ropa" }, table.Rows[2].Cells.Select(c => c.Paragraphs.Single()));
            Assert.Equal(2, Assert.Single(table.Rows[3].Cells).ColumnSpan);
        }

        // ============================================================
        // Eksport
        // ============================================================

        private static List<XElement> Named(XElement xml, string name) =>
            xml.Descendants().Where(e => e.Name.LocalName == name).ToList();

        private static string[] ChildNames(XElement element) =>
            element.Elements().Select(e => e.Name.LocalName).ToArray();

        private static string? Attr(XElement element, string name) =>
            element.Attributes().FirstOrDefault(a => a.Name.LocalName == name)?.Value;

        [Fact]
        public void Annexes_AreExportedAfterTheSignature_WithHeadingNumberTitleAndContent()
        {
            // Układ przykładu autorów schematu (DU/2026/252).
            var blocks = Act.Select(text => Block(text)).ToList();
            blocks.AddRange(
            [
                Block("Prezydent Rzeczypospolitej Polskiej: K. Nawrocki"),
                Block("Załączniki do ustawy z dnia 23 stycznia 2026 r.", AttachedToStyle),
                Block("(Dz. U. poz. 252)", AttachedToStyle),
                Block("Załącznik nr 1", DesignationStyle),
                Block("SEKTORY KLUCZOWE", TitleStyle),
                Block("Energia"),
                Block("Załącznik nr 2", DesignationStyle),
                Block("SEKTORY WAŻNE", TitleStyle),
                Block("Usługi pocztowe"),
            ]);
            var document = LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

            var xml = LegalDocumentConverter.Convert(document);
            var annexes = Named(xml, "zalacznik");

            // Prefiks aktu z nagłówka załączników („Załączniki do ustawy z dnia 23 stycznia 2026 r. (Dz. U. poz. 252)").
            Assert.Equal(new[] { "DU/2026/252__zal_1", "DU/2026/252__zal_2" }, annexes.Select(a => Attr(a, "eId")));
            Assert.Equal(
                new[] { "zalacznik-do", "zalacznik-do", "zalacznik-nr", "tytul-zalacznika", "segment-zalacznika" },
                ChildNames(annexes[0]));
            Assert.Equal(new[] { "zalacznik-nr", "tytul-zalacznika", "segment-zalacznika" }, ChildNames(annexes[1]));

            Assert.Equal("1", annexes[0].Elements().Single(e => e.Name.LocalName == "zalacznik-nr").Elements().Single().Value);
            Assert.Equal("SEKTORY KLUCZOWE", annexes[0].Elements().Single(e => e.Name.LocalName == "tytul-zalacznika").Value);
            Assert.Equal("Energia", annexes[0].Elements().Last().Elements().Single(e => e.Name.LocalName == "tekst-zwykly").Value);

            // Załączniki stoją w akcie za podpisem.
            var act = annexes[0].Parent!;
            Assert.Equal(new[] { "organ-podpisujacy", "zalacznik", "zalacznik" }, ChildNames(act)[^3..]);
        }

        [Fact]
        public void OnlyUnnumberedAnnex_IsAddressedAsTheFirst_WithoutAPrintedNumber()
        {
            // Autorzy schematu (2026-10-18): jedyny załącznik bez numeru to zal_1 — dodany później dostanie nr 2 (ADR-0099).
            var document = Parse(
                Block("Załącznik do ustawy z dnia 23 stycznia 2026 r."),
                Block("WYKAZ SEKTORÓW", TitleStyle),
                Block("Energia"));

            var annex = Assert.Single(Named(LegalDocumentConverter.Convert(document), "zalacznik"));

            Assert.Equal("zal_1", Attr(annex, "eId"));
            Assert.Null(Attr(annex, "brak-eid"));
            Assert.Equal(new[] { "zalacznik-do", "tytul-zalacznika", "segment-zalacznika" }, ChildNames(annex));
        }

        [Fact]
        public void UnnumberedAnnex_NextToNumberedOnes_NamesTheReasonInsteadOfAnEId()
        {
            // Załącznik bez numeru, który nie jest jedyny — adresu akt nie daje (ADR-0024).
            var document = Parse(
                Block("Załącznik do ustawy z dnia 23 stycznia 2026 r."),
                Block("Energia"),
                Block("Załącznik nr 2", DesignationStyle),
                Block("Transport"));

            var annexes = Named(LegalDocumentConverter.Convert(document), "zalacznik");

            Assert.Equal(new string?[] { null, "zal_2" }, annexes.Select(annex => Attr(annex, "eId")));
            Assert.Equal("jednostka-nieoznaczona", Attr(annexes[0], "brak-eid"));
        }

        [Fact]
        public void AnnexEId_CarriesTheActPrefix()
        {
            var blocks = new List<DocumentBlock>
            {
                new() { Text = "DZIENNIK USTAW RZECZYPOSPOLITEJ POLSKIEJ", Role = BlockRole.JournalHeader },
                new() { Text = "Warszawa, dnia 2 marca 2026 r.", Role = BlockRole.JournalHeader },
                new() { Text = "Poz. 252", Role = BlockRole.JournalHeader },
            };
            blocks.AddRange(Act.Select(text => Block(text)));
            blocks.AddRange([Block("Załączniki do ustawy z dnia 23 stycznia 2026 r."), Block("Załącznik nr 1"), Block("Energia")]);
            var document = LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

            var annex = Assert.Single(Named(LegalDocumentConverter.Convert(document), "zalacznik"));

            Assert.Equal("DU/2026/252__zal_1", Attr(annex, "eId"));
        }

        private static XElement ExportWithTable()
        {
            var body = string.Concat(Act.Select(line => P(line))) +
                P("Załącznik do ustawy z dnia 23 stycznia 2026 r.") +
                "<w:tbl>" +
                    $"<w:tr><w:trPr><w:tblHeader/></w:trPr>{Cell("Lp.")}{Cell("Sektor")}{Cell("Podmioty")}</w:tr>" +
                    Row(Cell("1"), Cell("Energia", @"<w:vMerge w:val=""restart""/>"), Cell("gaz")) +
                    Row(Cell("2"), Cell("", "<w:vMerge/>"), Cell("")) +
                    Row(Cell("3"), $"<w:tc><w:tcPr><w:gridSpan w:val=\"2\"/></w:tcPr>{P("1) podmioty publiczne:")}{P("a) jednostki sektora finansów,")}{P("b) instytuty badawcze;")}{P("2) spółki.")}</w:tc>") +
                "</w:tbl>";

            using var stream = new MemoryStream(DocxFixtures.WithBodyXml(body));
            var document = LegalDocumentParser.Parse(stream, "ustawa.docx",
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
            return LegalDocumentConverter.Convert(document);
        }

        [Fact]
        public void AnnexTable_IsExportedWithHeaderRowSpansAndTypedCells()
        {
            var table = Assert.Single(Named(ExportWithTable(), "tabela"));
            var rows = Named(table, "tr");

            Assert.Equal(new[] { "tak", null, null, null }, rows.Select(r => Attr(r, "naglowek")));
            Assert.All(Named(rows[0], "td"), cell => Assert.Equal(new[] { "tytul-kolumny" }, ChildNames(cell)));

            // Liczba porządkowa to „liczba"; komórka scalona w pionie niesie „rowspan" i stoi w jednym wierszu.
            var first = Named(rows[1], "td");
            Assert.Equal(new[] { "liczba" }, ChildNames(first[0]));
            Assert.Equal("2", Attr(first[1], "rowspan"));
            Assert.Equal(new[] { "tekst-zwykly" }, ChildNames(first[2]));

            // Wiersz pod scaleniem ma o komórkę mniej; pusta komórka niesie pusty akapit.
            var second = Named(rows[2], "td");
            Assert.Equal(2, second.Count);
            Assert.Equal(new[] { "tekst-zwykly" }, ChildNames(second[1]));
            Assert.Equal(string.Empty, second[1].Value);

            // Wyliczenie w komórce: punkty to poziom 1, litery pod nimi — poziom 2.
            var last = Named(rows[3], "td")[1];
            Assert.Equal("2", Attr(last, "colspan"));
            Assert.Equal(new[] { "wyliczenie-1", "wyliczenie-2", "wyliczenie-2", "wyliczenie-1" }, ChildNames(last));
        }

        [Fact]
        public void FootnoteInAnAnnex_IsEmbeddedInItsText()
        {
            using var stream = new MemoryStream(DocxFixtures.WithFootnotes(
                ["Zmiany wymienionej ustawy zostały ogłoszone w Dz. U. poz. 2."],
                [.. Act, "Załącznik do ustawy z dnia 23 stycznia 2026 r.", "Operator, o którym mowa w ustawie – Prawo pocztowe{1}."]));
            var document = LegalDocumentParser.Parse(stream, "ustawa.docx",
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

            var text = Assert.Single(Named(LegalDocumentConverter.Convert(document), "tekst-zwykly"));

            var footnote = Assert.Single(text.Elements());
            Assert.Equal("odnosnik", footnote.Name.LocalName);
            Assert.Equal("1", Attr(footnote, "symbol"));
            Assert.DoesNotContain("[", string.Concat(text.Nodes().OfType<XText>().Select(node => node.Value)));
        }

        [Theory]
        [InlineData("[)]Zmiany ustawy zostały ogłoszone w Dz. U. poz. 2.", "Zmiany ustawy zostały ogłoszone w Dz. U. poz. 2.")]
        // Indeks górny znacznika rozciągnięty na dalszy tekst — nawias zamykający stoi w środku treści.
        [InlineData("[)\tZmiany ustawy zostały ogłoszone w Dz. U. poz. 1911 i] 1914.", "Zmiany ustawy zostały ogłoszone w Dz. U. poz. 1911 i 1914.")]
        public void FootnoteText_LosesItsOwnMark(string paragraph, string text)
        {
            Assert.Equal(text, DocxFootnotes.WithoutOwnMark(paragraph));
        }

        [SchemaPlFact]
        public void ActWithAnnexes_IsSchemaValid_AsAWhole()
        {
            var blocks = new List<DocumentBlock>
            {
                new() { Text = "DZIENNIK USTAW RZECZYPOSPOLITEJ POLSKIEJ", Role = BlockRole.JournalHeader },
                new() { Text = "Warszawa, dnia 2 marca 2026 r.", Role = BlockRole.JournalHeader },
                new() { Text = "Poz. 252", Role = BlockRole.JournalHeader },
            };
            blocks.AddRange(Act.Select(text => Block(text)));
            blocks.AddRange(
            [
                Block("Załączniki do ustawy z dnia 23 stycznia 2026 r.", AttachedToStyle),
                Block("(Dz. U. poz. 252)", AttachedToStyle),
                Block("Załącznik nr 1", DesignationStyle),
                Block("SEKTORY KLUCZOWE", TitleStyle),
                Block("Energia"),
                Block("Załącznik nr 2 (uchylony)", DesignationStyle),
            ]);
            var document = LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

            Assert.Empty(SchemaPl.Validator.Validate(LegalDocumentConverter.Convert(document)));
        }

        // Akt z nagłówkiem dziennika i załącznikiem, którego treścią jest wyliczenie (jednostki pod segmentem).
        private static XElement WholeActWithAnnexUnits()
        {
            var blocks = new List<DocumentBlock>
            {
                new() { Text = "DZIENNIK USTAW RZECZYPOSPOLITEJ POLSKIEJ", Role = BlockRole.JournalHeader },
                new() { Text = "Warszawa, dnia 2 marca 2026 r.", Role = BlockRole.JournalHeader },
                new() { Text = "Poz. 252", Role = BlockRole.JournalHeader },
            };
            blocks.AddRange(Act.Select(text => Block(text)));
            blocks.AddRange(
            [
                Block("Załączniki do ustawy z dnia 23 stycznia 2026 r.", AttachedToStyle),
                Block("(Dz. U. poz. 252)", AttachedToStyle),
                Block("Załącznik nr 4", DesignationStyle),
                Block("WYMOGI DLA SYSTEMU ZARZĄDZANIA", TitleStyle),
                Block("I. System zarządzania bezpieczeństwem informacji obejmuje co najmniej:"),
                Block("1) inwentaryzację produktów ICT;", "PKTpunkt"),
                Block("2) zapewnienie ochrony informacji.", "PKTpunkt"),
                Block("II. Podmiot zapewnia:"),
                Block("1) szkolenia;", "PKTpunkt"),
                Block("2) audyty.", "PKTpunkt"),
            ]);
            var document = LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
            return LegalDocumentConverter.Convert(document);
        }

        [SchemaPlFact]
        public void ActWithAnnexUnits_IsSchemaValid_AsAWhole()
        {
            // Jednostki pod segmentem nie mają deklaracji w schemacie (treść dowolna). Walidator .NET brał ich
            // GUID za drugą wartość pola elementu nadrzędnego; libxml2 i Xerces taki dokument przyjmują.
            var xml = WholeActWithAnnexUnits();

            Assert.Contains(xml.Descendants().Where(e => e.Name.LocalName == "segment-zalacznika").Descendants(),
                e => e.Name.LocalName == "punkt");
            Assert.Empty(SchemaPl.Validator.Validate(xml));
        }

        [SchemaPlFact]
        public void RepeatedGuidOfAnAnnexUnit_IsStillReported()
        {
            // Pominięty komunikat nie może ukryć prawdziwego naruszenia: GUID jednostki załącznika powtarzający
            // GUID z treści aktu zgłaszamy sami, bo walidator .NET go tam nie sprawdza.
            var xml = WholeActWithAnnexUnits();
            XAttribute Guid(XElement element) => element.Attributes().Single(a => a.Name.LocalName == "GUID");

            var article = xml.Descendants().First(e => e.Name.LocalName == "artykul");
            var annexPoint = xml.Descendants().Where(e => e.Name.LocalName == "segment-zalacznika").Descendants()
                .First(e => e.Name.LocalName == "punkt");
            Guid(annexPoint).Value = Guid(article).Value;

            var problem = Assert.Single(SchemaPl.Validator.Validate(xml));
            Assert.Contains("Powtórzona wartość atrybutu GUID", problem);
        }

        [SchemaPlFact]
        public void ActWithAnAnnexTable_IsSchemaValid_InItsBody()
        {
            Assert.Empty(SchemaPl.Validator.ValidateActBody(ExportWithTable().Elements().First()));
        }

        // ============================================================
        // Orientacja strony (ADR-0077)
        // ============================================================

        private const string PortraitPage = @"<w:pgSz w:w=""11906"" w:h=""16838""/>";
        private const string LandscapePage = @"<w:pgSz w:w=""16838"" w:h=""11906"" w:orient=""landscape""/>";

        /// <summary>
        /// Załącznik ze wstępem na stronie pionowej i tabelą z objaśnieniem na poziomej — sekcję pionową kończy akapit
        /// wstępu, poziomą — ustawienia sekcji treści, tak jak zapisuje to Word.
        /// </summary>
        private static LegalDocument ParseAnnexWithLandscapeTable(string firstPage = PortraitPage, string lastPage = LandscapePage)
        {
            var body = string.Concat(Act.Select(line => P(line))) +
                P("Załącznik do ustawy z dnia 23 stycznia 2026 r.") +
                P("SEKTORY KLUCZOWE", @"<w:jc w:val=""center""/>") +
                P("Wykaz sektorów zawiera tabela.", $"<w:sectPr>{firstPage}</w:sectPr>") +
                "<w:tbl>" + Row(Cell("Sektor"), Cell("Podsektor")) + Row(Cell("Energia"), Cell("gaz")) + "</w:tbl>" +
                P("Objaśnienie pod tabelą.") +
                $"<w:sectPr>{lastPage}</w:sectPr>";

            using var stream = new MemoryStream(DocxFixtures.WithBodyXml(body));
            return LegalDocumentParser.Parse(stream, "ustawa.docx", new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
        }

        [Fact]
        public void AnnexContent_KeepsTheOrientationOfItsPage()
        {
            var annex = Assert.Single(ParseAnnexWithLandscapeTable().Annexes);

            Assert.Equal(new bool?[] { false, true, true }, annex.Content.Select(block => block.IsLandscape));
        }

        [Fact]
        public void ChangeOfOrientation_StartsANewSegment_MarkedWithTheOrientationOfItsPages()
        {
            var segments = Named(LegalDocumentConverter.Convert(ParseAnnexWithLandscapeTable()), "segment-zalacznika");

            Assert.Equal(new[] { "pionowa", "pozioma" }, segments.Select(segment => Attr(segment, "orientacja")));
            Assert.Equal(new[] { "tekst-zwykly" }, ChildNames(segments[0]));
            Assert.Equal(new[] { "tabela", "tekst-zwykly" }, ChildNames(segments[1]));
        }

        [Fact]
        public void PageWiderThanTall_IsLandscape_WithoutTheOrientationAttribute()
        {
            var annex = Assert.Single(ParseAnnexWithLandscapeTable(lastPage: @"<w:pgSz w:w=""16838"" w:h=""11906""/>").Annexes);

            Assert.Equal(new bool?[] { false, true, true }, annex.Content.Select(block => block.IsLandscape));
        }

        [Fact]
        public void SectionsWithoutPageSize_LeaveTheOrientationUnknown_InOneSegment()
        {
            var segment = Assert.Single(Named(
                LegalDocumentConverter.Convert(ParseAnnexWithLandscapeTable(firstPage: "", lastPage: "")), "segment-zalacznika"));

            Assert.Null(Attr(segment, "orientacja"));
            Assert.Equal(new[] { "tekst-zwykly", "tabela", "tekst-zwykly" }, ChildNames(segment));
        }

        [Fact]
        public void AnnexFromASourceWithoutPageLayout_HasOneSegment_WithoutOrientation()
        {
            var document = Parse(
                Block("Załącznik do ustawy z dnia 23 stycznia 2026 r.", AttachedToStyle),
                Block("SEKTORY KLUCZOWE", TitleStyle),
                Block("Wykaz sektorów zawiera tabela."),
                Block("Objaśnienie pod tabelą."));

            var segment = Assert.Single(Named(LegalDocumentConverter.Convert(document), "segment-zalacznika"));
            Assert.Null(Attr(segment, "orientacja"));
        }

        [SchemaPlFact]
        public void AnnexWithSegmentsOfBothOrientations_IsSchemaValid_InItsBody()
        {
            var xml = LegalDocumentConverter.Convert(ParseAnnexWithLandscapeTable());

            Assert.Empty(SchemaPl.Validator.ValidateActBody(xml.Elements().First()));
        }
    }
}
