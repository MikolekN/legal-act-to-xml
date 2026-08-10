using System.Text.RegularExpressions;

namespace Saga.Core.Helpers
{
	/// <summary>
	/// Przetwarza polskie liczebniki: słowniki, składanie wartości 1–99 z dwóch wyrazów i
	/// generowanie ze słowników fragmentów regex do wyszukiwania liczebników w tekście.
	/// </summary>
	internal static class PolishNumerals
	{
		// ============================================================
		// Liczebniki porządkowe
		// ============================================================

		private static readonly Dictionary<string, int> OrdinalUnitStems = new(StringComparer.OrdinalIgnoreCase)
		{
			["pierwsz"] = 1, ["drug"] = 2, ["trzec"] = 3, ["czwart"] = 4, ["piąt"] = 5,
			["szóst"] = 6, ["siódm"] = 7, ["ósm"] = 8, ["dziewiąt"] = 9, ["dziesiąt"] = 10,
			["jedenast"] = 11, ["dwunast"] = 12, ["trzynast"] = 13, ["czternast"] = 14,
			["piętnast"] = 15, ["szesnast"] = 16, ["siedemnast"] = 17, ["osiemnast"] = 18,
			["dziewiętnast"] = 19,
		};

		private static readonly Dictionary<string, int> OrdinalTensStems = new(StringComparer.OrdinalIgnoreCase)
		{
			["dwudziest"] = 20, ["trzydziest"] = 30, ["czterdziest"] = 40, ["pięćdziesiąt"] = 50,
			["sześćdziesiąt"] = 60, ["siedemdziesiąt"] = 70, ["osiemdziesiąt"] = 80,
			["dziewięćdziesiąt"] = 90,
		};

		// ============================================================
		// Liczebniki główne — FORMY DOKŁADNE w dopełniaczu („po upływie czternastu dni")
		// ============================================================

		private static readonly Dictionary<string, int> CardinalUnits = new(StringComparer.OrdinalIgnoreCase)
		{
			// „jeden" bo: „po upływie dwudziestu jeden dni" a „dwudziestu jednego dni" jest niepoprawne.
			["jednego"] = 1, ["jeden"] = 1,
			["dwóch"] = 2, ["trzech"] = 3, ["czterech"] = 4,
			["pięciu"] = 5, ["sześciu"] = 6, ["siedmiu"] = 7,
			["ośmiu"] = 8, ["dziewięciu"] = 9, ["dziesięciu"] = 10,
			["jedenastu"] = 11, ["dwunastu"] = 12, ["trzynastu"] = 13,
			["czternastu"] = 14, ["piętnastu"] = 15, ["szesnastu"] = 16,
			["siedemnastu"] = 17, ["osiemnastu"] = 18, ["dziewiętnastu"] = 19,
		};

		private static readonly Dictionary<string, int> CardinalTens = new(StringComparer.OrdinalIgnoreCase)
		{
			["dwudziestu"] = 20, ["trzydziestu"] = 30, ["czterdziestu"] = 40,
			["pięćdziesięciu"] = 50, ["sześćdziesięciu"] = 60, ["siedemdziesięciu"] = 70,
			["osiemdziesięciu"] = 80, ["dziewięćdziesięciu"] = 90,
		};

		// ============================================================
		// Liczebniki mnożne
		// ============================================================

		private static readonly Dictionary<string, int> MultiplicityStems = new(StringComparer.OrdinalIgnoreCase)
		{
			["podwójn"] = 2, ["potrójn"] = 3, ["poczwórn"] = 4, ["pięciokrotn"] = 5,
		};

		// ============================================================
		// Końcówki przymiotnikowe
		// ============================================================

		/// <summary>
		/// Zbiór końcówek: mianownik trzech rodzajów („pierwszy/pierwsza/pierwsze"),
		/// dopełniacz („pierwszego/pierwszej"), miejscownik i narzędnik („pierwszym").
		/// </summary>
		internal static readonly string[] AdjectiveEndings =
		{
			"iego", "iemu", "ego", "emu", "iej", "ej", "ia", "ie", "im", "ym",
			"a", "ą", "e", "i", "y",
		};

		private static readonly string EndingGroup =
			"(?:" + string.Join("|", AdjectiveEndings.Select(Regex.Escape)) + ")";

		// ============================================================
		// Regex
		// ============================================================

		/// <summary>
		/// Alternatywa dopasowująca liczebnik porządkowy 1–99, jeden lub dwa wyrazy, dowolny rodzaj i przypadek.
		/// </summary>
		internal static readonly string OrdinalAlternation =
			BuildAlternation(OrdinalUnitStems, OrdinalTensStems, EndingGroup);

