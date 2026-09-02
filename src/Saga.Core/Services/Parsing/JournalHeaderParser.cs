using System.Text.RegularExpressions;
using Saga.Core.Helpers;
using Saga.Core.Ingest;
using Saga.Model;
using Serilog;

namespace Saga.Core.Services.Parsing
{
    /// <summary>Czyta metadane publikatora z nagłówka aktu ogłoszonego.</summary>
    internal static class JournalHeaderParser
    {
        /// <summary>Publikator</summary>
        private static readonly Regex PublisherLine = new(
            @"^(?<publisher>DZIENNIK\s+USTAW|MONITOR\s+POLSKI)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Data ogłoszenia</summary>
        private static readonly Regex AnnouncementDateLine = new(
            @"(?:^|\s)dnia\s+(?<day>\d{1,2})\s+(?<month>" + PolishDates.MonthAlternation + @")\s+(?<year>\d{4})(?:\s*r\.?)?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>Pozycja publikatora</summary>
        private static readonly Regex PositionLine = new(
            @"\bPoz\.\s*(?<pos>\d{1,6})\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Zapisuje w dokumencie metadane odczytane z nagłówka.</summary>
        internal static void Apply(IReadOnlyList<DocumentBlock>? blocks, LegalDocument document)
        {
            if (blocks is null)
                return;

            var lines = HeaderLines(blocks);
            if (lines.Count == 0)
                return;


            var publisher = lines.Select(TryPublisher).FirstOrDefault(p => p is not null);
            if (publisher is null)
            {
                Log.Debug("Nagłówek bez rozpoznanego publikatora — metadane pominięte: {Header}",
                    string.Join(" | ", lines.Take(4)));
                return;
            }

            var date = lines.Select(TryAnnouncementDate).FirstOrDefault(d => d is not null);
            var position = lines.Select(TryPosition).FirstOrDefault(p => p is not null);

            if (date is null && position is null)
            {
                Log.Debug("Nagłówek publikatora bez daty i pozycji — metadane pominięte.");
                return;
            }

            document.SourceJournal.Publisher ??= publisher;

            if (date is { } announced)
            {
                document.AnnouncementDate ??= announced;
                document.SourceJournal.Year ??= announced.Year;
            }

            if (position is { } number && !document.SourceJournal.Positions.Contains(number))
                document.SourceJournal.Positions.Add(number);

            if (string.IsNullOrEmpty(document.SourceJournal.SourceString))
                document.SourceJournal.SourceString = string.Join(" ", lines);

            Log.Information("Nagłówek publikatora: {Publisher}, data ogłoszenia {Date}, poz. {Position}",
                publisher, date, position);
        }

        private static List<string> HeaderLines(IReadOnlyList<DocumentBlock> blocks) =>
            blocks
                .Where(b => b is { Role: BlockRole.JournalHeader } && !b.IsEmpty)
                .Select(b => b.Text.Sanitize().Trim())
                .Where(line => line.Length > 0)
                .ToList();

        private static PublisherType? TryPublisher(string segment)
        {
            var match = PublisherLine.Match(segment);
            if (!match.Success)
                return null;

            return match.Groups["publisher"].Value.StartsWith("DZIENNIK", StringComparison.OrdinalIgnoreCase)
                ? PublisherType.DziennikUstaw
                : PublisherType.MonitorPolski;
        }

        private static DateTime? TryAnnouncementDate(string segment)
        {
            var match = AnnouncementDateLine.Match(segment);
            if (!match.Success)
                return null;

            if (!PolishDates.TryMonth(match.Groups["month"].Value, out var month) ||
                !int.TryParse(match.Groups["day"].Value, out var day) ||
                !int.TryParse(match.Groups["year"].Value, out var year))
                return null;

            if (year > DateTime.Now.Year + 1)
                return null;

            return PolishDates.TryBuild(year, month, day, out var date) ? date : null;
        }

        private static int? TryPosition(string segment)
        {
            var match = PositionLine.Match(segment);
            if (!match.Success)
                return null;

            return int.TryParse(match.Groups["pos"].Value, out var position) && position > 0
                ? position
                : null;
        }
    }
}
