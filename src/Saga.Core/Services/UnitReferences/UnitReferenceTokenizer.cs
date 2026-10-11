using System.Text.RegularExpressions;
using Saga.Core.Helpers;

namespace Saga.Core.Services.UnitReferences
{
    /// <summary>Poziom jednostki redakcyjnej w powołaniu.</summary>
    internal enum UnitLevel
    {
        None = 0,
        Part = 1,
        Book = 2,
        Title = 3,
        Division = 4,
        Chapter = 5,
        Subchapter = 6,
        Article = 7,
        Paragraph = 8,
        Point = 9,
        Letter = 10,
        Tiret = 11,
        Sentence = 12,
    }

    /// <summary>Rodzaj tokenu w powołaniu jednostek.</summary>
    internal enum UnitTokenKind
    {
        /// <summary>Słowo kluczowe jednostki: „art.", „ust.", „pkt", „lit.", „tiret", „zdanie".</summary>
        UnitKeyword,

        /// <summary>Oznaczenie jednostki: „1", „85c", „a", „pierwsze".</summary>
        Designation,

        /// <summary>Przedział oznaczeń: „85c–85j", „19-21", „czwarte–szóste".</summary>
        Range,

        /// <summary>Separator wyliczenia: „,", „i", „oraz", „lub", „albo", „:", „;".</summary>
        Separator,

        /// <summary>Półpauza wprowadzająca zakres dla całej poprzedzającej grupy.</summary>
        Dash,

        /// <summary>Znacznik zakresu: „w zakresie", „w odniesieniu do", „w stosunku do".</summary>
        ScopeMarker,

        /// <summary>Zakres opisany prozą („w zakresie, w jakim dotyczy…").</summary>
        ProseScope,

        /// <summary>Fragment nierozpoznany — proza.</summary>
        Prose,
    }

    /// <summary>Token powołania jednostek.</summary>
    internal sealed record UnitToken(UnitTokenKind Kind, string Text)
    {
        internal UnitLevel Level { get; init; } = UnitLevel.None;
        internal int TiretDepth { get; init; } = 1;

        /// <summary>Czy słowem kluczowym był znak „§".</summary>
        internal bool IsSectionSign { get; init; }

        internal string? RangeFrom { get; init; }
        internal string? RangeTo { get; init; }
    }

    /// <summary>
    /// Rozbija tekst powołania jednostek („art. 1 pkt 3 i 9, art. 2 pkt 2 lit. a") na tokeny.
    /// </summary>
    internal static class UnitReferenceTokenizer
    {
        private const RegexOptions Opts =
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        /// <summary>Spacje wokół nawiasów indeksu górnego — do zwinięcia, w odróżnieniu od spacji wewnątrz liczebnika.</summary>
        private static readonly Regex BracketSpacing = new(@"\s*([\[\]])\s*", RegexOptions.Compiled);

        /// <summary>
        /// Współdzielona alternatywa liczebników porządkowych („tiret pierwsze", „tiret dwudzieste
        /// pierwsze", „tiret czwartego").
        /// </summary>
        private static readonly string OrdinalWord = PolishNumerals.OrdinalAlternation;

        /// <summary>Słowo kluczowe jednostki: „art.", „§", „ust.", „pkt", „lit.", „(podwójne) tiret", „zdanie".</summary>
        internal static readonly string UnitKeywordPattern =
            @"art\.|§|ust\.|pkt|lit\.|(?:" + PolishNumerals.MultiplicityAlternation +
            @")\s+tiret\p{L}*|tiret\p{L}*|zdani\p{L}+";

        /// <summary>
        /// Słowo kluczowe jednostki systematyzacyjnej w każdej odmianie, w której pojawia się w komendzie
        /// („po rozdziale 9", „w dziale II", „uchyla się rozdziały 4 i 5") — bez „działalności" i „częściowo".
        /// </summary>
        internal static readonly string SystematizingKeywordPattern =
            @"\b(?:roz|od)?dzia(?:łu|łem|łów|łach|łami|ły|ł|le)\b|\btytu(?:łu|łem|łów|łach|ły|ł|le)\b" +
            @"|\bczęś(?:ciach|ciami|ci|ć)\b|\bksię(?:gach|gami|ga|gi|dze|gę|gą)\b";

