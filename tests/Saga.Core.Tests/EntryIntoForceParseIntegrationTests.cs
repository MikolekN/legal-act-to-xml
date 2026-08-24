using System;
using System.IO;
using System.Linq;
using System.Text;
using Saga.Core;
using Saga.Model;
using Saga.Model.EntryIntoForce;
using Xunit;

namespace Saga.Core.Tests
{
	/// <summary>
	/// Pełny przebieg: strumień TXT → parser → klauzula wejścia w życie zapisana w modelu.
	/// Kanał TXT omija fixture'y DOCX, a mimo to przechodzi przez cały potok łącznie z
	/// <c>ParserOrchestrator.Finalize</c>, gdzie uruchamiany jest przebieg klauzuli.
	/// </summary>
	public class EntryIntoForceParseIntegrationTests
	{
		private static LegalDocument Parse(string text, DateTime? announcementDate = null)
		{
			using var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(text));
			var result = LegalDocumentParser.Parse(stream, "ustawa.txt", new ParseOptions
			{
				Policy = ParsePolicy.AlwaysParse,
			});

			Assert.NotNull(result.Document);

			// TYMCZASOWE: parser nie odczytuje jeszcze daty ogłoszenia z dokumentu, więc formuły
			// względne nie mają punktu odniesienia. Szczegóły i warunek usunięcia:
			// AnnouncementDateScaffold.
			return result.Document!.AsAnnouncedOn(announcementDate);
		}

		private const string Header = """
			USTAWA
			z dnia 5 marca 2025 r.
			o zmianie ustawy o rzeczach różnych

			""";

		[Fact]
		public void RelativeFormula_ResolvesAgainstAnnouncementDate()
		{
			var document = Parse(
				Header +
				"Art. 1. Wprowadza się zmiany porządkowe.\n" +
				"Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.\n",
				new DateTime(2025, 3, 20));

			var info = Assert.IsType<EntryIntoForceInfo>(document.EntryIntoForce);
			Assert.Equal(EntryIntoForceFormulaKind.AfterDaysFromAnnouncement, info.Formula.Kind);
			Assert.Equal(14, info.Formula.Operand);

			// Dzień ogłoszenia nie wlicza się do terminu: 20 III + 14 dni → wejście 4 IV.
			Assert.Equal(new DateTime(2025, 4, 4), info.ResolvedDate);
			Assert.Equal(new DateTime(2025, 4, 4), document.EffectiveDate);
		}

		[Fact]
		public void ExplicitDate_ResolvesWithoutAnnouncementDate()
		{
			var document = Parse(
				Header +
				"Art. 1. Wprowadza się zmiany porządkowe.\n" +
				"Art. 2. Ustawa wchodzi w życie z dniem 1 stycznia 2026 r.\n");

			var info = Assert.IsType<EntryIntoForceInfo>(document.EntryIntoForce);
			Assert.Equal(EntryIntoForceFormulaKind.OnSpecificDate, info.Formula.Kind);
			Assert.Equal(new DateTime(2026, 1, 1), info.ResolvedDate);
			Assert.Equal(new DateTime(2026, 1, 1), document.EffectiveDate);
		}

		[Fact]
		public void MissingAnnouncementDate_LeavesRelativeFormulaUnresolved_AndEffectiveDateUnset()
		{
			var document = Parse(
				Header +
				"Art. 1. Wprowadza się zmiany porządkowe.\n" +
				"Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.\n");

			var info = Assert.IsType<EntryIntoForceInfo>(document.EntryIntoForce);

			// Formuła rozpoznana mimo braku daty ogłoszenia…
			Assert.Equal(EntryIntoForceFormulaKind.AfterDaysFromAnnouncement, info.Formula.Kind);
			Assert.Equal(14, info.Formula.Operand);

			// …ale data NIE jest zgadywana — w szczególności nie podstawiamy daty aktu.
			Assert.Null(info.ResolvedDate);
			Assert.Equal(default, document.EffectiveDate);
		}

