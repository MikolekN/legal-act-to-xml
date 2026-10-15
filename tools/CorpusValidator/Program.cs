using System.Text;
using Saga.Model;
using Saga.Core.Ingest;
using Saga.Core.Services.Classify.Document;

// ============================================================================
// CorpusValidator — walidacja DocumentClassifier na oznakowanym korpusie DOCX.
//
// Ground truth = nazwa folderu kategorii (USTAWY / ROZPORZADZENIA / OBWIESZCZENIA)
// w układzie <root>/<rok>/<KATEGORIA>/*.docx. Dla każdego pliku uruchamia
// DocxBlockReader -> DocumentClassifier i zestawia predykcję z etykietą folderu.
// Wypisuje dokładność wg kategorii, macierz pomyłek, flagi kontekstowe
// (IsConsolidatedText / IsAmending), przykłady błędów oraz zapisuje raport TXT + CSV.
//
// Użycie:
//   dotnet run --project tools/CorpusValidator [-- <root-korpusu> [<sciezka-raportu.txt>]]
// Domyślny korpus (gitignore'owany, lokalny): DocRepo/Akty ogłoszone (szczotki)
// ============================================================================

// Tryb raportu klauzuli wejścia w życie — dowolny układ katalogów, przeszukiwany rekurencyjnie
// (documents/ ma układ <root>/<rok>, nie <root>/<rok>/<KATEGORIA> jak walidacja klasyfikatora):
//   dotnet run --project tools/CorpusValidator -- --entry-into-force <root> [<raport.txt>]
// Zrzut kanonicznego modelu pojedynczego pliku — do oglądania, co parser faktycznie zbudował
// (łącznie z treścią nowelizacji, której nie widać w raporcie korpusowym). Datę ogłoszenia parser
// bierze z nagłówka publikatora; argument uzupełnia ją tylko dla dokumentów, które nagłówka nie
// mają (szczotki RCL) — bez niej formuły względne zostają nierozstrzygnięte i zrzut wychodzi bez dat:
//   dotnet run --project tools/CorpusValidator -- --dump <plik.docx> <wyjscie.xml> [<rrrr-MM-dd>]
if (args.Length > 2 && args[0] == "--dump")
{
    using var dumpStream = System.IO.File.OpenRead(args[1]);
    var dumped = Saga.Core.LegalDocumentParser.Parse(dumpStream, System.IO.Path.GetFileName(args[1]),
        new Saga.Core.ParseOptions { Policy = Saga.Core.ParsePolicy.AlwaysParse });
    if (dumped.Document is null)
    {
        Console.Error.WriteLine("Parser nie zbudował modelu.");
        return 1;
    }

    // Awaryjna data dla dokumentów bez nagłówka publikatora — patrz SyntheticAnnouncementDate.
    CorpusValidator.SyntheticAnnouncementDate.ApplyIfMissing(dumped.Document, ParseDate(args, 3));

    System.IO.File.WriteAllText(args[2],
        Saga.Core.Services.Converters.CanonicalDtoXmlSerializer.Serialize(dumped.Document));
    Console.Error.WriteLine($"Zrzut modelu: {args[2]}");
    return 0;
}

// Eksport schemaPL pojedynczego pliku — przez ten sam LegalDocumentConverter, którego używa CLI.
// W odróżnieniu od --dump (kanoniczny zrzut DTO, format diagnostyczny) daje wyjście porównywalne
// wprost z plikami referencyjnymi RCL:
//   dotnet run --project tools/CorpusValidator -- --xml <plik.docx> <wyjscie.xml> [<rrrr-MM-dd>]
if (args.Length > 2 && args[0] == "--xml")
{
    using var xmlStream = System.IO.File.OpenRead(args[1]);
    var converted = Saga.Core.LegalDocumentParser.Parse(xmlStream, System.IO.Path.GetFileName(args[1]),
        new Saga.Core.ParseOptions { Policy = Saga.Core.ParsePolicy.AlwaysParse });
    if (converted.Document is null)
    {
        Console.Error.WriteLine("Parser nie zbudował modelu.");
        return 1;
    }

    // Awaryjna data dla dokumentów bez nagłówka publikatora — patrz SyntheticAnnouncementDate.
    CorpusValidator.SyntheticAnnouncementDate.ApplyIfMissing(converted.Document, ParseDate(args, 3));

    Saga.Core.Services.LegalDocumentConverter.Convert(converted.Document).Save(args[2]);
    Console.Error.WriteLine($"Eksport schemaPL: {args[2]}");
    return 0;
}

