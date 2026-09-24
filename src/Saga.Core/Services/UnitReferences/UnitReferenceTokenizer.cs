using System.Text.RegularExpressions;
using Saga.Core.Helpers;

namespace Saga.Core.Services.UnitReferences
{
    /// <summary>Poziom jednostki redakcyjnej w powołaniu.</summary>
    internal enum UnitLevel
    {
        None = 0,
        Article = 1,
        Paragraph = 2,
        Point = 3,
        Letter = 4,
        Tiret = 5,
        Sentence = 6,
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

        /// <summary>Przedział oznaczeń: „85c–85j", „a–c", „czwarte–szóste".</summary>
        internal static readonly string RangePattern =
            @"\d+[a-zA-Z]*\s*[-–]\s*\d+[a-zA-Z]*" +
            @"|\b[a-z]{1,2}\s*[-–]\s*[a-z]{1,2}\b" +
            @"|" + OrdinalWord + @"\s*[-–]\s*" + OrdinalWord;

        /// <summary>Oznaczenie jednostki: numer (z indeksem górnym „125[1]"), liczebnik porządkowy albo litera.</summary>
        internal static readonly string DesignationPattern =
            @"\d+[a-zA-Z]*(?:\s*\[\s*\d+[a-zA-Z]*\s*\])?|" + OrdinalWord + @"|\b[a-z]{1,2}\b";

        private static readonly Regex TokenPattern = new(
            @"(?<prosescope>w\s+zakresie\s*,\s*w\s+jakim.*|w\s+jakim\s+dotycz\p{L}*.*)" +
            @"|(?<scope>w\s+zakresie|w\s+odniesieniu\s+do|w\s+stosunku\s+do)" +
            @"|(?<dash>\s[-–]\s)" +
            @"|(?<unit>" + UnitKeywordPattern + ")" +
            @"|(?<range>" + RangePattern + ")" +
            @"|(?<separator>,|;|:|\bi\b|\boraz\b|\blub\b|\balbo\b)" +
            @"|(?<designation>" + DesignationPattern + ")",
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

        /// <summary>Rozbija tekst na tokeny.</summary>
        internal static IReadOnlyList<UnitToken> Tokenize(string? text)
        {
            var tokens = new List<UnitToken>();
            if (string.IsNullOrWhiteSpace(text))
                return tokens;

            var input = text.Trim();
            var cursor = 0;

            foreach (Match match in TokenPattern.Matches(input))
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

        private static void AddProse(List<UnitToken> tokens, string fragment)
        {
            var trimmed = fragment.Trim(' ', '\t', '.', '„', '”', '"');
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
                var parts = raw.Split(new[] { '-', '–' }, 2, StringSplitOptions.TrimEntries);
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
