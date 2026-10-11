using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Saga.Core;
using Saga.Core.Ingest;
using Saga.Core.Services;

namespace CorpusValidator;

/// <summary>
/// Pokrycie tekstu: czy tekst każdego akapitu źródła doszedł do eksportu schemaPL. Miara strat —
/// akapitów, które potok pominął, zamiast je wbudować — liczona na wyjściu, nie na modelu.
///
/// Porównanie jest na samych literach i cyfrach: odstępy, interpunkcja, cudzysłowy i wielkość liter
/// nie mają znaczenia, bo eksport przenosi je do atrybutów albo dzieli tekst na zdania. Z akapitu
/// źródła zdejmowane są oznaczenie jednostki („Art. 5.", „2)", „Rozdział 3") — w eksporcie stoi w
/// <c>nr</c> — oraz znaczniki odnośników. Z eksportu pomijane są metadane, treść odnośników
/// (odnośnik rozcina zdanie, w którym stoi) i znaczniki odnośników pozostawione tekstem. Akapit krótszy niż <see cref="MinimumLength"/> znaków
/// nie jest mierzony: po zdjęciu oznaczenia zostaje z niego za mało, żeby trafienie coś znaczyło.
///
/// Wynik: raport z pokryciem wg rodzaju aktu i lista dokumentów z największą stratą oraz
/// <c>text-coverage-lost.tsv</c> z każdym niedoniesionym akapitem (plik, rodzaj, numer bloku, styl,
/// komórka tabeli, tekst).
///
/// Użycie:
///   dotnet run --project tools/CorpusValidator -- --text-coverage &lt;root&gt; &lt;katalog-wyjsciowy&gt;
/// </summary>
internal static class TextCoverageReport
{
    /// <summary>Najkrótszy mierzony akapit, w literach i cyfrach po zdjęciu oznaczenia.</summary>
    private const int MinimumLength = 12;

    // Oznaczenie na początku akapitu — w eksporcie stoi w „nr", nie w tekście. Bywa ich kilka pod rząd
    // („Art. 5. 1. Treść").
    private static readonly Regex Designation = new(
        @"^\s*[„""]?\s*(?:Art\.\s*\d+[a-zł]*(?:\[[^\]]*\])?\.?|§\s*\d+[a-zł]*(?:\[[^\]]*\])?\.?|\d+[a-zł]*(?:\[[^\]]*\])?[.)]|[a-zł]{1,3}\)|[IVXLCDM]+\.|[–-]+|(?:CZĘŚĆ|KSIĘGA|TYTUŁ|DZIAŁ|ROZDZIAŁ|ODDZIAŁ|Załącznik\s+nr)\s+\S+)\s*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex FootnoteMarker = new(@"\[[^\[\]]*\d+\)[^\[\]]*\]", RegexOptions.Compiled);

    private static readonly HashSet<string> OutsideTheText = ["metadane", "odnosnik", "odsylacz-do-odnosnika"];

    private sealed record Lost(string File, string Kind, int Block, string? Style, string Cell, string Text);

    private sealed class Tally
    {
        public int Documents, Measured, Found, LostBlocks;
        public long MeasuredChars, LostChars;
    }

