using System.Text.RegularExpressions;
using Saga.Core.Helpers;

namespace Saga.Core.Services.EntryIntoForce
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

        private static readonly Regex TokenPattern = new(
            @"(?<prosescope>w\s+zakresie\s*,\s*w\s+jakim.*|w\s+jakim\s+dotycz\p{L}*.*)" +
            @"|(?<scope>w\s+zakresie|w\s+odniesieniu\s+do|w\s+stosunku\s+do)" +
            @"|(?<dash>\s[-–]\s)" +
            @"|(?<unit>art\.|§|ust\.|pkt|lit\.|(?:" + PolishNumerals.MultiplicityAlternation +
                @")\s+tiret\p{L}*|tiret\p{L}*|zdani\p{L}+)" +
            @"|(?<range>\d+[a-zA-Z]*\s*[-–]\s*\d+[a-zA-Z]*" +
            @"|\b[a-z]{1,2}\s*[-–]\s*[a-z]{1,2}\b" +
            @"|" + OrdinalWord + @"\s*[-–]\s*" + OrdinalWord + ")" +
            @"|(?<separator>,|;|:|\bi\b|\boraz\b|\blub\b|\balbo\b)" +
            @"|(?<designation>\d+[a-zA-Z]*(?:\s*\[\s*\d+[a-zA-Z]*\s*\])?|" + OrdinalWord + @"|\b[a-z]{1,2}\b)",
            Opts);

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
            return tokens;
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
