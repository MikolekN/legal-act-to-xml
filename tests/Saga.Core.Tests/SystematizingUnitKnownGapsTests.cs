using Saga.Core.Ingest;
using Saga.Model;
using Saga.Model.EditorialUnits;
using Saga.Model.SystematizingUnits;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Znane luki w rozpoznawaniu jednostek systematyzacyjnych (część … oddział) — każda na
    /// dosłownym fragmencie z korpusu tekstów jednolitych (plik źródłowy w komentarzu testu).
    ///
    /// Test luki asercjami opisuje ZAMIAR (jak ma być), a nie stan faktyczny, i dopóki luka jest
    /// otwarta, ma [Fact(Skip = "…")] z powodem samowystarczalnym. Domknięcie luki = zdjęcie Skip,
    /// bez dotykania asercji. Postać eId wynika z przyjętych reguł: oznaczenie rzymskie i słowne
    /// daje w eId liczbę arabską, sufiks zostaje jak w tekście, indeks górny zostaje w notacji „[n]",
    /// a jednostka nazwana bez numeru dostaje tymczasowe eId „TMPn".
    ///
    /// Kontrprzykłady są aktywne zawsze — pominięty nie strzegłby niczego właśnie wtedy, gdy
    /// powstaje poprawka, która mogłaby go złamać.
    /// </summary>
    public class SystematizingUnitKnownGapsTests
    {
        // Style szablonu aktu prawnego, dokładnie jak w korpusie.
        private const string ChapterDesignation = "ROZDZODDZOZNoznaczenierozdziauluboddziau";
        private const string ChapterHeading = "ROZDZODDZPRZEDMprzedmiotregulacjirozdziauluboddziau";
        private const string TitleDesignation = "TYTDZOZNoznaczenietytuulubdziau";
        private const string TitleHeading = "TYTDZPRZEDMprzedmiotregulacjitytuulubdziau";
        private const string PartOrBook = "CZKSIGAoznaczenieiprzedmiotczcilubksigi";
        private const string ArticleStyle = "ARTartustawynprozporzdzenia";


        // ============================================================
        // Oznaczenia spoza ZTP
        // ============================================================

        [Theory]
        // Kodeks karny (TJ 318682_2849299) i Kodeks pracy (TJ 317037_2830281).
        [InlineData("Rozdział I", "Zasady odpowiedzialności karnej", 1, "", "rozdz_1")]
        [InlineData("Rozdział IIa", "Równe traktowanie w zatrudnieniu", 2, "a", "rozdz_2a")]
        public void RomanNumberedChapter_IsRecognized_WithArabicEId(
            string designation, string heading, int number, string suffix, string eId)
        {
            var document = Parse(
                (designation, ChapterDesignation),
                (heading, ChapterHeading),
                ("Art. 1. Treść.", ArticleStyle));

            var chapter = Assert.IsType<Chapter>(Assert.Single(ExplicitUnits(document)));
            Assert.Equal(number, chapter.Number?.NumericPart);
            Assert.Equal(suffix, chapter.Number?.LexicalPart);
            Assert.Equal(designation["Rozdział ".Length..], chapter.Number?.RawValue);
            Assert.Equal(eId, chapter.Id);
            Assert.Equal(heading, chapter.Heading);
        }

        [Theory]
        // Sufiks wersalikiem — TJ 303163_2716076 i 311010_2798032; dział z cyfrą arabską — TJ 339883_3015114;
        // słowo kluczowe małą literą w stylu oznaczenia — TJ 299144_2679815.
        [InlineData("Dział IIIA", "dzial_3A")]
        [InlineData("DZIAŁ IIA", "dzial_2A")]
        // Mała litera w liczbie rzymskiej — TJ 342926_3066402 („IvA" między „IV" a „IVB").
        [InlineData("DZIAŁ IvA", "dzial_4A")]
        [InlineData("DZIAŁ 1", "dzial_1")]
        [InlineData("dział II", "dzial_2")]
        public void ToleratedDivisionDesignation_WithStyle_IsRecognized(string designation, string eId)
        {
            var document = Parse(
                (designation, TitleDesignation),
                ("Zakaz stosowania niedozwolonych postanowień wzorców umów", TitleHeading),
                ("Art. 1. Treść.", ArticleStyle));

            var division = Assert.IsType<Division>(Assert.Single(ExplicitUnits(document)));
            Assert.Equal(eId, division.Id);
            Assert.Equal("Zakaz stosowania niedozwolonych postanowień wzorców umów", division.Heading);
        }

        [Fact]
        public void FootnoteMarkerAfterNumber_IsNotPartOfTheNumber()
        {
            // TJ 281631_2528006: znacznik odnośnika „5)" wklejony za numer rozdziału.
            var document = Parse(
                ("Rozdział 3[5)]", ChapterDesignation),
                ("Art. 1. Treść.", ArticleStyle));

            var chapter = Assert.IsType<Chapter>(Assert.Single(ExplicitUnits(document)));
            Assert.Equal("3", chapter.Number?.Value);
            Assert.Equal(string.Empty, chapter.Number?.Superscript);
            Assert.Equal("rozdz_3", chapter.Id);
        }

        [Theory]
        // Kodeks cywilny (TJ 291207_2617005) i Kodeks postępowania cywilnego (TJ 302049_2704309).
        [InlineData("TYTUŁ XVII[1]", TitleDesignation, "tyt_17[1]")]
        [InlineData("Rozdział 1[1]", ChapterDesignation, "rozdz_1[1]")]
        // TJ 308528_2781833: indeks górny i odnośnik zlane w jeden nawias.
        [InlineData("Oddział 2[1 15)]", ChapterDesignation, "oddz_2[1]")]
        public void SuperscriptInNumber_IsKeptInEIdAndOnTheNumber(string designation, string style, string eId)
        {
            var document = Parse(
                (designation, style),
                ("Art. 1. Treść.", ArticleStyle));

            var unit = Assert.Single(ExplicitUnits(document));
            Assert.Equal(eId, unit.Id);
            Assert.Equal("1", unit.Number?.Superscript);
        }

        // ============================================================
        // Nagłówek ze stylu szablonu
        // ============================================================

        [Fact]
        public void StyledHeadingLongerThan120Characters_IsAssigned()
        {
            // TJ 299137_2675687 — nagłówek ma 211 znaków.
            const string heading = "Uprawnienia funkcjonariuszy Służby Kontrwywiadu Wojskowego i Służby Wywiadu " +
                "Wojskowego oraz żołnierzy zawodowych wyznaczonych na stanowiska służbowe w Służbie " +
                "Kontrwywiadu Wojskowego i Służbie Wywiadu Wojskowego";

            var chapter = SingleChapter(("Rozdział 3", ChapterDesignation), (heading, ChapterHeading));

            Assert.Equal(heading, chapter.Heading);
        }

        [Fact]
        public void StyledHeadingEndingWithPeriod_IsAssigned()
        {
            // TJ 302300_2720255 — kropka kończy skrót „r.", nie zdanie.
            const string heading = "Emerytura dla ubezpieczonych urodzonych po dniu 31 grudnia 1948 r.";

            var chapter = SingleChapter(("Rozdział 1", ChapterDesignation), (heading, ChapterHeading));

            Assert.Equal(heading, chapter.Heading);
        }

        [Fact]
        public void StyledRepealedHeading_IsAssigned()
        {
            // TJ 300758_2692741 — rozdział uchylony, nagłówek w stylu przedmiotu regulacji.
            var chapter = SingleChapter(("Rozdział 5", ChapterDesignation), ("(uchylony)", ChapterHeading));

            Assert.Equal("(uchylony)", chapter.Heading);
        }

        [Fact]
        public void StyledHeadingSplitOverTwoBlocks_IsJoined()
        {
            // TJ 300758_2692741 — nagłówek złamany na dwa akapity w tym samym stylu.
            var chapter = SingleChapter(
                ("Rozdział 3", ChapterDesignation),
                ("Rozporządzanie uprawnieniami do emisji utrzymywanymi na rachunkach w rejestrze Unii", ChapterHeading),
                ("i przenoszenie tych uprawnień", ChapterHeading));

            Assert.Equal(
                "Rozporządzanie uprawnieniami do emisji utrzymywanymi na rachunkach w rejestrze Unii i przenoszenie tych uprawnień",
                chapter.Heading);
        }

        // ============================================================
        // Części
        // ============================================================

        [Fact]
        public void SecondPart_IsBuilt()
        {
            // Kodeks postępowania cywilnego (TJ 302049_2704309).
            var document = Parse(
                ("CZĘŚĆ PIERWSZA", PartOrBook),
                ("Postępowanie rozpoznawcze", PartOrBook),
                ("KSIĘGA PIERWSZA", PartOrBook),
                ("PROCES", PartOrBook),
                ("Art. 1. Treść.", ArticleStyle),
                ("CZĘŚĆ DRUGA", PartOrBook),
                ("Postępowanie zabezpieczające", PartOrBook),
                ("Art. 730. Treść.", ArticleStyle));

            var part = Ancestor<Part>(ArticleNumbered(document, "730"));
            Assert.False(part.IsImplicit);
            Assert.Equal(2, part.Number?.NumericPart);
            Assert.Equal("Postępowanie zabezpieczające", part.Heading);
        }

        [Fact]
        public void BooksWithTheSameNumberInDifferentParts_HaveDistinctEIds()
        {
            // Kodeks postępowania cywilnego (TJ 302049_2704309): część pierwsza i czwarta mają
            // własną „KSIĘGĘ PIERWSZĄ".
            var document = Parse(
                ("CZĘŚĆ PIERWSZA", PartOrBook),
                ("Postępowanie rozpoznawcze", PartOrBook),
                ("KSIĘGA PIERWSZA", PartOrBook),
                ("PROCES", PartOrBook),
                ("Art. 1. Treść.", ArticleStyle),
                ("CZĘŚĆ CZWARTA", PartOrBook),
                ("Przepisy z zakresu międzynarodowego postępowania cywilnego", PartOrBook),
                ("KSIĘGA PIERWSZA", PartOrBook),
                ("JURYSDYKCJA KRAJOWA", PartOrBook),
                ("Art. 1097. Treść.", ArticleStyle));

            var books = ExplicitUnits(document).OfType<Book>().ToList();
            Assert.Equal(2, books.Count);
            Assert.NotEqual(books[0].Id, books[1].Id);
            Assert.Equal(4, Ancestor<Part>(books[1]).Number?.NumericPart);
        }

        [Fact]
        public void NamedParts_AreRecognized_WithTemporaryEIds()
        {
            // Kodeks karny (TJ 318682_2849299): części bez numeru.
            var document = Parse(
                ("CZĘŚĆ OGÓLNA", PartOrBook),
                ("Art. 1. Treść.", ArticleStyle),
                ("CZĘŚĆ SZCZEGÓLNA", PartOrBook),
                ("Art. 117. Treść.", ArticleStyle));

            var parts = ExplicitUnits(document).OfType<Part>().ToList();
            Assert.Equal(new string?[] { "OGÓLNA", "SZCZEGÓLNA" }, parts.Select(p => p.Number?.RawValue));
            Assert.Equal(new[] { "czesc_TMP1", "czesc_TMP2" }, parts.Select(p => p.Id));
            Assert.Same(parts[1], Ancestor<Part>(ArticleNumbered(document, "117")));
        }

        [Fact]
        public void NamedTitle_IsRecognized_WithTemporaryEId()
        {
            // Kodeks postępowania cywilnego (TJ 302049_2704309).
            var document = Parse(
                ("TYTUŁ WSTĘPNY", TitleDesignation),
                ("Przepisy ogólne", TitleHeading),
                ("Art. 1. Treść.", ArticleStyle));

            var title = Assert.IsType<Title>(Assert.Single(ExplicitUnits(document)));
            Assert.Equal("WSTĘPNY", title.Number?.RawValue);
            Assert.Equal("tyt_TMP1", title.Id);
            Assert.Equal("Przepisy ogólne", title.Heading);
        }

        // ============================================================
        // Kontrprzykłady — aktywne
        // ============================================================

        [Fact]
        public void NamedPartRightAfterBook_IsTheBookHeading_NotAPart()
        {
            // Kodeks cywilny (TJ 291207_2617005): „Część ogólna" to tytuł księgi pierwszej — ten sam
            // styl co oznaczenie, ale stoi tam, gdzie księga czeka na tytuł.
            var document = Parse(
                ("Księga pierwsza", PartOrBook),
                ("Część ogólna", PartOrBook),
                ("Art. 1. Treść.", ArticleStyle));

            var book = Assert.IsType<Book>(Assert.Single(ExplicitUnits(document)));
            Assert.Equal("Część ogólna", book.Heading);
        }

        [Fact]
        public void NamedPartAsDivisionTitle_IsTheDivisionHeading_NotAPart()
        {
            // Kodeks karny skarbowy (TJ 322161_2895706): „Część ogólna" w stylu tytułu działu.
            var document = Parse(
                ("TYTUŁ I", TitleDesignation),
                ("Przestępstwa skarbowe", TitleHeading),
                ("DZIAŁ I", TitleDesignation),
                ("Część ogólna", TitleHeading),
                ("Rozdział 1", ChapterDesignation),
                ("Przepisy wstępne", ChapterHeading),
                ("Art. 1. Treść.", ArticleStyle));

            Assert.DoesNotContain(ExplicitUnits(document), unit => unit is Part);
            var division = Assert.Single(ExplicitUnits(document).OfType<Division>());
            Assert.Equal("Część ogólna", division.Heading);
            Assert.Equal("tyt_1__dzial_1__rozdz_1", ArticleNumbered(document, "1").Parent!.Parent!.Id);
        }

        [Fact]
        public void UnitInAmendmentStyle_WithoutAmendmentCommand_IsAUnitOfTheAct()
        {
            // TJ 300753_2682674: rozdział dodany nowelizacją złożony w tekście jednolitym stylami zmian.
            var document = Parse(
                ("Rozdział 4a", ChapterDesignation),
                ("Termomodernizacja", ChapterHeading),
                ("Art. 11a. Treść.", ArticleStyle),
                ("Rozdział 4b", "ZROZDZODDZOZNzmoznrozdzoddzartykuempunktem"),
                ("Premia MZG i grant MZG", "ZROZDZODDZPRZEDMzmprzedmrozdzoddzartykuempunktem"),
                ("Art. 11g. Treść.", ArticleStyle));

            var chapters = ExplicitUnits(document).OfType<Chapter>().ToList();
            Assert.Equal(new[] { "rozdz_4a", "rozdz_4b" }, chapters.Select(c => c.Id));
            Assert.Equal("Premia MZG i grant MZG", chapters[1].Heading);
            Assert.Equal("rozdz_4b", ArticleNumbered(document, "11g").Parent!.Parent!.Id);
        }

        [Fact]
        public void RepealedUnitMarker_IsTheHeading()
        {
            // TJ 275050_2461922: „(uchylony)" w stylu uchylenia zamiast tytułu rozdziału.
            var document = Parse(
                ("Rozdział 6", ChapterDesignation),
                ("(uchylony)", "UCHYLENIEWTJoznaczenieuchyleniawtekciejednolitym"),
                ("Rozdział 7", ChapterDesignation),
                ("Przepisy końcowe", ChapterHeading),
                ("Art. 30. Treść.", ArticleStyle));

            Assert.Equal(new[] { "(uchylony)", "Przepisy końcowe" },
                ExplicitUnits(document).Select(u => ((ISystematizingUnit)u).Heading));
        }

        // ============================================================
        // Adnotacja, która sama nazywa uchyloną jednostkę (ADR-0066)
        // ============================================================

        private const string RepealAnnotation = "UCHYLENIEWTJoznaczenieuchyleniawtekciejednolitym";

        [Fact]
        public void AnnotationNamingARepealedChapter_KeepsTheChapterInItsPlace()
        {
            // TJ 345462_3070068: po „Rozdział 1 / (uchylony)" stoi sama adnotacja — bez wiersza oznaczenia.
            var document = Parse(
                ("Rozdział 1", ChapterDesignation),
                ("(uchylony)", RepealAnnotation),
                ("(Rozdział 2 – uchylony)", ChapterDesignation),
                ("Art. 36. Uprawnionym pracownikom przysługuje prawo do nabycia akcji.", ArticleStyle),
                ("Rozdział 3", ChapterDesignation),
                ("Przepisy końcowe", ChapterHeading),
                ("Art. 40. Treść.", ArticleStyle));

            var chapters = ExplicitUnits(document).OfType<Chapter>().ToList();
            Assert.Equal(new[] { "rozdz_1", "rozdz_2", "rozdz_3" }, chapters.Select(chapter => chapter.Id));

            // Adnotacja jest nagłówkiem nazwanej jednostki — jej tekst nie ginie.
            Assert.Equal("(Rozdział 2 – uchylony)", chapters[1].Heading);
            Assert.Equal("rozdz_2", ArticleNumbered(document, "36").Parent!.Parent!.Id);
        }

        [Fact]
        public void AnnotationNamingARepealedBook_StandsBetweenThePartAndItsTitles()
        {
            // TJ 302049_2704309 (Kodeks postępowania cywilnego): oznaczenie i tytuł księgi uchylone, tytuły zostały.
            var document = Parse(
                ("CZĘŚĆ DRUGA", PartOrBook),
                ("Postępowanie zabezpieczające", PartOrBook),
                ("(oznaczenie oraz tytuł księgi pierwszej – uchylone)", RepealAnnotation),
                ("TYTUŁ I", TitleDesignation),
                ("Przepisy ogólne", TitleHeading),
                ("Art. 730. W każdej sprawie cywilnej można żądać udzielenia zabezpieczenia.", ArticleStyle));

            var units = ExplicitUnits(document).ToList();
            Assert.Equal(new[] { typeof(Part), typeof(Book), typeof(Title) }, units.Select(unit => unit.GetType()));
            Assert.Equal("(oznaczenie oraz tytuł księgi pierwszej – uchylone)", ((ISystematizingUnit)units[1]).Heading);
            Assert.Equal("Postępowanie zabezpieczające", ((ISystematizingUnit)units[0]).Heading);
            // Tytuł stoi pod księgą — tak jak w akcie, zanim uchylono jej oznaczenie i tytuł.
            Assert.Same(units[1], units[2].Parent);
            Assert.Same(units[0], units[1].Parent);
        }

        [Theory]
        // TJ 320891_2890572, 348341_3096796, 317037_2830281 — nazwa jednostki w dopełniaczu, różny szyk.
        [InlineData("(oznaczenie i tytuł oddziału 1 – uchylone)", typeof(Subchapter), "1")]
        [InlineData("(oznaczenie rozdziału 4 i tytuł – uchylone)", typeof(Chapter), "4")]
        [InlineData("(oznaczenie oraz tytuł rozdziału I – uchylone)", typeof(Chapter), "1")]
        [InlineData("(Rozdział 2a – uchylony)", typeof(Chapter), "2a")]
        public void AnnotationNamingARepealedUnit_IsThatUnit(string annotation, Type unitType, string number)
        {
            var document = Parse(
                ("Dział trzynasty", TitleDesignation),
                ("Odpowiedzialność za wykroczenia", TitleHeading),
                (annotation, RepealAnnotation),
                ("Art. 281. Kto, będąc pracodawcą, narusza przepisy, podlega karze grzywny.", ArticleStyle));

            var repealed = Assert.Single(ExplicitUnits(document), unit => unit.GetType() == unitType);
            Assert.Equal(number, repealed.Number?.Value);
            Assert.Equal(annotation, ((ISystematizingUnit)repealed).Heading);
        }

        [Theory]
        // Adnotacja o zawartości jednostki albo o czymś, co jednostką nie jest, jednostki nie nazywa.
        [InlineData("(zawierający rozdziały 4 i 5 – uchylony)")]
        [InlineData("(Przepis wstępny – uchylone)")]
        [InlineData("(zawierający art. 19–21 – uchylony)")]
        public void AnnotationThatNamesNoUnit_DoesNotMakeOne(string annotation)
        {
            var document = Parse(
                ("Dział trzynasty", TitleDesignation),
                ("Odpowiedzialność za wykroczenia", TitleHeading),
                (annotation, RepealAnnotation),
                ("Art. 281. Kto, będąc pracodawcą, narusza przepisy, podlega karze grzywny.", ArticleStyle));

            Assert.IsType<Division>(Assert.Single(ExplicitUnits(document)));
        }

        [Fact]
        public void AnnotationRightAfterTheDesignationOfTheSameUnit_StaysItsHeading()
        {
            var document = Parse(
                ("Rozdział 2", ChapterDesignation),
                ("(Rozdział 2 – uchylony)", RepealAnnotation),
                ("Art. 36. Treść.", ArticleStyle));

            var chapter = Assert.IsType<Chapter>(Assert.Single(ExplicitUnits(document)));
            Assert.Equal("(Rozdział 2 – uchylony)", chapter.Heading);
        }

        [Fact]
        public void ChaptersInTheActsAnnex_AreNotUnitsOfTheAct()
        {
            // TJ 317053_2823243: załącznik ustawy ma własne „Rozdział 1…3".
            var document = Parse(
                ("Rozdział 1", ChapterDesignation),
                ("Przepisy ogólne", ChapterHeading),
                ("Art. 1. Treść.", ArticleStyle),
                ("Art. 2. Ustawa wchodzi w życie po upływie 30 dni od dnia ogłoszenia.", ArticleStyle),
                ("Załącznik do ustawy z dnia 4 marca 2010 r.", "TEKSTZacznikido"),
                ("TEMATY DANYCH PRZESTRZENNYCH", null),
                ("Rozdział 1", ChapterDesignation),
                ("Pierwsza grupa tematyczna", ChapterHeading),
                ("1) systemy odniesienia;", null));

            var chapter = Assert.IsType<Chapter>(Assert.Single(ExplicitUnits(document)));
            Assert.Equal("Przepisy ogólne", chapter.Heading);
            Assert.Empty(ArticleNumbered(document, "2").Paragraphs.SelectMany(p => p.Points));
        }

        [Fact]
        public void AnnexOfTheAnnouncement_OpensTheConsolidatedText_AndIsNotAnAnnexOfTheAct()
        {
            var document = Parse(
                ("Załącznik do obwieszczenia Marszałka Sejmu Rzeczypospolitej Polskiej z dnia 1 marca 2025 r.", "TEKSTZacznikido"),
                ("Rozdział 1", ChapterDesignation),
                ("Art. 1. Treść.", ArticleStyle),
                ("Rozdział 2", ChapterDesignation),
                ("Art. 2. Treść.", ArticleStyle));

            Assert.Equal(new[] { "rozdz_1", "rozdz_2" }, ExplicitUnits(document).Select(u => u.Id));
        }

        [Theory]
        // Style oznaczeń nadużywane w tabelach i załącznikach: TJ 326995_2969330, 336442_2996033,
        // ustawa 315576_2810842.
        [InlineData("STATUT REGIONALNEJ DYREKCJI OCHRONY ŚRODOWISKA W POZNANIU", ChapterDesignation)]
        [InlineData("31", PartOrBook)]
        [InlineData("1", ChapterDesignation)]
        public void DesignationStyleWithoutDesignationText_IsNotAUnit(string text, string style)
        {
            var document = Parse((text, style), ("Art. 1. Treść.", ArticleStyle));

            Assert.Empty(ExplicitUnits(document));
        }

        [Fact]
        public void LowercaseKeywordWithoutStyle_IsNotAUnit()
        {
            // Bez stylu oznaczenia „dział II" małą literą to początek prozy, nie jednostka.
            var document = Parse(("dział II", null), ("Art. 1. Treść.", null));

            Assert.Empty(ExplicitUnits(document));
        }

        [Fact]
        public void DivisionTextInChapterStyle_IsBuiltAsDivision()
        {
            // TJ 336501_2993884: styl oznaczenia rozdziału, treść „Dział II" — treść wygrywa ze stylem.
            var document = Parse(("Dział II", ChapterDesignation), ("Art. 1. Treść.", ArticleStyle));

            Assert.IsType<Division>(Assert.Single(ExplicitUnits(document)));
        }

        [Fact]
        public void ChapterTextInHeadingStyle_IsBuiltAsChapter()
        {
            // TJ 301071_2687931: oznaczenie „Rozdział 6a" w stylu nagłówka — treść wygrywa ze stylem.
            var document = Parse(("Rozdział 6a", ChapterHeading), ("Art. 1. Treść.", ArticleStyle));

            var chapter = Assert.IsType<Chapter>(Assert.Single(ExplicitUnits(document)));
            Assert.Equal("6a", chapter.Number?.Value);
        }

        // ============================================================
        // Pomocnicze
        // ============================================================

        private static LegalDocument Parse(params (string Text, string? StyleId)[] lines) =>
            LegalDocumentParser.ParseBlocks(
                lines.Select(line => new DocumentBlock { Text = line.Text, StyleId = line.StyleId }).ToList());

        private static Chapter SingleChapter(params (string Text, string? StyleId)[] lines) =>
            Assert.IsType<Chapter>(Assert.Single(ExplicitUnits(Parse([.. lines, ("Art. 1. Treść.", ArticleStyle)]))));

        /// <summary>Jawne jednostki systematyzacyjne w kolejności dokumentu (od części do oddziału).</summary>
        private static List<BaseEntity> ExplicitUnits(LegalDocument document)
        {
            var units = new List<BaseEntity>();
            foreach (var part in document.Parts)
                Visit(part);
            return units;

            void Visit(BaseEntity unit)
            {
                if (!((ISystematizingUnit)unit).IsImplicit)
                    units.Add(unit);

                var children = SystematizingHierarchy.Children(unit).Where(child => child is ISystematizingUnit);
                foreach (var child in children)
                    Visit(child);
            }
        }

        private static Article ArticleNumbered(LegalDocument document, string number) =>
            document.Articles.Single(a => a.Number?.Value == number);

        private static T Ancestor<T>(BaseEntity entity) where T : BaseEntity
        {
            for (var current = entity.Parent; current != null; current = current.Parent)
            {
                if (current is T match)
                    return match;
            }
            throw new InvalidOperationException($"Brak przodka typu {typeof(T).Name} dla {entity.Id}.");
        }
    }
}
