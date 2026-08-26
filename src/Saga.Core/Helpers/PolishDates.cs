using System.Text.RegularExpressions;

namespace Saga.Core.Helpers
{
	/// <summary>Parsowanie polskich dat w formie długiej („1 stycznia 2025 r.").</summary>
	internal static class PolishDates
	{
		private static readonly Dictionary<string, int> MonthMap = new(StringComparer.OrdinalIgnoreCase)
		{
			["stycznia"] = 1, ["lutego"] = 2, ["marca"] = 3, ["kwietnia"] = 4, ["maja"] = 5, ["czerwca"] = 6,
			["lipca"] = 7, ["sierpnia"] = 8, ["września"] = 9, ["października"] = 10, ["listopada"] = 11, ["grudnia"] = 12,
		};

		/// <summary>Nazwy miesięcy wykorzystywane do składania regex.</summary>
		public const string MonthAlternation =
			"stycznia|lutego|marca|kwietnia|maja|czerwca|lipca|sierpnia|września|października|listopada|grudnia";

		/// <summary>Data w formie długiej wewnątrz zdania: „(dnia) <dzień> <miesiąc> <rok> r.".</summary>
		private static readonly Regex LongDatePattern = new(
			@"(?:dnia\s+)?(?<day>\d{1,2})\s+(?<month>" + MonthAlternation + @")\s+(?<year>\d{4})",
			RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

		/// <summary>Nazwa miesiąca (dopełniacz) → numer.</summary>
		public static bool TryMonth(string? name, out int month)
		{
			month = 0;
            if (string.IsNullOrWhiteSpace(name))
                return false;
			return MonthMap.TryGetValue(name.Trim(), out month);
		}

		/// <summary>Wydobywa datę w formie długiej z podanego tekstu.</summary>
		public static bool TryParseLongDate(string? text, out DateTime date)
		{
			date = default;
			if (string.IsNullOrWhiteSpace(text))
				return false;

			var match = LongDatePattern.Match(text);
			if (!match.Success)
				return false;

			if (!TryMonth(match.Groups["month"].Value, out var month) ||
			    !int.TryParse(match.Groups["day"].Value, out var day) ||
			    !int.TryParse(match.Groups["year"].Value, out var year))
				return false;

			return TryBuild(year, month, day, out date);
		}

		/// <summary>Buduje datę.</summary>
		public static bool TryBuild(int year, int month, int day, out DateTime date)
		{
			date = default;
			if (year < 1 || year > 9999 || month < 1 || month > 12 || day < 1)
				return false;
			if (day > DateTime.DaysInMonth(year, month))
				return false;

			date = new DateTime(year, month, day);
			return true;
		}
	}
}
