using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Wersje czasowe jednostek tekstu jednolitego (ADR-0060, § 106a ZTP): jednostka, której zmiana nie weszła jeszcze
    /// w życie, stoi w tekście jednolitym dwa razy — w brzmieniu obowiązującym i przyszłym.
    /// </summary>
    public class WordingVersionTests
    {
        private const string Until2 = "W tym brzmieniu obowiązuje do wejścia w życie zmiany, o której mowa w odnośniku 2.";

        private const string ChangedOn2026 =
            "W brzmieniu ustalonym przez art. 1 pkt 1 ustawy z dnia 9 maja 2025 r. o zmianie ustawy o odbudowie (Dz. U. poz. 680); wejdzie w życie z dniem 1 stycznia 2026 r.";

        // Zdarzenie należy do aktu, który zmianę wprowadza (ADR-0063) — tu ustawy z Dz. U. z 2025 r. poz. 680.
        private const string ChangeEvent = "zdarz_wejscie_2026-01-01_DU-2025-680_odczytane";

        private static DocumentBlock Block(string text, params (string Symbol, string Text)[] footnotes) => new()
        {
            Text = text,
            Footnotes = footnotes.Select(footnote => new BlockFootnote(footnote.Symbol, [footnote.Text])).ToList(),
        };

        private static LegalDocument Parse(bool consolidated, params DocumentBlock[] body) => Parse(consolidated, atTheIssuer: null, body);

        private static LegalDocument Parse(bool consolidated, BlockFootnote? atTheIssuer, DocumentBlock[] body)
        {
            var blocks = new List<DocumentBlock>
            {
                new() { Text = "DZIENNIK USTAW RZECZYPOSPOLITEJ POLSKIEJ", Role = BlockRole.JournalHeader },
                new() { Text = "Warszawa, dnia 11 sierpnia 2025 r.", Role = BlockRole.JournalHeader },
                new() { Text = "Poz. 1094", Role = BlockRole.JournalHeader },
            };

            if (consolidated)
            {
                blocks.Add(Block("Obwieszczenie"));
                blocks.Add(atTheIssuer is null
                    ? Block("Marszałka Sejmu Rzeczypospolitej Polskiej")
                    : new DocumentBlock { Text = $"Marszałka Sejmu Rzeczypospolitej Polskiej[{atTheIssuer.Marker})]", Footnotes = [atTheIssuer] });
                blocks.AddRange(new[]
                {
                    "z dnia 6 sierpnia 2025 r.",
                    "w sprawie ogłoszenia jednolitego tekstu ustawy o odbudowie obiektów budowlanych",
                    "1. Na podstawie art. 16 ust. 1 ustawy z dnia 20 lipca 2000 r. o ogłaszaniu aktów normatywnych ogłasza się w załączniku do niniejszego obwieszczenia jednolity tekst ustawy z dnia 11 sierpnia 2001 r. o odbudowie obiektów budowlanych (Dz. U. z 2024 r. poz. 1100).",
                    "Marszałek Sejmu: S. Hołownia",
                    "Załącznik do obwieszczenia Marszałka Sejmu Rzeczypospolitej Polskiej z dnia 6 sierpnia 2025 r. (Dz. U. poz. 1094)",
                }.Select(line => Block(line)));
            }

            blocks.AddRange(new[] { "USTAWA", "z dnia 11 sierpnia 2001 r.", "o odbudowie obiektów budowlanych", "Art. 1. Ustawa reguluje zasady odbudowy." }
                .Select(line => Block(line)));
            blocks.AddRange(body);
            blocks.Add(Block("Art. 9. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia."));
            return LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
        }

        private static LegalDocument Parse(params DocumentBlock[] body) => Parse(consolidated: true, body);

        private static LegalDocument ParagraphPrintedTwice() => Parse(
            Block("Art. 2. 1.[1)] Odbudowę prowadzi się na wniosek.", ("1", Until2)),
            Block("1.[2)] Odbudowę prowadzi się na wniosek albo z urzędu.", ("2", ChangedOn2026)),
            Block("2. Wniosek składa się do wojewody."));

        private static List<Model.EditorialUnits.Paragraph> ParagraphsOf(LegalDocument document, string article) =>
            document.Articles.Single(a => a.Number?.Value == article).Paragraphs;

        // Adres jednostki w eksporcie zaczyna pozycja dziennika, w którym tekst jednolity ogłoszono.
        private const string Act = "DU/2025/1094__";

        private static List<XElement> Units(XElement xml, string eId) =>
            xml.Descendants().Where(e => e.Attribute("eId")?.Value == Act + eId).ToList();

        // ============================================================
        // Rozpoznanie
        // ============================================================

        [Fact]
        public void UnitPrintedTwice_IsTheCurrentAndTheFutureWording()
        {
            var paragraphs = ParagraphsOf(ParagraphPrintedTwice(), "2");
            Assert.Equal(new[] { "1", "1", "2" }, paragraphs.Select(p => p.Number?.Value));

            var current = paragraphs[0].Version!;
            Assert.Equal(WordingVersionKind.Current, current.Kind);
            var future = paragraphs[1].Version!;
            Assert.Equal(WordingVersionKind.Future, future.Kind);

            // Koniec wersji obowiązującej i początek przyszłej to ta sama zmiana.
            Assert.Equal("2", future.ChangeFootnote);
            Assert.Equal("2", current.ChangeFootnote);
            Assert.Equal(new DateTime(2026, 1, 1), future.ChangeDate);
            Assert.Equal(new DateTime(2026, 1, 1), current.ChangeDate);

            // Ustęp drukowany raz wersji nie ma.
            Assert.Null(paragraphs[2].Version);
            Assert.All(ParagraphsOf(ParagraphPrintedTwice(), "1"), p => Assert.Null(p.Version));
        }

        [Fact]
        public void UnitsOfTheSameChange_RepeatTheMarker()
        {
            // Korpus: 289861_2624926, art. 100c — kolejne ustępy tej samej zmiany powtarzają znaczniki odnośników.
            var paragraphs = ParagraphsOf(Parse(
                Block("Art. 2. 1.[1)] Odbudowę prowadzi się na wniosek.", ("1", Until2)),
                Block("1.[2)] Odbudowę prowadzi się na wniosek albo z urzędu.", ("2", ChangedOn2026)),
                Block("2.[1)] Wniosek składa się do wojewody."),
                Block("2.[2)] Wniosek składa się do starosty.")), "2");

            Assert.Equal(
                new[] { WordingVersionKind.Current, WordingVersionKind.Future, WordingVersionKind.Current, WordingVersionKind.Future },
                paragraphs.Select(p => p.Version!.Kind));
            Assert.All(paragraphs, p => Assert.Equal("2", p.Version!.ChangeFootnote));
        }

        [Fact]
        public void RepeatedMarker_InADocumentThatRestartsFootnoteNumbering_IsReadByItsPrintedNumber()
        {
            // Korpus: 329304_2949179 — obwieszczenie ma własny odnośnik („a)"), a ogłaszany akt numeruje odnośniki
            // od nowa. Powtórzony znacznik jest w pliku tekstem i niesie numer drukowany.
            var document = Parse(consolidated: true,
                atTheIssuer: new BlockFootnote("a", ["Marszałek ogłasza tekst jednolity."]) { Marker = "1" },
                [
                    new DocumentBlock
                    {
                        Text = "Art. 2. 1.[100001)] Odbudowę prowadzi się na wniosek.",
                        Footnotes = [new BlockFootnote("1", [Until2]) { Marker = "100001", Run = 1 }],
                    },
                    new DocumentBlock
                    {
                        Text = "1.[100002)] Odbudowę prowadzi się na wniosek albo z urzędu.",
                        Footnotes = [new BlockFootnote("2", [ChangedOn2026]) { Marker = "100002", Run = 1 }],
                    },
                    Block("2.[1)] Wniosek składa się do wojewody."),
                    Block("2.[2)] Wniosek składa się do starosty."),
                ]);

            var paragraphs = ParagraphsOf(document, "2");
            Assert.Equal(
                new WordingVersionKind?[] { WordingVersionKind.Current, WordingVersionKind.Future, WordingVersionKind.Current, WordingVersionKind.Future },
                paragraphs.Select(p => p.Version?.Kind));

            // Zmianę nazywa oznaczenie odnośnika jak w akcie — „2)", nie numer kolejny w dokumencie.
            Assert.All(paragraphs, p => Assert.Equal("2", p.Version!.ChangeFootnote));
            var change = Assert.Single(LegalDocumentConverter.Convert(document).Descendants(), e => e.Attribute("dowod") is not null);
            Assert.Equal(Act + "odn_2", change.Attribute("dowod")!.Value);
        }

        [Fact]
        public void MarkerAfterTheArticleDesignation_VersionsTheWholeArticle()
        {
            // Korpus: 299147_2705748, art. 179 — „Art. 179.[28)] 1. …": odnośnik stoi przy oznaczeniu artykułu.
            var document = Parse(
                Block("Art. 2.[1)] 1. Odbudowę prowadzi się na wniosek.", ("1", Until2)),
                Block("2. Wniosek składa się do wojewody:"),
                Block("1) pisemnie;"),
                Block("2) ustnie."),
                Block("Art. 2.[2)] 1. Odbudowę prowadzi się na wniosek albo z urzędu.", ("2", ChangedOn2026)),
                Block("2. Wniosek składa się do starosty."));

            var copies = document.Articles.Where(a => a.Number?.Value == "2").ToList();
            Assert.Equal(new WordingVersionKind?[] { WordingVersionKind.Current, WordingVersionKind.Future }, copies.Select(a => a.Version?.Kind));

            // Cała treść artykułu dzieli jego wersję — także ustęp bez własnego odnośnika i jego punkty.
            var inherited = copies[0].Paragraphs.Concat<BaseEntity>(copies[0].Paragraphs[1].Points).ToList();
            Assert.Equal(4, inherited.Count);
            Assert.All(inherited, unit => Assert.Same(copies[0].Version, unit.Version));
            Assert.All(copies[1].Paragraphs, paragraph => Assert.Same(copies[1].Version, paragraph.Version));
            Assert.Equal(new DateTime(2026, 1, 1), copies[0].Version!.ChangeDate);
        }

        [Fact]
        public void UnitRepealedInTheFuture_EndsWithARepeal()
        {
            // Korpus: 299130_2697165, art. 50 ust. 6 pkt 4a — § 106a ust. 2 ZTP: „Obowiązuje do wejścia w życie zmiany …".
            var points = ParagraphsOf(Parse(
                Block("Art. 2. Wniosek zawiera:"),
                Block("1) imię i nazwisko;"),
                Block("2)[1)] numer telefonu;", ("1", "Obowiązuje do wejścia w życie zmiany, o której mowa w odnośniku 2.")),
                Block("2) (uchylony)[2)]", ("2", "Przez art. 1 pkt 2 ustawy z dnia 9 maja 2025 r. o zmianie ustawy o odbudowie (Dz. U. poz. 680); wejdzie w życie z dniem 1 stycznia 2026 r.")),
                Block("3) podpis.")), "2").Single().Points;

            Assert.Equal(new[] { "1", "2", "2", "3" }, points.Select(p => p.Number?.Value));
            Assert.Equal(WordingVersionKind.Current, points[1].Version!.Kind);
            Assert.Equal(WordingVersionKind.Future, points[2].Version!.Kind);
            Assert.All(points.Skip(1).Take(2), point => Assert.True(point.Version!.ChangeRepeals));
            Assert.Null(points[0].Version);
            Assert.Null(points[3].Version);
        }

        [Fact]
        public void SecondCopyWithoutADate_IsTheFutureWordingByItsPlace()
        {
            // Korpus: 302049_2704309, art. 479(58) § 1 — odnośnik wersji przyszłej odsyła do ustawy i dnia nie podaje.
            var paragraphs = ParagraphsOf(Parse(
                Block("Art. 2. 1.[1)] Odbudowę prowadzi się na wniosek.", ("1", Until2)),
                Block("1.[2)] Odbudowę prowadzi się z urzędu.", ("2", "Ze zmianą wprowadzoną przez art. 2 pkt 3 ustawy, o której mowa w odnośniku 5.")),
                Block("2. Wniosek składa się do wojewody.")), "2");

            Assert.Equal(WordingVersionKind.Future, paragraphs[1].Version!.Kind);
            Assert.Null(paragraphs[1].Version!.ChangeDate);
            Assert.Null(paragraphs[1].Version!.ChangeTerm);
            Assert.Equal("2", paragraphs[0].Version!.ChangeFootnote);
        }

        [Fact]
        public void SecondCopyDescribedByItsDifference_IsStillTheFutureWording()
        {
            // Korpus: 329085_2947238, art. 93c ust. 1 — odnośnik drugiej kopii mówi, czym różni się od pierwszej.
            var paragraphs = ParagraphsOf(Parse(
                Block("Art. 2. 1.[1)] Odbudowę prowadzi się na wniosek. Wniosek składa się do wojewody.", ("1", Until2)),
                Block("1.[2)] Odbudowę prowadzi się na wniosek.", ("2", "Zdanie drugie uchylone przez art. 1 pkt 4 lit. a ustawy, o której mowa w odnośniku 5.")),
                Block("2. Wojewoda rozpatruje wniosek.")), "2");

            Assert.Equal(WordingVersionKind.Future, paragraphs[1].Version!.Kind);
            Assert.Equal("2", paragraphs[0].Version!.ChangeFootnote);
            Assert.Null(paragraphs[2].Version);
        }

        [Fact]
        public void UnitWithItsOwnFootnote_InsideAVersionedUnit_KeepsTheVersionForItsContent()
        {
            // Punkt z własnym, zwykłym odnośnikiem nie przerywa dziedziczenia wersji przez swoje litery.
            var document = Parse(
                Block("Art. 2.[1)] Wniosek zawiera:", ("1", "Dodany przez art. 1 pkt 3 ustawy, o której mowa w odnośniku 5; wejdzie w życie z dniem 1 września 2026 r.")),
                Block("1)[2)] dane wnioskodawcy:", ("2", "Ze zmianą wprowadzoną przez art. 7 ustawy, o której mowa w odnośniku 6.")),
                Block("a) imię,"),
                Block("b) nazwisko;"),
                Block("2) podpis."));

            var article = document.Articles.Single(a => a.Number?.Value == "2");
            var point = article.Paragraphs.Single().Points[0];
            Assert.Equal(WordingVersionKind.Future, article.Version!.Kind);
            Assert.Same(article.Version, point.Version);
            Assert.All(point.Letters, letter => Assert.Same(article.Version, letter.Version));
        }

        [Fact]
        public void AddedUnitNotYetInForce_IsAFutureWordingWithoutACurrentOne()
        {
            var paragraphs = ParagraphsOf(Parse(
                Block("Art. 2. 1. Odbudowę prowadzi się na wniosek."),
                Block("1a.[1)] Odbudowę zabytku prowadzi się z urzędu.", ("1", "Dodany przez art. 1 pkt 3 ustawy, o której mowa w odnośniku 5; wejdzie w życie z dniem 1 września 2026 r."))), "2");

            Assert.Null(paragraphs[0].Version);
            Assert.Equal(WordingVersionKind.Future, paragraphs[1].Version!.Kind);
            Assert.Equal(new DateTime(2026, 9, 1), paragraphs[1].Version!.ChangeDate);
        }

        // ============================================================
        // Termin zmiany zapisany przy akcie zmieniającym, w innym odnośniku
        // ============================================================

        private const string ActEnteringIn2026 =
            "Dodany przez art. 5 pkt 1 ustawy z dnia 14 kwietnia 2025 r. o zmianie ustawy o odbudowie (Dz. U. poz. 818), która wejdzie w życie z dniem 15 lutego 2026 r.";

        [Fact]
        public void ChangeByAnActNamedInAnotherFootnote_TakesTheDayThatActEntersIntoForce()
        {
            // Korpus: 275043_2474596 — odnośnik nazywa akt odesłaniem, a dzień stoi tam, gdzie akt nazwano w pełni.
            var paragraphs = ParagraphsOf(Parse(
                Block("Art. 2. 1. Odbudowę prowadzi się na wniosek."),
                Block("1a.[1)] Odbudowę zabytku prowadzi się z urzędu.", ("1", ActEnteringIn2026)),
                Block("1b.[2)] Odbudowę pomnika prowadzi wojewoda.", ("2", "Dodany przez art. 5 pkt 2 ustawy, o której mowa w odnośniku 1."))), "2");

            Assert.Null(paragraphs[0].Version);
            Assert.All(paragraphs.Skip(1), paragraph =>
            {
                Assert.Equal(WordingVersionKind.Future, paragraph.Version!.Kind);
                Assert.Equal(new DateTime(2026, 2, 15), paragraph.Version.ChangeDate);
            });

            // Dwie zmiany tego samego dnia to jedno zdarzenie; dowodem jest pierwszy odnośnik.
            var change = Assert.Single(LegalDocumentConverter.Convert(Parse(
                Block("Art. 2. 1. Odbudowę prowadzi się na wniosek."),
                Block("1a.[1)] Odbudowę zabytku prowadzi się z urzędu.", ("1", ActEnteringIn2026)),
                Block("1b.[2)] Odbudowę pomnika prowadzi wojewoda.", ("2", "Dodany przez art. 5 pkt 2 ustawy, o której mowa w odnośniku 1.")))).Descendants(),
                e => e.Name.LocalName == "Zdarzenie" && e.Attribute("rodzaj")!.Value == "wejscie-w-zycie");
            Assert.Equal("2026-02-15", change.Attribute("data")!.Value);
            Assert.Equal(Act + "odn_1", change.Attribute("dowod")!.Value);
        }

        [Fact]
        public void SecondCopyNamingTheActElsewhere_GetsItsDay()
        {
            // Korpus: 299137_2675687, art. 32 ust. 1 pkt 1 — brzmienie przyszłe odsyła po akt, a z nim po dzień.
            var paragraphs = ParagraphsOf(Parse(
                Block("Art. 2. 1.[1)] Odbudowę prowadzi się na wniosek.", ("1", "W tym brzmieniu obowiązuje do wejścia w życie zmiany, o której mowa w odnośniku 2.")),
                Block("1.[2)] Odbudowę prowadzi się z urzędu.", ("2", "W brzmieniu ustalonym przez art. 5 pkt 3 ustawy, o której mowa w odnośniku 3.")),
                Block("2.[3)] Wniosek składa się w systemie.", ("3", ActEnteringIn2026))), "2");

            Assert.Equal(new DateTime(2026, 2, 15), paragraphs[1].Version!.ChangeDate);
            Assert.Equal(new DateTime(2026, 2, 15), paragraphs[0].Version!.ChangeDate);
        }

        [Fact]
        public void ActNamedElsewhere_WithATermInProse_GivesTheTermVerbatim()
        {
            // Korpus: 315429_2819212 — akt wejdzie w życie w dniu, którego odnośnik nie podaje.
            var paragraphs = ParagraphsOf(Parse(
                Block("Art. 2. 1.[1)] Odbudowę prowadzi się na wniosek.", ("1", "Dodany przez art. 21 pkt 1 ustawy z dnia 18 października 2024 r. o udziale w Systemie Wjazdu/Wyjazdu (Dz. U. poz. 1688), która wejdzie w życie z dniem określonym w decyzji Komisji Europejskiej.")),
                Block("2.[2)] Wniosek składa się w systemie.", ("2", "Dodany przez art. 21 pkt 2 ustawy, o której mowa w odnośniku 1."))), "2");

            Assert.Null(paragraphs[1].Version!.ChangeDate);
            Assert.Equal("z dniem określonym w decyzji Komisji Europejskiej", paragraphs[1].Version!.ChangeTerm);

            // Termin zapisano raz — przy akcie; obie zmiany czekają na to samo zdarzenie.
            Assert.Equal("1", paragraphs[1].Version!.TermFootnote);
            Assert.Equal("2", paragraphs[1].Version!.ChangeFootnote);
        }

        [Fact]
        public void ChangesWaitingForTheSameUndatedEntryOfAnAct_ShareOneEvent()
        {
            var xml = LegalDocumentConverter.Convert(Parse(
                Block("Art. 2. 1.[1)] Odbudowę prowadzi się na wniosek.", ("1", "Dodany przez art. 21 pkt 1 ustawy z dnia 18 października 2024 r. o udziale w Systemie Wjazdu/Wyjazdu (Dz. U. poz. 1688), która wejdzie w życie z dniem określonym w decyzji Komisji Europejskiej.")),
                Block("2.[2)] Wniosek składa się w systemie.", ("2", "Dodany przez art. 21 pkt 2 ustawy, o której mowa w odnośniku 1."))));

            const string undated = "zdarz_wejscie_odn-1_DU-2024-1688_odczytane";
            var change = Assert.Single(xml.Descendants(), e => e.Name.LocalName == "Zdarzenie" && e.Attribute("rodzaj")!.Value == "wejscie-w-zycie");
            Assert.Equal(undated, change.Attribute("id")!.Value);
            Assert.Equal("z dniem określonym w decyzji Komisji Europejskiej", change.Attribute("warunek")!.Value);
            Assert.Equal(Act + "odn_1", change.Attribute("dowod")!.Value);
            Assert.All(new[] { "art_2__ust_1", "art_2__ust_2" }, eId =>
                Assert.Equal($"okr_obowiazywanie.{undated}.x", Assert.Single(Units(xml, eId)).Attribute("okres")?.Value));
        }

        [Theory]
        // Akt, który już wszedł w życie, wersji przyszłej nie tworzy.
        [InlineData("Dodany przez art. 5 pkt 1 ustawy z dnia 14 kwietnia 2025 r. o zmianie ustawy o odbudowie (Dz. U. poz. 818), która weszła w życie z dniem 15 lipca 2025 r.")]
        // Zdanie po średniku mówi o przepisie, przy którym stoi odnośnik — nie o całym akcie.
        [InlineData("Dodany przez art. 5 pkt 1 ustawy z dnia 14 kwietnia 2025 r. o zmianie ustawy o odbudowie (Dz. U. poz. 818); wejdzie w życie z dniem 15 lutego 2026 r.")]
        public void ActNamedElsewhere_WithoutItsOwnFutureEntry_DoesNotMakeAVersion(string actNote)
        {
            var paragraphs = ParagraphsOf(Parse(
                Block("Art. 2. 1. Odbudowę prowadzi się na wniosek."),
                Block("Art. 3.[1)] Odbudowę zabytku prowadzi się z urzędu.", ("1", actNote)),
                Block("Art. 4.[2)] Odbudowę pomnika prowadzi wojewoda.", ("2", "Dodany przez art. 5 pkt 2 ustawy, o której mowa w odnośniku 1."))), "4");

            Assert.All(paragraphs, paragraph => Assert.Null(paragraph.Version));
        }

        [Fact]
        public void FootnoteNamingTheAct_IsLookedUpInTheSameNumberingRun()
        {
            // Odnośnik obwieszczenia ma to samo oznaczenie „1)" co pierwszy odnośnik ogłaszanego aktu (ADR-0061).
            var document = Parse(consolidated: true,
                atTheIssuer: new BlockFootnote("1", ["Marszałek ogłasza tekst jednolity ustawy, która wejdzie w życie z dniem 1 stycznia 2030 r."]),
                [
                    Block("Art. 2. 1. Odbudowę prowadzi się na wniosek."),
                    new DocumentBlock
                    {
                        Text = "1a.[100001)] Odbudowę zabytku prowadzi się z urzędu.",
                        Footnotes = [new BlockFootnote("1", [ActEnteringIn2026]) { Marker = "100001", Run = 1 }],
                    },
                    new DocumentBlock
                    {
                        Text = "1b.[100002)] Odbudowę pomnika prowadzi wojewoda.",
                        Footnotes = [new BlockFootnote("2", ["Dodany przez art. 5 pkt 2 ustawy, o której mowa w odnośniku 1."]) { Marker = "100002", Run = 1 }],
                    },
                ]);

            Assert.Equal(new DateTime(2026, 2, 15), ParagraphsOf(document, "2")[2].Version!.ChangeDate);
        }

        [Fact]
        public void TermWrittenInProse_IsKeptVerbatim()
        {
            // Korpus: 292873_2637866 — zmiana wejdzie w życie w dniu, którego odnośnik nie podaje.
            var paragraphs = ParagraphsOf(Parse(
                Block("Art. 2. 1. Odbudowę prowadzi się na wniosek."),
                Block("2.[1)] Wniosek składa się w systemie.", ("1", "Dodany przez art. 4 pkt 28 ustawy z dnia 9 maja 2025 r. o zmianie ustawy o odbudowie (Dz. U. poz. 680); wejdzie w życie z dniem wdrożenia rozwiązań technicznych umożliwiających składanie wniosków, o których mowa w art. 3 ust. 1."))), "2");

            var version = paragraphs[1].Version!;
            Assert.Null(version.ChangeDate);
            Assert.Equal("z dniem wdrożenia rozwiązań technicznych umożliwiających składanie wniosków, o których mowa w art. 3 ust. 1", version.ChangeTerm);
        }

        [Theory]
        // Zmiana, która już weszła w życie, to zwykły odnośnik tekstu jednolitego.
        [InlineData("W brzmieniu ustalonym przez art. 1 pkt 1 ustawy z dnia 9 maja 2025 r. o zmianie ustawy o odbudowie (Dz. U. poz. 680), która weszła w życie z dniem 1 lipca 2025 r.")]
        [InlineData("Dodany przez art. 1 pkt 3 ustawy, o której mowa w odnośniku 5.")]
        // Odnośnik o fragmencie jednostki (zdaniu, wprowadzeniu do wyliczenia) nie mówi o całej jednostce.
        [InlineData("Zdanie drugie w tym brzmieniu obowiązuje do wejścia w życie zmiany, o której mowa w odnośniku 2.")]
        [InlineData("Wprowadzenie do wyliczenia w brzmieniu ustalonym przez art. 1 pkt 4 ustawy, o której mowa w odnośniku 5; wejdzie w życie z dniem 1 września 2026 r.")]
        public void OtherFootnoteAtTheUnit_IsNotAVersion(string note)
        {
            var paragraphs = ParagraphsOf(Parse(
                Block("Art. 2. 1.[1)] Odbudowę prowadzi się na wniosek.", ("1", note)),
                Block("2. Wniosek składa się do wojewody.")), "2");

            Assert.All(paragraphs, paragraph => Assert.Null(paragraph.Version));
        }

        [Fact]
        public void FootnoteInsideTheText_IsNotAVersionOfTheUnit()
        {
            // Odnośnik o wersji stoi zaraz za oznaczeniem jednostki; znacznik w środku zdania mówi o czym innym.
            var paragraphs = ParagraphsOf(Parse(
                Block("Art. 2. 1. Odbudowę prowadzi się na wniosek.[1)]", ("1", ChangedOn2026))), "2");

            Assert.Null(paragraphs.Single().Version);
        }

        [Fact]
        public void ActAnnouncedDirectly_HasNoWordingVersions()
        {
            var document = Parse(consolidated: false,
                Block("Art. 2. 1.[1)] Odbudowę prowadzi się na wniosek.", ("1", Until2)),
                Block("1.[2)] Odbudowę prowadzi się na wniosek albo z urzędu.", ("2", ChangedOn2026)));

            Assert.Null(document.Announcement);
            Assert.All(document.Articles.SelectMany(a => a.Paragraphs), paragraph => Assert.Null(paragraph.Version));
        }

        // ============================================================
        // Eksport: okres, grupy czasowe, zdarzenia
        // ============================================================

        [Fact]
        public void BothWordings_ShareTheEIdAndDifferByThePeriod()
        {
            var xml = LegalDocumentConverter.Convert(ParagraphPrintedTwice());

            var copies = Units(xml, "art_2__ust_1");
            Assert.Equal(2, copies.Count);
            Assert.Equal($"okr_obowiazywanie.x.{ChangeEvent}", copies[0].Attribute("okres")?.Value);
            Assert.Equal($"okr_obowiazywanie.{ChangeEvent}.x", copies[1].Attribute("okres")?.Value);

            // Projekcje okresu na daty: brzmienie obowiązujące DO dnia zmiany, przyszłe OD niego.
            Assert.Equal("2026-01-01", copies[0].Attribute("obowiazuje-do")?.Value);
            Assert.Null(copies[0].Attribute("wejscie-w-zycie"));
            Assert.Equal("2026-01-01", copies[1].Attribute("wejscie-w-zycie")?.Value);
            Assert.Null(copies[1].Attribute("obowiazuje-do"));

            // Jednostka drukowana raz czasu własnego nie ma.
            var single = Assert.Single(Units(xml, "art_2__ust_2"));
            Assert.Null(single.Attribute("okres"));

            // Kluczem dokumentu jest para (eId, okres).
            var keys = xml.Descendants()
                .Where(e => e.Attribute("eId") is not null && e.Name.LocalName is "artykul" or "ustep" or "punkt")
                .Select(e => (e.Attribute("eId")!.Value, e.Attribute("okres")?.Value))
                .ToList();
            Assert.Equal(keys.Count, keys.Distinct().Count());
        }

        [Fact]
        public void ChangeIsOneEvent_ClosingOneWordingAndOpeningTheOther()
        {
            var xml = LegalDocumentConverter.Convert(ParagraphPrintedTwice());

            var events = xml.Descendants().Where(e => e.Name.LocalName == "Zdarzenie").ToList();
            Assert.Equal(new[] { "ujednolicenie", "wejscie-w-zycie" }, events.Select(e => e.Attribute("rodzaj")!.Value));

            var change = events[1];
            Assert.Equal(ChangeEvent, change.Attribute("id")!.Value);
            Assert.Equal("odczytane", change.Attribute("pochodzenie")!.Value);
            Assert.Equal("2026-01-01", change.Attribute("data")!.Value);
            Assert.Equal(Act + "odn_2", change.Attribute("dowod")!.Value);

            var groups = xml.Descendants().Where(e => e.Name.LocalName == "GrupaCzasowa").ToList();
            Assert.Equal(3, groups.Count);

            // Główna zostaje grupa ujednolicenia.
            Assert.Equal(new[] { "true", "false", "false" }, groups.Select(g => g.Attribute("glowna")!.Value));

            var until = Assert.Single(groups[1].Elements());
            Assert.Equal("obowiazywanie", until.Attribute("rodzaj")!.Value);
            Assert.Null(until.Attribute("od"));
            Assert.Equal(ChangeEvent, until.Attribute("do")!.Value);

            var from = Assert.Single(groups[2].Elements());
            Assert.Equal(ChangeEvent, from.Attribute("od")!.Value);
            Assert.Null(from.Attribute("do"));
        }

        [Fact]
        public void FutureRepeal_IsARepealEvent_AndTheRepealedUnitCarriesItsState()
        {
            var xml = LegalDocumentConverter.Convert(Parse(
                Block("Art. 2. Wniosek zawiera:"),
                Block("1) imię i nazwisko;"),
                Block("2)[1)] numer telefonu;", ("1", "Obowiązuje do wejścia w życie zmiany, o której mowa w odnośniku 2.")),
                Block("2) (uchylony)[2)]", ("2", "Przez art. 1 pkt 2 ustawy z dnia 9 maja 2025 r. o zmianie ustawy o odbudowie (Dz. U. poz. 680); wejdzie w życie z dniem 1 stycznia 2026 r.")),
                Block("3) podpis.")));

            const string repeal = "zdarz_uchylenie_2026-01-01_DU-2025-680_odczytane";
            var change = Assert.Single(xml.Descendants(), e => e.Name.LocalName == "Zdarzenie" && e.Attribute("rodzaj")!.Value == "uchylenie");
            Assert.Equal(repeal, change.Attribute("id")!.Value);

            var copies = Units(xml, "art_2__pkt_2");
            Assert.Equal(2, copies.Count);
            Assert.Equal($"okr_obowiazywanie.x.{repeal}", copies[0].Attribute("okres")?.Value);
            Assert.Equal("2026-01-01", copies[0].Attribute("obowiazuje-do")?.Value);
            Assert.Null(copies[0].Attribute("stan-jednostki"));

            // Jednostka uchylana nie „wchodzi w życie": mówią o niej okres i stan.
            Assert.Equal($"okr_obowiazywanie.{repeal}.x", copies[1].Attribute("okres")?.Value);
            Assert.Equal("uchylony", copies[1].Attribute("stan-jednostki")?.Value);
            Assert.Null(copies[1].Attribute("wejscie-w-zycie"));
        }

        [Fact]
        public void ChangeWithoutADay_IsAnEventWithoutADate()
        {
            var xml = LegalDocumentConverter.Convert(Parse(
                Block("Art. 2. 1. Odbudowę prowadzi się na wniosek."),
                Block("2.[1)] Wniosek składa się w systemie.", ("1", "Dodany przez art. 4 pkt 28 ustawy z dnia 9 maja 2025 r. o zmianie ustawy o odbudowie (Dz. U. poz. 680); wejdzie w życie z dniem wdrożenia rozwiązań technicznych."))));

            // Granica istnieje, dnia nie znamy: zdarzenie bez daty, z terminem przeniesionym dosłownie.
            const string undated = "zdarz_wejscie_odn-1_DU-2025-680_odczytane";
            var change = Assert.Single(xml.Descendants(), e => e.Name.LocalName == "Zdarzenie" && e.Attribute("id")!.Value == undated);
            Assert.Null(change.Attribute("data"));
            Assert.Equal("z dniem wdrożenia rozwiązań technicznych", change.Attribute("warunek")?.Value);

            var added = Assert.Single(Units(xml, "art_2__ust_2"));
            Assert.Equal($"okr_obowiazywanie.{undated}.x", added.Attribute("okres")?.Value);
            Assert.Null(added.Attribute("wejscie-w-zycie"));
        }

        [Fact]
        public void CurrentWordingWithoutItsChange_KeepsNoPeriod()
        {
            // Wersji przyszłej obok nie ma — okresu nie da się zamknąć zdarzeniem, więc jednostka wychodzi jak dotąd.
            var document = Parse(
                Block("Art. 2. 1.[1)] Odbudowę prowadzi się na wniosek.", ("1", Until2)),
                Block("2. Wniosek składa się do wojewody."));

            Assert.Equal(WordingVersionKind.Current, ParagraphsOf(document, "2")[0].Version!.Kind);
            Assert.Equal(string.Empty, ParagraphsOf(document, "2")[0].Version!.ChangeFootnote);

            var xml = LegalDocumentConverter.Convert(document);
            Assert.DoesNotContain(xml.Descendants(), e => e.Attribute("okres") is not null || e.Attribute("obowiazuje-do") is not null);
            Assert.Single(xml.Descendants(), e => e.Name.LocalName == "Zdarzenie");
        }

        // ============================================================
        // Akt, który zmianę wprowadza (ADR-0063)
        // ============================================================

        private static List<XElement> ChangeEvents(XElement xml) =>
            xml.Descendants().Where(e => e.Name.LocalName == "Zdarzenie" && e.Attribute("rodzaj")!.Value != "ujednolicenie").ToList();

        [Fact]
        public void BothWordings_NameTheAmendingActAndItsProvision()
        {
            var paragraphs = ParagraphsOf(ParagraphPrintedTwice(), "2");

            // Rocznik bierze się z daty aktu (§ 158 ust. 7 ZTP): „ustawy z dnia 9 maja 2025 r. … (Dz. U. poz. 680)".
            Assert.All(paragraphs.Take(2), paragraph =>
            {
                Assert.Equal("DU/2025/680", paragraph.Version!.ChangeAct);
                Assert.Equal("art. 1 pkt 1", paragraph.Version.ChangeProvision);
            });
        }

        [Fact]
        public void ChangeEvent_NamesItsSourceBasisAndEvidence()
        {
            var change = Assert.Single(ChangeEvents(LegalDocumentConverter.Convert(ParagraphPrintedTwice())));

            Assert.Equal(ChangeEvent, change.Attribute("id")!.Value);
            Assert.EndsWith("/eli/DU/2025/680", change.Attribute("zrodlo")!.Value);
            Assert.Equal("art. 1 pkt 1", change.Attribute("podstawa")!.Value);
            Assert.Equal(Act + "odn_2", change.Attribute("dowod")!.Value);
        }

        [Fact]
        public void ChangesOfTwoActsOnOneDay_AreTwoEvents()
        {
            var xml = LegalDocumentConverter.Convert(Parse(
                Block("Art. 2. 1. Odbudowę prowadzi się na wniosek."),
                Block("1a.[1)] Odbudowę zabytku prowadzi się z urzędu.", ("1", "Dodany przez art. 1 pkt 3 ustawy z dnia 9 maja 2025 r. o zmianie ustawy o odbudowie (Dz. U. poz. 680); wejdzie w życie z dniem 1 stycznia 2026 r.")),
                Block("1b.[2)] Odbudowę pomnika prowadzi wojewoda.", ("2", "Dodany przez art. 7 ustawy z dnia 20 czerwca 2025 r. o ochronie zabytków (Dz. U. poz. 900); wejdzie w życie z dniem 1 stycznia 2026 r."))));

            Assert.Equal(
                new[] { "zdarz_wejscie_2026-01-01_DU-2025-680_odczytane", "zdarz_wejscie_2026-01-01_DU-2025-900_odczytane" },
                ChangeEvents(xml).Select(change => change.Attribute("id")!.Value));
            Assert.Equal(new[] { "art. 1 pkt 3", "art. 7" }, ChangeEvents(xml).Select(change => change.Attribute("podstawa")!.Value));
        }

        [Fact]
        public void ActNamedElsewhere_IsReadWhereTheReferenceLeads()
        {
            // Odnośnik zmiany nazywa akt odesłaniem; publikator w jego dalszej części należy do innego aktu.
            var paragraphs = ParagraphsOf(Parse(
                Block("Art. 2. 1. Odbudowę prowadzi się na wniosek."),
                Block("1a.[1)] Odbudowę zabytku prowadzi się z urzędu.", ("1", ActEnteringIn2026)),
                Block("1b.[2)] Odbudowę pomnika prowadzi wojewoda.", ("2", "Dodany przez art. 5 pkt 2 ustawy, o której mowa w odnośniku 1; wejdzie w życie z dniem 1 marca 2026 r. zgodnie z komunikatem ogłoszonym na podstawie ustawy z dnia 3 lutego 2020 r. o komunikatach (Dz. U. poz. 77)."))), "2");

            Assert.Equal("DU/2025/818", paragraphs[1].Version!.ChangeAct);
            Assert.Equal("DU/2025/818", paragraphs[2].Version!.ChangeAct);
            Assert.Equal("art. 5 pkt 2", paragraphs[2].Version!.ChangeProvision);
            Assert.Equal(new DateTime(2026, 3, 1), paragraphs[2].Version!.ChangeDate);
        }

        [Fact]
        public void EventOfSeveralProvisions_NamesThemAll()
        {
            // Dowodem zdarzenia jest odnośnik, w którym stoi termin; podstawą — każdy przepis, który na ten termin czeka.
            var xml = LegalDocumentConverter.Convert(Parse(
                Block("Art. 2. 1. Odbudowę prowadzi się na wniosek."),
                Block("1a.[1)] Odbudowę zabytku prowadzi się z urzędu.", ("1", ActEnteringIn2026)),
                Block("1b.[2)] Odbudowę pomnika prowadzi wojewoda.", ("2", "Dodany przez art. 5 pkt 2 ustawy, o której mowa w odnośniku 1."))));

            var change = Assert.Single(ChangeEvents(xml));
            Assert.Equal("zdarz_wejscie_2026-02-15_DU-2025-818_odczytane", change.Attribute("id")!.Value);
            Assert.Equal(Act + "odn_1", change.Attribute("dowod")!.Value);
            Assert.Equal("art. 5 pkt 1; art. 5 pkt 2", change.Attribute("podstawa")!.Value);
        }

        [Fact]
        public void ChangeWhoseActIsNotNamed_StaysWithTheDocumentItWasReadIn()
        {
            // Odnośnik odsyła po akt do odnośnika, którego w dokumencie nie ma.
            var xml = LegalDocumentConverter.Convert(Parse(
                Block("Art. 2. 1.[1)] Odbudowę prowadzi się na wniosek.", ("1", Until2)),
                Block("1.[2)] Odbudowę prowadzi się z urzędu.", ("2", "Ze zmianą wprowadzoną przez art. 2 pkt 3 ustawy, o której mowa w odnośniku 5.")),
                Block("2. Wniosek składa się do wojewody.")));

            var change = Assert.Single(ChangeEvents(xml));
            Assert.Equal("zdarz_wejscie_odn-2_DU-2025-1094_odczytane", change.Attribute("id")!.Value);
            Assert.Null(change.Attribute("zrodlo"));
            Assert.Equal("art. 2 pkt 3", change.Attribute("podstawa")!.Value);
        }

        [Theory]
        // Dalsze pozycje to zmiany aktu, nie miejsce jego ogłoszenia.
        [InlineData("ustawy z dnia 18 listopada 2020 r. o doręczeniach elektronicznych (Dz. U. poz. 2320, z 2021 r. poz. 72, 802 i 1135)", "DU/2020/2320")]
        [InlineData("ustawy z dnia 11 sierpnia 2001 r. o odbudowie (Dz. U. z 2024 r. poz. 1100 i 1473)", "DU/2024/1100")]
        [InlineData("uchwały z dnia 5 marca 2024 r. w sprawie regulaminu (M.P. poz. 210)", "MP/2024/210")]
        public void FirstActOfAJournalReference_IsItsFirstWrittenPosition(string text, string expected)
        {
            Assert.Equal(expected, JournalReferenceService.ReadFirstAct(text)?.GetELIStrings().Single());
        }

        [Fact]
        public void JournalWithoutAYearOrAnActDate_NamesNoAct()
        {
            Assert.Null(JournalReferenceService.ReadFirstAct("zgodnie z komunikatem (Dz. U. poz. 77)"));
            Assert.Null(JournalReferenceService.ReadFirstAct("zgodnie z komunikatem"));
        }

        // ============================================================
        // Zdanie w dwóch brzmieniach (ADR-0064)
        // ============================================================

        private const string SentenceUntil2 = "Zdanie drugie w tym brzmieniu obowiązuje do wejścia w życie zmiany, o której mowa w odnośniku 2.";

        private const string SentenceChangedIn2029 =
            "Zdanie drugie w brzmieniu ustalonym przez art. 82 pkt 8 ustawy z dnia 18 listopada 2020 r. o doręczeniach elektronicznych (Dz. U. poz. 2320); wejdzie w życie z dniem 1 października 2029 r.";

        // Korpus: 307517_2786682, art. 130 — odnośnik o zdaniu stoi za zdaniem, a oba brzmienia jedno za drugim.
        private static LegalDocument SentencePrintedTwice() => Parse(
            new DocumentBlock
            {
                Text = "Art. 2. Pisma doręcza się za pokwitowaniem odbioru. Odbierający potwierdza odbiór podpisem.[1)] "
                    + "Odbierający potwierdza odbiór czytelnym podpisem.[2)] Pokwitowanie zwraca się nadawcy.",
                Footnotes = [new BlockFootnote("1", [SentenceUntil2]), new BlockFootnote("2", [SentenceChangedIn2029])],
            });

        private static List<TextSegment> SentencesOf(LegalDocument document, string article) =>
            ParagraphsOf(document, article).Single().TextSegments;

        [Fact]
        public void SentencePrintedTwice_IsTwoWordingsOfOneSentence()
        {
            var sentences = SentencesOf(SentencePrintedTwice(), "2");

            // Oba brzmienia niosą numer zdania drugiego; zdanie za nimi jest trzecie, nie czwarte.
            Assert.Equal(new[] { 1, 2, 2, 3 }, sentences.Select(sentence => sentence.Order));
            Assert.Equal(
                new WordingVersionKind?[] { null, WordingVersionKind.Current, WordingVersionKind.Future, null },
                sentences.Select(sentence => sentence.Version?.Kind));

            Assert.All(sentences.Skip(1).Take(2), sentence =>
            {
                Assert.Equal("2", sentence.Version!.ChangeFootnote);
                Assert.Equal(new DateTime(2029, 10, 1), sentence.Version.ChangeDate);
                Assert.Equal("DU/2020/2320", sentence.Version.ChangeAct);
            });

            // Jednostka jako całość wersji nie ma — zmienia się jedno jej zdanie.
            Assert.Null(ParagraphsOf(SentencePrintedTwice(), "2").Single().Version);
        }

        [Fact]
        public void SentencePrintedTwice_IsExportedWithOneNumberAndTwoPeriods()
        {
            var xml = LegalDocumentConverter.Convert(SentencePrintedTwice());
            const string change = "zdarz_wejscie_2029-10-01_DU-2020-2320_odczytane";

            var sentences = xml.Descendants().Single(e => e.Attribute("eId")?.Value == Act + "art_2")
                .Descendants().Where(e => e.Name.LocalName == "zdanie" && e.Ancestors().All(a => a.Name.LocalName != "odnosnik")).ToList();
            Assert.Equal(new[] { "1", "2", "2", "3" }, sentences.Select(sentence => sentence.Attribute("nr")!.Value));
            Assert.Equal(
                new[] { null, $"okr_obowiazywanie.x.{change}", $"okr_obowiazywanie.{change}.x", null },
                sentences.Select(sentence => sentence.Attribute("okres")?.Value));
            Assert.Equal("2029-10-01", sentences[1].Attribute("obowiazuje-do")?.Value);
            Assert.Equal("2029-10-01", sentences[2].Attribute("wejscie-w-zycie")?.Value);

            var changeEvent = Assert.Single(ChangeEvents(xml));
            Assert.Equal(change, changeEvent.Attribute("id")!.Value);
            Assert.Equal("art. 82 pkt 8", changeEvent.Attribute("podstawa")!.Value);
        }

        [Fact]
        public void SentencesAddedLater_AreFutureWordingsWithoutACurrentOne()
        {
            // Korpus: 275050_2461922 — „Zdanie trzecie i czwarte dodane przez …": znacznik stoi za każdym z nich.
            var sentences = SentencesOf(Parse(
                new DocumentBlock
                {
                    Text = "Art. 2. Wniosek składa się do wojewody. Wojewoda rozpatruje wniosek. Termin wynosi 60 dni.[1)] "
                        + "Brak odpowiedzi uważa się za zgodę.[1)]",
                    Footnotes = [new BlockFootnote("1", ["Zdanie trzecie i czwarte dodane przez art. 14 pkt 2 lit. b ustawy z dnia 7 lipca 2023 r. o zmianie ustawy o planowaniu (Dz. U. poz. 1688); wejdzie w życie z dniem 1 stycznia 2026 r."])],
                }), "2");

            Assert.Equal(new[] { 1, 2, 3, 4 }, sentences.Select(sentence => sentence.Order));
            Assert.Equal(
                new WordingVersionKind?[] { null, null, WordingVersionKind.Future, WordingVersionKind.Future },
                sentences.Select(sentence => sentence.Version?.Kind));
        }

        [Fact]
        public void FootnoteAboutTheUnit_AtTheEndOfASentence_IsNotAVersionOfThatSentence()
        {
            // Odnośnik bez podmiotu „Zdanie …" nie mówi o zdaniu, za którym stoi.
            var sentences = SentencesOf(Parse(
                new DocumentBlock
                {
                    Text = "Art. 2. Wniosek składa się do wojewody.[1)] Wojewoda rozpatruje wniosek.",
                    Footnotes = [new BlockFootnote("1", [ChangedOn2026])],
                }), "2");

            Assert.All(sentences, sentence => Assert.Null(sentence.Version));
        }

        // ============================================================
        // Drugie brzmienie w osobnym akapicie (ADR-0065)
        // ============================================================

        private const string ChangedBy1222 =
            "ze zmianą wprowadzoną przez art. 33 pkt 1 lit. a ustawy z dnia 12 lipca 2024 r. – Przepisy wprowadzające ustawę – Prawo komunikacji elektronicznej (Dz. U. poz. 1222), która wejdzie w życie z dniem 10 listopada 2026 r.";

        // Korpus: 299137_2675687, art. 32 ust. 1 — wprowadzenie do wyliczenia wydrukowane dwa razy, drugi raz
        // osobnym akapitem bez oznaczenia; punkty stoją pod oboma brzmieniami.
        private static LegalDocument IntroductionPrintedTwice() => Parse(
            Block("Art. 2. 1. Organ może uzyskiwać dane przekazu telekomunikacyjnego określone w:[1)]",
                ("1", "Wprowadzenie do wyliczenia w tym brzmieniu obowiązuje do wejścia w życie zmiany, o której mowa w odnośniku 2.")),
            Block("Organ może uzyskiwać dane komunikatu elektronicznego określone w:[2)]",
                ("2", "Wprowadzenie do wyliczenia " + ChangedBy1222)),
            Block("1) art. 180c ustawy – Prawo telekomunikacyjne;"),
            Block("2) art. 82 ustawy – Prawo pocztowe."),
            Block("2. Dane udostępnia się nieodpłatnie."));

        [Fact]
        public void IntroductionPrintedTwice_IsOneSentenceOfTheUnitInTwoWordings()
        {
            var paragraph = ParagraphsOf(IntroductionPrintedTwice(), "2")[0];

            // Drugi akapit nie jest już „dalszą treścią" jednostki — to drugie brzmienie jej zdania.
            Assert.Empty(paragraph.Continuation);
            Assert.Equal(new[] { 1, 1 }, paragraph.TextSegments.Select(sentence => sentence.Order));
            Assert.Equal(
                new WordingVersionKind?[] { WordingVersionKind.Current, WordingVersionKind.Future },
                paragraph.TextSegments.Select(sentence => sentence.Version?.Kind));
            Assert.All(paragraph.TextSegments, sentence => Assert.Equal(new DateTime(2026, 11, 10), sentence.Version!.ChangeDate));

            // Jednostka i jej punkty wersji nie mają.
            Assert.Null(paragraph.Version);
            Assert.Equal(2, paragraph.Points.Count);
            Assert.All(paragraph.Points, point => Assert.Null(point.Version));
        }

        [Fact]
        public void IntroductionPrintedTwice_OpensOneEnumerationInTheExport()
        {
            var xml = LegalDocumentConverter.Convert(IntroductionPrintedTwice());
            var unit = Assert.Single(Units(xml, "art_2__ust_1"));

            // Oba brzmienia to pierwszy fragment tego samego zdania; punkty są jego dalszymi fragmentami.
            var sentences = unit.Descendants()
                .Where(e => e.Name.LocalName == "zdanie" && e.Ancestors().All(a => a.Name.LocalName != "odnosnik"))
                .ToList();
            Assert.Equal(new[] { "1", "1", "1", "1" }, sentences.Select(sentence => sentence.Attribute("nr")!.Value));
            Assert.Equal(new[] { "1", "1", "2", "3" }, sentences.Select(sentence => sentence.Attribute("fragment")!.Value));
            Assert.Single(sentences.Select(sentence => sentence.Attributes().Single(a => a.Name.LocalName == "zdGUID").Value).Distinct());

            const string change = "zdarz_wejscie_2026-11-10_DU-2024-1222_odczytane";
            Assert.Equal(
                new[] { $"okr_obowiazywanie.x.{change}", $"okr_obowiazywanie.{change}.x", null, null },
                sentences.Select(sentence => sentence.Attribute("okres")?.Value));
            Assert.DoesNotContain(unit.Descendants(), e => e.Name.LocalName == "fragment");
        }

        [Fact]
        public void SentenceWhoseSecondWordingIsASeparateParagraph_IsReadLikeOneInLine()
        {
            // Korpus: 336501_2993884, art. 94 ust. 4c — nowe brzmienie zdania pierwszego stoi w osobnym akapicie,
            // a za nim zdanie drugie, które się nie zmienia.
            var paragraphs = ParagraphsOf(Parse(
                Block("Art. 2. 1. Doręczenie zawiadomienia o uwzględnieniu korekty kończy kontrolę.[1)]",
                    ("1", "Zdanie pierwsze w tym brzmieniu obowiązuje do wejścia w życie zmiany, o której mowa w odnośniku 2.")),
                Block("Doręczenie zawiadomienia o uwzględnieniu deklaracji lub korekty kończy kontrolę.[2)] W tym przypadku przepisów art. 82 nie stosuje się.",
                    ("2", "Zdanie pierwsze w brzmieniu ustalonym przez art. 1 pkt 1 lit. b ustawy z dnia 9 maja 2025 r. o zmianie ustawy (Dz. U. poz. 680); wejdzie w życie z dniem 1 października 2026 r.")),
                Block("2. Kontrolę wszczyna się z urzędu.")), "2");

            Assert.Empty(paragraphs[0].Continuation);
            Assert.Equal(new[] { 1, 1, 2 }, paragraphs[0].TextSegments.Select(sentence => sentence.Order));
            Assert.Equal(
                new WordingVersionKind?[] { WordingVersionKind.Current, WordingVersionKind.Future, null },
                paragraphs[0].TextSegments.Select(sentence => sentence.Version?.Kind));
            Assert.Equal("W tym przypadku przepisów art. 82 nie stosuje się.", paragraphs[0].TextSegments[2].Text);
        }

        private const string CommonPartStyle = "CZWSPPKTczwsplnapunktw";

        // Korpus: 311012_2800574, art. 99a ust. 4 — część wspólna wydrukowana dwa razy.
        private static LegalDocument CommonPartPrintedTwice() => Parse(
            Block("Art. 2. 1. W przypadku:"),
            Block("1) sprzedaży suszu tytoniowego,"),
            Block("2) nabycia suszu tytoniowego"),
            new DocumentBlock
            {
                Text = "– bez jego oznaczenia znakami akcyzy, stawka akcyzy wynosi 812 zł za każdy kilogram.[1)]",
                StyleId = CommonPartStyle,
                Footnotes = [new BlockFootnote("1", ["Część wspólna w tym brzmieniu obowiązuje do wejścia w życie zmiany, o której mowa w odnośniku 2."])],
            },
            new DocumentBlock
            {
                Text = "– bez jego oznaczenia znakami akcyzy, stawka akcyzy wynosi 1336 zł za każdy kilogram.[2)]",
                StyleId = CommonPartStyle,
                Footnotes = [new BlockFootnote("2", ["Część wspólna w brzmieniu ustalonym przez art. 1 pkt 2 lit. b ustawy z dnia 9 maja 2025 r. o zmianie ustawy o podatku akcyzowym (Dz. U. poz. 680); wejdzie w życie z dniem 1 stycznia 2026 r."])],
            });

        [Fact]
        public void CommonPartPrintedTwice_IsTwoCommonPartsOfOneEnumeration()
        {
            var paragraph = ParagraphsOf(CommonPartPrintedTwice(), "2").Single();
            var closing = paragraph.CommonParts.Where(part => part.Type == CommonPartType.WrapUp).ToList();

            Assert.Equal(
                new WordingVersionKind?[] { WordingVersionKind.Current, WordingVersionKind.Future },
                closing.Select(part => part.Version?.Kind));
            Assert.Single(closing.Select(part => part.Id).Distinct());
            Assert.All(closing, part => Assert.Empty(part.Continuation));
            Assert.Contains("1336 zł", closing[1].ContentText);
            Assert.All(closing, part => Assert.Equal(new DateTime(2026, 1, 1), part.Version!.ChangeDate));
        }

        [Fact]
        public void CommonPartPrintedTwice_IsExportedTwiceUnderOneAddress()
        {
            var xml = LegalDocumentConverter.Convert(CommonPartPrintedTwice());
            const string change = "zdarz_wejscie_2026-01-01_DU-2025-680_odczytane";

            var parts = Units(xml, "art_2__ust_1__czw");
            Assert.Equal(2, parts.Count);
            Assert.All(parts, part => Assert.Equal("czesc-wspolna-punkt", part.Name.LocalName));
            Assert.Equal(
                new[] { $"okr_obowiazywanie.x.{change}", $"okr_obowiazywanie.{change}.x" },
                parts.Select(part => part.Attribute("okres")?.Value));

            // Oba brzmienia są tym samym fragmentem zdania otwartego wprowadzeniem do wyliczenia.
            var fragments = parts.Select(part => part.Descendants().First(e => e.Name.LocalName == "zdanie").Attribute("fragment")!.Value).ToList();
            Assert.Single(fragments.Distinct());
            Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName == "fragment");
        }

        [Fact]
        public void ParagraphAfterTheUnitText_WithoutSuchFootnotes_StaysItsFurtherContent()
        {
            var paragraph = ParagraphsOf(Parse(
                Block("Art. 2. 1. Organ może uzyskiwać dane określone w przepisach odrębnych.[1)]", ("1", ChangedOn2026)),
                Block("Dane udostępnia się nieodpłatnie.[2)]", ("2", "Zmiany wymienionej ustawy zostały ogłoszone w Dz. U. z 2024 r. poz. 100.")),
                Block("2. Wniosek składa się do wojewody.")), "2")[0];

            Assert.Single(paragraph.Continuation);
            Assert.Single(paragraph.TextSegments);
        }

        [SchemaPlFact]
        public void SecondWordingInASeparateParagraph_IsSchemaValid()
        {
            Assert.Empty(SchemaPl.Validator.Validate(LegalDocumentConverter.Convert(IntroductionPrintedTwice())));
            Assert.Empty(SchemaPl.Validator.Validate(LegalDocumentConverter.Convert(CommonPartPrintedTwice())));
        }

        [SchemaPlFact]
        public void SentenceInTwoWordings_IsSchemaValid()
        {
            Assert.Empty(SchemaPl.Validator.Validate(LegalDocumentConverter.Convert(SentencePrintedTwice())));
        }

        [SchemaPlFact]
        public void WordingVersions_AreSchemaValid()
        {
            Assert.Empty(SchemaPl.Validator.Validate(LegalDocumentConverter.Convert(ParagraphPrintedTwice())));
            Assert.Empty(SchemaPl.Validator.Validate(LegalDocumentConverter.Convert(Parse(
                Block("Art. 2.[1)] 1. Odbudowę prowadzi się na wniosek.", ("1", "Obowiązuje do wejścia w życie zmiany, o której mowa w odnośniku 2.")),
                Block("2. Wniosek składa się do wojewody."),
                Block("Art. 2. (uchylony)[2)]", ("2", "Przez art. 1 pkt 2 ustawy, o której mowa w odnośniku 5; wejdzie w życie z dniem wdrożenia rozwiązań technicznych."))))));
        }
    }
}
