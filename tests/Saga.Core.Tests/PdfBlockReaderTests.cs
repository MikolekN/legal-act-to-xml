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

        [Fact]
        public void TrailingHyphen_IsKeptAndJoinedDirectly()
        {
            // DECYZJA UŻYTKOWNIKA (zamiast dehyfenacji z planu): dywiz złożenia jest nieodróżnialny
            // od dzielenia wyrazów — zachowujemy dywiz i sklejamy bez spacji („nie zgubić informacji").
            var pdf = new TestPdfBuilder().AddPage()
                .AddText("Art. 1. Wspiera się rozwój społeczno-", 57, 700)
                .AddText("gospodarczy kraju oraz regionów.", 57, 686)
                .Build();

            var blocks = Read(pdf);

            Assert.Contains("społeczno-gospodarczy kraju", blocks[0].Text);
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
