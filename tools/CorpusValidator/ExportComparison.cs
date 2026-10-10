using System.Text;
using System.Text.RegularExpressions;

namespace CorpusValidator;

/// <summary>
/// Porównuje dwa katalogi eksportu schemaPL — dwa przebiegi <c>--xml-corpus</c>, przed zmianą i po niej.
/// Odpowiada na pytanie, co zmiana zrobiła z wyjściem: ile plików ruszyła, których elementów przybyło
/// i ubyło, i jak wyglądają zmienione wiersze.
///
/// Identyfikatory GUID są losowe w każdym przebiegu, więc są pomijane. Wiersze porównywane są jako
/// zbiory z krotnością (bez wyrównywania), co wystarcza do pokazania rodzaju zmiany i jest szybkie
/// dla całego korpusu.
///
/// Użycie:
///   dotnet run --project tools/CorpusValidator -- --compare-exports &lt;przed&gt; &lt;po&gt; [&lt;raport.txt&gt;]
/// </summary>
internal static class ExportComparison
{
    private const int FilesListed = 40;
    private const int SamplesPerElement = 3;
    private const int SampleWidth = 180;

    private static readonly Regex Volatile = new(
        @"\s(?:gd:)?\w*GUID\w*=""[^""]*""|<(?:gd:)?GUIDwersji>[^<]*</(?:gd:)?GUIDwersji>", RegexOptions.Compiled);

    private static readonly Regex ElementStart = new(@"<([A-Za-z][\w:.\-]*)", RegexOptions.Compiled);

    private sealed class ElementChange
    {
        public int Added, Removed;
        public readonly List<(string File, int Delta)> Files = [];
    }

    private sealed class LineChange
    {
        public int Added, Removed;
        public readonly List<string> Samples = [];
    }

