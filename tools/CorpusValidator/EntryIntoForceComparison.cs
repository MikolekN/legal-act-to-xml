using System.Xml.Linq;

namespace CorpusValidator;

/// <summary>
/// Zestawia daty wejścia w życie z ręcznie wypełnionym plikiem referencyjnym RCL. Obie strony mogą
/// być w schemaPL albo w kanonicznym zrzucie DTO, więc porównanie idzie po KLUCZU budowanym tak
/// samo niezależnie od formatu:
///
///   klucz jednostki = [prefiks cytatu] + eId          („art_1__pkt_9", „art_1__pkt_9//art_85a")
///   klucz zdania    = klucz jednostki + „__zd_" + nr
///
/// Prefiks cytatu jest konieczny, bo eId NIE jest w akcie unikalny: ta sama ścieżka („art_10")
/// istnieje raz w akcie, raz w przytoczonej treści nowelizacji.
/// </summary>
internal static class EntryIntoForceComparison
{
	internal static int Run(string referencePath, string dumpPath, string? reportPath)
	{
		if (!File.Exists(referencePath))
		{
			Console.Error.WriteLine($"Nie znaleziono pliku referencyjnego: {referencePath}");
			return 1;
		}

		if (!File.Exists(dumpPath))
		{
			Console.Error.WriteLine($"Nie znaleziono zrzutu: {dumpPath}");
			return 1;
		}

		var referenceRoot = XDocument.Load(referencePath).Root!;
		var dumpRoot = XDocument.Load(dumpPath).Root!;

		var referenceFormat = Detect(referenceRoot);
		var dumpFormat = Detect(dumpRoot);

		var reference = Collect(referenceRoot, referenceFormat);
		var dump = Collect(dumpRoot, dumpFormat);

		var sb = new System.Text.StringBuilder();
		void W(string s = "") { sb.AppendLine(s); Console.WriteLine(s); }

		W("================================================================");
		W(" DATY WEJŚCIA W ŻYCIE — ZESTAWIENIE Z PLIKIEM REFERENCYJNYM");
		W("================================================================");
		W($"Referencja: {referencePath}   ({reference.Count} dat)");
		W($"Zrzut:      {dumpPath}   ({dump.Count} dat)");
		W();

		// --- Pomiar 1: rozkład wartości. Tani i tępy, odporny na rozjazd eId. Wywala się
		// natychmiast, gdy data wyjątku rozleje się po drzewie albo gdy zatrucie zje za dużo.
		W("--- Rozkład dat ---");
		W($"{"data",-14}{"referencja",12}{"zrzut",12}");
		foreach (var date in reference.Values.Concat(dump.Values).Distinct().OrderBy(d => d))
			W($"{date,-14}{reference.Values.Count(v => v == date),12}{dump.Values.Count(v => v == date),12}");
		W();

		// --- Pomiar 2: pary klucz→data.
		var agree = new List<string>();
		var conflict = new List<string>();
		var missing = new List<string>();
		var extra = new List<string>();
		var unpairedReference = new List<string>();
		var unpairedDump = new List<string>();

		var dumpKeys = Keys(dumpRoot, dumpFormat);
		var referenceKeys = Keys(referenceRoot, referenceFormat);

		foreach (var (key, date) in reference.OrderBy(p => p.Key))
		{
			if (dump.TryGetValue(key, out var ours))
				(ours == date ? agree : conflict).Add($"{key}: RCL={date} zrzut={ours}");
			else if (dumpKeys.Contains(key))
				missing.Add($"{key}: RCL={date}, zrzut bez daty");
			else
				unpairedReference.Add($"{key}: RCL={date}, brak jednostki w zrzucie");
		}

		foreach (var (key, date) in dump.OrderBy(p => p.Key))
		{
			if (reference.ContainsKey(key))
				continue;
			if (referenceKeys.Contains(key))
				extra.Add($"{key}: zrzut={date}, RCL bez daty");
			else
				unpairedDump.Add($"{key}: zrzut={date}, brak jednostki w RCL");
		}

		W("--- Kubełki ---");
		W($"  SPRZECZNE (ta sama jednostka, różne daty)   {conflict.Count,6}   <- twarda bramka: 0");
		W($"  ZGODNE                                      {agree.Count,6}");
		W($"  BRAKUJĄCE (RCL ma datę, zrzut nie)          {missing.Count,6}");
		W($"  NADMIAROWE (zrzut ma datę, RCL nie)         {extra.Count,6}");
		W($"  BEZ PARY — jednostka tylko w RCL            {unpairedReference.Count,6}   (wierność struktury, nie dat)");
		W($"  BEZ PARY — jednostka tylko w zrzucie        {unpairedDump.Count,6}");
		W();

		Dump(W, "SPRZECZNE", conflict, 60);
		Dump(W, "BRAKUJĄCE", missing, 40);
		Dump(W, "NADMIAROWE", extra, 40);

		if (reportPath is not null)
		{
			File.WriteAllText(reportPath, sb.ToString());
			Console.Error.WriteLine($"\nRaport: {reportPath}");
		}

		return conflict.Count == 0 ? 0 : 2;
	}

