using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Saga.Core.Helpers;
using Xunit;

namespace Saga.Core.Tests
{
	/// <summary>
	/// Wspólne źródło prawdy o polskich liczebnikach: słowniki, składanie 1–99 i generowane
	/// alternatywy regex. Najważniejszy test w tym pliku to <c>FindAndParse_AgreeOnEveryValue</c> —
	/// dowodzi, że „znajdź w tekście" i „przeczytaj na liczbę" widzą ten sam zbiór, czyli że
	/// rozjazd między wyszukiwaniem a parsowaniem nie może wrócić.
	/// </summary>
	public class PolishNumeralsTests
	{
		// ============================================================
		// Poprawność pisowni — próbki wpisane ręcznie, nie generowane
		// ============================================================

		[Theory]
		[InlineData("pierwsze", 1)]
		[InlineData("drugie", 2)]
		[InlineData("trzecie", 3)]
		[InlineData("dwunaste", 12)]
		[InlineData("dwudzieste", 20)]
		[InlineData("dziewięćdziesiąte", 90)]
		// złożenia dwuwyrazowe
		[InlineData("dwudzieste pierwsze", 21)]
		[InlineData("trzydzieste drugie", 32)]
		[InlineData("czterdzieste drugie", 42)]
		[InlineData("dziewięćdziesiąte dziewiąte", 99)]
		// rodzaj męski i żeński — ta sama wartość
		[InlineData("pierwszy", 1)]
		[InlineData("pierwsza", 1)]
		[InlineData("trzeci", 3)]
		[InlineData("trzecia", 3)]
		[InlineData("ósmy", 8)]
		[InlineData("ósma", 8)]
		// przypadki inne niż mianownik
		[InlineData("czwartego", 4)]
		[InlineData("trzeciego", 3)]
		[InlineData("pierwszej", 1)]
		[InlineData("pierwszym", 1)]
		[InlineData("dwudziestego pierwszego", 21)]
		// wielkość liter bez znaczenia
		[InlineData("PIERWSZA", 1)]
		[InlineData("Dwudzieste Pierwsze", 21)]
		public void TryParseOrdinal_ReadsAllGendersAndCases(string text, int expected)
		{
			Assert.True(PolishNumerals.TryParseOrdinal(text, out var value), text);
			Assert.Equal(expected, value);
		}

		[Theory]
		[InlineData("jednego", 1)]
		[InlineData("czternastu", 14)]
		[InlineData("dziewiętnastu", 19)]
		[InlineData("trzydziestu", 30)]
		[InlineData("dziewięćdziesięciu", 90)]
		// złożenia: drugi człon w dopełniaczu…
		[InlineData("czterdziestu dwóch", 42)]
		[InlineData("dwudziestu trzech", 23)]
		[InlineData("dziewięćdziesięciu dziewięciu", 99)]
		// …albo NIEODMIENNA jedynka, i to jest forma poprawna, nie wyjątek
		[InlineData("dwudziestu jeden", 21)]
		[InlineData("sześćdziesięciu jeden", 61)]
		public void TryParseCardinal_ReadsGenitiveAndCompounds(string text, int expected)
		{
			Assert.True(PolishNumerals.TryParseCardinal(text, out var value), text);
			Assert.Equal(expected, value);
		}

		[Theory]
		[InlineData("podwójne", 2)]
		[InlineData("potrójne", 3)]
		[InlineData("poczwórne", 4)]
		[InlineData("pięciokrotne", 5)]
		[InlineData("podwójnego", 2)]
		public void TryParseMultiplicity_ReadsTiretDepth(string text, int expected)
		{
			Assert.True(PolishNumerals.TryParseMultiplicity(text, out var value), text);
			Assert.Equal(expected, value);
		}

		// ============================================================
		// Odrzucenia
		// ============================================================

		[Theory]
		// powyżej sufitu 99 — świadomie nieobsługiwane
		[InlineData("sto")]
		[InlineData("setne")]
		[InlineData("tysiąc")]
		// wyrazy zaczynające się jak liczebnik, ale nim nie będące
		[InlineData("pierwszeństwo")]
		[InlineData("pierwszorzędny")]
		[InlineData("czwartek")]
		[InlineData("piątek")]
		[InlineData("dziesiątka")]
		[InlineData("dwudziestolecie")]
		// złożenia niepoprawne strukturalnie
		[InlineData("dziesiąte drugie")]
		[InlineData("dwudzieste dwudzieste")]
		[InlineData("dwudzieste dziesiąte")]
		[InlineData("pierwsze drugie")]
		// więcej niż dwa wyrazy
		[InlineData("dwudzieste pierwsze drugie")]
		[InlineData("")]
		[InlineData(null)]
		public void TryParseOrdinal_RejectsNonNumerals(string? text)
		{
			Assert.False(PolishNumerals.TryParseOrdinal(text, out var value), text ?? "<null>");
			Assert.Equal(0, value);
		}

