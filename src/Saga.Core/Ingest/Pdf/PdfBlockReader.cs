#nullable enable
using System.Text;
using System.Text.RegularExpressions;
using Saga.Core.Exceptions;
using Saga.Core.Services;
using Saga.Core.Services.Parsing;
using UglyToad.PdfPig;

namespace Saga.Core.Ingest.Pdf
{
    /// <summary>
    /// Adapter PDF (warstwa tekstowa) → bloki reprezentacji pośredniej. Potok:
    /// 1. Otwarcie (PdfPig); dokument zaszyfrowany → <see cref="UnsupportedDocumentFormatException"/>.
    /// 2. Litery → wiersze (<see cref="PdfLineExtractor"/>: baseline, spacje z odstępów, superscript → [x]).
    /// 3. Detekcja skanu (średnio &lt; 20 znaków/stronę, &gt; 50% stron pustych lub &gt; 5% U+FFFD)
    ///    → <see cref="ScannedPdfException"/> (OCR poza zakresem).
    /// 4. Filtr artefaktów stron (<see cref="PageArtifactFilter"/>): nagłówki/stopki/paginacja.
    /// 5. Strefa przypisów (mniejszy font na dole strony, start „N)") → bloki <see cref="BlockRole.FootnoteText"/>
    ///    emitowane NA KOŃCU (parser je pomija; klasyfikator dokumentu czyta z nich sygnały TJ).
    /// 6. Składanie wierszy w bloki-akapity: granica na markerze jednostki (nadzbiór wzorców — tylko
    ///    segmentacja), skoku wcięcia (&gt; 0,5 em) lub odstępie pionowym (&gt; 1,9× typowej interlinii);
    ///    granica strony NIE jest granicą bloku (jednostka przecięta stroną się skleja). Odstęp akapitowy (&gt; 1,3×
    ///    interlinii) dzieli akapity bez oznaczenia, poza wierszami tabeli. Dehyfenacja
    ///    wyłącznie „-" (U+002D) na końcu wiersza + mała litera na początku następnego.
    /// 7. Wcięcia: X pierwszego wiersza bloku względem dominującego lewego marginesu → twips (1 pt = 20 twips).
    /// 8. Strefa tytułowa: nagłówek rodzaju (z organem), data aktu i przedmiot to osobne bloki, a przedmiot kończy
    ///    się na początku treści — skład nie oddziela ich odstępem większym od interlinii.
    ///    Wiersze końcowe — podpis i wiersz przyłączenia („Załącznik do …"), odsunięte w prawo — też.
    /// 9. Winieta dziennika na początku pierwszej strony → bloki <see cref="BlockRole.JournalHeader"/>
    ///    (<see cref="JournalMasthead"/>) — jak nagłówek strony DOCX, z danymi ogłoszenia.
    /// 10. Odnośniki ze strefy przypisów powiązane ze znacznikami treści (<see cref="PdfFootnotes"/>) — blok odwołania
    ///    niesie je jak blok DOCX, a bloki strefy zostają na końcu dla klasyfikatora dokumentu.
    /// Celowo NIE nadaje StyleId (zabetonowałoby błędy) — klasyfikacja idzie gałęzią bezstylową.
    /// </summary>
    public sealed class PdfBlockReader : IDocumentBlockReader
    {
        private const int MinAverageLettersPerPage = 20;
        private const double MaxEmptyPageRatio = 0.5;
        private const double MaxReplacementCharRatio = 0.05;
        private const double FootnoteSizeRatio = 0.85;

        /// <summary>
        /// Font przypisu bliski treści: skład Dziennika Ustaw pisze odnośniki 9 pt przy treści 10 pt (0,9×). Taką strefę
        /// przyjmujemy tylko, gdy zaczyna się znacznikiem w indeksie górnym — samo „N)" mniejszym fontem bywa wierszem
        /// tabeli załącznika.
        /// </summary>
        private const double CloseFootnoteSizeRatio = 0.95;
        private const double ParagraphGapRatio = 1.9;

        /// <summary>
        /// Odstęp akapitowy składu: wiersze akapitu stoją co interlinię, a akapity — z odstępem przed akapitem (Dziennik
        /// Ustaw: 13 i 19 pkt). Odstęp większy niż 1,3 interlinii dzieli akapity także bez oznaczenia jednostki (część
        /// wspólna, dalsza treść), ale tylko między wierszami bez kolumn — wiersze tabeli przytoczonej stoją z takim samym
        /// odstępem, a rozdzielone dawałyby z „1. Poziom naukowy … 70" fałszywe jednostki.
        /// </summary>
        private const double ParagraphSpacingRatio = 1.3;

        /// <summary>Odstęp w wierszu (w em), od którego wiersz uznajemy za wiersz tabeli — odstęp między kolumnami.</summary>
        private const double ColumnGapEm = 2.0;
        private const double PointsToTwips = 20.0;

