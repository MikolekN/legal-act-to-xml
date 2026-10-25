using System.Text.RegularExpressions;
using Saga.Core.Helpers;
using Saga.Model.EntryIntoForce;

namespace Saga.Core.Services.EntryIntoForce
{
    /// <summary>Rozkłada klauzule („wchodzi w życie") na strukturalny opis formuły.</summary>
    internal static class EntryIntoForceFormulaParser
    {
        /// <summary>
        /// Formuły w kolejności sprawdzania.
        /// </summary>
        private static readonly (Regex Pattern, EntryIntoForceFormulaKind Kind, int? DefaultOperand)[] Cascade =
        {
            (EntryIntoForcePatterns.FirstDayAfterLapseOfMonths, EntryIntoForceFormulaKind.FirstDayOfMonthAfterLapseOfMonths, 1),
            (EntryIntoForcePatterns.FirstDayAfterLapseOfDays, EntryIntoForceFormulaKind.FirstDayOfMonthAfterLapseOfDays, null),
            (EntryIntoForcePatterns.FirstDayOfMonthAfterAnnouncementMonth, EntryIntoForceFormulaKind.FirstDayOfMonthAfterAnnouncementMonth, null),
            (EntryIntoForcePatterns.DayAfterAnnouncement, EntryIntoForceFormulaKind.DayAfterAnnouncement, null),
            (EntryIntoForcePatterns.DayAfterAdoption, EntryIntoForceFormulaKind.DayAfterAdoption, null),
            (EntryIntoForcePatterns.DeterminedByCommunication, EntryIntoForceFormulaKind.DeterminedByCommunication, null),
            (EntryIntoForcePatterns.DeterminedByIntroducingAct, EntryIntoForceFormulaKind.DeterminedByIntroducingAct, null),
            (EntryIntoForcePatterns.OnSpecificDate, EntryIntoForceFormulaKind.OnSpecificDate, null),
            (EntryIntoForcePatterns.AfterMonths, EntryIntoForceFormulaKind.AfterMonthsFromAnnouncement, 1),
            (EntryIntoForcePatterns.AfterWeeks, EntryIntoForceFormulaKind.AfterWeeksFromAnnouncement, 1),
            (EntryIntoForcePatterns.AfterYears, EntryIntoForceFormulaKind.AfterYearsFromAnnouncement, 1),
            (EntryIntoForcePatterns.AfterDays, EntryIntoForceFormulaKind.AfterDaysFromAnnouncement, null),
            (EntryIntoForcePatterns.OnAnnouncementDay, EntryIntoForceFormulaKind.OnAnnouncementDay, null),
            (EntryIntoForcePatterns.OnAdoptionDay, EntryIntoForceFormulaKind.OnAdoptionDay, null),
        };

        /// <summary>Rozpoznaje formułę w podanej klauzuli.</summary>
        internal static EntryIntoForceFormula Parse(string? tail)
        {
            if (Trimmed(tail) is not { } text)
                return new EntryIntoForceFormula { Kind = EntryIntoForceFormulaKind.Unrecognized, RawText = string.Empty };

            foreach (var (pattern, kind, defaultOperand) in Cascade)
            {
                var match = pattern.Match(text);
                if (!match.Success)
                    continue;

                return new EntryIntoForceFormula
                {
                    Kind = kind,
                    Operand = ReadOperand(match, defaultOperand),
                    ExplicitDate = ReadExplicitDate(match, kind),
                    ReferenceDateOverride = ReadReferenceDate(match),
                    RawText = match.Value.Trim(),
                };
            }

            return new EntryIntoForceFormula { Kind = EntryIntoForceFormulaKind.Unrecognized, RawText = text };
        }

        private static string? Trimmed(string? text) =>
            (text ?? string.Empty).Trim() is { Length: > 0 } value ? value : null;

        /// <summary>Odcina część o mocy wstecznej i zwraca ją osobno.</summary>
        internal static (string Tail, DateTime? RetroactiveFrom, string? RetroactiveRawText) SplitRetroactivity(string? tail)
        {
            if (Trimmed(tail) is not { } text)
                return (string.Empty, null, null);

            var match = EntryIntoForcePatterns.Retroactivity.Match(text);
            if (!match.Success)
                return (text, null, null);

            var remainder = (text[..match.Index] + text[(match.Index + match.Length)..]).Trim();
            DateTime? retro = PolishDates.TryParseLongDate(match.Groups["date"].Value, out var parsed) ? parsed : null;

            return (remainder, retro, match.Value.Trim());
        }

        /// <summary>
        /// Rozbija człon „z tym że …" na pary (powołanie jednostek, moc wsteczna). Człon potrafi
        /// nieść kilka par naraz: „art. 1 pkt 5 z mocą od dnia 1 stycznia 2025 r., a art. 2 z mocą
        /// od dnia 1 marca 2025 r." — powołanie każdej pary to tekst od końca poprzedniej mocy
        /// wstecznej do początku kolejnej.
        /// </summary>
        internal static IEnumerable<(string Units, DateTime RetroactiveFrom, string RawText)>
            SplitScopedRetroactivity(string? chunk)
        {
            if (Trimmed(chunk) is not { } text)
                yield break;

            var cursor = 0;

            foreach (Match match in EntryIntoForcePatterns.Retroactivity.Matches(text))
            {
                var units = EntryIntoForcePatterns.LeadingConjunction
                    .Replace(text[cursor..match.Index], string.Empty)
                    .Trim()
                    .TrimEnd(',', ' ');

                cursor = match.Index + match.Length;

                if (units.Length > 0 && PolishDates.TryParseLongDate(match.Groups["date"].Value, out var date))
                    yield return (units, date, match.Value.Trim());
            }
        }

        /// <summary>Data odniesienia jawnie podana („…od dnia 1 marca 2025 r.").</summary>
        private static DateTime? ReadReferenceDate(Match match) =>
            match.Groups["ref"].Success && PolishDates.TryParseLongDate(match.Groups["ref"].Value, out var parsed)
                ? parsed
                : null;

        private static int? ReadOperand(Match match, int? defaultOperand)
        {
            var raw = match.Groups["n"].Success ? match.Groups["n"].Value : null;

            if (string.IsNullOrWhiteSpace(raw))
                return defaultOperand;

            if (int.TryParse(raw, out var digits))
                return digits;

            return PolishNumerals.TryParseCardinal(raw, out var spelled) ? spelled : null;
        }

        private static DateTime? ReadExplicitDate(Match match, EntryIntoForceFormulaKind kind)
        {
            if (kind != EntryIntoForceFormulaKind.OnSpecificDate)
                return null;

            return PolishDates.TryParseLongDate(match.Groups["date"].Value, out var parsed) ? parsed : null;
        }
    }
}