		[Theory]
		[InlineData("stu")]                      // powyżej sufitu 99
		[InlineData("dziesięciu dwóch")]         // pierwszy człon nie jest dziesiątką
		[InlineData("dwudziestu dwudziestu")]    // dwie dziesiątki
		[InlineData("dwudziestu dziesięciu")]    // drugi człon spoza 1–9
		public void TryParseCardinal_RejectsMalformedCompounds(string text)
		{
			Assert.False(PolishNumerals.TryParseCardinal(text, out var value), text);
			Assert.Equal(0, value);
		}

		[Fact]
		public void TryParseCardinal_AcceptsBareTens()
		{
			// Granica dla testu wyżej: sama dziesiątka jest poprawnym liczebnikiem.
			Assert.True(PolishNumerals.TryParseCardinal("dwudziestu", out var value));
			Assert.Equal(20, value);
		}

		// ============================================================
		// Niezmienniki tabel
		// ============================================================

		[Fact]
		public void ReferenceSpellings_ParseIntoDisjointUnitsAndTens()
		{
			// UWAGA: to nie jest niezmiennik produkcyjnych słowników — te są prywatne. Test waliduje
			// PISOWNIE REFERENCYJNE z tego pliku, na których opiera się FindAndParse_AgreeOnEveryValue:
			// gdyby któraś przestała się parsować albo wpadła w zły przedział, tamten test zgłaszałby
			// mylący błąd.
			foreach (var (family, units, tens) in Families())
			{
				Assert.All(units, u => Assert.InRange(u.Value, 1, 19));
				Assert.All(tens, t =>
				{
					Assert.InRange(t.Value, 20, 90);
					Assert.Equal(0, t.Value % 10);
				});

				var overlap = units.Keys.Intersect(tens.Keys).ToArray();
				Assert.Empty(overlap);

				// Rozłączność WARTOŚCI jedności i dziesiątek zapewnia, że fold nie ma dwuznaczności.
				Assert.Empty(units.Values.Intersect(tens.Values));
				Assert.NotEmpty(units);
				Assert.NotEmpty(tens);
			}
		}

		[Fact]
		public void AdjectiveEndings_AreOrderedLongestFirst()
		{
			// „trzeciego" wymaga próby „iego" przed „ego" — kolejność jest nośna, nie kosmetyczna.
			var lengths = PolishNumerals.AdjectiveEndings.Select(e => e.Length).ToArray();
			Assert.Equal(lengths.OrderByDescending(l => l), lengths);
		}

		// ============================================================
		// Round-trip: alternatywa regex ≡ słownik
		// ============================================================

		[Fact]
		public void FindAndParse_AgreeOnEveryValue()
		{
			// Dla każdej wartości 1–99 i każdej kanonicznej pisowni: wzorzec wyszukujący MUSI ją
			// dopasować, a parser MUSI odczytać tę samą liczbę. To jest strażnik niezmiennika
			// find ≡ parse — jedyna rzecz, która nie pozwala tym dwóm stronom znów się rozjechać.
			var ordinal = Anchored(PolishNumerals.OrdinalAlternation);
			var cardinal = Anchored(PolishNumerals.CardinalAlternation);

			for (var n = 1; n <= 99; n++)
			{
				foreach (var spelling in OrdinalSpellings(n))
				{
					Assert.True(ordinal.IsMatch(spelling), $"alternatywa porządkowa nie widzi „{spelling}\" ({n})");
					Assert.True(PolishNumerals.TryParseOrdinal(spelling, out var parsed),
						$"parser porządkowy nie czyta „{spelling}\" ({n})");
					Assert.Equal(n, parsed);
				}

				foreach (var spelling in CardinalSpellings(n))
				{
					Assert.True(cardinal.IsMatch(spelling), $"alternatywa główna nie widzi „{spelling}\" ({n})");
					Assert.True(PolishNumerals.TryParseCardinal(spelling, out var parsed),
						$"parser główny nie czyta „{spelling}\" ({n})");
					Assert.Equal(n, parsed);
				}
			}
		}

		[Theory]
		[InlineData("pierwszeństwo")]
		[InlineData("czwartek")]
		[InlineData("dziesiątka")]
		[InlineData("dwudziestolecie")]
		public void OrdinalAlternation_DoesNotMatchLookalikeWords(string word)
		{
			// Guard (?!\p{L}) w generowanym fragmencie jest jedyną rzeczą, która to zapewnia.
			Assert.False(Anchored(PolishNumerals.OrdinalAlternation).IsMatch(word), word);
		}

