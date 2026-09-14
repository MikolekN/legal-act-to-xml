using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace CorpusValidator;

/// <summary>
/// Walidacja wyeksportowanego XML-a wobec schemaPL.xsd.
///
/// Schemat jest zasobem LOKALNYM (katalog <c>schema/</c> jest gitignorowany, tak jak <c>DocRepo/</c>),
/// więc na czystym klonie go nie ma. Brak schematu NIE jest błędem — walidacja jest wtedy
/// pomijana z powodem, dokładnie tak jak testy oznaczone <c>[DocRepoFact]</c>. Inaczej narzędzie
/// czerwieniłoby się u każdego, kto nie ma lokalnych plików.
///
/// Schemat kompiluje się RAZ, przy pierwszym użyciu; import plików zależnych (mathml3, typografia,
/// instrukcje nowelizacyjne) rozwiązuje się względem katalogu schematu, dlatego ładujemy go
/// ze ścieżki, a nie ze strumienia.
/// </summary>
internal sealed class SchemaValidator
{
	private const string SchemaDirectory = "schema";
	private const string SchemaFile = "schemaPL.xsd";

	private readonly XmlSchemaSet _schemas;

	private SchemaValidator(XmlSchemaSet schemas) => _schemas = schemas;

	/// <summary>Ścieżka schematu, z której korzysta walidacja — do wypisania w raporcie.</summary>
	internal string SchemaPath { get; private init; } = string.Empty;

	/// <summary>
	/// Ładuje schemat, jeśli jest osiągalny. Zwraca <c>null</c> i powód, gdy walidacja ma zostać
	/// pominięta — brak katalogu <c>schema/</c> albo schemat, którego nie da się skompilować.
	/// </summary>
	internal static SchemaValidator? TryLoad(out string reason)
	{
		var path = FindSchema();
		if (path is null)
		{
			reason = $"brak pliku {SchemaDirectory}/{SchemaFile} powyżej katalogu roboczego " +
			         "— schemat jest zasobem lokalnym, więc walidacja została pominięta";
			return null;
		}

		try
		{
			var schemas = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
			schemas.Add(null, path);
			schemas.Compile();

			reason = string.Empty;
			return new SchemaValidator(schemas) { SchemaPath = path };
		}
		catch (Exception ex)
		{
			// Niekompilujący się schemat to problem schematu, nie eksportu — nie wolno go
			// zgłaszać jako błędów w setkach dokumentów.
			reason = $"schematu {path} nie udało się skompilować ({ex.GetType().Name}: {ex.Message})";
			return null;
		}
	}

	/// <summary>Komunikaty walidacji dla jednego dokumentu; pusta lista znaczy „zgodny".</summary>
	internal IReadOnlyList<string> Validate(XElement root)
	{
		var problems = new List<string>();

		try
		{
			new XDocument(root).Validate(_schemas, (_, e) => problems.Add(e.Message));
		}
		catch (XmlSchemaValidationException ex)
		{
			// Wyjątek zamiast zdarzenia zdarza się przy błędzie, po którym walidator nie potrafi
			// kontynuować — dokument i tak jest niezgodny, więc traktujemy go jak zwykły problem.
			problems.Add(ex.Message);
		}

		return problems;
	}

	/// <summary>
	/// Szuka <c>schema/schemaPL.xsd</c> w bieżącym katalogu i w górę — narzędzie bywa uruchamiane
	/// z korzenia repozytorium, ale i z katalogu wyjściowego builda.
	/// </summary>
	private static string? FindSchema()
	{
		var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

		while (directory is not null)
		{
			var candidate = Path.Combine(directory.FullName, SchemaDirectory, SchemaFile);
			if (File.Exists(candidate))
				return candidate;

			directory = directory.Parent;
		}

		return null;
	}

	/// <summary>
	/// Sprowadza komunikat do KSZTAŁTU, wycinając konkretne nazwy i wartości. Bez tego raport
	/// z korpusu to tysiące wierszy mówiących to samo o różnych jednostkach.
	/// </summary>
	internal static string Shape(string message) =>
		System.Text.RegularExpressions.Regex.Replace(message, "'[^']*'", "'…'");
}
