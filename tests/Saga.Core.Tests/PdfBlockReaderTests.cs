using Saga.Core.Exceptions;
using Saga.Core.Ingest;
using Saga.Core.Ingest.Pdf;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Testy adaptera PDF (Etap 9): odtwarzanie wierszy i bloków z warstwy tekstowej, polskie znaki,
    /// filtr artefaktów stron, dehyfenacja, jednostka przecięta granicą stron, superscript → [x],
    /// strefa przypisów, detekcja skanu i wcięcia → twips. Fikstury: <see cref="TestPdfBuilder"/>
    /// (Helvetica + /Differences — bez plików fontów).
    ///
    /// LUKA (udokumentowana): gałąź PDF zaszyfrowanego hasłem (PdfDocumentEncryptedException →
    /// UnsupportedDocumentFormatException) nie ma testu — TestPdfBuilder nie buduje szyfrowanych
    /// PDF-ów, a binarna fikstura wymagałaby odstępstwa od zasady „bez binariów w repo".
    /// Typ wyjątku PdfPig 0.1.15 zweryfikowany w przeglądzie adwersaryjnym Etapu 9.
    /// </summary>
    public class PdfBlockReaderTests
    {
        private static System.Collections.Generic.IReadOnlyList<DocumentBlock> Read(byte[] pdf)
        {
            using var ms = new MemoryStream(pdf);
            return new PdfBlockReader().ReadBlocks(ms);
        }

        // Gęsty tekst zapełniający stronę (detekcja skanu wymaga ≥ 20 znaków/stronę średnio).
        private static TestPdfBuilder WithBodyPadding(TestPdfBuilder b, double fromY = 400, int lines = 5)
        {
            for (int i = 0; i < lines; i++)
                b.AddText($"Art. {90 + i}. Przepis wypełniający strony do celów testowych.", 57, fromY - i * 14);
            return b;
        }

        [Fact]
        public void PolishDiacritics_RoundTripThroughTextLayer()
        {
            var pdf = WithBodyPadding(new TestPdfBuilder().AddPage()
                .AddText("Art. 1. Świadczeń pieniężnych udziela się żołnierzom oraz ich małżonkom.", 57, 700))
                .Build();

            var blocks = Read(pdf);

            var first = blocks.First(b => b.Text.StartsWith("Art. 1."));
            Assert.Contains("Świadczeń pieniężnych", first.Text);
            Assert.Contains("małżonkom", first.Text);
        }

        [Fact]
        public void ReadingOrder_TopToBottom_BlockPerUnitLine()
        {
            var pdf = new TestPdfBuilder().AddPage()
                .AddText("Art. 1. Artykuł pierwszy.", 57, 700)
                .AddText("Art. 2. Artykuł drugi zawiera wyliczenie:", 57, 680)
                .AddText("1) punkt pierwszy;", 57, 666)
                .AddText("2) punkt drugi.", 57, 652)
                .Build();

            var blocks = Read(pdf);

            Assert.Equal(4, blocks.Count);
            Assert.StartsWith("Art. 1.", blocks[0].Text);
            Assert.StartsWith("Art. 2.", blocks[1].Text);
            Assert.StartsWith("1)", blocks[2].Text);
            Assert.StartsWith("2)", blocks[3].Text);
        }

        [Fact]
        public void ContinuationLines_JoinIntoSingleBlock()
        {
            // Zawinięty ustęp: kontynuacja zaczyna się małą literą, bez markera, w typowej interlinii.
            var pdf = new TestPdfBuilder().AddPage()
                .AddText("Art. 1. Organ prowadzi rejestr dokumentów oraz", 57, 700)
                .AddText("udostępnia go na wniosek zainteresowanego.", 57, 686)
                .AddText("Art. 2. Przepis odrębny.", 57, 672)
                .Build();

            var blocks = Read(pdf);

            Assert.Equal(2, blocks.Count);
            Assert.Equal("Art. 1. Organ prowadzi rejestr dokumentów oraz udostępnia go na wniosek zainteresowanego.",
                blocks[0].Text);
        }

        [Theory]
        // ADR-0111, konwencja składu Dz. U.: przeniesienie wyrazu nie powtarza dywizu (Dz. U. 2026 poz. 1358: „prak-" /
        // „tyczne") — dywiz znika; złożenie dzielone na dywizie ma go powtórzonego na początku wiersza — zostaje jeden.
        [InlineData("Art. 1. Organy podejmują prak-", "tyczne i proporcjonalne środki.", "podejmują praktyczne i proporcjonalne")]
        [InlineData("Art. 1. Wspiera się rozwój społeczno-", "-gospodarczy kraju oraz regionów.", "rozwój społeczno-gospodarczy kraju")]
        [InlineData("Art. 1. Pomoc w związku z epidemią COVID-", "19 przysługuje do końca roku.", "epidemią COVID-19 przysługuje")]
        public void TrailingHyphen_IsAWordBreak_UnlessRepeatedOnTheNextLine(string first, string second, string expected)
        {
            var pdf = new TestPdfBuilder().AddPage()
                .AddText(first, 57, 700)
                .AddText(second, 57, 686)
                .Build();

            var blocks = Read(pdf);

            Assert.Contains(expected, blocks[0].Text);
        }

        [Theory]
        // PDF z edytora (korpus, 297516) dzieli złożenie bez powtórzonego dywizu. Człon „kolejowo-” stoi w dokumencie
        // w środku wiersza przed dywizem — zostaje złożeniem; „okreś-” nie — przeniesienie.
        [InlineData("Art. 2. Przejazd kolejowo-", "drogowy kategorii A.", "Przejazd kolejowo-drogowy kategorii")]
        [InlineData("Art. 2. Zarządca okreś-", "la wymagania przejazdu.", "Zarządca określa wymagania")]
        public void TrailingHyphen_BeforeLowercase_KeepsACompoundTheDocumentWritesWhole(string first, string second, string expected)
        {
            var pdf = new TestPdfBuilder().AddPage()
                .AddText("Art. 1. Przejazdy kolejowo-drogowe oznacza się tablicą.", 57, 700)
                .AddText(first, 57, 680)
                .AddText(second, 57, 666)
                .Build();

            var blocks = Read(pdf);

            Assert.Contains(expected, blocks[1].Text);
        }

        [Fact]
        public void TrailingHyphen_BeforeLowercase_JoinsAWordTheDocumentWritesWhole_EvenAfterACompoundHead()
        {
            // „samo-chodu”: człon „samo” stoi w dokumencie przed dywizem („samo-pomocy”), ale wyraz „samochodu” też —
            // wyraz sklejony rozstrzyga na korzyść przeniesienia.
            var pdf = new TestPdfBuilder().AddPage()
                .AddText("Art. 1. Grupy samo-pomocy korzystają z samochodu gminy.", 57, 700)
                .AddText("Art. 2. Kierowca samo-", 57, 680)
                .AddText("chodu ma uprawnienia.", 57, 666)
                .Build();

            var blocks = Read(pdf);

            Assert.Contains("Kierowca samochodu ma", blocks[1].Text);
        }

        [Fact]
        public void TrailingHyphen_FollowedByASpaceGlyph_IsJoinedDirectly()
        {
            // Dz. U. 2026 poz. 1209: składacz zostawił za dywizem na końcu wiersza glif odstępu („moto- ") — wiersz
            // kończy się wtedy spacją, a nie dywizem, i sklejenie wstawiało drugą („moto- rowerów").
            var pdf = new TestPdfBuilder().AddPage()
                .AddText("Art. 1. Zwrot kosztów używania motocykli i moto- ", 57, 700)
                .AddText("rowerów następuje na wniosek.", 57, 686)
                .Build();

            var blocks = Read(pdf);

            Assert.Contains("motocykli i motorowerów następuje", blocks[0].Text);
        }

        [Theory]
        // Korpus PDF: 295666_2708885, 329307_2940144 — „Tele- i Radiokomunikacji" z glifem odstępu za dywizem i bez niego.
        [InlineData("Art. 1. Instytut Tele- ")]
        [InlineData("Art. 1. Instytut Tele-")]
        public void SuspendedHyphen_AtTheLineEnd_KeepsItsSpace(string firstLine)
        {
            var pdf = new TestPdfBuilder().AddPage()
                .AddText(firstLine, 57, 700)
                .AddText("i Radiotechniki prowadzi badania.", 57, 686)
                .Build();

            var blocks = Read(pdf);

            Assert.Contains("Instytut Tele- i Radiotechniki prowadzi", blocks[0].Text);
        }

        [Fact]
        public void LegalBasisFormula_EndsTheParagraph_EvenWithoutADesignationAfterIt()
        {
            // M.P. 2026 poz. 994: za „obwieszcza się, co następuje:" akapit bez oznaczenia, oddzielony w składzie tylko
            // wcięciem pierwszego wiersza — sklejony z podstawą prawną, która przez to nie była rozpoznawana.
            var pdf = new TestPdfBuilder().AddPage()
                .AddText("Na podstawie art. 20a § 4 ustawy z dnia 27 lipca 2001 r. – Prawo o ustroju sądów", 57, 700)
                .AddText("powszechnych obwieszcza się, co następuje:", 57, 686)
                .AddText("W sądach powszechnych zostały zwolnione:", 70, 672)
                .AddText("1) stanowisko sędziego sądu okręgowego w Sądzie Okręgowym w Bydgoszczy;", 57, 658)
                .AddText("2) stanowisko sędziego sądu okręgowego w Sądzie Okręgowym w Krakowie.", 57, 644)
                .Build();

            var blocks = Read(pdf);

            Assert.EndsWith("obwieszcza się, co następuje:", blocks[0].Text);
            Assert.Equal("W sądach powszechnych zostały zwolnione:", blocks[1].Text);
        }

        [Fact]
        public void OverprintedText_IsReadOnce()
        {
            // M.P. 2026 poz. 948: skład pogrubia nagłówek, drukując go drugi raz w tym samym miejscu — „Poz. 948" czytało
            // się „994488", a strefa tytułowa w ogóle nie była rozpoznawana.
            var pdf = WithBodyPadding(new TestPdfBuilder().AddPage()
                .AddText("Art. 1. Uchwała wchodzi w życie z dniem następującym po dniu ogłoszenia.", 57, 700)
                .AddText("Art. 2. Organ prowadzi rejestr.", 57, 686)
                .AddText("Art. 2. Organ prowadzi rejestr.", 57, 686))
                .Build();

            var blocks = Read(pdf);

            Assert.Contains(blocks, b => b.Text == "Art. 2. Organ prowadzi rejestr.");
        }

        [Fact]
        public void TextSetAcrossThePage_IsReadInItsOwnLines()
        {
            // M.P. 2026 poz. 948: tabela załącznika złożona w poprzek strony pionowej — tekst obrócony o ćwierć obrotu.
            // Czytany w układzie strony dawał kolumny pojedynczych liter („z ar o yt zs ok").
            var pdf = new TestPdfBuilder().AddPage()
                .AddRotatedText("Art. 1. Pierwszy przepis złożony w poprzek strony.", 100, 100)
                .AddRotatedText("Art. 2. Drugi przepis złożony w poprzek strony.", 114, 100)
                .Build();

            var blocks = Read(pdf);

            Assert.Equal(new[] { "Art. 1. Pierwszy przepis złożony w poprzek strony.", "Art. 2. Drugi przepis złożony w poprzek strony." },
                blocks.Select(b => b.Text.Trim()));
        }

        [Fact]
        public void TableSetAcrossThePage_IsReadByItsRowsAndColumns()
        {
            // Obramowanie obraca się razem z tekstem: kolumny tabeli to w układzie strony linie poziome, wiersze — pionowe.
            var pdf = new TestPdfBuilder().AddPage()
                .AddRotatedText("Tabela – zestawienie kosztów programu wieloletniego", 80, 100)
                .AddGrid(rows: [100, 300, 500], columns: [95, 115, 135])
                .AddRotatedText("Część", 111, 110)
                .AddRotatedText("Kwota", 111, 310)
                .AddRotatedText("1", 131, 110)
                .AddRotatedText("500 zł", 131, 310)
                .Build();

            var cells = Read(pdf).Where(b => b.TableCell is not null)
                .Select(b => $"{b.TableCell!.Row}:{b.TableCell.Column}={b.Text.Trim()}");

            Assert.Equal(new[] { "0:0=Część", "0:1=Kwota", "1:0=1", "1:1=500 zł" }, cells);
        }

        [Fact]
        public void ContentSetAcrossThePage_IsLandscape_AndTheRestOfTheDocumentIsNot()
        {
            // Orientacja jak sekcja DOCX: załącznik dzieli się na segmenty pionowe i poziome (atrybut „orientacja”).
            var pdf = new TestPdfBuilder().AddPage()
                .AddText("Art. 1. Koszty programu określa załącznik do uchwały.", 57, 700)
                .AddPage()
                .AddRotatedText("Tabela – zestawienie kosztów programu wieloletniego", 80, 100)
                .AddGrid(rows: [100, 300, 500], columns: [95, 115, 135])
                .AddRotatedText("Część", 111, 110)
                .AddRotatedText("Kwota", 111, 310)
                .Build();

            var blocks = Read(pdf);

            Assert.False(Assert.Single(blocks, b => b.Text.StartsWith("Art. 1.")).IsLandscape);
            Assert.All(blocks.Where(b => b.Source.PageNumber == 2), b => Assert.True(b.IsLandscape));
            Assert.Equal(4, blocks.Count(b => b.TableCell is not null));
        }

        [Fact]
        public void WidePage_IsLandscape()
        {
            var pdf = new TestPdfBuilder { PageWidth = 842, PageHeight = 595 }.AddPage()
                .AddText("Art. 1. Przepis na stronie poziomej.", 57, 500)
                .Build();

            Assert.True(Assert.Single(Read(pdf)).IsLandscape);
        }

        [Fact]
        public void ReferenceToAnArticle_AtTheLineStart_ContinuesTheSentence()
        {
            // Dz. U. 2026 poz. 1336 (art. 28 § 2 k.p.w.): zawinięcie postawiło „Art. 57 § 1 Kodeksu …" na początku wiersza —
            // czytane jako nowy artykuł 57. Oznaczenie artykułu ma kropkę za numerem, powołanie — nie.
            var pdf = new TestPdfBuilder().AddPage()
                .AddText("§ 2. Odstąpienie oskarżyciela posiłkowego od oskarżenia nie tamuje rozpoznania sprawy.", 57, 700)
                .AddText("Art. 57 § 1 Kodeksu postępowania karnego stosuje się odpowiednio.", 57, 686)
                .AddText("Art. 29. § 1. Niestawiennictwo pokrzywdzonego nie tamuje rozpoznania sprawy.", 70, 672)
                .AddText("Art. 41a–Art. 41i. (uchylone)", 70, 658)
                .AddText("Art. 32g 1. Do czasu określenia zasad udzielania ulg stosuje się przepisy dotychczasowe.", 70, 644)
                .Build();

            var blocks = Read(pdf);

            Assert.Equal(4, blocks.Count);
            Assert.EndsWith("sprawy. Art. 57 § 1 Kodeksu postępowania karnego stosuje się odpowiednio.", blocks[0].Text);
            Assert.StartsWith("Art. 29. § 1.", blocks[1].Text);
            // Zakres z powtórzonym oznaczeniem po myślniku to nadal oznaczenie (Dz. U. 2025 poz. 18).
            Assert.Equal("Art. 41a–Art. 41i. (uchylone)", blocks[2].Text.Trim());
            // Kropka zgubiona za numerem artykułu, a za nim oznaczenie ustępu (Dz. U. 2024 poz. 1473) — nadal oznaczenie.
            Assert.StartsWith("Art. 32g 1. Do czasu", blocks[3].Text);
        }

        [Fact]
        public void AnnexInALargerFont_DoesNotTurnTheActPagesIntoFootnotes()
        {
            // Dz. U. 2026 poz. 1320: akt 10 pt, wzór wniosku w załączniku 12 pt. Rozmiar treści liczony z całego dokumentu
            // wypadał na 12 pt, a strona aktu zaczęta „6) …" wpadała w całości w strefę przypisów.
            var builder = new TestPdfBuilder().AddPage()
                .AddText("§ 1. Wniosek o zawarcie umowy zawiera:", 57, 700, 10);
            for (var i = 1; i <= 5; i++)
                builder.AddText($"{i}) element wniosku numer {i} określony w rozporządzeniu;", 57, 700 - 14 * i, 10);
            builder.AddPage()
                .AddText("6) informacje o stanie zagospodarowania złoża kopaliny;", 57, 760, 10)
                .AddText("7) cel korzystania z informacji geologicznej.", 57, 746, 10)
                .AddText("§ 2. Rozporządzenie wchodzi w życie po upływie 14 dni od dnia ogłoszenia.", 57, 732, 10)
                .AddPage()
                .AddText("Załącznik nr 1 do rozporządzenia Ministra Klimatu i Środowiska", 300, 780, 12);
            for (var i = 0; i < 30; i++)
                builder.AddText($"Pole formularza numer {i} do wypełnienia przez wnioskodawcę ...........", 57, 740 - 20 * i, 12);

            var blocks = Read(builder.Build());

            var section = Assert.Single(blocks, b => b.Text.StartsWith("§ 2.", StringComparison.Ordinal));
            Assert.Equal(BlockRole.Body, section.Role);
            Assert.Equal(BlockRole.Body, Assert.Single(blocks, b => b.Text.StartsWith("6)", StringComparison.Ordinal)).Role);
        }

        [Fact]
        public void SignatureOfAShortAnnouncement_EndsItsBlock_BeforeTheAnnexes()
        {
            // M.P. 2026 poz. 1002: krótkie obwieszczenie — wiersze wyśrodkowane i po prawej przeważają nad treścią, moda
            // początków wierszy nie była marginesem, podpis przestawał stać „po prawej" i sklejał się z nagłówkiem załączników.
            var pdf = new TestPdfBuilder().AddPage()
                .AddText("OBWIESZCZENIE", 255, 760)
                .AddText("MINISTRA FINANSÓW I GOSPODARKI", 205, 746)
                .AddText("z dnia 6 października 2026 r.", 230, 732)
                .AddText("w sprawie stawek minimalnych podatku od środków transportowych", 150, 718)
                .AddText("Na podstawie art. 12b ust. 4 ustawy o podatkach i opłatach lokalnych ogłasza się stawki", 63, 696)
                .AddText("minimalne podatku od środków transportowych na rok 2027.", 51, 682)
                .AddText("Minister Finansów i Gospodarki: wz. J. Neneman", 330, 650)
                .AddPage()
                .AddText("Załączniki do obwieszczenia Ministra Finansów i Gospodarki", 230, 780)
                .AddText("z dnia 6 października 2026 r. (M.P. poz. 1002)", 230, 766)
                .AddText("Załącznik nr 1", 470, 740)
                .AddText("STAWKI MINIMALNE PODATKU DLA POJAZDÓW", 180, 720)
                .AddPage()
                .AddText("Załącznik nr 2", 470, 780)
                .AddText("STAWKI MINIMALNE PODATKU DLA CIĄGNIKÓW", 170, 760)
                .AddPage()
                .AddText("Załącznik nr 3", 470, 780)
                .AddText("STAWKI MINIMALNE PODATKU DLA PRZYCZEP", 175, 760)
                .Build();

            var blocks = Read(pdf);

            Assert.Contains(blocks, b => b.Text.Trim() == "Minister Finansów i Gospodarki: wz. J. Neneman");
            Assert.Contains(blocks, b => b.Text.StartsWith("Załączniki do obwieszczenia", StringComparison.Ordinal));
        }

        [Fact]
        public void TrailingHyphen_BeforeUppercase_JoinsCompoundName()
        {
            var pdf = new TestPdfBuilder().AddPage()
                .AddText("Art. 1. Siedzibą sądu jest Warszawa-", 57, 700)
                .AddText("Praga oraz miasto stołeczne.", 57, 686)
                .Build();

            var blocks = Read(pdf);

            Assert.Contains("Warszawa-Praga oraz", blocks[0].Text);
        }

        [Fact]
        public void UnitCutByPageBoundary_JoinsAcrossPages()
        {
            var builder = new TestPdfBuilder();
            builder.AddPage();
            WithBodyPadding(builder, fromY: 700);
            builder.AddText("Art. 2. Przepis stosuje się do umów", 57, 100);
            builder.AddPage();
            builder.AddText("zawartych przed dniem wejścia w życie ustawy.", 57, 780);
            WithBodyPadding(builder, fromY: 700);

            var blocks = Read(builder.Build());

            var cut = blocks.Single(b => b.Text.StartsWith("Art. 2."));
            Assert.Equal("Art. 2. Przepis stosuje się do umów zawartych przed dniem wejścia w życie ustawy.", cut.Text);
        }

        [Fact]
        public void PageArtifacts_HeaderAndPagination_AreFiltered()
        {
            // Nagłówek wydawniczy (góra) i paginacja (dół) powtórzone na obu stronach + whitelist.
            var builder = new TestPdfBuilder();
            for (int p = 1; p <= 2; p++)
            {
                builder.AddPage();
                builder.AddText($"Dziennik Ustaw – {p} – Poz. 902", 200, 820, 9);
                WithBodyPadding(builder, fromY: 700);
                builder.AddText($"– {p} –", 280, 30, 9);
            }

            var blocks = Read(builder.Build());

            Assert.DoesNotContain(blocks, b => b.Text.Contains("Dziennik Ustaw"));
            Assert.DoesNotContain(blocks, b => b.Text.Contains("Poz. 902"));
            Assert.DoesNotContain(blocks, b => b.Text.Trim().StartsWith("–") && b.Text.Contains("1 –"));
        }

        [Fact]
        public void Superscript_UnitNumber_YieldsBracketChannel()
        {
            // „Art. 15¹" — indeks górny mniejszym fontem z podniesioną linią bazową → kanał [1].
            // x=89,6: koniec „Art. 15" wg metryk AFM Helvetiki = 57 + 2,946 em × 11 pt ≈ 89,4 pt;
            // odstęp 0,2 pt < próg spacji (0,2 em = 2,2 pt), więc indeks klei się do numeru.
            var pdf = WithBodyPadding(new TestPdfBuilder().AddPage()
                .AddText("Art. 15", 57, 700, 11)
                .AddText("1", 89.6, 703.5, 7)
                .AddText(". Nowa jednostka dodana nowelizacją.", 94, 700, 11))
                .Build();

            var blocks = Read(pdf);

            Assert.Contains(blocks, b => b.Text.Contains("Art. 15[1]"));
        }

        [Fact]
        public void Superscript_SetWithItsBrackets_KeepsOnePair()
        {
            // Dz. U. 2026 poz. 1340: indeks złożony razem z nawiasami („32[1]”, nawiasy też podniesione i mniejsze) —
            // kanał [x] obejmował go drugi raz („Art. 32[[1]].”), a artykuł 32¹ czytał się jako 32.
            var pdf = WithBodyPadding(new TestPdfBuilder().AddPage()
                .AddText("Art. 15", 57, 700, 11)
                .AddText("[1]", 89.6, 703.5, 7)
                .AddText(". Nowa jednostka dodana nowelizacją.", 98, 700, 11))
                .Build();

            var blocks = Read(pdf);

            Assert.Contains(blocks, b => b.Text.StartsWith("Art. 15[1]. Nowa jednostka"));
        }

        [Fact]
        public void SpaceSetInSuperscriptSize_StaysOutsideTheSuperscript()
        {
            // „po art. 4e² dodaje się" — spacja za indeksem złożona jego rozmiarem i podniesiona jak on.
            var pdf = WithBodyPadding(new TestPdfBuilder().AddPage()
                .AddText("4) po art. 4e", 57, 700, 11)
                .AddText("2 ", 116.5, 703.5, 7)
                .AddText("dodaje się art. 4e", 124, 700, 11))
                .Build();

            var blocks = Read(pdf);

            Assert.Contains(blocks, b => b.Text.StartsWith("4) po art. 4e[2] dodaje się"));
        }

        [Fact]
        public void FootnoteZone_SmallerFontAtBottom_BecomesFootnoteBlocksAtEnd()
        {
            var builder = new TestPdfBuilder().AddPage();
            WithBodyPadding(builder, fromY: 700);
            builder.AddText("1) Zmiany tekstu jednolitego wymienionej ustawy ogłoszone zostały", 57, 90, 8);
            builder.AddText("w Dz. U. z 2023 r. poz. 100 oraz z 2024 r. poz. 5.", 57, 80, 8);

            var blocks = Read(builder.Build());

            var footnotes = blocks.Where(b => b.Role == BlockRole.FootnoteText).ToList();
            var footnote = Assert.Single(footnotes);
            Assert.StartsWith("1)", footnote.Text);
            Assert.Contains("poz. 100", footnote.Text);

            // Przypisy emitowane NA KOŃCU i nie mieszają się z punktami treści.
            Assert.Equal(blocks.Count - 1, blocks.ToList().IndexOf(footnote));
            Assert.DoesNotContain(blocks, b => b.Role == BlockRole.Body && b.Text.StartsWith("1) Zmiany"));
        }

        /// <summary>
        /// Wiersze w interlinii składu Dziennika Ustaw (14 pt), bez odstępu między strefą tytułową a treścią, i dopełnienie
        /// przepisami — żeby typowa interlinia była interlinią treści.
        /// </summary>
        private static IReadOnlyList<DocumentBlock> ReadLines(params string[] lines)
        {
            var builder = new TestPdfBuilder().AddPage();
            var y = 760.0;
            foreach (var line in lines.Concat(Enumerable.Range(90, 8).Select(n => $"Art. {n}. Przepis wypełniający stronę.")))
            {
                builder.AddText(line, 57, y);
                y -= 14;
            }

            return Read(builder.Build());
        }

        [Fact]
        public void TitleZone_KindDateAndSubject_AreSeparateBlocks()
        {
            var blocks = ReadLines(
                "USTAWA",
                "z dnia 18 grudnia 2025 r.",
                "o zmianie ustawy o systemie",
                "ubezpieczeń społecznych",
                "Art. 1. W ustawie wprowadza się zmiany.");

            Assert.Equal(
                ["Ustawa", "z dnia 18 grudnia 2025 r.", "o zmianie ustawy o systemie ubezpieczeń społecznych", "Art. 1. W ustawie wprowadza się zmiany."],
                blocks.Take(4).Select(b => b.Text));
        }

        [Fact]
        public void TitleZone_RegulationSubject_EndsAtTheLegalBasis()
        {
            var blocks = ReadLines(
                "ROZPORZĄDZENIE",
                "MINISTRA FINANSÓW",
                "z dnia 23 czerwca 2025 r.",
                "zmieniające rozporządzenie w sprawie zgłoszeń INTRASTAT",
                "Na podstawie art. 98 ust. 2 ustawy z dnia 19 marca 2004 r. – Prawo celne zarządza się,",
                "co następuje:",
                "§ 1. W rozporządzeniu wprowadza się zmiany.");

            Assert.Equal(
                ["Rozporządzenie Ministra Finansów", "z dnia 23 czerwca 2025 r.", "zmieniające rozporządzenie w sprawie zgłoszeń INTRASTAT",
                 "Na podstawie art. 98 ust. 2 ustawy z dnia 19 marca 2004 r. – Prawo celne zarządza się, co następuje:",
                 "§ 1. W rozporządzeniu wprowadza się zmiany."],
                blocks.Take(5).Select(b => b.Text));
        }

        [Fact]
        public void TitleZone_KindAndIssuerInCapitals_GetTheCaseOfTheActText()
        {
            var blocks = ReadLines(
                "ROZPORZĄDZENIE",
                "MINISTRA SPRAW WEWNĘTRZNYCH I ADMINISTRACJI",
                "z dnia 1 sierpnia 2025 r.",
                "w sprawie rejestru statków powietrznych",
                "Na podstawie art. 5 ustawy zarządza się, co następuje:",
                "§ 1. Rejestr prowadzi się w postaci elektronicznej.");

            Assert.Equal("Rozporządzenie Ministra Spraw Wewnętrznych i Administracji", blocks[0].Text);
            Assert.Equal("w sprawie rejestru statków powietrznych", blocks[2].Text);
        }

        [Fact]
        public void TitleZone_ResolutionNumber_AndHeaderWithLowercase_AreKeptRight()
        {
            Assert.Equal("Uchwała nr 103 Rady Ministrów", ReadLines("UCHWAŁA NR 103", "RADY MINISTRÓW", "z dnia 1 marca 2025 r.", "w sprawie programu",
                "§ 1. Przyjmuje się program.")[0].Text);
            Assert.Equal("Obwieszczenie Marszałka Sejmu Rzeczypospolitej Polskiej[a)]", ReadLines("OBWIESZCZENIE", "MARSZAŁKA SEJMU RZECZYPOSPOLITEJ POLSKIEJ[a)]",
                "z dnia 1 marca 2025 r.", "w sprawie ogłoszenia tekstu", "1. Na podstawie art. 16 ogłasza się tekst.")[0].Text);
            // Wiersz z małymi literami ma wielkość liter tekstu — zostaje, jak stoi.
            Assert.Equal("ROZPORZĄDZENIE Ministra Finansów", ReadLines("ROZPORZĄDZENIE", "Ministra Finansów", "z dnia 1 marca 2025 r.", "w sprawie opłat",
                "§ 1. Opłata wynosi 10 zł.")[0].Text);
        }

        [Fact]
        public void TitleZone_AnnouncementSubject_EndsAtItsFirstPoint()
        {
            var blocks = ReadLines(
                "OBWIESZCZENIE",
                "MARSZAŁKA SEJMU RZECZYPOSPOLITEJ POLSKIEJ",
                "z dnia 27 listopada 2024 r.",
                "w sprawie ogłoszenia jednolitego tekstu ustawy",
                "o żegludze śródlądowej",
                "1. Na podstawie art. 16 ust. 1 ustawy ogłasza się tekst jednolity.");

            Assert.Equal("w sprawie ogłoszenia jednolitego tekstu ustawy o żegludze śródlądowej", blocks[2].Text);
            Assert.StartsWith("1. Na podstawie", blocks[3].Text);
        }

        [Fact]
        public void DateLine_OutsideTheTitleZone_StaysInItsParagraph()
        {
            var blocks = ReadLines(
                "Art. 5. Do spraw wszczętych na podstawie ustawy",
                "z dnia 5 marca 2025 r.",
                "stosuje się przepisy dotychczasowe.");

            Assert.Equal("Art. 5. Do spraw wszczętych na podstawie ustawy z dnia 5 marca 2025 r. stosuje się przepisy dotychczasowe.",
                blocks[0].Text);
        }

        /// <summary>Wiersze z położeniem X, w interlinii 14 pt, z dopełnieniem przepisami przy lewym marginesie (57 pt).</summary>
        private static IReadOnlyList<DocumentBlock> ReadPlaced(params (string Text, double X)[] lines)
        {
            var builder = new TestPdfBuilder().AddPage();
            var y = 760.0;
            foreach (var (text, x) in Enumerable.Range(90, 8).Select(n => ($"Art. {n}. Przepis wypełniający stronę.", 57.0)).Concat(lines))
            {
                builder.AddText(text, x, y);
                y -= 14;
            }

            return Read(builder.Build());
        }

        [Fact]
        public void Signature_RightAligned_IsItsOwnBlock()
        {
            var blocks = ReadPlaced(
                ("§ 4. Rozporządzenie wchodzi w życie po upływie 14 dni od dnia ogłoszenia.", 72),
                ("Minister Finansów: A. Domański", 412));

            Assert.Equal("§ 4. Rozporządzenie wchodzi w życie po upływie 14 dni od dnia ogłoszenia.", blocks[^2].Text);
            Assert.Equal("Minister Finansów: A. Domański", blocks[^1].Text);
        }

        [Theory]
        [InlineData("Minister Spraw Wewnętrznych i Administracji: wz. C. Mroczek")]
        [InlineData("Minister Obrony Narodowej: z up. P. Bejda")]
        [InlineData("Marszałek Sejmu: Sz. Hołownia")]
        public void Signature_InEveryFormOfTheParser_IsItsOwnBlock(string signature)
        {
            var blocks = ReadPlaced(
                ("§ 2. Rozporządzenie wchodzi w życie z dniem 1 sierpnia 2025 r.", 72),
                (signature, 340));

            Assert.Equal(signature, blocks[^1].Text);
        }

        [Fact]
        public void AttachmentLine_RightAligned_OpensTheActAfterTheSignature()
        {
            var blocks = ReadPlaced(
                ("„Art. 88. Ustawa wchodzi w życie z dniem 28 czerwca 2025 r.”.", 72),
                ("Marszałek Sejmu: S. Hołownia", 400),
                ("Załącznik do obwieszczenia Marszałka Sejmu Rzeczypospolitej", 337),
                ("Polskiej z dnia 27 listopada 2024 r. (Dz. U. z 2025 r. poz. 18)", 337),
                ("USTAWA", 275));

            Assert.Equal(
                ["„Art. 88. Ustawa wchodzi w życie z dniem 28 czerwca 2025 r.”.", "Marszałek Sejmu: S. Hołownia",
                 "Załącznik do obwieszczenia Marszałka Sejmu Rzeczypospolitej Polskiej z dnia 27 listopada 2024 r. (Dz. U. z 2025 r. poz. 18)",
                 "Ustawa"],
                blocks.Skip(8).Select(b => b.Text));
        }

        [Fact]
        public void SharedAttachmentLine_AndAnnexDesignations_AreSeparateBlocks()
        {
            var blocks = ReadPlaced(
                ("§ 3. Rozporządzenie wchodzi w życie z dniem 20 stycznia 2026 r.", 72),
                ("Załączniki do rozporządzenia Ministra Infrastruktury", 330),
                ("z dnia 16 stycznia 2026 r. (Dz. U. poz. 56)", 330),
                ("Załącznik nr 1", 420),
                ("WZÓR", 280));

            Assert.Equal(
                ["Załączniki do rozporządzenia Ministra Infrastruktury z dnia 16 stycznia 2026 r. (Dz. U. poz. 56)", "Załącznik nr 1", "WZÓR"],
                blocks.Skip(9).Select(b => b.Text));
        }

        [Fact]
        public void AttachmentWords_AtTheLeftMargin_StayInTheParagraph()
        {
            // Zdanie zaczynające się od „Załącznik do" w zawiniętym akapicie — przy marginesie to nie wiersz przyłączenia.
            var blocks = ReadPlaced(
                ("Art. 5. Wzory dokumentów określa rozporządzenie, a ich wykaz", 72),
                ("Załącznik do rozporządzenia zawiera wzór wniosku.", 57));

            Assert.Equal("Art. 5. Wzory dokumentów określa rozporządzenie, a ich wykaz Załącznik do rozporządzenia zawiera wzór wniosku.",
                blocks[^1].Text);
        }

        [Fact]
        public void QuotedProvision_AfterAClosedQuotation_StartsANewBlock()
        {
            // Koniec przepisu przytoczonego „… 2024 r.”" i następny przepis przytoczony w kolejnym wierszu.
            var blocks = ReadLines(
                "„Art. 18. Do postępowań wszczętych od dnia 1 lipca 2024 r.”",
                "„Art. 22. W latach 2026–2035 maksymalny limit wydatków wynosi 575,4 mln zł.”");

            Assert.Equal("„Art. 18. Do postępowań wszczętych od dnia 1 lipca 2024 r.”", blocks[0].Text);
            Assert.StartsWith("„Art. 22.", blocks[1].Text);
        }

        [Fact]
        public void RepealedUnit_ClosesItsLine()
        {
            var blocks = ReadLines(
                "Art. 1. 1. Ustawa reguluje:",
                "1) zasady dochodzenia roszczeń;",
                "2) (uchylony)",
                "2. Przepisy ustawy stosuje się również do innych podmiotów.",
                "1a. (utracił moc)",
                "3. Ustawa wchodzi w życie.");

            Assert.Equal(
                ["Art. 1. 1. Ustawa reguluje:", "1) zasady dochodzenia roszczeń;", "2) (uchylony)",
                 "2. Przepisy ustawy stosuje się również do innych podmiotów.", "1a. (utracił moc)", "3. Ustawa wchodzi w życie."],
                blocks.Take(6).Select(b => b.Text));
        }

        [Fact]
        public void QuotedDesignationLine_IsSeparateFromItsTitle()
        {
            var blocks = ReadLines(
                "Art. 105. W ustawie po dziale IIIA dodaje się dział IIIB w brzmieniu:",
                "„Dział IIIB",
                "Realizacja działań finansowanych z udziałem środków Europejskiego Funduszu Społecznego Plus",
                "Art. 134n. 1. Minister właściwy do spraw zabezpieczenia społecznego pełni funkcję instytucji.”");

            Assert.Equal(
                ["„Dział IIIB", "Realizacja działań finansowanych z udziałem środków Europejskiego Funduszu Społecznego Plus"],
                blocks.Skip(1).Take(2).Select(b => b.Text));
        }

        [Fact]
        public void RepealNoteOnItsOwnLine_IsSeparateFromTheTitle()
        {
            var blocks = ReadLines(
                "Rozdział 2",
                "Skutki ogłoszenia upadłości co do zobowiązań upadłego",
                "(oznaczenie i tytuł oddziału 1 – uchylone)",
                "Art. 83. Postanowienia umowy są nieważne.");

            Assert.Equal(
                ["Rozdział 2", "Skutki ogłoszenia upadłości co do zobowiązań upadłego", "(oznaczenie i tytuł oddziału 1 – uchylone)"],
                blocks.Take(3).Select(b => b.Text));
        }

        [Fact]
        public void EnumerationUnderAnUppercaseTitle_StartsANewBlock()
        {
            var blocks = ReadLines(
                "DROGI KRAJOWE LUB ICH ODCINKI, NA KTÓRYCH POBIERA SIĘ OPŁATĘ ELEKTRONICZNĄ",
                "1) autostrada A1 na odcinku węzeł Toruń Południe – Gorzyczki;",
                "2) autostrada A2.");

            Assert.Equal("DROGI KRAJOWE LUB ICH ODCINKI, NA KTÓRYCH POBIERA SIĘ OPŁATĘ ELEKTRONICZNĄ", blocks[0].Text);
            Assert.StartsWith("1) autostrada A1", blocks[1].Text);
        }

        [Fact]
        public void ListItem_EndingWithAConjunction_IsClosed()
        {
            var blocks = ReadLines(
                "Art. 5. Rekompensata przysługuje pod warunkiem:",
                "1) zgłoszenia siebie jako płatnika składek oraz",
                "2) zgłoszenia co najmniej jednej osoby do ubezpieczeń.");

            Assert.Equal("1) zgłoszenia siebie jako płatnika składek oraz", blocks[1].Text);
            Assert.StartsWith("2) zgłoszenia", blocks[2].Text);
        }

        [Fact]
        public void ConjunctionAloneOnTheLastLineOfAnItem_ClosesIt()
        {
            var blocks = ReadLines(
                "Art. 89. 1. Kto nie powiadomił Prezesa Urzędu o:",
                "1) wstrzymaniu badania klinicznego lub nie przedstawił uzasadnienia,",
                "lub",
                "2) wstrzymaniu badania działania.");

            Assert.Equal("1) wstrzymaniu badania klinicznego lub nie przedstawił uzasadnienia, lub", blocks[1].Text);
            Assert.StartsWith("2) wstrzymaniu badania działania", blocks[2].Text);
        }

        [Fact]
        public void FootnoteZone_LetterMarker_BecomesFootnote()
        {
            // Tekst jednolity: odnośnik obwieszczenia oznaczony literą w indeksie górnym.
            var builder = new TestPdfBuilder().AddPage();
            WithBodyPadding(builder, fromY: 700);
            builder.AddText("a)", 57, 93.5, 6);
            builder.AddText("Zmiany tekstu jednolitego wymienionej ustawy zostały ogłoszone w Dz. U. poz. 5.", 66, 90, 10);

            var blocks = Read(builder.Build());

            var footnote = Assert.Single(blocks, b => b.Role == BlockRole.FootnoteText);
            Assert.StartsWith("[a)]", footnote.Text);
        }

        /// <summary>
        /// Strona składu z interlinią 13 pkt i odstępem akapitowym (akapity co 19 pkt): przed badanymi wierszami kilka
        /// trzywierszowych przepisów, żeby typowa interlinia była interlinią wewnątrz akapitu.
        /// </summary>
        private static TestPdfBuilder WithSpacedParagraphs(TestPdfBuilder builder, ref double y)
        {
            for (var n = 90; n < 96; n++)
            {
                builder.AddText($"Art. {n}. Przepis wypełniający stronę, którego zdanie biegnie", 57, y);
                builder.AddText("na drugi wiersz akapitu, a potem jeszcze", 57, y - 13);
                builder.AddText("na trzeci.", 57, y - 26);
                y -= 26 + 19;
            }

            return builder;
        }

        [Fact]
        public void ParagraphSpacing_SeparatesAParagraphWithoutADesignation()
        {
            // Część wspólna bez myślnika stoi za ostatnim podpunktem z odstępem akapitowym, nie z interlinią.
            var y = 760.0;
            var builder = WithSpacedParagraphs(new TestPdfBuilder().AddPage(), ref y);
            builder.AddText("7) ustawą z dnia 9 października 2025 r. o zmianie ustawy o zawodach pielęgniarki", 57, y);
            builder.AddText("oraz zmian wynikających z przepisów ogłoszonych przed dniem 3 grudnia 2025 r.", 57, y - 19);

            var blocks = Read(builder.Build());

            Assert.Equal(
                ["7) ustawą z dnia 9 października 2025 r. o zmianie ustawy o zawodach pielęgniarki",
                 "oraz zmian wynikających z przepisów ogłoszonych przed dniem 3 grudnia 2025 r."],
                blocks.TakeLast(2).Select(b => b.Text));
        }

        [Fact]
        public void AnnouncementCommonPart_StartsABlock()
        {
            var blocks = ReadLines(
                "1. Na podstawie art. 16 ustawy ogłasza się tekst jednolity, z uwzględnieniem zmian wprowadzonych:",
                "1) ustawą z dnia 21 lutego 2025 r. o zmianie ustawy o biokomponentach (Dz. U. poz. 303)",
                "oraz zmian wynikających z przepisów ogłoszonych przed dniem 7 października 2025 r.",
                "2. Podany w załączniku tekst jednolity nie obejmuje art. 5.");

            Assert.Equal("oraz zmian wynikających z przepisów ogłoszonych przed dniem 7 października 2025 r.", blocks[2].Text);
        }

        [Theory]
        [InlineData("3) (uchylony)", "4) prowadzenie urzędowego sprawdzenia;")]
        [InlineData("2. Dane są udostępniane wyłącznie:", "1) organom Krajowej Administracji Skarbowej;")]
        public void ClosedLineWithAFootnoteAnchor_EndsBeforeTheNextUnit(string closed, string next)
        {
            // Tekst jednolity: odwołanie do odnośnika stoi za znakiem domykającym wiersz.
            var builder = new TestPdfBuilder().AddPage();
            WithBodyPadding(builder, fromY: 600);
            builder.AddText("Art. 1. Do zadań organu należy:", 57, 714);
            builder.AddText(closed, 57, 700);
            builder.AddText("19)", 250, 703.5, 6);
            builder.AddText(next, 57, 686);

            var blocks = Read(builder.Build());

            Assert.Contains(blocks, b => b.Text.StartsWith(closed) && b.Text.EndsWith("[19)]"));
            Assert.Contains(blocks, b => b.Text == next);
        }

        [Fact]
        public void BorderedTable_ReadsCellsIntoTheirPlaces()
        {
            // Tabela z obramowaniem: wiersz dwóch komórek, w drugiej komórce dwa wiersze tekstu; kolumny w jednym
            // wierszu strony nie mieszają się, a tekst za tabelą jest osobnym blokiem.
            var builder = new TestPdfBuilder().AddPage();
            WithBodyPadding(builder, fromY: 400);
            builder.AddText("Art. 1. Stawki określa tabela:", 57, 720);
            builder.AddGrid([705, 685, 645], [57, 200, 500]);
            builder.AddText("Lp.", 60, 692);
            builder.AddText("Nazwa stawki", 205, 692);
            builder.AddText("1", 60, 670);
            builder.AddText("stawka podstawowa dla", 205, 670);
            builder.AddText("przedsiębiorców", 205, 656);
            builder.AddText("Art. 2. Rozporządzenie wchodzi w życie.", 57, 620);

            var blocks = Read(builder.Build());

            var cells = blocks.Where(b => b.TableCell is not null).ToList();
            Assert.Equal(
                [(0, 0, "Lp."), (0, 1, "Nazwa stawki"), (1, 0, "1"), (1, 1, "stawka podstawowa dla przedsiębiorców")],
                cells.Select(b => (b.TableCell!.Row, b.TableCell.Column, b.Text)));
            Assert.Contains(blocks, b => b.TableCell is null && b.Text == "Art. 1. Stawki określa tabela:");
            Assert.Contains(blocks, b => b.TableCell is null && b.Text == "Art. 2. Rozporządzenie wchodzi w życie.");
        }

        [Fact]
        public void BorderedTable_MissingSeparatorMergesCells()
        {
            // Nagłówek na całą szerokość: linia pionowa między kolumnami zaczyna się dopiero pod nim; pusta komórka
            // też jest komórką.
            var builder = new TestPdfBuilder().AddPage();
            WithBodyPadding(builder, fromY: 400);
            builder.AddText("Art. 1. Wykaz:", 57, 720);
            foreach (var y in new double[] { 705, 685, 665 })
                builder.AddLine(57, y, 500, y);
            builder.AddLine(57, 705, 57, 665);
            builder.AddLine(500, 705, 500, 665);
            builder.AddLine(200, 685, 200, 665);
            builder.AddText("WYKAZ STAWEK", 220, 692);
            builder.AddText("opłata", 60, 672);

            var blocks = Read(builder.Build());

            var cells = blocks.Where(b => b.TableCell is not null).ToList();
            Assert.Equal(
                [(0, 0, 2, "WYKAZ STAWEK"), (1, 0, 1, "opłata"), (1, 1, 1, "")],
                cells.Select(b => (b.TableCell!.Row, b.TableCell.Column, b.TableCell.ColumnSpan, b.Text)));
        }

        [Fact]
        public void BorderedTable_ColumnLineDrawnPerCell_SeparatesCellsMergedDownwards()
        {
            // M.P. 2026 poz. 948: edytor rysuje obramowanie komórkami, więc linia między dwiema kolumnami scalonymi
            // w pionie składa się z kawałków; środkowy nie styka się z żadną linią wiersza. Bez sklejenia kawałków
            // w jedną linię wypadał z obramowania i „Nazwa zadania” zlewała się z „Cel” i „Jednostka”.
            var builder = new TestPdfBuilder().AddPage();
            WithBodyPadding(builder, fromY: 400);
            builder.AddText("§ 1. Mierniki programu:", 57, 720);
            double[] columns = [57, 150, 250, 350, 500], rows = [705, 685, 665, 645, 625];
            foreach (var x in columns)
                for (var r = 0; r + 1 < rows.Length; r++)
                    builder.AddLine(x, rows[r], x, rows[r + 1]);
            foreach (var y in new double[] { 705, 645, 625 })
                builder.AddLine(57, y, 500, y);
            builder.AddLine(57, 685, 150, 685);
            builder.AddLine(350, 685, 500, 685);
            builder.AddLine(350, 665, 500, 665);
            builder.AddText("Część", 60, 692);
            builder.AddText("Nazwa zadania", 155, 692);
            builder.AddText("Cel", 255, 692);
            builder.AddText("Miernik", 355, 692);
            builder.AddText("Jednostka", 60, 672);
            builder.AddText("Wartość", 355, 672);
            builder.AddText("Bazowa", 355, 652);
            builder.AddText("1", 60, 632);
            builder.AddText("2", 155, 632);
            builder.AddText("3", 255, 632);
            builder.AddText("4", 355, 632);

            var blocks = Read(builder.Build());

            Assert.Equal(
                [(0, 0, 1, "Część"), (0, 1, 3, "Nazwa zadania"), (0, 2, 3, "Cel"), (0, 3, 1, "Miernik"), (1, 0, 2, "Jednostka"),
                    (1, 3, 1, "Wartość"), (2, 3, 1, "Bazowa"), (3, 0, 1, "1"), (3, 1, 1, "2"), (3, 2, 1, "3"), (3, 3, 1, "4")],
                blocks.Where(b => b.TableCell is not null).Select(b => (b.TableCell!.Row, b.TableCell.Column, b.TableCell.RowSpan, b.Text)));
        }

        [Theory]
        // Tabela przecięta stroną: na następnej stronie te same kolumny, bez tekstu przed nimi. Edytor dopasowuje
        // szerokości kolumn na każdej stronie osobno — linie rozjeżdżają się o kilka punktów (M.P. 2026 poz. 948).
        [InlineData(57, 200, 500)]
        [InlineData(55, 203, 504)]
        public void BorderedTableContinuedOnTheNextPage_IsOneTable(double left, double middle, double right)
        {
            var builder = new TestPdfBuilder().AddPage();
            WithBodyPadding(builder, fromY: 700);
            builder.AddText("§ 1. Stawki:", 57, 600);
            builder.AddGrid([585, 565], [57, 200, 500]);
            builder.AddText("1", 60, 572);
            builder.AddText("stawka pierwsza", 205, 572);
            builder.AddPage();
            builder.AddGrid([800, 780], [left, middle, right]);
            builder.AddText("2", 60, 787);
            builder.AddText("stawka druga", 208, 787);
            WithBodyPadding(builder, fromY: 700);

            var blocks = Read(builder.Build());

            var cells = blocks.Where(b => b.TableCell is not null).ToList();
            Assert.Equal(
                [(0, 0, "1"), (0, 1, "stawka pierwsza"), (1, 0, "2"), (1, 1, "stawka druga")],
                cells.Select(b => (b.TableCell!.Row, b.TableCell.Column, b.Text)));
            Assert.All(cells, b => Assert.Equal(0, b.TableCell!.Table));
        }

        [Fact]
        public void BorderedTableCell_NarrowerThanAWord_JoinsTheBrokenWordAndNumber()
        {
            // M.P. 2026 poz. 948: w kolumnie na jeden znak edytor stawia literę pod literą, a liczba przechodzi do
            // następnego wiersza za przecinkiem dziesiętnym; przedział lat łamie się za półpauzą.
            var builder = new TestPdfBuilder().AddPage();
            WithBodyPadding(builder, fromY: 400);
            builder.AddText("§ 1. Mierniki:", 57, 720);
            builder.AddGrid([705, 640], [57, 70, 200, 400, 500]);
            var y = 697.0;
            foreach (var letter in "Bazowa")
            {
                builder.AddText(letter.ToString(), 59, y, 8);
                y -= 9;
            }

            builder.AddText("4 761 777,", 75, 697, 8);
            builder.AddText("307", 75, 688, 8);
            builder.AddText("2023–", 205, 697, 8);
            builder.AddText("2029 r.", 205, 688, 8);
            builder.AddText("W", 405, 697, 8);
            builder.AddText("ramach", 405, 688, 8);
            builder.AddText("zadania", 405, 679, 8);

            var blocks = Read(builder.Build());

            Assert.Equal(["Bazowa", "4 761 777,307", "2023–2029 r.", "W ramach zadania"],
                blocks.Where(b => b.TableCell is not null).Select(b => b.Text));
        }

        [Fact]
        public void NumberRange_BrokenAfterTheEnDash_JoinsWithoutASpace()
        {
            var pdf = new TestPdfBuilder().AddPage()
                .AddText("Art. 1. Program realizuje się w latach 2023–", 57, 700)
                .AddText("2029 na obszarze całego kraju.", 57, 686)
                .Build();

            var blocks = Read(pdf);

            Assert.Contains("w latach 2023–2029 na obszarze", blocks[0].Text);
        }

        [Fact]
        public void BorderedTableOnTheNextPage_WithOtherColumns_IsAnotherTable()
        {
            var builder = new TestPdfBuilder().AddPage();
            WithBodyPadding(builder, fromY: 700);
            builder.AddText("§ 1. Stawki:", 57, 600);
            builder.AddGrid([585, 565], [57, 200, 500]);
            builder.AddText("1", 60, 572);
            builder.AddText("stawka pierwsza", 205, 572);
            builder.AddPage();
            builder.AddGrid([800, 780], [57, 300, 500]);
            builder.AddText("Kod", 60, 787);
            builder.AddText("Opis", 305, 787);
            WithBodyPadding(builder, fromY: 700);

            var blocks = Read(builder.Build());

            Assert.Equal([0, 0, 1, 1], blocks.Where(b => b.TableCell is not null).Select(b => b.TableCell!.Table));
        }

        [Fact]
        public void BorderedTableCell_WithAShortNumberAndAnAnchor_KeepsTheAnchorSuperscript()
        {
            // Komórka „2" z odwołaniem do odnośnika: liter indeksu górnego więcej niż liter tekstu.
            var builder = new TestPdfBuilder().AddPage();
            WithBodyPadding(builder, fromY: 400);
            builder.AddText("§ 1. Wykaz podmiotów:", 57, 720);
            builder.AddGrid([705, 685], [57, 97, 500]);
            builder.AddText("2", 60, 692);
            builder.AddText("10)", 67, 695.5, 6);
            builder.AddText("Centrum Badań i Dozoru", 100, 692);

            var blocks = Read(builder.Build());

            Assert.Contains(blocks, b => b.TableCell is { Column: 0 } && b.Text == "2[10)]");
        }

        [Fact]
        public void FootnoteReferencedOnlyFromATableCell_IsAFootnote()
        {
            // Odnośnik oznaczony zwykłym tekstem („6)Kody …") — jedyne odwołanie do niego stoi w nagłówku tabeli.
            var builder = new TestPdfBuilder().AddPage();
            WithBodyPadding(builder, fromY: 400);
            builder.AddText("§ 1. Wykaz siedlisk:", 57, 720);
            builder.AddGrid([705, 685], [57, 97, 500]);
            builder.AddText("Kod", 60, 692);
            builder.AddText("6)", 79, 695.5, 6);
            builder.AddText("Nazwa siedliska", 100, 692);
            builder.AddText("6)Kody siedlisk są zgodne z rozporządzeniem Ministra Środowiska.", 57, 90, 10);

            var blocks = Read(builder.Build());

            var referencing = Assert.Single(blocks, b => b.Footnotes.Count > 0);
            Assert.NotNull(referencing.TableCell);
            Assert.Equal(["Kody siedlisk są zgodne z rozporządzeniem Ministra Środowiska."], Assert.Single(referencing.Footnotes).Paragraphs);
        }

        [Fact]
        public void TableRowsAtParagraphSpacing_StayTogether()
        {
            // Wiersze tabeli przytoczonej stoją co odstęp akapitowy; rozdzielone dawałyby z „1. … 70" jednostki.
            var y = 760.0;
            var builder = WithSpacedParagraphs(new TestPdfBuilder().AddPage(), ref y);
            builder.AddText("14) tabela nr 2 otrzymuje brzmienie: Tabela nr 2", 57, y);
            builder.AddText("1. Poziom naukowy prowadzonej działalności", 57, y - 19);
            builder.AddText("70", 400, y - 19);
            builder.AddText("2. Efekty finansowe badań naukowych", 57, y - 38);
            builder.AddText("10", 400, y - 38);

            var blocks = Read(builder.Build());

            Assert.Contains(blocks, b => b.Text.Contains("Tabela nr 2 1. Poziom naukowy prowadzonej działalności 70 2. Efekty"));
        }

        [Fact]
        public void ScannedPdf_NoTextLayer_ThrowsScannedPdfException()
        {
            var pdf = new TestPdfBuilder()
                .AddPage().AddText("x", 57, 700)
                .AddPage()
                .Build();

            using var ms = new MemoryStream(pdf);
            var ex = Assert.Throws<ScannedPdfException>(() => new PdfBlockReader().ReadBlocks(ms));
            Assert.Contains("skan", ex.Message);
        }

        [Fact]
        public void Indents_TiretLines_CarryLeftIndentTwips()
        {
            var pdf = WithBodyPadding(new TestPdfBuilder().AddPage()
                .AddText("Art. 1. Wyliczenie obejmuje:", 57, 700)
                .AddText("1) punkt z literami:", 57, 686)
                .AddText("a) litera z tiretami:", 71, 672)
                .AddText("– tiret pierwszy,", 85, 658)
                .AddText("– tiret drugi.", 85, 644))
                .Build();

            var blocks = Read(pdf);

            var tiret = blocks.First(b => b.Text.StartsWith("– tiret pierwszy"));
            // 85pt - 57pt (dominujący lewy margines) = 28pt = 560 twips.
            Assert.Equal(560, tiret.Layout?.LeftIndentTwips);

            var article = blocks.First(b => b.Text.StartsWith("Art. 1."));
            Assert.Equal(0, article.Layout?.LeftIndentTwips);
        }

        // ============================================================
        // Regresje z przeglądu adwersaryjnego Etapu 9
        // ============================================================

        [Fact]
        public void HangingIndentContinuation_JoinsIntoUnitBlock()
        {
            // KRYTYCZNE z przeglądu: skład DU używa wcięcia WISZĄCEGO — kontynuacja zawiniętego
            // punktu zaczyna się GŁĘBIEJ niż marker; granica wcięciowa cięłaby ją i gubiła tekst.
            var pdf = WithBodyPadding(new TestPdfBuilder().AddPage()
                .AddText("Art. 1. Wyliczenie obejmuje:", 57, 700)
                .AddText("1) punkt pierwszy o treści dłuższej, która", 57, 686)
                .AddText("zawija się do wcięcia wiszącego pod tekstem;", 71, 672)
                .AddText("2) punkt drugi.", 57, 658))
                .Build();

            var blocks = Read(pdf);

            var point = blocks.Single(b => b.Text.StartsWith("1)"));
            Assert.Equal("1) punkt pierwszy o treści dłuższej, która zawija się do wcięcia wiszącego pod tekstem;",
                point.Text);
        }

        [Fact]
        public void FootnoteZone_FontCloseToTheBody_WithSuperscriptMarker_BecomesFootnote()
        {
            // Skład Dziennika Ustaw: odnośnik 0,9× treści, znacznik w indeksie górnym.
            var builder = new TestPdfBuilder().AddPage();
            WithBodyPadding(builder, fromY: 700);
            builder.AddText("1)", 57, 93.5, 6);
            builder.AddText("Niniejszą ustawą zmienia się ustawy wymienione w wykazie.", 66, 90, 10);

            var blocks = Read(builder.Build());

            var footnote = Assert.Single(blocks, b => b.Role == BlockRole.FootnoteText);
            Assert.StartsWith("[1)]", footnote.Text);
            Assert.DoesNotContain(blocks, b => b.Role == BlockRole.Body && b.Text.Contains("Niniejszą ustawą"));
        }

        [Fact]
        public void PageSetCloseToTheBodySize_AfterAFinishedFootnote_StaysBody()
        {
            // Strona załącznika złożona w całości fontem 0,9× (tabela) po stronie z zakończonym odnośnikiem.
            var builder = new TestPdfBuilder().AddPage();
            WithBodyPadding(builder, fromY: 700);
            builder.AddText("1)", 57, 93.5, 6);
            builder.AddText("Niniejszą ustawą zmienia się ustawy wymienione w wykazie.", 66, 90, 10);
            builder.AddPage();
            for (int i = 0; i < 6; i++)
                builder.AddText($"Pozycja {i + 1} wykazu miast w tabeli załącznika", 57, 700 - i * 14, 10);

            var blocks = Read(builder.Build());

            Assert.Single(blocks, b => b.Role == BlockRole.FootnoteText);
            Assert.Contains(blocks, b => b.Role == BlockRole.Body && b.Text.Contains("Pozycja 6 wykazu"));
        }

        [Fact]
        public void FootnoteCloseToTheBodySize_CutMidSentence_ContinuesOnTheNextPage()
        {
            var builder = new TestPdfBuilder().AddPage();
            WithBodyPadding(builder, fromY: 700);
            builder.AddText("1)", 57, 93.5, 6);
            builder.AddText("Zmiany tekstu jednolitego wymienionej ustawy zostały ogłoszone", 66, 90, 10);
            builder.AddPage();
            WithBodyPadding(builder, fromY: 700);
            builder.AddText("w Dz. U. z 2023 r. poz. 100 oraz z 2024 r. poz. 5.", 66, 90, 10);

            var blocks = Read(builder.Build());

            var footnote = Assert.Single(blocks, b => b.Role == BlockRole.FootnoteText);
            Assert.Contains("ogłoszone w Dz. U. z 2023 r.", footnote.Text);
        }

        [Fact]
        public void FootnoteNumberedInPlainText_ReferencedOnItsPage_BecomesFootnote()
        {
            // Odnośnik 0,9× treści z numerem bez indeksu górnego; treść strony ma odwołanie „[3)]".
            var builder = new TestPdfBuilder().AddPage();
            WithBodyPadding(builder, fromY: 600);
            builder.AddText("Art. 1. Laboratoria, o których mowa w rozporządzeniu 2017/625", 57, 700);
            builder.AddText("3)", 400, 703.5, 6);
            builder.AddText("3) Rozporządzenie Parlamentu Europejskiego i Rady (UE) 2017/625.", 57, 90, 10);

            var blocks = Read(builder.Build());

            var footnote = Assert.Single(blocks, b => b.Role == BlockRole.FootnoteText);
            Assert.StartsWith("3) Rozporządzenie", footnote.Text);
            Assert.Equal("3", Assert.Single(blocks.SelectMany(b => b.Footnotes)).Symbol);
        }

        [Fact]
        public void FontCloseToTheBody_WithoutSuperscriptMarker_StaysBody()
        {
            // „N)" bez indeksu górnego, niewiele mniejszym fontem — wiersz tabeli albo wyliczenia, nie przypis.
            var builder = new TestPdfBuilder().AddPage();
            WithBodyPadding(builder, fromY: 700);
            builder.AddText("1) wiersz tabeli załącznika pisany mniejszym fontem;", 57, 90, 10);

            var blocks = Read(builder.Build());

            Assert.DoesNotContain(blocks, b => b.Role == BlockRole.FootnoteText);
            Assert.Contains(blocks, b => b.Role == BlockRole.Body && b.Text.StartsWith("1) wiersz tabeli"));
        }

        /// <summary>
        /// Strona z przepisem, którego zdanie kończy znacznik odnośnika w indeksie górnym, i z odnośnikiem na dole strony.
        /// </summary>
        private static TestPdfBuilder AddReferencingPage(TestPdfBuilder builder, string provision, string symbol, string? footnote)
        {
            builder.AddPage();
            WithBodyPadding(builder, fromY: 600);
            builder.AddText(provision, 57, 700);
            builder.AddText(symbol + ")", 330, 703.5, 6);
            if (footnote is not null)
            {
                builder.AddText(symbol + ")", 57, 93.5, 6);
                builder.AddText(footnote, 66, 90, 8);
            }

            return builder;
        }

        [Fact]
        public void Footnote_IsAttachedToTheBlockWithItsMarker()
        {
            var builder = AddReferencingPage(new TestPdfBuilder(), "Art. 1. Ustawa zmienia ustawy wymienione", "1",
                "Niniejszą ustawą zmienia się ustawę o podatku.");

            var blocks = Read(builder.Build());

            var referencing = Assert.Single(blocks, b => b.Footnotes.Count > 0);
            Assert.StartsWith("Art. 1.", referencing.Text);
            Assert.Contains("[1)]", referencing.Text);
            var footnote = Assert.Single(referencing.Footnotes);
            Assert.Equal(("1", "1", 0), (footnote.Symbol, footnote.Marker, footnote.Run));
            Assert.Equal(["Niniejszą ustawą zmienia się ustawę o podatku."], footnote.Paragraphs);
        }

        [Fact]
        public void LetterFootnotes_ThenNumbered_AreSeparateRunsOfNumbering()
        {
            // Tekst jednolity: odnośniki obwieszczenia literami, ogłaszanego aktu — liczbami.
            var builder = AddReferencingPage(new TestPdfBuilder(), "Art. 1. Obwieszczenie ogłasza tekst jednolity", "a",
                "Zmiany tekstu jednolitego ogłoszono w Dz. U. poz. 5.");
            AddReferencingPage(builder, "Art. 2. Ustawa wdraża dyrektywę Unii Europejskiej", "1",
                "Niniejsza ustawa wdraża dyrektywę 2008/50/WE.");

            var blocks = Read(builder.Build());

            var footnotes = blocks.SelectMany(b => b.Footnotes).ToList();
            Assert.Equal([("a", "1", 0), ("1", "100001", 1)], footnotes.Select(f => (f.Symbol, f.Marker, f.Run)));
            Assert.Contains(blocks, b => b.Text.StartsWith("Art. 2.") && b.Text.Contains("[100001)]"));
        }

        [Fact]
        public void FootnoteWithoutItsNumberInTheTextLayer_IsLinkedToTheMarkerOnItsPage()
        {
            // Warstwa tekstowa zapisała z oznaczenia odnośnika tylko nawias: „[)] Artykuł 24 zawiera zmiany …".
            var builder = AddReferencingPage(new TestPdfBuilder(), "Art. 30. Ustawa zmieniana w art. 24", "4", footnote: null);
            builder.AddText(")", 57, 93.5, 6);
            builder.AddText("Artykuł 24 zawiera zmiany do ustawy o mieszkalnictwie.", 66, 90, 8);

            var blocks = Read(builder.Build());

            var footnote = Assert.Single(blocks.SelectMany(b => b.Footnotes));
            Assert.Equal("4", footnote.Symbol);
            Assert.Equal(["Artykuł 24 zawiera zmiany do ustawy o mieszkalnictwie."], footnote.Paragraphs);
        }

        [Fact]
        public void PointWithAFootnoteAnchorRightAfterItsNumber_StartsABlock()
        {
            // Tekst jednolity: „1)" z odwołaniem do odnośnika w indeksie górnym tuż za numerem („1)[)]").
            var builder = new TestPdfBuilder().AddPage();
            WithBodyPadding(builder, fromY: 600);
            builder.AddText("Art. 2. Ilekroć w niniejszej ustawie jest mowa o:", 57, 700);
            builder.AddText("1)", 57, 686);
            builder.AddText(")", 66.5, 689.5, 6);
            builder.AddText("badaniu ustawowym – należy przez to rozumieć badanie sprawozdania;", 72, 686);

            var blocks = Read(builder.Build());

            Assert.Contains(blocks, b => b.Text == "Art. 2. Ilekroć w niniejszej ustawie jest mowa o:");
            Assert.Contains(blocks, b => b.Text.StartsWith("1) badaniu ustawowym"));
        }

        [Fact]
        public void UnnumberedReference_IsLinkedToTheUnnumberedFootnoteOnItsPage()
        {
            // Warstwa tekstowa zgubiła numer i w odwołaniu („[)]"), i w odnośniku („[)] …").
            var builder = AddReferencingPage(new TestPdfBuilder(), "Art. 30. Ustawa zmieniana w art. 24", "", footnote: null);
            builder.AddText(")", 57, 93.5, 6);
            builder.AddText("Artykuł 24 zawiera zmiany do ustawy o mieszkalnictwie.", 66, 90, 8);

            var blocks = Read(builder.Build());

            var referencing = Assert.Single(blocks, b => b.Footnotes.Count > 0);
            Assert.Contains("[1)]", referencing.Text);
            var footnote = Assert.Single(referencing.Footnotes);
            Assert.Equal(("1", "1"), (footnote.Symbol, footnote.Marker));
            Assert.Equal(["Artykuł 24 zawiera zmiany do ustawy o mieszkalnictwie."], footnote.Paragraphs);
        }

        [Fact]
        public void FootnoteNumberWithOnlyTheDigitInSuperscript_IsLinked()
        {
            // Szablon przypisu: numer w indeksie górnym, nawias zwykłym tekstem — „[1]) Minister …".
            var builder = AddReferencingPage(new TestPdfBuilder(), "Art. 1. Minister ustala wzory dokumentów", "1", footnote: null);
            builder.AddText("1", 57, 93.5, 6);
            builder.AddText(") Minister Infrastruktury kieruje działem transport.", 60.5, 90, 8);

            var blocks = Read(builder.Build());

            var footnote = Assert.Single(blocks.SelectMany(b => b.Footnotes));
            Assert.Equal("1", footnote.Symbol);
            Assert.Equal(["Minister Infrastruktury kieruje działem transport."], footnote.Paragraphs);
        }

        [Fact]
        public void FootnoteNumberWithOnlyTheParenthesisInSuperscript_IsLinked()
        {
            // Odwrotnie: numer zwykłym tekstem, nawias w indeksie górnym — „1[)] Minister …", font 0,9× treści.
            var builder = AddReferencingPage(new TestPdfBuilder(), "Art. 1. Minister ustala wzory dokumentów", "1", footnote: null);
            builder.AddText("1", 57, 90, 10);
            builder.AddText(")", 62.6, 93.5, 6);
            builder.AddText("Minister Infrastruktury kieruje działem transport.", 68, 90, 10);

            var blocks = Read(builder.Build());

            var footnote = Assert.Single(blocks.SelectMany(b => b.Footnotes));
            Assert.Equal("1", footnote.Symbol);
            Assert.Equal(["Minister Infrastruktury kieruje działem transport."], footnote.Paragraphs);
            Assert.DoesNotContain(blocks, b => b.Role == BlockRole.Body && b.Text.Contains("Minister Infrastruktury"));
        }

        [Fact]
        public void FootnoteWithAnEnumeration_KeepsItsItemsAsParagraphs()
        {
            // Odnośnik tekstu jednolitego z wyliczeniem — pozycje „1)", „2)" zwykłym tekstem są jego akapitami,
            // nie odnośnikami 1 i 2.
            var builder = AddReferencingPage(new TestPdfBuilder(), "Art. 80. Ewidencję prowadzi starosta", "7", footnote: null);
            builder.AddText("7)", 57, 113.5, 6);
            builder.AddText("Dodany przez art. 5 ustawy; wejdzie w życie z dniem wdrożenia rejestrów, o których mowa:", 66, 110, 8);
            builder.AddText("1) w art. 28 ust. 1 ustawy;", 66, 100, 8);
            builder.AddText("2) w art. 39g ust. 3 ustawy o transporcie drogowym.", 66, 90, 8);

            var blocks = Read(builder.Build());

            var footnote = Assert.Single(blocks.SelectMany(b => b.Footnotes));
            Assert.Equal("7", footnote.Symbol);
            Assert.Equal(
                ["Dodany przez art. 5 ustawy; wejdzie w życie z dniem wdrożenia rejestrów, o których mowa:",
                 "1) w art. 28 ust. 1 ustawy;",
                 "2) w art. 39g ust. 3 ustawy o transporcie drogowym."],
                footnote.Paragraphs);
        }

        [Fact]
        public void FootnoteNumberPrintedTwice_IsLinkedToItsReference()
        {
            // Automatyczny numer odnośnika i ten sam numer wpisany ręcznie: odnośnik 1 drukuje się „11)".
            var builder = AddReferencingPage(new TestPdfBuilder(), "Art. 1. Minister ustala wzory dokumentów", "1", footnote: null);
            builder.AddText("11)", 57, 93.5, 6);
            builder.AddText("Minister Rolnictwa i Rozwoju Wsi kieruje działem rozwój wsi.", 66, 90, 8);

            var blocks = Read(builder.Build());

            var footnote = Assert.Single(blocks.SelectMany(b => b.Footnotes));
            Assert.Equal("1", footnote.Symbol);
            Assert.Equal(["Minister Rolnictwa i Rozwoju Wsi kieruje działem rozwój wsi."], footnote.Paragraphs);
        }

        [Fact]
        public void FootnoteMarkPrintedTwice_IsReadOnce()
        {
            // Oznaczenie własne odnośnika („zc)") w odwołaniu: indeks górny i ten sam tekst w linii pisma;
            // w odnośniku: dwa razy w indeksie górnym.
            var builder = new TestPdfBuilder().AddPage();
            WithBodyPadding(builder, fromY: 600);
            builder.AddText("Art. 1. Rozporządzenie (UE) 2017/745, z późn. zm.", 57, 700);
            builder.AddText("zc)", 340, 703.5, 6);
            builder.AddText("zc)), stosuje się.", 349, 700);
            builder.AddText("zc)zc)", 57, 93.5, 6);
            builder.AddText("Zmiany wymienionego rozporządzenia zostały ogłoszone w Dz. Urz. UE.", 80, 90, 8);

            var blocks = Read(builder.Build());

            var referencing = Assert.Single(blocks, b => b.Footnotes.Count > 0);
            Assert.DoesNotContain("zc)", referencing.Text);
            var footnote = Assert.Single(referencing.Footnotes);
            Assert.Equal("zc", footnote.Symbol);
            Assert.Equal(["Zmiany wymienionego rozporządzenia zostały ogłoszone w Dz. Urz. UE."], footnote.Paragraphs);
        }

        [Fact]
        public void UnnumberedReferenceWithAComma_KeepsTheComma()
        {
            // Kilka odwołań w jednym indeksie górnym („1), 2)"): pierwszy stracił numer, został „[),]".
            var builder = new TestPdfBuilder().AddPage();
            WithBodyPadding(builder, fromY: 600);
            builder.AddText("Art. 30. Ustawa zmieniana w art. 24", 57, 700);
            builder.AddText("),", 330, 703.5, 6);
            builder.AddText(")", 57, 93.5, 6);
            builder.AddText("Artykuł 24 zawiera zmiany do ustawy o mieszkalnictwie.", 66, 90, 8);

            var blocks = Read(builder.Build());

            var referencing = Assert.Single(blocks, b => b.Footnotes.Count > 0);
            Assert.Contains("[1),]", referencing.Text);
            Assert.Equal(["Artykuł 24 zawiera zmiany do ustawy o mieszkalnictwie."], Assert.Single(referencing.Footnotes).Paragraphs);
        }

        [Fact]
        public void FootnotesNumberedInPlainText_StaySeparateFootnotes()
        {
            // Odnośniki oznaczone zwykłym tekstem: następny zaczyna się wielką literą po odnośniku zakończonym kropką.
            var builder = new TestPdfBuilder().AddPage();
            WithBodyPadding(builder, fromY: 600);
            builder.AddText("Art. 1. Minister ustala wzory", 57, 700);
            builder.AddText("1)", 300, 703.5, 6);
            builder.AddText("Art. 2. Przepisy wdrażają dyrektywę", 57, 680);
            builder.AddText("2)", 300, 683.5, 6);
            builder.AddText("1) Minister Infrastruktury kieruje działem administracji rządowej.", 57, 100, 10);
            builder.AddText("2) Niniejsze rozporządzenie wdraża dyrektywę 2006/126/WE.", 57, 88, 10);

            var blocks = Read(builder.Build());

            var footnotes = blocks.SelectMany(b => b.Footnotes).ToList();
            Assert.Equal(["1", "2"], footnotes.Select(f => f.Symbol));
            Assert.Equal(["Niniejsze rozporządzenie wdraża dyrektywę 2006/126/WE."], footnotes[1].Paragraphs);
        }

        [Fact]
        public void MarkerWithoutAFootnoteOnItsPage_StaysAsPrinted()
        {
            // Powtórzone odwołanie na dalszej stronie: odnośnik stoi tylko przy pierwszym.
            var builder = AddReferencingPage(new TestPdfBuilder(), "Art. 1. Ustawa zmienia ustawy wymienione", "1",
                "Niniejszą ustawą zmienia się ustawę o podatku.");
            AddReferencingPage(builder, "Art. 2. Ustawa zmieniana w art. 1 wchodzi", "1", footnote: null);

            var blocks = Read(builder.Build());

            Assert.Single(blocks, b => b.Footnotes.Count > 0);
            var repeated = Assert.Single(blocks, b => b.Text.StartsWith("Art. 2."));
            Assert.Contains("[1)]", repeated.Text);
            Assert.Empty(repeated.Footnotes);
        }

        [Fact]
        public void FootnoteContinuedOnNextPage_StaysInFootnoteZone()
        {
            // Odnośnik wykazu zmian TJ regularnie zajmuje 2+ stron; kontynuacja zaczyna się
            // środkiem zdania (bez „N)") i nie może wpaść do treści aktu.
            var builder = new TestPdfBuilder();
            builder.AddPage();
            WithBodyPadding(builder, fromY: 700);
            builder.AddText("1) Zmiany tekstu jednolitego wymienionej ustawy ogłoszone zostały", 57, 90, 8);
            builder.AddPage();
            WithBodyPadding(builder, fromY: 700);
            builder.AddText("w Dz. U. z 2023 r. poz. 100 oraz z 2024 r. poz. 5.", 57, 90, 8);

            var blocks = Read(builder.Build());

            var footnote = Assert.Single(blocks, b => b.Role == BlockRole.FootnoteText);
            Assert.Contains("ogłoszone zostały w Dz. U. z 2023 r.", footnote.Text);
            Assert.DoesNotContain(blocks, b => b.Role == BlockRole.Body && b.Text.StartsWith("w Dz. U."));
        }

        [Fact]
        public void WrappedJournalReference_DoesNotBecomeFalsePointBoundary()
        {
            // Zawinięcie wewnątrz „(Dz. U. poz. ⏎ 1234) wprowadza się…" nie może rozciąć zdania
            // (fragment „1234)…" wyglądałby jak punkt 1234).
            var pdf = WithBodyPadding(new TestPdfBuilder().AddPage()
                .AddText("Art. 1. W ustawie z dnia 5 maja 2020 r. (Dz. U. poz.", 57, 700)
                .AddText("1234) wprowadza się następujące zmiany:", 57, 686))
                .Build();

            var blocks = Read(pdf);

            var intro = blocks.Single(b => b.Text.StartsWith("Art. 1."));
            Assert.Contains("(Dz. U. poz. 1234) wprowadza się", intro.Text);
            Assert.DoesNotContain(blocks, b => b.Text.StartsWith("1234)"));
        }

        [Fact]
        public void WrappedQuotedWording_AfterOpenPhrase_DoesNotSplit()
        {
            // Komenda zamiany wyrazów zawinięta przed cytowanym brzmieniem: poprzedni wiersz kończy
            // się środkiem frazy (bez interpunkcji) → „ nie otwiera nowego bloku.
            var pdf = WithBodyPadding(new TestPdfBuilder().AddPage()
                .AddText("1) w art. 3 wyrazy „dom pomocy” zastępuje się wyrazami", 57, 700)
                .AddText("„centrum usług społecznych”;", 57, 686))
                .Build();

            var blocks = Read(pdf);

            var command = blocks.Single(b => b.Text.StartsWith("1)"));
            Assert.Contains("zastępuje się wyrazami „centrum usług społecznych”;", command.Text);
        }

        [Fact]
        public void QuotedContentAfterColon_StillStartsNewBlock()
        {
            // Kanoniczny cytat po „brzmienie:" — poprzedni wiersz domknięty dwukropkiem → „ otwiera blok.
            var pdf = WithBodyPadding(new TestPdfBuilder().AddPage()
                .AddText("1) art. 5 otrzymuje brzmienie:", 57, 700)
                .AddText("„Art. 5. Nowe brzmienie.”;", 57, 686))
                .Build();

            var blocks = Read(pdf);

            Assert.Contains(blocks, b => b.Text.StartsWith("„Art. 5."));
        }

        [Fact]
        public void BareNumberInMarginZone_SinglePage_IsKept()
        {
            // Goła liczba (komórka tabeli załącznika) na dole strony NIE może zostać skasowana
            // pojedynczo; kasowana jest tylko jednoznaczna paginacja „– N –".
            var builder = new TestPdfBuilder().AddPage();
            WithBodyPadding(builder, fromY: 700);
            builder.AddText("500", 57, 30);
            builder.AddText("– 5 –", 280, 20, 9);

            var blocks = Read(builder.Build());

            Assert.Contains(blocks, b => b.Text == "500");
            Assert.DoesNotContain(blocks, b => b.Text.Contains("– 5 –"));
        }

        [Fact]
        public void NumericAnnexRows_AcrossPages_AreNotMergedByDigitNormalization()
        {
            // Wiersze tabeli liczbowej („1 15 200" vs „2 16 300") normalizują się do tej samej
            // postaci „# ## ###" — sygnał powtórzeniowy nie może ich skasować jako artefaktu.
            var builder = new TestPdfBuilder();
            for (int p = 0; p < 2; p++)
            {
                builder.AddPage();
                WithBodyPadding(builder, fromY: 700);
                builder.AddText($"{p + 1} 1{5 + p} {200 + p * 100}", 57, 30);
            }

            var blocks = Read(builder.Build());

            Assert.Contains(blocks, b => b.Text == "1 15 200");
            Assert.Contains(blocks, b => b.Text == "2 16 300");
        }

        [Fact]
        public void HeaderOnlyTextLayer_AfterFiltering_ThrowsScannedPdfException()
        {
            // Skan częściowo cyfrowy: warstwa tekstowa to wyłącznie nagłówki/paginacja — po filtrze
            // artefaktów nie zostaje nic; to skan, nie pusty sukces.
            var builder = new TestPdfBuilder();
            for (int p = 1; p <= 3; p++)
            {
                builder.AddPage();
                builder.AddText($"Dziennik Ustaw – {p} – Poz. 902", 180, 820, 9);
                builder.AddText($"– {p} –", 280, 30, 9);
            }

            using var ms = new MemoryStream(builder.Build());
            var ex = Assert.Throws<ScannedPdfException>(() => new PdfBlockReader().ReadBlocks(ms));
            Assert.Contains("artefakty", ex.Message);
        }

        [Fact]
        public void FootnoteHeavyPage_BodySizeStillDominant_FootnotesDetected()
        {
            // Krótka nowela z długim odnośnikiem: wiersze przypisów przeważają liczebnie —
            // dominanta treści nie może spaść do rozmiaru przypisów (wyłączałoby to ich strefę).
            var builder = new TestPdfBuilder().AddPage();
            builder.AddText("Art. 1. W ustawie o podatku rolnym wprowadza się zmiany.", 57, 700, 11);
            builder.AddText("Art. 2. Ustawa wchodzi w życie po upływie 14 dni.", 57, 686, 11);
            for (int i = 0; i < 7; i++)
                builder.AddText(i == 0
                    ? "1) Zmiany tekstu jednolitego wymienionej ustawy ogłoszone zostały"
                    : $"w Dz. U. z 20{10 + i} r. poz. {100 + i} oraz kolejne zmiany wymienione dalej,", 57, 150 - i * 10, 8);

            var blocks = Read(builder.Build());

            Assert.Contains(blocks, b => b.Role == BlockRole.FootnoteText);
            Assert.DoesNotContain(blocks, b => b.Role == BlockRole.Body && b.Text.Contains("Zmiany tekstu jednolitego"));
        }

        [Fact]
        public void GraphicAnnexPages_EmptyMajority_DoesNotRejectFullTextAct()
        {
            // Akt z pełnym tekstem + wielostronicowy załącznik graficzny (mapy/wzory bez tekstu):
            // większość stron pusta, ale strony niepuste są bogate w treść → NIE skan.
            var builder = new TestPdfBuilder();
            builder.AddPage();
            WithBodyPadding(builder, fromY: 700, lines: 8);
            for (int i = 0; i < 4; i++)
                builder.AddPage();

            var blocks = Read(builder.Build());

            Assert.NotEmpty(blocks);
        }

        [Fact]
        public void SeparateTextRuns_OnOneLine_GetSpaceFromGap()
        {
            // PDF-y bez glifów spacji pozycjonują słowa osobnymi operacjami — spacja musi wynikać
            // z odstępu geometrycznego.
            var pdf = WithBodyPadding(new TestPdfBuilder().AddPage()
                .AddText("Art.", 57, 700)
                .AddText("1. Przepis ogólny.", 80, 700))
                .Build();

            var blocks = Read(pdf);

            Assert.Contains(blocks, b => b.Text.StartsWith("Art. 1. Przepis"));
        }

        [Fact]
        public void NestedTiretsFromPdfIndentation_ProduceNestedModel()
        {
            // Integracja z Etapem 7a: wcięcia z PDF zasilają wnioskowanie głębokości tiretu.
            var pdf = WithBodyPadding(new TestPdfBuilder().AddPage()
                .AddText("Art. 1. Wyliczenie obejmuje:", 57, 700)
                .AddText("1) punkt z literami:", 57, 686)
                .AddText("a) litera z tiretami:", 71, 672)
                .AddText("– tiret pierwszy,", 85, 658)
                .AddText("– tiret zagnieżdżony,", 110, 644)
                .AddText("– tiret powrotny.", 85, 630))
                .Build();

            var model = Saga.Core.LegalDocumentParser.ParseBlocks(Read(pdf));

            var letter = model.Articles.First().Paragraphs[0].Points[0].Letters[0];
            Assert.Equal(2, letter.Tirets.Count);
            Assert.Single(letter.Tirets[0].Tirets);
        }
    }
}
