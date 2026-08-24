using System.Globalization;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ModelDto;
using Serilog;
using Word = DocumentFormat.OpenXml.Wordprocessing;

namespace WordParserCore.Services.Parsing
{
	/// <summary>
	/// Parsuje metadane dokumentu spoza struktury jednostek redakcyjnych:
	/// - publikator z nagłówka strony (dziennik urzędowy, rok, pozycja, data ogłoszenia),
	/// - linie czołowe aktu (rodzaj, data, tytuł) wraz z datą wydania,
	/// - odnośniki (przypisy dolne) z części word/footnotes.xml.
	/// Wywoływany przez <see cref="LegalDocumentParser"/> przed głównym potokiem parsowania.
	/// </summary>
	public static class DocumentMetadataParser
	{
		public static void Parse(WordprocessingDocument wordDocument, LegalDocument document)
		{
			var mainPart = wordDocument.MainDocumentPart!;

			ParseJournalHeader(mainPart, document);
			ParseFrontMatter(mainPart, document);
			ParseFootnotes(mainPart, document);
		}

		private static readonly Dictionary<string, int> PolishMonths = new(StringComparer.OrdinalIgnoreCase)
		{
			["stycznia"] = 1,  ["lutego"]      = 2,  ["marca"]    = 3,
			["kwietnia"] = 4,  ["maja"]        = 5,  ["czerwca"]  = 6,
			["lipca"]    = 7,  ["sierpnia"]    = 8,  ["września"] = 9,
			["października"] = 10, ["listopada"] = 11, ["grudnia"] = 12,
		};

		private static readonly Regex DatePattern = new(
			@"z\s+dnia\s+(\d{1,2})\s+(\p{L}+)\s+(\d{4})\s+r\.",
			RegexOptions.CultureInvariant | RegexOptions.Compiled);

		private static readonly Regex FallbackDatePattern = new(
			@"(\d{1,2})\s+(\p{L}+)\s+(\d{4})",
			RegexOptions.CultureInvariant | RegexOptions.Compiled);

		/// <summary>
		/// Odczytuje trzy pierwsze niepuste akapity aktu (rodzaj aktu, "z dnia ...", przedmiot regulacji)
		/// i wypełnia tytuł, elementy czołowe oraz datę wydania dokumentu.
		/// </summary>
		private static void ParseFrontMatter(MainDocumentPart mainPart, LegalDocument document)
		{
			var paragraphs = mainPart.Document.Body!
				.Elements<Paragraph>()
				.Where(p => !string.IsNullOrWhiteSpace(p.InnerText))
				.Take(3)
				.ToList();

			if (paragraphs.Count < 3 || !TryParseActDate(paragraphs[1].InnerText, out var publicationDate))
			{
				Log.Information("Pominięto linie czołowe — dokument nie ma układu ogłoszonej ustawy.");
				return;
			}

			var lines = paragraphs.Select(p => p.InnerText).ToList();

			document.Title = string.Join(" ", lines).Trim().TrimEnd(')', ',', ' ');
			document.TypeElement = lines[0].Trim().TrimEnd(')', ',', ' ');
			document.DateElement = lines[1].Trim().TrimEnd(')', ',', ' ');
			// Tytuł przez GetFullText — zachowuje marker odnośnika "[N)]", dzięki czemu
			// konwerter może osadzić <odnosnik> w elemencie tytul.
			document.TitleElement = paragraphs[2].GetFullText().Trim().TrimEnd(',', ' ');

			document.PublicationDate = publicationDate;
		}

		/// <summary>
		/// Próbuje odczytać datę wydania aktu z linii "z dnia D miesiąca RRRR r.".
		/// Zwraca false, gdy linia nie zawiera rozpoznawalnej daty — jest to sygnał, że akapit
		/// nie jest linią daty ogłoszonej ustawy, a nie błąd.
		/// </summary>
		private static bool TryParseActDate(string dateLine, out DateTime date)
		{
			date = default;

			var normalized = Regex.Replace(dateLine, @"\s+", " ").Trim();

			var match = DatePattern.Match(normalized);
			if (!match.Success)
				match = FallbackDatePattern.Match(normalized);
			if (!match.Success)
				return false;

			int    day      = int.Parse(match.Groups[1].Value);
			string monthKey = match.Groups[2].Value;
			int    year     = int.Parse(match.Groups[3].Value);

			if (!PolishMonths.TryGetValue(monthKey, out int month))
				return false;

			try
			{
				date = new DateTime(year, month, day);
				return true;
			}
			catch (ArgumentOutOfRangeException)
			{
				return false;
			}
		}