        /// <summary>Oznaczenie rzymskie jednostki wyższej („dział VIa", „tytuł XIII") — wersaliki.</summary>
        private const string RomanDesignationPattern = @"(?-i:[IVXLC]+[a-z]?)\b";

        /// <summary>Liczba z literami oznaczenia: „85c", „48ł".</summary>
        private const string Number = @"\d+" + DesignationAlphabet.Letter + "*";

        /// <summary>Liczba z indeksem górnym: „125[1]", „130[1a]".</summary>
        private const string NumberWithSuperscript = Number + @"(?:\s*\[\s*" + Number + @"\s*\])?";

        /// <summary>Litera jako oznaczenie: „a", „za", „ł".</summary>
        private const string LetterDesignation = @"(?<!\w)" + DesignationAlphabet.LowerLetter + @"{1,2}(?!\w)";

        /// <summary>Przedział oznaczeń: „85c–85j", „a–c", „czwarte–szóste".</summary>
        internal static readonly string RangePattern =
            Number + RangeDash + Number +
            @"|" + LetterDesignation + RangeDash + LetterDesignation +
            @"|" + OrdinalWord + RangeDash + OrdinalWord;

        private const string RangeDash = @"\s*[" + Dashes.Any + @"]\s*";

        /// <summary>Oznaczenie jednostki: numer (z indeksem górnym „125[1]"), liczebnik porządkowy albo litera.</summary>
        internal static readonly string DesignationPattern =
            NumberWithSuperscript + "|" + OrdinalWord + "|" + LetterDesignation;

        private static readonly Regex TokenPattern = BuildTokenPattern(UnitKeywordPattern, DesignationPattern);

        /// <summary>
        /// Wariant z jednostkami systematyzacyjnymi — dla celów nowelizacji („po rozdziale 9 dodaje się …")
        /// i wyjątków wejścia w życie („…, z wyjątkiem rozdziału 4, który…"). Parser komend zostaje przy
        /// samych jednostkach redakcyjnych.
        /// </summary>
        private static readonly Regex SystematizingTokenPattern = BuildTokenPattern(
            SystematizingKeywordPattern + "|" + UnitKeywordPattern,
            NumberWithSuperscript + "|" + OrdinalWord + "|" + RomanDesignationPattern + "|" + LetterDesignation);

        private static Regex BuildTokenPattern(string unitKeyword, string designation) => new(
            @"(?<prosescope>w\s+zakresie\s*,\s*w\s+jakim.*|w\s+jakim\s+dotycz\p{L}*.*)" +
            @"|(?<scope>w\s+zakresie|w\s+odniesieniu\s+do|w\s+stosunku\s+do)" +
            @"|(?<dash>\s[" + Dashes.Any + @"]\s)" +
            @"|(?<unit>" + unitKeyword + ")" +
            @"|(?<range>" + RangePattern + ")" +
            @"|(?<separator>,|;|:|\bi\b|\boraz\b|\blub\b|\balbo\b)" +
            @"|(?<designation>" + designation + ")",
            Opts);

        /// <summary>
        /// Przyimki wprowadzające jednostkę („w art. 5", „po pkt 3"; § 89 ust. 2 ZTP: „przed oznaczeniem
        /// każdej kolejnej jednostki redakcyjnej pisze się przyimek „w”"). Pasują do wzorca oznaczenia
        /// litery, więc bez tej listy „w art. 5 w ust. 2" dałoby literę „w".
        /// </summary>
        private static readonly HashSet<string> Prepositions = new(StringComparer.OrdinalIgnoreCase)
        {
            "w", "we", "po", "do", "od", "z", "ze", "na", "o",
        };

        /// <summary>Rozbija tekst na tokeny; <paramref name="systematizing"/> — także jednostki systematyzacyjne.</summary>
        internal static IReadOnlyList<UnitToken> Tokenize(string? text, bool systematizing = false)
        {
            var tokens = new List<UnitToken>();
            if (string.IsNullOrWhiteSpace(text))
                return tokens;

            // Znacznik odnośnika przy powołaniu („art. 217[22)] oraz art. 227") nie jest oznaczeniem jednostki.
            var input = FootnoteMarkers.Blank(text.Trim());
            var cursor = 0;

            foreach (Match match in (systematizing ? SystematizingTokenPattern : TokenPattern).Matches(input))
            {
                if (match.Index > cursor)
                    AddProse(tokens, input[cursor..match.Index]);

                cursor = match.Index + match.Length;
                tokens.Add(Build(match));
            }

            if (cursor < input.Length)
                AddProse(tokens, input[cursor..]);

            NormalizeLeadingMultiplicityOrdinal(tokens);
            DemotePrepositions(tokens);
            PromoteLetterI(tokens);
            PromoteRomanI(tokens);
            return tokens;
        }

