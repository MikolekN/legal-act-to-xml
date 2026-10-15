using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Saga.Core;
using Saga.Core.Ingest;
using Saga.Core.Services;

namespace CorpusValidator;

/// <summary>
/// Zgodność bez stylów: czy potok buduje tę samą strukturę jednostek, gdy akapity nie niosą stylu Word.
/// Styl szablonu jest najsilniejszym sygnałem klasyfikacji, ale wejście bywa go pozbawione — DOCX bez
/// szablonu, PDF, TXT. Miara mówi, ile struktury zostaje z samej treści i układu.
///
/// Każdy dokument jest parsowany trzy razy z tych samych bloków:
///   • ze stylami — wynik odniesienia,
///   • „bez stylów": bloki bez <c>StyleId</c> (układ i tabele zostają — DOCX bez szablonu),
///   • „sam tekst": także bez układu i bez miejsca w tabeli (jak TXT).
/// Porównywane są jednostki z eksportu schemaPL: element, eId, głębokość cytatu i umiejscowienie w jednostkach
/// systematyzacyjnych — ten sam klucz co w <c>--compare-reference</c>.
///
/// Wynik: raport wg rodzaju aktu, jednostki ginące i nadmiarowe wg elementu, dokumenty z największą różnicą,
/// <c>styleless-parity.tsv</c> (wiersz na dokument) oraz <c>styleless-parity-units.tsv</c> i
/// <c>styleless-parity-text-units.tsv</c> — każda jednostka, która bez stylów (albo w samym tekście) ginie („-") albo
/// dochodzi („+").
///
/// Użycie:
///   dotnet run --project tools/CorpusValidator -- --styleless-parity &lt;root&gt; &lt;katalog-wyjsciowy&gt;
/// </summary>
internal static class StylelessParityReport
{
    private sealed class Variant
    {
        public long Matched, Missing, Extra;
        public int IdenticalDocuments;
        public readonly Dictionary<string, int> MissingByElement = new(StringComparer.Ordinal);
        public readonly Dictionary<string, int> ExtraByElement = new(StringComparer.Ordinal);
    }

    private sealed class Tally
    {
        public int Documents;
        public long Units;
        public readonly Variant WithoutStyles = new();
        public readonly Variant TextOnly = new();
    }

    private sealed record Row(string File, string Kind, int Units, int MissingA, int ExtraA, int MissingB, int ExtraB);

    internal static int Run(string root, string outputDirectory)
    {
        if (!Directory.Exists(root))
        {
            Console.Error.WriteLine($"Brak katalogu korpusu: {root}");
            return 1;
        }

        Directory.CreateDirectory(outputDirectory);

        var byKind = new SortedDictionary<string, Tally>(StringComparer.Ordinal);
        var rows = new List<Row>();
        var failures = 0;

        using var units = new StreamWriter(Path.Combine(outputDirectory, "styleless-parity-units.tsv"), append: false, Encoding.UTF8);
        units.WriteLine("plik\trodzaj\tstrona\telement\tklucz");
        using var textUnits = new StreamWriter(Path.Combine(outputDirectory, "styleless-parity-text-units.tsv"), append: false, Encoding.UTF8);
        textUnits.WriteLine("plik\trodzaj\tstrona\telement\tklucz");

        foreach (var path in Directory.EnumerateFiles(root, "*.docx", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
        {
            try
            {
                IReadOnlyList<DocumentBlock> blocks;
                using (var stream = File.OpenRead(path))
                    blocks = DocumentBlockReaderFactory.Create(SourceFormat.Docx).ReadBlocks(stream);

                var styled = Export(blocks);
                var reference = Keys(styled);
                if (reference.Count == 0)
                    continue;

                var kind = styled.Descendants().FirstOrDefault(e => e.Name.LocalName == "Rodzaj")?.Value ?? "?";
                if (!byKind.TryGetValue(kind, out var tally))
                    byKind[kind] = tally = new Tally();
                tally.Documents++;
                tally.Units += reference.Count;

                var withoutStyles = Keys(Export(blocks.Select(block => block with { StyleId = null }).ToList()));
                var textOnly = Keys(Export(blocks.Select(block => block with { StyleId = null, Layout = null, TableCell = null }).ToList()));

                var file = Path.GetFileNameWithoutExtension(path);
                var (missingA, extraA) = Compare(reference, withoutStyles, tally.WithoutStyles,
                    (side, key) => units.WriteLine($"{file}\t{kind}\t{side}\t{key}"));
                var (missingB, extraB) = Compare(reference, textOnly, tally.TextOnly,
                    (side, key) => textUnits.WriteLine($"{file}\t{kind}\t{side}\t{key}"));
                rows.Add(new Row(file, kind, reference.Count, missingA, extraA, missingB, extraB));
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                failures++;
            }
        }

        WriteRows(Path.Combine(outputDirectory, "styleless-parity.tsv"), rows);
        var report = Report(root, byKind, rows, failures);
        File.WriteAllText(Path.Combine(outputDirectory, "styleless-parity.txt"), report);
        Console.WriteLine(report);
        return 0;
    }

    private static XElement Export(IReadOnlyList<DocumentBlock> blocks) =>
        LegalDocumentConverter.Convert(
            LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!);

    /// <summary>Klucze jednostek eksportu z nazwą elementu na początku — do zestawień wg elementu.</summary>
    private static List<string> Keys(XElement root) =>
        ReferenceStructureComparison.Collect(root).Select(unit => $"{unit.Name}\t{unit.Key}").ToList();

    /// <param name="report">Odbiera każdą różnicę: stronę („-" ginie, „+" dochodzi) i klucz jednostki.</param>
    private static (int Missing, int Extra) Compare(
        List<string> reference, List<string> actual, Variant variant, Action<char, string>? report)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var key in actual)
            counts[key] = counts.GetValueOrDefault(key) + 1;

        var missing = 0;
        foreach (var key in reference)
        {
            if (counts.TryGetValue(key, out var n) && n > 0)
            {
                counts[key] = n - 1;
                variant.Matched++;
                continue;
            }

            missing++;
            Bump(variant.MissingByElement, ElementOf(key));
            report?.Invoke('-', key);
        }

        var extra = 0;
        foreach (var (key, n) in counts)
        {
            if (n <= 0)
                continue;
            extra += n;
            Bump(variant.ExtraByElement, ElementOf(key), n);
            report?.Invoke('+', key);
        }

        variant.Missing += missing;
        variant.Extra += extra;
        if (missing == 0 && extra == 0)
            variant.IdenticalDocuments++;

        return (missing, extra);
    }

