using Saga.Core.Services.UnitReferences;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Wspólne źródło powołań jednostek (ADR-0015): słownik i kształt oznaczeń (tokenizer), reguły ścieżki
    /// z rozstrzygnięciem „§" (kursor, <see cref="SectionSignRole"/>) oraz polityka tekstu komendy
    /// (<see cref="UnitAddressReader"/>). Z tych reguł korzystają wejście w życie i cele nowelizacji.
    /// </summary>
    public class UnitReferencesTests
    {
        // ============================================================
        // Tokenizer — słownik
        // ============================================================

        private static UnitTokenKind[] Kinds(string text) =>
            UnitReferenceTokenizer.Tokenize(text).Select(t => t.Kind).ToArray();

        [Fact]
        public void Preposition_BeforeUnitKeyword_IsProse()
        {
            // § 89 ust. 2 ZTP: „w" przed każdą jednostką — to nie litera „w".
            Assert.Equal(
                new[]
                {
                    UnitTokenKind.Prose, UnitTokenKind.UnitKeyword, UnitTokenKind.Designation,
                    UnitTokenKind.Prose, UnitTokenKind.UnitKeyword, UnitTokenKind.Designation,
                },
                Kinds("w art. 5 w ust. 2"));
        }

        [Fact]
        public void LetterW_RightAfterLitKeyword_StaysADesignation()
        {
            var tokens = UnitReferenceTokenizer.Tokenize("lit. w tiret pierwsze");

            Assert.Equal(UnitTokenKind.Designation, tokens[1].Kind);
            Assert.Equal("w", tokens[1].Text);
        }

        [Fact]
        public void LetterI_RightAfterLitKeyword_IsALetterNotAConjunction()
        {
            var tokens = UnitReferenceTokenizer.Tokenize("lit. i oraz pkt 10");

            Assert.Equal(UnitTokenKind.Designation, tokens[1].Kind);
            Assert.Equal("i", tokens[1].Text);
        }

        [Theory]
        [InlineData("art.", "Article")]
        [InlineData("§", "Article")]
        [InlineData("ust.", "Paragraph")]
        [InlineData("pkt", "Point")]
        [InlineData("lit.", "Letter")]
        [InlineData("podwójne tiret", "Tiret")]
        [InlineData("zdaniu", "Sentence")]
        public void LevelOf_KeywordWithoutPathContext(string keyword, string expectedLevel)
        {
            // „§" bez kontekstu to jednostka główna — paragrafem kodeksu robi go dopiero kursor.
            Assert.Equal(Enum.Parse<UnitLevel>(expectedLevel), UnitReferenceTokenizer.LevelOf(keyword));
        }

        // ============================================================
        // Kursor — kształt oznaczeń i „§"
        // ============================================================

        [Theory]
        [InlineData("Article", "5", true)]
        [InlineData("Article", "125[1]", true)]
        [InlineData("Article", "o", false)]
        [InlineData("Paragraph", "2a", true)]
        [InlineData("Point", "w", false)]
        [InlineData("Letter", "zp", true)]
        [InlineData("Letter", "A", false)]
        [InlineData("Letter", "5", false)]
        [InlineData("Tiret", "pierwsze", true)] // tiret i zdanie sprawdza liczebnik porządkowy, nie kształt
        public void FitsLevel_FollowsZtpDesignationShape(string level, string designation, bool fits)
        {
            // § 57 ust. 1–4 ZTP: artykuł, ustęp, punkt — cyfry arabskie; litera — mała litera łacińska.
            Assert.Equal(fits, UnitPathCursor.FitsLevel(Enum.Parse<UnitLevel>(level), designation));
        }

        [Fact]
        public void SectionSignWithoutArticle_InStatute_IsUnresolvable()
        {
            var cursor = new UnitPathCursor();
            cursor.Announce(UnitReferenceTokenizer.Tokenize("§").Single());

            var step = cursor.Step(cursor.BeginStep()!.Value, "2");

            Assert.False(step.IsApplied);
        }

        [Fact]
        public void SectionSignWithoutArticle_InSectionBasedAct_IsTheMainUnit()
        {
            var cursor = new UnitPathCursor { SectionSignIsBasicUnit = true };
            cursor.Announce(UnitReferenceTokenizer.Tokenize("§").Single());

            Assert.True(cursor.Step(cursor.BeginStep()!.Value, "5").IsApplied);
            Assert.Equal("art_5", cursor.Path.ToString());
        }

        [Fact]
        public void SectionSignAfterArticle_IsACodeParagraph_EvenInSectionBasedAct()
        {
            // „art. 5 § 2 Kodeksu" w rozporządzeniu — po artykule „§" to zawsze paragraf kodeksu.
            var cursor = new UnitPathCursor { SectionSignIsBasicUnit = true };
            foreach (var token in UnitReferenceTokenizer.Tokenize("art. 5 § 2"))
            {
                if (token.Kind == UnitTokenKind.UnitKeyword)
                    cursor.Announce(token);
                else
                    cursor.Step(cursor.BeginStep()!.Value, token.Text);
            }

            Assert.Equal("art_5__par_2", cursor.Path.ToString());
        }

        [Fact]
        public void SectionSignAfterSection_InSectionBasedAct_IsTheNextMainUnit()
        {
            // „§ 6 i § 7" w rozporządzeniu: drugi „§" stoi obok pierwszego, nie pod nim.
            var cursor = new UnitPathCursor { SectionSignIsBasicUnit = true };
            foreach (var token in UnitReferenceTokenizer.Tokenize("§ 6 § 7"))
            {
                if (token.Kind == UnitTokenKind.UnitKeyword)
                    cursor.Announce(token);
                else
                    cursor.Step(cursor.BeginStep()!.Value, token.Text);
            }

            Assert.Equal("art_7", cursor.Path.ToString());
        }

        [Fact]
        public void StartingAt_InheritedSection_InSectionBasedAct_KeepsSectionSignTheMainUnit()
        {
            // „W § 5 wprowadza się następujące zmiany:" — odziedziczona jednostka główna jest paragrafem.
            var cursor = UnitPathCursor.StartingAt(new StructuralReference { Article = "5" }, sectionSignIsBasicUnit: true);
            cursor.Announce(UnitReferenceTokenizer.Tokenize("§").Single());

            cursor.Step(cursor.BeginStep()!.Value, "5a");

            Assert.Equal("art_5a", cursor.Path.ToString());
        }

        [Fact]
        public void StartingAt_InheritedTiret_LetsDoubleTiretDescend()
        {
            // „podwójne tiret drugie" pod odziedziczonym tiretem pierwszym — ścieżka tiretu z kontekstu.
            var cursor = UnitPathCursor.StartingAt(
                new StructuralReference { Article = "5", Point = "3", Letter = "a", Tiret = "1" });
            cursor.Announce(UnitReferenceTokenizer.Tokenize("podwójne tiret").Single());

            Assert.True(cursor.Step(cursor.BeginStep()!.Value, "drugie").IsApplied);
            Assert.Equal("art_5__pkt_3__lit_a__tir_1__dwtir_2", cursor.Path.ToString());
        }

        [Fact]
        public void StartingAt_BareDesignation_DoesNotInheritTheContextLevel()
        {
            // Kontekst niesie ścieżkę, nie poziom: bez słowa kluczowego nie ma czego rozwinąć.
            var cursor = UnitPathCursor.StartingAt(new StructuralReference { Article = "5", Point = "3" });

            Assert.Null(cursor.BeginStep());
        }

        [Fact]
        public void StartingAt_InheritedArticle_MakesSectionSignACodeParagraph()
        {
            var cursor = UnitPathCursor.StartingAt(new StructuralReference { Article = "12" });
            cursor.Announce(UnitReferenceTokenizer.Tokenize("§").Single());

            cursor.Step(cursor.BeginStep()!.Value, "2b");

            Assert.Equal("art_12__par_2b", cursor.Path.ToString());
        }

        // ============================================================
        // Czytnik adresu komendy
        // ============================================================

        /// <summary>Adresy wskazane w tekście; przedział jako „początek–koniec".</summary>
        private static string[] Read(string? text, StructuralReference? context = null) =>
            UnitAddressReader.Read(text, context)
                .Select(address => address.End is { } end ? $"{address.Start}–{end}" : address.Start.ToString())
                .ToArray();

        [Theory]
        [InlineData(null)]
        [InlineData("   ")]
        [InlineData("Przepis stosuje się odpowiednio.")]
        public void Reader_TextWithoutUnits_ReadsNothing(string? text)
        {
            Assert.Empty(Read(text, new StructuralReference { Article = "5" }));
        }

        [Theory]
        // Korpus ustaw zmieniających (akn4pl DU/2026/252, DU/2026/26 i in.).
        [InlineData("po rozdziale 9 dodaje się rozdział 9a w brzmieniu:", "rozdz_9")]
        [InlineData("po rozdziale 10 dodaje się rozdziały 10a–10c w brzmieniu:", "rozdz_10")]
        [InlineData("w dziale II po rozdziale 9 dodaje się rozdział 9a w brzmieniu:", "dzial_2__rozdz_9")]
        [InlineData("po dziale VI dodaje się dział VIa w brzmieniu:", "dzial_6")]
        [InlineData("uchyla się rozdziały 4 i 5;", "rozdz_4", "rozdz_5")]
        [InlineData("w dziale V tytuł rozdziału 4 otrzymuje brzmienie:", "dzial_5__rozdz_4")]
        [InlineData("w tytule I w dziale I w tytule rozdziału 3 po wyrazie „sąd” dodaje się wyrazy", "tyt_1__dzial_1__rozdz_3")]
        // Jednostka redakcyjna w rozdziale — eId bez ścieżki systematyzacyjnej (ADR-0017).
        [InlineData("w rozdziale 3 po art. 5 dodaje się art. 5a w brzmieniu:", "art_5")]
        // „tytuł" jako nagłówek i odmiany spoza słownika („działalności") nie są jednostkami.
        [InlineData("po tytule ustawy dodaje się oznaczenie i tytuł rozdziału w brzmieniu:")]
        [InlineData("w art. 5 w ust. 2 wyrazy dotyczące działalności i części zastępuje się", "art_5__ust_2")]
        public void Reader_SystematizingUnits_InAmendmentCommands(string text, params string[] expected)
        {
            Assert.Equal(expected, Read(text));
        }

        [Fact]
        public void Reader_SystematizingContext_FromTheParentCommand_IsKept()
        {
            // „W dziale II wprowadza się następujące zmiany: 1) po rozdziale 9 …"
            var context = Read("w dziale II wprowadza się następujące zmiany:").Single();
            Assert.Equal("dzial_2", context);

            var targets = UnitAddressReader.Read("po rozdziale 9 dodaje się rozdział 9a w brzmieniu:",
                new StructuralReference { Systematizing = context });
            Assert.Equal("dzial_2__rozdz_9", targets.Single().Start.ToString());
        }

        [Fact]
        public void Reader_QuotedActTitle_DoesNotHideTheAddressAfterIt()
        {
            // Korpus: cudzysłów w tytule ustawy to nie cytat komendy — adres stoi za nim.
            var targets = Read(
                "W ustawie z dnia 4 listopada 2016 r. o wsparciu kobiet w ciąży i rodzin „Za życiem” " +
                "(Dz. U. z 2020 r. poz. 1329) w art. 8 ust. 6 otrzymuje brzmienie:");

            Assert.Equal(new[] { "art_8__ust_6" }, targets);
        }

        [Fact]
        public void Reader_UnitsInsideQuotes_AreNotTargets()
        {
            // Korpus: poziomy z cytowanych wyrazów nie wchodzą do adresu.
            var targets = Read(
                "w art. 6 w ust. 1 wyrazy „art. 4 ust. 1 pkt 1-2d” zastępuje się wyrazami „art. 4 ust. 1 pkt 1-2e”;");

            Assert.Equal(new[] { "art_6__ust_1" }, targets);
        }

        [Fact]
        public void Reader_UnclosedQuote_ReachesTheEndOfText()
        {
            Assert.Equal(new[] { "art_5" }, Read("art. 5 otrzymuje brzmienie: „Art. 5. Treść z odesłaniem do art. 7"));
        }

        [Fact]
        public void Reader_ProseBreaksTheRun_ActNumberIsNotADesignation()
        {
            // „rozporządzenia 2016/679" po artykule to proza — „2016" nie jest kolejnym artykułem,
            // więc nie domyka celu przed właściwym „pkt 3".
            Assert.Equal(new[] { "art_5__pkt_3" }, Read("w art. 5 rozporządzenia 2016/679 dodaje się pkt 3"));
        }

        [Fact]
        public void Reader_DigitsBeforeTheAddress_AreIgnored_EvenWithContext()
        {
            // Z kontekstem (artykuł z komendy nadrzędnej) gołe „4" z daty dziedziczyłoby poziom artykułu.
            var targets = Read(
                "W ustawie z dnia 4 listopada 2016 r. o wsparciu kobiet w ciąży i rodzin „Za życiem” w art. 8 ust. 6 " +
                "otrzymuje brzmienie:",
                new StructuralReference { Article = "1" });

            Assert.Equal(new[] { "art_8__ust_6" }, targets);
        }

        [Fact]
        public void Reader_DeeperUnitsInsideQuotes_DoNotRefineTheTarget()
        {
            // Korpus: cytowane „pkt 11 i 12" pod art. 27 dawało dotąd cel art_27__pkt_11. Poza cytatem tekst
            // nie wskazuje żadnej jednostki, więc czytnik nic nie zwraca — cel zostaje artykułem z kontekstu.
            Assert.Empty(Read("wyrazy „pkt 11 i 12” zastępuje się wyrazami „pkt 11-12”;", new StructuralReference { Article = "27" }));
        }

        [Fact]
        public void EntryIntoForce_MisshapenDesignation_IsNotAUnit()
        {
            // Kształt oznaczenia (§ 57 ZTP): „o" po przecinku nie jest ustępem „o".
            var paths = Saga.Core.Services.EntryIntoForce.UnitEnumerationParser.Parse("art. 5 ust. 2, o którym mowa")
                .Targets.Select(t => t.Structure.ToString()).ToArray();

            Assert.Equal(new[] { "art_5__ust_2" }, paths);
        }

        [Fact]
        public void Reader_SiblingJoinedByProse_IsTheObjectOfTheCommand()
        {
            // „po art. 5 dodaje się art. 5a" — kotwicą jest art. 5, dodawany art. 5a jest przedmiotem komendy.
            Assert.Equal(new[] { "art_5" }, Read("po art. 5 dodaje się art. 5a w brzmieniu:"));
        }

        [Fact]
        public void Reader_KeepsSuperscriptDesignations()
        {
            // Korpus: indeks górny należy do oznaczenia (art. 328¹¹) — w odczycie i w eId (ADR-0019).
            var address = UnitAddressReader.Read("w art. 328[11 ]dodaje się § 3 w brzmieniu:", null).Single();

            Assert.Equal("328[11]", address.Start.Article);
            Assert.Equal("art_328[11]__par_3", address.Start.ToString());
        }

        // ============================================================
        // Wyliczenie i przedział celów (ADR-0016)
        // ============================================================

        [Fact]
        public void Reader_Enumeration_WithRangesAndPrepositions_GivesEveryTarget()
        {
            // Korpus: 310239_2768075 (art. 82). Każda jednostka wyliczenia to osobny cel, przedział „6-9" — jeden;
            // przyimek „w" przed kolejną jednostką (§ 89 ust. 2 ZTP) wyliczenia nie przerywa.
            var targets = Read(
                "w ust. 5 w pkt 1 i 7, w ust. 6-9 i 11 oraz w ust. 12 w pkt 2 wyrazy „art. 75 ust. 1 pkt 2” " +
                "zastępuje się wyrazami „art. 75 ust. 1 pkt 1-5”,",
                new StructuralReference { Article = "82" });

            Assert.Equal(
                new[]
                {
                    "art_82__ust_5__pkt_1", "art_82__ust_5__pkt_7", "art_82__ust_6–art_82__ust_9",
                    "art_82__ust_11", "art_82__ust_12__pkt_2",
                },
                targets);
        }

        [Fact]
        public void Reader_EnumerationAfterTheVerb_IncludesARange()
        {
            // Korpus: 318581_2836177 (art. 23).
            Assert.Equal(
                new[] { "art_23__ust_4a–art_23__ust_4c", "art_23__ust_6", "art_23__ust_8", "art_23__ust_9" },
                Read("uchyla się ust. 4a-4c, 6, 8 i 9,", new StructuralReference { Article = "23" }));
        }

        [Fact]
        public void Reader_ProseQualifierBetweenItems_DoesNotEndTheEnumeration()
        {
            // Korpus: 278228_2482477 — komenda złożona (§ 88 ZTP); „we wprowadzeniu do wyliczenia" dookreśla
            // poprzednią jednostkę, a kolejną dołącza przecinek.
            var targets = Read(
                "użyte w art. 1 w ust. 5 we wprowadzeniu do wyliczenia, w art. 4 w ust. 1 i 8, w art. 11 w ust. 6, " +
                "w art. 14 w ust. 8, w art. 16 w ust. 1 w pkt 1, w art. 18 w ust. 7, w art. 19 w ust. 3, w art. 28 w ust. 3, " +
                "w art. 33 w ust. 2 we wprowadzeniu do wyliczenia i w art. 45, w różnym przypadku, wyrazy „minister " +
                "właściwy do spraw rozwoju regionalnego” zastępuje się użytymi w odpowiednim przypadku wyrazami " +
                "„minister właściwy do spraw szkolnictwa wyższego i nauki”;");

            Assert.Equal(
                new[]
                {
                    "art_1__ust_5", "art_4__ust_1", "art_4__ust_8", "art_11__ust_6", "art_14__ust_8",
                    "art_16__ust_1__pkt_1", "art_18__ust_7", "art_19__ust_3", "art_28__ust_3", "art_33__ust_2", "art_45",
                },
                targets);
        }

        [Fact]
        public void Reader_UnitAfterTheCommandVerb_IsNotAnotherTarget()
        {
            // Korpus: 315576_2810842 — „przecinek i pkt 7" to przedmiot „dodaje się", nie drugi cel.
            Assert.Equal(
                new[] { "art_44__ust_2__pkt_6" },
                Read("w ust. 2 po pkt 6 dodaje się przecinek i pkt 7 w brzmieniu:", new StructuralReference { Article = "44" }));
        }

        [Theory]
        [InlineData("w art. 22 w pkt 2, w art. 19a uchyla się ust. 3;", null, "art_22__pkt_2")] // korpus: 316893_2819277
        [InlineData("w pkt 28, art. 108g otrzymuje brzmienie:", "1", "art_1__pkt_28")]           // korpus: 339271_3007851
        public void Reader_TrailingCommaJoin_IsTheAmendedActsAddress_NotASecondTarget(
            string text, string? contextArticle, string expected)
        {
            // Nowelizacja nowelizacji: za przecinkiem stoi adres w zmienianym akcie nowelizującym. Wyliczenie
            // zamyka spójnik, więc jednostka dołączona na końcu samym przecinkiem nie jest kolejnym celem.
            var context = contextArticle is null ? null : new StructuralReference { Article = contextArticle };

            Assert.Equal(new[] { expected }, Read(text, context));
        }

        // ============================================================
        // Rozstrzygnięcie „§" dla dokumentu
        // ============================================================

        private static DocumentClassificationResult Classified(LegalActType type, LegalActType? dominance = null) => new()
        {
            ActType = type,
            Signals = dominance is null
                ? new List<DocumentSignal>()
                : new List<DocumentSignal> { new() { Kind = DocumentSignalKind.BaseUnitDominance, SupportsType = dominance } },
        };

        [Fact]
        public void SectionSignRole_FollowsTheActType()
        {
            Assert.True(SectionSignRole.IsBasicUnit(Classified(LegalActType.Regulation)));
            Assert.True(SectionSignRole.IsBasicUnit(Classified(LegalActType.Resolution)));
            Assert.False(SectionSignRole.IsBasicUnit(Classified(LegalActType.AmendingStatute)));
            Assert.False(SectionSignRole.IsBasicUnit(Classified(LegalActType.Statute)));
            Assert.False(SectionSignRole.IsBasicUnit(null));
        }

        [Fact]
        public void SectionSignRole_ForAnnouncement_FollowsTheAnnouncedAct()
        {
            // Obwieszczenie niesie rodzaj ogłaszanego aktu w sygnale przewagi jednostki podstawowej;
            // tekst jednolity kodeksu (≈60% wierszy „§") tego sygnału nie ma — i słusznie zostaje ustawą.
            Assert.True(SectionSignRole.IsBasicUnit(Classified(LegalActType.Announcement, LegalActType.Regulation)));
            Assert.False(SectionSignRole.IsBasicUnit(Classified(LegalActType.Announcement, LegalActType.Statute)));
            Assert.False(SectionSignRole.IsBasicUnit(Classified(LegalActType.Announcement)));
        }
    }
}
