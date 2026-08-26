using System.Text;
using Saga.Core;
using Saga.Model.EntryIntoForce;

namespace CorpusValidator;

/// <summary>
/// Tryb raportu klauzuli wejścia w życie: przepuszcza korpus DOCX przez parser i zestawia, co udało
/// się rozpoznać. Służy PĘTLI ZWROTNEJ — każdy wiersz „brak klauzuli" albo z formułą
/// <c>Unrecognized</c> jest kandydatem na nowy przypadek testowy, a nie sygnałem awarii.
///
/// Data ogłoszenia nie jest znana dla szczotek RCL (to metadana Dz.U., powstająca po ich wytworzeniu),
/// więc bez argumentu <paramref name="announcementDate"/> daty względne pozostają nierozstrzygnięte
/// i raport mierzy wyłącznie DETEKCJĘ i ROZPOZNANIE FORMUŁY. Data podana z wiersza poleceń jest
/// SYNTETYCZNA — służy tylko temu, żeby formuły względne się rozstrzygnęły i dało się zmierzyć
/// rozsyłanie dat po drzewie; nie twierdzi nic o rzeczywistej dacie ogłoszenia aktu.
///
/// Główny sposób użycia: przebieg PRZED i PO zmianie w parserze, potem diff dwóch CSV.
/// Kryterium regresu: żadna już rozstrzygnięta data ani rodzaj formuły NIE MOŻE się zmienić.
/// Dlatego metryki rozsyłania idą do OSOBNEGO pliku (<c>…-daty.csv</c>) — główny CSV ma zostać
/// porównywalny bajt w bajt między wersjami.
/// </summary>
internal static class EntryIntoForceReport
{
	internal static int Run(string corpusRoot, string reportPath, DateTime? announcementDate = null)
	{
		if (!Directory.Exists(corpusRoot))
		{
			Console.Error.WriteLine($"Nie znaleziono katalogu korpusu: {corpusRoot}");
			return 1;
		}

		var csvPath = Path.ChangeExtension(reportPath, ".csv");
		var csv = new StringBuilder(
			"plik,znaleziona,jednostka,formula,operand,dataRozstrzygnieta,mocWsteczna," +
			"wyjatkow,wyjatkowNieprzypisanych,celow,fragmentowNierozlozonych,klauzula\n");

		var datesPath = Path.ChangeExtension(reportPath, null) + "-daty.csv";
		var dates = new StringBuilder(
			"plik,dataOgolna,jednostekOgolem,jednostekZData,jednostekBezDaty," +
			"zdanOgolem,zdanZData,datRoznychOdOgolnej\n");
		var totals = new PropagationCounters();

		var kindCounts = new Dictionary<string, int>();
		var files = EnumerateDistinct(corpusRoot).ToList();

		int processed = 0, found = 0, complete = 0, readErrors = 0, withPartials = 0;
		var misses = new List<string>();
		var unrecognized = new List<(string File, string Text)>();
		var unresolvedFragments = new List<(string File, string Fragment)>();

		foreach (var file in files)
		{
			processed++;
			if (processed % 25 == 0)
				Console.Error.WriteLine($"  ...{processed}/{files.Count}");

			var name = Path.GetFileName(file);

			try
			{
				using var stream = File.OpenRead(file);
				var result = LegalDocumentParser.Parse(stream, name, new ParseOptions
				{
					Policy = ParsePolicy.AlwaysParse,
				});

				// TYMCZASOWE OBEJŚCIE braku parsowania daty ogłoszenia — patrz AnnouncementDateScaffold.
				if (result.Document is { } built)
					AnnouncementDateScaffold.Reapply(built, announcementDate);

				var info = result.Document?.EntryIntoForce;
				if (info is null)
				{
					misses.Add(name);
					csv.Append($"\"{name}\",nie,,,,,,,,,,\n");
					continue;
				}

				var counters = PropagationCounters.Measure(result.Document!, info.ResolvedDate);
				totals.Add(counters);
				dates.Append($"\"{name}\",{info.ResolvedDate:yyyy-MM-dd},{counters.Units},{counters.UnitsDated}," +
				             $"{counters.Units - counters.UnitsDated},{counters.Sentences},{counters.SentencesDated}," +
				             $"{counters.DifferentFromGeneral}\n");

				found++;
				if (info.IsComplete) complete++;
				if (info.Partials.Count > 0) withPartials++;

				var kind = info.Formula.Kind.ToString();
				kindCounts[kind] = kindCounts.GetValueOrDefault(kind) + 1;

				if (info.Formula.Kind == EntryIntoForceFormulaKind.Unrecognized && unrecognized.Count < 40)
					unrecognized.Add((name, Truncate(info.Formula.RawText, 160)));

				foreach (var fragment in info.UnresolvedFragments)
					if (unresolvedFragments.Count < 80)
						unresolvedFragments.Add((name, Truncate(fragment, 160)));

				var notApplicable = info.Partials.Count(p => !p.IsApplicable);
				var targets = info.Partials.Sum(p => p.Targets.Count);

				csv.Append($"\"{name}\",tak,{info.SourceUnitEId},{kind},{info.Formula.Operand}," +
				           $"{info.ResolvedDate:yyyy-MM-dd},{info.RetroactiveFrom:yyyy-MM-dd}," +
				           $"{info.Partials.Count},{notApplicable},{targets},{info.UnresolvedFragments.Count}," +
				           $"\"{Csv(Truncate(info.RawText, 400))}\"\n");
			}
			catch (Exception ex)
			{
				readErrors++;
				csv.Append($"\"{name}\",BLAD-ODCZYTU,,,,,,,,,,\"{Csv(ex.Message)}\"\n");
			}
		}

		var sb = new StringBuilder();
		void W(string s = "") { sb.AppendLine(s); Console.WriteLine(s); }

		W("================================================================");
		W(" KLAUZULA WEJŚCIA W ŻYCIE — RAPORT KORPUSOWY");
		W("================================================================");
		W($"Korpus: {corpusRoot}");
		W($"Plików: {processed}   błędów odczytu: {readErrors}");
		W();

		var parsed = processed - readErrors;
		W("--- Detekcja ---");
		W($"Klauzula znaleziona:            {found,5} / {parsed}   ({Percent(found, parsed)})");
		W($"  w tym z wyjątkami:            {withPartials,5}");
		W($"  rozłożona bez reszty:         {complete,5}   (bez daty ogłoszenia dotyczy to tylko dat jawnych)");
		W($"Brak klauzuli:                  {misses.Count,5}");
		W();

		W("--- Rozpoznane formuły ---");
		foreach (var (kind, count) in kindCounts.OrderByDescending(k => k.Value))
			W($"  {kind,-42} {count,5}");
		W();

		if (misses.Count > 0)
		{
			W($"--- Pliki bez wykrytej klauzuli ({misses.Count}) ---");
			foreach (var name in misses.Take(40)) W($"  {name}");
			if (misses.Count > 40) W($"  … i {misses.Count - 40} więcej (pełna lista w CSV)");
			W();
		}

		if (unrecognized.Count > 0)
		{
			W($"--- Formuły nierozpoznane ({unrecognized.Count}) — kandydaci na nowe wzorce ---");
			foreach (var (file, text) in unrecognized) W($"  [{file}] {text}");
			W();
		}

		if (unresolvedFragments.Count > 0)
		{
			W($"--- Fragmenty nierozłożone ({unresolvedFragments.Count}) — kandydaci na InlineData ---");
			foreach (var (file, fragment) in unresolvedFragments) W($"  [{file}] {fragment}");
			W();
		}

		W("--- Rozsyłanie dat po drzewie ---");
		W(announcementDate is { } given
			? $"Data ogłoszenia (syntetyczna, z wiersza poleceń): {given:yyyy-MM-dd}"
			: "Bez daty ogłoszenia — formuły względne nierozstrzygnięte, więc niskie liczby są POPRAWNE.");
		W($"Jednostek ogółem:               {totals.Units,7}");
		W($"  z datą:                       {totals.UnitsDated,7}   ({Percent(totals.UnitsDated, totals.Units)})");
		W($"  bez daty:                     {totals.Units - totals.UnitsDated,7}");
		W($"Zdań ogółem:                    {totals.Sentences,7}");
		W($"  z datą:                       {totals.SentencesDated,7}   ({Percent(totals.SentencesDated, totals.Sentences)})");
		W($"Dat różnych od reguły ogólnej:  {totals.DifferentFromGeneral,7}   (nie może zmaleć między wersjami)");
		W();

		File.WriteAllText(reportPath, sb.ToString());
		File.WriteAllText(csvPath, csv.ToString());
		File.WriteAllText(datesPath, dates.ToString());
		Console.Error.WriteLine($"\nRaport: {reportPath}");
		Console.Error.WriteLine($"CSV:    {csvPath}");
		Console.Error.WriteLine($"Daty:   {datesPath}");
		return 0;
	}

	/// <summary>
	/// Pliki bez duplikatów. Katalog documents/ trzyma te same akty dwa razy: płasko w korzeniu
	/// i w podkatalogach rocznikowych — liczenie obu zafałszowałoby statystyki.
	/// </summary>
	internal static IEnumerable<string> EnumerateDistinct(string root) =>
		Directory.EnumerateFiles(root, "*.docx", SearchOption.AllDirectories)
			.GroupBy(Path.GetFileName)
			.Select(g => g.First())
			.OrderBy(f => f);

	private static string Truncate(string? text, int max)
	{
		var value = (text ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ').Trim();
		return value.Length > max ? value[..max] + "…" : value;
	}

	private static string Csv(string text) => text.Replace("\"", "\"\"");

	private static string Percent(int part, int total) =>
		total > 0 ? $"{100.0 * part / total:F1}%" : "—";
}
