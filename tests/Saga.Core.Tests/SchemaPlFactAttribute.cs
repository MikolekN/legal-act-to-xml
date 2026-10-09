using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace Saga.Core.Tests
{
    /// <summary>
    /// [Fact] dla testu walidującego eksport wobec schemaPL.xsd. Schemat jest lokalny (katalog
    /// <c>schema/</c> jest gitignorowany), więc gdy go nie ma albo się nie kompiluje, test jest
    /// POMIJANY z powodem — jak <see cref="DocRepoFactAttribute"/>.
    /// </summary>
    public sealed class SchemaPlFactAttribute : FactAttribute
    {
        public SchemaPlFactAttribute(
            [CallerFilePath] string? sourceFilePath = null,
            [CallerLineNumber] int sourceLineNumber = -1)
            : base(sourceFilePath, sourceLineNumber)
        {
            Skip = SchemaPl.SkipReason;
        }
    }

    /// <summary>
    /// schemaPL.xsd i walidacja częściowa: element poziomu aktu sprawdzany wobec swojej deklaracji
    /// w <c>UstawaTyp</c>, bez nagłówka aktu, którego eksport jeszcze nie spełnia schematu.
    /// </summary>
    internal static class SchemaPl
    {
        public const string Namespace = "https://projects.task.gda.pl/caise/NLP/kisi/rcl/schemaPL/1.0";

        private static readonly Lazy<(XmlSchemaSet? Schemas, string? SkipReason)> Loaded = new(Load);

        public static string? SkipReason => Loaded.Value.SkipReason;

        /// <summary>Problemy walidacji wszystkich elementów poziomu aktu poza nagłówkiem.</summary>
        public static List<string> ValidateActBody(XElement act)
        {
            var schemas = Loaded.Value.Schemas
                ?? throw new InvalidOperationException(SkipReason);
            var declarations = ActLevelDeclarations(schemas);

            var problems = new List<string>();
            foreach (var child in act.Elements())
            {
                if (child.Name.LocalName is "metadane" or "tytul" or "data" or "rodzaj-aktu")
                    continue;
                if (!declarations.TryGetValue(child.Name.LocalName, out var declaration))
                {
                    problems.Add($"Element poziomu aktu spoza UstawaTyp: {child.Name.LocalName}");
                    continue;
                }

                child.Validate(declaration, schemas, (sender, e) =>
                    problems.Add($"{(sender as XObject)?.Parent?.Name.LocalName ?? (sender as XElement)?.Name.LocalName}: {e.Message}"));
            }

            return problems;
        }

        private static Dictionary<string, XmlSchemaElement> ActLevelDeclarations(XmlSchemaSet schemas)
        {
            var actType = (XmlSchemaComplexType)schemas.GlobalTypes[new XmlQualifiedName("UstawaTyp", Namespace)]!;
            var declarations = new Dictionary<string, XmlSchemaElement>();
            Collect(actType.ContentTypeParticle);
            return declarations;

            void Collect(XmlSchemaParticle? particle)
            {
                switch (particle)
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

        private static (XmlSchemaSet?, string?) Load()
        {
            var path = Path.Combine(TestFiles.GetRepositoryRoot(), "schema", "schemaPL.xsd");
            if (!File.Exists(path))
                return (null, $"Brak {path} — schemat jest lokalny (niewersjonowany), więc walidacja XSD jest pominięta.");

            try
            {
                var schemas = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
                schemas.Add(null, path);
                schemas.Compile();
                return (schemas, null);
            }
            catch (Exception ex) when (ex is XmlSchemaException or XmlException or IOException)
            {
                return (null, $"Schematu {path} nie udało się skompilować ({ex.GetType().Name}: {ex.Message}).");
            }
        }
    }
}