    internal static int Run(string before, string after, string? reportPath)
    {
        if (!Directory.Exists(before) || !Directory.Exists(after))
        {
            Console.Error.WriteLine($"Brak katalogu: {(Directory.Exists(before) ? after : before)}");
            return 1;
        }

        var beforeFiles = Directory.EnumerateFiles(before, "*.xml").Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal)!;
        var afterFiles = Directory.EnumerateFiles(after, "*.xml").Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal)!;
        var common = beforeFiles.Intersect(afterFiles).OrderBy(name => name, StringComparer.Ordinal).ToList();

        var elements = new SortedDictionary<string, ElementChange>(StringComparer.Ordinal);
        var lines = new SortedDictionary<string, LineChange>(StringComparer.Ordinal);
        var changed = new List<(string File, int Removed, int Added)>();

        foreach (var name in common)
        {
            var old = Volatile.Replace(File.ReadAllText(Path.Combine(before, name!)), "");
            var current = Volatile.Replace(File.ReadAllText(Path.Combine(after, name!)), "");
            if (string.Equals(old, current, StringComparison.Ordinal))
                continue;

            CompareElements(name!, old, current, elements);
            var (removed, added) = CompareLines(name!, old, current, lines);
            changed.Add((name!, removed, added));
        }

        var report = Report(before, after, beforeFiles!, afterFiles!, common.Count, changed, elements, lines);
        if (reportPath is not null)
            File.WriteAllText(reportPath, report);
        Console.WriteLine(report);
        return 0;
    }

    private static void CompareElements(string file, string old, string current, SortedDictionary<string, ElementChange> elements)
    {
        var before = Count(ElementStart.Matches(old).Select(m => m.Groups[1].Value));
        var after = Count(ElementStart.Matches(current).Select(m => m.Groups[1].Value));

        foreach (var name in before.Keys.Union(after.Keys))
        {
            var delta = after.GetValueOrDefault(name) - before.GetValueOrDefault(name);
            if (delta == 0)
                continue;

            if (!elements.TryGetValue(name, out var change))
                elements[name] = change = new ElementChange();
            if (delta > 0) change.Added += delta; else change.Removed -= delta;
            change.Files.Add((file, delta));
        }
    }

    private static (int Removed, int Added) CompareLines(string file, string old, string current, SortedDictionary<string, LineChange> lines)
    {
        // Wiersz z samym znacznikiem zamykającym powtarza tylko to, co mówi wiersz otwierający.
        static bool Carries(string line) =>
            line.Length > 0 && !(line.StartsWith("</", StringComparison.Ordinal) && line.IndexOf('>') == line.Length - 1);

        var before = Count(old.Split('\n').Select(line => line.Trim()).Where(Carries));
        var after = Count(current.Split('\n').Select(line => line.Trim()).Where(Carries));
        int removed = 0, added = 0;

        foreach (var line in before.Keys.Union(after.Keys))
        {
            var delta = after.GetValueOrDefault(line) - before.GetValueOrDefault(line);
            if (delta == 0)
                continue;

            var element = ElementStart.Match(line) is { Success: true } start ? start.Groups[1].Value : "(tekst)";
            if (!lines.TryGetValue(element, out var change))
                lines[element] = change = new LineChange();

            if (delta > 0) { change.Added += delta; added += delta; } else { change.Removed -= delta; removed -= delta; }
            if (change.Samples.Count < SamplesPerElement)
                change.Samples.Add($"{(delta > 0 ? '+' : '-')} {file}: {(line.Length > SampleWidth ? line[..SampleWidth] + "…" : line)}");
        }

        return (removed, added);
    }

    private static Dictionary<string, int> Count(IEnumerable<string> items)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in items)
            counts[item] = counts.GetValueOrDefault(item) + 1;
        return counts;
    }

    private static string Report(
        string before, string after, HashSet<string> beforeFiles, HashSet<string> afterFiles, int common,
        List<(string File, int Removed, int Added)> changed,
        SortedDictionary<string, ElementChange> elements, SortedDictionary<string, LineChange> lines)
    {
        var sb = new StringBuilder();
        void W(string line = "") => sb.AppendLine(line);

        W("================================================================");
        W(" PORÓWNANIE DWÓCH EKSPORTÓW (bez identyfikatorów GUID)");
        W("================================================================");
        W($"Przed: {before}   ({beforeFiles.Count} plików)");
        W($"Po:    {after}   ({afterFiles.Count} plików)");
        W($"Tylko przed: {beforeFiles.Except(afterFiles).Count()}   tylko po: {afterFiles.Except(beforeFiles).Count()}");
        W($"Zmienionych: {changed.Count} z {common} wspólnych");
        if (changed.Count == 0)
            return sb.ToString();

        W();
        W("--- Elementy: zmiana liczby wystąpień ---");
        foreach (var (name, change) in elements.OrderByDescending(e => e.Value.Added + e.Value.Removed))
        {
            var top = string.Join(", ", change.Files.OrderByDescending(f => Math.Abs(f.Delta)).Take(3)
                .Select(f => $"{Path.GetFileNameWithoutExtension(f.File)} {f.Delta:+#;-#}"));
            W($"  {name,-28} +{change.Added,-6} -{change.Removed,-6} w {change.Files.Count,4} plikach   ({top})");
        }

        W();
        W("--- Zmienione wiersze wg elementu, z próbką ---");
        foreach (var (name, change) in lines.OrderByDescending(l => l.Value.Added + l.Value.Removed))
        {
            W($"  {name,-28} +{change.Added,-6} -{change.Removed,-6}");
            foreach (var sample in change.Samples)
                W($"      {sample}");
        }

        W();
        W($"--- Zmienione pliki ({Math.Min(changed.Count, FilesListed)} z {changed.Count}, od największej zmiany) ---");
        foreach (var (file, removed, added) in changed.OrderByDescending(c => c.Removed + c.Added).Take(FilesListed))
            W($"  {Path.GetFileNameWithoutExtension(file),-22} -{removed,-6} +{added,-6} wierszy");

        return sb.ToString();
    }
}