    private static string ElementOf(string key) => key[..key.IndexOf('\t')];

    private static void Bump(Dictionary<string, int> counts, string key, int by = 1) =>
        counts[key] = counts.GetValueOrDefault(key) + by;

    private static void WriteRows(string path, List<Row> rows)
    {
        using var writer = new StreamWriter(path, append: false, Encoding.UTF8);
        writer.WriteLine("plik\trodzaj\tjednostek\tbez_stylow_brak\tbez_stylow_nadmiar\tsam_tekst_brak\tsam_tekst_nadmiar");
        foreach (var row in rows)
            writer.WriteLine($"{row.File}\t{row.Kind}\t{row.Units}\t{row.MissingA}\t{row.ExtraA}\t{row.MissingB}\t{row.ExtraB}");
    }

    private static string Report(string root, SortedDictionary<string, Tally> byKind, List<Row> rows, int failures)
    {
        var sb = new StringBuilder();
        void W(string line = "") => sb.AppendLine(line);
        static string Percent(long part, long whole) =>
            whole == 0 ? "—" : (100.0 * part / whole).ToString("0.00", CultureInfo.InvariantCulture) + " %";

        W("================================================================");
        W(" ZGODNOŚĆ BEZ STYLÓW — STRUKTURA JEDNOSTEK");
        W("================================================================");
        W($"Korpus: {root}");
        W($"Dokumentów z jednostkami: {rows.Count}   nieprzetworzonych: {failures}");
        W("Odniesieniem jest wynik ze stylami. „Bez stylów\": bloki bez StyleId (DOCX bez szablonu);");
        W("„sam tekst\": także bez układu i bez miejsca w tabeli (jak TXT).");

        foreach (var (name, pick) in new (string, Func<Tally, Variant>)[]
                 {
                     ("BEZ STYLÓW", t => t.WithoutStyles),
                     ("SAM TEKST", t => t.TextOnly),
                 })
        {
            W();
            W($"--- {name} ---");
            W($"{"Rodzaj",-10} {"dok.",6} {"jednostek",10} {"zgodnych",10} {"brak",8} {"nadmiar",8} {"dok. w pełni zgodnych",22}");
            foreach (var (kind, tally) in byKind)
            {
                var v = pick(tally);
                W($"{kind,-10} {tally.Documents,6} {tally.Units,10} {Percent(v.Matched, tally.Units),10} {v.Missing,8} {v.Extra,8} {v.IdenticalDocuments,10} ({Percent(v.IdenticalDocuments, tally.Documents)})");
            }

            var missing = Sum(byKind.Values.Select(t => pick(t).MissingByElement));
            var extra = Sum(byKind.Values.Select(t => pick(t).ExtraByElement));
            W("  jednostki ginące wg elementu:      " + Top(missing));
            W("  jednostki nadmiarowe wg elementu:  " + Top(extra));
        }

        W();
        W("--- Dokumenty z największą różnicą bez stylów (20) ---");
        foreach (var row in rows.Where(r => r.MissingA + r.ExtraA > 0).OrderByDescending(r => r.MissingA + r.ExtraA).Take(20))
            W($"  {row.File,-20} {row.Kind,-8} jednostek {row.Units,6}: brak {row.MissingA,6}, nadmiar {row.ExtraA,6}   (sam tekst: brak {row.MissingB}, nadmiar {row.ExtraB})");

        return sb.ToString();
    }

    private static Dictionary<string, int> Sum(IEnumerable<Dictionary<string, int>> parts)
    {
        var total = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var part in parts)
            foreach (var (key, n) in part)
                total[key] = total.GetValueOrDefault(key) + n;
        return total;
    }

    private static string Top(Dictionary<string, int> counts) =>
        counts.Count == 0
            ? "(brak)"
            : string.Join(", ", counts.OrderByDescending(c => c.Value).Take(8).Select(c => $"{c.Key} {c.Value}"));
}
