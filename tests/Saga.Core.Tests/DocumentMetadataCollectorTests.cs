using Saga.Core.Ingest;
using Saga.Core.Services.Parsing;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Testy zbierania metadanych aktu ze strefy tytułowej (Etap 7b, § 16-19 ZTP): rodzaj/data/przedmiot →
    /// <see cref="LegalDocument.Title"/> / <see cref="LegalDocument.ActDate"/>. Sprawdza wierność brzmienia
    /// (bez przeinaczeń), wydobycie daty, ignorowanie prozy sprzed nagłówka i zapieczętowanie na artykule.
    /// </summary>
    public class DocumentMetadataCollectorTests
    {
        // ============================================================
        // Bezpośrednio na kolektorze
        // ============================================================

        [Fact]
        public void Collects_KindDateSubject_IntoVerbatimTitleAndStructuredDate()
        {
            var c = new DocumentMetadataCollector();
            Assert.True(c.Observe("USTAWA"));
            Assert.True(c.Observe("z dnia 1 marca 2024 r."));
            Assert.True(c.Observe("o ochronie konkurencji i konsumentów"));

            var doc = new LegalDocument();
            c.ApplyTo(doc);

            Assert.Equal("USTAWA z dnia 1 marca 2024 r. o ochronie konkurencji i konsumentów", doc.Title);
            Assert.Equal(new DateTime(2024, 3, 1), doc.ActDate);
        }

        [Fact]
        public void Collects_KindDateSubject_AsSeparateLines()
        {
            var c = new DocumentMetadataCollector();
            c.Observe("USTAWA");
            c.Observe("z dnia 1 marca 2024 r.");
            c.Observe("o ochronie konkurencji i konsumentów");

            var doc = new LegalDocument();
            c.ApplyTo(doc);

            Assert.Equal("USTAWA", doc.KindLine);
            Assert.Equal("z dnia 1 marca 2024 r.", doc.DateLine);
            Assert.Equal("o ochronie konkurencji i konsumentów", doc.Subject);
        }

        [Fact]
        public void AmendingTitle_IsTheSubject()
        {
            // Ustawa 316639_2817102 (§ 96 ZTP): tytuł „zmieniająca ustawę …" zamiast „o zmianie ustawy …".
            const string subject = "zmieniająca ustawę – Prawo wodne oraz ustawę o zmianie ustawy – Prawo wodne oraz niektórych innych ustaw";
            var c = new DocumentMetadataCollector();
            c.Observe("Ustawa");
            c.Observe("z dnia 24 stycznia 2025 r.");
            Assert.True(c.Observe(subject));

            var doc = new LegalDocument();
            c.ApplyTo(doc);

            Assert.Equal(subject, doc.Subject);
        }

        [Fact]
        public void ProseBeforeKindHeader_IsIgnored()
        {
            var c = new DocumentMetadataCollector();
            Assert.False(c.Observe("Dziennik Ustaw Rzeczypospolitej Polskiej"));
            Assert.False(c.Observe("o czymś tam")); // przedmiot bez wcześniejszego nagłówka rodzaju — pomijany
            Assert.Equal(0, c.CollectedLineCount);

            var doc = new LegalDocument();
            c.ApplyTo(doc);
            Assert.Equal(string.Empty, doc.Title);
            Assert.Null(doc.ActDate);
        }

        [Fact]
        public void AfterSeal_ObserveReturnsFalse()
        {
            var c = new DocumentMetadataCollector();
            Assert.True(c.Observe("USTAWA"));
            c.Seal();
            Assert.False(c.Observe("z dnia 1 marca 2024 r."));
        }

        [Fact]
        public void InvalidCalendarDate_KeepsLineInTitle_ButLeavesActDateNull()
        {
            var c = new DocumentMetadataCollector();
            c.Observe("USTAWA");
            Assert.True(c.Observe("z dnia 31 lutego 2024 r.")); // wiersz-daty rozpoznany, ale 31 lutego nie istnieje
            c.Observe("o rzeczy niemożliwej");

            var doc = new LegalDocument();
            c.ApplyTo(doc);
            Assert.Contains("z dnia 31 lutego 2024 r.", doc.Title);
            Assert.Null(doc.ActDate);
        }

        [Fact]
        public void ApplyTo_DoesNotOverwriteExistingTitleOrDate()
        {
            var c = new DocumentMetadataCollector();
            c.Observe("USTAWA");
            c.Observe("z dnia 1 marca 2024 r.");

            var doc = new LegalDocument { Title = "Wcześniejszy tytuł", ActDate = new DateTime(2000, 1, 1) };
            c.ApplyTo(doc);

            Assert.Equal("Wcześniejszy tytuł", doc.Title);
            Assert.Equal(new DateTime(2000, 1, 1), doc.ActDate);
        }

        // ============================================================
        // Integracja przez orkiestrator (strefa tytułowa przed pierwszym artykułem)
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

        [Fact]
        public void Pipeline_TitleZoneBeforeFirstArticle_PopulatesDocumentMetadata()
        {
            var (orch, ctx) = NewPipeline();

            orch.ProcessBlock(Blk("USTAWA"), ctx);
            orch.ProcessBlock(Blk("z dnia 6 września 2001 r."), ctx);
            orch.ProcessBlock(Blk("o dostępie do informacji publicznej"), ctx);
            orch.ProcessBlock(Blk("Art. 1. Informacja publiczna podlega udostępnieniu."), ctx);
            orch.Finalize(ctx);

            Assert.Equal("USTAWA z dnia 6 września 2001 r. o dostępie do informacji publicznej", ctx.Document.Title);
            Assert.Equal(new DateTime(2001, 9, 6), ctx.Document.ActDate);
            Assert.Single(ctx.Document.Articles);
        }

        [Fact]
        public void Pipeline_NoTitleZone_LeavesMetadataEmpty()
        {
            var (orch, ctx) = NewPipeline();

            orch.ProcessBlock(Blk("Art. 1. Sama treść, bez strefy tytułowej."), ctx);
            orch.Finalize(ctx);

            Assert.Equal(string.Empty, ctx.Document.Title);
            Assert.Null(ctx.Document.ActDate);
        }

        [Fact]
        public void Pipeline_KindHeaderAfterFirstArticle_IsNotCollected()
        {
            // Po pierwszym artykule kolektor jest zapieczętowany — „USTAWA" w treści nie tworzy tytułu.
            var (orch, ctx) = NewPipeline();

            orch.ProcessBlock(Blk("Art. 1. Treść artykułu."), ctx);
            orch.ProcessBlock(Blk("USTAWA"), ctx);
            orch.ProcessBlock(Blk("z dnia 1 marca 2024 r."), ctx);
            orch.Finalize(ctx);

            Assert.Equal(string.Empty, ctx.Document.Title);
            Assert.Null(ctx.Document.ActDate);
        }

        [Fact]
        public void Pipeline_SectionBasedActWithoutArticle_DoesNotGlueBodyProseIntoTitle()
        {
            // Regresja: akt oparty na „§" nie tworzy artykułu (sygnał Seal ze StructureProcessora nie nadchodzi),
            // więc kolektor domyka strefę tytułową sam — pierwszy wiersz spoza niej („§ 1. …") ją kończy,
            // a późniejsza proza pasująca do wzorca przedmiotu („o ile …") NIE trafia do tytułu.
            var (orch, ctx) = NewPipeline();

            orch.ProcessBlock(Blk("ROZPORZĄDZENIE"), ctx);
            orch.ProcessBlock(Blk("z dnia 1 marca 2024 r."), ctx);
            orch.ProcessBlock(Blk("§ 1. Przepis ogólny."), ctx);
            orch.ProcessBlock(Blk("o ile przepisy odrębne nie stanowią inaczej."), ctx);
            orch.Finalize(ctx);

            Assert.Equal("ROZPORZĄDZENIE z dnia 1 marca 2024 r.", ctx.Document.Title);
            Assert.DoesNotContain("o ile", ctx.Document.Title);
            Assert.Empty(ctx.Document.Articles);
        }

        [Fact]
        public void Collects_IssuingOrganOnSeparateLine()
        {
            // § 120 ust. 4: organ wydający rozporządzenie w osobnym wierszu jest częścią tytułu (nie gubić).
            var c = new DocumentMetadataCollector();
            c.Observe("ROZPORZĄDZENIE");
            Assert.True(c.Observe("MINISTRA ZDROWIA"));
            c.Observe("z dnia 5 stycznia 2020 r.");
            c.Observe("w sprawie badań lekarskich");

            var doc = new LegalDocument();
            c.ApplyTo(doc);

            Assert.Equal("ROZPORZĄDZENIE MINISTRA ZDROWIA z dnia 5 stycznia 2020 r. w sprawie badań lekarskich", doc.Title);
            Assert.Equal(new DateTime(2020, 1, 5), doc.ActDate);
        }

        // ============================================================
        // Drugi sygnał: styl wiersza albo miejsce w dokumencie
        // ============================================================

        private const string KindStyle = "OZNRODZAKTUtznustawalubrozporzdzenieiorganwydajcy";
        private const string SubjectStyle = "TYTUAKTUprzedmiotregulacjiustawylubrozporzdzenia";

        private static LegalDocument Applied(DocumentMetadataCollector collector)
        {
            var document = new LegalDocument();
            collector.ApplyTo(document);
            return document;
        }

        [Theory]
        // Korpus: 327892_2995332, 303866_2776495, 298226_2711116 — pisownia spoza wzorca, wiersz w stylu rodzaju aktu.
        [InlineData("ROZPORZĄDZENIE MINISTRA zDROWIA[1)]", "MINISTRA zDROWIA[1)]")]
        [InlineData("rozporządzenie", "")]
        [InlineData("ZARządzenie NR 249", "")]
        public void KindWordInUnusualSpelling_IsTheKindHeader_WhenTheLineHasTheKindStyle(string line, string issuer)
        {
            var collector = new DocumentMetadataCollector();

            Assert.True(collector.Observe(line, KindStyle));
            Assert.True(collector.Observe("z dnia 18 sierpnia 2025 r."));

            var document = Applied(collector);
            Assert.Equal(line, document.KindLine);
            Assert.Equal(issuer, document.IssuerLine);
            Assert.Equal(new DateTime(2025, 8, 18), document.ActDate);
        }

        [Theory]
        // Słowo rodzaju bez drugiego sygnału to może być proza.
        [InlineData("rozporządzenie", null)]
        [InlineData("Rozporządzenie wchodzi w życie po upływie 14 dni od dnia ogłoszenia.", null)]
        // Sam styl bez słowa rodzaju nagłówka nie czyni (organ w stylu rodzaju przed nagłówkiem).
        [InlineData("Rady Ministrów", KindStyle)]
        public void OneSignalAlone_DoesNotOpenTheTitleZone(string line, string? style)
        {
            var collector = new DocumentMetadataCollector();

            Assert.False(collector.Observe(line, style));
            Assert.Equal(0, collector.CollectedLineCount);
        }

        [Fact]
        public void KindWordInUnusualSpelling_IsTheKindHeader_AtTheStartOfAnAnnouncedAct()
        {
            // Bez stylu wystarcza miejsce: pierwszy wiersz aktu w załączniku do obwieszczenia.
            var collector = new DocumentMetadataCollector { ExpectKindFirst = true };

            Assert.True(collector.Observe("zarządzenie nr 136"));
            Assert.True(collector.Observe("prezesa rady ministrów"));
            Assert.True(collector.Observe("z dnia 14 listopada 2002 r."));
            Assert.True(collector.Observe("w sprawie nadania statutu Urzędowi Lotnictwa Cywilnego"));

            var document = Applied(collector);
            Assert.Equal("zarządzenie nr 136", document.KindLine);
            Assert.Equal("prezesa rady ministrów", document.IssuerLine);
        }

        [Fact]
        public void ExpectedKind_AppliesOnlyToTheFirstLine()
        {
            var collector = new DocumentMetadataCollector { ExpectKindFirst = true };

            Assert.False(collector.Observe("Tekst przed nagłówkiem."));
            Assert.False(collector.Observe("rozporządzenie"));
        }

        [Fact]
        public void TitleCaseIssuerOnTheKindLine_IsRecognisedFromTheTextAlone()
        {
            // Korpus: 316888_2965777 — „Rozporządzenie Ministra Finansów[1)]" w jednym wierszu.
            var collector = new DocumentMetadataCollector();

            Assert.True(collector.Observe("Rozporządzenie Ministra Finansów i Gospodarki[1)]"));
            collector.Observe("z dnia 5 sierpnia 2025 r.");
            collector.Observe("zmieniające rozporządzenie w sprawie zakresu informacji");

            var document = Applied(collector);
            Assert.Equal("Ministra Finansów i Gospodarki[1)]", document.IssuerLine);
            Assert.StartsWith("zmieniające rozporządzenie", document.Subject);
        }

        [Fact]
        public void IssuerOfAJointlyIssuedAct_SpansTwoLines()
        {
            // Korpus: 329092_3181272.
            var collector = new DocumentMetadataCollector();
            collector.Observe("ROZPORZĄDZENIE");
            Assert.True(collector.Observe("MINISTRÓW INFRASTRUKTURY[1)]"));
            Assert.True(collector.Observe("ORAZ SPRAW WEWNĘTRZNYCH I ADMINISTRACJI[2)]"));
            Assert.True(collector.Observe("z dnia 14 stycznia 2026 r."));
            Assert.True(collector.Observe("zmieniające rozporządzenie w sprawie znaków i sygnałów drogowych"));

            var document = Applied(collector);
            Assert.Equal("MINISTRÓW INFRASTRUKTURY[1)] ORAZ SPRAW WEWNĘTRZNYCH I ADMINISTRACJI[2)]", document.IssuerLine);
            Assert.Equal(new DateTime(2026, 1, 14), document.ActDate);
            Assert.StartsWith("zmieniające rozporządzenie", document.Subject);
        }

        [Theory]
        // Tytuł uchylający — wzorzec; tytuł o innym początku — styl tytułu aktu.
        [InlineData("uchylające rozporządzenie w sprawie wprowadzenia stanu klęski żywiołowej", null)]
        [InlineData("dotyczące zasad wynagradzania pracowników", SubjectStyle)]
        public void Subject_IsRecognisedByItsOpeningOrByItsStyle(string subject, string? style)
        {
            var collector = new DocumentMetadataCollector();
            collector.Observe("Rozporządzenie");
            collector.Observe("Rady Ministrów");
            collector.Observe("z dnia 15 października 2024 r.");

            Assert.True(collector.Observe(subject, style));
            Assert.Equal(subject, Applied(collector).Subject);
        }

        [Fact]
        public void UnusualSubjectWithoutAStyle_ClosesTheTitleZone()
        {
            var collector = new DocumentMetadataCollector();
            collector.Observe("Rozporządzenie");
            collector.Observe("z dnia 15 października 2024 r.");

            Assert.False(collector.Observe("dotyczące zasad wynagradzania pracowników"));
            Assert.Equal(string.Empty, Applied(collector).Subject);
        }

        [Fact]
        public void SecondKindHeader_SealsWithoutMerging()
        {
            // Tekst jednolity: „USTAWA" w załączniku nie może zostać doklejona do tytułu obwieszczenia
            // ani nadpisać jego daty datą aktu ogłaszanego.
            var c = new DocumentMetadataCollector();
            c.Observe("OBWIESZCZENIE MARSZAŁKA SEJMU RZECZYPOSPOLITEJ POLSKIEJ");
            c.Observe("z dnia 5 stycznia 2020 r.");
            Assert.False(c.Observe("USTAWA"));
            Assert.False(c.Observe("z dnia 20 lipca 2000 r."));

            var doc = new LegalDocument();
            c.ApplyTo(doc);

            Assert.DoesNotContain("USTAWA", doc.Title);
            Assert.DoesNotContain("20 lipca 2000", doc.Title);
            Assert.Equal(new DateTime(2020, 1, 5), doc.ActDate);
        }
    }
}
