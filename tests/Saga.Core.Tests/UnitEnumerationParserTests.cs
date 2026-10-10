using Saga.Core.Services.EntryIntoForce;
using Saga.Model.EntryIntoForce;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Rozbiór powołań jednostek z wyjątków wejścia w życie.
    /// Ciągi wejściowe są DOSŁOWNYMI fragmentami z korpusu aktów — nie upraszczaj ich przy edycji,
    /// bo to one definiują, co parser musi udźwignąć.
    /// </summary>
    public class UnitEnumerationParserTests
    {
        private static string[] Paths(string units) =>
            UnitEnumerationParser.Parse(units).Targets.Select(t => t.Structure.ToString()).ToArray();

        // ============================================================
        // Dziedziczenie kontekstu — sedno problemu
        // ============================================================

        [Fact]
        public void BareDesignation_RepeatsLastLevel_NotDescends()
        {
            // „art. 1 pkt 3 i 9" → gołe „9" to pkt 9 tego samego artykułu, nie litera punktu 3.
            Assert.Equal(new[] { "art_1__pkt_3", "art_1__pkt_9" }, Paths("art. 1 pkt 3 i 9"));
        }

        [Fact]
        public void ArticlePrefix_IsNotATargetOnItsOwn()
        {
            // „art. 1 pkt 3" — celem jest punkt, artykuł jest wyłącznie prefiksem.
            Assert.Equal(new[] { "art_1__pkt_3" }, Paths("art. 1 pkt 3"));
        }

        [Fact]
        public void BareArticle_IsATarget()
        {
            Assert.Equal(new[] { "art_28", "art_33" }, Paths("art. 28 i art. 33"));
        }

        [Fact]
        public void PointFollowedByDeeperPoint_KeepsBothWhenSiblingsDiffer()
        {
            // „pkt 5, 6 lit. a i b oraz pkt 7" → pkt 5 (cały), pkt 6 lit. a, pkt 6 lit. b, pkt 7.
            Assert.Equal(
                new[] { "art_13__pkt_5", "art_13__pkt_6__lit_a", "art_13__pkt_6__lit_b", "art_13__pkt_7" },
                Paths("art. 13 pkt 5, 6 lit. a i b oraz pkt 7"));
        }

        [Fact]
        public void MixedLevels_ResetSubordinateLevels()
        {
            Assert.Equal(
                new[] { "art_1__pkt_3", "art_1__pkt_9", "art_2__pkt_2__lit_a", "art_4", "art_6__ust_1", "art_6__ust_2" },
                Paths("art. 1 pkt 3 i 9, art. 2 pkt 2 lit. a, art. 4 oraz art. 6 ust. 1 i 2"));
        }

        [Fact]
        public void PointAndLetter_AcrossArticles()
        {
            Assert.Equal(new[] { "art_1__pkt_3", "art_1__pkt_16__lit_b" }, Paths("art. 1 pkt 3 i pkt 16 lit. b"));
        }

        [Fact]
        public void ParagraphEnumeration()
        {
            Assert.Equal(new[] { "art_3__ust_2", "art_3__ust_3" }, Paths("art. 3 ust. 2 i 3"));
        }

        // ============================================================
        // Przedziały
        // ============================================================

        [Fact]
        public void NumericRange_IsExpanded()
        {
            Assert.Equal(
                new[] { "art_1__pkt_4", "art_1__pkt_5", "art_1__pkt_6", "art_1__pkt_7" },
                Paths("art. 1 pkt 4–7"));
        }

        [Fact]
        public void ArticleRange_IsExpanded()
        {
            Assert.Equal(new[] { "art_4", "art_5", "art_6" }, Paths("art. 4–6"));
        }

        [Fact]
        public void LetterRange_IsExpanded()
        {
            Assert.Equal(
                new[] { "art_4__pkt_1__lit_b", "art_4__pkt_1__lit_c", "art_4__pkt_1__lit_d", "art_4__pkt_1__lit_e" },
                Paths("art. 4 pkt 1 lit. b-e"));
        }

        [Fact]
        public void SuffixedNumericRange_SharesNumericPart()
        {
            // Celem pozostaje sam punkt, ale sprawdzamy TO, od czego nazwa — rozwinięcie przedziału
            // o wspólnej części liczbowej wewnątrz zakresu.
            var target = Assert.Single(UnitEnumerationParser.Parse("art. 1 pkt 9 w zakresie art. 85c–85j").Targets);

            Assert.Equal("art_1__pkt_9", target.Structure.ToString());
            Assert.Equal(
                new[] { "art_85c", "art_85d", "art_85e", "art_85f", "art_85g", "art_85h", "art_85i", "art_85j" },
                target.ScopeStructures.Select(s => s.ToString()));
        }

        [Fact]
        public void MixedRangeAndSingles()
        {
            // „lit. a–c, e, f i h–j" — trzy przedziały i dwie pojedyncze litery.
            Assert.Equal(
                new[]
                {
                    "art_1__pkt_14__lit_a", "art_1__pkt_14__lit_b", "art_1__pkt_14__lit_c",
                    "art_1__pkt_14__lit_e", "art_1__pkt_14__lit_f",
                    "art_1__pkt_14__lit_h", "art_1__pkt_14__lit_i", "art_1__pkt_14__lit_j",
                },
                Paths("art. 1 pkt 14 lit. a–c, e, f i h–j"));
        }

        // ============================================================
        // Tirety
        // ============================================================

        [Fact]
        public void TiretOrdinals_AreNumbered()
        {
            Assert.Equal(
                new[] { "art_1__pkt_2__lit_a__tir_1", "art_1__pkt_2__lit_a__tir_2" },
                Paths("art. 1 pkt 2 lit. a tiret pierwsze i drugie"));
        }

        [Fact]
        public void NestedTiret_UsesDottedPath()
        {
            // „tiret pierwsze podwójne tiret drugie" → tir_1__dwtir_2. Każda głębokość ma własny
            // levelType; kropkowe „1.2" jest wyłącznie sposobem składowania w jednym polu.
            Assert.Equal(
                new[] { "art_1__pkt_13__lit_a__tir_1__dwtir_2" },
                Paths("art. 1 pkt 13 lit. a tiret pierwsze podwójne tiret drugie"));
        }

        [Fact]
        public void TiretOrdinalRange_IsExpanded()
        {
            Assert.Equal(
                new[]
                {
                    "art_1__pkt_48__lit_a__tir_4",
                    "art_1__pkt_48__lit_a__tir_5",
                    "art_1__pkt_48__lit_a__tir_6",
                },
                Paths("art. 1 pkt 48 lit. a tiret czwarte–szóste"));
        }

        // ============================================================
        // Zakres („w zakresie …")
        // ============================================================

        [Fact]
        public void StructuralScope_IsAttachedAndTargetStaysApplicable()
        {
            var result = UnitEnumerationParser.Parse("art. 1 pkt 4 w zakresie art. 18b ust. 2");

            var target = Assert.Single(result.Targets);
            Assert.Equal("art_1__pkt_4", target.Structure.ToString());
            Assert.Equal(ScopeKind.Structural, target.ScopeKind);
            Assert.Equal(new[] { "art_18b__ust_2" }, target.ScopeStructures.Select(s => s.ToString()));
            Assert.True(target.IsApplicable);
        }

        [Fact]
        public void ScopeQualifier_IsCaptured()
        {
            var result = UnitEnumerationParser.Parse("art. 1 pkt 26 w zakresie dodawanego art. 26e");

            var target = Assert.Single(result.Targets);
            Assert.Equal("dodawanego", target.ScopeQualifier);
            Assert.Equal(new[] { "art_26e" }, target.ScopeStructures.Select(s => s.ToString()));
        }

        [Fact]
        public void ScopeEndsAtOraz_SoFollowingUnitIsAMainTarget()
        {
            // „art. 4 pkt 3 w zakresie art. 30h–30j oraz art. 5 pkt 15" — art. 5 pkt 15 to cel główny.
            Assert.Equal(
                new[] { "art_4__pkt_3", "art_5__pkt_15" },
                Paths("art. 4 pkt 3 w zakresie art. 30h–30j oraz art. 5 pkt 15"));
        }

        [Fact]
        public void ScopeEndsAtComma_SoFollowingUnitIsAMainTarget()
        {
            Assert.Equal(
                new[] { "art_1__pkt_10", "art_1__pkt_11__lit_a", "art_1__pkt_11__lit_e" },
                Paths("art. 1 pkt 10 w zakresie art. 12k-12n, pkt 11 lit. a i e"));
        }

        // ============================================================
        // Przypadki nieparsowalne — muszą być oflagowane, nie zgadnięte
        // ============================================================

        [Fact]
        public void ProseScope_FlagsTargetAsNotApplicable()
        {
            var result = UnitEnumerationParser.Parse(
                "art. 1 pkt 20 lit. a tiret drugie w zakresie, w jakim dotyczy dziesięcioletniego " +
                "planu rozwoju sieci wodorowej o zasięgu unijnym");

            Assert.NotEmpty(result.Targets);
            Assert.All(result.Targets, t => Assert.False(t.IsApplicable));
            Assert.False(result.IsComplete);
        }

        [Theory]
        // Przykład autorów schematu (DU/2024/928, art. 64): gołe „4" po „rozdziału" to rozdział, nie punkt.
        [InlineData("art. 5 ust. 4, art. 25 ust. 1 pkt 8 i rozdziału 4", "art_5__ust_4", "art_25__ust_1__pkt_8", "rozdz_4")]
        [InlineData("rozdziału 3 i 4", "rozdz_3", "rozdz_4")]
        [InlineData("działu II rozdziału 3 oraz art. 7", "dzial_2__rozdz_3", "art_7")]
        public void SystematizingUnit_IsATarget(string units, params string[] expected)
        {
            var result = UnitEnumerationParser.Parse(units);

            Assert.Equal(expected, result.Targets.Select(t => t.Structure.ToString()));
            Assert.All(result.Targets, t => Assert.True(t.IsApplicable));
            Assert.True(result.IsComplete);
        }

        [Fact]
        public void SystematizingUnitScope_IsProse_AndFlagged()
        {
            // „w zakresie oznaczenia i tytułu rozdziału 2a" — zakres nie jest jednostką redakcyjną.
            var result = UnitEnumerationParser.Parse("art. 4 pkt 3 w zakresie oznaczenia i tytułu rozdziału 2a");

            Assert.NotEmpty(result.Targets);
            Assert.All(result.Targets, t => Assert.False(t.IsApplicable));
            Assert.False(result.IsComplete);
        }

        [Fact]
        public void AmbiguousRange_IsFlagged_NotGuessed()
        {
            // „5a–7b" — § 89 ZTP nie definiuje następnika przez dwie różne części liczbowe.
            var result = UnitEnumerationParser.Parse("art. 1 pkt 5a–7b");

            Assert.False(result.IsComplete);
            Assert.NotEmpty(result.Targets);
            Assert.All(result.Targets, t => Assert.False(t.IsApplicable));
        }

        [Fact]
        public void ScopeEnumeration_IsExpandedToEveryUnit()
        {
            // Dz.U. 2026 poz. 26: „art. 1 pkt 9 w zakresie art. 85a ust. 1 i art. 85c–85j".
            // Zakres jest WYLICZENIEM z przedziałem — musi dać wszystkie dziesięć jednostek,
            // nie tylko pierwszą.
            var result = UnitEnumerationParser.Parse("art. 1 pkt 9 w zakresie art. 85a ust. 1 i art. 85c–85j");

            var target = Assert.Single(result.Targets);
            Assert.Equal("art_1__pkt_9", target.Structure.ToString());
            Assert.Equal(ScopeKind.Structural, target.ScopeKind);
            Assert.Equal(
                new[]
                {
                    "art_85a__ust_1",
                    "art_85c", "art_85d", "art_85e", "art_85f",
                    "art_85g", "art_85h", "art_85i", "art_85j",
                },
                target.ScopeStructures.Select(s => s.ToString()));
        }

        [Fact]
        public void ScopeEnds_WhenEnumerationClimbsBackUp()
        {
            // Dz.U.: „art. 4 pkt 2 w zakresie uchylanego pkt 5 i art. 5". Wewnątrz zakresu można
            // zostać na poziomie albo zejść głębiej; powrót W GÓRĘ znaczy, że zakres się skończył
            // i wróciliśmy do wyliczenia głównego. Bez tego „art. 5" znika wchłonięty przez zakres.
            var result = UnitEnumerationParser.Parse("art. 4 pkt 2 w zakresie uchylanego pkt 5 i art. 5");

            Assert.Equal(new[] { "art_4__pkt_2", "art_5" },
                result.Targets.Select(t => t.Structure.ToString()));

            var scoped = result.Targets[0];
            Assert.Equal(new[] { "pkt_5" }, scoped.ScopeStructures.Select(s => s.ToString()));
            Assert.Equal(ScopeKind.None, result.Targets[1].ScopeKind);
        }

        [Fact]
        public void ScopeContinues_WhenEnumerationStaysAtItsLevelOrGoesDeeper()
        {
            // Granica działa w jedną stronę. Ten sam poziom co początek zakresu…
            var sameLevel = UnitEnumerationParser.Parse("art. 1 pkt 9 w zakresie art. 85a ust. 1 i art. 85c");
            Assert.Equal(new[] { "art_85a__ust_1", "art_85c" },
                Assert.Single(sameLevel.Targets).ScopeStructures.Select(s => s.ToString()));

            // …i poziom głębszy niż początek zakresu — oba zostają w zakresie.
            var deeper = UnitEnumerationParser.Parse("art. 1 pkt 9 w zakresie art. 85a ust. 1 i ust. 3");
            Assert.Equal(new[] { "art_85a__ust_1", "art_85a__ust_3" },
                Assert.Single(deeper.Targets).ScopeStructures.Select(s => s.ToString()));
        }

        [Fact]
        public void ScopeEnumeration_InheritsLevelForBareDesignation()
        {
            // Dz.U. 2026 poz. 26: „art. 13 pkt 2 lit. b w zakresie ust. 1d i 1e".
            // Gołe „1e" dziedziczy poziom ustępu — tak samo jak w wyliczeniu głównym.
            var result = UnitEnumerationParser.Parse("art. 13 pkt 2 lit. b w zakresie ust. 1d i 1e");

            var target = Assert.Single(result.Targets);
            Assert.Equal("art_13__pkt_2__lit_b", target.Structure.ToString());
            Assert.Equal(new[] { "ust_1d", "ust_1e" }, target.ScopeStructures.Select(s => s.ToString()));
        }

        // ============================================================
        // Parytet wyliczenia głównego i zakresu — oba idą tym samym kursorem
        // ============================================================

        [Fact]
        public void ScopeNestedTiret_UsesDottedPath()
        {
            // Zakres musi kodować zagnieżdżony tiret tak samo jak wyliczenie główne: „1.2", nie dwa cele.
            var result = UnitEnumerationParser.Parse(
                "art. 1 pkt 13 w zakresie art. 5 pkt 2 lit. a tiret pierwsze podwójne tiret drugie");

            var target = Assert.Single(result.Targets);
            Assert.Equal(ScopeKind.Structural, target.ScopeKind);
            Assert.Equal(new[] { "art_5__pkt_2__lit_a__tir_1__dwtir_2" },
                target.ScopeStructures.Select(s => s.ToString()));
        }

        [Fact]
        public void ScopeSentenceOrdinal_IsNumbered()
        {
            var result = UnitEnumerationParser.Parse("art. 1 pkt 4 w zakresie art. 5 ust. 2 zdanie drugie");

            var target = Assert.Single(result.Targets);
            Assert.Equal(new[] { "art_5__ust_2__zdn_2" }, target.ScopeStructures.Select(s => s.ToString()));
        }

        [Fact]
        public void ScopeSentenceThatIsNotANumeral_MakesTheWholeScopeProse()
        {
            // „ostatnie" nie jest liczebnikiem porządkowym. Zakres rozkłada się w całości albo wcale —
            // wcześniej dawał ścieżkę „zd_ostatnie", która wyglądała na kompletną.
            var result = UnitEnumerationParser.Parse("art. 1 pkt 4 w zakresie art. 5 ust. 2 zdanie ostatnie");

            var target = Assert.Single(result.Targets);
            Assert.Equal(ScopeKind.Prose, target.ScopeKind);
            Assert.False(target.IsApplicable);
            Assert.NotEmpty(result.Unresolved);
        }

        [Fact]
        public void MainAndScope_AgreeOnNestedTiret()
        {
            // Ten sam ogon powołania raz jako wyliczenie główne, raz w zakresie — rozwidlenie kursora
            // natychmiast łamie ten test.
            const string tail = "art. 5 pkt 2 lit. a tiret pierwsze podwójne tiret drugie";

            var asMain = UnitEnumerationParser.Parse(tail)
                .Targets.Select(t => t.Structure.ToString());

            var asScope = UnitEnumerationParser.Parse($"art. 1 pkt 13 w zakresie {tail}")
                .Targets.Single().ScopeStructures.Select(s => s.ToString());

            Assert.Equal(asMain, asScope);
        }

        [Fact]
        public void OrdinalBeforeMultiplicityKeyword_IsNesting_NotTwoSiblings()
        {
            // Prawodawca zapisuje zagnieżdżony tiret w dwóch szykach. Tu liczebnik stoi PRZED słowem
            // kluczowym („pierwsze podwójne tiret" = podwójne tiret pierwsze), więc dwa sąsiadujące
            // oznaczenia to jedna ścieżka w głąb, a nie dwa tirety obok siebie.
            // Kształt z Dz.U.: „art. 5 … pkt 18 lit. a tiret drugie pierwsze podwójne tiret i lit. d".
            Assert.Equal(
                new[] { "art_5__pkt_18__lit_a__tir_2__dwtir_1" },
                Paths("art. 5 pkt 18 lit. a tiret drugie pierwsze podwójne tiret"));
        }

        [Fact]
        public void OrdinalsSeparatedByConjunction_AreSiblings_NotNesting()
        {
            // Ten sam poziom, bo oznaczenia rozdziela separator — to jest granica między wariantami.
            Assert.Equal(
                new[] { "art_5__pkt_18__lit_a__tir_1", "art_5__pkt_18__lit_a__tir_2" },
                Paths("art. 5 pkt 18 lit. a tiret pierwsze i drugie"));

            Assert.Equal(
                new[] { "art_5__pkt_18__lit_a__tir_1", "art_5__pkt_18__lit_a__tir_2" },
                Paths("art. 5 pkt 18 lit. a tiret pierwsze, drugie"));
        }

        [Fact]
        public void MultiplicityKeywordWithoutAnyOrdinal_IsNotFedFromTheLevelAbove()
        {
            // „lit. a podwójne tiret" — przed słowem kluczowym stoi oznaczenie LITERY, nie drugi
            // liczebnik tiretu. Zabranie go zostawiłoby literę bez numeru, a że „a" nie jest
            // liczebnikiem, krok tiretu padłby i cel zniknąłby CAŁKOWICIE. Dlatego asercja musi
            // sprawdzać, że litera PRZETRWAŁA — samo wykluczenie ścieżki „dwtir_a" jest spełnione
            // także wtedy, gdy strażnik nie działa.
            Assert.Equal(
                new[] { "art_5__pkt_18__lit_a" },
                Paths("art. 5 pkt 18 lit. a podwójne tiret"));
        }

        [Fact]
        public void SentenceOrdinal_IsNumbered()
        {
            // Zdanie powołuje się liczebnikiem porządkowym (§ 57 ust. 6 ZTP), tak jak tiret.
            Assert.Equal(new[] { "art_5__ust_2__zdn_2" }, Paths("art. 5 ust. 2 zdanie drugie"));
        }

        [Fact]
        public void SentenceDesignationThatIsNotANumeral_IsFlagged_NotGuessed()
        {
            // „zdanie ostatnie" nie jest liczebnikiem porządkowym — numeru zdania nie da się ustalić,
            // więc cel nie powstaje, a fragment ląduje wśród nierozłożonych.
            var result = UnitEnumerationParser.Parse("art. 5 ust. 2 zdanie ostatnie");

            Assert.DoesNotContain(result.Targets, t => t.Structure.Sentence is not null);
            Assert.Contains("ostatnie", result.Unresolved);
            Assert.False(result.IsComplete);
        }

        // ============================================================
        // Paragraf kodeksu — „§" po artykule to „par", nie „ust" (ADR-0014)
        // ============================================================

        [Fact]
        public void CodeParagraphInScope_UsesPar()
        {
            // Korpus: 311010_2798032 — zakres wskazuje paragraf kodeksu.
            var result = UnitEnumerationParser.Parse(
                "art. 140 pkt 4 w zakresie art. 14zq § 5 oraz art. 145 pkt 1-3, 5 i 6");

            var scoped = result.Targets.First();
            Assert.Equal("art_140__pkt_4", scoped.Structure.ToString());
            Assert.Equal("art_14zq__par_5", Assert.Single(scoped.ScopeStructures).ToString());
        }

        [Fact]
        public void LetterI_IsALetter_NotAConjunction()
        {
            // Korpus: 354656_3144435. „lit. i" czytane jako spójnik przenosiło datę wyjątku na cały pkt 13.
            var paths = Paths(
                "art. 1 pkt 12 lit. a i b, pkt 13 lit. i, pkt 14, pkt 15 lit. a w zakresie art. 23 ust. 1 pkt 1 lit. b, " +
                "pkt 16 lit. a, c, d i e, pkt 17, 18, 20 i 25, art. 21, art. 26 oraz art. 28");

            Assert.Contains("art_1__pkt_13__lit_i", paths);
            Assert.DoesNotContain("art_1__pkt_13", paths);
            Assert.Contains("art_1__pkt_12__lit_a", paths);
            Assert.Contains("art_1__pkt_12__lit_b", paths);
        }

        [Fact]
        public void BareSectionSign_InSectionBasedAct_IsTheMainUnit()
        {
            // Korpus: 316175_2967132 (rozporządzenie) — „§" jest tu jednostką główną.
            Assert.Equal(new[] { "art_1__pkt_3", "art_1__pkt_4__lit_b" },
                UnitEnumerationParser.Parse("§ 1 pkt 3 i pkt 4 lit. b", sectionSignIsBasicUnit: true)
                    .Targets.Select(t => t.Structure.ToString()).ToArray());
        }

        [Fact]
        public void BareSectionSign_InStatute_IsNotReadAsArticle()
        {
            // Ten sam fragment w ustawie: samodzielny „§" to paragraf kodeksu nieznanego artykułu,
            // więc artykułu się nie zgaduje (wcześniej powstawało art_1).
            var result = UnitEnumerationParser.Parse("§ 1 pkt 3 i pkt 4 lit. b");

            Assert.DoesNotContain(result.Targets, t => t.Structure.Article is not null);
            Assert.Contains("1", result.Unresolved);
        }

        [Fact]
        public void ScopeInSectionBasedAct_LetterWAfterLit_IsALetter()
        {
            // Korpus: 326666_2941945 — zakres w obrębie rozporządzenia; „lit. w" to litera, nie przyimek.
            var scoped = UnitEnumerationParser.Parse(
                "§ 1 pkt 1 w zakresie § 17 pkt 2 lit. w oraz x", sectionSignIsBasicUnit: true).Targets.First();

            Assert.Equal("art_1__pkt_1", scoped.Structure.ToString());
            Assert.Equal("art_17__pkt_2__lit_w", scoped.ScopeStructures.First().ToString());
        }

        [Fact]
        public void BareDesignationAfterCodeParagraph_StaysCodeParagraph()
        {
            // Korpus: 361298_3194530 — gołe „3" dziedziczy nie tylko poziom, ale i to, że jest paragrafem.
            var paths = Paths("art. 586-591 i art. 592 § 2 i 3");

            Assert.Equal(new[] { "art_592__par_2", "art_592__par_3" }, paths.TakeLast(2).ToArray());
            Assert.DoesNotContain(paths, p => p.Contains("ust_", StringComparison.Ordinal));
        }

        [Fact]
        public void SuperscriptInBrackets_IsPartOfTheDesignation()
        {
            // schemaPL zapisuje indeks górny w nawiasach kwadratowych: „art. 125[1]" = art. 125¹.
            var result = UnitEnumerationParser.Parse("art. 1 pkt 2 w zakresie art. 125 [ 1 ] § 2");

            var target = Assert.Single(result.Targets);
            Assert.Equal("art_1__pkt_2", target.Structure.ToString());
            Assert.Equal(ScopeKind.Structural, target.ScopeKind);
            Assert.Equal("125[1]", Assert.Single(target.ScopeStructures).Article);
        }

        [Fact]
        public void EmptyInput_YieldsNoTargets()
        {
            var result = UnitEnumerationParser.Parse("   ");
            Assert.Empty(result.Targets);
            Assert.Empty(result.Unresolved);
        }

        // ============================================================
        // Znacznik odnośnika przy powołaniu
        // ============================================================

        [Theory]
        // TJ 317125_2841297: odnośnik przy „art. 217" czytał się jako kolejny artykuł („art. 22").
        [InlineData("art. 217[22)] oraz art. 227", new[] { "art_217", "art_227" })]
        [InlineData("art. 5 ust. 2[3)] i ust. 4", new[] { "art_5__ust_2", "art_5__ust_4" })]
        // Indeks górny oznaczenia zlany w DOCX z odnośnikiem w jeden nawias.
        [InlineData("art. 4[1 15)] i art. 6", new[] { "art_4[1]", "art_6" })]
        public void FootnoteMarker_IsNotAUnitDesignation(string units, string[] expected)
        {
            var result = UnitEnumerationParser.Parse(units);

            Assert.Equal(expected, result.Targets.Select(t => t.Structure.ToString()));
        }
    }
}