		[Fact]
		public void Retroactivity_IsCapturedSeparately_AndDoesNotShiftEntryDate()
		{
			var document = Parse(
				Header +
				"Art. 1. Wprowadza się zmiany porządkowe.\n" +
				"Art. 2. Ustawa wchodzi w życie z dniem następującym po dniu ogłoszenia, " +
				"z mocą od dnia 1 lipca 2024 r.\n",
				new DateTime(2025, 3, 20));

			var info = Assert.IsType<EntryIntoForceInfo>(document.EntryIntoForce);
			Assert.Equal(EntryIntoForceFormulaKind.DayAfterAnnouncement, info.Formula.Kind);
			Assert.Equal(new DateTime(2025, 3, 21), info.ResolvedDate);
			Assert.Equal(new DateTime(2024, 7, 1), info.RetroactiveFrom);
		}

		[Fact]
		public void InlineException_IsAppliedToTheNamedArticle()
		{
			var document = Parse(
				Header +
				"Art. 1. Wprowadza się zmiany porządkowe.\n" +
				"Art. 2. Przepis przejściowy.\n" +
				"Art. 3. Ustawa wchodzi w życie z dniem 1 stycznia 2026 r., z wyjątkiem art. 2, " +
				"który wchodzi w życie z dniem 1 marca 2026 r.\n",
				new DateTime(2025, 12, 1));

			var info = Assert.IsType<EntryIntoForceInfo>(document.EntryIntoForce);
			Assert.Equal(new DateTime(2026, 1, 1), info.ResolvedDate);

			var partial = Assert.Single(info.Partials);
			Assert.True(partial.IsApplicable);
			Assert.Equal(new DateTime(2026, 3, 1), partial.ResolvedDate);

			// Data wyjątku trafia na wskazany artykuł…
			var article2 = document.Articles.Single(a => a.Number?.Value == "2");
			Assert.Equal(new DateTime(2026, 3, 1), article2.EffectiveDate);

			// …i dziedziczy się w dół, na jego jednostki podrzędne.
			Assert.All(article2.Paragraphs, p => Assert.Equal(new DateTime(2026, 3, 1), p.EffectiveDate));

			// Artykuł nieobjęty wyjątkiem dostaje datę REGUŁY OGÓLNEJ — wraz z ustępami i zdaniami.
			// Brak daty jest zarezerwowany dla wyjątków nierozstrzygalnych.
			var article1 = document.Articles.Single(a => a.Number?.Value == "1");
			Assert.Equal(new DateTime(2026, 1, 1), article1.EffectiveDate);
			Assert.All(article1.Paragraphs, p =>
			{
				Assert.Equal(new DateTime(2026, 1, 1), p.EffectiveDate);
				Assert.All(p.TextSegments, s => Assert.Equal(new DateTime(2026, 1, 1), s.EffectiveDate));
			});
		}

