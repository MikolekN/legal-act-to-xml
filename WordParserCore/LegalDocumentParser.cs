using DocumentFormat.OpenXml.Packaging;
using Word = DocumentFormat.OpenXml.Wordprocessing;
using ModelDto;
using ModelDto.SystematizingUnits;
using WordParserCore.Exceptions;
using WordParserCore.Services.Parsing;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text.RegularExpressions;
using System.Globalization;
using Serilog;

namespace WordParserCore
{
	public static class LegalDocumentParser
	{
		public static LegalDocument Parse(string filePath)
		{
			using var wordDoc = WordprocessingDocument.Open(filePath, false);
			return Parse(wordDoc);
		}

        private static readonly Dictionary<string, int> _polishMonths = new(StringComparer.OrdinalIgnoreCase)
        {
            ["stycznia"] = 1,  ["lutego"]      = 2,  ["marca"]    = 3,
            ["kwietnia"] = 4,  ["maja"]        = 5,  ["czerwca"]  = 6,
            ["lipca"]    = 7,  ["sierpnia"]    = 8,  ["września"] = 9,
            ["października"] = 10, ["listopada"] = 11, ["grudnia"] = 12,
        };

        private static readonly Regex _dateRegex = new(
            @"z\s+dnia\s+(\d{1,2})\s+(\p{L}+)\s+(\d{4})\s+r\.",
            RegexOptions.CultureInvariant | RegexOptions.Compiled
        );

        private static readonly Regex _fallbackDateRegex = new(
            @"(\d{1,2})\s+(\p{L}+)\s+(\d{4})",
            RegexOptions.CultureInvariant | RegexOptions.Compiled
        );

        private static void ProcessThreeLines(WordprocessingDocument wordDocument, LegalDocument document)
        {
            var lines = wordDocument.MainDocumentPart.Document.Body
                .Elements<Paragraph>()
                .Where(p => !string.IsNullOrWhiteSpace(p.InnerText))
                .Take(3)
                .Select(p => p.InnerText);

            document.Title = string.Join(" ", lines).Trim().TrimEnd(')', ',', ' ');
            document.TypeElement = lines.ElementAt(0).Trim().TrimEnd(')', ',', ' ');
            document.DateElement = lines.ElementAt(1).Trim().TrimEnd(')', ',', ' ');
            document.TitleElement = lines.ElementAt(2).Trim().TrimEnd(')', ',', ' ');

            string date_string = lines.ElementAt(1);
            string normalized = Regex.Replace(date_string, @"\s+", " ").Trim();

            var match = _dateRegex.Match(normalized);
            if (!match.Success)
                match = _fallbackDateRegex.Match(normalized);
            if (!match.Success)
                throw new FormatException($"No date pattern found in: \"{date_string}\"");

            int    day      = int.Parse(match.Groups[1].Value);
            string monthKey = match.Groups[2].Value;
            int    year     = int.Parse(match.Groups[3].Value);

            if (!_polishMonths.TryGetValue(monthKey, out int month))
                throw new FormatException($"Unrecognised Polish month name: \"{monthKey}\"");

            document.PublicationDate = new DateTime(year, month, day);
        }

		public static LegalDocument Parse(WordprocessingDocument wordDocument)
		{
			var mainPart = wordDocument.MainDocumentPart ??
				throw new ParsingException("MainDocumentPart dokumentu jest null - plik moze byc uszkodzony lub pusty.");

			var document = new LegalDocument();

            ParseHeader(mainPart, document);

            ProcessThreeLines(wordDocument, document);

			var subchapter = GetDefaultSubchapter(document);

			var context = new ParsingContext(document, subchapter);
			var orchestrator = new ParserOrchestrator();

			foreach (var paragraph in mainPart.Document.Descendants<Word.Paragraph>())
			{
				orchestrator.ProcessParagraph(paragraph, context);
			}

			// Finalizacja — wypróżnienie bufora nowelizacji jeśli dokument
			// kończy się wewnątrz treści nowelizacji
			orchestrator.Finalize(context);

			return document;
		}