        /// <summary>
        /// Markery BEZWARUNKOWE początku jednostki/nagłówka — segmentacja bloków (nie klasyfikacja).
        /// Tiret i litera są bezwarunkowe: element listy przed częścią wspólną kanonicznie NIE ma
        /// interpunkcji końcowej (§ 57 ZTP), więc warunek interpunkcyjny gubiłby granice tiretów.
        /// </summary>
        private static readonly Regex UnconditionalMarkerPattern = new(
            @"^(?:Art\.|§\s*\d|USTAWA\b|ROZPORZĄDZENIE\b|OBWIESZCZENIE\b|UCHWAŁA\b|ZARZĄDZENIE\b" +
            @"|CZĘŚĆ\s|KSIĘGA\s|TYTUŁ\s|DZIAŁ\s|ROZDZIAŁ\s|Rozdział\s|ODDZIAŁ\s|Oddział\s" +
            @"|[a-z]{1,2}\)(?:\[[^\[\]]*\])?\s|[–\-−]\s)",
            RegexOptions.Compiled);

        /// <summary>
        /// Markery WARUNKOWE — cyfrowe „N)/N." (limit 3 cyfry, także z indeksem górnym oznaczenia „5[1])" i ze
        /// znacznikiem odnośnika tuż za oznaczeniem „1)[12)]", „1)[)]") i cytat otwierający „. Odpalają tylko,
        /// gdy poprzedni wiersz wygląda na DOMKNIĘTY (kończy się interpunkcją [;:,.] i nie skrótem
        /// „poz./nr/str./art./ust./pkt/lit."): zawinięcie „…(Dz. U. poz. ⏎ 1234) wprowadza…" ani
        /// „…zastępuje się wyrazami ⏎ „centrum…"" nie może rozcinać zdania (fałszywy punkt/cytat).
        /// </summary>
        private static readonly Regex ConditionalMarkerPattern = new(
            @"^(?:\d{1,3}[a-zA-Z]*(?:\[[^\[\]]*\])?[\.\)](?:\[[^\[\]]*\])?\s|„)", RegexOptions.Compiled);

        /// <summary>
        /// Wiersz, który sam jest pełnym oznaczeniem jednostki systematyzacyjnej („Rozdział 1", „DZIAŁ II"), także
        /// przytoczonej w nowelizacji („„Dział IIIB"). Tytuł jednostki stoi w następnym wierszu (§ 62 ust. 2 ZTP) — po
        /// takim wierszu zaczyna się nowy blok.
        /// </summary>
        private static readonly Regex DesignationLinePattern = new(
            @"^„?(?:CZĘŚĆ|Część|KSIĘGA|Księga|TYTUŁ|Tytuł|DZIAŁ|Dział|ROZDZIAŁ|Rozdział|ODDZIAŁ|Oddział)\s+\S+\s*$",
            RegexOptions.Compiled);

        /// <summary>Nagłówek rodzaju aktu — otwiera strefę tytułową (§ 16–19, § 120 ZTP).</summary>
        private static readonly Regex KindHeaderPattern = new(
            @"^(?:USTAWA|ROZPORZĄDZENIE|OBWIESZCZENIE|UCHWAŁA|ZARZĄDZENIE)\b", RegexOptions.Compiled);

        /// <summary>Data aktu w strefie tytułowej: cały wiersz „z dnia 18 grudnia 2025 r." (ze znacznikiem odnośnika).</summary>
        private static readonly Regex ActDateLinePattern = new(
            @"^z\s+dnia\s+\d{1,2}\s+\p{L}+\s+\d{4}\s*r\.(?:\s*\[[^\]]*\])?\s*$", RegexOptions.Compiled);

        /// <summary>Wiersz organu wydającego w strefie tytułowej: same wielkie litery („MINISTRA FINANSÓW").</summary>
        private static readonly Regex IssuerLinePattern = new(
            @"^[\p{Lu}][\p{Lu}\s,–\-]*(?:\[[^\]]*\])?\s*$", RegexOptions.Compiled);

        /// <summary>
        /// Początek treści za przedmiotem aktu: podstawa prawna rozporządzenia („Na podstawie art. …"), pierwszy punkt
        /// obwieszczenia („1. Na podstawie …") albo pierwszy przepis. Przedmiot nie kończy się interpunkcją, więc
        /// warunkowy marker cyfrowy sam by tu nie zadziałał.
        /// </summary>
        private static readonly Regex SubjectEndPattern = new(
            @"^(?:Na\s+podstawie\b|\d{1,3}[a-z]*[\.\)]\s|Art\.|§\s*\d)", RegexOptions.Compiled);

        /// <summary>
        /// Miejsce w strefie tytułowej bieżącego bloku. Skład Dziennika Ustaw stawia nagłówek rodzaju, datę, przedmiot
        /// i początek treści w kolejnych wierszach bez odstępu większego od interlinii, więc bez tego cała strefa
        /// sklejała się w jeden blok z pierwszym akapitem treści, a parser tracił rodzaj, datę i tytuł aktu.
        /// </summary>
        private enum TitleZone
        {
            None,

            /// <summary>Blok otwarty nagłówkiem rodzaju (z organem w kolejnych wierszach).</summary>
            Kind,

            /// <summary>Blok daty aktu.</summary>
            Date,

            /// <summary>Blok przedmiotu aktu — za datą.</summary>
            Subject,
        }