		[Fact]
		public void StructuralScope_DatesQuotedUnits_NotTheHostUnit()
		{
			// Kształt z Dz.U. 2026 poz. 26: „art. 1 pkt 9 w zakresie art. 85a ust. 1 i art. 85c–85j".
			// Wyjątek zawęża się do jednostek WEWNĄTRZ cytatu; punkt 9 zachowuje datę ogólną aktu.
			var document = Parse(
				Header +
				"Art. 1. W ustawie z dnia 13 października 1998 r. o systemie ubezpieczeń społecznych " +
				"wprowadza się następujące zmiany:\n" +
				"9) po rozdziale 9 dodaje się rozdział 9a w brzmieniu:\n" +
				"„Art. 85a. 1. Zadania są realizowane w jednostkach terenowych.\n" +
				"2. Fizjoterapeuta może wydawać orzeczenia.\n" +
				"Art. 85b. Przepis przejściowy.\n" +
				"Art. 85c. Kolejny przepis.”\n" +
				"Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia, z wyjątkiem " +
				"art. 1 pkt 9 w zakresie art. 85a ust. 1 i art. 85c, które wchodzą w życie " +
				"z dniem 1 stycznia 2027 r.\n",
				new DateTime(2026, 3, 29));

			var info = Assert.IsType<EntryIntoForceInfo>(document.EntryIntoForce);
			var partial = Assert.Single(info.Partials);
			Assert.Equal(new DateTime(2027, 1, 1), partial.ResolvedDate);

			var target = Assert.Single(partial.Targets);
			Assert.Equal(ScopeKind.Structural, target.ScopeKind);
			Assert.Equal(new[] { "art_85a__ust_1", "art_85c" },
				target.ScopeStructures.Select(s => s.ToString()));

			// Jednostka goszcząca NIE dostaje daty wyjątku, tylko ogólną — zakres jest zawężeniem:
			// odbiega wyłącznie część tego, co ta jednostka wprowadza. Kształt potwierdzony
			// w Dz.U. 2026 poz. 26, gdzie art_1__pkt_9 nosi właśnie datę ogólną 2026-04-13.
			var general = new DateTime(2026, 4, 13);
			var point = document.Articles.Single(a => a.Number?.Value == "1")
				.Paragraphs.SelectMany(p => p.Points).Single(p => p.Number?.Value == "9");
			Assert.Equal(general, point.EffectiveDate);

			// …a jednostki wskazane zakresem — tak, wewnątrz cytowanej treści nowelizacji.
			var quoted = point.Amendment!.Content!;
			var art85a = quoted.Articles.Single(a => a.Number?.Value == "85a");
			Assert.Equal(new DateTime(2027, 1, 1),
				art85a.Paragraphs.Single(p => p.Number?.Value == "1").EffectiveDate);

			// Sąsiedztwo nietknięte: ust. 2 artykułu 85a i cały art. 85b zachowują datę ogólną.
			Assert.Equal(general, art85a.Paragraphs.Single(p => p.Number?.Value == "2").EffectiveDate);
			Assert.Equal(general, quoted.Articles.Single(a => a.Number?.Value == "85b").EffectiveDate);
			Assert.Equal(new DateTime(2027, 1, 1),
				quoted.Articles.Single(a => a.Number?.Value == "85c").EffectiveDate);

			// Nowelizacja wchodzi w życie razem z jednostką, która ją zarządza.
			Assert.Equal(general, point.Amendment!.EffectiveDate);
		}

		// ============================================================
		// Rozsyłanie daty reguły ogólnej po drzewie
		// ============================================================

		[Fact]
		public void UnresolvedException_LeavesTheWholeSubtreeBlank()
		{
			// „Z dniem określonym w komunikacie" — cel wyjątku ZNAMY, daty nie. Brak daty jest
			// wtedy nośnikiem informacji: sygnalizuje jednostkę, której nie dało się rozstrzygnąć.
			// Zatrucie idzie w dół, bo ustęp nie może wejść w życie wcześniej niż jego artykuł.
			var document = Parse(
				Header +
				"Art. 1. Wprowadza się zmiany porządkowe.\n" +
				"Art. 2. 1. Pierwszy ustęp artykułu drugiego.\n" +
				"2. Drugi ustęp artykułu drugiego.\n" +
				"Art. 3. Ustawa wchodzi w życie z dniem 1 stycznia 2026 r., z wyjątkiem art. 2, " +
				"który wchodzi w życie z dniem określonym w komunikacie.\n",
				new DateTime(2025, 12, 1));

			var article2 = document.Articles.Single(a => a.Number?.Value == "2");
			Assert.Equal(default, article2.EffectiveDate);
			Assert.All(article2.Paragraphs, p =>
			{
				Assert.Equal(default, p.EffectiveDate);
				Assert.All(p.TextSegments, s => Assert.Equal(default, s.EffectiveDate));
			});

			// Reszta aktu zostaje przy regule ogólnej — jeden nierozstrzygalny wyjątek nie kasuje dat
			// jednostkom, których nie dotyczy.
			var article1 = document.Articles.Single(a => a.Number?.Value == "1");
			Assert.Equal(new DateTime(2026, 1, 1), article1.EffectiveDate);
		}

