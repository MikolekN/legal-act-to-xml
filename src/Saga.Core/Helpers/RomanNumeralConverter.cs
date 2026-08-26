namespace Saga.Core.Helpers
{
	/// <summary>
	/// Konwersja cyfr rzymskich używanych w oznaczeniach jednostek
    /// systematyzacyjnych na wartość liczbową i z powrotem.
	/// </summary>
	internal static class RomanNumeralConverter
	{
		private static readonly Dictionary<char, int> RomanDigits = new()
		{
			['I'] = 1, ['V'] = 5, ['X'] = 10, ['L'] = 50, ['C'] = 100, ['D'] = 500, ['M'] = 1000,
		};

		/// <summary>
		/// Parsuje cyfrę rzymską (I, IV, XII…) na liczbę. Zwraca null dla pustego lub niepoprawnego wejścia.
		/// </summary>
		public static int? RomanToInt(string? roman)
		{
			if (string.IsNullOrWhiteSpace(roman))
				return null;

			var s = roman.Trim().ToUpperInvariant();
			int total = 0, prev = 0;
			foreach (var c in s)
			{
				if (!RomanDigits.TryGetValue(c, out var value))
					return null;
				total += value <= prev ? value : value - 2 * prev;
				prev = value;
			}

			return total > 0 && ToRoman(total) == s ? total : null;
		}

		/// <summary>Zamienia liczbę na kanoniczną cyfrę rzymską (do normalizacji eId jednostek wyższych).</summary>
		internal static string ToRoman(int number)
		{
			if (number <= 0 || number >= 4000)
				return string.Empty;

			(int Value, string Symbol)[] map =
			{
				(1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"),
				(50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I"),
			};

			var sb = new System.Text.StringBuilder();
			foreach (var (value, symbol) in map)
				while (number >= value)
				{
					sb.Append(symbol);
					number -= value;
				}

			return sb.ToString();
		}
	}
}
