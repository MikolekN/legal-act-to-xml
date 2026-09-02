using System.Text;
using Saga.Core.Ingest;
using Saga.Core.Services.Parsing;
using Saga.Model;
using Word = DocumentFormat.OpenXml.Wordprocessing;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Odczyt metadanych publikatora z nagłówka strony aktu ogłoszonego (JournalHeaderParser).
    ///
    /// Dwie osie testów. Pierwsza — sam parser na blokach: co przyjmuje, a co (częściej)
    /// odrzuca. Druga — pełny potok z pakietu DOCX zbudowanego w pamięci, bo tylko on
    /// przechodzi przez prawdziwy odczyt części nagłówkowej w DocxBlockReader.
    ///
    /// Reguła przewodnia: nagłówek jest kanałem OPCJONALNYM. Każdy nierozpoznany układ ma dać
    /// brak metadanych, nigdy wyjątek ani wartość „prawie dobrą" — dokument nieogłoszony
    /// (projekt ustawy) oraz inny rodzaj aktu muszą przechodzić potok bez zmian.
    /// </summary>
    public class JournalHeaderParserTests
    {
        private static readonly string[] JournalHeaderLines =
        {
            "DZIENNIK USTAW",
            "RZECZYPOSPOLITEJ POLSKIEJ",
            "Warszawa, dnia 12 stycznia 2026 r.",
            "Poz. 26",
        };

        private static readonly string[] StatuteBodyLines =
        {
            "USTAWA",
            "z dnia 5 marca 2025 r.",
            "o zmianie ustawy o rzeczach różnych",
            "Art. 1. Wprowadza się zmiany porządkowe.",
            "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
        };

        private static DocumentBlock HeaderBlock(string text)
            => new() { Text = text, Role = BlockRole.JournalHeader };

        private static DocumentBlock BodyBlock(string text)
            => new() { Text = text };

        /// <summary>Uruchamia sam parser nagłówka na świeżym dokumencie.</summary>
        private static LegalDocument ApplyToFreshDocument(params string[] headerLines)
        {
            var document = new LegalDocument();
            JournalHeaderParser.Apply(headerLines.Select(HeaderBlock).ToList(), document);
            return document;
        }

        // ============================================================
        // Odczyt z bloków nagłówka
        // ============================================================

        [Fact]
        public void Apply_FullHeader_ReadsDatePublisherAndPosition()
        {
            var document = ApplyToFreshDocument(JournalHeaderLines);

            Assert.Equal(new DateTime(2026, 1, 12), document.AnnouncementDate);
            Assert.Equal(PublisherType.DziennikUstaw, document.SourceJournal.Publisher);
            Assert.Equal(2026, document.SourceJournal.Year);
            Assert.Equal(new[] { 26 }, document.SourceJournal.Positions);
            Assert.Contains("Warszawa", document.SourceJournal.SourceString);
        }

        [Fact]
        public void Apply_MonitorPolski_Recognized()
        {
            var document = ApplyToFreshDocument(
                "MONITOR POLSKI",
                "DZIENNIK URZĘDOWY RZECZYPOSPOLITEJ POLSKIEJ",
                "Warszawa, dnia 3 lutego 2026 r.",
                "Poz. 118");

            Assert.Equal(PublisherType.MonitorPolski, document.SourceJournal.Publisher);
            Assert.Equal(new DateTime(2026, 2, 3), document.AnnouncementDate);
        }

        [Fact]
        public void Apply_SingleTabSeparatedBlock_Parsed()
        {
            // Nagłówek bywa JEDNYM akapitem rozstrzelonym tabulatorami. Podział musi nastąpić
            // przed Sanitize, które zwija tabulator do spacji i skleiłoby wiersze.
            var document = new LegalDocument();
            JournalHeaderParser.Apply(
                new[] { HeaderBlock("DZIENNIK USTAW\tWarszawa, dnia 12 stycznia 2026 r.\tPoz. 26") },
                document);

            Assert.Equal(new DateTime(2026, 1, 12), document.AnnouncementDate);
            Assert.Equal(new[] { 26 }, document.SourceJournal.Positions);
        }

        [Fact]
        public void Apply_HeaderWithoutPosition_StillReadsDate()
        {
            // Regres wobec starej implementacji: jeden regex wymagał publikatora, daty ORAZ
            // pozycji naraz, więc brak „Poz." gubił także datę ogłoszenia.
            var document = ApplyToFreshDocument(
                "DZIENNIK USTAW",
                "RZECZYPOSPOLITEJ POLSKIEJ",
                "Warszawa, dnia 12 stycznia 2026 r.");

            Assert.Equal(new DateTime(2026, 1, 12), document.AnnouncementDate);
            Assert.Empty(document.SourceJournal.Positions);
        }

        [Fact]
        public void Apply_HeaderWithoutDate_StillReadsPosition()
        {
            var document = ApplyToFreshDocument("DZIENNIK USTAW", "Poz. 26");

            Assert.Null(document.AnnouncementDate);
            Assert.Equal(new[] { 26 }, document.SourceJournal.Positions);
        }

        // ============================================================
        // Odmowy — nagłówek nierozpoznany nie może nic zapisać
        // ============================================================

        [Fact]
        public void Apply_WithoutPublisher_WritesNothing()
        {
            // Sygnał konieczny: „Warszawa, dnia …" bez publikatora trafia się w pismach
            // i projektach — sam z siebie nie jest dowodem ogłoszenia aktu.
            var document = ApplyToFreshDocument("Warszawa, dnia 12 stycznia 2026 r.", "Poz. 26");

            Assert.Null(document.AnnouncementDate);
            Assert.Null(document.SourceJournal.Publisher);
            Assert.Empty(document.SourceJournal.Positions);
            Assert.Empty(document.SourceJournal.SourceString);
        }

        [Fact]
        public void Apply_PublisherAlone_WritesNothing()
        {
            // Wymóg dwóch sygnałów: sam napis publikatora bez daty i bez pozycji nie wystarcza.
            var document = ApplyToFreshDocument("DZIENNIK USTAW", "RZECZYPOSPOLITEJ POLSKIEJ");

            Assert.Null(document.SourceJournal.Publisher);
            Assert.Null(document.AnnouncementDate);
        }

        [Fact]
        public void Apply_ImpossibleDate_LeavesDateNull()
        {
            // 31 lutego: pozycja zostaje odczytana, data NIE — brak jest lepszy niż wartość
            // „prawie dobra", bo od daty ogłoszenia liczą się terminy wejścia w życie.
            var document = ApplyToFreshDocument(
                "DZIENNIK USTAW", "Warszawa, dnia 31 lutego 2026 r.", "Poz. 26");

            Assert.Null(document.AnnouncementDate);
            Assert.Null(document.SourceJournal.Year);
            Assert.Equal(new[] { 26 }, document.SourceJournal.Positions);
        }

        [Fact]
        public void Apply_YearBeforeFirstJournal_Rejected()
        {
            var document = ApplyToFreshDocument(
                "DZIENNIK USTAW", "Warszawa, dnia 12 stycznia 1815 r.", "Poz. 26");

            Assert.Null(document.AnnouncementDate);
        }

        [Fact]
        public void Apply_YearFarInFuture_Rejected()
        {
            var absurd = DateTime.Now.Year + 5;
            var document = ApplyToFreshDocument(
                "DZIENNIK USTAW", "Warszawa, dnia 12 stycznia " + absurd + " r.", "Poz. 26");

            Assert.Null(document.AnnouncementDate);
        }

        [Fact]
        public void Apply_BodyBlocksWithHeaderText_Ignored()
        {
            // Bramka na roli: ta sama treść w biegu głównym dokumentu (np. cytat w uzasadnieniu)
            // nie jest nagłówkiem strony i nie może wypełnić metadanych.
            var document = new LegalDocument();
            JournalHeaderParser.Apply(JournalHeaderLines.Select(BodyBlock).ToList(), document);

            Assert.Null(document.AnnouncementDate);
            Assert.Empty(document.SourceJournal.Positions);
        }

        [Fact]
        public void Apply_DoesNotOverwriteValuesSetEarlier()
        {
            var document = new LegalDocument
            {
                AnnouncementDate = new DateTime(2001, 1, 1),
                SourceJournal = { Publisher = PublisherType.MonitorPolski, Year = 2001 },
            };

            JournalHeaderParser.Apply(JournalHeaderLines.Select(HeaderBlock).ToList(), document);

            Assert.Equal(new DateTime(2001, 1, 1), document.AnnouncementDate);
            Assert.Equal(PublisherType.MonitorPolski, document.SourceJournal.Publisher);
            Assert.Equal(2001, document.SourceJournal.Year);
        }

        [Fact]
        public void Apply_NoBlocks_DoesNotThrow()
        {
            var document = new LegalDocument();

            JournalHeaderParser.Apply(Array.Empty<DocumentBlock>(), document);
            JournalHeaderParser.Apply(null, document);

            Assert.Null(document.AnnouncementDate);
        }

        // ============================================================
        // Pełny potok — DOCX z prawdziwą częścią nagłówkową
        // ============================================================

        private static LegalDocument ParseDocx(byte[] docx)
        {
            using var stream = new MemoryStream(docx);
            var result = LegalDocumentParser.Parse(stream, "ustawa.docx",
                new ParseOptions { Policy = ParsePolicy.AlwaysParse });

            Assert.NotNull(result.Document);
            return result.Document!;
        }

        [Fact]
        public void Parse_DocxWithJournalHeader_FillsMetadataAndResolvesEntryIntoForce()
        {
            var document = ParseDocx(DocxFixtures.WithFirstPageHeader(JournalHeaderLines, StatuteBodyLines));

            Assert.Equal(new DateTime(2026, 1, 12), document.AnnouncementDate);
            Assert.Equal(2026, document.SourceJournal.Year);
            Assert.Equal(new[] { 26 }, document.SourceJournal.Positions);

            // Data ogłoszenia jest punktem odniesienia formuły § 45 ZTP: 12 I + 14 dni
            // (dzień ogłoszenia się nie liczy) → 27 I.
            Assert.Equal(new DateTime(2026, 1, 27), document.EntryIntoForce!.ResolvedDate);
        }

        [Fact]
        public void Parse_DocxWithDefaultHeader_AlsoRead()
        {
            // Word zakłada osobny nagłówek pierwszej strony tylko przy „innej stronie tytułowej";
            // bez niej blok Dz.U. siedzi w nagłówku domyślnym.
            var document = ParseDocx(DocxFixtures.WithDefaultHeader(JournalHeaderLines, StatuteBodyLines));

            Assert.Equal(new DateTime(2026, 1, 12), document.AnnouncementDate);
        }

        [Fact]
        public void Parse_DocxHeaderText_NeverBecomesActContent()
        {
            // Najmocniejsza gwarancja nieskażenia modelu: treść nagłówka jest odrzucana PRZED
            // klasyfikacją akapitu, więc nawet nagłówek udający jednostkę redakcyjną
            // nie zbuduje artykułu ani nie zmieni tytułu aktu.
            var document = ParseDocx(DocxFixtures.WithFirstPageHeader(
                new[] { "DZIENNIK USTAW", "Art. 999. Treść z nagłówka.", "Poz. 26" },
                StatuteBodyLines));

            Assert.Equal(2, document.Articles.Count());
            Assert.DoesNotContain(document.Articles, a => a.ContentText.Contains("nagłówka"));
            Assert.StartsWith("USTAWA", document.Title);
        }

        [Fact]
        public void Parse_DocxWithoutHeader_ParsesActAndLeavesMetadataEmpty()
        {
            // Projekt ustawy / akt nieogłoszony: brak części nagłówkowej nie może niczego zepsuć.
            var document = ParseDocx(DocxFixtures.WithoutHeader(StatuteBodyLines));

            Assert.Null(document.AnnouncementDate);
            Assert.Null(document.SourceJournal.Year);
            Assert.Equal(2, document.Articles.Count());

            // Konsekwencja braku punktu odniesienia (ADR-0008): formuła rozpoznana,
            // data świadomie nierozstrzygnięta — bez podstawiania daty aktu.
            Assert.NotNull(document.EntryIntoForce);
            Assert.Null(document.EntryIntoForce!.ResolvedDate);
        }

        [Fact]
        public void Parse_DocxWithForeignHeader_ParsesActAndLeavesMetadataEmpty()
        {
            // Nagłówek innego rodzaju aktu — nieznany układ. Nie zgadujemy: metadane puste,
            // akt sparsowany normalnie.
            var document = ParseDocx(DocxFixtures.WithFirstPageHeader(
                new[] { "Załącznik do zarządzenia nr 5", "Strona 1 z 12" },
                StatuteBodyLines));

            Assert.Null(document.AnnouncementDate);
            Assert.Empty(document.SourceJournal.Positions);
            Assert.Equal(2, document.Articles.Count());
        }

        [Fact]
        public void Parse_PlainTextWithJournalLines_DoesNotInventAnnouncementDate()
        {
            // Świadome ograniczenie zakresu: kanał TXT nie ma części nagłówkowej, a wiersze
            // publikatora w treści to zwykły tekst. Uznanie ich za nagłówek byłoby zgadywaniem
            // struktury z treści — osobna decyzja, nie efekt uboczny tej zmiany.
            var lines = JournalHeaderLines.Concat(StatuteBodyLines);
            using var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(string.Join("\n", lines)));

            var document = LegalDocumentParser.Parse(stream, "ustawa.txt",
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document;

            Assert.NotNull(document);
            Assert.Null(document!.AnnouncementDate);
            Assert.NotEmpty(document.Articles);
        }

        // ============================================================
        // Układ z korpusu RCL — odwzorowany z prawdziwych plików
        // ============================================================

        [Fact]
        public void Parse_DocxWithRclCorpusHeader_ReadsDateAndPosition()
        {
            // Układ przepisany z korpusu (documents/, 240 z 241 plików): winieta w TRZECIEJ
            // części nagłówkowej podpiętej jako first, wartości daty i pozycji w kontrolkach
            // zawartości (w:sdt), akapit sygnatury z samych spacji, żywa pagina jako nagłówek
            // domyślny. Każdy z tych elementów potrafiłby samodzielnie zepsuć odczyt.
            var document = ParseDocx(DocxFixtures.WithRclJournalHeader(StatuteBodyLines));

            Assert.Equal(new DateTime(2026, 1, 12), document.AnnouncementDate);
            Assert.Equal(PublisherType.DziennikUstaw, document.SourceJournal.Publisher);
            Assert.Equal(2026, document.SourceJournal.Year);
            Assert.Equal(new[] { 26 }, document.SourceJournal.Positions);

            // Winieta nie może wsiąknąć w treść aktu ani w jego tytuł.
            Assert.Equal(2, document.Articles.Count());
            Assert.StartsWith("USTAWA", document.Title);
        }

        [Fact]
        public void Parse_EmptyFirstPageHeader_FallsBackToRunningHeader()
        {
            // Sama obecność odsyłacza first nie znaczy, że jest tam treść — w korpusie części
            // nagłówkowe bywają puste. Pusty nagłówek pierwszej strony nie może przesłonić
            // domyślnego: z żywej paginy da się odczytać publikator i pozycję (daty tam nie ma).
            var document = ParseDocx(DocxFixtures.WithHeaders(
                new[]
                {
                    DocxFixtures.Header(Word.HeaderFooterValues.First, "   "),
                    DocxFixtures.Header(Word.HeaderFooterValues.Default, "Dziennik Ustaw\t–\t2\t–\tPoz.\t26"),
                },
                StatuteBodyLines));

            Assert.Equal(PublisherType.DziennikUstaw, document.SourceJournal.Publisher);
            Assert.Equal(new[] { 26 }, document.SourceJournal.Positions);

            // Żywa pagina nie niesie daty ogłoszenia — i nie wolno jej stamtąd zmyślić.
            Assert.Null(document.AnnouncementDate);
            Assert.Null(document.SourceJournal.Year);
        }

        [Fact]
        public void Parse_RunningHeaderPageNumber_IsNotMistakenForPosition()
        {
            // „Dziennik Ustaw – 2 – Poz. 26": liczba przed „Poz." to numer STRONY.
            var document = ParseDocx(DocxFixtures.WithFirstPageHeader(
                new[] { "Dziennik Ustaw\t–\t2\t–\tPoz.\t26" }, StatuteBodyLines));

            Assert.Equal(new[] { 26 }, document.SourceJournal.Positions);
        }
    }
}