        /// <summary>Wiersz przyłączenia: „Załącznik do obwieszczenia …", „Załącznik nr 2 do rozporządzenia …".</summary>
        private static readonly Regex AttachmentLinePattern = new(
            @"^Załączniki?(?:\s+nr\s+\S+)?\s+do\s+\p{L}", RegexOptions.Compiled);

        /// <summary>
        /// Oznaczenie załącznika w osobnym wierszu („Załącznik nr 1" nad treścią załącznika, gdy wiersz przyłączenia
        /// wspólny dla kilku załączników stoi wyżej: „Załączniki do rozporządzenia …"). W DOCX to osobny akapit; w PDF
        /// sklejał się z wierszem przyłączenia i z tytułem załącznika („… (Dz. U. poz. 56) Załącznik nr 1 WZÓR …").
        /// </summary>
        private static readonly Regex AnnexDesignationLinePattern = new(
            @"^Załącznik\s+nr\s+\S+\s*$", RegexOptions.Compiled);

        /// <summary>
        /// Najmniejsze odsunięcie wiersza końcowego od lewego marginesu (1 cal). Skład Dziennika Ustaw stawia podpis
        /// i wiersz przyłączenia po prawej stronie, a akapit treści zaczyna się przy marginesie albo wcięciem
        /// akapitowym — poniżej tej odległości wiersz o tej treści jest zdaniem akapitu, nie wierszem końcowym.
        /// </summary>
        private const double ClosingLineMinIndentPoints = 72;

        /// <summary>
        /// Adnotacja o jednostce uchylonej stojąca w osobnym wierszu: „(uchylony)" pod oznaczeniem rozdziału,
        /// „(zawierający art. 25–30 – uchylony)", „(oznaczenie i tytuł oddziału 1 – uchylone)". W DOCX to osobny akapit;
        /// w PDF bez granicy doklejała się do tytułu jednostki, a jednostki za nią zmieniały adres.
        /// </summary>
        private static readonly Regex RepealNoteLinePattern = new(
            @"^\([^()]*(?:uchylon|pominięt|utracił|utraciła|wygasł|skreślon)[^()]*\)$", RegexOptions.Compiled);

        /// <summary>
        /// Poprzedni wiersz domknięty: kończy się interpunkcją — także przed cudzysłowem zamykającym („… 2024 r.”",
        /// koniec przepisu przytoczonego) — albo spójnikiem, którym pozycja wyliczenia przechodzi w następną
        /// („1) zgłoszenia płatnika składek oraz" / „2) …") albo adnotacją jednostki bez treści („5) (uchylony)",
        /// „Art. 7. (pominięty)", „2. (utracił moc)") — taka jednostka jest kompletna, a tekst jednolity ma ich wiele.
        /// Skrót referencyjny wyklucza osobny wzorzec.
        /// </summary>
        private static readonly Regex ClosedLineEndPattern = new(
            $@"(?:[;:,.][{QuotationMarks.ClosingOrStraight}]*|(?:^|\s)(?:oraz|lub|albo)|\((?:uchylon|pominięt|utracił|utraciła|wygasł|skreślon)[^()]{{0,12}}\))$",
            RegexOptions.Compiled);

        private static readonly Regex ReferenceAbbreviationEndPattern = new(
            @"(?:\b(?:poz|[Nn]r|str|art|ust|pkt|lit)\.?|§)\s*$", RegexOptions.Compiled);

        /// <summary>Początek przypisu: „N)" (także w kanale [N)]) lub gwiazdki — nawias WYMAGANY
        /// (bez niego goła liczba „2023 r. …" na początku zawiniętej kontynuacji rozcinałaby przypis).
        /// Odnośnik oznaczony literą („[a)]" — tekst jednolity oznacza tak odnośniki obwieszczenia) tylko w kanale
        /// indeksu górnego: goła „a)" na początku wiersza to litera wyliczenia. Sam nawias w indeksie górnym („[)]")
        /// to znacznik, którego oznaczenia warstwa tekstowa nie zapisała (zdarza się w PDF z LibreOffice).</summary>
        private static readonly Regex FootnoteStartPattern = new(
            @"^(?:\[?\d+\)\]?\s|\[(?:[a-z]{1,2})?\)\]\s?|\*+\s?)", RegexOptions.Compiled);

        /// <summary>Przypis zakończony: ostatni wiersz strefy kończy zdanie — następna strona go nie kontynuuje.</summary>
        private static readonly Regex FootnoteEndPattern = new(
            @"[.;]\)?\s*$", RegexOptions.Compiled);

        /// <summary>Początek przypisu oznaczeniem zapisanym zwykłym tekstem („3) …", „3)…").</summary>
        private static readonly Regex PlainFootnoteStartPattern = new(
            @"^(?<symbol>\d{1,3})\)", RegexOptions.Compiled);

        /// <summary>
        /// Oznaczenie odnośnika na początku wiersza strefy przypisów złożone w indeksie górnym tylko w części: sam numer
        /// („[1]) Minister …") albo sam nawias („1[)] Minister …") — tak wychodzi z szablonu przypisu, w którym numer
        /// i nawias mają różne formatowanie.
        /// </summary>
        private static readonly Regex SplitNoteNumberPattern = new(
            @"^(?:\[(?<symbol>\d{1,3}|[a-z]{1,2})\]\)|(?<symbol>\d{1,3})\s?\[\)\])", RegexOptions.Compiled);