		private static readonly Regex HeaderPattern = new(
			@"(?<publisher>DZIENNIK\s*USTAW|MONITOR\s*POLSKI|DZ\.?\s*U\.?|M\.?\s*P\.?|DU|MP).*?" +
			@"dnia\s*(?<date>\d{1,2}\s*\w+\s*\d{4}).*?" +
			@"Poz\.?\s*(?<pos>\d+)",
			RegexOptions.IgnoreCase);

		/// <summary>
		/// Odczytuje publikator (dziennik, rok, pozycję) oraz datę ogłoszenia
		/// z nagłówka pierwszej strony dokumentu.
		/// </summary>
		private static void ParseJournalHeader(MainDocumentPart mainPart, LegalDocument document)
		{
			string? headerText = FindFirstPageHeader(mainPart);
			if (string.IsNullOrWhiteSpace(headerText))
				return;

			var match = HeaderPattern.Match(headerText.Trim());
			if (!match.Success)
			{
				Log.Warning("Header text did not match expected pattern. Header: {Header}", headerText);
				return;
			}

			var publisher = ParsePublisher(NormalizePublisher(match.Groups["publisher"].Value));
			if (publisher != null)
				document.SourceJournal.Publisher = publisher.Value;

			var date = ParseHeaderDate(NormalizeDate(match.Groups["date"].Value));
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

		private static string? FindFirstPageHeader(MainDocumentPart mainPart)
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

		private static string NormalizeDate(string input)
		{
			if (string.IsNullOrWhiteSpace(input))
				return string.Empty;

			// Rozdziel sklejone cyfry i litery (np. "12stycznia2026" → "12 stycznia 2026")
			input = Regex.Replace(input, @"(\d)([A-Za-ząćęłńóśźżĄĆĘŁŃÓŚŹŻ])", "$1 $2");
			input = Regex.Replace(input, @"([A-Za-ząćęłńóśźżĄĆĘŁŃÓŚŹŻ])(\d)", "$1 $2");
			input = Regex.Replace(input, @"\s+", " ");

			return input.Trim();
		}

		private static DateTime? ParseHeaderDate(string input)
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

			return input switch
			{
				"DZIENNIK USTAW" => PublisherType.DziennikUstaw,
				"MONITOR POLSKI" => PublisherType.MonitorPolski,
				_ => null
			};
		}

		/// <summary>
		/// Wczytuje odnośniki (przypisy dolne) z części word/footnotes.xml.
		/// Pomija separatory; usuwa wiodące ")" (oznaczenie numeru jest wpisywane
		/// w treści przypisu po automatycznym numerze).
		/// </summary>
		private static void ParseFootnotes(MainDocumentPart mainPart, LegalDocument document)
		{
			var footnotes = mainPart.FootnotesPart?.Footnotes;
			if (footnotes == null)
				return;

			var number = 0;
			foreach (var footnote in footnotes.Elements<Word.Footnote>())
			{
				// Pomiń separatory i inne przypisy techniczne (mają ustawiony Type)
				if (footnote.Type != null && footnote.Type != FootnoteEndnoteValues.Normal)
					continue;

				if (footnote.Id?.Value is not { } id || id < 1)
					continue;

				var content = string.Concat(
					footnote.Descendants<Text>().Select(t => t.Text)).Trim();
				content = content.TrimStart(')', ' ').Trim();
				if (string.IsNullOrEmpty(content))
					continue;

				// Number = kolejny numer widoczny w akcie (Word numeruje przypisy sekwencyjnie)
				document.Footnotes.Add(new ModelDto.Footnote { Id = (int)id, Number = ++number, Content = content });
			}

			if (document.Footnotes.Count > 0)
				Log.Information("Wczytano {Count} odnośników (przypisów dolnych)", document.Footnotes.Count);
		}
	}
}
