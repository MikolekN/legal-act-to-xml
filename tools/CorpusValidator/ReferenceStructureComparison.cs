using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Saga.Core;
using Saga.Core.Services;

namespace CorpusValidator;

/// <summary>
/// Zestawia strukturę jednostek z naszego eksportu schemaPL ze strukturą z pliku referencyjnego
/// (przykłady autorów schematu: akn4pl/przyklady/*/schemaPL_no_ref.xml i źródłowy DOCX obok).
///
/// Obie strony spłaszcza do listy jednostek w kolejności dokumentu — element, eId, nr, tytuł
/// jednostki systematyzacyjnej, ścieżka jednostek nad nią, głębokość cytatu — i porównuje:
///   1. jednostki systematyzacyjne (z „-n") z umiejscowieniem,
///   2. eId jednostek redakcyjnych,
///   3. umiejscowienie artykułów w jednostkach systematyzacyjnych.
///
/// Z porównania wyłączone są rozjazdy spoza struktury jednostek: prefiks aktu w eId
/// („DU/2024/928__"), atrybuty GUID, okres, daty i treść zdań.
///
/// Użycie:
///   dotnet run --project tools/CorpusValidator -- --compare-reference &lt;plik.docx&gt; &lt;referencja.xml&gt; &lt;katalog-wyjsciowy&gt;
/// </summary>
internal static class ReferenceStructureComparison
{
    private static readonly HashSet<string> SystematizingElements =
    [
        "czesc", "czesc-n", "ksiega", "ksiega-n", "tytul-jednostka", "tytul-n-jednostka", "tytul-n",
        "dzial", "dzial-n", "rozdzial", "rozdzial-n", "oddzial", "oddzial-n",
    ];

    private static readonly HashSet<string> EditorialElements =
    [
        "artykul", "ustep", "ustep-n", "paragraf-kodeksu", "paragraf-kodeksu-n", "punkt", "litera",
        "tiret", "podw-tiret", "potr-tiret", "poczw-tiret", "piec-tiret",
    ];

    private static readonly Regex ActPrefix = new(@"^[A-Z]{2}/\d{4}/\d+__", RegexOptions.Compiled);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    internal sealed record Unit(string Name, string EId, string Nr, string Heading, string Path, int QuoteDepth)
    {
        public bool IsSystematizing => SystematizingElements.Contains(Name);
        public string Key => $"{QuoteDepth}|{Path}|{Name}|{EId}";
        public override string ToString() =>
            $"{new string('>', QuoteDepth)}{Path}/{Name}" + (EId.Length > 0 ? $" [{EId}]" : "")
            + (Nr.Length > 0 ? $" nr={Nr}" : "") + (Heading.Length > 0 ? $" „{Truncate(Heading, 60)}”" : "");
    }

    internal static int Run(string docxPath, string referencePath, string outputDirectory)
    {
        if (!File.Exists(docxPath) || !File.Exists(referencePath))
        {
            Console.Error.WriteLine($"Brak pliku: {(File.Exists(docxPath) ? referencePath : docxPath)}");
            return 1;
        }

        Directory.CreateDirectory(outputDirectory);

        XElement ours;
        using (var stream = File.OpenRead(docxPath))
        {
            var parsed = LegalDocumentParser.Parse(stream, Path.GetFileName(docxPath),
                new ParseOptions { Policy = ParsePolicy.AlwaysParse });
            ours = LegalDocumentConverter.Convert(parsed.Document!);
        }
        ours.Save(Path.Combine(outputDirectory, "ours.xml"));

        var reference = Collect(XDocument.Load(referencePath).Root!);
        var actual = Collect(ours);

        var report = Report(docxPath, referencePath, reference, actual);
        File.WriteAllText(Path.Combine(outputDirectory, "comparison.txt"), report);
        Console.WriteLine(report);
        return 0;
    }

    internal static List<Unit> Collect(XElement root)
    {
        var units = new List<Unit>();
        Walk(root, path: "", quoteDepth: 0);
        return units;

        void Walk(XElement element, string path, int quoteDepth)
        {
            foreach (var child in element.Elements())
            {
                var name = child.Name.LocalName;
                var childDepth = name == "cytat-strukt" ? quoteDepth + 1 : quoteDepth;
                var childPath = name == "cytat-strukt" ? "" : path;

                // „tytul" bez eId to tytuł aktu, nie jednostka; tytuł-jednostka pod księgą ma eId.
                var isUnit = SystematizingElements.Contains(name) || EditorialElements.Contains(name)
                    || (name == "tytul" && child.Attribute("eId") != null);
                if (isUnit)
                {
                    var unitName = name == "tytul" ? "tytul-jednostka" : name;
                    units.Add(new Unit(unitName, EId(child), Nr(child), Heading(child), path, quoteDepth));
                    childPath = SystematizingElements.Contains(unitName) || unitName == "tytul-jednostka"
                        ? (path.Length == 0 ? unitName : $"{path}/{unitName}")
                        : path;
                }

                Walk(child, childPath, childDepth);
            }
        }
    }

