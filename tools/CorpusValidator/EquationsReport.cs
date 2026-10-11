using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Saga.Core;
using Saga.Core.Helpers;
using Saga.Core.Services.Converters;

namespace CorpusValidator;

/// <summary>
/// Równania Worda w korpusie: ile ich jest, ile stoi w osobnym akapicie (wzór), a ile w zdaniu (symbol),
/// czy konwersja do MathML (<see cref="OfficeMathMl"/>) daje wyrażenie zgodne ze schematem i których
/// konstrukcji OMML konwerter nie odwzorowuje.
///
/// Wynik: raport oraz <c>equations-invalid.tsv</c> (plik, zapis liniowy, komunikat walidatora).
///
/// Użycie:
///   dotnet run --project tools/CorpusValidator -- --equations &lt;root&gt; &lt;katalog-wyjsciowy&gt;
/// </summary>
internal static class EquationsReport
{
    private const string MathNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/math";

    // Elementy OMML, które konwerter odwzorowuje albo świadomie pomija (właściwości, wartości właściwości).
    private static readonly HashSet<string> Handled =
    [
        "oMath", "r", "t", "e", "num", "den", "sub", "sup", "deg", "lim", "fName", "sSub", "sSup", "sSubSup", "sPre",
        "f", "d", "nary", "rad", "func", "limLow", "limUpp", "acc", "bar", "groupChr", "m", "mr", "eqArr", "box",
        "borderBox", "phant",
    ];

    internal static int Run(string root, string outputDirectory)
    {
        if (!Directory.Exists(root))
        {
            Console.Error.WriteLine($"Brak katalogu korpusu: {root}");
            return 1;
        }

        Directory.CreateDirectory(outputDirectory);
        var validator = SchemaPlValidator.TryLoad(Directory.GetCurrentDirectory(), out var reason);

        int documents = 0, withEquations = 0, equations = 0, display = 0, invalid = 0;
        var unmapped = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var invalidShapes = new Dictionary<string, int>(StringComparer.Ordinal);
        var perDocument = new List<(string File, int Count)>();
        var rows = new StringBuilder("plik\tzapis liniowy\tkomunikat\n");

        foreach (var path in Directory.EnumerateFiles(root, "*.docx", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
        {
            documents++;
            using var package = WordprocessingDocument.Open(path, false);
            var body = package.MainDocumentPart?.Document?.Body;
            if (body is null)
                continue;

            var found = body.Descendants()
                .Where(element => OfficeMathText.IsMath(element) && !element.Ancestors().Any(OfficeMathText.IsMath))
                .ToList();
            if (found.Count == 0)
                continue;

            withEquations++;
            equations += found.Count;
            perDocument.Add((Path.GetFileNameWithoutExtension(path), found.Count));

            foreach (var equation in found)
            {
                var isDisplay = equation.Parent is { } parent && parent.NamespaceUri == MathNamespace && parent.LocalName == "oMathPara";
                if (isDisplay)
                    display++;

                foreach (var element in equation.Descendants().Where(e => e.NamespaceUri == MathNamespace))
                {
                    if (!Handled.Contains(element.LocalName) && !IsProperty(element))
                        unmapped[element.LocalName] = unmapped.GetValueOrDefault(element.LocalName) + 1;
                }

                if (validator is null)
                    continue;

                var messages = validator.Validate(OfficeMathMl.ToMathMl(equation, isDisplay));
                if (messages.Count == 0)
                    continue;

                invalid++;
                var shape = SchemaPlValidator.Shape(messages[0]);
                invalidShapes[shape] = invalidShapes.GetValueOrDefault(shape) + 1;
                rows.Append(Path.GetFileNameWithoutExtension(path)).Append('\t')
                    .Append(OfficeMathText.Linear(equation).CollapseWhitespace()).Append('\t')
                    .Append(messages[0]).Append('\n');
            }
        }

        var report = new StringBuilder();
        report.AppendLine("================================================================");
        report.AppendLine(" RÓWNANIA WORDA W KORPUSIE — KONWERSJA DO MathML");
        report.AppendLine("================================================================");
        report.AppendLine($"Dokumentów: {documents}   z równaniami: {withEquations}");
        report.AppendLine($"Równań: {equations}   w osobnym akapicie (wzór): {display}   w zdaniu (symbol): {equations - display}");
        report.AppendLine(validator is null
            ? $"MathML nie był walidowany: {reason}"
            : $"MathML zgodny ze schematem: {equations - invalid} / {equations}   niezgodnych: {invalid}");

        report.AppendLine();
        report.AppendLine($"--- KONSTRUKCJE OMML BEZ ODWZOROWANIA ({unmapped.Count}) ---");
        foreach (var (name, count) in unmapped)
            report.AppendLine($"  {count,6}  m:{name}");

        report.AppendLine();
        report.AppendLine($"--- RODZAJE NIEZGODNOŚCI ({invalidShapes.Count}) ---");
        foreach (var (shape, count) in invalidShapes.OrderByDescending(pair => pair.Value))
            report.AppendLine($"  {count,6}  {shape}");

        report.AppendLine();
        report.AppendLine("--- DOKUMENTY Z NAJWIĘKSZĄ LICZBĄ RÓWNAŃ ---");
        foreach (var (file, count) in perDocument.OrderByDescending(entry => entry.Count).Take(15))
            report.AppendLine($"  {count,6}  {file}");

        File.WriteAllText(Path.Combine(outputDirectory, "equations.txt"), report.ToString(), Encoding.UTF8);
        File.WriteAllText(Path.Combine(outputDirectory, "equations-invalid.tsv"), rows.ToString(), Encoding.UTF8);
        Console.Write(report.ToString());
        return 0;
    }

    // Właściwości konstrukcji („…Pr") i wszystko pod nimi (wartości, opis kolumn macierzy) nie są treścią równania.
    private static bool IsProperty(OpenXmlElement element) =>
        element.Ancestors().Prepend(element)
            .TakeWhile(ancestor => !OfficeMathText.IsMath(ancestor))
            .Any(ancestor => ancestor.NamespaceUri == MathNamespace && ancestor.LocalName.EndsWith("Pr", StringComparison.Ordinal));
}
