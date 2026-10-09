using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Walidacja eksportu wobec schemaPL.xsd — jedna dla narzędzi i testów.
    ///
    /// Schemat jest zasobem lokalnym (katalog <c>schema/</c> jest gitignorowany), więc na czystym klonie
    /// go nie ma: <see cref="TryLoad"/> zwraca wtedy null z powodem, a wywołujący pomija walidację,
    /// zamiast się czerwienić. Import plików zależnych (mathml3, typografia, instrukcje nowelizacyjne)
    /// rozwiązuje się względem katalogu schematu, dlatego schemat ładujemy ze ścieżki.
    /// </summary>
    internal sealed class SchemaPlValidator
    {
        internal const string SchemaDirectory = "schema";
        internal const string SchemaFile = "schemaPL.xsd";

        /// <summary>Nagłówek aktu — poza walidacją treści (<see cref="ValidateActBody"/>).</summary>
        private static readonly HashSet<string> ActHeader = ["metadane", "oznaczenie", "rodzaj-aktu", "data", "tytul"];

        private static readonly Regex Quoted = new("'[^']*'", RegexOptions.Compiled);

        private readonly XmlSchemaSet _schemas;

        private SchemaPlValidator(XmlSchemaSet schemas, string schemaPath)
        {
            _schemas = schemas;
            SchemaPath = schemaPath;
        }

        /// <summary>Ścieżka schematu, z której korzysta walidacja.</summary>
        internal string SchemaPath { get; }

        /// <summary>
        /// Szuka <c>schema/schemaPL.xsd</c> w <paramref name="startDirectory"/> i wyżej. Null z powodem, gdy
        /// schematu nie ma albo się nie kompiluje — niekompilujący się schemat to problem schematu, nie eksportu.
        /// </summary>
        internal static SchemaPlValidator? TryLoad(string startDirectory, out string reason)
        {
            var path = FindSchema(startDirectory);
            if (path is null)
            {
                reason = $"brak pliku {SchemaDirectory}/{SchemaFile} w {startDirectory} ani wyżej — schemat jest zasobem lokalnym";
                return null;
            }

            try
            {
                var schemas = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
                schemas.Add(null, path);
                schemas.Compile();
                reason = string.Empty;
                return new SchemaPlValidator(schemas, path);
            }
            catch (Exception ex) when (ex is XmlSchemaException or XmlException or IOException)
            {
                reason = $"schematu {path} nie udało się skompilować ({ex.GetType().Name}: {ex.Message})";
                return null;
            }
        }

        /// <summary>Problemy całego dokumentu; pusta lista znaczy „zgodny".</summary>
        internal IReadOnlyList<string> Validate(XElement root)
        {
            var problems = new List<string>();
            try
            {
                new XDocument(new XElement(root)).Validate(_schemas, (_, e) => problems.Add(e.Message));
            }
            catch (XmlSchemaValidationException ex)
            {
                // Błąd, po którym walidator nie potrafi kontynuować — dokument i tak jest niezgodny.
                problems.Add(ex.Message);
            }
            return problems;
        }

        /// <summary>
        /// Problemy treści aktu bez nagłówka: każdy element poziomu aktu sprawdzany wobec swojej deklaracji
        /// w typie aktu (<c>ustawa</c> → <c>UstawaTyp</c>). Nagłówek ma osobne braki, a pełna walidacja
        /// zatrzymuje się na nich, zanim dojdzie do jednostek.
        /// </summary>
        internal IReadOnlyList<string> ValidateActBody(XElement act)
        {
            var declarations = ActLevelDeclarations(act.Name);
            if (declarations is null)
                return [$"Element aktu spoza schematu: {act.Name.LocalName}"];

            var problems = new List<string>();
            foreach (var child in act.Elements().Where(e => !ActHeader.Contains(e.Name.LocalName)))
            {
                if (!declarations.TryGetValue(child.Name.LocalName, out var declaration))
                {
                    problems.Add($"Element poziomu aktu spoza typu aktu: {child.Name.LocalName}");
                    continue;
                }

                child.Validate(declaration, _schemas, (_, e) => problems.Add(e.Message));
            }
            return problems;
        }

        /// <summary>Czy akt ma treść poza nagłówkiem — akt bez jednostek przechodzi walidację treści „z pustego".</summary>
        internal static bool HasBody(XElement act) => act.Elements().Any(e => !ActHeader.Contains(e.Name.LocalName));

        /// <summary>Kształt komunikatu bez konkretnych nazw i wartości — do grupowania w raportach.</summary>
        internal static string Shape(string message) => Quoted.Replace(message, "'…'");

        private Dictionary<string, XmlSchemaElement>? ActLevelDeclarations(XName actName)
        {
            var root = _schemas.GlobalElements[new XmlQualifiedName("akt-prawny", actName.NamespaceName)] as XmlSchemaElement;
            var actDeclaration = Declarations((root?.ElementSchemaType as XmlSchemaComplexType)?.ContentTypeParticle)
                .GetValueOrDefault(actName.LocalName);
            return actDeclaration?.ElementSchemaType is XmlSchemaComplexType actType
                ? Declarations(actType.ContentTypeParticle)
                : null;
        }

        private static Dictionary<string, XmlSchemaElement> Declarations(XmlSchemaParticle? particle)
        {
            var declarations = new Dictionary<string, XmlSchemaElement>();
            Collect(particle);
            return declarations;

            void Collect(XmlSchemaParticle? current)
            {
                switch (current)
                {
                    case XmlSchemaElement element:
                        declarations.TryAdd(element.QualifiedName.Name, element);
                        break;
                    case XmlSchemaGroupBase group:
                        foreach (var item in group.Items)
                            Collect(item as XmlSchemaParticle);
                        break;
                }
            }
        }

        private static string? FindSchema(string startDirectory)
        {
            for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, SchemaDirectory, SchemaFile);
                if (File.Exists(candidate))
                    return candidate;
            }
            return null;
        }
    }
}