	private static void Dump(Action<string> w, string title, List<string> items, int limit)
	{
		if (items.Count == 0)
			return;

		w($"--- {title} ({items.Count}) ---");
		foreach (var item in items.Take(limit)) w($"  {item}");
		if (items.Count > limit) w($"  … i {items.Count - limit} więcej");
		w(string.Empty);
	}

	private enum Format { SchemaPl, CanonicalDto }

	/// <summary>
	/// Format po elemencie głównym: schemaPL zaczyna się od <c>akt-prawny</c>, kanoniczny zrzut DTO
	/// od <c>legalDocument</c>. Dzięki temu ten sam tryb porównuje zarówno zrzut diagnostyczny,
	/// jak i prawdziwy eksport schemaPL — z plikiem referencyjnym RCL.
	/// </summary>
	private static Format Detect(XElement root) =>
		root.Name.LocalName == "legalDocument" ? Format.CanonicalDto : Format.SchemaPl;

	/// <summary>Klucz jednostki: klucz zdania bez końcowego „__zd_N".</summary>
	private static string HostUnit(string key)
	{
		var marker = key.LastIndexOf("__zd_", StringComparison.Ordinal);
		return marker < 0 ? key : key[..marker];
	}

	/// <summary>Klucz → data, dla elementów, które datę NIOSĄ.</summary>
	private static Dictionary<string, string> Collect(XElement root, Format format)
	{
		var result = new Dictionary<string, string>();
		Walk(root, format, prefix: string.Empty, ancestorKey: string.Empty,
			(key, date) => result.TryAdd(key, date));
		return result;
	}

	/// <summary>Wszystkie klucze obecne w pliku — także jednostek BEZ daty (do rozróżnienia
	/// „jednostka jest, ale bez daty" od „jednostki w ogóle nie ma").</summary>
	private static HashSet<string> Keys(XElement root, Format format)
	{
		var result = new HashSet<string>();
		Walk(root, format, prefix: string.Empty, ancestorKey: string.Empty,
			(key, _) => result.Add(key), keysOnly: true);
		return result;
	}

	private static void Walk(
		XElement element, Format format, string prefix, string ancestorKey,
		Action<string, string> emit, bool keysOnly = false)
	{
		var name = element.Name.LocalName;
		var dateAttribute = format == Format.SchemaPl ? "wejscie-w-zycie" : "effectiveDate";

		var key = element.Attribute("eId") is { } eId && !string.IsNullOrEmpty(eId.Value)
			? prefix + eId.Value
			: ancestorKey;

		// Zdanie nie ma eId — adresuje się numerem w obrębie jednostki nadrzędnej. Uwaga: RCL
		// potrafi rozbić jedno zdanie na kilka „fragmentów" o tym samym nr; klucz je scala,
		// a wygrywa pierwszy napotkany (patrz TryAdd w Collect).
		var isSentence = format == Format.SchemaPl ? name == "zdanie" : name == "segment";
		if (isSentence && ancestorKey.Length > 0)
		{
			var number = (format == Format.SchemaPl
				? element.Attribute("nr")?.Value
				: element.Attribute("order")?.Value) ?? "?";
			key = $"{ancestorKey}__zd_{number}";
		}

		// <amendment> w zrzucie nie jest jednostką — nie ma eId, więc wpadłby pod klucz jednostki
		// goszczącej i podmienił jej datę. Nowelizacja wchodzi w życie razem z gospodarzem,
		// więc jej data nie wnosi tu nic własnego.
		// W schemaPL atrybut niesie już datę PRZYSŁONIĘTĄ mocą wsteczną, a w zrzucie DTO obie daty
		// stoją osobno. Żeby porównywać to samo, po stronie zrzutu moc wsteczna ma pierwszeństwo —
		// w schemaPL tego atrybutu nie ma, więc odczyt spada na właściwy.
		var date = format == Format.CanonicalDto && name == "amendment"
			? null
			: element.Attribute("retroactiveFrom")?.Value ?? element.Attribute(dateAttribute)?.Value;

		if (keysOnly)
		{
			if (key.Length > 0)
				emit(key, string.Empty);
		}
		else if (date is not null && key.Length > 0)
		{
			emit(key, date);
		}

		// Granica cytatu: wszystko poniżej jest adresowane względem aktu ZMIENIANEGO.
		// Prefiks liczony od JEDNOSTKI goszczącej, nie od zdania — w schemaPL cytat leży wewnątrz
		// <zdanie>, a w zrzucie DTO bezpośrednio pod jednostką. Bez odcięcia „__zd_N" ten sam cytat
		// dostawałby po obu stronach inny prefiks i żadna para by się nie zeszła.
		var isQuote = format == Format.SchemaPl ? name == "cytat-strukt" : name == "amendmentContent";
		var childPrefix = isQuote ? HostUnit(key) + "//" : prefix;
		var childAncestor = isQuote ? string.Empty : key;

		foreach (var child in element.Elements())
			Walk(child, format, childPrefix, childAncestor, emit, keysOnly);
	}
}
