using System.Text.RegularExpressions;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Bilans cudzysłowów treści nowelizacji bez stylów Z/* (Etap 8, § 94 ZTP: nowe brzmienie
    /// jest ujęte w cudzysłów „…"). Wyznacza granicę końca cytowanej treści tam, gdzie ścieżka
    /// stylowa ma styl ustawy matki, a bezstylowa dotąd nie miała żadnego sygnału.
    ///
    /// Cykl życia (w obrębie jednego zbierania <see cref="AmendmentCollector"/>):
    /// - UZBROJENIE: pierwszy zbierany blok jest bezstylowy i zaczyna się od „ (U+201E) — także wtedy,
    ///   gdy cudzysłów stoi w indeksie górnym („[„2)] …", cytowany odnośnik); w przeciwnym razie tracker
    ///   trwale rozbrojony — obowiązuje dotychczasowe zachowanie (wyjście na stylu ustawy matki /
    ///   kolejnym triggerze / końcu dokumentu).
    /// - KAŻDY blok ze stylem rozbraja trwale — cyklem nowelizacji stylowej rządzą style (doc001 bez zmian).
    /// - BILANS: „ (U+201E) otwiera; zamykają znaki z <see cref="QuotationMarks.Closing"/>
    ///   (głębokość nie schodzi poniżej zera). Cytaty zagnieżdżone „…„…"…" utrzymują Depth &gt; 0.
    ///   Prosty cudzysłów " (U+0022) jest DWUZNACZNY (otwiera i zamyka): pary wewnątrz bloku
    ///   („nazwa "programu"") są neutralne; dopiero NIEPARZYSTA liczba prostych cudzysłowów w bloku
    ///   zakończonym prostym cudzysłowem liczy się jako jedno zamknięcie (konwencja mieszana „…").
    /// - ZAMKNIĘCIE: Depth == 0 i blok kończy się cudzysłowem zamykającym, z opcjonalną interpunkcją
    ///   „…";  „…".  „…") → <see cref="ClosureReached"/>; orkiestrator finalizuje nowelizację.
    /// - PRZECINEK po cudzysłowie („…",) nie rozstrzyga sam: w komendzie mnogiej („art. 5 i 6 otrzymują
    ///   brzmienie:") następuje kolejna cytowana jednostka, a w wyliczeniu zmian — następna litera aktu
    ///   („a) ust. 1 otrzymuje brzmienie: „…", b) w ust. 2 …"). Rozstrzyga początek następnego bloku:
    ///   <see cref="ClosesBefore"/> — cytat trwa tylko wtedy, gdy blok zaczyna się od „.
    /// - Trigger nowelizacyjny przy Depth &gt; 0 to komenda ZAGNIEŻDŻONA (ZZ) — cytat trwa,
    ///   a fakt odnotowuje <see cref="MarkNestedTrigger"/> (Warning przy finalizacji).
    ///
    /// Ograniczenie (udokumentowane): dokument cytujący wyłącznie prostym " (bez „) nie uzbraja
    /// trackera — degradacja do dotychczasowego zachowania zamiast zgadywania granic.
    /// </summary>
    public sealed class QuoteBalanceTracker
    {
        /// <summary>
        /// Cudzysłów zamykający na końcu bloku (typograficzny lub prosty), z opcjonalnym nawiasem
        /// i interpunkcją kończącą (; .) po nim. Celowo BEZ przecinka — „…", kontynuuje komendę mnogą.
        /// </summary>
        private static readonly Regex ClosingQuoteAtEnd = new(
            $@"[{QuotationMarks.ClosingOrStraight}]\s*\)?\s*[;.]?$", RegexOptions.Compiled);

        /// <summary>Prosty cudzysłów " na końcu bloku (z opcjonalnym nawiasem/interpunkcją) — kandydat zamknięcia konwencji mieszanej.</summary>
        private static readonly Regex StraightQuoteAtEnd = new(
            @"""\s*\)?\s*[;.]?$", RegexOptions.Compiled);

        /// <summary>Cudzysłów zamykający z przecinkiem na końcu bloku — zamknięcie zależne od następnego bloku.</summary>
        private static readonly Regex ClosingQuoteWithComma = new(
            $@"[{QuotationMarks.ClosingOrStraight}]\s*\)?\s*,$", RegexOptions.Compiled);

        /// <summary>
        /// Cudzysłów otwierający na początku bloku; nawias kwadratowy przed nim to indeks górny w kanale
        /// tekstu (znak odnośnika sformatowany razem z cudzysłowem).
        /// </summary>
        private static readonly Regex OpeningQuoteAtStart = new(
            $@"^\s*\[?{QuotationMarks.Opening}", RegexOptions.Compiled);

        /// <summary>Czy blok zaczyna się od cudzysłowu otwierającego — początek treści cytowanej.</summary>
        public static bool OpensQuote(string text) => OpeningQuoteAtStart.IsMatch(text);

        /// <summary>
        /// Czy blok zamyka cytat, którego otwarcia nie widać: kończy się cudzysłowem zamykającym i ma w sobie
        /// więcej zamknięć niż otwarć. Wyrażenie w cudzysłowie wewnątrz bloku („zwany dalej „ministrem".")
        /// jest sparowane i cytatu nie zamyka.
        /// </summary>
        public static bool ClosesUnopenedQuote(string text)
        {
            var trimmed = text.TrimEnd();
            if (!ClosingQuoteAtEnd.IsMatch(trimmed))
                return false;

            var open = 0;
            foreach (var character in trimmed)
            {
                if (character == QuotationMarks.Opening)
                    open++;
                else if (QuotationMarks.IsClosing(character))
                    open--;
            }

            return open < 0;
        }

        private static bool EndsWithStraightQuote(string trimmed) => StraightQuoteAtEnd.IsMatch(trimmed);

        private bool _first = true;
        private bool _disarmed;

        /// <summary>Czy tracker jest uzbrojony (pierwszy blok bezstylowy zaczynał się od „).</summary>
        public bool IsArmed { get; private set; }

        /// <summary>Bieżąca głębokość cudzysłowów po ostatnim obserwowanym bloku (≥ 0).</summary>
        public int Depth { get; private set; }

        /// <summary>Czy ostatni obserwowany blok domknął cytat (Depth == 0 i kończy się cudzysłowem zamykającym).</summary>
        public bool ClosureReached { get; private set; }

        /// <summary>
        /// Czy ostatni obserwowany blok zamknął cudzysłów przecinkiem („…",) przy Depth == 0 — cytat jest
        /// domknięty, o ile następny blok nie otwiera kolejnego.
        /// </summary>
        public bool CommaClosurePending { get; private set; }

        /// <summary>
        /// Czy cytat zamknięty przecinkiem kończy się przed blokiem o podanym tekście: tak, jeśli blok nie
        /// zaczyna się od cudzysłowu otwierającego (kolejna cytowana jednostka komendy mnogiej).
        /// </summary>
        public bool ClosesBefore(string nextBlockText) =>
            CommaClosurePending && !OpensQuote(nextBlockText);

        /// <summary>Czy wewnątrz otwartego cytatu (Depth &gt; 0) — tłumi wyjście na fałszywym triggerze.</summary>
        public bool IsInsideQuote => IsArmed && Depth > 0;

        /// <summary>Czy wewnątrz cytatu stłumiono komendę nowelizacyjną (zagnieżdżona ZZ — nierozkładalna bez stylów).</summary>
        public bool NestedTriggerSuppressed { get; private set; }

        /// <summary>
        /// Obserwuje kolejny zbierany blok treści nowelizacji.
        /// <paramref name="hasStyle"/> = blok niesie styl akapitu (rozbraja tracker na stałe).
        /// </summary>
        public void Observe(string text, bool hasStyle)
        {
            ClosureReached = false;
            CommaClosurePending = false;

            if (_disarmed)
                return;

            if (hasStyle)
            {
                Disarm();
                return;
            }

            if (_first)
            {
                _first = false;
                if (!OpensQuote(text))
                {
                    Disarm();
                    return;
                }
                IsArmed = true;
            }

            int straightQuotes = 0;
            foreach (var c in text)
            {
                if (c == QuotationMarks.Opening)
                    Depth++;
                else if (QuotationMarks.IsClosing(c))
                    Depth = System.Math.Max(0, Depth - 1);
                else if (c == QuotationMarks.Straight)
                    straightQuotes++;
            }

            // Prosty cudzysłów: pary wewnętrzne neutralne; nieparzysta liczba w bloku zakończonym
            // prostym cudzysłowem = jedno zamknięcie cytatu zewnętrznego (konwencja mieszana „…").
            var trimmed = text.TrimEnd();
            if (straightQuotes % 2 == 1 && EndsWithStraightQuote(trimmed))
                Depth = System.Math.Max(0, Depth - 1);

            ClosureReached = IsArmed && Depth == 0 && ClosingQuoteAtEnd.IsMatch(trimmed);
            CommaClosurePending = IsArmed && Depth == 0 && ClosingQuoteWithComma.IsMatch(trimmed);
        }

        /// <summary>Odnotowuje stłumioną komendę nowelizacyjną wewnątrz cytatu (ZZ).</summary>
        public void MarkNestedTrigger() => NestedTriggerSuppressed = true;

        /// <summary>Przywraca stan początkowy (nowe zbieranie nowelizacji).</summary>
        public void Reset()
        {
            _first = true;
            _disarmed = false;
            IsArmed = false;
            Depth = 0;
            ClosureReached = false;
            CommaClosurePending = false;
            NestedTriggerSuppressed = false;
        }

        private void Disarm()
        {
            _disarmed = true;
            IsArmed = false;
            Depth = 0;
            ClosureReached = false;
        }
    }
}