		[Theory]
		[InlineData("potrójnienie")]
		[InlineData("podwójniactwo")]
		[InlineData("pięciokrotnościami")]
		public void MultiplicityAlternation_DoesNotMatchLookalikeWords(string word)
		{
			// Odpowiednik strażnika dla liczebników porządkowych: rdzeń mnożny plus DALSZE litery
			// to inny wyraz, nie forma liczebnika. Odrzuca je wyłącznie guard (?!\p{L}).
			//
			// Uwaga: „podwójnie" (przysłówek) alternatywa dopasowuje, bo „ie" jest legalną końcówką
			// przymiotnikową i sama pisownia ich nie różnicuje. Jest to nieszkodliwe, bo fragment
			// używany jest wyłącznie w kontekście „<mnożny> tiret".
			Assert.False(Anchored(PolishNumerals.MultiplicityAlternation).IsMatch(word), word);
		}

		[Theory]
		[InlineData("podwójne")]
		[InlineData("poczwórnym")]
		[InlineData("potrójnego")]
		public void MultiplicityAlternation_MatchesInflectedForms(string word)
		{
			Assert.True(Anchored(PolishNumerals.MultiplicityAlternation).IsMatch(word), word);
		}

		// ============================================================
		// Pomocnicze
		// ============================================================

		private static Regex Anchored(string alternation) =>
			new("^" + alternation + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

		private static IEnumerable<(string Family, IReadOnlyDictionary<string, int> Units, IReadOnlyDictionary<string, int> Tens)> Families()
		{
			// Odbicie tabel przez publiczne zachowanie: budujemy je z pisowni, które MUSZĄ się parsować.
			yield return ("porządkowe", ToMap(OrdinalUnitWords, PolishNumerals.TryParseOrdinal),
				ToMap(OrdinalTensWords, PolishNumerals.TryParseOrdinal));
			yield return ("główne", ToMap(CardinalUnitWords, PolishNumerals.TryParseCardinal),
				ToMap(CardinalTensWords, PolishNumerals.TryParseCardinal));
		}

		private delegate bool TryParse(string? text, out int value);

		private static IReadOnlyDictionary<string, int> ToMap(IEnumerable<string> words, TryParse parse)
		{
			var map = new Dictionary<string, int>();
			foreach (var word in words)
			{
				Assert.True(parse(word, out var value), word);
				map[word] = value;
			}
			return map;
		}

		private static readonly string[] OrdinalUnitWords =
		{
			"pierwsze", "drugie", "trzecie", "czwarte", "piąte", "szóste", "siódme", "ósme",
			"dziewiąte", "dziesiąte", "jedenaste", "dwunaste", "trzynaste", "czternaste",
			"piętnaste", "szesnaste", "siedemnaste", "osiemnaste", "dziewiętnaste",
		};

		private static readonly string[] OrdinalTensWords =
		{
			"dwudzieste", "trzydzieste", "czterdzieste", "pięćdziesiąte",
			"sześćdziesiąte", "siedemdziesiąte", "osiemdziesiąte", "dziewięćdziesiąte",
		};

		private static readonly string[] CardinalUnitWords =
		{
			"jednego", "dwóch", "trzech", "czterech", "pięciu", "sześciu", "siedmiu", "ośmiu",
			"dziewięciu", "dziesięciu", "jedenastu", "dwunastu", "trzynastu", "czternastu",
			"piętnastu", "szesnastu", "siedemnastu", "osiemnastu", "dziewiętnastu",
		};

		private static readonly string[] CardinalTensWords =
		{
			"dwudziestu", "trzydziestu", "czterdziestu", "pięćdziesięciu",
			"sześćdziesięciu", "siedemdziesięciu", "osiemdziesięciu", "dziewięćdziesięciu",
		};

		/// <summary>Kanoniczne pisownie liczebnika porządkowego: rodzaj nijaki oraz męski.</summary>
		private static IEnumerable<string> OrdinalSpellings(int n)
		{
			if (n <= 19)
			{
				yield return OrdinalUnitWords[n - 1];
				yield return Masculine(OrdinalUnitWords[n - 1]);
				yield break;
			}

			var tens = OrdinalTensWords[n / 10 - 2];
			if (n % 10 == 0)
			{
				yield return tens;
				yield return Masculine(tens);
				yield break;
			}

			var units = OrdinalUnitWords[n % 10 - 1];
			yield return $"{tens} {units}";
			yield return $"{Masculine(tens)} {Masculine(units)}";
		}

		private static IEnumerable<string> CardinalSpellings(int n)
		{
			if (n <= 19)
			{
				yield return CardinalUnitWords[n - 1];
				yield break;
			}

			var tens = CardinalTensWords[n / 10 - 2];
			if (n % 10 == 0)
			{
				yield return tens;
				yield break;
			}

			yield return $"{tens} {CardinalUnitWords[n % 10 - 1]}";

			// Jedynka w złożeniu jest nieodmienna — druga poprawna pisownia dla 21, 31, …
			if (n % 10 == 1)
				yield return $"{tens} jeden";
		}

		/// <summary>Forma męska z nijakiej: „pierwsze"→„pierwszy", „trzecie"→„trzeci", „piąte"→„piąty".</summary>
		private static string Masculine(string neuter) =>
			neuter.EndsWith("ie") ? neuter[..^2] + "i" : neuter[..^1] + "y";
	}
}
