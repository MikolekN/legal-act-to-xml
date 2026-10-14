using System.Text.RegularExpressions;
using Saga.Core.Helpers;
using Saga.Model;

namespace Saga.Core.Services.EntryIntoForce
{
    /// <summary>
    /// Dzień stanu prawnego tekstu jednolitego: tekst jednolity ustawy sporządza się według stanu prawnego
    /// z dnia określonego w obwieszczeniu (§ 100a ZTP), a wzór obwieszczenia podaje go w punkcie 1:
    /// „…, z uwzględnieniem stanu prawnego na dzień … oraz zmian wprowadzonych:" (§ 104 ZTP). Obwieszczenia
    /// według wcześniejszego wzoru tego dnia nie podają (ADR-0072).
    /// </summary>
    internal static class ConsolidatedTextState
    {
        private static readonly Regex LegalStateDay = new(
            @"stanu\s+prawnego\s+na\s+dzień\s+(?<date>\d{1,2}\s+(?:" + PolishDates.MonthAlternation + @")\s+\d{4})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>Dzień stanu prawnego z treści obwieszczenia; null, gdy go nie podano.</summary>
        internal static DateTime? Read(Announcement announcement)
        {
            var texts = announcement.Points.Select(point => point.ContentText)
                .Append(announcement.Body?.ContentText)
                .Concat(announcement.Content.Select(block => block.Text));

            foreach (var text in texts)
            {
                if (text is not null && LegalStateDay.Match(text) is { Success: true } match
                    && PolishDates.TryParseLongDate(match.Groups["date"].Value, out var day))
                {
                    return day;
                }
            }

            return null;
        }
    }
}
