using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Saga.Core;
using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Core.Services.Converters;

namespace CorpusValidator;

/// <summary>
/// Zgodność formatów: czy PDF i TXT tego samego aktu dają to, co DOCX. Korpus jest w DOCX, więc pliki PDF i TXT
/// powstają z niego konwersją (np. LibreOffice headless) i leżą w osobnym drzewie pod tą samą ścieżką względną,
/// z rozszerzeniem <c>.pdf</c> albo <c>.txt</c>. Lokalnie: <c>documents-pdf/</c> i <c>documents-txt/</c> (gitignorowane),
/// z LibreOffice wywołanego dla każdego katalogu korpusu:
/// <c>soffice --headless --convert-to pdf --outdir &lt;katalog&gt; *.docx</c> oraz
/// <c>--convert-to "txt:Text (encoded):UTF8"</c> (jeden akapit w wierszu, bez nagłówków stron i bez treści odnośników).
///
/// Dla każdej pary powstają trzy eksporty schemaPL:
///   • odniesienie — DOCX ze stylami,
///   • „sam tekst" — bloki DOCX bez stylu, układu i miejsca w tabeli: tyle struktury, ile daje sama treść przy
///     idealnym podziale na akapity (to samo co wariant „sam tekst" w <c>--styleless-parity</c>),
///   • plik przekonwertowany, czytany adapterem swojego formatu.
/// Różnica wobec odniesienia to cała strata formatu; różnica wobec „samego tekstu" — strata adaptera (podział na
/// akapity, artefakty stron, utracone znaki), bo klasyfikacja bez stylów jest w obu ta sama.
///
/// Miary: jednostki (element, eId, głębokość cytatu, umiejscowienie — klucz z <c>--compare-reference</c>);
/// pokrycie słów treści (bez metadanych i odnośników) i osobno słów treści odnośników; pola metadanych;
/// zgodność ze schematem całego dokumentu i samej treści aktu, bez nagłówka (gdy schemat jest lokalnie dostępny) —
/// nagłówek ma osobne braki: danych ogłoszenia nie ma w TXT, a pełna walidacja zatrzymuje się na nich.
///
/// Wynik: <c>format-parity.txt</c> (wg formatu i rodzaju aktu), <c>format-parity.tsv</c> (wiersz na plik),
/// <c>format-parity-units.tsv</c> (każda jednostka ginąca „-" albo nadmiarowa „+" wobec odniesienia) oraz eksporty
/// plików przekonwertowanych w <c>xml/&lt;format&gt;/</c>.
///
/// Użycie:
///   dotnet run --project tools/CorpusValidator -- --format-parity &lt;korpus-docx&gt; &lt;korpus-przekonwertowany&gt; &lt;katalog-wyjsciowy&gt;
/// </summary>
internal static class FormatParityReport
{
    private static readonly string[] Extensions = [".pdf", ".txt"];

    private static readonly Regex Word = new(@"[\p{L}\p{N}]+", RegexOptions.Compiled);

    /// <summary>Pola metadanych, których nie porównujemy: identyfikator wersji jest losowy.</summary>
    private static readonly HashSet<string> VolatileMetadata = ["GUIDwersji"];

    private sealed class Tally
    {
        public int Documents, Failed, Identical, MetadataIdentical;
        public int SchemaChecked, SchemaValid, ReferenceSchemaValid, BodyValid, ReferenceBodyValid;
        public long Units, Matched, TextOnlyUnits, MatchedTextOnly;
        public long Words, WordsFound, FootnoteWords, FootnoteWordsFound;
        public readonly Dictionary<string, int> MissingByElement = new(StringComparer.Ordinal);
        public readonly Dictionary<string, int> ExtraByElement = new(StringComparer.Ordinal);
        public readonly Dictionary<string, int> MetadataMismatch = new(StringComparer.Ordinal);
        public readonly Dictionary<string, int> Errors = new(StringComparer.Ordinal);
    }

