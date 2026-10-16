using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Model;
using Word = DocumentFormat.OpenXml.Wordprocessing;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Oznaczenie odnośnika jak w akcie (ADR-0061): sekcja dokumentu może wznawiać numerację odnośników i zmieniać
    /// jej zapis, więc numer drukowany nie jest numerem kolejnym odwołania w dokumencie.
    /// </summary>
    public class FootnoteNumberingTests
    {
        private static readonly string[] Notes =
        [
            "Minister kieruje działem administracji rządowej.",
            "Niniejsza ustawa wdraża dyrektywę 2019/1937.",
            "Zmiany wymienionej ustawy zostały ogłoszone w Dz. U. z 2024 r. poz. 100.",
            "Dodany przez art. 1 ustawy z dnia 9 maja 2025 r. (Dz. U. poz. 680).",
        ];

        // Układ plików tekstów jednolitych (329085_2947238): odnośnik obwieszczenia „a)", w ogłaszanym akcie
        // numeracja od nowa.
        private static byte[] AnnouncementThenAct(params string[] actLines) => DocxFixtures.WithFootnoteSections(
            Notes,
            new DocxFixtures.FootnoteSection(Restart: true, Word.NumberFormatValues.LowerLetter,
                "Obwieszczenie", "Ministra Finansów{1}"),
            new DocxFixtures.FootnoteSection(Restart: true, Format: null, actLines));

        private static IReadOnlyList<DocumentBlock> Read(byte[] docx)
        {
            using var stream = new MemoryStream(docx);
            return new DocxBlockReader().ReadBlocks(stream);
        }

        private static List<BlockFootnote> FootnotesOf(byte[] docx) => Read(docx).SelectMany(block => block.Footnotes).ToList();

        // ============================================================
        // Odczyt: oznaczenie, numer kolejny, ciąg numeracji
        // ============================================================

        [Fact]
        public void SectionThatRestartsNumbering_StartsItsFootnotesFromOne()
        {
            var footnotes = FootnotesOf(AnnouncementThenAct(
                "USTAWA", "o ochronie sygnalistów{2}", "Art. 1. Przepisy ustawy{3} stosuje się.", "Art. 2.{4} Ustawa wchodzi w życie."));

            // Oznaczenie jak w akcie: „a)" w obwieszczeniu, „1)", „2)", „3)" w ustawie.
            Assert.Equal(new[] { "a", "1", "2", "3" }, footnotes.Select(f => f.Symbol));

            // Znacznik w tekście jest jednoznaczny: w drugim ciągu numeracji niesie numer spoza zakresu numerów
            // drukowanych, więc „[1)]" wpisane w akcie tekstem nie pomyli się z odwołaniem.
            Assert.Equal(new[] { "1", "100001", "100002", "100003" }, footnotes.Select(f => f.Marker));
            Assert.Equal(new[] { 0, 1, 1, 1 }, footnotes.Select(f => f.Run));
            Assert.Contains(Read(AnnouncementThenAct("USTAWA", "o ochronie sygnalistów{2}")), block => block.Text == "o ochronie sygnalistów[100001)]");
        }

        [Fact]
        public void SectionsWithoutRestart_NumberFootnotesThroughTheDocument()
        {
            var footnotes = FootnotesOf(DocxFixtures.WithFootnoteSections(
                Notes,
                new DocxFixtures.FootnoteSection(Restart: false, Format: null, "USTAWA", "o ochronie sygnalistów{1}"),
                new DocxFixtures.FootnoteSection(Restart: false, Format: null, "Art. 1. Przepisy ustawy{2} stosuje się.{3}")));

            Assert.Equal(new[] { "1", "2", "3" }, footnotes.Select(f => f.Symbol));
            Assert.Equal(new[] { "1", "2", "3" }, footnotes.Select(f => f.Marker));
            Assert.All(footnotes, footnote => Assert.Equal(0, footnote.Run));
        }

        [Fact]
        public void SectionWithoutFootnotes_DoesNotStartANewRun()
        {
            // Najczęstszy układ tekstu jednolitego: obwieszczenie bez odnośników, potem akt. Znaczniki aktu niosą
            // numery drukowane — nowy ciąg numeracji zaczyna się dopiero po sekcji, która odnośniki ma.
            var footnotes = FootnotesOf(DocxFixtures.WithFootnoteSections(
                Notes,
                new DocxFixtures.FootnoteSection(Restart: true, Word.NumberFormatValues.LowerLetter, "Obwieszczenie", "Ministra Finansów"),
                new DocxFixtures.FootnoteSection(Restart: true, Format: null, "USTAWA", "o ochronie sygnalistów{1}", "Art. 1. Przepisy{2} stosuje się.")));

            Assert.Equal(new[] { "1", "2" }, footnotes.Select(f => f.Symbol));
            Assert.Equal(new[] { "1", "2" }, footnotes.Select(f => f.Marker));
            Assert.All(footnotes, footnote => Assert.Equal(0, footnote.Run));
        }

        [Fact]
        public void DocumentWithoutSectionSettings_NumbersFootnotesAsBefore()
        {
            var footnotes = FootnotesOf(DocxFixtures.WithFootnotes(Notes, "USTAWA", "o ochronie sygnalistów{1}", "Art. 1. Przepisy{2} stosuje się."));

            Assert.Equal(new[] { "1", "2" }, footnotes.Select(f => f.Symbol));
            Assert.Equal(new[] { "1", "2" }, footnotes.Select(f => f.Marker));
        }

        [Fact]
        public void ReferenceWithoutAParenthesis_InARestartedSection_IsLeftAsPrinted()
        {
            // Korpus: 343214_3102996 — odwołanie bez nawiasu nie jest znacznikiem odnośnika i trafia do treści
            // tak, jak stoi w akcie; numer znacznika dalszego ciągu nie może z niego wyciec.
            var block = new DocumentBlock
            {
                Text = "Uzasadnienie[100001]: zgodnie z umową[100002)] i art. 5[1]",
                Footnotes =
                [
                    new BlockFootnote("1", []) { Marker = "100001", Run = 1 },
                    new BlockFootnote("2", []) { Marker = "100002", Run = 1 },
                ],
            };

            Assert.Equal(
                "Uzasadnienie[1]: zgodnie z umową[100002)] i art. 5[1]",
                DocxFootnotes.WithBareReferencesAsPrinted(block.Text, block.Footnotes));
        }

        [Theory]
        [InlineData(1, "a")]
        [InlineData(26, "z")]
        [InlineData(27, "aa")]
        public void LetterNumbering_RepeatsTheLetterAfterZ(int count, string last)
        {
            var notes = Enumerable.Range(1, count).Select(i => $"Odnośnik {i}.").ToList();
            var line = string.Concat(Enumerable.Range(1, count).Select(i => $"słowo{{{i}}} "));
            var footnotes = FootnotesOf(DocxFixtures.WithFootnoteSections(
                notes, new DocxFixtures.FootnoteSection(Restart: false, Word.NumberFormatValues.LowerLetter, line)));

            Assert.Equal(last, footnotes[^1].Symbol);
        }

        // ============================================================
        // Rozstrzyganie znacznika
        // ============================================================

        private static Footnote Note(string marker, string symbol, int run) => new() { Marker = marker, Symbol = symbol, Run = run };

        private static FootnoteResolver TwoRuns() =>
            new([Note("1", "1", 0), Note("2", "2", 0), Note("100001", "1", 1), Note("100002", "2", 1)]);

        [Fact]
        public void FirstMarker_IsTheFootnoteStandingUnderThatNumber()
        {
            var resolver = TwoRuns();

            Assert.Equal(0, resolver.First("1")?.Run);
            Assert.Equal(1, resolver.First("100001")?.Run);

            // Numery pierwszych wystąpień rosną — numer już miniony pierwszym wystąpieniem nie jest.
            Assert.Null(resolver.First("100001"));
            Assert.Null(resolver.First("2"));
            Assert.Null(resolver.First("9"));
        }

        [Fact]
        public void RepeatedMarker_CarriesThePrintedNumber_OfTheCurrentRun()
        {
            var resolver = TwoRuns();
            resolver.First("1");
            Assert.Equal("1", resolver.Repeated("1")?.Marker);

            resolver.First("100001");
            resolver.First("100002");

            // „[1)]" wpisane tekstem w ogłaszanym akcie to jego odnośnik „1)" — nie pierwszy odnośnik dokumentu.
            Assert.Equal("100001", resolver.Repeated("1")?.Marker);
            Assert.Equal("100002", resolver.Repeated("2")?.Marker);
            Assert.Null(resolver.Repeated("3"));
        }

        [Fact]
        public void TextMarkerBeforeTheSectionOfItsNumber_DoesNotTakeAFootnoteOfTheNextSection()
        {
            // Korpus: 308671_2972970 — w obwieszczeniu przytoczony odnośnik „2)" aktu zmieniającego; ogłaszany akt
            // ma własny odnośnik „2)", ale jego znacznik niesie numer, którego tekst aktu nie zawiera.
            var resolver = new FootnoteResolver([Note("1", "1", 0), Note("100001", "1", 1), Note("100002", "2", 1)]);
            resolver.First("1");

            Assert.Null(resolver.First("2"));
            Assert.Null(resolver.Repeated("2"));
            Assert.Equal("2", resolver.First("100002")?.Symbol);
        }

        [Fact]
        public void MarkerShownWithoutItsFootnote_IsWrittenAsPrinted()
        {
            var resolver = TwoRuns();

            Assert.Equal("2)", resolver.AsPrinted("100002)"));
            Assert.Equal("1), 2)", resolver.AsPrinted("100001), 100002)"));

            // Numer, pod którym nie stoi odwołanie dalszego ciągu, zostaje, jak stoi.
            Assert.Equal("2)", resolver.AsPrinted("2)"));
            Assert.Equal("7)", resolver.AsPrinted("7)"));
        }

        [Fact]
        public void RepeatedMarker_DoesNotReachAFootnoteNotMetYet()
        {
            var resolver = new FootnoteResolver([Note("1", "1", 0), Note("2", "2", 0)]);
            resolver.First("1");

            Assert.Null(resolver.Repeated("2"));
        }

        // ============================================================
        // Eksport
        // ============================================================

        private static XElement Export(params string[] actLines)
        {
            using var stream = new MemoryStream(AnnouncementThenAct(actLines));
            var document = LegalDocumentParser.Parse(stream, "akt.docx", new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
            return LegalDocumentConverter.Convert(document);
        }

        private static List<XElement> Named(XElement xml, string name) =>
            xml.Descendants().Where(e => e.Name.LocalName == name).ToList();

        [Fact]
        public void ExportedFootnote_CarriesThePrintedSymbol_AndAUniqueIdentifier()
        {
            var xml = Export(
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o ochronie sygnalistów",
                "Art. 1. Ustawa{2} określa zasady ochrony, a przepisy ustawy{3} stosuje się.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

            // Oznaczenie jak w akcie; identyfikator — numer kolejny w dokumencie, bo oznaczenia się powtarzają.
            var footnotes = Named(xml, "odnosnik");
            Assert.Equal(new[] { "a", "1", "2" }, footnotes.Select(f => f.Attribute("symbol")?.Value));
            Assert.Equal(new[] { "1", "100001", "100002" }, footnotes.Select(f => f.Attribute("id-odnosnika")?.Value));
        }

        [Fact]
        public void RepeatedMarkerInARestartedSection_PointsAtTheFootnoteOfThatSection()
        {
            // Korpus: 329304_2949179 — powtórzony znacznik „1)" jest w pliku tekstem i niesie numer drukowany.
            var xml = Export(
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o ochronie sygnalistów",
                "Art. 1. 1. {2} Ustawa określa zasady ochrony.",
                "2. {1)} Przepisy stosuje się odpowiednio.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

            var reference = Assert.Single(Named(xml, "odsylacz-do-odnosnika"));
            Assert.Equal("100001", reference.Attribute("id-odnosnika")?.Value);
            Assert.Equal("1", reference.Attribute("symbol")?.Value);

            var footnote = Assert.Single(Named(xml, "odnosnik"), f => f.Attribute("id-odnosnika")?.Value == "100001");
            Assert.Contains("dyrektywę", footnote.Value);
        }

        // ============================================================
        // Odwołanie z własnym znakiem (w:customMarkFollows) — ADR-0095
        // ============================================================

        private static byte[] WithCustomMark(string line) => DocxFixtures.WithFootnotes(
            Notes, "USTAWA", "o ochronie sygnalistów{1}", line, "Art. 2. Ustawa{3} wchodzi w życie.");

        [Fact]
        public void CustomMarkReference_IsAFootnote_WithItsOwnMarkAsTheSymbol()
        {
            // Korpus: 346446_3115101 — „3." i znak „3)" wpisany za odwołaniem w jego przebiegu; Word nie liczy go
            // licznikiem, więc następny zwykły odnośnik to „2)".
            var docx = WithCustomMark("Art. 1. Przepisy{2:3)} stosuje się.");

            var footnotes = FootnotesOf(docx);
            Assert.Equal(new[] { "1", "3", "2" }, footnotes.Select(f => f.Symbol));
            Assert.Equal(new[] { "1", "2", "3" }, footnotes.Select(f => f.Marker));

            // Znacznik z numerem kolejnym — bez identyfikatora w:id i bez powtórzonego znaku.
            Assert.Contains(Read(docx), block => block.Text == "Art. 1. Przepisy[2)] stosuje się.");
        }

        [Theory]
        // 337123_3071682: gwiazdka w zwykłym przebiegu, nawias w następnym, w indeksie górnym.
        [InlineData("Art. 1. Przepisy{2;*}{) }stosuje się.", "Art. 1. Przepisy[2)] stosuje się.", "*")]
        // 349921_3119995: „za" i nawias znaku w indeksie górnym, a za nim nawias zamykający wtrącenie.
        [InlineData("Art. 1. Przepisy (lit. a{2:za}{)}) stosuje się.", "Art. 1. Przepisy (lit. a[2)]) stosuje się.", "za")]
        public void CustomMarkWithoutItsParenthesis_TakesTheParenthesisOfTheNextSuperscriptRun(string line, string text, string symbol)
        {
            var docx = WithCustomMark(line);

            Assert.Contains(Read(docx), block => block.Text == text);
            Assert.Equal(symbol, FootnotesOf(docx)[1].Symbol);
        }

        [Fact]
        public void CustomMarkFootnote_IsExportedAtItsPlace()
        {
            using var stream = new MemoryStream(WithCustomMark("Art. 1. Przepisy{2:*)} stosuje się."));
            var xml = LegalDocumentConverter.Convert(
                LegalDocumentParser.Parse(stream, "akt.docx", new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!);

            var footnote = Assert.Single(Named(xml, "odnosnik"), f => f.Attribute("symbol")?.Value == "*");
            Assert.Equal(Notes[1], footnote.Value.Trim());
            Assert.Equal("artykul", footnote.Ancestors().First(e => e.Attribute("eId") != null).Name.LocalName);
            Assert.DoesNotContain("[", xml.Value);
        }

        [Fact]
        public void CustomMarkFootnote_DoesNotSwitchTheRunOfRepeatedMarkers()
        {
            // Korpus: 341275_3063424 — załącznik w sekcji wznawiającej numerację; jego odnośnik ma własny znak „6)",
            // ciągnący numerację aktu, a „1)" wpisane w tabeli załącznika powtarzają odnośnik aktu.
            var resolver = new FootnoteResolver(
                [Note("1", "1", 0), Note("2", "2", 0), new Footnote { Marker = "100001", Symbol = "6", Run = 1, IsCustomMark = true }]);
            resolver.First("1");
            resolver.First("2");

            Assert.Equal("6", resolver.First("100001")?.Symbol);
            Assert.Equal("1", resolver.Repeated("1")?.Marker);

            // Sam własny znak powtarza się w ciągu bieżącym.
            Assert.Equal("100001", resolver.Repeated("6")?.Marker);
        }

        [Theory]
        [InlineData("[3)]W brzmieniu ustalonym", "3", "W brzmieniu ustalonym")]
        [InlineData("za)Zmiana wymienionego", "za", "Zmiana wymienionego")]
        [InlineData("*)WSSE oznacza", "*", "WSSE oznacza")]
        [InlineData("1 Niniejszy dokument", "1", "Niniejszy dokument")]
        [InlineData("12 lutego 2024 r.", "1", "12 lutego 2024 r.")]
        public void CustomMarkTypedAtTheStartOfTheFootnote_IsNotItsContent(string text, string symbol, string content)
        {
            Assert.Equal(content, DocxFootnotes.WithoutTypedCustomMark(text, symbol));
        }
    }
}
