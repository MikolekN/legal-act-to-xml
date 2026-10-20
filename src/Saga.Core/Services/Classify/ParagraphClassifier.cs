using System.Text.RegularExpressions;
using Saga.Core.Helpers;
using Saga.Core.Services.Parsing;
using Saga.Model;

namespace Saga.Core.Services.Classify
{
    /// <summary>
    /// Klasyfikator akapitów. Rozpoznaje typ jednostki redakcyjnej i oblicza pewność (1–100).
    /// Rozwiązywanie konfliktów deleguje do <see cref="IConflictResolver"/>.
    /// </summary>
    public sealed class ParagraphClassifier : IParagraphClassifier
    {
        private readonly IConflictResolver _conflictResolver;
        private readonly ConfidencePenaltyConfig _cfg;

        public ParagraphClassifier(
            IConflictResolver? conflictResolver = null,
            ConfidencePenaltyConfig? penaltyConfig = null)
        {
            _conflictResolver = conflictResolver ?? new DefaultConflictResolver();
            _cfg = penaltyConfig ?? ConfidencePenaltyConfig.Default;
        }

        // ============================================================
        // Współdzielone wzorce regex (reużywane przez ParsingFactories i AmendmentBuilder)
        // ============================================================

        /// <summary>Opcjonalny prefiks cytatu otwierającego („ " ") w tekście akapitu.</summary>
        internal const string OptionalQuotePrefix = $"(?:[{QuotationMarks.Any}]\\s*)?";

        /// <summary>
        /// Opcjonalny indeks górny numeru jednostki w notacji [x] (§ 89 ust. 6 ZTP; kanał GetFullText):
        /// liczba, w aktach wielokrotnie nowelizowanych także z literą („130[1a]"). Bez nawiasu zamykającego —
        /// odnośniki przypisów mają postać [N)] i nie mogą tu wpadać.
        /// </summary>
        internal const string OptionalSuperscript = @"(?:\[\d+[a-z]*\])?";

        /// <summary>Litery za liczbą w oznaczeniu („2a", „15zzs", „48ł").</summary>
        private const string LetterSuffix = DesignationAlphabet.Letter + "*";

        /// <summary>Znacznik odnośnika w notacji GetFullText: indeks górny zakończony nawiasem — „[9)]", „[2), 3)]".</summary>
        internal const string FootnoteMarker = FootnoteMarkers.Pattern;

        /// <summary>
        /// Co stoi za kropką numeru ustępu: odstęp albo — w tekstach jednolitych — od razu znacznik odnośnika
        /// („2.[9)] Do wniosku…"). Znacznik zostaje w treści jednostki, numer kończy się na kropce.
        /// </summary>
        internal const string AfterNumberDot = @"(?:\s+|(?=" + FootnoteMarker + "))";

        /// <summary>Opcjonalny znak paragrafu kodeksu.</summary>
        internal const string OptionalParagraphMark = @"(?:§\s*)?";

        /// <summary>
        /// Znak jednostki podstawowej przy odczycie numeru i treści jednostki już rozpoznanej: „Art." albo „§".
        /// O tym, czy „§ 5." jest jednostką podstawową, rozstrzyga rozpoznanie (<see cref="SectionUnitPattern"/>).
        /// </summary>
        private const string BasicUnitMark = @"(?:Art\.?|§)";