    internal static int Run(string root, string outputDirectory)
    {
        if (!Directory.Exists(root))
        {
            Console.Error.WriteLine($"Brak katalogu korpusu: {root}");
            return 1;
        }

        Directory.CreateDirectory(outputDirectory);

        var byKind = new SortedDictionary<string, Tally>(StringComparer.Ordinal);
        var byDocument = new List<(string File, string Kind, int Measured, int LostBlocks, long LostChars)>();
        var lost = new List<Lost>();
        var failures = 0;

        foreach (var path in Directory.EnumerateFiles(root, "*.docx", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
        {
            IReadOnlyList<DocumentBlock> blocks;
            XElement xml;
            try
            {
                using (var stream = File.OpenRead(path))
                    blocks = DocumentBlockReaderFactory.Create(SourceFormat.Docx).ReadBlocks(stream);
                using (var stream = File.OpenRead(path))
                {
                    var parsed = LegalDocumentParser.Parse(stream, Path.GetFileName(path),
                        new ParseOptions { Policy = ParsePolicy.AlwaysParse });
                    xml = LegalDocumentConverter.Convert(parsed.Document!);
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                failures++;
                continue;
            }

            var file = Path.GetFileNameWithoutExtension(path);
            var kind = xml.Descendants().FirstOrDefault(e => e.Name.LocalName == "Rodzaj")?.Value ?? "?";
            var exported = ExportedText(xml);

            if (!byKind.TryGetValue(kind, out var tally))
                byKind[kind] = tally = new Tally();
            tally.Documents++;

            int measured = 0, lostBlocks = 0, cursor = 0;
            long lostChars = 0;
            for (var i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                if (block.Role != BlockRole.Body || block.IsEmpty)
                    continue;

                var needle = Needle(block.Text);
                if (needle.Length < MinimumLength)
                    continue;

                measured++;
                tally.Measured++;
                tally.MeasuredChars += needle.Length;

                // Bloki stoją w eksporcie zwykle w kolejności źródła — szukanie od ostatniego trafienia
                // jest szybkie, a dopiero potem w całości.
                var at = exported.IndexOf(needle, cursor, StringComparison.Ordinal);
                if (at < 0)
                    at = exported.IndexOf(needle, StringComparison.Ordinal);

                if (at >= 0)
                {
                    tally.Found++;
                    cursor = at;
                    continue;
                }

                lostBlocks++;
                lostChars += needle.Length;
                tally.LostBlocks++;
                tally.LostChars += needle.Length;

                var cell = block.TableCell is { } c ? $"t{c.Table}r{c.Row}c{c.Column}" : "";
                var text = Regex.Replace(block.Text.Trim(), @"\s+", " ");
                lost.Add(new Lost(file, kind, i, block.StyleId, cell, text.Length > 160 ? text[..160] : text));
            }

            byDocument.Add((file, kind, measured, lostBlocks, lostChars));
        }

        WriteLost(Path.Combine(outputDirectory, "text-coverage-lost.tsv"), lost);
        var report = Report(root, byKind, byDocument, lost, failures);
        File.WriteAllText(Path.Combine(outputDirectory, "text-coverage.txt"), report);
        Console.WriteLine(report);
        return 0;
    }

    /// <summary>Litery i cyfry akapitu źródła bez oznaczenia jednostki i znaczników odnośników.</summary>
    internal static string Needle(string blockText)
    {
        var text = FootnoteMarker.Replace(blockText, "");
        for (var i = 0; i < 3; i++)
        {
            var stripped = Designation.Replace(text, "", 1);
            if (stripped.Length == text.Length)
                break;
            text = stripped;
        }

        return LettersAndDigits(text);
    }

    /// <summary>Litery i cyfry treści aktu w kolejności dokumentu — bez metadanych i treści odnośników.</summary>
    internal static string ExportedText(XElement root)
    {
        var sb = new StringBuilder();
        Append(root, sb);
        return sb.ToString();

        static void Append(XElement element, StringBuilder sb)
        {
            foreach (var node in element.Nodes())
            {
                // Znacznik odnośnika bywa w eksporcie zwykłym tekstem (w cytacie należy do aktu zmienianego).
                if (node is XText text)
                    AppendLettersAndDigits(FootnoteMarker.Replace(text.Value, ""), sb);
                else if (node is XElement { Name.LocalName: "mroot" } root && root.Elements().Count() == 2)
                {
                    // Pierwiastek w MathML ma stopień za wyrażeniem; w tekście źródła (zapis liniowy) — przed nim.
                    Append(root.Elements().Last(), sb);
                    Append(root.Elements().First(), sb);
                }
                else if (node is XElement child && !OutsideTheText.Contains(child.Name.LocalName))
                    Append(child, sb);
            }
        }
    }

    private static string LettersAndDigits(string text)
    {
        var sb = new StringBuilder(text.Length);
        AppendLettersAndDigits(text, sb);
        return sb.ToString();
    }

    // Cudzysłów zamykający bywa zapisany literą modyfikującą (U+02EE) — to nadal cudzysłów, nie treść.
    private static void AppendLettersAndDigits(string text, StringBuilder sb)
    {
        foreach (var c in text)
            if (char.IsLetterOrDigit(c) && !QuotationMarks.Any.Contains(c))
                sb.Append(char.ToLowerInvariant(c));
    }

    private static void WriteLost(string path, List<Lost> lost)
    {
        using var writer = new StreamWriter(path, append: false, Encoding.UTF8);
        writer.WriteLine("plik\trodzaj\tblok\tstyl\tkomorka\ttekst");
        foreach (var item in lost)
            writer.WriteLine($"{item.File}\t{item.Kind}\t{item.Block}\t{item.Style}\t{item.Cell}\t{item.Text.Replace('\t', ' ')}");
    }

    private static string Report(
        string root,
        SortedDictionary<string, Tally> byKind,
        List<(string File, string Kind, int Measured, int LostBlocks, long LostChars)> byDocument,
        List<Lost> lost,
        int failures)
    {
        var sb = new StringBuilder();
        void W(string line = "") => sb.AppendLine(line);
        static string Percent(long part, long whole) =>
            whole == 0 ? "—" : (100.0 * part / whole).ToString("0.00", CultureInfo.InvariantCulture) + " %";

        W("================================================================");
        W(" POKRYCIE TEKSTU — AKAPITY ŹRÓDŁA W EKSPORCIE");
        W("================================================================");
        W($"Korpus: {root}");
        W($"Dokumentów: {byDocument.Count}   nieprzetworzonych: {failures}");
        W();
        W($"{"Rodzaj",-10} {"dok.",6} {"akapitów",10} {"zgubionych",11} {"w dok.",7} {"pokrycie akapitów",18} {"pokrycie znaków",16}");
        foreach (var (kind, t) in byKind)
        {
            var documentsWithLoss = byDocument.Count(d => d.Kind == kind && d.LostBlocks > 0);
            W($"{kind,-10} {t.Documents,6} {t.Measured,10} {t.LostBlocks,11} {documentsWithLoss,7} {Percent(t.Found, t.Measured),18} {Percent(t.MeasuredChars - t.LostChars, t.MeasuredChars),16}");
        }

        W();
        W("--- Zgubione akapity wg stylu (15 najczęstszych) ---");
        foreach (var group in lost.GroupBy(l => (l.Kind, Style: string.IsNullOrEmpty(l.Style) ? "(brak)" : l.Style, InTable: l.Cell.Length > 0))
                     .OrderByDescending(g => g.Count()).Take(15))
            W($"  {group.Count(),6}  {group.Key.Kind,-8} {(group.Key.InTable ? "tabela " : "       ")}{group.Key.Style}");

        W();
        W("--- Dokumenty z największą stratą (20) ---");
        foreach (var d in byDocument.Where(d => d.LostBlocks > 0).OrderByDescending(d => d.LostChars).Take(20))
            W($"  {d.File,-20} {d.Kind,-8} zgubionych {d.LostBlocks,5} z {d.Measured,5} akapitów, {d.LostChars,8} znaków");

        return sb.ToString();
    }
}