// Eksport schemaPL CAŁEGO korpusu + sprawdzenie wymagań na wyjściu XML (a nie na modelu):
//   dotnet run --project tools/CorpusValidator -- --xml-corpus <root> <katalog-wyjsciowy> [<rrrr-MM-dd>]
if (args.Length > 2 && args[0] == "--xml-corpus")
    return CorpusValidator.SchemaPlExportReport.Run(args[1], args[2], ParseDate(args, 3));

// Zestawienie dat wejścia w życie ze zrzutu z plikiem referencyjnym RCL (schemaPL):
//   dotnet run --project tools/CorpusValidator -- --compare-eif <referencja.xml> <zrzut.xml> [<raport.txt>]
if (args.Length > 2 && args[0] == "--compare-eif")
    return CorpusValidator.EntryIntoForceComparison.Run(args[1], args[2], args.Length > 3 ? args[3] : null);

// Raport podziału na zdania — klasyfikacja każdej granicy wyprodukowanej przez splitter.
// Pętla zwrotna dla grup poprawek A–E (docs/internal/wiedza-segmentacja-zdan.md §13): przebieg
// PRZED i PO zmianie reguły, potem diff dwóch CSV. Kryterium: własna kategoria spada do zera,
// pozostałe nie drgną.
//   dotnet run --project tools/CorpusValidator -- --sentence-split <root> [<raport.txt>]
if (args.Length > 0 && args[0] == "--sentence-split")
{
    var splitRoot = args.Length > 1 ? args[1] : "documents";
    var splitReport = args.Length > 2 ? args[2] : "raport-zdania.txt";
    return CorpusValidator.SentenceSplitReport.Run(splitRoot, splitReport);
}

// Raport jednostek systematyzacyjnych (część … oddział): styl szablonu vs klasyfikator vs zbudowany
// model vs eksport XML — miara planu docs/internal/plan-jednostki-systematyzacyjne.md (tabela 1.4).
// Przebieg PRZED i PO zmianie, potem diff systematizing-docs.tsv (kolumna xmlHash pilnuje, że akty
// bez jawnych jednostek eksportują się bez zmian).
//   dotnet run --project tools/CorpusValidator -- --systematizing <root> <katalog-wyjsciowy>
if (args.Length > 2 && args[0] == "--systematizing")
    return CorpusValidator.SystematizingUnitsReport.Run(args[1], args[2]);

// Struktura jednostek z naszego eksportu zestawiona z plikiem referencyjnym autorów schematu
// (legal-doc-schema/przyklady/<akt>/schemaPL.xml + źródłowy DOCX z korpusu):
//   dotnet run --project tools/CorpusValidator -- --compare-reference <plik.docx> <referencja.xml> <katalog-wyjsciowy>
if (args.Length > 3 && args[0] == "--compare-reference")
    return CorpusValidator.ReferenceStructureComparison.Run(args[1], args[2], args[3]);

// Porównanie dwóch przebiegów --xml-corpus (przed zmianą i po niej): zmienione pliki, elementy, próbka wierszy.
//   dotnet run --project tools/CorpusValidator -- --compare-exports <przed> <po> [<raport.txt>]
if (args.Length > 2 && args[0] == "--compare-exports")
    return CorpusValidator.ExportComparison.Run(args[1], args[2], args.Length > 3 ? args[3] : null);