    private static string EId(XElement element) =>
        ActPrefix.Replace(element.Attribute("eId")?.Value ?? "", "");

    private static string Nr(XElement element)
    {
        var nr = element.Element(element.Name.Namespace + "nr")
                 ?? element.Elements().FirstOrDefault(e => e.Name.LocalName.StartsWith("naglowek-"))
                     ?.Element(element.Name.Namespace + "nr");
        return nr == null ? "" : Normalize(string.Concat(nr.Nodes().OfType<XText>().Select(t => t.Value)));
    }

    private static string Heading(XElement element)
    {
        var header = element.Elements().FirstOrDefault(e => e.Name.LocalName.StartsWith("naglowek-"));
        var p = header?.Elements().FirstOrDefault(e => e.Name.LocalName == "p");
        return p == null ? "" : Normalize(p.Value);
    }

    private static string Normalize(string text) => Whitespace.Replace(text, " ").Trim();

    private static string Report(string docxPath, string referencePath, List<Unit> reference, List<Unit> actual)
    {
        var sb = new StringBuilder();
        void W(string line = "") => sb.AppendLine(line);

        W("================================================================");
        W(" STRUKTURA JEDNOSTEK — ZESTAWIENIE Z PLIKIEM REFERENCYJNYM");
        W("================================================================");
        W($"Źródło:     {docxPath}");
        W($"Referencja: {referencePath}");
        W();

        // 1. Jednostki systematyzacyjne: pełny opis (ścieżka, element, eId, nr, tytuł) w kolejności.
        var refSys = reference.Where(u => u.IsSystematizing).Select(u => u.ToString()).ToList();
        var ourSys = actual.Where(u => u.IsSystematizing).Select(u => u.ToString()).ToList();
        W($"--- Jednostki systematyzacyjne: referencja {refSys.Count}, nasze {ourSys.Count}, zgodne {Matching(refSys, ourSys)} ---");
        WriteDiff(W, refSys, ourSys, 40);
        W();

        // 2. Jednostki redakcyjne po kluczu (głębokość cytatu + ścieżka + element + eId).
        var refEd = reference.Where(u => !u.IsSystematizing).Select(u => u.Key).ToList();
        var ourEd = actual.Where(u => !u.IsSystematizing).Select(u => u.Key).ToList();
        W($"--- Jednostki redakcyjne (z umiejscowieniem): referencja {refEd.Count}, nasze {ourEd.Count}, zgodne {Matching(refEd, ourEd)} ---");
        WriteDiff(W, refEd, ourEd, 40);
        W();

        // 3. Te same jednostki bez umiejscowienia — oddziela błąd eId od błędu drzewa.
        static string Flat(Unit u) => $"{u.QuoteDepth}|{u.Name}|{u.EId}|{u.Nr}";
        var refFlat = reference.Where(u => !u.IsSystematizing).Select(Flat).ToList();
        var ourFlat = actual.Where(u => !u.IsSystematizing).Select(Flat).ToList();
        W($"--- Jednostki redakcyjne (element, eId, nr; bez umiejscowienia): zgodne {Matching(refFlat, ourFlat)} z {refFlat.Count} ---");
        WriteDiff(W, refFlat, ourFlat, 25);

        return sb.ToString();
    }

    private static int Matching(List<string> a, List<string> b)
    {
        var counts = b.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        var matched = 0;
        foreach (var item in a)
        {
            if (counts.TryGetValue(item, out var n) && n > 0)
            {
                counts[item] = n - 1;
                matched++;
            }
        }
        return matched;
    }

    private static void WriteDiff(Action<string> write, List<string> reference, List<string> actual, int limit)
    {
        var onlyReference = Subtract(reference, actual);
        var onlyOurs = Subtract(actual, reference);
        if (onlyReference.Count == 0 && onlyOurs.Count == 0)
        {
            write("  (zgodne)");
            return;
        }

        write($"  tylko w referencji ({onlyReference.Count}):");
        foreach (var item in onlyReference.Take(limit))
            write($"    - {item}");
        write($"  tylko u nas ({onlyOurs.Count}):");
        foreach (var item in onlyOurs.Take(limit))
            write($"    + {item}");
    }

    private static List<string> Subtract(List<string> from, List<string> what)
    {
        var counts = what.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        var rest = new List<string>();
        foreach (var item in from)
        {
            if (counts.TryGetValue(item, out var n) && n > 0)
                counts[item] = n - 1;
            else
                rest.Add(item);
        }
        return rest;
    }

    private static string Truncate(string text, int max) => text.Length > max ? text[..max] + "…" : text;
}
