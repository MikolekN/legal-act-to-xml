using System.Text.RegularExpressions;
using Saga.Model;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Czyta zmianę załączników zapisaną odesłaniem do załączników aktu zmieniającego (ADR-0058):
    /// „załączniki nr 1 i 2 do ustawy otrzymują brzmienie określone w załącznikach nr 1 i 2 do niniejszej ustawy",
    /// „po załączniku nr 2 dodaje się załączniki nr 2a i 2b w brzmieniu określonym w załącznikach nr 3 i 4 do
    /// niniejszego rozporządzenia". Przedmiotem komendy musi być sam załącznik — „w załączniku nr 4 tabela D otrzymuje
    /// brzmienie określone w załączniku …" zmienia część załącznika i tu nie pasuje.
    /// </summary>
    internal static class AnnexInstructionReader
    {
        private const string Numbers = @"nr\s+(?<{0}>\d+[a-z]*(?:\s*(?:[," + Dashes.Any + @"]|i|oraz)\s*\d+[a-z]*)*)";

        private static readonly string Sources =
            @"w\s+załącznik(?:u|ach)\s*(?:" + string.Format(Numbers, "sources") + @"\s*)?do\s+niniejsz";

        private static readonly Regex Replacement = new(
            @"(?<![\p{L}])załącznik(?:i)?\s+(?:" + string.Format(Numbers, "targets") + @"\s+)?(?:do\s+[\p{L}]+\s+)?otrzymuj[eą]\s+brzmienie\s+określone\s+" + Sources,
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex Addition = new(
            @"(?:po\s+załączniku\s+nr\s+(?<anchor>\d+[a-z]*)\s+(?:do\s+[\p{L}]+\s+)?)?dodaje\s+się\s+załącznik(?:i)?\s+(?:"
            + string.Format(Numbers, "added") + @"\s+)?(?:do\s+[\p{L}]+\s+)?w\s+brzmieniu\s+określonym\s+" + Sources,
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex Range = new($@"^(?<from>\d+)\s*[{Dashes.Any}]\s*(?<to>\d+)$", RegexOptions.Compiled);
        private static readonly Regex Item = new($@"\d+[a-z]*(?:\s*[{Dashes.Any}]\s*\d+[a-z]*)?", RegexOptions.Compiled);
        private static readonly Regex Number = new(@"\d+[a-z]*", RegexOptions.Compiled);
        private static readonly Regex LetteredNumber = new(@"^(?<number>\d+)[a-z]+$", RegexOptions.Compiled);

        /// <summary>Najdłuższy przedział rozwijany w pojedyncze załączniki; dłuższy to raczej błąd odczytu.</summary>
        private const int LongestRange = 50;

        /// <summary>Instrukcje zapisane w przepisie; pusta lista, gdy przepis nie zmienia załączników odesłaniem.</summary>
        internal static IReadOnlyList<AnnexInstruction> Read(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return [];

            var addition = Addition.Match(text);
            if (addition.Success)
                return [Added(addition)];

            var replacement = Replacement.Match(text);
            return replacement.Success ? Replaced(replacement) : [];
        }

        /// <summary>
        /// Zastąpienie: załączniki aktu zmienianego dostają brzmienie kolejnych załączników aktu zmieniającego.
        /// Gdy po obu stronach jest ich tyle samo, tworzą pary; inaczej źródło zostaje niewskazane.
        /// </summary>
        private static List<AnnexInstruction> Replaced(Match match)
        {
            var targets = Expand(match.Groups["targets"].Value);
            var sources = Expand(match.Groups["sources"].Value);
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
            var sources = Expand(match.Groups["sources"].Value);

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

        /// <summary>Numery z wyliczenia („1, 3 i 5") i przedziału liczb („3–9" → 3, 4, …, 9).</summary>
        private static List<string> Expand(string numbers)
        {
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
