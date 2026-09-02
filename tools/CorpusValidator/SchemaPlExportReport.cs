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
///   3. każda zapisana data pochodzi z klauzuli: należy do zbioru {reguła ogólna} ∪ {daty wyjątków}.
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

		var files = EntryIntoForceReport.EnumerateDistinct(corpusRoot).ToList();
		int converted = 0, conversionErrors = 0, undated = 0, datedTotal = 0, foreignDates = 0;
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
		Console.WriteLine();

		Dump("BŁĘDY KONWERSJI", failures, 40);
		Dump("PLIKI Z ELEMENTAMI BEZ DATY", withUndated, 40);
		Dump("PLIKI Z DATĄ SPOZA KLAUZULI", withForeign, 40);

		return conversionErrors == 0 && foreignDates == 0 ? 0 : 2;
	}

	/// <summary>Daty, które klauzula w ogóle dopuszcza w tym akcie.</summary>
	private static HashSet<string> AllowedDates(LegalDocument document)
	{
		var allowed = new HashSet<string>();
		if (document.EntryIntoForce is not { } info)
			return allowed;

		if (info.ResolvedDate is { } general)
			allowed.Add(general.ToString("yyyy-MM-dd"));

		foreach (var partial in info.Partials)
			if (partial.ResolvedDate is { } exception)
				allowed.Add(exception.ToString("yyyy-MM-dd"));

		return allowed;
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
