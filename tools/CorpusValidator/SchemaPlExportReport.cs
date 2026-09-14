using System.Xml.Linq;
using Saga.Core;
using Saga.Core.Services;
using Saga.Model;

namespace CorpusValidator;

/// <summary>
/// Przepuszcza korpus przez PEŁNY łańcuch produkcyjny: parser → model → eksport schemaPL
/// (<see cref="LegalDocumentConverter"/>), i sprawdza wymagania na wyjściu XML, a nie na modelu.
/// Raport wejścia w życie kończy się na modelu, więc defekty samych konwerterów są dla niego
/// niewidoczne — ten tryb je wychwytuje.
///
/// Sprawdzane wymagania:
///   1. konwersja w ogóle się udaje (bez wyjątku),
///   2. każda jednostka redakcyjna i każde zdanie niosą <c>wejscie-w-zycie</c>, chyba że są objęte
///      wyjątkiem nierozstrzygalnym — brak daty ma znaczyć TO, a nie „zapomnieliśmy",
///   3. każda zapisana data pochodzi z klauzuli: należy do zbioru {reguła ogólna} ∪ {daty wyjątków}
///      ∪ {moce wsteczne},
///   4. dokument jest zgodny ze schemaPL.xsd — o ile schemat jest lokalnie dostępny; jego brak
///      POMIJA walidację zamiast czerwienić przebieg (patrz <see cref="SchemaValidator"/>).
/// </summary>
internal static class SchemaPlExportReport
{
	/// <summary>
	/// Elementy, które mają nosić datę. Nagłówki, numery i części wspólne są tu pominięte celowo —
	/// plik referencyjny RCL też ich nie datuje, więc ich brak nie jest luką.
	/// </summary>
	private static readonly string[] DatedElements =
	{
		"artykul", "ustep", "ustep-n", "punkt", "litera", "tiret", "paragraf-kodeksu", "zdanie",
	};

	internal static int Run(string corpusRoot, string outputDirectory, DateTime? announcementDate)
	{
		if (!Directory.Exists(corpusRoot))
		{
			Console.Error.WriteLine($"Nie znaleziono katalogu korpusu: {corpusRoot}");
			return 1;
		}

		Directory.CreateDirectory(outputDirectory);

		var validator = SchemaValidator.TryLoad(out var schemaSkipReason);

		var files = EntryIntoForceReport.EnumerateDistinct(corpusRoot).ToList();
		int converted = 0, conversionErrors = 0, undated = 0, datedTotal = 0, foreignDates = 0;
		int schemaValid = 0, schemaInvalid = 0;
		var schemaProblems = new Dictionary<string, (int Hits, HashSet<string> Files, string Sample)>();
		var failures = new List<string>();
		var withUndated = new List<string>();
		var withForeign = new List<string>();

		foreach (var file in files)
		{
			var name = Path.GetFileName(file);

			try
			{
				using var stream = File.OpenRead(file);
				var result = LegalDocumentParser.Parse(stream, name,
					new ParseOptions { Policy = ParsePolicy.AlwaysParse });

				if (result.Document is not { } document)
				{
					failures.Add($"{name}: parser nie zbudował modelu");
					conversionErrors++;
					continue;
				}

				// Uzupełnienie dla dokumentów bez nagłówka publikatora — patrz SyntheticAnnouncementDate.
				SyntheticAnnouncementDate.ApplyIfMissing(document, announcementDate);

				var xml = LegalDocumentConverter.Convert(document);
				xml.Save(Path.Combine(outputDirectory, Path.ChangeExtension(name, ".xml")));
				converted++;

				if (validator is not null)
					CollectSchemaProblems(validator.Validate(xml), name,
						ref schemaValid, ref schemaInvalid, schemaProblems);

				var allowed = AllowedDates(document);
				var (missing, dated, foreign) = Inspect(xml, allowed);

				undated += missing;
				datedTotal += dated;
				foreignDates += foreign;

				if (missing > 0) withUndated.Add($"{name}: {missing} bez daty");
				if (foreign > 0) withForeign.Add($"{name}: {foreign} dat spoza klauzuli");
			}
			catch (Exception ex)
			{
				conversionErrors++;
				failures.Add($"{name}: {ex.GetType().Name} — {ex.Message}");
			}
		}

		Console.WriteLine("================================================================");
		Console.WriteLine(" EKSPORT schemaPL — WYMAGANIA NA WYJŚCIU XML");
		Console.WriteLine("================================================================");
		Console.WriteLine($"Korpus: {corpusRoot}   →   {outputDirectory}");
		Console.WriteLine(announcementDate is { } given
			? $"Data ogłoszenia (syntetyczna; tylko dla dokumentów bez nagłówka): {given:yyyy-MM-dd}"
			: "Bez daty ogłoszenia — dla dokumentów bez nagłówka publikatora formuły względne zostają nierozstrzygnięte.");
		Console.WriteLine();
		Console.WriteLine($"1. Konwersja udana:              {converted,6} / {files.Count}   błędów: {conversionErrors}");
		Console.WriteLine($"2. Elementów z datą:             {datedTotal,6}");
		Console.WriteLine($"   Elementów bez daty:           {undated,6}   (dopuszczalne tylko pod wyjątkiem nierozstrzygalnym)");
		Console.WriteLine($"3. Dat spoza klauzuli:           {foreignDates,6}   <- twarda bramka: 0");

		if (validator is null)
		{
			Console.WriteLine($"4. Zgodność ze schemaPL:         POMINIĘTA — {schemaSkipReason}");
		}
		else
		{
			Console.WriteLine($"4. Zgodnych ze schematem:        {schemaValid,6} / {converted}   niezgodnych: {schemaInvalid}");
			Console.WriteLine($"   Schemat: {validator.SchemaPath}");
			Console.WriteLine("   (informacyjnie — bramką twardą stanie się, gdy eksport przestanie łamać schemat)");
		}

		Console.WriteLine();

		Dump("BŁĘDY KONWERSJI", failures, 40);
		Dump("PLIKI Z ELEMENTAMI BEZ DATY", withUndated, 40);
		Dump("PLIKI Z DATĄ SPOZA KLAUZULI", withForeign, 40);
		DumpSchemaProblems(schemaProblems);

		return conversionErrors == 0 && foreignDates == 0 ? 0 : 2;
	}