		/// <summary>Alternatywa dopasowująca liczebnik główny 1–99 w dopełniaczu, jeden lub dwa wyrazy.</summary>
		internal static readonly string CardinalAlternation =
			BuildAlternation(CardinalUnits, CardinalTens, suffix: string.Empty);

		/// <summary>Alternatywa dopasowująca liczebnik mnożny.</summary>
		internal static readonly string MultiplicityAlternation =
			@"\b(?:" + Alternatives(MultiplicityStems) + ")" + EndingGroup + @"(?!\p{L})";

		// ============================================================
		// Parsowanie
		// ============================================================

		/// <summary>
		/// Liczebnik porządkowy → wartość 1–99. Przyjmuje jeden wyraz („pierwsze", „czwartego")
		/// lub dwa („dwudzieste pierwsze").
		/// </summary>
		internal static bool TryParseOrdinal(string? text, out int value) =>
			TryCompose(text, OrdinalUnitStems, OrdinalTensStems, stemmed: true, out value);

		/// <summary>
		/// Liczebnik główny w dopełniaczu → wartość 1–99. Przyjmuje jeden wyraz („czternastu")
		/// lub dwa („czterdziestu dwóch").
		/// </summary>
		internal static bool TryParseCardinal(string? text, out int value) =>
			TryCompose(text, CardinalUnits, CardinalTens, stemmed: false, out value);

		/// <summary>Liczebnik mnożny. Nie składa się z dwóch wyrazów.</summary>
		internal static bool TryParseMultiplicity(string? text, out int value)
		{
			value = 0;
			if (string.IsNullOrWhiteSpace(text))
				return false;

			return TryWord(Normalize(text).Split(' ')[0], MultiplicityStems, stemmed: true, out value);
		}

		/// <summary>Składa wartość z jednego albo dwóch wyrazów.</summary>
		private static bool TryCompose(
			string? text,
			Dictionary<string, int> units,
			Dictionary<string, int> tens,
			bool stemmed,
			out int value)
		{
			value = 0;
			if (string.IsNullOrWhiteSpace(text))
				return false;

			var words = Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);

			if (words.Length == 1)
				return TryWord(words[0], units, stemmed, out value)
				    || TryWord(words[0], tens, stemmed, out value);

			if (words.Length != 2)
				return false;

			if (!TryWord(words[0], tens, stemmed, out var tensValue))
				return false;
			if (!TryWord(words[1], units, stemmed, out var unitsValue))
				return false;

			if (unitsValue is < 1 or > 9)
				return false;

			value = tensValue + unitsValue;
			return true;
		}

		/// <summary>Odczytuje jeden wyraz z tabeli. Próbuje kolejno sprawdzić każdą znaną końcówkę.</summary>
		private static bool TryWord(string word, Dictionary<string, int> table, bool stemmed, out int value)
		{
			if (!stemmed)
				return table.TryGetValue(word, out value);

			value = 0;
			foreach (var ending in AdjectiveEndings)
			{
				if (word.Length <= ending.Length || !word.EndsWith(ending, StringComparison.Ordinal))
					continue;
				if (table.TryGetValue(word[..^ending.Length], out value))
					return true;
			}

			return false;
		}

		private static string Normalize(string text) =>
			WhitespaceRun.Replace(text.Trim().ToLowerInvariant(), " ");

		private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);

		// ============================================================
		// Generator alternatyw
		// ============================================================

		/// <summary>Buduje alternatywę pokrywającą jeden LUB dwa wyrazy.</summary>
		private static string BuildAlternation(
			Dictionary<string, int> units, Dictionary<string, int> tens, string suffix)
		{
			var allUnits = Alternatives(units);
			var compoundUnits = Alternatives(units.Where(p => p.Value is >= 1 and <= 9));
			var allTens = Alternatives(tens);

			if (allUnits.Length == 0 || allTens.Length == 0 || compoundUnits.Length == 0)
				throw new InvalidOperationException(
					"Puste tabele liczebników — alternatywa dopasowałaby pusty ciąg. " +
					"Prawdopodobna przyczyna: cykl inicjalizacji statycznej.");

			return @"\b(?:"
			       + $"(?:{allTens}){suffix}" + @"\s+" + $"(?:{compoundUnits}){suffix}" + "|"
			       + $"(?:{allUnits}){suffix}" + "|"
			       + $"(?:{allTens}){suffix}"
			       + @")(?!\p{L})";
		}

		/// <summary>Klucze tabeli jako alternatywa regex.</summary>
		private static string Alternatives(IEnumerable<KeyValuePair<string, int>> table) =>
			string.Join("|", table
				.Select(p => p.Key)
				.OrderByDescending(k => k.Length)
				.ThenBy(k => k, StringComparer.Ordinal)
				.Select(Regex.Escape));
	}
}
