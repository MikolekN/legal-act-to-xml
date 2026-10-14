using System.Text.RegularExpressions;
using Saga.Core.Helpers;
using Saga.Model;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Komenda, której przedmiotem jest tekst, a nie jednostka: zdanie albo akapit („zdanie pierwsze otrzymuje
    /// brzmienie:", „akapity czwarty i piąty otrzymują brzmienie:", „dodaje się akapit w brzmieniu:"). Treść cytowana
    /// jest wtedy tekstem — ani styl jej akapitów (szablon składa cytowane zdania stylem części wspólnej), ani
    /// oznaczenie na ich początku nie czynią z niej jednostek (ADR-0079).
    /// </summary>
    internal static class QuotedTextSubject
    {
        // Przedmiot tuż przed zwrotem komendy; między nimi tylko liczebniki porządkowe i spójniki („czwarty i piąty",
        // „od pierwszego do trzeciego"). Inny rzeczownik albo oznaczenie („akapit szósty i rysunek 6.3.2.10a",
        // „na końcu zdania zastępuje się średnikiem i dodaje się pkt 8") czyni przedmiot innym albo mieszanym.
        // Miejscownik („w zdaniu pierwszym …", „w akapicie drugim …") przedmiotu nie nazywa.
        private static readonly Regex Subject = new(
            $@"(?<![\p{{L}}])(?<noun>zdani[ea]|akapity?)(?:\s+(?:{PolishNumerals.OrdinalAlternation}|i|oraz|od|do))*\s+(?:otrzymuj[eą]\s+brzmienie|w\s+brzmieniu)\s*:\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Czy komenda zmienia albo dodaje zdanie lub akapit.</summary>
        internal static bool Names(string? commandText) => SubjectOf(commandText) is not null;

        /// <summary>
        /// Treść cytowana jako tekst — akapity w kolejności, każdy w osobnym wierszu — gdy przedmiotem komendy jest
        /// zdanie albo akapit. Null, gdy przedmiotem jest co innego albo treść składa zwykły odczyt jednostek: stoi w niej
        /// tabela lub wzór, albo cytowane zdanie biegnie przez kilka akapitów — zdanie z wyliczeniem („…: 1) …; 2) …")
        /// obejmuje pozycje wyliczenia, a te są jednostkami przepisu, który zmienia (§ 56 ZTP).
        /// </summary>
        internal static AmendmentContent? Read(string? commandText, IReadOnlyList<CollectedAmendmentParagraph> paragraphs)
        {
            if (SubjectOf(commandText) is not { } noun
                || paragraphs.Any(paragraph => paragraph.Table is not null || paragraph.FormulaMathMl is not null))
            {
                return null;
            }

            var lines = paragraphs.Select(paragraph => paragraph.Text.Trim()).Where(text => text.Length > 0).ToList();
            var isSentence = noun.StartsWith("zdani", StringComparison.OrdinalIgnoreCase);
            if (lines.Count == 0 || (isSentence && lines.Count > 1))
                return null;

            return new AmendmentContent { ObjectType = AmendmentObjectType.None, PlainText = string.Join(Environment.NewLine, lines) };
        }

        private static string? SubjectOf(string? commandText) =>
            !string.IsNullOrWhiteSpace(commandText) && Subject.Match(commandText.TrimEnd()) is { Success: true } subject
                ? subject.Groups["noun"].Value
                : null;
    }
}