        /// <summary>Wiersz strefy przypisów z oznaczeniem odnośnika w kanale indeksu górnego („[1)] …").</summary>
        private static PdfTextLine WithNoteNumber(PdfTextLine line)
        {
            var split = SplitNoteNumberPattern.Match(line.Text);
            return split.Success
                ? line with { Text = "[" + split.Groups["symbol"].Value + ")]" + line.Text[split.Length..] }
                : line;
        }

        /// <summary>
        /// Oznaczenie odnośnika wydrukowane dwa razy: odwołanie z oznaczeniem własnym („za)") ma w warstwie tekstowej
        /// i znak w indeksie górnym, i ten sam tekst w linii pisma („zm.[za)]za))"), a odnośnik — oba w indeksie
        /// górnym („[zc)zc)] Zmiany …"). Oznaczenie kończy nawias, którego w środku oznaczenia nie ma, więc powtórzenie
        /// jest jednoznaczne.
        /// </summary>
        private static readonly Regex DuplicatedMarkPattern = new(
            @"\[(?<mark>[a-z0-9]{1,3}\))(?:\k<mark>\]|\]\k<mark>)", RegexOptions.Compiled);

        private static string WithoutDuplicatedMarks(string text) =>
            text.Contains(')') ? DuplicatedMarkPattern.Replace(text, "[${mark}]") : text;

        /// <summary>Początek przypisu znacznikiem w indeksie górnym — kanał <c>[N)]</c> albo <c>[*]</c> z <see cref="PdfLineExtractor"/>.</summary>
        private static readonly Regex SuperscriptFootnoteStartPattern = new(
            @"^\[(?:\d+\)?|(?:[a-z]{1,2})?\)|\*+)\]", RegexOptions.Compiled);

        public SourceFormat Format => SourceFormat.Pdf;

        public IReadOnlyList<DocumentBlock> ReadBlocks(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            if (stream.CanSeek)
                stream.Position = 0;

            using var document = OpenDocument(stream);

            int pageCount = document.NumberOfPages;
            if (pageCount == 0)
                return Array.Empty<DocumentBlock>();

            var allLines = new List<PdfTextLine>();
            var pageLetterCounts = new List<int>(pageCount);
            int totalChars = 0, replacementChars = 0;

            for (int i = 1; i <= pageCount; i++)
            {
                var page = document.GetPage(i);
                pageLetterCounts.Add(page.Letters.Count);
                foreach (var letter in page.Letters)
                {
                    totalChars += letter.Value.Length;
                    // Zliczanie per ZNAK — Letter.Value bywa wieloznakowe (ligatury/ToUnicode),
                    // a znak zastępczy może siedzieć wewnątrz takiej wartości.
                    replacementChars += letter.Value.Count(c => c == '�');
                }

                // Łącznik nierozdzielający jak w DOCX (TextNormalizer.NonBreakingHyphen).
                allLines.AddRange(PdfLineExtractor.ExtractLines(page)
                    .Select(line => line with { Text = WithoutDuplicatedMarks(line.Text.Replace(TextNormalizer.NonBreakingHyphen, '-')) }));
            }

            DetectScan(pageLetterCounts, totalChars, replacementChars);

            var lines = PageArtifactFilter.Filter(allLines, pageCount);
            if (lines.Count == 0)
                throw new ScannedPdfException(
                    "Warstwa tekstowa PDF zawiera wyłącznie artefakty stron (nagłówki/paginację) — " +
                    "treść dokumentu jest prawdopodobnie zeskanowana; OCR jest poza zakresem parsera.");

            var docDominantSize = DominantBodySize(lines);
            var (bodyLines, footnoteLines) = SplitFootnoteZone(lines, docDominantSize);

            var dominantLeft = bodyLines.Count > 0
                ? Mode(bodyLines.Select(l => Math.Round(l.StartX)))
                : 0;

            var lastPages = new List<int>();
            var body = AssembleBlocks(bodyLines, docDominantSize, dominantLeft, BlockRole.Body,
                startIndex: 0, forceBoundary: null, lastPages);
            var footnotes = AssembleBlocks(footnoteLines, docDominantSize, dominantLeft, BlockRole.FootnoteText,
                startIndex: body.Count, forceBoundary: l => FootnoteStartPattern.IsMatch(l.Text));

            // Odnośniki przed winietą: winieta scala swoje bloki, a strony bloków (lastPages) idą równolegle do listy.
            var blocks = JournalMasthead.Mark(PdfFootnotes.Link(body, lastPages, footnotes)).ToList();

            blocks.AddRange(footnotes);
            return blocks;
        }

        private static PdfDocument OpenDocument(Stream stream)
        {
            try
            {
                return PdfDocument.Open(stream);
            }
            catch (UglyToad.PdfPig.Exceptions.PdfDocumentEncryptedException ex)
            {
                throw new UnsupportedDocumentFormatException(
                    "Dokument PDF jest zaszyfrowany hasłem — parsowanie niemożliwe bez odszyfrowania.", ex);
            }
            catch (UglyToad.PdfPig.Core.PdfDocumentFormatException ex)
            {
                throw new UnsupportedDocumentFormatException(
                    "Plik PDF jest uszkodzony lub niezgodny ze specyfikacją — nie można odczytać dokumentu.", ex);
            }
        }

