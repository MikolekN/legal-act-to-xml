using System.Text.RegularExpressions;
using Saga.Core.Helpers;
using Saga.Core.Services.Classify.Document;
using Saga.Model;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Zbiera metadane aktu ze strefy tytułowej — akapitów sprzed pierwszego artykułu (§ 16-19, § 102, § 120 ZTP):
    /// rodzaj (nagłówek USTAWA/ROZPORZĄDZENIE/OBWIESZCZENIE/UCHWAŁA/ZARZĄDZENIE), organ wydający, datę i przedmiot.
    /// Karmiony z <see cref="StructureProcessor"/> akapitami nierozpoznanymi (Unknown) przed pierwszym artykułem.
    ///
    /// Strefa tytułowa jest OGRANICZONA i kolektor pieczętuje się SAM (niezależnie od sygnału artykułu):
    /// - rusza dopiero na nagłówku rodzaju (proza przed nim jest ignorowana);
    /// - po nagłówku przyjmuje wyłącznie organ / datę / przedmiot; przedmiot (§ 18-19) jest komponentem
    ///   końcowym tytułu, więc go domyka;
    /// - drugi nagłówek rodzaju (np. „USTAWA" w załączniku tekstu jednolitego, § 102) NIE jest scalany —
    ///   domyka strefę i wraca do wywołującego (należy do treści/załącznika);
    /// - pierwszy wiersz spoza strefy tytułowej (np. „Na podstawie art. …", „§ 1. …") również domyka.
    /// To ogranicza okno zbierania także dla aktów opartych na „§" (rozporządzenia/uchwały), gdzie żaden wiersz
    /// nie jest klasyfikowany jako artykuł i sygnał <see cref="Seal"/> ze <see cref="StructureProcessor"/> nie nadchodzi.
    ///
    /// Tytuł składany jest WERBATIM z rozpoznanych wierszy (zasada „nie przeinaczyć"); datę wydobywa dodatkowo
    /// w postaci strukturalnej (<see cref="LegalDocument.ActDate"/>).
    ///
    /// Wiersz rozpoznaje wzorzec tekstowy; gdy tekst nie ma wzorcowej postaci („rozporządzenie" małymi literami,
    /// „ROZPORZĄDZENIE MINISTRA zDROWIA", tytuł „uchylające …"), rozstrzyga drugi sygnał: styl wiersza
    /// (<c>OZNRODZAKTU…</c>, <c>TEKSTOBWIESZCZENIE…</c>, <c>TYTUAKTU…</c>) albo miejsce — pierwszy wiersz aktu ogłaszanego w załączniku
    /// do obwieszczenia (<see cref="ExpectKindFirst"/>). Sam styl bez słowa rodzaju nagłówka nie czyni.
    /// </summary>
    public sealed class DocumentMetadataCollector
    {
        // Wiersz rodzaju aktu i wiersz rodzaju obwieszczenia tekstu jednolitego („Obwieszczenie", organ).
        private static readonly string[] KindLineStyles = ["OZNRODZAKTU", "TEKSTOBWIESZCZENIE"];
        private const string SubjectStyle = "TYTUAKTU";

        /// <summary>
        /// Pierwszy obserwowany wiersz stoi w miejscu nagłówka rodzaju (akt w załączniku do obwieszczenia) —
        /// słowo rodzaju w dowolnej pisowni wystarcza wtedy za nagłówek.
        /// </summary>
        public bool ExpectKindFirst { get; init; }

        private bool _anyObserved;
        private readonly List<string> _titleParts = new();
        private string _kindLine = string.Empty;
        private string _issuerLine = string.Empty;
        private string _dateLine = string.Empty;
        private string _subject = string.Empty;
        private DateTime? _actDate;
        private bool _kindSeen;
        private bool _sealed;

        /// <summary>Czy strefa tytułowa doszła do przedmiotu aktu — za nim stoi już treść: preambuła albo przepisy.</summary>
        public bool HasSubject => _subject.Length > 0;

        /// <summary>Liczba zebranych wierszy metadanych (diagnostyka/testy).</summary>
        public int CollectedLineCount => _titleParts.Count;

        /// <summary>
        /// Obserwuje nierozpoznany akapit sprzed pierwszego artykułu. Zwraca true, gdy wiersz uznano
        /// za metadaną (wywołujący pomija wtedy ostrzeżenie „pominięty akapit nierozpoznany").
        /// </summary>
        /// <param name="styleId">Styl akapitu — drugi sygnał dla wiersza, którego tekst nie ma wzorcowej postaci.</param>
        public bool Observe(string text, string? styleId = null)
        {
            if (_sealed || string.IsNullOrWhiteSpace(text))
                return false;

            var t = text.Trim();
            var first = !_anyObserved;
            _anyObserved = true;

            if (!_kindSeen)
            {
                // Strefa tytułowa rusza dopiero na nagłówku rodzaju — inaczej proza sprzed aktu trafiłaby do tytułu.
                if (IsKindHeader(t) || IsKindBySecondSignal(t, styleId, first && ExpectKindFirst))
                {
                    _kindSeen = true;
                    _titleParts.Add(t);

                    // Organ w wierszu nagłówka („ROZPORZĄDZENIE MINISTRA FINANSÓW"): wiersz zostaje w całości
                    // nagłówkiem rodzaju, a organ jest dodatkowo znany osobno — eksport rozdziela je tam,
                    // gdzie korzeń ma miejsce na organ.
                    _kindLine = t;
                    if (ZtpPatterns.KindWithIssuerPattern.Match(t) is { Success: true } sameLine)
                        _issuerLine = sameLine.Groups["issuer"].Value;
                    return true;
                }
                return false;
            }

            // Drugi nagłówek rodzaju (np. „USTAWA" w załączniku tekstu jednolitego) — nie scalaj dwóch aktów.
            if (IsKindHeader(t))
            {
                Seal();
                return false;
            }

            // Organ wydający w osobnym wierszu (§ 120 ust. 4 — rozporządzenie; § 102 — „Marszałka Sejmu…").
            // Akt wydany wspólnie ma go w dwóch wierszach („MINISTRÓW INFRASTRUKTURY" / „ORAZ SPRAW WEWNĘTRZNYCH
            // I ADMINISTRACJI") — drugi dołącza do pierwszego.
            if (IsIssuingOrgan(t) || IsIssuerByPosition(t))
            {
                _titleParts.Add(t);
                _issuerLine = _issuerLine.Length == 0 ? t : _issuerLine + " " + t;
                return true;
            }

            // Data aktu (§ 17): „z dnia D miesiąca RRRR r."
            var dm = ZtpPatterns.ActDateLinePattern.Match(t);
            if (dm.Success)
            {
                _titleParts.Add(t);
                if (_dateLine.Length == 0)
                    _dateLine = t;
                TryCaptureDate(dm.Groups["day"].Value, dm.Groups["month"].Value, dm.Groups["year"].Value);
                return true;
            }

            // Przedmiot aktu (§ 18-19 / § 120 ust. 6) — komponent końcowy tytułu; domyka strefę tytułową.
            // Tytuł o nietypowym początku rozpoznaje styl tytułu aktu.
            if (ZtpPatterns.ActSubjectPattern.IsMatch(t) || HasStyle(styleId, SubjectStyle))
            {
                _titleParts.Add(t);
                _subject = t;
                Seal();
                return true;
            }

            // Pierwszy wiersz spoza strefy tytułowej kończy zbieranie (ogranicza okno także dla aktów „§").
            Seal();
            return false;
        }

        /// <summary>Pieczętuje kolektor — dalsze <see cref="Observe"/> zwracają false. Idempotentne.</summary>
        public void Seal() => _sealed = true;

        /// <summary>
        /// Zapisuje zebrane metadane do dokumentu (przy finalizacji parsowania). Nie nadpisuje wartości
        /// już ustawionych (np. przez ścieżkę stylową/inny kanał).
        /// </summary>
        public void ApplyTo(LegalDocument document)
        {
            if (_titleParts.Count > 0 && string.IsNullOrEmpty(document.Title))
                document.Title = string.Join(" ", _titleParts);

            if (_actDate.HasValue && !document.ActDate.HasValue)
                document.ActDate = _actDate;

            if (document.KindLine.Length == 0) document.KindLine = _kindLine;
            if (document.IssuerLine.Length == 0) document.IssuerLine = _issuerLine;
            if (document.DateLine.Length == 0) document.DateLine = _dateLine;
            if (document.Subject.Length == 0) document.Subject = _subject;
        }

        /// <summary>
        /// Zapisuje zebraną strefę tytułową jako nagłówek aktu ogłaszanego w załączniku do obwieszczenia —
        /// nagłówek dokumentu pozostaje nagłówkiem obwieszczenia.
        /// </summary>
        public void ApplyTo(Announcement announcement)
        {
            announcement.ActKindLine = _kindLine;
            announcement.ActIssuerLine = _issuerLine;
            announcement.ActDateLine = _dateLine;
            announcement.ActSubject = _subject;
        }

        private void TryCaptureDate(string day, string month, string year)
        {
            if (_actDate.HasValue)
                return;
            if (!PolishDates.TryMonth(month, out var mo) ||
                !int.TryParse(day, out var d) ||
                !int.TryParse(year, out var y))
                return;

            // Niepoprawna data (np. 31 lutego) — pomijamy datę strukturalną, wiersz zostaje w tytule.
            if (PolishDates.TryBuild(y, mo, d, out var parsed))
                _actDate = parsed;
        }

        // Odnośnik przy nagłówku („OBWIESZCZENIE MINISTRA …[1)]") nie zmienia tego, że to nagłówek rodzaju.
        private static bool IsKindHeader(string line)
        {
            var t = FootnoteMarkers.Remove(line);
            return ZtpPatterns.StatuteHeaderPattern.IsMatch(t) ||
                ZtpPatterns.RegulationHeaderPattern.IsMatch(t) ||
                ZtpPatterns.AnnouncementHeaderPattern.IsMatch(t) ||
                ZtpPatterns.ResolutionHeaderPattern.IsMatch(t) ||
                ZtpPatterns.OrderHeaderPattern.IsMatch(t);
        }

        /// <summary>
        /// Wiersz zaczynający się słowem rodzaju w pisowni spoza wzorca jest nagłówkiem, gdy potwierdza to drugi
        /// sygnał: styl wiersza rodzaju albo miejsce pierwszego wiersza aktu ogłaszanego w załączniku.
        /// </summary>
        private static bool IsKindBySecondSignal(string line, string? styleId, bool expectedHere) =>
            (expectedHere || KindLineStyles.Any(style => HasStyle(styleId, style)))
            && ZtpPatterns.ActKindKeywordPattern.IsMatch(FootnoteMarkers.Remove(line));

        private static bool HasStyle(string? styleId, string prefix) =>
            styleId is not null && styleId.StartsWith(prefix, StringComparison.Ordinal);

        private static bool IsIssuingOrgan(string t) =>
            ZtpPatterns.IssuingOrganLinePattern.IsMatch(t) ||
            ZtpPatterns.MarshalOfSejmIssuerPattern.IsMatch(t);

        /// <summary>Drugi wiersz organu aktu wydanego wspólnie zaczyna się spójnikiem („ORAZ SPRAW WEWNĘTRZNYCH …").</summary>
        private static readonly Regex IssuerContinuation = new(@"^(?:oraz|i)\s", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Początek wiersza, który organem na pewno nie jest: podstawa prawna albo jednostka.</summary>
        private static readonly Regex NotAnIssuer = new(@"^(?:Na\s+podstawie\b|Art\.|§|\d)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Między nagłówkiem rodzaju a datą stoi w strefie tytułowej tylko organ wydający — w dowolnej pisowni
        /// („Ministra Finansów", „MINISTRa transportu…") i z odnośnikiem („Ministra Zdrowia[1)]"). Ustawa nie ma
        /// tu nic: po „USTAWA" stoi od razu data.
        /// </summary>
        private bool IsIssuerByPosition(string t)
        {
            if (_dateLine.Length > 0 || t.Length > 200)
                return false;

            if (_issuerLine.Length > 0 && !IssuerContinuation.IsMatch(t))
                return false;

            if (ZtpPatterns.ActDateLinePattern.IsMatch(t) || ZtpPatterns.ActSubjectPattern.IsMatch(t) || NotAnIssuer.IsMatch(t))
                return false;

            var name = FootnoteMarkers.Remove(t).TrimEnd();
            return name.Length > 0 && char.IsLetter(name[0]) && name[^1] is not ('.' or ':' or ';');
        }
    }
}