        private static string? FindHeader(MainDocumentPart mainPart)
        {
            var body = mainPart.Document.Body;
            if (body == null)
                return null;

            foreach (var section in body.Descendants<SectionProperties>())
            {
                var firstHeaderRef = section.Elements<HeaderReference>()
                    .FirstOrDefault(h => h.Type != null && h.Type == HeaderFooterValues.First);

                if (firstHeaderRef != null)
                {
                    var id = firstHeaderRef.Id?.Value;
                    if (string.IsNullOrEmpty(id))
                        continue;

                    if (!mainPart.TryGetPartById(id, out var part) || part is not HeaderPart headerPart)
                        continue;

                    return headerPart.Header?.InnerText;
                }
            }
            return null;
        }

        private static readonly Regex HeaderRegex = new Regex(
            @"(?<publisher>DZIENNIK\s*USTAW|MONITOR\s*POLSKI|DZ\.?\s*U\.?|M\.?\s*P\.?|DU|MP).*?" +
            @"dnia\s*(?<date>\d{1,2}\s*\w+\s*\d{4}).*?" +
            @"Poz\.?\s*(?<pos>\d+)",
            RegexOptions.IgnoreCase
        );

        private static void ParseHeader(MainDocumentPart mainPart, LegalDocument document)
        {
            string? headerText = FindHeader(mainPart);

            if (headerText == null)
                return;

            if (string.IsNullOrWhiteSpace(headerText))
                return;

            headerText = headerText.Trim();

            var match = HeaderRegex.Match(headerText);

            if (!match.Success)
            {
                Log.Warning("Header text did not match expected pattern. Header: {Header}", headerText);
                return;
            }

            var publisherText = NormalizePublisher(match.Groups["publisher"].Value);
            var publisher = ParsePublisher(publisherText);
            if (publisher != null)
                document.SourceJournal.Publisher = publisher.Value;

            var dateText = NormalizeDate(match.Groups["date"].Value);
            var date = ParseDate(dateText);
            if (date != null)
            {
                document.SourceJournal.Year = date.Value.Year;
                document.AnnouncementDate = date.Value;
            }

            var positionText = match.Groups["pos"].Value;
            if (int.TryParse(positionText, out var position))
                document.SourceJournal.Positions.Add(position);
            else
                Log.Warning("Failed to parse position number from header: {Text}", positionText);
        }

        private static string NormalizeDate(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            input = Regex.Replace(input, @"(\d)([A-Za-ząćęłńóśźżĄĆĘŁŃÓŚŹŻ])", "$1 $2");
            input = Regex.Replace(input, @"([A-Za-ząćęłńóśźżĄĆĘŁŃÓŚŹŻ])(\d)", "$1 $2");
            input = Regex.Replace(input, @"\s+", " ");

            return input.Trim();
        }

        private static DateTime? ParseDate(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            var culture = new CultureInfo("pl-PL");

            if (DateTime.TryParse(input, culture, DateTimeStyles.None, out var date))
                return date;

            return null;
        }

        private static string NormalizePublisher(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            input = input.ToUpperInvariant();

            input = input.Replace(".", "");
            input = Regex.Replace(input, @"\s+", "");

            if (input.Contains("DZIENNIKUSTAW") || input == "DZU" || input == "DU")
                return "DZIENNIK USTAW";

            if (input.Contains("MONITORPOLSKI") || input == "MP")
                return "MONITOR POLSKI";

            return "UNKNOWN";
        }

        private static PublisherType? ParsePublisher(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Publisher cannot be empty");

            return input switch {
                "DZIENNIK USTAW" => PublisherType.DziennikUstaw,
                "MONITOR POLSKI" => PublisherType.MonitorPolski,
                _ => null
            };
        }


		private static Subchapter GetDefaultSubchapter(LegalDocument document)
		{
			var part = document.RootPart;
			var book = part.Books.First();
			var title = book.Titles.First();
			var division = title.Divisions.First();
			var chapter = division.Chapters.First();
			return chapter.Subchapters.First();
		}

	}
}