        /// <summary>Minimalna średnia liczba liter na NIEPUSTEJ stronie, poniżej której puste strony
        /// wskazują na skan częściowy (a nie np. graficzny załącznik przy pełnotekstowej treści).</summary>
        private const int MinAverageLettersPerNonEmptyPage = 100;

        private static void DetectScan(List<int> pageLetterCounts, int totalChars, int replacementChars)
        {
            int pageCount = pageLetterCounts.Count;
            int totalLetters = pageLetterCounts.Sum();
            int emptyPages = pageLetterCounts.Count(c => c == 0);

            if ((double)totalLetters / pageCount < MinAverageLettersPerPage)
                throw new ScannedPdfException(
                    "PDF nie zawiera użytecznej warstwy tekstowej (prawdopodobnie skan) — " +
                    "wymagany dokument z tekstem; OCR jest poza zakresem parsera.");

            // Puste strony sygnalizują skan częściowy tylko, gdy strony NIEPUSTE też są ubogie w tekst
            // (nagłówki). Akt z pełnym tekstem + wielostronicowym załącznikiem graficznym przechodzi.
            int nonEmptyPages = pageCount - emptyPages;
            if (nonEmptyPages > 0
                && (double)emptyPages / pageCount > MaxEmptyPageRatio
                && (double)totalLetters / nonEmptyPages < MinAverageLettersPerNonEmptyPage)
                throw new ScannedPdfException(
                    "Ponad połowa stron PDF nie zawiera tekstu, a pozostałe są ubogie w treść " +
                    "(prawdopodobnie skan częściowy) — wymagany dokument z pełną warstwą tekstową.");

            if (totalChars > 0 && (double)replacementChars / totalChars > MaxReplacementCharRatio)
                throw new ScannedPdfException(
                    "Warstwa tekstowa PDF zawiera zbyt wiele znaków nieodwzorowanych (uszkodzone kodowanie) — " +
                    "tekst nie nadaje się do parsowania.");
        }

        /// <summary>
        /// Rozmiar fontu TREŚCI dokumentu: NAJWIĘKSZY rozmiar o istotnym udziale znaków (≥ 15%).
        /// Moda po WIERSZACH zawodzi, gdy wiersze przypisów przeważają liczebnie (krótka nowela
        /// z długim odnośnikiem) — dominanta spadałaby do rozmiaru przypisów i wyłączała ich strefę.
        /// Próg 15% odsiewa tytuły/nagłówki (mały udział), a przepuszcza treść nawet przy
        /// dokumencie zdominowanym objętościowo przez odnośniki.
        /// </summary>
        private static double DominantBodySize(List<PdfTextLine> lines)
        {
            var weights = lines
                .GroupBy(l => l.DominantSize)
                .Select(g => (Size: g.Key, Weight: g.Sum(l => l.Text.Length)))
                .ToList();
            var totalWeight = weights.Sum(w => w.Weight);

            var substantial = weights.Where(w => w.Weight >= 0.15 * totalWeight).ToList();
            return substantial.Count > 0
                ? substantial.Max(w => w.Size)
                : weights.OrderByDescending(w => w.Weight).First().Size;
        }

        /// <summary>
        /// Wydziela strefę przypisów: na każdej stronie spójny blok wierszy NA DOLE strony pisany
        /// mniejszym fontem (≤ 0,85× dominanty dokumentu), którego pierwszy wiersz wygląda jak
        /// początek przypisu („N)") ALBO który kontynuuje przypis z poprzedniej strony (odnośnik
        /// wykazu zmian TJ regularnie zajmuje 2+ stron, a jego kontynuacja zaczyna się środkiem
        /// zdania). Gdy takiej strefy nie ma, druga próba przyjmuje font bliski treści
        /// (≤ <see cref="CloseFootnoteSizeRatio"/>), ale tylko ze znacznikiem w indeksie górnym albo jako kontynuację
        /// przypisu, który na poprzedniej stronie urwał się w pół zdania — strona załącznika złożona w całości takim
        /// fontem (tabela) nie jest kontynuacją przypisu, który się skończył.
        /// Przypisy wracają osobną listą (emisja na końcu dokumentu).
        /// </summary>
        private static (List<PdfTextLine> Body, List<PdfTextLine> Footnotes) SplitFootnoteZone(
            List<PdfTextLine> lines, double docDominantSize)
        {
            var body = new List<PdfTextLine>();
            var footnotes = new List<PdfTextLine>();
            bool previousPageHadFootnotes = false;
            bool previousFootnoteOpen = false;

            foreach (var pageGroup in lines.GroupBy(l => l.PageNumber).OrderBy(g => g.Key))
            {
                // Wiersze strony w porządku czytania (Y malejąco) — sufiks małego fontu od dołu strony.
                var pageLines = pageGroup.OrderByDescending(l => l.BaselineY).ToList();
                var noteLines = pageLines.Select(WithNoteNumber).ToList();

                var continued = previousPageHadFootnotes;
                var continuedMidSentence = previousFootnoteOpen;
                var (zoneStart, hasZone) = FindFootnoteZone(noteLines, FootnoteSizeRatio * docDominantSize,
                    line => FootnoteStartPattern.IsMatch(line.Text) || continued);
                if (!hasZone)
                    (zoneStart, hasZone) = FindFootnoteZone(noteLines, CloseFootnoteSizeRatio * docDominantSize,
                        line => SuperscriptFootnoteStartPattern.IsMatch(line.Text) || continuedMidSentence
                            || IsReferencedOnPage(line, noteLines));

                // Wiersze strefy niosą oznaczenie w kanale indeksu górnego; treść strony — tak, jak ją złożono.
                if (hasZone)
                {
                    body.AddRange(pageLines.Take(zoneStart));
                    footnotes.AddRange(noteLines.Skip(zoneStart));
                }
                else
                {
                    body.AddRange(pageLines);
                }

                previousPageHadFootnotes = hasZone;
                previousFootnoteOpen = hasZone && !FootnoteEndPattern.IsMatch(pageLines[^1].Text);
            }

            return (body, footnotes);
        }