// Zgodność bez stylów: ta sama struktura jednostek z bloków pozbawionych stylu Word (DOCX bez szablonu, PDF, TXT)?
//   dotnet run --project tools/CorpusValidator -- --styleless-parity <root> <katalog-wyjsciowy>
if (args.Length > 2 && args[0] == "--styleless-parity")
    return CorpusValidator.StylelessParityReport.Run(args[1], args[2]);

// Pokrycie tekstu: czy tekst każdego akapitu źródła doszedł do eksportu — miara akapitów pominiętych przez potok.
//   dotnet run --project tools/CorpusValidator -- --text-coverage <root> <katalog-wyjsciowy>
if (args.Length > 2 && args[0] == "--text-coverage")
    return CorpusValidator.TextCoverageReport.Run(args[1], args[2]);

// Równania Worda: ile ich jest i czy konwersja OMML → MathML daje wyrażenia zgodne ze schematem.
//   dotnet run --project tools/CorpusValidator -- --equations <root> <katalog-wyjsciowy>
if (args.Length > 2 && args[0] == "--equations")
    return CorpusValidator.EquationsReport.Run(args[1], args[2]);

if (args.Length > 0 && args[0] == "--entry-into-force")
{
    var eifRoot = args.Length > 1 ? args[1] : "documents";
    var eifReport = args.Length > 2 ? args[2] : "raport-eif.txt";
    return CorpusValidator.EntryIntoForceReport.Run(eifRoot, eifReport, ParseDate(args, 3));
}

