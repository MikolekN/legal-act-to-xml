using System.Text.RegularExpressions;
using Saga.Model;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Czyta zmianę załączników zapisaną odesłaniem do załączników aktu zmieniającego (ADR-0058):
    /// „załączniki nr 1 i 2 do ustawy otrzymują brzmienie określone w załącznikach nr 1 i 2 do niniejszej ustawy",
    /// „po załączniku nr 2 dodaje się załączniki nr 2a i 2b w brzmieniu określonym w załącznikach nr 3 i 4 do
    /// niniejszego rozporządzenia". Część załącznika zmieniana tak samo („w załączniku nr 4 tabela D otrzymuje
    /// brzmienie określone w załączniku …", „… dodaje się lp. 73 w brzmieniu określonym w załączniku …") daje instrukcję
    /// ręczną na załączniku, w którym stoi (ADR-0108).
    /// </summary>
    internal static class AnnexInstructionReader
    {
        private const string Numbers = @"nr\s+(?<{0}>\d+[a-z]*(?:\s*(?:[," + Dashes.Any + @"]|i|oraz)\s*\d+[a-z]*)*)";

        // „określone w załącznikach nr 1 i 2", „określone odpowiednio w załącznikach nr 1 i 2" — „odpowiednio" mówi
        // tylko, że załączniki idą parami w kolejności wyliczenia.
        private static readonly string Sources =
            @"(?:odpowiednio\s+)?w\s+załącznik(?:u|(?<sourcesPlural>ach))\s*(?:" + string.Format(Numbers, "sources") + @"\s*)?do\s+niniejsz";

        // „okreś- lone" — wyraz przeniesiony w źródle ręcznie (dywiz i spacja w jego środku).
        private const string Specified = @"okreś(?:-\s?)?lon";

        private static readonly Regex Replacement = new(
            @"(?<![\p{L}])załącznik(?<targetsPlural>i)?\s+(?:" + string.Format(Numbers, "targets") + @"\s+)?(?:do\s+[\p{L}]+\s+)?otrzymuj[eą]\s+brzmienie\s+" + Specified + @"e\s+" + Sources,
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex Addition = new(
            @"(?:po\s+załączniku\s+nr\s+(?<anchor>\d+[a-z]*)\s+(?:do\s+[\p{L}]+\s+)?)?dodaje\s+się\s+załącznik(?:i)?\s+(?:"
            + string.Format(Numbers, "added") + @"\s+)?(?:do\s+[\p{L}]+\s+)?w\s+brzmieniu\s+" + Specified + @"ym\s+" + Sources,
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Załącznik aktu zmienianego jako miejsce zmiany: „w załączniku nr 1 do rozporządzenia", „w załączniku nr 4
        // „Wykaz świadczeń …”", „w załączniku do rozporządzenia".
        private static readonly string InAnnex =
            @"(?<![\p{L}])w\s+załączniku\s+(?:nr\s+(?<target>\d+[a-z]*)\s+)?(?:do\s+(?!niniejsz)[\p{L}]+\s+)?"
            + $@"(?:{QuotationMarks.Opening}[^{QuotationMarks.Closing}]*[{QuotationMarks.Closing}]\s+)?";

        // Część załącznika zmieniana odesłaniem: „w załączniku nr 1 do rozporządzenia tabela C otrzymuje brzmienie
        // określone w załączniku nr 1 do niniejszego rozporządzenia".
        private static readonly Regex PartReplacement = new(
            InAnnex + @"(?<part>\S.*?)\s+otrzymuj[eą]\s+brzmienie\s+" + Specified + @"e\s+" + Sources,
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Część dodawana odesłaniem: „w załączniku nr 4 „…” dodaje się lp. 73 w brzmieniu określonym w załączniku do
        // niniejszego rozporządzenia".
        private static readonly Regex PartAddition = new(
            InAnnex + @"(?:\S.*?\s+)?dodaje\s+się\s+(?<part>\S.*?)\s+w\s+brzmieniu\s+" + Specified + @"ym\s+" + Sources,
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Załącznik części załącznika: „załącznik nr 1 do Programu „Wydatki …” otrzymuje brzmienie określone w załączniku do
        // niniejszej uchwały" (Program jest załącznikiem uchwały zmienianej — M.P. 2026 poz. 912). Załącznik samego aktu
        // nazywa się „do ustawy", „do rozporządzenia" …, więc ten jest częścią załącznika, w którym stoi.
        private static readonly Regex NestedAnnexReplacement = new(
            @"(?<![\p{L}])(?<part>załącznik\s+nr\s+\d+[a-z]*\s+do\s+(?!(?:ustaw|rozporządze|uchwał|zarządze|obwieszcze|kodeks|niniejsz)\p{L}*)[\p{L}]+"
            + $@"(?:\s+{QuotationMarks.Opening}[^{QuotationMarks.Closing}]*[{QuotationMarks.Closing}])?)\s+otrzymuje\s+brzmienie\s+" + Specified + @"e\s+" + Sources,
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Część zmieniana albo dodawana odesłaniem w przepisie pod miejscem w załączniku („2) w załączniku do uchwały:" /
        // „a) rozdział I „Informacje ogólne” otrzymuje brzmienie określone w załączniku nr 1 do niniejszej uchwały" —
        // M.P. 2026 poz. 343): załącznik nazywa przepis nadrzędny, ten nazywa tylko część.
        private static readonly Regex PartReplacementUnderAnAnnex = new(
            @"^(?<part>\S.*?)\s+otrzymuj[eą]\s+brzmienie\s+" + Specified + @"e\s+" + Sources,
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex PartAdditionUnderAnAnnex = new(
            @"^(?:\S.*?\s+)?dodaje\s+się\s+(?<part>\S.*?)\s+w\s+brzmieniu\s+" + Specified + @"ym\s+" + Sources,
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Przepis nadrzędny podający miejsce zmian: „w załączniku nr 3 do rozporządzenia:", „w tabeli XII:",
        // „w rozdziale III „…”:" — ewentualnie z zapowiedzią zmian, która miejsca nie zmienia.
        private static readonly Regex AnnexOfAnIntroduction = new(
            @"(?<![\p{L}])w\s+załączniku(?:\s+nr\s+\d+[a-z]*)?(?:\s+do\s+(?!niniejsz)[\p{L}]+)?"
            + $@"(?:\s+{QuotationMarks.Opening}[^{QuotationMarks.Closing}]*[{QuotationMarks.Closing}])?(?<rest>[^:]*):\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex PlaceIntroduction = new(@"^w\s+[^:]+:\s*$", RegexOptions.Compiled);
        private static readonly Regex NamesAnAct = new(@"\bz\s+dnia\b|\((?:Dz\.|M\.\s?P\.)", RegexOptions.Compiled);
        private static readonly Regex ChangesAnnounced = new(@"\s*\bwprowadza\s+się\s+(?:następując[eą]\s+)?zmian[ay]\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex Range = new($@"^(?<from>\d+)\s*[{Dashes.Any}]\s*(?<to>\d+)$", RegexOptions.Compiled);
        private static readonly Regex Item = new($@"\d+[a-z]*(?:\s*[{Dashes.Any}]\s*\d+[a-z]*)?", RegexOptions.Compiled);
        private static readonly Regex Number = new(@"\d+[a-z]*", RegexOptions.Compiled);
        private static readonly Regex LetteredNumber = new(@"^(?<number>\d+)[a-z]+$", RegexOptions.Compiled);

        /// <summary>Najdłuższy przedział rozwijany w pojedyncze załączniki; dłuższy to raczej błąd odczytu.</summary>
        private const int LongestRange = 50;

        /// <summary>
        /// Część zmieniana albo dodawana odesłaniem w przepisie, który stoi w załączniku aktu zmienianego z woli przepisu
        /// nadrzędnego („w załączniku nr 13 do ustawy:") i nazywa tylko część — instrukcja na tym załączniku. Treść
        /// przepisu bez jego oznaczenia; <paramref name="places"/> — miejsca w załączniku z przepisów nadrzędnych, od
        /// najdalszego („w tabeli XII"), którymi część się zaczyna, tak jak w przepisie, który nazywa wszystko sam
        /// („w tabeli VI część I. Urzędy morskie"). Pusta lista, gdy przepis nie zmienia części odesłaniem.
        /// </summary>
        internal static IReadOnlyList<AnnexInstruction> ReadPartUnderAnAnnex(string? content, string enclosingAnnex, IReadOnlyList<string>? places = null)
        {
            if (string.IsNullOrWhiteSpace(content))
                return [];

            content = content.Trim();
            var added = PartAdditionUnderAnAnnex.Match(content);
            var match = added.Success ? added : PartReplacementUnderAnAnnex.Match(content);
            if (!match.Success)
                return [];

            var instruction = Part(match, adds: added.Success, enclosingAnnex);
            if (places is { Count: > 0 })
                instruction.Part = string.Join(" ", places.Append(instruction.Part));
            return [instruction];
        }

        /// <summary>
        /// Miejsce w załączniku, które podaje przepis nadrzędny zakończony dwukropkiem: „w tabeli XII:" → „w tabeli XII",
        /// „w załączniku nr 3 do rozporządzenia w tabeli XII:" → „w tabeli XII" (i <paramref name="namesTheAnnex"/>);
        /// null, gdy przepis miejsca nie podaje (sam załącznik też nie jest miejscem w nim). Przepis nazywający akt
        /// („w ustawie z dnia …") miejscem w załączniku nie jest.
        /// </summary>
        internal static string? PlaceOfAnIntroduction(string? text, out bool namesTheAnnex)
        {
            namesTheAnnex = false;
            text = text?.Trim();
            if (string.IsNullOrEmpty(text))
                return null;

            if (AnnexOfAnIntroduction.Match(text) is { Success: true } annex)
            {
                namesTheAnnex = true;
                var rest = ChangesAnnounced.Replace(annex.Groups["rest"].Value, string.Empty).Trim();
                return rest.Length > 0 ? rest : null;
            }

            if (!PlaceIntroduction.IsMatch(text) || NamesAnAct.IsMatch(text))
                return null;

            var place = ChangesAnnounced.Replace(text.TrimEnd().TrimEnd(':'), string.Empty).Trim();
            return AmendmentCommandParser.HasChangeVerb(place) ? null : place;
        }

        /// <summary>Instrukcje zapisane w przepisie; pusta lista, gdy przepis nie zmienia załączników odesłaniem.</summary>
        internal static IReadOnlyList<AnnexInstruction> Read(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return [];

            var addition = Addition.Match(text);
            if (addition.Success)
                return [Added(addition)];

            if (NestedAnnexReplacement.Match(text) is { Success: true } nested)
                return [Part(nested, adds: false)];

            var replacement = Replacement.Match(text);
            if (replacement.Success)
                return Replaced(replacement);

            if (PartAddition.Match(text) is { Success: true } added)
                return [Part(added, adds: true)];

            return PartReplacement.Match(text) is { Success: true } replaced ? [Part(replaced, adds: false)] : [];
        }

        /// <summary>
        /// Część załącznika zmieniana albo dodawana odesłaniem: jedna instrukcja na załącznik, w którym stoi, z częścią
        /// nazwaną tak jak w przepisie. Załącznik bez numeru to jedyny — „1" (ADR-0099).
        /// </summary>
        private static AnnexInstruction Part(Match match, bool adds, string? enclosingAnnex = null) => new()
        {
            Adds = adds,
            Target = match.Groups["target"].Success ? match.Groups["target"].Value : enclosingAnnex ?? Annex.OnlyAnnexNumber,
            Part = match.Groups["part"].Value.Trim().TrimEnd(',', ' '),
            Source = Expand(match.Groups["sources"].Value, single: !match.Groups["sourcesPlural"].Success).FirstOrDefault(),
        };

        /// <summary>
        /// Zastąpienie: załączniki aktu zmienianego dostają brzmienie kolejnych załączników aktu zmieniającego.
        /// Gdy po obu stronach jest ich tyle samo, tworzą pary; inaczej źródło zostaje niewskazane. Załącznik bez
        /// numeru („załącznik do rozporządzenia", „w załączniku do niniejszego rozporządzenia") jest jedynym, więc
        /// pierwszym — „1" (ADR-0099).
        /// </summary>
        private static List<AnnexInstruction> Replaced(Match match)
        {
            var targets = Expand(match.Groups["targets"].Value, single: !match.Groups["targetsPlural"].Success);
            var sources = Expand(match.Groups["sources"].Value, single: !match.Groups["sourcesPlural"].Success);
            if (targets.Count == 0)
                return [];

            return targets
                .Select((target, index) => new AnnexInstruction
                {
                    Target = target,
                    Source = sources.Count == targets.Count ? sources[index] : null,
                })
                .ToList();
        }

        /// <summary>
        /// Dodanie: jedna instrukcja na wszystkie dodawane załączniki. Miejsce to załącznik wskazany („po załączniku
        /// nr 2") albo poprzedzający pierwszy dodawany — załączniki numeruje się kolejno („nr 4" staje po „nr 3",
        /// „nr 2a" po „nr 2").
        /// </summary>
        private static AnnexInstruction Added(Match match)
        {
            var added = Expand(match.Groups["added"].Value);
            var sources = Expand(match.Groups["sources"].Value, single: !match.Groups["sourcesPlural"].Success);

            return new AnnexInstruction
            {
                Adds = true,
                Target = match.Groups["anchor"].Success ? match.Groups["anchor"].Value : Preceding(added.FirstOrDefault()),
                Source = sources.FirstOrDefault(),
                SourceTo = sources.Count > 1 ? sources[^1] : null,
            };
        }

        private static string? Preceding(string? number)
        {
            if (number is null)
                return null;

            if (LetteredNumber.Match(number) is { Success: true } lettered)
                return lettered.Groups["number"].Value;

            return int.TryParse(number, out var value) && value > 1 ? (value - 1).ToString() : null;
        }

        /// <summary>
        /// Numery z wyliczenia („1, 3 i 5") i przedziału liczb („3–9" → 3, 4, …, 9). Załącznik w liczbie pojedynczej
        /// bez numeru (<paramref name="single"/>) to jedyny załącznik aktu — „1" (ADR-0099).
        /// </summary>
        private static List<string> Expand(string numbers, bool single = false)
        {
            if (numbers.Length == 0)
                return single ? [Annex.OnlyAnnexNumber] : [];

            var result = new List<string>();
            foreach (Match item in Item.Matches(numbers))
            {
                if (Range.Match(item.Value) is { Success: true } range
                    && int.TryParse(range.Groups["from"].Value, out var from)
                    && int.TryParse(range.Groups["to"].Value, out var to)
                    && to >= from && to - from < LongestRange)
                {
                    result.AddRange(Enumerable.Range(from, to - from + 1).Select(number => number.ToString()));
                }
                else
                {
                    // Przedział, którego nie da się rozwinąć („2a–2c"), zostawia swoje końce.
                    result.AddRange(Number.Matches(item.Value).Select(number => number.Value));
                }
            }

            return result;
        }
    }
}