    private sealed record Result(
        string File, string Format, string Kind, int Units, int TextOnlyUnits,
        List<string> Missing, List<string> Extra, int MatchedTextOnly,
        long Words, long WordsFound, long FootnoteWords, long FootnoteWordsFound,
        List<string> MetadataMismatch, Validity? Schema, Validity? ReferenceSchema, string? Error);

    /// <summary>Zgodność ze schematem całego dokumentu i samej treści aktu.</summary>
    private sealed record Validity(bool Document, bool Body);

    internal static int Run(string docxRoot, string convertedRoot, string outputDirectory)
    {
        foreach (var root in new[] { docxRoot, convertedRoot })
        {
            if (!Directory.Exists(root))
            {
                Console.Error.WriteLine($"Brak katalogu: {root}");
                return 1;
            }
        }

        Directory.CreateDirectory(outputDirectory);
        var validator = SchemaPlValidator.TryLoad(Directory.GetCurrentDirectory(), out var schemaSkipReason);

        var pairs = (
            from docx in EntryIntoForceReport.EnumerateDistinct(docxRoot)
            let relative = Path.GetRelativePath(docxRoot, docx)
            from extension in Extensions
            let converted = Path.Combine(convertedRoot, Path.ChangeExtension(relative, extension))
            where File.Exists(converted)
            select (Docx: docx, Converted: converted)).ToList();

        var done = 0;
        var results = pairs
            .AsParallel().AsOrdered()
            .WithDegreeOfParallelism(Math.Max(1, Environment.ProcessorCount / 2))
            .Select(pair =>
            {
                var result = Compare(pair.Docx, pair.Converted, validator, outputDirectory);
                var n = Interlocked.Increment(ref done);
                if (n % 100 == 0)
                    Console.Error.WriteLine($"  ...{n}/{pairs.Count}");
                return result;
            })
            .Where(result => result is not null)
            .Select(result => result!)
            .ToList();

        var byFormat = new SortedDictionary<string, SortedDictionary<string, Tally>>(StringComparer.Ordinal);
        using (var units = new StreamWriter(Path.Combine(outputDirectory, "format-parity-units.tsv"), append: false, Encoding.UTF8))
        {
            units.WriteLine("plik\tformat\trodzaj\tstrona\telement\tklucz");
            foreach (var r in results)
            {
                if (!byFormat.TryGetValue(r.Format, out var kinds))
                    byFormat[r.Format] = kinds = new SortedDictionary<string, Tally>(StringComparer.Ordinal);
                if (!kinds.TryGetValue(r.Kind, out var tally))
                    kinds[r.Kind] = tally = new Tally();
                Add(tally, r);

                foreach (var key in r.Missing)
                    units.WriteLine($"{r.File}\t{r.Format}\t{r.Kind}\t-\t{key}");
                foreach (var key in r.Extra)
                    units.WriteLine($"{r.File}\t{r.Format}\t{r.Kind}\t+\t{key}");
            }
        }

        WriteRows(Path.Combine(outputDirectory, "format-parity.tsv"), results);
        var report = Report(docxRoot, convertedRoot, byFormat, results, validator is null ? schemaSkipReason : null);
        File.WriteAllText(Path.Combine(outputDirectory, "format-parity.txt"), report);
        Console.WriteLine(report);
        return 0;
    }