        internal static readonly Regex ArticlePattern = new(
            $@"^{OptionalQuotePrefix}Art\.?\s*\d+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Oznaczenie jednostki podstawowej aktu opartego na paragrafach: „§ 5." (ADR-0034). Rozpoznawane
        /// tylko wtedy, gdy rodzaj aktu na to wskazuje — w ustawie „§" pod artykułem to paragraf kodeksu.
        /// </summary>
        internal static readonly Regex SectionUnitPattern = new(
            $@"^{OptionalQuotePrefix}§\s*\d+{LetterSuffix}{OptionalSuperscript}\.", RegexOptions.Compiled);

        internal static readonly Regex ParagraphPattern = new(
            $@"^{OptionalQuotePrefix}{OptionalParagraphMark}\d+{LetterSuffix}{OptionalSuperscript}\.{AfterNumberDot}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        internal static readonly Regex PointPattern = new(
            $@"^{OptionalQuotePrefix}\d+{LetterSuffix}{OptionalSuperscript}\)\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // ROZPOZNANIE litery z samego tekstu (§ 56 ZTP): litera to MAŁA litera alfabetu łacińskiego (bez
        // polskich znaków diakrytycznych), a po wyczerpaniu alfabetu dwuznak „za", „zb"… — stąd [a-z]{1,2}
        // BEZ IgnoreCase. Wersalik „A)" czy token 3+ znaków to nie litera redakcyjna (wyliczenie załącznika
        // lub proza). Litera „ł)", którą akty wstawiają między „l)" a „m)", wchodzi tylko ścieżką stylową. Wzorzec węższy jest CELOWO tylko dla rozpoznania (gałąź regex-only); wydobycie numeru i
        // obcięcie prefiksu (po sklasyfikowaniu, także ze stylu LIT) używają szerszych <see cref="LetterStripPattern"/>
        // / <see cref="LetterNumberCapture"/>, aby nie zmieniać ścieżki stylowej.
        internal static readonly Regex LetterPattern = new(
            $@"^{OptionalQuotePrefix}[a-z]{{1,2}}{OptionalSuperscript}\)\s*", RegexOptions.Compiled);

        /// <summary>
        /// Prefiks litery do OBCIĘCIA po sklasyfikowaniu (szerszy niż <see cref="LetterPattern"/>: obejmuje też
        /// litery sklasyfikowane ze stylu LIT, których marker nie spełnia § 56 — np. wersalik). Nie uczestniczy
        /// w rozpoznaniu, więc szerokość nie zwiększa liczby fałszywych trafień.
        /// </summary>
        internal static readonly Regex LetterStripPattern = new(
            $@"^{OptionalQuotePrefix}{DesignationAlphabet.Letter}{{1,5}}{OptionalSuperscript}\)\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Tiret zaczyna myślnik — w każdej postaci, jaką piszą szczotki (Dashes, ADR-0040) — a za nim odstęp albo,
        // w tekstach jednolitych, od razu znacznik odnośnika („–[*)] 12 miesięcy dla DME…" — 336403_3088135).
        internal static readonly Regex TiretPattern = new(
            $@"^{OptionalQuotePrefix}[{Dashes.Any}]+(?:\s+|(?={FootnoteMarker}))", RegexOptions.Compiled);

        /// <summary>Artykuł z grupą przechwytującą numer (do ParseArticleNumber).</summary>
        internal static readonly Regex ArticleNumberCapture = new(
            $@"^{OptionalQuotePrefix}{BasicUnitMark}\s*(\d+{LetterSuffix}{OptionalSuperscript})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Artykuł z przechwyceniem ogona (do GetArticleTail).</summary>
        internal static readonly Regex ArticleTailCapture = new(
            $@"^{OptionalQuotePrefix}{BasicUnitMark}\s*\d+{LetterSuffix}{OptionalSuperscript}\.?\s*(.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Koniec zakresu oznaczeń na początku ogona jednostki („Art. 39–51. (pominięte)" → „–51."): myślnik, numer
        /// i kropka, za nią odstęp, znacznik odnośnika albo koniec akapitu; przed numerem bywa powtórzony znak jednostki
        /// („Art. 41a–Art. 41i." — 307514_2780745). Pojedyncze oznaczenie kończy kropka tuż za numerem, więc myślnik
        /// z numerem w tym miejscu znaczy zakres (ADR-0097).
        /// </summary>
        internal static readonly Regex DesignationRangeEndCapture = new(
            $@"^[{Dashes.Any}]\s*(?:{BasicUnitMark}\s*)?(\d+{LetterSuffix}{OptionalSuperscript})\.(?:{AfterNumberDot}|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Numer ustępu/paragrafu kodeksu z grupą przechwytującą (do ParseParagraphNumber).</summary>
        internal static readonly Regex ParagraphNumberCapture = new(
            $@"^{OptionalQuotePrefix}{OptionalParagraphMark}(\d+{LetterSuffix}{OptionalSuperscript})\.{AfterNumberDot}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Znaczniki odnośników na początku ogona artykułu („Art. 13ja.[36)] 1. Podmiot…") — odnoszą się
        /// do oznaczenia artykułu i stoją przed numerem pierwszego ustępu.
        /// </summary>
        internal static readonly Regex LeadingFootnoteMarkers = new(
            $@"^(?:{FootnoteMarker}\s*)+", RegexOptions.Compiled);

        /// <summary>Numer punktu z grupą przechwytującą (do ParsePointNumber).</summary>
        internal static readonly Regex PointNumberCapture = new(
            $@"^{OptionalQuotePrefix}(\d+{LetterSuffix}{OptionalSuperscript})\)\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Numer litery z grupą przechwytującą (do ParseLetterNumber). Wzorzec PARSUJĄCY (uruchamiany po
        /// klasyfikacji, także dla liter ze stylu LIT) jest celowo szerszy niż rozpoznający <see cref="LetterPattern"/>,
        /// aby wydobyć numer i uniknąć pozostawienia prefiksu w treści (zasada „nie przeinaczyć", ścieżka stylowa bez zmian).
        /// </summary>
        internal static readonly Regex LetterNumberCapture = new(
            $@"^{OptionalQuotePrefix}({DesignationAlphabet.Letter}{{1,5}}{OptionalSuperscript})\)\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // === Jednostki systematyzacyjne (§ 60-62 ZTP) ===
        // Wzorce ZAKOTWICZONE na całej linii ($): marker jednostki stoi w osobnym wierszu (§ 60), więc
        // proza „Rozdział 5 stosuje się…"/„Tytuł V wprowadza się…" NIE jest brana za nagłówek. Dozwolona
        // końcowa kropka/przecinek. Wersaliki to sygnał ZTP; dopuszczamy też kapitalizację tytułową
        // (nie IgnoreCase — słowo małą literą to proza; uznaje je tylko styl oznaczenia). Numer wyższych
        // jednostek: cyfra rzymska (opcjonalny sufiks litery jednostki wtrąconej, „IVa") lub liczebnik
        // słowny; rozstrzyga walidacja, więc „Część majątku…" nie zostanie uznane za jednostkę.
        // Rozdział/Oddział: cyfra arabska + sufiks. Odczyt tolerancyjny (spoza § 62 ZTP, z korpusu):
        // rozdział i oddział rzymski (KK, KW, KP), tytuł i dział z sufiksem wersalikiem („IIIA") albo cyfrą
        // arabską. Za numerem: indeks górny „[1]" (część oznaczenia) i znacznik odnośnika „[5)]" (pomijany);
        // sąsiadujące w DOCX indeks i odnośnik zlewają się w jeden nawias „[1 15)]".
        private const string DesignationEnd = @"(?:\[(?<sup>\d+[a-z]*)(?:\s+[^\]]*\))?\])?(?:\[[^\]]*\)\])?\s*[.,]?\s*$";
        private const string PartOrBookNumber = @"(?<n>[IVXLCDM]+[a-z]?|\p{L}+(?:\s+\p{L}+)?)";
        private const string TitleOrDivisionNumber = @"(?<n>[IVXLCDM]+[A-Za-z]?|\d+[a-z]?|\p{L}+(?:\s+\p{L}+)?)";
        private const string ChapterOrSubchapterNumber = @"(?<n>\d+[a-zA-Z]{0,3}|[IVXLCDM]+[a-z]?)";

        internal static readonly Regex PartUnitPattern = new(
            @"^(?:CZĘŚĆ|Część)\s+" + PartOrBookNumber + DesignationEnd, RegexOptions.Compiled);
        internal static readonly Regex BookUnitPattern = new(
            @"^(?:KSIĘGA|Księga)\s+" + PartOrBookNumber + DesignationEnd, RegexOptions.Compiled);
        internal static readonly Regex TitleUnitPattern = new(
            @"^(?:TYTUŁ|Tytuł)\s+" + TitleOrDivisionNumber + DesignationEnd, RegexOptions.Compiled);
        internal static readonly Regex DivisionUnitPattern = new(
            @"^(?:DZIAŁ|Dział)\s+" + TitleOrDivisionNumber + DesignationEnd, RegexOptions.Compiled);
        internal static readonly Regex ChapterUnitPattern = new(
            @"^(?:ROZDZIAŁ|Rozdział)\s+" + ChapterOrSubchapterNumber + DesignationEnd, RegexOptions.Compiled);
        internal static readonly Regex SubchapterUnitPattern = new(
            @"^(?:ODDZIAŁ|Oddział)\s+" + ChapterOrSubchapterNumber + DesignationEnd, RegexOptions.Compiled);

        // Typy stylów szablonu dla jednostek systematyzacyjnych (wartości ClassificationResult.StyleType).
        internal const string ChapterOrSubchapterStyle = "ROZDZ_ODDZ";
        internal const string TitleOrDivisionStyle = "TYT_DZ";
        internal const string PartOrBookStyle = "CZ_KS";
        internal const string UnitHeadingStyle = "PRZEDM";

        // ============================================================
        // Implementacja IParagraphClassifier
        // ============================================================

        /// <summary>
        /// Klasyfikuje akapit do typu jednostki redakcyjnej i oblicza pewność (1–100).
        /// Confidence = 100 gdy styl, regex i numeracja są w pełnej zgodzie.
        /// </summary>
        public ClassificationResult Classify(ClassificationInput input)
        {
            var text = input.Text;
            var styleType = GetStyleType(input.StyleId);
            var isAmend = styleType == "AMENDMENT";

            // WrapUp — priorytet przed pozostałą logiką.
            // Wykrywany wyłącznie przez styl (CZ_WSP_*); tekst jedynie potwierdza lub obniża pewność.
            if (styleType == "WRAPUP")
                return BuildWrapUpResult(styleType, isAmend, byStyle: true, IsWrapUpByText(text));

            // Sygnał ze stylu (null gdy AMENDMENT lub nieznany)
            ParagraphKind? styleKind = isAmend ? null : MapStyleToKind(styleType);

            // Sygnał syntaktyczny (regex)
            // Słowo kluczowe małą literą („dział II") to oznaczenie tylko przy stylu oznaczenia.
            ParagraphKind? syntacticKind = MatchRegex(text, input.SectionIsBasicUnit)
                ?? (IsDesignationStyle(styleType) ? MatchSystematizingRegex(CapitalizeFirst(text)) : null);

            return BuildResult(input, styleType, isAmend, styleKind, syntacticKind);
        }

        // ============================================================
        // Metody pomocnicze — publiczne statyczne (reużywane zewnętrznie)
        // ============================================================

        public static string? GetStyleType(string? styleId)
        {
            if (string.IsNullOrEmpty(styleId))
                return null;

            // Wykryj style nowelizacji po mapie styli (priorytet nad heurystyką)
            if (StyleLibraryMapper.TryGetStyleInfo(styleId, out var info) && info != null)
            {
                if (info.IsAmendment)
                    return "AMENDMENT";

                // WrapUp — styl zamknięcia listy (CZ_WSP_*)
                if (info.DisplayName.StartsWith("CZ_WSP_", StringComparison.OrdinalIgnoreCase))
                    return "WRAPUP";
            }

            // Fallback dla styli nowelizacji spoza mapy
            if (styleId.StartsWith("Z/", StringComparison.OrdinalIgnoreCase) ||
                styleId.StartsWith("ZZ", StringComparison.OrdinalIgnoreCase) ||
                styleId.StartsWith("Z_", StringComparison.OrdinalIgnoreCase))
                return "AMENDMENT";

            // Jednostki systematyzacyjne: styl oznaczenia wskazuje rodzinę dwóch jednostek (konkretną
            // rozstrzyga treść), styl CZĘŚĆ(KSIĘGA) służy też tytułowi części i księgi.
            if (styleId.StartsWith("ROZDZODDZOZN", StringComparison.OrdinalIgnoreCase)) return ChapterOrSubchapterStyle;
            if (styleId.StartsWith("TYTDZOZN", StringComparison.OrdinalIgnoreCase)) return TitleOrDivisionStyle;
            if (styleId.StartsWith("CZKSIGA", StringComparison.OrdinalIgnoreCase)) return PartOrBookStyle;
            if (styleId.StartsWith("ROZDZODDZPRZEDM", StringComparison.OrdinalIgnoreCase) ||
                styleId.StartsWith("TYTDZPRZEDM", StringComparison.OrdinalIgnoreCase)) return UnitHeadingStyle;

            if (styleId.StartsWith("ART", StringComparison.OrdinalIgnoreCase)) return "ART";
            if (styleId.StartsWith("UST", StringComparison.OrdinalIgnoreCase)) return "UST";
            if (styleId.StartsWith("PKT", StringComparison.OrdinalIgnoreCase)) return "PKT";
            if (styleId.StartsWith("LIT", StringComparison.OrdinalIgnoreCase)) return "LIT";
            if (styleId.StartsWith("TIR", StringComparison.OrdinalIgnoreCase)) return "TIR";
            if (styleId.StartsWith("2TIR", StringComparison.OrdinalIgnoreCase)) return "TIR";
            if (styleId.StartsWith("3TIR", StringComparison.OrdinalIgnoreCase)) return "TIR";

            return null;
        }

        public static bool IsArticleByText(string text) => ArticlePattern.IsMatch(text.Trim());
        public static bool IsParagraphByText(string text) => ParagraphPattern.IsMatch(text);
        public static bool IsPointByText(string text) => PointPattern.IsMatch(text);
        public static bool IsLetterByText(string text) => LetterPattern.IsMatch(text);
        public static bool IsTiretByText(string text) => TiretPattern.IsMatch(text);

        /// <summary>
        /// Sprawdza czy tekst to WrapUp (rozpoczyna się półpauzą lub dywizem po których stoi spacja).
        /// </summary>
        public static bool IsWrapUpByText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;
            var trimmed = text.TrimStart();
            if (trimmed.Length < 2)
                return false;
            return Dashes.IsDash(trimmed[0]) && char.IsWhiteSpace(trimmed[1]);
        }

        // ============================================================
        // Prywatne metody budowania wyniku
        // ============================================================

        private ClassificationResult BuildWrapUpResult(
            string? styleType, bool isAmend, bool byStyle, bool byText)
        {
            var penalties = new List<ClassificationPenalty>();
            int confidence = 100;

            if (!byStyle)
            {
                penalties.Add(new ClassificationPenalty
                {
                    Reason = "Brak stylu WrapUp — typ ustalony wyłącznie z tekstu",
                    Value = _cfg.StyleAbsentPenalty,
                });
                confidence -= _cfg.StyleAbsentPenalty;
            }
            if (!byText)
            {
                penalties.Add(new ClassificationPenalty
                {
                    Reason = "Brak półpauzy w tekście — typ ustalony wyłącznie ze stylu",
                    Value = _cfg.SyntaxAbsentPenalty,
                });
                confidence -= _cfg.SyntaxAbsentPenalty;
            }

            return new ClassificationResult
            {
                Kind = ParagraphKind.WrapUp,
                Confidence = Math.Clamp(confidence, 1, 100),
                IsAmendmentContent = isAmend,
                StyleType = styleType,
                Penalties = penalties,
            };
        }

        private ClassificationResult BuildResult(
            ClassificationInput input,
            string? styleType,
            bool isAmend,
            ParagraphKind? styleKind,
            ParagraphKind? syntacticKind)
        {
            var penalties = new List<ClassificationPenalty>();
            int confidence = 100;
            ParagraphKind kind;

            if (styleKind == null && syntacticKind == null)
            {
                // Brak obu sygnałów
                kind = ParagraphKind.Unknown;
                confidence = 1;
            }
            else if (styleKind != null && syntacticKind != null)
            {
                if (styleKind == syntacticKind || InStyleFamily(styleType, syntacticKind.Value))
                {
                    // Oba sygnały zgodne
                    kind = syntacticKind.Value;
                }
                else
                {
                    // Konflikt — delegacja do IConflictResolver
                    kind = _conflictResolver.Resolve(styleKind.Value, syntacticKind.Value, input.Text, input.StyleId);
                    penalties.Add(new ClassificationPenalty
                    {
                        Reason = $"Konflikt: styl={styleKind}, regex={syntacticKind}; rozstrzygnięto: {kind}",
                        Value = _cfg.StyleSyntaxConflictPenalty,
                    });
                    confidence -= _cfg.StyleSyntaxConflictPenalty;
                }
            }
            else if (syntacticKind != null)
            {
                // Tylko regex
                kind = syntacticKind.Value;
                penalties.Add(new ClassificationPenalty
                {
                    Reason = "Brak rozpoznanego stylu Word",
                    Value = _cfg.StyleAbsentPenalty,
                });
                confidence -= _cfg.StyleAbsentPenalty;
            }
            else
            {
                // Tylko styl — artykuł i oznaczenie jednostki systematyzacyjnej wymagają sygnatury tekstowej
                if (styleKind == ParagraphKind.Article || IsDesignationStyle(styleType))
                {
                    kind = ParagraphKind.Unknown;
                    confidence = 1;
                }
                else
                {
                    kind = styleKind!.Value;
                    penalties.Add(new ClassificationPenalty
                    {
                        Reason = "Brak dopasowania regex — typ z samego stylu",
                        Value = _cfg.SyntaxAbsentPenalty,
                    });
                    confidence -= _cfg.SyntaxAbsentPenalty;
                }
            }

            // Sprawdzenie ciągłości numeracji (NumberingHint)
            if (input.NumberingHint is { } hint && hint.ExpectedKind == kind && kind != ParagraphKind.Unknown)
            {
                var parsedNumber = ParseNumberForKind(kind, input.Text);
                if (parsedNumber != null && !hint.IsContinuous(parsedNumber))
                {
                    penalties.Add(new ClassificationPenalty
                    {
                        Reason = $"Nieciągłość numeracji {kind}: oczekiwano następnika {hint.ExpectedNumber?.Value}",
                        Value = _cfg.NumberingBreakPenalty,
                    });
                    confidence -= _cfg.NumberingBreakPenalty;
                }
            }

            return new ClassificationResult
            {
                Kind = kind,
                Confidence = Math.Clamp(confidence, 1, 100),
                IsAmendmentContent = isAmend,
                StyleType = styleType,
                Penalties = penalties,
            };
        }

        private static ParagraphKind? MapStyleToKind(string? styleType) =>
            styleType switch
            {
                "ART" => ParagraphKind.Article,
                "UST" => ParagraphKind.Paragraph,
                "PKT" => ParagraphKind.Point,
                "LIT" => ParagraphKind.Letter,
                "TIR" => ParagraphKind.Tiret,
                ChapterOrSubchapterStyle => ParagraphKind.ChapterUnit,
                TitleOrDivisionStyle => ParagraphKind.DivisionUnit,
                PartOrBookStyle => ParagraphKind.PartUnit,
                UnitHeadingStyle => ParagraphKind.UnitHeading,
                _ => null,
            };

        private static bool IsDesignationStyle(string? styleType) =>
            styleType is ChapterOrSubchapterStyle or TitleOrDivisionStyle or PartOrBookStyle;

        internal static bool IsSystematizingStyle(string? styleType) =>
            IsDesignationStyle(styleType) || styleType == UnitHeadingStyle;

        /// <summary>Styl oznaczenia wskazuje rodzinę dwóch jednostek — którą z nich, rozstrzyga treść.</summary>
        private static bool InStyleFamily(string? styleType, ParagraphKind kind) => styleType switch
        {
            ChapterOrSubchapterStyle => kind is ParagraphKind.ChapterUnit or ParagraphKind.SubchapterUnit,
            TitleOrDivisionStyle => kind is ParagraphKind.TitleUnit or ParagraphKind.DivisionUnit,
            PartOrBookStyle => kind is ParagraphKind.PartUnit or ParagraphKind.BookUnit,
            _ => false,
        };

        private static ParagraphKind? MatchRegex(string text, bool sectionIsBasicUnit)
        {
            // Jednostki systematyzacyjne sprawdzane najpierw (§ 60-62); rozstrzyga walidacja numeru.
            var systematizing = MatchSystematizingRegex(text);
            if (systematizing != null) return systematizing;

            if (IsArticleByText(text)) return ParagraphKind.Article;
            if (sectionIsBasicUnit && SectionUnitPattern.IsMatch(text)) return ParagraphKind.Article;
            if (IsParagraphByText(text)) return ParagraphKind.Paragraph;
            if (IsPointByText(text)) return ParagraphKind.Point;
            if (IsLetterByText(text)) return ParagraphKind.Letter;
            if (IsTiretByText(text)) return ParagraphKind.Tiret;
            return null;
        }

        /// <summary>
        /// Rozpoznaje jednostkę systematyzacyjną. Dla jednostek wyższych (Część/Księga/Tytuł/Dział)
        /// wymaga poprawnego numeru rzymskiego lub liczebnika słownego; Rozdział/Oddział — arabskiego.
        /// </summary>
        internal static ParagraphKind? MatchSystematizingRegex(string text)
        {
            foreach (var kind in new[] { ParagraphKind.PartUnit, ParagraphKind.BookUnit, ParagraphKind.TitleUnit,
                         ParagraphKind.DivisionUnit, ParagraphKind.ChapterUnit, ParagraphKind.SubchapterUnit })
            {
                if (ParseSystematizingNumber(kind, text, capitalize: false) != null)
                    return kind;
            }
            return null;
        }

        /// <summary>Poziom hierarchii jednostki systematyzacyjnej; null dla innych rodzajów akapitu.</summary>
        internal static UnitType? LevelOf(ParagraphKind kind) => kind switch
        {
            ParagraphKind.PartUnit => UnitType.Part,
            ParagraphKind.BookUnit => UnitType.Book,
            ParagraphKind.TitleUnit => UnitType.Title,
            ParagraphKind.DivisionUnit => UnitType.Division,
            ParagraphKind.ChapterUnit => UnitType.Chapter,
            ParagraphKind.SubchapterUnit => UnitType.Subchapter,
            _ => null,
        };

        /// <summary>
        /// Oznaczenie z powołania („VIa" w „po dziale VIa") czytane tak jak oznaczenie w akcie — ta sama
        /// postać w eId (ADR-0018).
        /// </summary>
        internal static EntityNumber? ParseSystematizingDesignation(UnitType level, string designation)
        {
            var (kind, keyword) = level switch
            {
                UnitType.Part => (ParagraphKind.PartUnit, "Część"),
                UnitType.Book => (ParagraphKind.BookUnit, "Księga"),
                UnitType.Title => (ParagraphKind.TitleUnit, "Tytuł"),
                UnitType.Division => (ParagraphKind.DivisionUnit, "Dział"),
                UnitType.Chapter => (ParagraphKind.ChapterUnit, "Rozdział"),
                _ => (ParagraphKind.SubchapterUnit, "Oddział"),
            };
            return ParseSystematizingNumber(kind, $"{keyword} {designation}");
        }

        private static Regex? PatternFor(ParagraphKind kind) => kind switch
        {
            ParagraphKind.PartUnit => PartUnitPattern,
            ParagraphKind.BookUnit => BookUnitPattern,
            ParagraphKind.TitleUnit => TitleUnitPattern,
            ParagraphKind.DivisionUnit => DivisionUnitPattern,
            ParagraphKind.ChapterUnit => ChapterUnitPattern,
            ParagraphKind.SubchapterUnit => SubchapterUnitPattern,
            _ => null,
        };

        private static readonly string[] SystematizingKeywords = ["część", "księga", "tytuł", "dział", "rozdział", "oddział"];

        /// <summary>
        /// Słowo kluczowe jednostki w pisowni spoza ZTP („dział II", „DZiał VIa") czyta się jak „Dział" — tylko przy
        /// stylu oznaczenia, więc proza („dział administracji rządowej") nie staje się jednostką. Inny tekst — wielka
        /// pierwsza litera.
        /// </summary>
        private static string CapitalizeFirst(string text)
        {
            var end = 0;
            while (end < text.Length && char.IsLetter(text[end])) end++;
            var keyword = text[..end];
            if (keyword.Length > 0 && !IsZtpKeyword(keyword)
                && SystematizingKeywords.Contains(keyword.ToLowerInvariant()))
            {
                return char.ToUpperInvariant(keyword[0]) + keyword[1..].ToLowerInvariant() + text[end..];
            }

            return text.Length > 0 && char.IsLower(text[0]) ? char.ToUpperInvariant(text[0]) + text[1..] : text;
        }

        private static bool IsZtpKeyword(string keyword) =>
            keyword == keyword.ToUpperInvariant() || keyword == char.ToUpperInvariant(keyword[0]) + keyword[1..].ToLowerInvariant();

        /// <summary>
        /// Parsuje numer jednostki: cyfry arabskie z sufiksem, cyfra rzymska (opcjonalnie z sufiksem litery
        /// jednostki wtrąconej — „IVa", tolerancyjnie „IIIA") albo liczebnik słowny. Zwraca wartość liczbową
        /// i sufiks jak w tekście; null gdy to nie numer.
        /// </summary>
        private static (int numeric, string suffix)? ParseDesignationToken(string token, bool allowUppercaseSuffix)
        {
            int digits = 0;
            while (digits < token.Length && char.IsDigit(token[digits])) digits++;
            if (digits > 0)
                return int.TryParse(token[..digits], out var arabic) ? (arabic, token[digits..]) : null;

            var n = RomanNumeralConverter.RomanToInt(token);
            if (n != null) return (n.Value, string.Empty);

            // Litera jednostki wtrąconej, która jest zarazem cyfrą rzymską („IIC" = II + C, „IIIC" = III + C): cały zapis
            // nie jest liczbą rzymską, a bez niej jest. Tylko C i D — dalsze cyfry rzymskie jako litery wtrącone to już zgadywanie.
            if (allowUppercaseSuffix && token.Length > 1 && token[^1] is 'C' or 'D'
                && RomanNumeralConverter.RomanToInt(token[..^1]) is { } stem)
            {
                return (stem, token[^1..]);
            }

            if (PolishNumerals.TryParseOrdinal(token, out var wordValue))
                return (wordValue, string.Empty);

            // Liczebnik słowny z literą jednostki wtrąconej („czternastya" = czternasty + a).
            if (token.Length > 1 && char.IsLower(token[^1]) && PolishNumerals.TryParseOrdinal(token[..^1], out var wordStem))
                return (wordStem, token[^1..]);

            // Sufiks: małe litery, a dla tytułu i działu także wersalik spoza cyfr rzymskich („IIIA" = III + A).
            int b = token.Length;
            while (b > 0 && (char.IsLower(token[b - 1])
                             || (allowUppercaseSuffix && char.IsUpper(token[b - 1]) && !"IVXLCDM".Contains(token[b - 1]))))
                b--;
            if (b > 0 && b < token.Length)
            {
                // Sufiks mieszany („IvA" = I + vA) to literówka w liczbie rzymskiej: IVA = IV + A.
                if (token[b..].Any(char.IsLower) && token[b..].Any(char.IsUpper))
                    return token.Any(char.IsLower) ? ParseDesignationToken(token.ToUpperInvariant(), allowUppercaseSuffix) : null;

                var baseNum = RomanNumeralConverter.RomanToInt(token[..b]);
                if (baseNum != null) return (baseNum.Value, token[b..]);
            }
            return null;
        }

        /// <summary>
        /// Wyodrębnia numer jednostki systematyzacyjnej jako EntityNumber. Value (źródło eId) to liczba
        /// arabska z sufiksem jak w tekście — także dla oznaczenia rzymskiego i słownego (ADR-0018);
        /// RawValue zostaje oznaczeniem z aktu, Superscript — indeksem górnym (ADR-0019).
        /// </summary>
        public static EntityNumber? ParseSystematizingNumber(ParagraphKind kind, string text) =>
            ParseSystematizingNumber(kind, text, capitalize: true);

        private static EntityNumber? ParseSystematizingNumber(ParagraphKind kind, string text, bool capitalize)
        {
            var m = PatternFor(kind)?.Match(capitalize ? CapitalizeFirst(text.Trim()) : text);
            if (m is not { Success: true }) return null;

            var token = m.Groups["n"].Value;
            if (IsNamedDesignation(kind, token))
                return new EntityNumber { Value = token.ToUpperInvariant(), RawValue = token, IsName = true };

            var parsed = ParseDesignationToken(token,
                allowUppercaseSuffix: kind is ParagraphKind.TitleUnit or ParagraphKind.DivisionUnit);
            if (parsed == null) return null;

            // „KSIĘGA PIERWSZA" i „KSIĘGA I" → Value "1" → ten sam eId ksi_1.
            return new EntityNumber
            {
                Value = $"{parsed.Value.numeric}{parsed.Value.suffix}", RawValue = token,
                NumericPart = parsed.Value.numeric, LexicalPart = parsed.Value.suffix,
                Superscript = m.Groups["sup"].Success ? m.Groups["sup"].Value : string.Empty,
            };
        }

        /// <summary>
        /// Jednostki nazwane bez numeru — zamknięta lista z korpusu: części KK, KW i KKW (także końcowa KKW), tytuł
        /// wstępny KPC. Dowolny wyraz („Część budżetowa") pozostaje prozą. Nazwa jest oznaczeniem jednostki (ADR-0098).
        /// </summary>
        private static bool IsNamedDesignation(ParagraphKind kind, string token) => kind switch
        {
            ParagraphKind.PartUnit => token.ToUpperInvariant() is "OGÓLNA" or "SZCZEGÓLNA" or "WOJSKOWA" or "KOŃCOWA",
            ParagraphKind.TitleUnit => token.ToUpperInvariant() is "WSTĘPNY",
            _ => false,
        };

        /// <summary>
        /// Odczyt tolerancyjny oznaczenia (spoza § 62 ZTP) — powód dla komunikatu na jednostce albo null.
        /// </summary>
        public static string? ToleratedDesignationReason(ParagraphKind kind, string text, EntityNumber number)
        {
            var raw = number.RawValue ?? string.Empty;
            var arabic = raw.Length > 0 && char.IsDigit(raw[0]);
            var lowerUnits = kind is ParagraphKind.ChapterUnit or ParagraphKind.SubchapterUnit;

            if (text.TrimStart() is [var first, ..] && char.IsLower(first))
                return "Słowo kluczowe jednostki małą literą — oznaczenie uznane dzięki stylowi oznaczenia.";
            if (text.TrimStart().Split(' ', 2)[0] is { Length: > 1 } keyword && !IsZtpKeyword(keyword))
                return $"Słowo kluczowe jednostki w pisowni spoza ZTP („{keyword}\") — oznaczenie uznane dzięki stylowi oznaczenia.";
            if (lowerUnits && !arabic)
                return $"Rozdział/oddział oznaczony cyframi rzymskimi („{raw}\"); § 62 ust. 1 ZTP wymaga arabskich.";
            if (!lowerUnits && arabic)
                return $"Jednostka wyższa oznaczona cyframi arabskimi („{raw}\"); § 62 ust. 1 ZTP wymaga rzymskich.";
            if (!lowerUnits && !arabic && number.LexicalPart.Any(char.IsUpper) && raw.Any(char.IsLower))
                return $"Mała litera w liczbie rzymskiej („{raw}\") — odczytane jako {raw.ToUpperInvariant()}.";
            if (!lowerUnits && number.LexicalPart.Any(char.IsUpper))
                return $"Sufiks oznaczenia wersalikiem („{raw}\").";
            return null;
        }

        internal static EntityNumber? ParseNumberForKind(ParagraphKind kind, string text) =>
            kind switch
            {
                ParagraphKind.Article => ParsingFactories.ParseArticleNumber(text),
                ParagraphKind.Paragraph => ParsingFactories.ParseParagraphNumber(text),
                ParagraphKind.Point => ParsingFactories.ParsePointNumber(text),
                ParagraphKind.Letter => ParsingFactories.ParseLetterNumber(text),
                _ => null,
            };
    }
}
