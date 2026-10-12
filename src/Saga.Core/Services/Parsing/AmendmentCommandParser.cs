using System.Text.RegularExpressions;
using Saga.Core.Helpers;
using Saga.Core.Services.UnitReferences;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Rodzaj komendy nowelizacyjnej rozpoznanej z treści (§ 82-97 ZTP).
    /// </summary>
    public enum AmendmentCommandKind
    {
        /// <summary>„… otrzymuje brzmienie:" — zmiana brzmienia (cytowana treść następuje po komendzie).</summary>
        Change,
        /// <summary>„po art. X dodaje się art. Xa w brzmieniu:" — dodanie jednostki (cytowana treść następuje).</summary>
        Add,
        /// <summary>„uchyla się …" — uchylenie (bez treści).</summary>
        Repeal,
        /// <summary>„wyrazy „X" zastępuje się wyrazami „Y"" — zamiana wyrazów (komenda kompletna w jednym akapicie).</summary>
        ReplaceWords,
        /// <summary>
        /// „po wyrazach „X" dodaje się wyrazy „Y"" albo „przed wyrazami „X" …" — dodanie wyrazów przy wskazanych
        /// (komenda kompletna w jednym akapicie). Jest zmianą w tekście jak zamiana wyrazów: wyrazy wskazane
        /// zastępuje się nimi samymi razem z dodanymi.
        /// </summary>
        AddWords,
    }

    /// <summary>
    /// Komenda nowelizacyjna wywnioskowana z treści akapitu-triggera.
    /// Źródło o NIŻSZYM priorytecie niż mapa stylów (styl zawsze dominuje).
    /// </summary>
    public sealed record AmendmentCommand
    {
        public required AmendmentCommandKind Kind { get; init; }

        /// <summary>Rodzaj jednostki będącej bezpośrednim przedmiotem komendy (token przy czasowniku).</summary>
        public AmendmentTargetKind TargetKind { get; init; } = AmendmentTargetKind.Unknown;

        /// <summary>Oznaczenie jednostki-przedmiotu komendy (np. „7a", „2", „b"); null gdy nierozpoznane.</summary>
        public string? TargetNumber { get; init; }

        /// <summary>ReplaceWords: brzmienie zastępowane (stare wyrazy). AddWords: wyrazy, przy których się dodaje.</summary>
        public string? OldWording { get; init; }

        /// <summary>ReplaceWords: brzmienie zastępujące (nowe wyrazy). AddWords: wyrazy wskazane razem z dodanymi.</summary>
        public string? NewWording { get; init; }

        /// <summary>Czy komenda jest zmianą w tekście jednostki (wyrazów), a nie zmianą jednostki.</summary>
        public bool ChangesText => Kind is AmendmentCommandKind.ReplaceWords or AmendmentCommandKind.AddWords;

        /// <summary>Pełny dopasowany tekst komendy (do wycinania przy analizie komend złożonych).</summary>
        public string? MatchedCommandText { get; init; }

        /// <summary>
        /// Wartość liczbowa oznaczenia jednostki: cyfrowa („2", „7a"→7) lub z liczebnika porządkowego
        /// nijakiego („tiret pierwsze"→1, kanoniczna forma powołania tiretu wg § 57 ust. 6 ZTP).
        /// Null gdy oznaczenie nieobecne/nierozpoznane.
        /// </summary>
        public int? TargetOrdinalValue { get; init; }
    }

    /// <summary>
    /// Parser komend nowelizacyjnych z treści akapitu (Etap 8): rozpoznaje rodzaj komendy oraz
    /// jednostkę-przedmiot z tokenu stojącego bezpośrednio przy czasowniku komendy. Uzupełnia
    /// mechanizmy stylowe TYLKO tam, gdzie stylów nie ma — wynik jest źródłem o niższym priorytecie
    /// niż mapa stylów (<see cref="StyleLibraryMapper.AmendmentStyleInfoMap"/>).
    ///
    /// </summary>
    public static class AmendmentCommandParser
    {
        // Token jednostki redakcyjnej + opcjonalne oznaczenie (przedział „5a-5g", numer „7a", litera „b",
        // liczebnik porządkowy „zdanie drugie") — słownik i kształt oznaczeń ze wspólnego źródła powołań.
        private static readonly string UnitToken =
            @"(?<unit>" + UnitReferenceTokenizer.UnitKeywordPattern + @")\s*(?<no>" +
            UnitReferenceTokenizer.RangePattern + "|" + UnitReferenceTokenizer.DesignationPattern + ")?";

        private static readonly Regex UnitTokenPattern = new(UnitToken, RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>„ust. 2 otrzymuje brzmienie:" / „art. 5 i 6 otrzymują brzmienie:" — token przy czasowniku.</summary>
        internal static readonly Regex ChangeCommandPattern = new(
            UnitToken + @"[^\p{L}]*(?:i\s+\d+[a-zA-Z]*\s*)?otrzymuj[eą]\s+brzmienie\s*:",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>„(po art. X) dodaje się art. Xa (w brzmieniu:)" — przedmiotem jest jednostka DODAWANA.</summary>
        internal static readonly Regex AddCommandPattern = new(
            @"dodaje\s+się\s+" + UnitToken,
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>„uchyla się art. 281" — przedmiotem jest jednostka uchylana.</summary>
        internal static readonly Regex RepealCommandPattern = new(
            @"uchyla\s+się\s+" + UnitToken,
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// „wyrazy „X" zastępuje się wyrazami „Y"" (§ 87 ZTP) — komenda kompletna w akapicie,
        /// bez cytowanego bloku po niej. Stare/nowe brzmienie w grupach old/new.
        /// Obsługuje też formę § 88 („użyte … wyrazy „X" zastępuje się użytymi w odpowiedniej liczbie
        /// i przypadku wyrazami „Y"") — dopuszczalna wstawka między „zastępuje się" a „wyrazami" —
        /// oraz proste cudzysłowy "…" jako ogranicznik brzmień (kanał TXT/PDF; Sanitize nie normalizuje cudzysłowów).
        /// </summary>
        internal static readonly Regex ReplaceWordsPattern = new(
            $@"wyraz(?:y|ów|em)?\s+[{QuotationMarks.OpeningOrStraight}](?<old>[^{QuotationMarks.Any}]*)[{QuotationMarks.ClosingOrStraight}]\s+zastępuje\s+się\s+"
            + $@"(?:[^{QuotationMarks.Any}]{{0,80}}?)wyraz(?:ami|em|y)?\s+[{QuotationMarks.OpeningOrStraight}](?<new>[^{QuotationMarks.Any}]*)[{QuotationMarks.ClosingOrStraight}]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// „po wyrazach „X" dodaje się wyrazy „Y"" i „przed wyrazami „X" dodaje się wyrazy „Y"" (§ 87 ZTP) —
        /// komenda kompletna w akapicie. Wstawka „przecinek i" („dodaje się przecinek i wyrazy „Y"") dokłada
        /// przecinek przed dodanymi wyrazami. Dodanie „od nowego wiersza" nie pasuje: nie da się go zapisać
        /// zamianą tekstu.
        /// </summary>
        internal static readonly Regex AddWordsPattern = new(
            $@"(?<side>po|przed)\s+wyraz(?:ach|ie|ami|em)\s+[{QuotationMarks.OpeningOrStraight}](?<anchor>[^{QuotationMarks.Any}]*)[{QuotationMarks.ClosingOrStraight}]\s+dodaje\s+się\s+"
            + $@"(?<comma>przecinek\s+(?:i|oraz)\s+)?wyraz(?:y|em)?\s+[{QuotationMarks.OpeningOrStraight}](?<added>[^{QuotationMarks.Any}]*)[{QuotationMarks.ClosingOrStraight}]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Rozpoznaje komendę nowelizacyjną w treści akapitu. Zwraca null, gdy akapit nie zawiera
        /// żadnej znanej komendy. Kolejność: ReplaceWords → Repeal → Add → Change (komenda „dodaje się …
        /// w brzmieniu:" zawiera też frazę zmiany, więc Add musi być sprawdzone przed Change).
        /// </summary>
        public static AmendmentCommand? Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var rw = ReplaceWordsPattern.Match(text);
            if (rw.Success)
            {
                // Jednostka-przedmiot zamiany wyrazów (jeśli wskazana przed komendą, np. „w art. 3 wyrazy …").
                var unitBefore = LastUnitBefore(text, rw.Index);
                return new AmendmentCommand
                {
                    Kind = AmendmentCommandKind.ReplaceWords,
                    TargetKind = unitBefore?.Kind ?? AmendmentTargetKind.Unknown,
                    TargetNumber = unitBefore?.Number,
                    TargetOrdinalValue = OrdinalValue(unitBefore?.Number),
                    OldWording = rw.Groups["old"].Value,
                    NewWording = rw.Groups["new"].Value,
                    MatchedCommandText = rw.Value,
                };
            }

            var aw = AddWordsPattern.Match(text);
            if (aw.Success)
            {
                var unitBefore = LastUnitBefore(text, aw.Index);
                var anchor = aw.Groups["anchor"].Value.Trim();
                var added = aw.Groups["added"].Value.Trim();
                if (aw.Groups["comma"].Success)
                    added = ", " + added;

                return new AmendmentCommand
                {
                    Kind = AmendmentCommandKind.AddWords,
                    TargetKind = unitBefore?.Kind ?? AmendmentTargetKind.Unknown,
                    TargetNumber = unitBefore?.Number,
                    TargetOrdinalValue = OrdinalValue(unitBefore?.Number),
                    OldWording = anchor,
                    NewWording = aw.Groups["side"].Value.Equals("przed", StringComparison.OrdinalIgnoreCase)
                        ? JoinWords(added, anchor)
                        : JoinWords(anchor, added),
                    MatchedCommandText = aw.Value,
                };
            }

            var repeal = RepealCommandPattern.Match(text);
            if (repeal.Success)
                return Build(AmendmentCommandKind.Repeal, repeal);

            var add = AddCommandPattern.Match(text);
            if (add.Success)
                return Build(AmendmentCommandKind.Add, add);

            var change = ChangeCommandPattern.Match(text);
            if (change.Success)
                return Build(AmendmentCommandKind.Change, change);

            return null;
        }

        /// <summary>
        /// Składa wyrazy stojące obok siebie w tekście: odstęp między nimi, chyba że drugie zaczynają się znakiem
        /// przestankowym albo nawiasem zamykającym („Funduszu" + „, o którym mowa" → „Funduszu, o którym mowa"),
        /// albo pierwsze kończą się nawiasem otwierającym.
        /// </summary>
        internal static string JoinWords(string left, string right)
        {
            if (left.Length == 0 || right.Length == 0)
                return left + right;

            var glued = right[0] is ',' or '.' or ';' or ':' or ')' or ']' || left[^1] is '(' or '[';
            return glued ? left + right : left + " " + right;
        }

        /// <summary>
        /// Wartość liczbowa oznaczenia: wiodące cyfry („7a"→7) lub liczebnik porządkowy nijaki („pierwsze"→1).
        /// </summary>
        private static int? OrdinalValue(string? designation)
        {
            if (string.IsNullOrEmpty(designation))
                return null;

            if (char.IsDigit(designation[0]))
            {
                int i = 0;
                while (i < designation.Length && char.IsDigit(designation[i])) i++;
                return int.TryParse(designation[..i], out var n) ? n : null;
            }

            return PolishNumerals.TryParseOrdinal(designation, out var ord) ? ord : null;
        }

        private static AmendmentCommand Build(AmendmentCommandKind kind, Match m)
        {
            var designation = m.Groups["no"].Success && m.Groups["no"].Value.Length > 0 ? m.Groups["no"].Value : null;
            return new AmendmentCommand
            {
                Kind = kind,
                TargetKind = MapUnit(m.Groups["unit"].Value),
                TargetNumber = designation,
                TargetOrdinalValue = OrdinalValue(designation),
                MatchedCommandText = m.Value,
            };
        }

        /// <summary>Ostatni token jednostki przed pozycją <paramref name="beforeIndex"/> (np. „w art. 3 wyrazy…").</summary>
        private static (AmendmentTargetKind Kind, string? Number)? LastUnitBefore(string text, int beforeIndex)
        {
            Match? last = null;
            foreach (Match m in UnitTokenPattern.Matches(text[..beforeIndex]))
                last = m;
            if (last == null)
                return null;
            return (MapUnit(last.Groups["unit"].Value),
                last.Groups["no"].Success && last.Groups["no"].Value.Length > 0 ? last.Groups["no"].Value : null);
        }

        /// <summary>
        /// Rodzaj jednostki dla słowa kluczowego — poziom ze wspólnego źródła, bez kontekstu ścieżki
        /// (samodzielny „§" to jednostka główna; paragraf kodeksu rozstrzyga dopiero adres celu).
        /// </summary>
        private static AmendmentTargetKind MapUnit(string unit) =>
            UnitReferenceTokenizer.LevelOf(unit) switch
            {
                UnitLevel.Article => AmendmentTargetKind.Article,
                UnitLevel.Paragraph => AmendmentTargetKind.Paragraph,
                UnitLevel.Point => AmendmentTargetKind.Point,
                UnitLevel.Letter => AmendmentTargetKind.Letter,
                UnitLevel.Tiret => AmendmentTargetKind.Tiret,
                UnitLevel.Sentence => AmendmentTargetKind.Fragment,
                _ => AmendmentTargetKind.Unknown,
            };
    }
}