    private static Result? Compare(string docxPath, string convertedPath, SchemaPlValidator? validator, string outputDirectory)
    {
        var file = Path.GetFileNameWithoutExtension(docxPath);
        var format = Path.GetExtension(convertedPath).TrimStart('.').ToLowerInvariant();

        XElement styled, textOnly;
        try
        {
            IReadOnlyList<DocumentBlock> blocks;
            using (var stream = File.OpenRead(docxPath))
                blocks = DocumentBlockReaderFactory.Create(SourceFormat.Docx).ReadBlocks(stream);
            styled = Export(LegalDocumentParser.Parse(blocks, Options));
            textOnly = Export(LegalDocumentParser.Parse(
                blocks.Select(block => block with { StyleId = null, Layout = null, TableCell = null }).ToList(), Options));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Odniesienie się nie buduje — para nie mówi nic o formacie.
            return null;
        }

        var reference = Keys(styled);
        if (reference.Count == 0)
            return null;

        var kind = styled.Descendants().FirstOrDefault(e => e.Name.LocalName == "Rodzaj")?.Value ?? "?";
        var (words, footnoteWords) = Words(styled);
        var referenceValid = Validate(validator, styled);

        XElement converted;
        try
        {
            using var stream = File.OpenRead(convertedPath);
            var parsed = LegalDocumentParser.Parse(stream, Path.GetFileName(convertedPath), Options);
            if (parsed.Document is null)
                return Failed("brak modelu");
            converted = LegalDocumentConverter.Convert(parsed.Document);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return Failed(ex.GetType().Name);
        }

        var exportDirectory = Path.Combine(outputDirectory, "xml", format);
        Directory.CreateDirectory(exportDirectory);
        converted.Save(Path.Combine(exportDirectory, file + ".xml"));

        var actual = Keys(converted);
        var (missing, extra) = Difference(reference, actual);
        var textOnlyKeys = Keys(textOnly);
        var (missingFromTextOnly, _) = Difference(textOnlyKeys, actual);
        var (convertedWords, convertedFootnoteWords) = Words(converted);

        return new Result(file, format, kind, reference.Count, textOnlyKeys.Count, missing, extra,
            textOnlyKeys.Count - missingFromTextOnly.Count,
            words.Values.Sum(), Found(words, convertedWords),
            footnoteWords.Values.Sum(), Found(footnoteWords, convertedFootnoteWords),
            MetadataMismatch(styled, converted),
            Validate(validator, converted), referenceValid, Error: null);

        Result Failed(string error) => new(file, format, kind, reference.Count, 0, [], [], 0,
            words.Values.Sum(), 0, footnoteWords.Values.Sum(), 0, [], null, referenceValid, error);
    }

    private static readonly ParseOptions Options = new() { Policy = ParsePolicy.AlwaysParse };

    private static Validity? Validate(SchemaPlValidator? validator, XElement root) =>
        validator is null
            ? null
            : new Validity(validator.Validate(root).Count == 0, validator.ValidateActBody(root.Elements().First()).Count == 0);

    private static XElement Export(ParseResult result) => LegalDocumentConverter.Convert(result.Document!);

    /// <summary>
    /// Klucze jednostek bez prefiksu własnego aktu. Prefiks eId pochodzi z danych ogłoszenia (nagłówek strony
    /// DOCX), których plik przekonwertowany może nie mieć — ich brak liczą pola metadanych, a struktura ma być
    /// porównana sama. Prefiksy aktów zmienianych (cele i jednostki cytowane) zostają: pochodzą z treści.
    /// </summary>
    private static List<string> Keys(XElement root)
    {
        var ownActs = root.Descendants()
            .Where(e => e.Name.LocalName == "ELI" && e.Ancestors().Any(a => a.Name.LocalName == "metadane"))
            .Select(e => e.Value.Trim() + "__")
            .Where(prefix => prefix.Length > 2)
            .Distinct()
            .ToList();

        return ReferenceStructureComparison.Collect(root)
            .Select(unit => ownActs.Aggregate($"{unit.Name}\t{unit.Key}", (key, prefix) => key.Replace(prefix, "", StringComparison.Ordinal)))
            .ToList();
    }