		[Fact]
		public void DeeperException_OverridesPoisonedParent()
		{
			// Wyjątek głębszy wygrywa z płytszym sam z siebie — nadpisanie dzieje się w trakcie
			// zejścia po drzewie, więc kolejność wyjątków w klauzuli nie ma znaczenia.
			var document = Parse(
				Header +
				"Art. 1. 1. Pierwszy ustęp.\n" +
				"2. Drugi ustęp.\n" +
				"Art. 2. Ustawa wchodzi w życie z dniem 1 stycznia 2026 r., z wyjątkiem art. 1, " +
				"który wchodzi w życie z dniem określonym w komunikacie, z wyjątkiem art. 1 ust. 2, " +
				"który wchodzi w życie z dniem 1 marca 2026 r.\n",
				new DateTime(2025, 12, 1));

			var article1 = document.Articles.Single(a => a.Number?.Value == "1");
			Assert.Equal(default, article1.EffectiveDate);

			var paragraph1 = article1.Paragraphs.Single(p => p.Number?.Value == "1");
			Assert.Equal(default, paragraph1.EffectiveDate);

			var paragraph2 = article1.Paragraphs.Single(p => p.Number?.Value == "2");
			Assert.Equal(new DateTime(2026, 3, 1), paragraph2.EffectiveDate);
		}

		[Fact]
		public void GeneralDate_ReachesCommonPartsAndImplicitUnits()
		{
			// Jednostki niejawne (ustęp domyślny, jednostki systematyzacyjne domyślne) i części
			// wspólne też niosą datę: konwerter schemaPL czyta datę zdania części wspólnej wprost
			// z CommonPart.EffectiveDate, więc pominięcie ich zabrałoby datę ich zdaniom.
			var document = Parse(
				Header +
				"Art. 1. Wprowadza się następujące zmiany:\n" +
				"1) pierwsza zmiana;\n" +
				"2) druga zmiana\n" +
				"– przy czym stosuje się je odpowiednio.\n" +
				"Art. 2. Ustawa wchodzi w życie z dniem 1 stycznia 2026 r.\n",
				new DateTime(2025, 12, 1));

			var expected = new DateTime(2026, 1, 1);
			Assert.Equal(expected, document.RootPart.EffectiveDate);

			var paragraph = document.Articles.Single(a => a.Number?.Value == "1").Paragraphs.Single();
			Assert.True(paragraph.IsImplicit);
			Assert.Equal(expected, paragraph.EffectiveDate);
			Assert.All(paragraph.CommonParts, cp => Assert.Equal(expected, cp.EffectiveDate));
			Assert.NotEmpty(paragraph.CommonParts);
		}

		[Fact]
		public void TransitionalPhrase_IsNotMistakenForAClause()
		{
			// „przed dniem wejścia w życie" to przepis przejściowy — wystąpienie rzeczownikowe,
			// nie klauzula. Jedyną klauzulą jest artykuł końcowy.
			var document = Parse(
				Header +
				"Art. 1. Do spraw wszczętych i niezakończonych przed dniem wejścia w życie niniejszej " +
				"ustawy stosuje się przepisy dotychczasowe.\n" +
				"Art. 2. Ustawa wchodzi w życie z dniem 1 lutego 2026 r.\n");

			var info = Assert.IsType<EntryIntoForceInfo>(document.EntryIntoForce);
			Assert.Equal(new DateTime(2026, 2, 1), info.ResolvedDate);
			Assert.Empty(info.UnresolvedFragments);
		}

		[Fact]
		public void CommunicationFormula_IsRecognizedButNeverResolved()
		{
			var document = Parse(
				Header +
				"Art. 1. Wprowadza się zmiany porządkowe.\n" +
				"Art. 2. Ustawa wchodzi w życie z dniem określonym w komunikacie, " +
				"o którym mowa w art. 18 ust. 1.\n",
				new DateTime(2025, 3, 20));

			var info = Assert.IsType<EntryIntoForceInfo>(document.EntryIntoForce);
			Assert.Equal(EntryIntoForceFormulaKind.DeterminedByCommunication, info.Formula.Kind);
			Assert.False(info.Formula.IsResolvable);
			Assert.Null(info.ResolvedDate);
			Assert.Equal(default, document.EffectiveDate);
		}
	}
}