// Data ogłoszenia z wiersza poleceń — nierozpoznana wartość jest błędem, a nie cichym null,
// bo pomyłka w formacie dawałaby raport bez dat wyglądający jak regres parsera.
static DateTime? ParseDate(string[] arguments, int index)
{
    if (arguments.Length <= index)
        return null;

    if (DateTime.TryParseExact(arguments[index], "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var date))
        return date;

    throw new ArgumentException($"Nieprawidłowa data ogłoszenia: „{arguments[index]}\" (oczekiwano rrrr-MM-dd).");
}

var corpusRoot = args.Length > 0
    ? args[0]
    : "DocRepo/Akty ogłoszone (szczotki)";
var reportPath = args.Length > 1 ? args[1] : "corpus-report.txt";
var csvPath = System.IO.Path.ChangeExtension(reportPath, ".csv");

if (!System.IO.Directory.Exists(corpusRoot))
{
    Console.Error.WriteLine($"Nie znaleziono katalogu korpusu: {corpusRoot}");
    Console.Error.WriteLine("Podaj ścieżkę jako pierwszy argument, np.:");
    Console.Error.WriteLine("  dotnet run --project tools/CorpusValidator -- \"DocRepo/Akty ogłoszone (szczotki)\"");
    return 1;
}

// folder kategorii -> oczekiwany "bucket"
static string GroundTruthBucket(string categoryFolder) => categoryFolder.ToUpperInvariant() switch
{
    "USTAWY" => "USTAWA",
    "ROZPORZADZENIA" => "ROZPORZADZENIE",
    "OBWIESZCZENIA" => "OBWIESZCZENIE",
    _ => "?",
};

// przewidziany ActType -> "bucket" do porównania z ground truth
static string PredictedBucket(DocumentClassificationResult r)
{
    if (!r.IsLegalAct || r.ActType is null) return "NIE-AKT";
    return r.ActType switch
    {
        LegalActType.Statute or LegalActType.AmendingStatute => "USTAWA",
        LegalActType.Regulation => "ROZPORZADZENIE",
        LegalActType.Announcement => "OBWIESZCZENIE",
        LegalActType.Resolution => "UCHWALA",
        LegalActType.ExecutiveOrder => "ZARZADZENIE",
        LegalActType.LocalLegalAct => "AKT-MIEJSC",
        LegalActType.Code => "KODEKS",
        _ => r.ActType.ToString()!,
    };
}

var reader = new DocxBlockReader();
var classifier = new DocumentClassifier();

var buckets = new[] { "USTAWA", "ROZPORZADZENIE", "OBWIESZCZENIE", "UCHWALA", "ZARZADZENIE", "AKT-MIEJSC", "KODEKS", "NIE-AKT" };

// confusion[groundTruth][predicted]
var confusion = new Dictionary<string, Dictionary<string, int>>();
foreach (var gt in new[] { "USTAWA", "ROZPORZADZENIE", "OBWIESZCZENIE" })
    confusion[gt] = buckets.ToDictionary(b => b, _ => 0);

var perCatTotal = new Dictionary<string, int>();
var perCatCorrect = new Dictionary<string, int>();
var perCatErrors = new Dictionary<string, int>();          // błędy odczytu docx
var perCatConsolidated = new Dictionary<string, int>();    // IsConsolidatedText
var perCatAmending = new Dictionary<string, int>();        // IsAmending
var confSum = new Dictionary<string, long>();

var misclass = new List<(string bucket, string file, string pred, int conf, bool cons, bool amend, string just)>();
var readErrors = new List<(string file, string err)>();
var csv = new StringBuilder("year,category,file,groundTruth,predicted,actType,isAct,confidence,isConsolidated,isAmending,blockCount\n");

var years = new[] { "2024", "2025", "2026" };
var categories = new[] { "USTAWY", "ROZPORZADZENIA", "OBWIESZCZENIA" };

int processed = 0;
foreach (var year in years)
{
    foreach (var cat in categories)
    {
        var dir = System.IO.Path.Combine(corpusRoot, year, cat);
        if (!System.IO.Directory.Exists(dir)) continue;
        var gt = GroundTruthBucket(cat);
        perCatTotal.TryAdd(gt, 0);
        perCatCorrect.TryAdd(gt, 0);
        perCatErrors.TryAdd(gt, 0);
        perCatConsolidated.TryAdd(gt, 0);
        perCatAmending.TryAdd(gt, 0);
        confSum.TryAdd(gt, 0);

        foreach (var file in System.IO.Directory.EnumerateFiles(dir, "*.docx").OrderBy(f => f))
        {
            processed++;
            if (processed % 200 == 0)
                Console.Error.WriteLine($"  ...{processed} plików");

            perCatTotal[gt]++;
            try
            {
                using var fs = System.IO.File.OpenRead(file);
                var blocks = reader.ReadBlocks(fs);
                var result = classifier.Classify(blocks);
                var pred = PredictedBucket(result);

                confusion[gt][pred] = confusion[gt].GetValueOrDefault(pred) + 1;
                confSum[gt] += result.Confidence;
                if (result.IsConsolidatedText) perCatConsolidated[gt]++;
                if (result.IsAmending) perCatAmending[gt]++;

                if (pred == gt) perCatCorrect[gt]++;
                else if (misclass.Count(m => m.bucket == gt) < 15)
                    misclass.Add((gt, System.IO.Path.GetFileName(file), pred, result.Confidence,
                        result.IsConsolidatedText, result.IsAmending,
                        result.Justification.Length > 120 ? result.Justification[..120] : result.Justification));

                csv.Append($"{year},{cat},\"{System.IO.Path.GetFileName(file)}\",{gt},{pred},{result.ActType},{result.IsLegalAct},{result.Confidence},{result.IsConsolidatedText},{result.IsAmending},{blocks.Count}\n");
            }
            catch (Exception ex)
            {
                perCatErrors[gt]++;
                if (readErrors.Count < 30)
                    readErrors.Add((System.IO.Path.GetFileName(file), ex.GetType().Name + ": " + ex.Message));
                csv.Append($"{year},{cat},\"{System.IO.Path.GetFileName(file)}\",{gt},READ-ERROR,,,,,,,\n");
            }
        }
    }
}

var sb = new StringBuilder();
void W(string s = "") { sb.AppendLine(s); Console.WriteLine(s); }

W("================================================================");
W(" WALIDACJA DocumentClassifier NA KORPUSIE AKTÓW OGŁOSZONYCH");
W("================================================================");
W($"Korpus: {corpusRoot}");
W($"Plików przetworzonych: {processed}");
W();

W("--- Dokładność wg kategorii (ground truth = folder) ---");
W($"{"Kategoria",-16} {"Plików",7} {"Trafień",8} {"Błędów odczytu",15} {"Dokładność",11} {"Śr. pewność",12}");
int totAll = 0, totCorrect = 0, totErr = 0;
foreach (var gt in new[] { "USTAWA", "ROZPORZADZENIE", "OBWIESZCZENIE" })
{
    var tot = perCatTotal.GetValueOrDefault(gt);
    var cor = perCatCorrect.GetValueOrDefault(gt);
    var err = perCatErrors.GetValueOrDefault(gt);
    var okReads = tot - err;
    var acc = okReads > 0 ? 100.0 * cor / okReads : 0;
    var avgConf = okReads > 0 ? (double)confSum.GetValueOrDefault(gt) / okReads : 0;
    totAll += tot; totCorrect += cor; totErr += err;
    W($"{gt,-16} {tot,7} {cor,8} {err,15} {acc,10:F1}% {avgConf,11:F1}");
}
var okReadsAll = totAll - totErr;
W($"{"RAZEM",-16} {totAll,7} {totCorrect,8} {totErr,15} {(okReadsAll > 0 ? 100.0 * totCorrect / okReadsAll : 0),10:F1}%");
W();

W("--- Macierz pomyłek (wiersz = prawda, kolumna = predykcja) ---");
var cols = new[] { "USTAWA", "ROZPORZADZENIE", "OBWIESZCZENIE", "UCHWALA", "ZARZADZENIE", "AKT-MIEJSC", "KODEKS", "NIE-AKT" };
W($"{"prawda\\pred",-16}" + string.Concat(cols.Select(c => c.Length > 8 ? c[..8].PadLeft(9) : c.PadLeft(9))));
foreach (var gt in new[] { "USTAWA", "ROZPORZADZENIE", "OBWIESZCZENIE" })
{
    var row = confusion[gt];
    W($"{gt,-16}" + string.Concat(cols.Select(c => row.GetValueOrDefault(c).ToString().PadLeft(9))));
}
W();

W("--- Flagi kontekstowe ---");
foreach (var gt in new[] { "USTAWA", "ROZPORZADZENIE", "OBWIESZCZENIE" })
    W($"{gt,-16} IsConsolidatedText={perCatConsolidated.GetValueOrDefault(gt),5}  IsAmending={perCatAmending.GetValueOrDefault(gt),5}");
W();

if (readErrors.Count > 0)
{
    W($"--- Błędy odczytu DOCX (pierwsze {readErrors.Count}) ---");
    foreach (var (file, err) in readErrors) W($"  {file}: {err}");
    W();
}

W("--- Przykłady błędnej klasyfikacji (do 15 na kategorię) ---");
foreach (var gt in new[] { "USTAWA", "ROZPORZADZENIE", "OBWIESZCZENIE" })
{
    var items = misclass.Where(m => m.bucket == gt).ToList();
    if (items.Count == 0) continue;
    W($"  [{gt}] — {items.Count} próbek:");
    foreach (var m in items)
        W($"    {m.file,-30} -> {m.pred,-14} conf={m.conf,3} cons={m.cons,-5} amend={m.amend,-5} | {m.just}");
    W();
}

System.IO.File.WriteAllText(reportPath, sb.ToString());
System.IO.File.WriteAllText(csvPath, csv.ToString());
Console.Error.WriteLine($"\nRaport: {reportPath}");
Console.Error.WriteLine($"CSV:    {csvPath}");
return 0;