        /// <summary>
        /// Odnośnik z oznaczeniem bez indeksu górnego („3) Rozporządzenie …" — część składów tak numeruje odnośniki):
        /// przyjęty, gdy treść tej samej strony ma odwołanie o tym oznaczeniu w indeksie górnym („…2017/625[3)]").
        /// Samo „N)" mniejszym fontem to także wiersz tabeli; odwołanie na stronie jest drugim sygnałem.
        /// </summary>
        private static bool IsReferencedOnPage(PdfTextLine line, List<PdfTextLine> pageLines)
        {
            var start = PlainFootnoteStartPattern.Match(line.Text);
            if (!start.Success)
                return false;

            var symbol = start.Groups["symbol"].Value;
            return pageLines.Any(other => !ReferenceEquals(other, line) && PdfFootnotes.ReferencedSymbols(other.Text).Contains(symbol));
        }

        /// <summary>
        /// Strefa przypisów strony: najdłuższy sufiks wierszy o foncie nie większym niż <paramref name="maxSize"/>,
        /// przyjęty, gdy jego pierwszy wiersz spełnia <paramref name="opens"/>.
        /// </summary>
        private static (int Start, bool Found) FindFootnoteZone(
            List<PdfTextLine> pageLines, double maxSize, Func<PdfTextLine, bool> opens)
        {
            int start = pageLines.Count;
            while (start > 0 && pageLines[start - 1].DominantSize <= maxSize)
                start--;

            return start < pageLines.Count && opens(pageLines[start]) ? (start, true) : (pageLines.Count, false);
        }

        /// <summary>
        /// Skleja wiersze w bloki-akapity. Granica bloku: marker bezwarunkowy / marker warunkowy po
        /// wierszu domkniętym interpunkcją / odstęp pionowy &gt; 1,9× typowej interlinii / predykat
        /// wymuszony (przypisy). Granica STRONY nie jest granicą bloku (jednostka przecięta stroną
        /// się skleja). CELOWO bez granicy na skoku wcięcia: skład Dziennika Ustaw używa wcięcia
        /// WISZĄCEGO (marker przy marginesie, kontynuacje głębiej) — granica wcięciowa cięłaby każdy
        /// zawinięty punkt/literę/tiret i gubiła tekst. Dywiz na końcu wiersza: sklejenie bezpośrednie
        /// z ZACHOWANIEM dywizu (decyzja użytkownika — patrz komentarz przy sklejaniu).
        /// </summary>
        /// <param name="lastPages">Gdy podana — dostaje stronę ostatniego wiersza każdego bloku (blok bywa przecięty stroną).</param>
        private static List<DocumentBlock> AssembleBlocks(
            List<PdfTextLine> lines, double emSize, double dominantLeft, BlockRole role,
            int startIndex, Func<PdfTextLine, bool>? forceBoundary, List<int>? lastPages = null)
        {
            var blocks = new List<DocumentBlock>();
            if (lines.Count == 0)
                return blocks;

            var typicalGap = TypicalLineGap(lines, emSize);

            var buffer = new StringBuilder();
            PdfTextLine? blockFirst = null;
            PdfTextLine? prev = null;
            var titleZone = TitleZone.None;

            void FlushBlock()
            {
                if (blockFirst == null || buffer.Length == 0)
                    return;
                lastPages?.Add(prev?.PageNumber ?? blockFirst.PageNumber);
                blocks.Add(new DocumentBlock
                {
                    Text = titleZone == TitleZone.Kind ? WithTitleCase(buffer.ToString()) : buffer.ToString(),
                    Role = role,
                    Layout = new BlockLayoutInfo
                    {
                        LeftIndentTwips = (int)Math.Round(Math.Max(0, blockFirst.StartX - dominantLeft) * PointsToTwips),
                        FontSizeHalfPoints = blockFirst.DominantSize * 2,
                    },
                    Source = new BlockSourceLocation
                    {
                        BlockIndex = startIndex + blocks.Count,
                        PageNumber = blockFirst.PageNumber,
                    },
                });
                buffer.Clear();
                blockFirst = null;
            }

            foreach (var line in lines)
            {
                bool boundary = prev == null
                    || (forceBoundary?.Invoke(line) ?? IsMarkerBoundary(line, prev))
                    || DesignationLinePattern.IsMatch(prev.Text)
                    || (forceBoundary is null && RepealNoteLinePattern.IsMatch(line.Text.Trim()))
                    || (forceBoundary is null && IsTitleZoneBoundary(line, titleZone))
                    || (forceBoundary is null && IsClosingLineBoundary(line, prev, dominantLeft))
                    || (forceBoundary is null && AnnouncementCommonPartPattern.IsMatch(line.Text))
                    || (line.PageNumber == prev.PageNumber
                        && prev.BaselineY - line.BaselineY > ParagraphGapRatio * typicalGap)
                    || (forceBoundary is null && line.PageNumber == prev.PageNumber
                        && prev.BaselineY - line.BaselineY > ParagraphSpacingRatio * typicalGap
                        && !IsTabular(prev) && !IsTabular(line));

                if (boundary)
                {
                    FlushBlock();
                    blockFirst = line;
                    buffer.Append(line.Text);
                    titleZone = forceBoundary is not null ? TitleZone.None : NextTitleZone(line, titleZone);
                }
                else
                {
                    // Kontynuacja akapitu: wiersz zakończony dywizem skleja się BEZPOŚREDNIO (dywiz zostaje).
                    // DECYZJA UŻYTKOWNIKA (2026-07-19, zamiast dehyfenacji z planu): bez słownika nie da się
                    // odróżnić dywizu złożenia („społeczno-gospodarczy") od dzielenia wyrazów składem
                    // („posta-nowienia"); usuwanie scala złożenia NIEODWRACALNIE, zachowanie zostawia
                    // widoczny i odwracalny artefakt — mniejsza szkoda dla wierności tekstu prawnego.
                    if (buffer.Length > 0 && buffer[^1] == '-')
                    {
                        buffer.Append(line.Text);
                    }
                    else
                    {
                        buffer.Append(' ');
                        buffer.Append(line.Text);
                    }
                }

                prev = line;
            }

            FlushBlock();
            return blocks;
        }

