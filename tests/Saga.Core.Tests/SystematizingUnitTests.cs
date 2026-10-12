using DocumentFormat.OpenXml.Wordprocessing;
using Saga.Core.Helpers;
using Saga.Core.Services.Classify;
using Saga.Core.Services.Parsing;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Testy jednostek systematyzacyjnych (Etap 6b, § 60-62 ZTP): rozpoznanie z treści bez stylu,
    /// przejęcie węzła niejawnego / tworzenie rodzeństwa, wzorzec dwuwierszowy (tytuł) oraz wpływ na eId.
    /// </summary>
    public class SystematizingUnitTests
    {
        private static ParsingContext ConnectedContext()
        {
            // Kontekst z PODŁĄCZONYM oddziałem z drzewa dokumentu (jak LegalDocumentParser).
            var document = new LegalDocument
            {
                Type = LegalActType.Statute,
                SourceJournal = new JournalInfo { Year = 2024, Positions = { 1 } },
            };
            var subchapter = document.RootPart.Books[0].Titles[0].Divisions[0].Chapters[0].Subchapters[0];
            return new ParsingContext(document, subchapter);
        }

        private static Paragraph P(string text)
        {
            var p = new Paragraph();
            p.Append(new Run(new Text(text)));
            return p;
        }

        private static void Feed(ParserOrchestrator orch, ParsingContext ctx, params string[] lines)
        {
            foreach (var line in lines)
                orch.ProcessParagraph(P(line), ctx);
        }

        /// <summary>
        /// Ścieżka jednostek systematyzacyjnych, w których stoi artykuł — eId jego oddziału (oddział
        /// niejawny oddaje ścieżkę najbliższych jawnych przodków). eId artykułu jej nie zawiera
        /// (ADR-0017), więc umiejscowienie sprawdza się tutaj, a nie w eId artykułu.
        /// </summary>
        private static string UnitPath(BaseEntity article) => article.Parent!.Id;

        // ============================================================
        // RomanNumeralConverter
        // ============================================================

        [Theory]
        [InlineData("I", 1)]
        [InlineData("IV", 4)]
        [InlineData("IX", 9)]
        [InlineData("XII", 12)]
        [InlineData("XL", 40)]
        public void Roman_Parses(string roman, int expected) =>
            Assert.Equal(expected, RomanNumeralConverter.RomanToInt(roman));

        [Theory]
        [InlineData("IIII")]  // niekanoniczne
        [InlineData("VX")]    // niekanoniczne
        [InlineData("ABC")]   // nie-rzymskie
        [InlineData("")]
        public void Roman_Invalid_ReturnsNull(string roman) =>
            Assert.Null(RomanNumeralConverter.RomanToInt(roman));

        [Theory]
        [InlineData("pierwsza", 1)]
        [InlineData("PIERWSZA", 1)]
        [InlineData("trzecia", 3)]
        // Formy MĘSKIE — wymagane przez „DZIAŁ PIERWSZY"/„TYTUŁ PIERWSZY"; wcześniej cicho padały,
        // bo słownik jednostek wyższych znał wyłącznie rodzaj żeński.
        [InlineData("pierwszy", 1)]
        [InlineData("trzeci", 3)]
        public void WordOrdinal_Parses(string word, int expected)
        {
            Assert.True(PolishNumerals.TryParseOrdinal(word, out var value));
            Assert.Equal(expected, value);
        }

        // ============================================================
        // Budowanie drzewa
        // ============================================================

        [Fact]
        public void Chapters_TakeoverThenSibling_WithHeadingsAndArticleEIds()
        {
            var orch = new ParserOrchestrator();
            var ctx = ConnectedContext();

            Feed(orch, ctx,
                "Rozdział 1", "Przepisy ogólne",
                "Art. 1. Tresc pierwsza.",
                "Art. 2. Tresc druga.",
                "Rozdział 2", "Odpowiedzialność",
                "Art. 3. Tresc trzecia.");

            var division = ctx.Document.RootPart.Books[0].Titles[0].Divisions[0];
            Assert.Equal(2, division.Chapters.Count);

            // Rozdział 1 przejął węzeł niejawny; Rozdział 2 to rodzeństwo.
            Assert.False(division.Chapters[0].IsImplicit);
            Assert.Equal("1", division.Chapters[0].Number?.Value);
            Assert.Equal("Przepisy ogólne", division.Chapters[0].Heading);
            Assert.False(division.Chapters[1].IsImplicit);
            Assert.Equal("2", division.Chapters[1].Number?.Value);
            Assert.Equal("Odpowiedzialność", division.Chapters[1].Heading);

            var placed = ctx.Document.Articles.Select(a => $"{UnitPath(a)} {a.Id}").ToList();
            Assert.Equal(new[] { "rozdz_1 art_1", "rozdz_1 art_2", "rozdz_2 art_3" }, placed);
        }

        [Fact]
        public void Division_Roman_TakesOverImplicit_WithArabicEId()
        {
            var orch = new ParserOrchestrator();
            var ctx = ConnectedContext();

            Feed(orch, ctx, "DZIAŁ II", "Art. 5. Tresc.");

            var division = ctx.Document.RootPart.Books[0].Titles[0].Divisions[0];
            Assert.False(division.IsImplicit);
            Assert.Equal("2", division.Number?.Value);
            Assert.Equal("II", division.Number?.RawValue);
            Assert.Equal(2, division.Number?.NumericPart);

            var article = ctx.Document.Articles.Single();
            Assert.Equal("dzial_2", UnitPath(article));
            Assert.Equal("art_5", article.Id);
        }

        [Fact]
        public void Subchapter_UnderChapter_ProducesNestedEId()
        {
            var orch = new ParserOrchestrator();
            var ctx = ConnectedContext();

            Feed(orch, ctx, "Rozdział 1", "Oddział 1", "Art. 7. Tresc.");

            var article = ctx.Document.Articles.Single();
            Assert.Equal("rozdz_1__oddz_1", UnitPath(article));
            Assert.Equal("art_7", article.Id);
        }

        [Fact]
        public void ArticleBeforeFirstChapter_IsNotReparented()
        {
            // Strażnik: przejęcie węzła niejawnego tylko gdy nie ma jeszcze artykułów pod nim —
            // artykuł sprzed pierwszego Rozdziału nie może trafić do tego rozdziału.
            var orch = new ParserOrchestrator();
            var ctx = ConnectedContext();

            Feed(orch, ctx, "Art. 1. Przed rozdzialem.", "Rozdział 1", "Art. 2. W rozdziale.");

            var placed = ctx.Document.Articles.Select(a => $"[{UnitPath(a)}] {a.Id}").ToList();
            Assert.Equal(new[] { "[] art_1", "[rozdz_1] art_2" }, placed);
        }

        [Fact]
        public void NamedPartWithoutNumber_DoesNotBecomeUnit()
        {
            var orch = new ParserOrchestrator();
            var ctx = ConnectedContext();

            Feed(orch, ctx, "Rozdział 1", "Art. 1. Tresc.");
            // „CZĘŚĆ OGÓLNA" (bez numeru) nie jest jednostką — RootPart pozostaje niejawny.
            Assert.True(ctx.Document.RootPart.IsImplicit);
        }

        // ============================================================
        // Regresje z przeglądu adwersaryjnego 6b
        // ============================================================

        [Theory]
        [InlineData("Rozdział 5 stosuje się odpowiednio do umów.")]
        [InlineData("Oddział 2 niniejszej ustawy stosuje się odpowiednio.")]
        [InlineData("Tytuł V wprowadza się z dniem ogłoszenia.")]
        [InlineData("Dział III otrzymuje brzmienie:")]
        [InlineData("Część I stosuje się odpowiednio")]
        public void ProseReferencingUnit_IsNotClassifiedAsUnit(string text)
        {
            // Kotwica $ na całej linii: proza odwołująca się do jednostki nie jest brana za nagłówek
            // (inaczej gubiłaby treść i tworzyła widmowe jednostki).
            var result = new ParagraphClassifier().Classify(new ClassificationInput(text, null));

            Assert.False(IsSystematizing(result.Kind), $"'{text}' nie powinno być jednostką, jest {result.Kind}");
        }

        [Theory]
        [InlineData("DZIAŁ I.")]
        [InlineData("CZĘŚĆ II,")]
        [InlineData("ROZDZIAŁ 3.")]
        public void UnitWithTrailingPunctuation_IsStillRecognized(string text)
        {
            // Końcowa kropka/przecinek przyklejone do markera nie mogą zgubić jednostki.
            var result = new ParagraphClassifier().Classify(new ClassificationInput(text, null));

            Assert.True(IsSystematizing(result.Kind), $"'{text}' powinno być jednostką, jest {result.Kind}");
        }

        [Fact]
        public void ChapterWithUppercaseLetterSuffix_IsRecognized()
        {
            var orch = new ParserOrchestrator();
            var ctx = ConnectedContext();
            Feed(orch, ctx, "ROZDZIAŁ 1A", "Art. 1. Tresc.");

            Assert.Equal("rozdz_1A", UnitPath(ctx.Document.Articles.Single()));
        }

        [Fact]
        public void DivisionWithRomanLetterSuffix_IsRecognized_WithNormalizedEId()
        {
            var orch = new ParserOrchestrator();
            var ctx = ConnectedContext();
            Feed(orch, ctx, "DZIAŁ IVa", "Art. 5. Tresc.");

            var division = ctx.Document.RootPart.Books[0].Titles[0].Divisions[0];
            Assert.Equal("4a", division.Number?.Value);
            Assert.Equal("IVa", division.Number?.RawValue);
            Assert.Equal(4, division.Number?.NumericPart);
            Assert.Equal("dzial_4a", UnitPath(ctx.Document.Articles.Single()));
        }

        [Fact]
        public void BookWithWordOrdinal_HasArabicEId()
        {
            // „KSIĘGA PIERWSZA" i „KSIĘGA I" muszą dać ten sam eId (ksi_1) — liczba arabska (ADR-0018).
            var orch = new ParserOrchestrator();
            var ctx = ConnectedContext();
            Feed(orch, ctx, "KSIĘGA PIERWSZA", "Art. 1. Tresc.");

            var book = ctx.Document.RootPart.Books[0];
            Assert.False(book.IsImplicit);
            Assert.Equal("1", book.Number?.Value);
            Assert.Equal("PIERWSZA", book.Number?.RawValue);
            Assert.Equal("ksi_1", UnitPath(ctx.Document.Articles.Single()));
        }

        [Fact]
        public void StyledChapterAfterAmendmentStyledBlock_ClosesTheAmendment()
        {
            // TJ 330983_2964483: pominięte przepisy zmieniające stoją w stylu nowelizacji, a zaraz po nich
            // rozdział w stylu szablonu. Styl jednostki systematyzacyjnej to styl ustawy matki — zamyka
            // nowelizację, zamiast zostać wciągnięty do jej treści.
            var document = LegalDocumentParser.ParseBlocks(new (string Text, string Style)[]
            {
                ("DZIAŁ XIII", "TYTDZOZNoznaczenietytuulubdziau"),
                ("Zmiany w przepisach, przepisy przejściowe, dostosowujące i końcowe", "TYTDZPRZEDMprzedmiotregulacjitytuulubdziau"),
                ("Rozdział 1", "ROZDZODDZOZNoznaczenierozdziauluboddziau"),
                ("Zmiany w przepisach", "ROZDZODDZPRZEDMprzedmiotregulacjirozdziauluboddziau"),
                ("Art. 480–524. (pominięte)", "ZPKTzmpktartykuempunktem"),
                ("Rozdział 2", "ROZDZODDZOZNoznaczenierozdziauluboddziau"),
                ("Przepisy przejściowe, dostosowujące i końcowe", "ROZDZODDZPRZEDMprzedmiotregulacjirozdziauluboddziau"),
                ("Art. 525. Z dniem wejścia w życie ustawy znosi się Prezesa Krajowego Zarządu Gospodarki Wodnej.", "ARTartustawynprozporzdzenia"),
            }.Select(line => new Ingest.DocumentBlock { Text = line.Text, StyleId = line.Style }).ToList());

            var article = document.Articles.Single(a => a.Number?.Value == "525");
            Assert.Equal("dzial_13__rozdz_2", UnitPath(article));
            Assert.Equal("Przepisy przejściowe, dostosowujące i końcowe",
                ((Saga.Model.SystematizingUnits.ISystematizingUnit)article.Parent!.Parent!).Heading);
        }

        [Fact]
        public void TableCellsInDesignationStyle_StayInTheAmendmentContent()
        {
            // Ustawa 315576_2810842: dodawana część tabeli ma komórki w stylach oznaczeń i tytułów jednostek.
            // Bez oznaczenia potwierdzonego treścią to nie jest styl ustawy matki — nowelizacja trwa.
            var description = ModelEquivalenceComparer.Describe(ParseStyled(
                ("Art. 3. W załączniku do ustawy w tabeli nr 3 dodaje się część VII w brzmieniu:", "ARTartustawynprozporzdzenia"),
                ("Część VII", "TYTDZPRZEDMprzedmiotregulacjitytuulubdziau"),
                ("Sprawowanie bieżącego nadzoru nad operatorami systemów bezzałogowych statków powietrznych", "TYTDZPRZEDMprzedmiotregulacjitytuulubdziau"),
                ("Rodzaj czynności urzędowej", "ROZDZODDZOZNoznaczenierozdziauluboddziau"),
                ("Wysokość stawki w zł", "ROZDZODDZOZNoznaczenierozdziauluboddziau"),
                ("1", "ROZDZODDZOZNoznaczenierozdziauluboddziau"),
                ("Art. 4. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.", "ARTartustawynprozporzdzenia")));

            Assert.Contains("Rodzaj czynności urzędowej", description);
            Assert.Contains("Wysokość stawki w zł", description);
            Assert.Contains("art 4 ", description);
        }

        [Fact]
        public void ChapterInAmendmentStyle_WithoutAmendmentCommand_KeepsItsHeading()
        {
            // TJ 300755_2688666: „Rozdział 5a" w stylu nowelizacji bez komendy nowelizacyjnej przed nim —
            // rozdział samego aktu; jego tytuł w zwykłym stylu tytułu jednostki nie ginie.
            var document = ParseStyled(
                ("Art. 41. Obsługę administracyjnobiurową Rady zapewnia Kancelaria Prezesa Rady Ministrów.", "ARTartustawynprozporzdzenia"),
                ("Rozdział 5a", "ZROZDZODDZOZNzmoznrozdzoddzartykuempunktem"),
                ("Rada Dialogu z Młodym Pokoleniem", "ROZDZODDZPRZEDMprzedmiotregulacjirozdziauluboddziau"),
                ("Art. 42. 1. Rada Dialogu z Młodym Pokoleniem jest organem opiniodawczo-doradczym.", "ARTartustawynprozporzdzenia"));

            var chapter = Assert.IsType<Saga.Model.SystematizingUnits.Chapter>(document.Articles.Single(a => a.Number?.Value == "42").Parent!.Parent);
            Assert.Equal("rozdz_5a", chapter.Id);
            Assert.Equal("Rada Dialogu z Młodym Pokoleniem", chapter.Heading);
            Assert.All(document.Articles.Single(a => a.Number?.Value == "41").Paragraphs, p => Assert.Null(p.Amendment));
        }

        [Fact]
        public void BookTitleInPartOrBookStyle_IsAssigned_EvenWhenLong()
        {
            // KPC (TJ 302049_2704309): tytuł księgi w tym samym stylu CZĘŚĆ(KSIĘGA) co jej oznaczenie,
            // 157 znaków — heurystyka tytułu bez stylu by go odrzuciła.
            const string heading = "Uznanie i wykonanie niektórych orzeczeń sądów państw członkowskich Unii " +
                "Europejskiej oraz pochodzących z tych państw ugód, dokumentów urzędowych i porozumień";

            var document = ParseStyled(
                ("KSIĘGA CZWARTA", "CZKSIGAoznaczenieiprzedmiotczcilubksigi"),
                (heading, "CZKSIGAoznaczenieiprzedmiotczcilubksigi"),
                ("Art. 1153[1]. Treść.", "ARTartustawynprozporzdzenia"));

            Assert.Equal(heading, document.RootPart.Books[0].Heading);
        }

        [Fact]
        public void BookDesignationRightAfterPart_IsABook_NotThePartTitle()
        {
            var document = ParseStyled(
                ("CZĘŚĆ PIERWSZA", "CZKSIGAoznaczenieiprzedmiotczcilubksigi"),
                ("KSIĘGA PIERWSZA", "CZKSIGAoznaczenieiprzedmiotczcilubksigi"),
                ("PROCES", "CZKSIGAoznaczenieiprzedmiotczcilubksigi"),
                ("Art. 1. Treść.", "ARTartustawynprozporzdzenia"));

            Assert.Equal(string.Empty, document.RootPart.Heading);
            Assert.False(document.RootPart.Books[0].IsImplicit);
            Assert.Equal("PROCES", document.RootPart.Books[0].Heading);
        }

        [Theory]
        // KPC (TJ 302049_2704309) i prawo bankowe (TJ 300754_2723313): po tytule jednostki stoi
        // nienumerowany podtytuł w tym samym stylu — osobny nagłówek, nie ciąg dalszy tytułu.
        [InlineData("DZIAŁ I", "TYTDZOZNoznaczenietytuulubdziau", "Właściwość sądu", "Przepis wstępny", "TYTDZPRZEDMprzedmiotregulacjitytuulubdziau")]
        [InlineData("Rozdział 12", "ROZDZODDZOZNoznaczenierozdziauluboddziau", "Plany naprawy oraz wczesna interwencja, likwidacja i upadłość banku", "A. Plany naprawy", "ROZDZODDZPRZEDMprzedmiotregulacjirozdziauluboddziau")]
        public void SubheadingStartingWithCapital_IsNotJoinedToTheTitle(
            string designation, string designationStyle, string title, string subheading, string headingStyle)
        {
            var document = ParseStyled(
                (designation, designationStyle),
                (title, headingStyle),
                (subheading, headingStyle),
                ("Art. 1. Treść.", "ARTartustawynprozporzdzenia"));

            var unit = (Saga.Model.SystematizingUnits.ISystematizingUnit)ExplicitUnit(document);
            Assert.Equal(title, unit.Heading);
        }

        private static BaseEntity ExplicitUnit(LegalDocument document)
        {
            BaseEntity unit = document.Articles.Single().Parent!;
            while (((Saga.Model.SystematizingUnits.ISystematizingUnit)unit).IsImplicit)
                unit = unit.Parent!;
            return unit;
        }

        [Fact]
        public void StrayHeadingStyledBlockLater_IsNotAppendedToAnEarlierTitle()
        {
            var document = ParseStyled(
                ("Rozdział 1", "ROZDZODDZOZNoznaczenierozdziauluboddziau"),
                ("Przepisy ogólne", "ROZDZODDZPRZEDMprzedmiotregulacjirozdziauluboddziau"),
                ("Art. 1. Treść.", "ARTartustawynprozporzdzenia"),
                ("Zasady", "ROZDZODDZPRZEDMprzedmiotregulacjirozdziauluboddziau"),
                ("Art. 2. Treść.", "ARTartustawynprozporzdzenia"));

            Assert.Equal("Przepisy ogólne",
                document.RootPart.Books[0].Titles[0].Divisions[0].Chapters[0].Heading);
        }

        [Fact]
        public void ToleratedDesignation_LeavesAnInfoMessageOnTheUnit()
        {
            // Kodeks karny (TJ 318682_2849299): rozdział rzymski wbrew § 62 ust. 1 ZTP.
            var document = ParseStyled(
                ("Rozdział IV", "ROZDZODDZOZNoznaczenierozdziauluboddziau"),
                ("Kary", "ROZDZODDZPRZEDMprzedmiotregulacjirozdziauluboddziau"),
                ("Art. 32. Karami są: grzywna, ograniczenie wolności, pozbawienie wolności.", "ARTartustawynprozporzdzenia"));

            var chapter = ExplicitUnit(document);
            Assert.Equal("rozdz_4", chapter.Id);
            Assert.Contains(chapter.ValidationMessages,
                m => m.Level == ValidationLevel.Info && m.Message.Contains("cyframi rzymskimi"));
        }

        [Theory]
        // Wersalikowy sufiks dopuszczony tylko dla tytułu i działu — „Część A" i „Część IA" to oznaczenia
        // części załącznika, nie jednostki aktu.
        [InlineData("Część A")]
        [InlineData("Część IA")]
        [InlineData("KSIĘGA IA")]
        public void UppercaseSuffixOnPartOrBook_IsNotAUnit(string text)
        {
            var result = new ParagraphClassifier().Classify(new ClassificationInput(text, null));

            Assert.False(IsSystematizing(result.Kind), $"'{text}' nie powinno być jednostką, jest {result.Kind}");
        }

        private static LegalDocument ParseStyled(params (string Text, string Style)[] lines) =>
            LegalDocumentParser.ParseBlocks(lines
                .Select(line => new Ingest.DocumentBlock { Text = line.Text, StyleId = line.Style }).ToList());

        [Fact]
        public void PartAfterArticle_BecomesASiblingPart_ArticleNotReparented()
        {
            // Strażnik EnterPart: artykuł sprzed pierwszej CZĘŚCI zostaje w części niejawnej, a CZĘŚĆ I
            // staje obok niej jako kolejna część aktu.
            var orch = new ParserOrchestrator();
            var ctx = ConnectedContext();
            Feed(orch, ctx, "Art. 1. Przed czescia.", "CZĘŚĆ I", "Art. 2. W czesci.");

            var placed = ctx.Document.Articles.Select(a => $"[{UnitPath(a)}] {a.Id}").ToList();
            Assert.Equal(new[] { "[] art_1", "[czesc_1] art_2" }, placed);
            Assert.True(ctx.Document.RootPart.IsImplicit);
            Assert.Equal(2, ctx.Document.Parts.Count);
        }

        [Fact]
        public void TitlelessChapter_DoesNotAbsorbSentenceAsHeading()
        {
            // Zdanie treści (kończy się kropką) po nagłówku bez tytułu NIE może zostać tytułem jednostki.
            var orch = new ParserOrchestrator();
            var ctx = ConnectedContext();
            Feed(orch, ctx,
                "Rozdział 1",
                "Do postepowan wszczetych przed dniem wejscia w zycie stosuje sie przepisy dotychczasowe.");

            var chapter = ctx.Document.RootPart.Books[0].Titles[0].Divisions[0].Chapters[0];
            Assert.Equal(string.Empty, chapter.Heading);
        }

        [Theory]
        [InlineData("(uchylony)")]
        // Korpus: 317037_2830281 — adnotacja z zakresem uchylonej jednostki stoi w miejscu jej tytułu.
        [InlineData("(zawierający art. 19–21 – uchylony)")]
        [InlineData("(zawierający art. 392–393[20] – uchylony)")]
        [InlineData("(pominięty)")]
        public void RepealNoteAfterTheDesignation_StandsInPlaceOfTheTitle(string note)
        {
            var orch = new ParserOrchestrator();
            var ctx = ConnectedContext();
            Feed(orch, ctx, "Rozdział 1", "Przepisy ogolne", "Art. 1. Przepis.", "Rozdział 2", note, "Rozdział 3", "Przepisy koncowe", "Art. 22. Przepis.");

            var chapters = ctx.Document.RootPart.Books[0].Titles[0].Divisions[0].Chapters;
            Assert.Equal(new[] { "Przepisy ogolne", note, "Przepisy koncowe" }, chapters.Select(chapter => chapter.Heading));

            // Adnotacja nie przykleja się do ostatniego przepisu poprzedniego rozdziału.
            Assert.All(ctx.Document.Articles, article => Assert.Empty(article.Paragraphs.Single().Continuation));
        }

        [Fact]
        public void ParenthesisedTextThatIsNotARepealNote_IsNotTakenForOne()
        {
            // Nawias z innym tekstem po oznaczeniu nie jest adnotacją o uchyleniu — przechodzi zwykłą drogą tytułu.
            var orch = new ParserOrchestrator();
            var ctx = ConnectedContext();
            Feed(orch, ctx, "Rozdział 1", "(tekst jednolity)", "Art. 1. Przepis.");

            var chapter = ctx.Document.RootPart.Books[0].Titles[0].Divisions[0].Chapters[0];
            Assert.Equal(string.Empty, chapter.Heading);
        }

        [Fact]
        public void LongUnstyledTitle_IsStillTheTitleOfTheChapter()
        {
            // Korpus: 314725_2944776 — tytuł rozdziału rozporządzenia ma ponad 200 znaków. O tytule rozstrzyga
            // miejsce i budowa (wielka litera, bez znaku końca zdania), nie długość: próg długości był dopasowany
            // do korpusu i gubił takie tytuły.
            const string title =
                "Szczegółowe warunki uznawania kwalifikacji i uprawnień w celu uzyskania licencji pilota, dyspozytora " +
                "lotniczego, nawigatora lotniczego, mechanika lotniczego obsługi technicznej oraz mechanika pokładowego, " +
                "a także świadectwa kwalifikacji członka personelu lotniczego";
            Assert.True(title.Length > 200);

            var orch = new ParserOrchestrator();
            var ctx = ConnectedContext();
            Feed(orch, ctx, "Rozdział 2", title, "Art. 5. Przepis rozdzialu.");

            var chapter = ctx.Document.RootPart.Books[0].Titles[0].Divisions[0].Chapters.Last();
            Assert.Equal(title, chapter.Heading);
            Assert.Equal("art_5", ctx.Document.Articles.Single().Id);
        }

        [Theory]
        // Wprowadzenie do wyliczenia i urwany człon zdania to treść przepisu, nie tytuł.
        [InlineData("Ilekroc w ustawie jest mowa o:")]
        [InlineData("W sprawach nieuregulowanych stosuje sie przepisy odrebne,")]
        [InlineData("Przepisy stosuje sie do postepowan wszczetych;")]
        public void TitlelessChapter_DoesNotAbsorbUnfinishedContentAsHeading(string line)
        {
            var orch = new ParserOrchestrator();
            var ctx = ConnectedContext();
            Feed(orch, ctx, "Rozdział 1", line);

            var chapter = ctx.Document.RootPart.Books[0].Titles[0].Divisions[0].Chapters[0];
            Assert.Equal(string.Empty, chapter.Heading);
        }

        private static bool IsSystematizing(ParagraphKind kind) => kind is
            ParagraphKind.PartUnit or ParagraphKind.BookUnit or ParagraphKind.TitleUnit or
            ParagraphKind.DivisionUnit or ParagraphKind.ChapterUnit or ParagraphKind.SubchapterUnit;

        // ============================================================
        // Audyt kompletności ParagraphKind
        // ============================================================

        [Fact]
        public void ParagraphKind_AllValuesAccountedFor()
        {
            // Kanarek: dodanie wartości ParagraphKind wymusza rewizję obsługi w StructureProcessor.Process
            // (jednostki redakcyjne, systematyzacyjne, UnitHeading, Unknown) i tej listy.
            var expected = new HashSet<ParagraphKind>
            {
                ParagraphKind.Article, ParagraphKind.Paragraph, ParagraphKind.Point, ParagraphKind.Letter,
                ParagraphKind.Tiret, ParagraphKind.WrapUp,
                ParagraphKind.PartUnit, ParagraphKind.BookUnit, ParagraphKind.TitleUnit,
                ParagraphKind.DivisionUnit, ParagraphKind.ChapterUnit, ParagraphKind.SubchapterUnit,
                ParagraphKind.UnitHeading, ParagraphKind.Unknown,
            };

            Assert.Equal(expected, Enum.GetValues<ParagraphKind>().ToHashSet());
        }
    }
}
