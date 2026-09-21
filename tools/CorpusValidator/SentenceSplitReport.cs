using System.Text;
using System.Text.RegularExpressions;
using Saga.Core;
using Saga.Core.Services.EntryIntoForce;
using Saga.Model;

namespace CorpusValidator;

/// <summary>
/// Tryb raportu podziału na zdania: przepuszcza korpus przez parser i klasyfikuje KAŻDĄ granicę
/// zdania, którą splitter wyprodukował. Służy PĘTLI ZWROTNEJ dla grup poprawek A–E opisanych
/// w docs/internal/wiedza-segmentacja-zdan.md §13 — każda grupa ma zbić swoją kategorię do zera,
/// nie ruszając pozostałych.
///
/// Raport mierzy granice na modelu zbudowanym przez pełny potok (a nie na surowych akapitach),
/// więc widzi dokładnie to, co trafia do XML: tekst po zdjęciu prefiksów przez buildery,
/// łącznie z treścią cytowaną w nowelizacjach.
///
/// Klasyfikatory poniżej są ŚWIADOMIE zduplikowane w narzędziu i nie są podłączone do potoku
/// parsowania: w tej wersji mają tylko mierzyć. Grupy A–D zamienią je po kolei na weta
/// w <c>ParsingFactories.IsSentenceBoundary</c> — i dopiero wtedy zmienią wynik parsowania.
///
/// Użycie:
///   dotnet run --project tools/CorpusValidator -- --sentence-split &lt;root&gt; [&lt;raport.txt&gt;]
/// </summary>
internal static class SentenceSplitReport
{
    /// <summary>Kategoria granicy — rozłączna, przypisywana w kolejności od najpewniejszej.</summary>
    private enum BoundaryCategory
    {
        /// <summary>Wygląda poprawnie albo nie daje się rozstrzygnąć automatycznie.</summary>
        Ok,

        /// <summary>Grupa A — lewy segment to samo oznaczenie jednostki, bez treści.</summary>
        LoneDesignator,

        /// <summary>Grupa B — cięcie w środku imienia i nazwiska.</summary>
        PersonInitial,

        /// <summary>Grupa C — cięcie tuż po skrócie.</summary>
        Abbreviation,
    }

    private const string Upper = "A-ZĄĆĘŁŃÓŚŹŻ";
    private const string Lower = "a-ząćęłńóśźż";