        /// <summary>
        /// Granica w strefie tytułowej: data aktu zamyka blok nagłówka rodzaju (i organu), za datą zaczyna się przedmiot,
        /// a przedmiot kończy początek treści (<see cref="SubjectEndPattern"/>).
        /// </summary>
        private static bool IsTitleZoneBoundary(PdfTextLine line, TitleZone zone) => zone switch
        {
            TitleZone.Kind => ActDateLinePattern.IsMatch(line.Text),
            TitleZone.Date => true,
            TitleZone.Subject => SubjectEndPattern.IsMatch(line.Text),
            _ => false,
        };

        /// <summary>
        /// Granica wierszy końcowych aktu: podpis i wiersz przyłączenia („Załącznik do …") stoją po prawej stronie
        /// i są osobnymi akapitami — bez granicy doklejały się do ostatniego przepisu (podpis), a akt ogłaszany
        /// w załączniku do obwieszczenia nie miał wiersza, który go otwiera. Wymagane oba sygnały: treść i położenie.
        /// Podpis rozpoznaje ta sama formuła co parser (<see cref="ActSignatureParser"/>, także „wz." i „z up.") —
        /// blok, którego parser nie uzna za podpis, nic by nie dał. Za podpisem zaczyna się nowy blok.
        /// </summary>
        private static bool IsClosingLineBoundary(PdfTextLine line, PdfTextLine? prev, double dominantLeft)
        {
            if (prev is not null && IsSignature(prev) && IsRightSide(prev, dominantLeft))
                return true;

            if (prev is not null && AnnexDesignationLinePattern.IsMatch(prev.Text) && IsRightSide(prev, dominantLeft))
                return true;

            return IsRightSide(line, dominantLeft)
                && (IsSignature(line) || AttachmentLinePattern.IsMatch(line.Text) || AnnexDesignationLinePattern.IsMatch(line.Text));
        }

        // Tekst wiersza jak w parserze (Sanitize) — PDF przynosi twarde spacje (U+00A0) w nazwie organu.
        private static bool IsSignature(PdfTextLine line) => ActSignatureParser.TryParse(line.Text.Sanitize()) is not null;

        private static bool IsRightSide(PdfTextLine line, double dominantLeft) =>
            line.StartX - dominantLeft >= ClosingLineMinIndentPoints;

        /// <summary>Wyrazy, które w nazwie rodzaju i organu stoją małą literą („Ministra Spraw Wewnętrznych i Administracji").</summary>
        private static readonly HashSet<string> LowercaseTitleWords = ["i", "oraz", "w", "z", "ze", "do", "na", "dla", "nr", "o", "od", "po", "przy"];

        // Wyraz poza nawiasem kwadratowym — znacznik odnośnika („[a)]") zostaje, jak stoi.
        private static readonly Regex TitleWord = new(@"(?<!\[[^\[\]]*)\p{L}+", RegexOptions.Compiled);