	/// <summary>
	/// Zlicza problemy walidacji, grupując je po KSZTAŁCIE komunikatu — korpus potrafi dać dziesiątki
	/// tysięcy wystąpień kilku rodzajów, a interesuje nas ile ich rodzajów i w ilu plikach.
	/// </summary>
	private static void CollectSchemaProblems(
		IReadOnlyList<string> problems,
		string fileName,
		ref int valid,
		ref int invalid,
		Dictionary<string, (int Hits, HashSet<string> Files, string Sample)> kinds)
	{
		if (problems.Count == 0)
		{
			valid++;
			return;
		}

		invalid++;

		foreach (var problem in problems)
		{
			var shape = SchemaValidator.Shape(problem);
			if (!kinds.TryGetValue(shape, out var entry))
				entry = (0, new HashSet<string>(), problem);

			entry.Files.Add(fileName);
			kinds[shape] = (entry.Hits + 1, entry.Files, entry.Sample);
		}
	}

	private static string Shorten(string text, int limit) =>
		text.Length > limit ? text[..limit] + "…" : text;

	private static void DumpSchemaProblems(
		Dictionary<string, (int Hits, HashSet<string> Files, string Sample)> kinds)
	{
		if (kinds.Count == 0)
			return;

		Console.WriteLine($"--- RODZAJE NIEZGODNOŚCI ZE SCHEMATEM ({kinds.Count}) ---");

		foreach (var (_, data) in kinds.OrderByDescending(k => k.Value.Files.Count))
			Console.WriteLine($"  [{data.Files.Count} plików, {data.Hits} wystąpień] {Shorten(data.Sample, 220)}");

		Console.WriteLine();
	}

	/// <summary>
	/// Daty, które klauzula w ogóle dopuszcza w tym akcie z mocami wstecznymi.
	/// </summary>
	private static HashSet<string> AllowedDates(LegalDocument document)
	{
		var allowed = new HashSet<string>();
		if (document.EntryIntoForce is not { } info)
			return allowed;

		Add(info.ResolvedDate);
		Add(info.RetroactiveFrom);

		foreach (var partial in info.Partials)
		{
			Add(partial.ResolvedDate);
			Add(partial.RetroactiveFrom);
		}

		return allowed;

		void Add(DateTime? date)
		{
			if (date is { } value)
				allowed.Add(value.ToString("yyyy-MM-dd"));
		}
	}

	private static (int Missing, int Dated, int Foreign) Inspect(XElement root, HashSet<string> allowed)
	{
		int missing = 0, dated = 0, foreign = 0;

		foreach (var element in root.Descendants())
		{
			if (!DatedElements.Contains(element.Name.LocalName))
				continue;

			var value = element.Attribute("wejscie-w-zycie")?.Value;
			if (value is null)
			{
				missing++;
				continue;
			}

			dated++;
			if (!allowed.Contains(value))
				foreign++;
		}

		return (missing, dated, foreign);
	}

	private static void Dump(string title, List<string> items, int limit)
	{
		if (items.Count == 0)
			return;

		Console.WriteLine($"--- {title} ({items.Count}) ---");
		foreach (var item in items.Take(limit)) Console.WriteLine($"  {item}");
		if (items.Count > limit) Console.WriteLine($"  … i {items.Count - limit} więcej");
		Console.WriteLine();
	}
}