    /// <summary>Cały lewy segment jest oznaczeniem: „Art. 59a. 1.", „§ 2.", „VI.", „I.1.".</summary>
    private static readonly Regex LoneDesignatorPattern = new(
        @"^[„""()\s ]*(?:Art\.[\s ]*\d+[a-z]*\.[\s ]*)?(?:§[\s ]*\d+[a-z]*\.[\s ]*)?"
        + @"(?:\d+[a-z]*\.[\s ]*)*(?:[IVXLC]{1,6}(?:\.\d+[a-z]*)*\.[\s ]*)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Oznaczenie musi zawierać liczbę — inaczej wzorzec łapie pusty lewy segment.</summary>
    private static readonly Regex ContainsNumber = new(@"[0-9IVXLC]", RegexOptions.Compiled);

    /// <summary>Lewy segment kończy się pojedynczą wielką literą z kropką („…: A.").</summary>
    private static readonly Regex InitialAtEnd = new(
        @"(?:^|[\s „(:])[" + Upper + @"]\.$", RegexOptions.Compiled);

    /// <summary>Prawy segment zaczyna się nazwiskiem: wielka litera, dalej małe.</summary>
    private static readonly Regex SurnameAtStart = new(
        @"^[" + Upper + @"][" + Lower + @"\-]+", RegexOptions.Compiled);

    /// <summary>Funkcja poprzedzająca podpis — odróżnia inicjał od zdania kończącego się skrótem.</summary>
    private static readonly Regex SignatureContext = new(
        @"Prezydent|Marszałek|Prezes|Minister|Wojewoda|Rzecznik|Przewodnicząc|Kierownik|Szef|Dyrektor"
        + @"|Sekretarz|Podsekretarz|Kanclerz|wz\.|z up\.",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Skróty, po których w korpusie realnie dochodzi do cięcia.</summary>
    private static readonly Regex AbbreviationAtEnd = new(
        @"(?<!\w)(?:m|m\.st|im|ds|ul|prof|itp|itd|np|dr|tj|tzw|tzn|ww|ok|por|zob|cyt|st|woj|gen|ks|mgr"
        + @"|art|ust|pkt|lit|rozdz|oddz|zd|poz|nr|str|tys|mln|mld|godz|proc|zm|sp|in|syn)\.$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex Letter = new(@"[^\W\d_]", RegexOptions.Compiled);

    internal static int Run(string corpusRoot, string reportPath)
    {
        if (!Directory.Exists(corpusRoot))
        {
            Console.Error.WriteLine($"Nie znaleziono katalogu korpusu: {corpusRoot}");
            return 1;
        }

        var stats = new Dictionary<string, KindStatistics>(StringComparer.Ordinal);
        var samples = new Dictionary<BoundaryCategory, List<string>>();
        foreach (BoundaryCategory category in Enum.GetValues<BoundaryCategory>())
            samples[category] = new List<string>();

        var files = Directory.EnumerateFiles(corpusRoot, "*.docx", SearchOption.AllDirectories)
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToList();

        foreach (var file in files)
        {
            var kind = DescribeKind(corpusRoot, file);
            if (!stats.TryGetValue(kind, out var kindStats))
                stats[kind] = kindStats = new KindStatistics();

            kindStats.Documents++;

            LegalDocument? document;
            try
            {
                using var stream = File.OpenRead(file);
                document = LegalDocumentParser
                    .Parse(stream, Path.GetFileName(file), new ParseOptions { Policy = ParsePolicy.AlwaysParse })
                    .Document;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"  pominięto {Path.GetFileName(file)}: {exception.GetType().Name}");
                kindStats.Failed++;
                continue;
            }

            if (document is null)
            {
                kindStats.Failed++;
                continue;
            }

            Measure(document, kindStats, samples);
        }

        WriteReport(corpusRoot, reportPath, stats, samples);
        return 0;
    }

    private static void Measure(
        LegalDocument document,
        KindStatistics stats,
        Dictionary<BoundaryCategory, List<string>> samples)
    {
        foreach (var unit in EditorialUnitWalker.SelfAndDescendants(document.RootPart, includeAmendmentContent: true))
        {
            if (unit is not IHasTextSegments holder)
                continue;

            var segments = holder.TextSegments;
            if (segments.Count == 0)
                continue;

            stats.Units++;
            if (segments.Count == 1)
                continue;

            stats.UnitsWithSplit++;

            for (int index = 0; index < segments.Count - 1; index++)
            {
                var left = segments[index].Text.Trim();
                var right = segments[index + 1].Text.Trim();

                stats.Boundaries++;

                var category = Classify(left, right);
                stats.ByCategory[category]++;

                if (IsAllCapsContext(left))
                    stats.AllCapsContext++;

                var bucket = samples[category];
                if (bucket.Count < SampleLimit)
                    bucket.Add($"…{Tail(left, 60)}  ⟂  {Head(right, 46)}…");
            }
        }
    }

    /// <summary>
    /// Kolejność kategorii jest istotna: „samo oznaczenie" bije skrót, bo „Art. 59a. 1."
    /// kończy się kropką po liczbie, a nie po skrócie, i to oznaczenie jest tu przyczyną.
    /// </summary>
    private static BoundaryCategory Classify(string left, string right)
    {
        if (LoneDesignatorPattern.IsMatch(left) && ContainsNumber.IsMatch(left))
            return BoundaryCategory.LoneDesignator;

        if (IsPersonInitial(left, right))
            return BoundaryCategory.PersonInitial;

        if (AbbreviationAtEnd.IsMatch(left))
            return BoundaryCategory.Abbreviation;

        return BoundaryCategory.Ok;
    }

    /// <summary>
    /// Inicjał rozpoznajemy tylko razem z prawym kontekstem (nazwisko) i z lewym (funkcja albo
    /// bardzo krótki segment). Sam wzorzec „wielka litera z kropką" łapałby też zdania kończące
    /// się skrótem jednoliterowym.
    /// </summary>
    private static bool IsPersonInitial(string left, string right)
    {
        if (!InitialAtEnd.IsMatch(left) || !SurnameAtStart.IsMatch(right))
            return false;

        return SignatureContext.IsMatch(left)
            || left.TrimEnd('.').TrimEnd().EndsWith(':')
            || left.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 3;
    }

    /// <summary>
    /// Kontekst wersalikowy — w nim warunek „po kropce stoi wielka litera" jest spełniony zawsze,
    /// więc nie niesie informacji. Liczony OSOBNO, bo nakłada się na pozostałe kategorie
    /// (nagłówek „…PKT 2 LIT. A USTAWY" jest jednocześnie cięciem po skrócie).
    /// </summary>
    private static bool IsAllCapsContext(string text)
    {
        var letters = Letter.Matches(text).Count;
        if (letters < 12)
            return false;

        var upper = text.Count(char.IsUpper);
        return upper > letters * 0.9;
    }

    private static string DescribeKind(string corpusRoot, string file)
    {
        var relative = Path.GetRelativePath(corpusRoot, file);
        var top = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];

        return top is "Obwieszczenia" or "Rozporządzenia" ? top : "Ustawy";
    }

    private static void WriteReport(
        string corpusRoot,
        string reportPath,
        Dictionary<string, KindStatistics> stats,
        Dictionary<BoundaryCategory, List<string>> samples)
    {
        var total = new KindStatistics();
        foreach (var kindStats in stats.Values)
            total.Add(kindStats);

        var report = new StringBuilder();
        report.AppendLine("RAPORT PODZIAŁU NA ZDANIA");
        report.AppendLine($"korpus: {corpusRoot}");
        report.AppendLine($"data:   {DateTime.Now:yyyy-MM-dd HH:mm}");
        report.AppendLine();
        report.AppendLine($"{"rodzaj",-16}{"dok.",8}{"jedn.",10}{"z podz.",10}{"granic",9}{"błędnych",10}{"%",7}");
        report.AppendLine(new string('-', 70));

        foreach (var (kind, kindStats) in stats.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            report.AppendLine(FormatRow(kind, kindStats));

        report.AppendLine(new string('-', 70));
        report.AppendLine(FormatRow("RAZEM", total));
        report.AppendLine();

        report.AppendLine("ROZBICIE BŁĘDNYCH GRANIC NA KATEGORIE");
        foreach (var category in new[]
                 {
                     BoundaryCategory.LoneDesignator,
                     BoundaryCategory.PersonInitial,
                     BoundaryCategory.Abbreviation,
                 })
        {
            report.AppendLine($"  {Describe(category),-46}{total.ByCategory[category],8}");
        }

        report.AppendLine($"  {"(nakładka) granice w tekście wersalikami",-46}{total.AllCapsContext,8}");
        report.AppendLine($"  {"granice wyglądające poprawnie",-46}{total.ByCategory[BoundaryCategory.Ok],8}");
        report.AppendLine();

        foreach (var (category, bucket) in samples.OrderBy(entry => entry.Key.ToString(), StringComparer.Ordinal))
        {
            if (bucket.Count == 0 || category == BoundaryCategory.Ok)
                continue;

            report.AppendLine($"PRÓBKI — {Describe(category)}");
            foreach (var sample in bucket)
                report.AppendLine($"   {sample}");
            report.AppendLine();
        }

        File.WriteAllText(reportPath, report.ToString());

        // CSV do porównywania przebiegów PRZED i PO zmianie w regule — jeden wiersz na rodzaj aktu.
        var csv = new StringBuilder("rodzaj,dokumenty,nieudane,jednostki,zPodzialem,granice,"
            + "samoOznaczenie,inicjal,skrot,wersaliki,poprawne\n");
        foreach (var (kind, kindStats) in stats.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            csv.Append(kind).Append(',')
                .Append(kindStats.Documents).Append(',')
                .Append(kindStats.Failed).Append(',')
                .Append(kindStats.Units).Append(',')
                .Append(kindStats.UnitsWithSplit).Append(',')
                .Append(kindStats.Boundaries).Append(',')
                .Append(kindStats.ByCategory[BoundaryCategory.LoneDesignator]).Append(',')
                .Append(kindStats.ByCategory[BoundaryCategory.PersonInitial]).Append(',')
                .Append(kindStats.ByCategory[BoundaryCategory.Abbreviation]).Append(',')
                .Append(kindStats.AllCapsContext).Append(',')
                .Append(kindStats.ByCategory[BoundaryCategory.Ok]).Append('\n');
        }

        var csvPath = Path.ChangeExtension(reportPath, ".csv");
        File.WriteAllText(csvPath, csv.ToString());

        Console.Error.WriteLine(report.ToString());
        Console.Error.WriteLine($"Raport: {reportPath}");
        Console.Error.WriteLine($"CSV:    {csvPath}");
    }

    private static string FormatRow(string label, KindStatistics stats)
    {
        var wrong = stats.WrongBoundaries;
        var share = stats.Boundaries == 0 ? 0 : wrong * 100.0 / stats.Boundaries;

        return $"{label,-16}{stats.Documents,8}{stats.Units,10}{stats.UnitsWithSplit,10}"
            + $"{stats.Boundaries,9}{wrong,10}{share,6:F1}%";
    }

    private static string Describe(BoundaryCategory category) => category switch
    {
        BoundaryCategory.LoneDesignator => "A — samo oznaczenie jednostki",
        BoundaryCategory.PersonInitial => "B — inicjał imienia",
        BoundaryCategory.Abbreviation => "C — skrót",
        _ => "poprawne",
    };

    private static string Tail(string text, int length)
        => text.Length <= length ? text : text[^length..];

    private static string Head(string text, int length)
        => text.Length <= length ? text : text[..length];

    private const int SampleLimit = 8;

    private sealed class KindStatistics
    {
        public int Documents;
        public int Failed;
        public int Units;
        public int UnitsWithSplit;
        public int Boundaries;
        public int AllCapsContext;

        public Dictionary<BoundaryCategory, int> ByCategory { get; } =
            Enum.GetValues<BoundaryCategory>().ToDictionary(category => category, _ => 0);

        public int WrongBoundaries => Boundaries - ByCategory[BoundaryCategory.Ok];

        public void Add(KindStatistics other)
        {
            Documents += other.Documents;
            Failed += other.Failed;
            Units += other.Units;
            UnitsWithSplit += other.UnitsWithSplit;
            Boundaries += other.Boundaries;
            AllCapsContext += other.AllCapsContext;

            foreach (var (category, count) in other.ByCategory)
                ByCategory[category] += count;
        }
    }
}