        /// <summary>Czy słowo to przyimek wprowadzający jednostkę („w", „po", „do" …).</summary>
        internal static bool IsPreposition(string word) => Prepositions.Contains(word);

        /// <summary>
        /// Poziom jednostki dla słowa kluczowego bez kontekstu ścieżki. „§" daje jednostkę główną —
        /// na paragraf kodeksu przestawia go dopiero <see cref="UnitPathCursor"/>, gdy w ścieżce jest artykuł.
        /// </summary>
        internal static UnitLevel LevelOf(string keyword) => MapLevel(keyword);

        /// <summary>
        /// „i" tuż po „lit." to litera i (§ 57 ust. 4 ZTP), nie spójnik: „w lit. i kropkę zastępuje się …".
        /// </summary>
        private static void PromoteLetterI(List<UnitToken> tokens)
        {
            for (var i = 1; i < tokens.Count; i++)
            {
                if (tokens[i] is { Kind: UnitTokenKind.Separator } candidate &&
                    candidate.Text.Equals("i", StringComparison.OrdinalIgnoreCase) &&
                    tokens[i - 1] is { Kind: UnitTokenKind.UnitKeyword, Level: UnitLevel.Letter })
                {
                    tokens[i] = new UnitToken(UnitTokenKind.Designation, candidate.Text);
                }
            }
        }

        /// <summary>
        /// „I" wersalikiem tuż po jednostce systematyzacyjnej to oznaczenie rzymskie („w dziale I"), nie spójnik.
        /// </summary>
        private static void PromoteRomanI(List<UnitToken> tokens)
        {
            for (var i = 1; i < tokens.Count; i++)
            {
                if (tokens[i] is { Kind: UnitTokenKind.Separator, Text: "I" } candidate &&
                    tokens[i - 1] is { Kind: UnitTokenKind.UnitKeyword, Level: >= UnitLevel.Part and <= UnitLevel.Subchapter })
                {
                    tokens[i] = new UnitToken(UnitTokenKind.Designation, candidate.Text);
                }
            }
        }

        /// <summary>
        /// Przyimek tuż przed słowem kluczowym („w art. 5 w ust. 2") to proza, nie oznaczenie. Wyjątek:
        /// miejsce oznaczenia zaraz po „lit." — „lit. w tiret pierwsze" to litera „w".
        /// </summary>
        private static void DemotePrepositions(List<UnitToken> tokens)
        {
            for (var i = 0; i + 1 < tokens.Count; i++)
            {
                if (tokens[i] is not { Kind: UnitTokenKind.Designation } candidate ||
                    !Prepositions.Contains(candidate.Text) ||
                    tokens[i + 1].Kind != UnitTokenKind.UnitKeyword)
                    continue;

                if (i > 0 && tokens[i - 1] is { Kind: UnitTokenKind.UnitKeyword, Level: UnitLevel.Letter })
                    continue;

                tokens[i] = new UnitToken(UnitTokenKind.Prose, candidate.Text);
            }
        }

        /// <summary>
        /// Sprowadza szyk „liczebnik przed słowem kluczowym" do postaci zwykłej: w powołaniu
        /// „tiret drugie pierwsze podwójne tiret" liczebnik „pierwsze" należy do podówjnego tiretu,
        /// więc chodzi o jedną ścieżkę tir_2__dwtir_1, a nie o dwa tirety.
        /// </summary>
        private static void NormalizeLeadingMultiplicityOrdinal(List<UnitToken> tokens)
        {
            for (var i = 2; i < tokens.Count; i++)
            {
                if (tokens[i] is not { Kind: UnitTokenKind.UnitKeyword, Level: UnitLevel.Tiret } keyword ||
                    keyword.TiretDepth <= 1)
                    continue;

                if (i + 1 < tokens.Count && IsDesignation(tokens[i + 1]))
                    continue;

                if (!IsDesignation(tokens[i - 1]) || !IsDesignation(tokens[i - 2]))
                    continue;

                var ordinal = tokens[i - 1];
                tokens.RemoveAt(i - 1);
                tokens.Insert(i, ordinal);
            }
        }