    /// <summary>Jednostki odniesienia, których brak w wyniku, i jednostki wyniku spoza odniesienia (z krotnością).</summary>
    private static (List<string> Missing, List<string> Extra) Difference(List<string> reference, List<string> actual)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var key in actual)
            counts[key] = counts.GetValueOrDefault(key) + 1;

        var missing = new List<string>();
        foreach (var key in reference)
        {
            if (counts.TryGetValue(key, out var n) && n > 0)
                counts[key] = n - 1;
            else
                missing.Add(key);
        }

        var extra = counts.Where(c => c.Value > 0).SelectMany(c => Enumerable.Repeat(c.Key, c.Value)).ToList();
        return (missing, extra);
    }

    /// <summary>
    /// Słowa (litery i cyfry, małymi literami) tekstu treści i osobno tekstu odnośników. Metadane są poza
    /// miarą — porównuje je <see cref="MetadataMismatch"/>. Atrybuty też: oznaczenia jednostek
    /// porównują klucze jednostek.
    /// </summary>
    private static (Dictionary<string, int> Body, Dictionary<string, int> Footnotes) Words(XElement root)
    {
        var body = new Dictionary<string, int>(StringComparer.Ordinal);
        var footnotes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var text in root.DescendantNodes().OfType<XText>())
        {
            var ancestors = text.Ancestors().Select(a => a.Name.LocalName).ToList();
            if (ancestors.Contains("metadane"))
                continue;
            var target = ancestors.Contains("odnosnik") ? footnotes : body;
            foreach (Match word in Word.Matches(text.Value))
            {
                var key = word.Value.ToLowerInvariant();
                target[key] = target.GetValueOrDefault(key) + 1;
            }
        }

        return (body, footnotes);
    }

    private static long Found(Dictionary<string, int> reference, Dictionary<string, int> actual) =>
        reference.Sum(w => (long)Math.Min(w.Value, actual.GetValueOrDefault(w.Key)));

    /// <summary>
    /// Pola metadanych z różną wartością: liście <c>metadane</c> (nazwa → wartości w kolejności dokumentu)
    /// oraz zdarzenia cyklu życia jako zbiór „rodzaj data".
    /// </summary>
    private static List<string> MetadataMismatch(XElement reference, XElement actual)
    {
        var expected = Metadata(reference);
        var found = Metadata(actual);
        return expected.Keys.Union(found.Keys)
            .Where(field => expected.GetValueOrDefault(field) != found.GetValueOrDefault(field))
            .OrderBy(field => field, StringComparer.Ordinal)
            .ToList();
    }

    private static Dictionary<string, string> Metadata(XElement root)
    {
        var fields = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var metadata in root.Descendants().Where(e => e.Name.LocalName == "metadane"))
        {
            foreach (var leaf in metadata.Descendants().Where(e => !e.HasElements && !VolatileMetadata.Contains(e.Name.LocalName)))
            {
                var value = Regex.Replace(leaf.Value, @"\s+", " ").Trim();
                if (leaf.Name.LocalName == "Zdarzenie")
                    value = $"{leaf.Attribute("rodzaj")?.Value} {leaf.Attribute("data")?.Value}";
                if (value.Length == 0)
                    continue;
                var name = leaf.Name.LocalName == "Zdarzenie" ? "Zdarzenia" : leaf.Name.LocalName;
                if (!fields.TryGetValue(name, out var values))
                    fields[name] = values = [];
                values.Add(value);
            }
        }

        // Zdarzenia porównujemy jako zbiór — kolejność ich wypisania nie jest treścią.
        return fields.ToDictionary(
            f => f.Key,
            f => string.Join(" | ", f.Key == "Zdarzenia" ? f.Value.Distinct().Order(StringComparer.Ordinal) : f.Value),
            StringComparer.Ordinal);
    }

    private static void Add(Tally tally, Result r)
    {
        tally.Documents++;
        tally.Units += r.Units;
        tally.Words += r.Words;
        tally.FootnoteWords += r.FootnoteWords;
        if (r.ReferenceSchema?.Document == true)
            tally.ReferenceSchemaValid++;
        if (r.ReferenceSchema?.Body == true)
            tally.ReferenceBodyValid++;

        if (r.Error is not null)
        {
            tally.Failed++;
            Bump(tally.Errors, r.Error);
            Bump(tally.MissingByElement, "(dokument nieprzetworzony)", r.Units);
            return;
        }

        tally.Matched += r.Units - r.Missing.Count;
        tally.TextOnlyUnits += r.TextOnlyUnits;
        tally.MatchedTextOnly += r.MatchedTextOnly;
        tally.WordsFound += r.WordsFound;
        tally.FootnoteWordsFound += r.FootnoteWordsFound;
        if (r.Missing.Count == 0 && r.Extra.Count == 0)
            tally.Identical++;
        if (r.MetadataMismatch.Count == 0)
            tally.MetadataIdentical++;
        foreach (var field in r.MetadataMismatch)
            Bump(tally.MetadataMismatch, field);
        foreach (var key in r.Missing)
            Bump(tally.MissingByElement, ElementOf(key));
        foreach (var key in r.Extra)
            Bump(tally.ExtraByElement, ElementOf(key));
        if (r.Schema is { } validity)
        {
            tally.SchemaChecked++;
            if (validity.Document)
                tally.SchemaValid++;
            if (validity.Body)
                tally.BodyValid++;
        }
    }

    private static string ElementOf(string key) => key[..key.IndexOf('\t')];

    private static void Bump(Dictionary<string, int> counts, string key, int by = 1) =>
        counts[key] = counts.GetValueOrDefault(key) + by;

    private static void WriteRows(string path, List<Result> results)
    {
        using var writer = new StreamWriter(path, append: false, Encoding.UTF8);
        writer.WriteLine("plik\tformat\trodzaj\tjednostek\tbrak\tnadmiar\tzgodnych_z_samym_tekstem\tjednostek_samego_tekstu"
            + "\tslowa\tslowa_znalezione\tslowa_odnosnikow\tslowa_odnosnikow_znalezione\tmetadane_rozne"
            + "\tschemat\tschemat_docx\ttresc_wg_schematu\ttresc_wg_schematu_docx\tblad");
        foreach (var r in results)
            writer.WriteLine($"{r.File}\t{r.Format}\t{r.Kind}\t{r.Units}\t{r.Missing.Count}\t{r.Extra.Count}\t{r.MatchedTextOnly}\t{r.TextOnlyUnits}"
                + $"\t{r.Words}\t{r.WordsFound}\t{r.FootnoteWords}\t{r.FootnoteWordsFound}\t{string.Join(",", r.MetadataMismatch)}"
                + $"\t{Flag(r.Schema?.Document)}\t{Flag(r.ReferenceSchema?.Document)}\t{Flag(r.Schema?.Body)}\t{Flag(r.ReferenceSchema?.Body)}\t{r.Error}");

        static string Flag(bool? value) => value switch { true => "tak", false => "nie", null => "" };
    }

    private static string Report(
        string docxRoot, string convertedRoot,
        SortedDictionary<string, SortedDictionary<string, Tally>> byFormat, List<Result> results, string? schemaSkipReason)
    {
        var sb = new StringBuilder();
        void W(string line = "") => sb.AppendLine(line);
        static string Percent(long part, long whole) =>
            whole == 0 ? "—" : (100.0 * part / whole).ToString("0.00", CultureInfo.InvariantCulture) + " %";

        W("================================================================");
        W(" ZGODNOŚĆ FORMATÓW — PDF I TXT WOBEC DOCX");
        W("================================================================");
        W($"Korpus DOCX: {docxRoot}");
        W($"Pliki przekonwertowane: {convertedRoot}");
        W("Odniesieniem jest eksport DOCX ze stylami. „Sam tekst DOCX\": bloki DOCX bez stylu, układu i tabel —");
        W("struktura z samej treści przy idealnym podziale na akapity; różnica wobec niego to strata adaptera formatu.");
        W("Schemat: zgodnych / sprawdzonych (w nawiasie: zgodnych eksportów DOCX tych samych aktów).");
        if (schemaSkipReason is not null)
            W($"Walidacja schematu pominięta: {schemaSkipReason}");

        foreach (var (format, kinds) in byFormat)
        {
            var all = new Tally();
            W();
            W($"--- {format.ToUpperInvariant()} ---");
            W($"{"Rodzaj",-10} {"dok.",5} {"błąd",5} {"jednostek",9} {"zgodnych",9} {"w pełni",8} {"wobec samego tekstu",20} {"słowa treści",13} {"słowa odnośn.",13} {"metadane",9} {"schemat",16} {"treść wg schematu",18}");
            foreach (var (kind, t) in kinds)
            {
                W(Line(kind, t));
                Merge(all, t);
            }

            W(Line("RAZEM", all));
            W("  jednostki ginące wg elementu:      " + Top(all.MissingByElement));
            W("  jednostki nadmiarowe wg elementu:  " + Top(all.ExtraByElement));
            W("  pola metadanych różne (dokumentów): " + Top(all.MetadataMismatch, 12));
            if (all.Errors.Count > 0)
                W("  nieprzetworzone:                   " + Top(all.Errors));

            W();
            W($"  Dokumenty {format} z największą różnicą jednostek (15):");
            foreach (var r in results.Where(r => r.Format == format && r.Error is null && r.Missing.Count + r.Extra.Count > 0)
                         .OrderByDescending(r => r.Missing.Count + r.Extra.Count).Take(15))
                W($"    {r.File,-20} {r.Kind,-8} jednostek {r.Units,6}: brak {r.Missing.Count,5}, nadmiar {r.Extra.Count,5}, słowa {Percent(r.WordsFound, r.Words)}");
        }

        return sb.ToString();

        string Line(string kind, Tally t) =>
            $"{kind,-10} {t.Documents,5} {t.Failed,5} {t.Units,9} {Percent(t.Matched, t.Units),9} {t.Identical,8} {Percent(t.MatchedTextOnly, t.TextOnlyUnits),20}"
            + $" {Percent(t.WordsFound, t.Words),13} {Percent(t.FootnoteWordsFound, t.FootnoteWords),13} {t.MetadataIdentical,9}"
            + $" {(t.SchemaChecked == 0 ? "—" : $"{t.SchemaValid}/{t.SchemaChecked} ({t.ReferenceSchemaValid})"),16}"
            + $" {(t.SchemaChecked == 0 ? "—" : $"{t.BodyValid}/{t.SchemaChecked} ({t.ReferenceBodyValid})"),18}";
    }

    private static void Merge(Tally into, Tally from)
    {
        into.Documents += from.Documents;
        into.Failed += from.Failed;
        into.Identical += from.Identical;
        into.MetadataIdentical += from.MetadataIdentical;
        into.SchemaChecked += from.SchemaChecked;
        into.SchemaValid += from.SchemaValid;
        into.ReferenceSchemaValid += from.ReferenceSchemaValid;
        into.BodyValid += from.BodyValid;
        into.ReferenceBodyValid += from.ReferenceBodyValid;
        into.Units += from.Units;
        into.Matched += from.Matched;
        into.TextOnlyUnits += from.TextOnlyUnits;
        into.MatchedTextOnly += from.MatchedTextOnly;
        into.Words += from.Words;
        into.WordsFound += from.WordsFound;
        into.FootnoteWords += from.FootnoteWords;
        into.FootnoteWordsFound += from.FootnoteWordsFound;
        foreach (var (target, source) in new[]
                 {
                     (into.MissingByElement, from.MissingByElement), (into.ExtraByElement, from.ExtraByElement),
                     (into.MetadataMismatch, from.MetadataMismatch), (into.Errors, from.Errors),
                 })
            foreach (var (key, n) in source)
                Bump(target, key, n);
    }

    private static string Top(Dictionary<string, int> counts, int take = 8) =>
        counts.Count == 0
            ? "(brak)"
            : string.Join(", ", counts.OrderByDescending(c => c.Value).Take(take).Select(c => $"{c.Key} {c.Value}"));
}
