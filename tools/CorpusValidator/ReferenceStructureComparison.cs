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
///   4. datę wejścia w życie i grupę czasową (<c>okres</c>) każdej jednostki z eId,
///   5. treść zdań — własny tekst każdego zdania, bez treści odnośników i cytatów, z podziałem różnic
///      na rodzaje (odstępy, myślniki, cudzysłowy, indeks górny, inne),
///   6. dane czasowe w metadanych — zdarzenia z podstawą i dowodem, jednostki wyjątków z zakresem,
///      grupy czasowe,
///   7. załączniki — każdy element załącznika z umiejscowieniem, scaleniami komórek i własnym tekstem.
///
/// Adresy porównywane są z prefiksem aktu („DU/2024/928__art_1", w cytacie prefiks aktu zmienianego).
/// Z porównania wyłączone są atrybuty GUID.
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

    // Jednostka podstawowa aktu wykonawczego („paragraf"), jednostki obwieszczenia i preambuła liczą się tak samo
    // jak jednostki ustawy; warianty spoza ZTP („nie-ztp-*") rozpoznaje przedrostek (IsEditorial).
    private static readonly HashSet<string> EditorialElements =
    [
        "artykul", "artykul-kodeksu", "paragraf", "ustep", "ustep-n", "paragraf-kodeksu", "paragraf-kodeksu-n",
        "punkt", "litera", "tiret", "podw-tiret", "potr-tiret", "poczw-tiret", "piec-tiret",
        "czesc-wspolna-punkt", "czesc-wspolna-litera", "czesc-wspolna-tiret", "czesc-wspolna-podw-tiret",
        "punkt-OTJ", "podpunkt-OTJ", "czesc-wspolna-podpunkt-OTJ", "preambula",
    ];

    private const string NonZtpPrefix = "nie-ztp-";

    private static bool IsEditorial(string name) =>
        EditorialElements.Contains(name) || name.StartsWith(NonZtpPrefix, StringComparison.Ordinal);

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    internal sealed record Unit(string Name, string EId, string Nr, string Heading, string Path, int QuoteDepth,
        string EntryDate = "", string Period = "")
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

        var referenceRoot = XDocument.Load(referencePath).Root!;
        var reference = Collect(referenceRoot);
        var actual = Collect(ours);

        var report = Report(docxPath, referencePath, reference, actual)
            + SentenceReport(Sentences(referenceRoot), Sentences(ours))
            + LifecycleReport(LifecycleLines(referenceRoot), LifecycleLines(ours))
            + AnnexReport(AnnexLines(referenceRoot), AnnexLines(ours));
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
                var isUnit = SystematizingElements.Contains(name) || IsEditorial(name)
                    || (name == "tytul" && child.Attribute("eId") != null);
                if (isUnit)
                {
                    var unitName = name == "tytul" ? "tytul-jednostka" : name;
                    units.Add(new Unit(unitName, EId(child), Nr(child), Heading(child), path, quoteDepth,
                        child.Attribute("wejscie-w-zycie")?.Value ?? "", child.Attribute("okres")?.Value ?? ""));
                    childPath = SystematizingElements.Contains(unitName) || unitName == "tytul-jednostka"
                        ? (path.Length == 0 ? unitName : $"{path}/{unitName}")
                        : path;
                }

                Walk(child, childPath, childDepth);
            }
        }
    }

    private static string EId(XElement element) => element.Attribute("eId")?.Value ?? "";

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
        W();

        // 4. Data wejścia w życie i grupa czasowa każdej jednostki z eId — ta sama klauzula ma dać te same okresy.
        static string Dated(Unit u) => $"{u.QuoteDepth}|{u.Name}|{u.EId}|{u.EntryDate}|{u.Period}";
        var refDated = reference.Where(u => u.EId.Length > 0).Select(Dated).ToList();
        var ourDated = actual.Where(u => u.EId.Length > 0).Select(Dated).ToList();
        W($"--- Daty i okresy jednostek (element, eId, wejscie-w-zycie, okres): zgodne {Matching(refDated, ourDated)} z {refDated.Count} ---");
        WriteDiff(W, refDated, ourDated, 25);

        return sb.ToString();
    }

    // ============================================================
    // 5. Treść zdań
    // ============================================================

    /// <summary>Elementy, których zawartość nie jest własnym tekstem zdania.</summary>
    private static readonly HashSet<string> ForeignToSentence =
    [
        "odnosnik", "odsylacz-do-odnosnika", "cytat-strukt", "cytat-w-tekscie", "cytat-blok",
    ];

    internal sealed record Sentence(string Key, string Text);

    /// <summary>
    /// Zdania spoza metadanych i odnośników, w kolejności dokumentu. Klucz: eId najbliższej jednostki,
    /// elementy między nią a zdaniem, numer zdania i fragmentu — bez niego ten sam tekst w dwóch
    /// jednostkach zgadzałby się na krzyż.
    /// </summary>
    internal static List<Sentence> Sentences(XElement root)
    {
        var sentences = new List<Sentence>();
        foreach (var sentence in root.Descendants().Where(e => e.Name.LocalName == "zdanie"))
        {
            var ancestors = sentence.Ancestors().ToList();
            if (ancestors.Any(a => a.Name.LocalName is "metadane" or "odnosnik"))
                continue;

            var owner = ancestors.FirstOrDefault(a => a.Attribute("eId") != null);
            var between = ancestors.TakeWhile(a => a != owner).Select(a => a.Name.LocalName).Reverse();
            var key = $"{(owner == null ? "" : EId(owner))}/{string.Join("/", between)}" +
                      $"#{sentence.Attribute("nr")?.Value}.{sentence.Attribute("fragment")?.Value}";
            sentences.Add(new Sentence(key, OwnText(sentence)));
        }

        return sentences;
    }

    // Instrukcje nowelizacyjne w zdaniu (przestrzeń „now") są w obu plikach zapisane inaczej — poza miarą tekstu.
    private static string OwnText(XElement element) => string.Concat(element.Nodes().Select(node => node switch
    {
        XText text => text.Value,
        XElement child when ForeignToSentence.Contains(child.Name.LocalName) => "",
        XElement child when child.Name.NamespaceName != element.Name.NamespaceName => "",
        XElement child => OwnText(child),
        _ => "",
    }));

    private static readonly (string Name, Func<string, string> Normalize)[] DifferenceKinds =
    [
        ("odstępy", text => Whitespace.Replace(text.Replace('\u00a0', ' '), " ").Trim()),
        ("myślnik", text => text.Replace('\u2013', '-').Replace('\u2212', '-')),
        ("cudzysłów", text => Regex.Replace(text, "[\u201e\u201d\u201c\"]", "\"")),
        ("indeks górny w nawiasie", text => Regex.Replace(text, @"\[([^\[\]]*)\]", "$1")),
    ];

    private static string SentenceReport(List<Sentence> reference, List<Sentence> actual)
    {
        var sb = new StringBuilder();
        void W(string line = "") => sb.AppendLine(line);

        var ours = actual.GroupBy(s => s.Key).ToDictionary(g => g.Key, g => new Queue<string>(g.Select(s => s.Text)));
        int exact = 0, missing = 0;
        var kinds = new Dictionary<string, int>();
        var samples = new List<string>();

        foreach (var sentence in reference)
        {
            if (!ours.TryGetValue(sentence.Key, out var candidates) || candidates.Count == 0)
            {
                missing++;
                continue;
            }

            var text = candidates.Dequeue();
            if (text == sentence.Text)
            {
                exact++;
                continue;
            }

            // Rodzaj różnicy: zestaw normalizacji, po którym teksty się zgadzają.
            string a = sentence.Text, b = text;
            var applied = new List<string>();
            foreach (var (name, normalize) in DifferenceKinds)
            {
                var (na, nb) = (normalize(a), normalize(b));
                if (na == a && nb == b)
                    continue;

                (a, b) = (na, nb);
                applied.Add(name);
                if (a == b)
                    break;
            }

            var kind = a == b ? string.Join(" + ", applied) : "inne";
            kinds[kind] = kinds.GetValueOrDefault(kind) + 1;
            if (samples.Count(s => s.StartsWith($"[{kind}]")) < 4)
                samples.Add($"[{kind}] {sentence.Key}\n      ref:   {Truncate(Diverging(sentence.Text, text), 150)}\n      nasze: {Truncate(Diverging(text, sentence.Text), 150)}");
        }

        W();
        W($"--- Treść zdań: referencja {reference.Count}, zgodne co do znaku {exact}, różne {kinds.Values.Sum()}, bez odpowiednika {missing} ---");
        foreach (var (kind, count) in kinds.OrderByDescending(k => k.Value))
            W($"  {count,5}  {kind}");
        foreach (var sample in samples)
            W($"  {sample}");

        return sb.ToString();
    }

    /// <summary>Fragment tekstu od miejsca pierwszej różnicy, z krótkim kontekstem przed nią.</summary>
    // ============================================================
    // 6. Dane czasowe w metadanych
    // ============================================================

    /// <summary>
    /// Elementy <c>DaneCzasowe</c> jako wiersze: zdarzenia z podstawą i dowodem, jednostki wyjątków
    /// z zakresem, grupy czasowe z przedziałami. Element podrzędny niesie w wierszu swojego właściciela,
    /// bo jednostka i przedział znaczą coś tylko w swoim zdarzeniu albo grupie.
    /// </summary>
    internal static List<string> LifecycleLines(XElement root)
    {
        var temporal = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "DaneCzasowe");
        if (temporal is null)
            return [];

        static string Label(XElement e) =>
            e.Attribute("id")?.Value ?? e.Attribute("eId")?.Value ?? e.Name.LocalName;

        return temporal.Descendants().Select(element =>
        {
            var owners = element.Ancestors().TakeWhile(a => a != temporal).Reverse().Select(Label);
            var attributes = element.Attributes()
                .Where(a => !a.IsNamespaceDeclaration && !a.Name.LocalName.Contains("GUID"))
                .Select(a => $"{a.Name.LocalName}=\"{a.Value}\"");
            return string.Join(" > ", owners.Append(element.Name.LocalName)) + " " + string.Join(" ", attributes);
        }).ToList();
    }

    private static string LifecycleReport(List<string> reference, List<string> actual)
    {
        var sb = new StringBuilder();
        void W(string line = "") => sb.AppendLine(line);

        W();
        W($"--- Dane czasowe (zdarzenia, jednostki wyjątków, grupy): referencja {reference.Count}, nasze {actual.Count}, zgodne {Matching(reference, actual)} ---");
        WriteDiff(W, reference, actual, 40);
        return sb.ToString();
    }

    // ============================================================
    // 7. Załączniki
    // ============================================================

    private static readonly HashSet<string> AnnexAttributes = ["eId", "brak-eid", "rowspan", "colspan", "naglowek", "orientacja"];

    /// <summary>
    /// Elementy załączników jako wiersze: załącznik, ścieżka elementów nad elementem, atrybuty znaczące
    /// (adres, scalenia, wiersz nagłówka) i tekst stojący wprost w elemencie — bez tekstu elementów podrzędnych,
    /// żeby jedna różnica w komórce nie pociągała za sobą wiersza, tabeli i załącznika.
    /// </summary>
    internal static List<string> AnnexLines(XElement root)
    {
        var lines = new List<string>();
        foreach (var annex in root.Descendants().Where(e => e.Name.LocalName == "zalacznik"))
        {
            var id = annex.Attribute("eId")?.Value ?? annex.Attribute("brak-eid")?.Value ?? "";
            foreach (var element in annex.DescendantsAndSelf())
            {
                var path = string.Join("/", element.AncestorsAndSelf().TakeWhile(a => a != annex.Parent).Reverse().Select(a => a.Name.LocalName));
                var attributes = string.Join(" ", element.Attributes()
                    .Where(a => AnnexAttributes.Contains(a.Name.LocalName))
                    .Select(a => $"{a.Name.LocalName}=\"{a.Value}\""));
                var text = string.Concat(element.Nodes().OfType<XText>().Select(node => node.Value));
                lines.Add($"{id} {path} {attributes} | {Normalize(text)}");
            }
        }

        return lines;
    }

    private static string AnnexReport(List<string> reference, List<string> actual)
    {
        var sb = new StringBuilder();
        void W(string line = "") => sb.AppendLine(line);

        W();
        W($"--- Załączniki (elementy z umiejscowieniem i tekstem): referencja {reference.Count}, nasze {actual.Count}, zgodne {Matching(reference, actual)} ---");
        WriteDiff(W, reference, actual, 40);
        return sb.ToString();
    }

    private static string Diverging(string text, string other)
    {
        var index = 0;
        while (index < text.Length && index < other.Length && text[index] == other[index])
            index++;

        var start = Math.Max(0, index - 30);
        return (start > 0 ? "…" : "") + text[start..];
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