        private static bool IsDesignation(UnitToken token) =>
            token.Kind is UnitTokenKind.Designation or UnitTokenKind.Range;

        /// <summary>Znaki zdejmowane z brzegów prozy między oznaczeniami: odstępy, kropka i cudzysłowy.</summary>
        private static readonly char[] ProseEdge = [' ', '\t', '.', .. QuotationMarks.Any];

        private static void AddProse(List<UnitToken> tokens, string fragment)
        {
            var trimmed = fragment.Trim(ProseEdge);
            if (trimmed.Length > 0)
                tokens.Add(new UnitToken(UnitTokenKind.Prose, trimmed));
        }

        private static UnitToken Build(Match match)
        {
            if (match.Groups["prosescope"].Success)
                return new UnitToken(UnitTokenKind.ProseScope, match.Groups["prosescope"].Value.Trim());

            if (match.Groups["scope"].Success)
                return new UnitToken(UnitTokenKind.ScopeMarker, match.Groups["scope"].Value);

            if (match.Groups["dash"].Success)
                return new UnitToken(UnitTokenKind.Dash, match.Groups["dash"].Value.Trim());

            if (match.Groups["unit"].Success)
            {
                var raw = match.Groups["unit"].Value;
                return new UnitToken(UnitTokenKind.UnitKeyword, raw)
                {
                    Level = MapLevel(raw),
                    TiretDepth = MapTiretDepth(raw),
                    IsSectionSign = raw.StartsWith('§'),
                };
            }

            if (match.Groups["range"].Success)
            {
                var raw = match.Groups["range"].Value;
                var parts = raw.Split(Dashes.Characters, 2, StringSplitOptions.TrimEntries);
                return new UnitToken(UnitTokenKind.Range, raw)
                {
                    RangeFrom = parts.Length == 2 ? parts[0] : null,
                    RangeTo = parts.Length == 2 ? parts[1] : null,
                };
            }

            if (match.Groups["separator"].Success)
                return new UnitToken(UnitTokenKind.Separator, match.Groups["separator"].Value);

            var designation = BracketSpacing.Replace(match.Groups["designation"].Value, "$1").Trim();
            return new UnitToken(UnitTokenKind.Designation, designation);
        }

        private static UnitLevel MapLevel(string keyword)
        {
            var k = keyword.ToLowerInvariant();
            if (k.StartsWith("rozdzia", StringComparison.Ordinal))
                return UnitLevel.Chapter;
            if (k.StartsWith("oddzia", StringComparison.Ordinal))
                return UnitLevel.Subchapter;
            if (k.StartsWith("dzia", StringComparison.Ordinal))
                return UnitLevel.Division;
            if (k.StartsWith("tytu", StringComparison.Ordinal))
                return UnitLevel.Title;
            if (k.StartsWith("częś", StringComparison.Ordinal))
                return UnitLevel.Part;
            if (k.StartsWith("księ", StringComparison.Ordinal))
                return UnitLevel.Book;
            if (k.StartsWith("art.", StringComparison.Ordinal) || k.StartsWith("§", StringComparison.Ordinal))
                return UnitLevel.Article;
            if (k.StartsWith("ust.", StringComparison.Ordinal))
                return UnitLevel.Paragraph;
            if (k.StartsWith("pkt", StringComparison.Ordinal))
                return UnitLevel.Point;
            if (k.StartsWith("lit.", StringComparison.Ordinal))
                return UnitLevel.Letter;
            if (k.Contains("tiret", StringComparison.Ordinal))
                return UnitLevel.Tiret;
            if (k.StartsWith("zdani", StringComparison.Ordinal))
                return UnitLevel.Sentence;

            return UnitLevel.None;
        }

        private static int MapTiretDepth(string keyword) =>
            PolishNumerals.TryParseMultiplicity(keyword, out var depth) ? depth : 1;
    }
}