        /// <summary>
        /// Nagłówek rodzaju i organ zapisane w PDF wielkimi literami („ROZPORZĄDZENIE MINISTRA FINANSÓW I GOSPODARKI")
        /// — tak drukuje je skład, a tekst aktu ma „Rozporządzenie" i „Ministra Finansów i Gospodarki" (DOCX zapisuje wielkie
        /// litery formatowaniem, przykłady autorów schematu mają „Ustawa"). Wielkość liter wraca do tej postaci: rodzaj
        /// wielką literą na początku, w organie każdy wyraz wielką literą poza spójnikami i przyimkami. Wiersz, który
        /// ma już małe litery, zostaje bez zmian.
        /// </summary>
        private static string WithTitleCase(string text)
        {
            if (TitleWord.Matches(text).Any(word => word.Value.Any(char.IsLower)))
                return text;

            var first = true;
            var afterDo = false;
            return TitleWord.Replace(text, match =>
            {
                var lower = match.Value.ToLowerInvariant();
                var keepLower = !first && (LowercaseTitleWords.Contains(lower) || (afterDo && lower == "spraw"));
                afterDo = lower == "do";
                first = false;
                return keepLower ? lower : char.ToUpperInvariant(lower[0]) + lower[1..];
            });
        }

        private static bool IsTabular(PdfTextLine line) => line.MaxGap > ColumnGapEm * line.DominantSize;

        /// <summary>
        /// Część wspólna wyliczenia aktów zmieniających w obwieszczeniu tekstu jednolitego — stała formuła („oraz zmian
        /// wynikających z przepisów ogłoszonych przed dniem …", art. 16 ustawy o ogłaszaniu aktów normatywnych). Zaczyna
        /// się małą literą, bez myślnika, w zwykłej interlinii pod ostatnim podpunktem, więc ani marker, ani odstęp jej nie
        /// oddzielają; rozpoznaje ją jej brzmienie.
        /// </summary>
        private static readonly Regex AnnouncementCommonPartPattern = new(
            @"^oraz\s+zmian\s+wynikających\s+z\s+prze", RegexOptions.Compiled);

        /// <summary>Miejsce w strefie tytułowej bloku, który zaczyna się wierszem <paramref name="line"/>.</summary>
        private static TitleZone NextTitleZone(PdfTextLine line, TitleZone previous)
        {
            if (KindHeaderPattern.IsMatch(line.Text))
                return TitleZone.Kind;
            if (previous == TitleZone.Kind)
            {
                // Organ odsunięty od nagłówka rodzaju („MARSZAŁKA SEJMU …") należy jeszcze do niego.
                if (ActDateLinePattern.IsMatch(line.Text))
                    return TitleZone.Date;
                if (IssuerLinePattern.IsMatch(line.Text))
                    return TitleZone.Kind;
            }

            return previous == TitleZone.Date ? TitleZone.Subject : TitleZone.None;
        }

        /// <summary>
        /// Granica na markerze jednostki: bezwarunkowa (Art./§/tiret/litera/nagłówki) albo warunkowa
        /// (cyfrowa „N)/N.", cytat „) — ta druga tylko po wierszu domkniętym interpunkcją i niekończącym
        /// się skrótem referencyjnym („poz.", „nr", „art."…), by zawinięcie zdania nie tworzyło
        /// fałszywego punktu/cytatu.
        /// </summary>
        private static bool IsMarkerBoundary(PdfTextLine line, PdfTextLine prev)
        {
            if (UnconditionalMarkerPattern.IsMatch(line.Text))
                return true;

            if (!ConditionalMarkerPattern.IsMatch(line.Text))
                return false;

            var prevText = prev.Text.TrimEnd();
            return (ClosedLineEndPattern.IsMatch(prevText) && !ReferenceAbbreviationEndPattern.IsMatch(prevText))
                || IsUppercaseHeading(prevText);
        }

        /// <summary>
        /// Wiersz wielkimi literami — tytuł załącznika, tabeli, jednostki („DROGI KRAJOWE … ELEKTRONICZNĄ"). Kończy się bez
        /// znaku, a jednak jest domknięty: wyliczenie pod nim zaczyna nowy blok. Zdanie treści wielkimi literami nie bywa.
        /// </summary>
        private static bool IsUppercaseHeading(string text)
        {
            var letters = BracketContent.Replace(text, string.Empty).Where(char.IsLetter).ToList();
            return letters.Count >= 3 && !letters.Any(char.IsLower);
        }

        private static readonly Regex BracketContent = new(@"\[[^\[\]]*\]", RegexOptions.Compiled);

        /// <summary>Typowa interlinia: mediana dodatnich odstępów Y kolejnych wierszy tej samej strony.</summary>
        private static double TypicalLineGap(List<PdfTextLine> lines, double emSize)
        {
            var gaps = new List<double>();
            for (int i = 1; i < lines.Count; i++)
            {
                if (lines[i].PageNumber != lines[i - 1].PageNumber)
                    continue;
                var gap = lines[i - 1].BaselineY - lines[i].BaselineY;
                if (gap > 0)
                    gaps.Add(gap);
            }

            if (gaps.Count == 0)
                return emSize * 1.2;

            gaps.Sort();
            return gaps[gaps.Count / 2];
        }

        private static double Mode(IEnumerable<double> values) =>
            values.GroupBy(v => v)
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Key)
                .First().Key;
    }
}
