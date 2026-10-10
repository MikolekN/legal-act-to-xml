using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Saga.Core;
using Saga.Core.Helpers;
using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Core.Services.Classify;
using Saga.Core.Services.Classify.Document;
using Saga.Model;
using Saga.Model.SystematizingUnits;

namespace CorpusValidator;

/// <summary>
/// Tryb raportu jednostek systematyzacyjnych (część … oddział) — miara wyjściowa i pętla zwrotna
/// planu docs/internal/plan-jednostki-systematyzacyjne.md (krok A1, tabela 1.4).
///
/// Dla każdego bloku w stylu szablonu przeznaczonym dla jednostek systematyzacyjnych albo
/// o kształcie oznaczenia („Rozdział 3") zestawia trzy odpowiedzi: co mówi styl, co rozpoznał
/// klasyfikator i co zbudował model. Wynik budowy pochodzi z obserwatora bloków w
/// <c>LegalDocumentParser.ParseBlocks</c>, więc raport mierzy ten sam potok co produkcja, a nie
/// jego kopię. Na koniec eksportuje model do schemaPL i sprawdza, czy jednostki doszły do XML.
///
/// Styl CZĘŚĆ(KSIĘGA) oznacza w szablonie zarówno oznaczenie części/księgi, jak i jej tytuł
/// (KC: „KSIĘGA PIERWSZA" / „CZĘŚĆ OGÓLNA"), więc blok w tym stylu tuż po rozpoznanej części albo
/// księdze liczy się jako nagłówek, a nie oznaczenie.
///
/// Kolumna <c>xmlHash</c> (eksport bez GUID-ów, które są losowe) pozwala porównać dwa przebiegi:
/// akt bez jawnych jednostek ma po zmianach eksportować się identycznie.
///
/// Użycie:
///   dotnet run --project tools/CorpusValidator -- --systematizing &lt;root&gt; &lt;katalog-wyjsciowy&gt;
/// </summary>
internal static class SystematizingUnitsReport
{
    private const string PartOrBookFamily = "CZĘŚĆ(KSIĘGA)";
    private const string AmendmentFamily = "Z*/JEDN_SYST";
    private const string RepealedHeadingFamily = "UCHYLENIE*TJ";

    private static readonly HashSet<string> DesignationFamilies = ["ROZDZ(ODDZ)_OZN", "TYT(DZ)_OZN"];
    private static readonly HashSet<string> HeadingFamilies = ["ROZDZ(ODDZ)_PRZEDM", "TYT(DZ)_PRZEDM"];

    /// <summary>Kształt oznaczenia niezależnie od stylu: słowo kluczowe jednostki na początku krótkiego wiersza.</summary>
    private static readonly Regex DesignationShape = new(
        @"^(?:CZĘŚĆ|KSIĘGA|TYTUŁ|DZIAŁ|ROZDZIAŁ|ODDZIAŁ)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Dłuższy wiersz z tym słowem na początku to proza („Rozdział 5 stosuje się…").</summary>
    private const int DesignationShapeMaxLength = 80;

    private static readonly Regex Digits = new(@"\d+", RegexOptions.Compiled);
    private static readonly Regex Roman = new(@"\b[IVXLCDM]+(?=[A-Za-z]?\b)", RegexOptions.Compiled);

    /// <summary>Elementy eksportu schemaPL reprezentujące JAWNĄ jednostkę (wersje „-n" to jednostki niejawne).</summary>
    private static readonly HashSet<string> ExplicitUnitElements =
        ["czesc", "ksiega", "tytul-jednostka", "dzial", "rozdzial", "oddzial"];

    private enum Role { None, Designation, Heading }

    private enum Outcome { NotAUnit, Built, Dropped, InAmendment, AsHeading, InAnnex }

    private sealed class BlockInfo
    {
        public required int Index { get; init; }
        public required string StyleId { get; init; }
        public required string Family { get; init; }
        public required ParagraphKind Kind { get; init; }
        public required string Text { get; init; }
        public Role Role { get; set; }
        public Outcome Outcome { get; set; }
        public ISystematizingUnit? Unit { get; set; }
        public BlockInfo? Next { get; set; }
    }

    private sealed class DocResult
    {
        public required string Category { get; init; }
        public required string File { get; init; }
        public string ActType { get; set; } = "";
        public string Error { get; set; } = "";
        public List<BlockInfo> Rows { get; } = [];
        public List<int> HeadingLengths { get; } = [];
        public int Articles { get; set; }
        public int ArticlesInExplicit { get; set; }
        public Dictionary<UnitType, int> Explicit { get; } = new();
        public int EmptyHeadings { get; set; }
        public List<string> DuplicatedUnitEIds { get; } = [];
        public List<(string Id, ValidationMessage Message)> UnitMessages { get; } = [];
        public int XmlUnitElements { get; set; } = -1;
        public int XmlQuotedUnitElements { get; set; }
        public Dictionary<string, int> XmlDuplicatedEIds { get; } = new();
        public string XmlHash { get; set; } = "";

        public IEnumerable<BlockInfo> StyledDesignations => Rows.Where(r => r.Role == Role.Designation);
        public IEnumerable<BlockInfo> Units => Rows.Where(r => IsSystematizing(r.Kind));
        public IEnumerable<BlockInfo> Built => Rows.Where(r => r.Outcome == Outcome.Built);
        public IEnumerable<BlockInfo> HeadingExpected => Built.Where(r => r.Next?.Role == Role.Heading);
        public int ExplicitTotal => Explicit.Values.Sum();
    }

    internal static int Run(string corpusRoot, string outputDirectory)
    {
        if (!Directory.Exists(corpusRoot))
        {
            Console.Error.WriteLine($"Nie znaleziono katalogu korpusu: {corpusRoot}");
            return 1;
        }

        Directory.CreateDirectory(outputDirectory);

        var files = EntryIntoForceReport.EnumerateDistinct(corpusRoot).ToList();
        var docs = new List<DocResult>(files.Count);
        foreach (var file in files)
        {
            docs.Add(Analyze(corpusRoot, file));
            if (docs.Count % 200 == 0)
                Console.Error.WriteLine($"  ...{docs.Count}/{files.Count} plików");
        }

        File.WriteAllText(Path.Combine(outputDirectory, "systematizing-blocks.tsv"), BlocksTsv(docs));
        File.WriteAllText(Path.Combine(outputDirectory, "systematizing-docs.tsv"), DocsTsv(docs));

        var summary = Summary(corpusRoot, docs);
        File.WriteAllText(Path.Combine(outputDirectory, "systematizing-report.txt"), summary);
        Console.WriteLine(summary);
        Console.Error.WriteLine($"Raport: {Path.Combine(outputDirectory, "systematizing-report.txt")}");
        return 0;
    }

    // ============================================================
    // Analiza jednego dokumentu
    // ============================================================

    private static DocResult Analyze(string root, string file)
    {
        var relative = Path.GetRelativePath(root, file);
        var doc = new DocResult { Category = Category(relative), File = relative };

        LegalDocument document;
        var body = new List<BlockInfo>();
        try
        {
            IReadOnlyList<DocumentBlock> blocks;
            using (var stream = File.OpenRead(file))
                blocks = new DocxBlockReader().ReadBlocks(stream);

            var classification = new DocumentClassifier().Classify(blocks);
            doc.ActType = classification.ActType?.ToString() ?? "NIE-AKT";

            var classifier = new ParagraphClassifier();
            ISystematizingUnit? pendingBefore = null;
            var index = -1;

            document = LegalDocumentParser.ParseBlocks(blocks, classification: classification,
                observeBlock: (block, context) =>
                {
                    index++;
                    var pendingAfter = context.PendingHeadingUnit;
                    if (!block.IsEmpty && block.Role == BlockRole.Body)
                    {
                        // Ten sam tekst i styl, które widzi orkiestrator; bez NumberingHint, bo ten
                        // dokłada wyłącznie kary — rodzaj jednostki się nie zmienia.
                        var text = block.Text.Sanitize().Trim();
                        var info = new BlockInfo
                        {
                            Index = index,
                            StyleId = block.StyleId ?? "",
                            Family = StyleFamily(block.StyleId),
                            Kind = classifier.Classify(new ClassificationInput(text, block.StyleId)).Kind,
                            Text = text,
                        };

                        // Wejście w jednostkę ustawia PendingHeadingUnit na nowy węzeł; porzucona część
                        // go nie ustawia, a blok pochłonięty przez nowelizację w ogóle nie dochodzi
                        // do StructureProcessor.
                        if (IsSystematizing(info.Kind))
                        {
                            if (pendingAfter != null && !ReferenceEquals(pendingAfter, pendingBefore))
                            {
                                info.Outcome = Outcome.Built;
                                info.Unit = pendingAfter;
                            }
                            else if (pendingBefore != null && pendingBefore.Heading == text)
                            {
                                // „KSIĘGA PIERWSZA" / „Część ogólna" — część nazwana jako tytuł jednostki.
                                info.Outcome = Outcome.AsHeading;
                            }
                            else
                            {
                                info.Outcome = context.InAnnex ? Outcome.InAnnex
                                    : context.InsideAmendment || context.AmendmentCollector.IsCollecting ? Outcome.InAmendment
                                    : Outcome.Dropped;
                            }
                        }

                        body.Add(info);
                    }

                    pendingBefore = pendingAfter;
                });
        }
        catch (Exception ex)
        {
            doc.Error = $"{ex.GetType().Name}: {ex.Message}";
            return doc;
        }

        AssignRoles(body);
        doc.Rows.AddRange(body.Where(IsReported));
        doc.HeadingLengths.AddRange(body.Where(b => b.Role == Role.Heading).Select(b => b.Text.Length));

        MeasureTree(document, doc);
        MeasureExport(document, doc);
        return doc;
    }

    /// <summary>Rola bloku wg stylu; CZĘŚĆ(KSIĘGA) rozstrzyga pozycja (patrz opis klasy).</summary>
    private static void AssignRoles(List<BlockInfo> body)
    {
        for (var i = 0; i < body.Count; i++)
        {
            var block = body[i];
            var previousKind = i > 0 ? body[i - 1].Kind : ParagraphKind.Unknown;
            block.Next = i + 1 < body.Count ? body[i + 1] : null;
            block.Role = block.Family switch
            {
                var f when DesignationFamilies.Contains(f) => Role.Designation,
                var f when HeadingFamilies.Contains(f) => Role.Heading,
                PartOrBookFamily => previousKind is ParagraphKind.PartUnit or ParagraphKind.BookUnit
                    ? Role.Heading
                    : Role.Designation,
                _ => Role.None,
            };
        }
    }

    private static bool IsReported(BlockInfo block) =>
        block.Family.Length > 0
        || IsSystematizing(block.Kind)
        || (block.Text.Length <= DesignationShapeMaxLength && DesignationShape.IsMatch(block.Text));

    private static void MeasureTree(LegalDocument document, DocResult doc)
    {
        doc.Articles = document.Articles.Count();
        var explicitIds = new List<string>();
        foreach (var part in document.Parts)
            Walk(part, underExplicit: false);

        doc.DuplicatedUnitEIds.AddRange(explicitIds
            .GroupBy(id => id)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}×{g.Count()}"));

        void Walk(BaseEntity entity, bool underExplicit)
        {
            var unit = (ISystematizingUnit)entity;
            var isExplicit = !unit.IsImplicit;
            if (isExplicit)
            {
                doc.Explicit[entity.UnitType] = doc.Explicit.GetValueOrDefault(entity.UnitType) + 1;
                explicitIds.Add(entity.Id);
                if (string.IsNullOrWhiteSpace(unit.Heading))
                    doc.EmptyHeadings++;
                doc.UnitMessages.AddRange(entity.ValidationMessages.Select(m => (entity.Id, m)));
            }

            if (entity is Subchapter subchapter && (underExplicit || isExplicit))
                doc.ArticlesInExplicit += subchapter.Articles.Count;

            foreach (var child in Children(entity))
                Walk(child, underExplicit || isExplicit);
        }
    }

    private static IEnumerable<BaseEntity> Children(BaseEntity unit) =>
        SystematizingHierarchy.Children(unit).Where(child => child is ISystematizingUnit);

    private static void MeasureExport(LegalDocument document, DocResult doc)
    {
        XElement xml;
        try
        {
            xml = LegalDocumentConverter.Convert(document);
        }
        catch (Exception ex)
        {
            doc.Error = $"eksport — {ex.GetType().Name}: {ex.Message}";
            return;
        }

        // Jednostki w cytatach należą do aktu zmienianego — liczone osobno, a ich eId nie są duplikatami.
        // Metadane powołują jednostki po eId (zdarzenia cyklu życia) — to też nie duplikaty.
        static bool Quoted(XElement e) => e.Ancestors().Any(a => a.Name.LocalName is "cytat-strukt" or "metadane");
        static bool IsUnit(XElement e) => ExplicitUnitElements.Contains(e.Name.LocalName)
            || (e.Name.LocalName == "tytul" && e.Attribute("eId") != null);

        doc.XmlUnitElements = xml.Descendants().Count(e => IsUnit(e) && !Quoted(e));
        doc.XmlQuotedUnitElements = xml.Descendants().Count(e => IsUnit(e) && Quoted(e));

        foreach (var group in xml.Descendants()
                     .Where(e => e.Attribute("eId") != null && !Quoted(e))
                     .GroupBy(e => e.Attribute("eId")!.Value)
                     .Where(g => g.Count() > 1))
        {
            foreach (var element in group.Skip(1))
                doc.XmlDuplicatedEIds[element.Name.LocalName] =
                    doc.XmlDuplicatedEIds.GetValueOrDefault(element.Name.LocalName) + 1;
        }

        doc.XmlHash = NormalizedHash(xml);
    }

    /// <summary>Skrót eksportu bez GUID-ów (losowych przy każdym przebiegu) — do porównania dwóch przebiegów.</summary>
    private static string NormalizedHash(XElement xml)
    {
        var copy = new XElement(xml);
        copy.Descendants().Where(e => e.Name.LocalName == "GUIDwersji").Remove();
        foreach (var element in copy.DescendantsAndSelf())
            element.Attributes().Where(a => a.Name.LocalName.Contains("GUID", StringComparison.Ordinal)).Remove();

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(copy.ToString(SaveOptions.DisableFormatting)));
        return Convert.ToHexString(bytes)[..16];
    }

    // ============================================================
    // Style i kategorie
    // ============================================================

    /// <summary>
    /// Rodzina stylu szablonu związana z jednostkami systematyzacyjnymi, po nazwie wyświetlanej
    /// (część przed „ – "), albo pusty napis dla stylu spoza tej grupy.
    /// </summary>
    private static string StyleFamily(string? styleId)
    {
        if (string.IsNullOrEmpty(styleId))
            return "";

        if (StyleLibraryMapper.TryGetAmendmentStyleInfo(styleId) is { TargetKind: AmendmentTargetKind.SystematizingUnit })
            return AmendmentFamily;

        var name = StyleLibraryMapper.TryGetOriginalStyleName(styleId);
        var code = name?.Split(" – ")[0].Trim();
        if (code != null && (code == PartOrBookFamily || DesignationFamilies.Contains(code) || HeadingFamilies.Contains(code)))
            return code;

        // Nagłówek jednostki uchylonej w tekście jednolitym („(uchylony)") — styl spoza mapy.
        return styleId.StartsWith("UCHYLENIE", StringComparison.OrdinalIgnoreCase) ? RepealedHeadingFamily : "";
    }

    /// <summary>Kategoria = pierwszy katalog pod korzeniem; w documents/ katalogi lat (&lt;rok&gt;/) to ustawy.</summary>
    internal static string Category(string relativePath)
    {
        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Length < 2)
            return "(korzeń)";
        return Regex.IsMatch(parts[0], @"^\d{4}$") ? "Ustawy" : parts[0];
    }

    private static bool IsSystematizing(ParagraphKind kind) => kind is
        ParagraphKind.PartUnit or ParagraphKind.BookUnit or ParagraphKind.TitleUnit or
        ParagraphKind.DivisionUnit or ParagraphKind.ChapterUnit or ParagraphKind.SubchapterUnit;

    /// <summary>Kształt oznaczenia do grupowania: cyfry → 9, liczba rzymska → R.</summary>
    private static string Shape(string text) =>
        Truncate(Roman.Replace(Digits.Replace(text, "9"), "R"), 50);

    // ============================================================
    // Wyjście
    // ============================================================

    private static string BlocksTsv(List<DocResult> docs)
    {
        var tsv = new StringBuilder("category\tfile\tblock\tfamily\trole\tkind\toutcome\teId\theading\tnextFamily\tstyleId\ttext\n");
        foreach (var doc in docs)
        {
            foreach (var row in doc.Rows)
            {
                var unit = row.Unit as BaseEntity;
                tsv.Append(doc.Category).Append('\t').Append(doc.File).Append('\t').Append(row.Index).Append('\t')
                    .Append(row.Family).Append('\t').Append(row.Role).Append('\t').Append(row.Kind).Append('\t')
                    .Append(row.Outcome).Append('\t').Append(unit?.Id ?? "").Append('\t')
                    .Append(Cell(row.Unit?.Heading)).Append('\t').Append(row.Next?.Family ?? "").Append('\t')
                    .Append(row.StyleId).Append('\t').Append(Cell(row.Text)).Append('\n');
            }
        }
        return tsv.ToString();
    }

    private static string DocsTsv(List<DocResult> docs)
    {
        var tsv = new StringBuilder(
            "category\tfile\tactType\tarticles\tarticlesInExplicit\tstyledDesignations\tstyledRecognized\t" +
            "built\tdropped\tdroppedParts\tinAmendment\theadingExpected\theadingCaptured\tnextRepealed\t" +
            "czesc\tksi\ttyt\tdzial\trozdz\toddz\temptyHeadings\tduplicatedUnitEIds\txmlUnitElements\t" +
            "xmlDuplicatedEIds\txmlHash\terror\n");
        foreach (var doc in docs)
        {
            tsv.Append(string.Join('\t',
                doc.Category, doc.File, doc.ActType, doc.Articles, doc.ArticlesInExplicit,
                doc.StyledDesignations.Count(), doc.StyledDesignations.Count(r => IsSystematizing(r.Kind)),
                doc.Built.Count(), doc.Units.Count(r => r.Outcome == Outcome.Dropped),
                doc.Units.Count(r => r.Outcome == Outcome.Dropped && r.Kind == ParagraphKind.PartUnit),
                doc.Units.Count(r => r.Outcome == Outcome.InAmendment),
                doc.HeadingExpected.Count(), doc.HeadingExpected.Count(r => HasHeading(r)),
                doc.Built.Count(r => r.Next?.Family == RepealedHeadingFamily),
                doc.Explicit.GetValueOrDefault(UnitType.Part), doc.Explicit.GetValueOrDefault(UnitType.Book),
                doc.Explicit.GetValueOrDefault(UnitType.Title), doc.Explicit.GetValueOrDefault(UnitType.Division),
                doc.Explicit.GetValueOrDefault(UnitType.Chapter), doc.Explicit.GetValueOrDefault(UnitType.Subchapter),
                doc.EmptyHeadings, string.Join(' ', doc.DuplicatedUnitEIds), doc.XmlUnitElements,
                string.Join(' ', doc.XmlDuplicatedEIds.Select(kv => $"{kv.Key}={kv.Value}")),
                doc.XmlHash, Cell(doc.Error))).Append('\n');
        }
        return tsv.ToString();
    }

    private static string Summary(string corpusRoot, List<DocResult> docs)
    {
        var sb = new StringBuilder();
        void W(string line = "") => sb.AppendLine(line);

        var ok = docs.Where(d => d.Error.Length == 0).ToList();
        var styled = ok.SelectMany(d => d.StyledDesignations).ToList();
        var styledRecognized = styled.Count(r => IsSystematizing(r.Kind));
        var units = ok.SelectMany(d => d.Units).ToList();
        var headingExpected = ok.SelectMany(d => d.HeadingExpected).ToList();
        var codesWithoutUnits = ok.Where(d => d.StyledDesignations.Count() >= 3 && d.ExplicitTotal == 0).ToList();
        var droppedParts = ok.SelectMany(d => d.Units.Where(r => r.Outcome == Outcome.Dropped && r.Kind == ParagraphKind.PartUnit)).Count();
        var withDuplicates = ok.Where(d => d.DuplicatedUnitEIds.Count > 0).ToList();
        var explicitInModel = ok.Sum(d => d.ExplicitTotal);
        var explicitInXml = ok.Where(d => d.XmlUnitElements >= 0).Sum(d => d.XmlUnitElements);

        W("================================================================");
        W(" JEDNOSTKI SYSTEMATYZACYJNE — STYL / KLASYFIKATOR / MODEL / XML");
        W("================================================================");
        W($"Korpus: {corpusRoot}");
        W($"Dokumentów: {docs.Count}   błędów: {docs.Count - ok.Count}");
        W();

        W("--- Miary planu (tabela 1.4) ---");
        W($"Oznaczenia w stylu szablonu rozpoznane jako jednostka:     {styledRecognized} / {styled.Count} ({Percent(styledRecognized, styled.Count)})");
        W($"Nagłówek przypisany, gdy następny blok ma styl nagłówka:   {headingExpected.Count(HasHeading)} / {headingExpected.Count} ({Percent(headingExpected.Count(HasHeading), headingExpected.Count)})");
        W($"Dokumenty z ≥3 oznaczeniami w stylu i 0 jawnych jednostek: {codesWithoutUnits.Count}");
        W($"Części porzucone (rozpoznane, niezbudowane):              {droppedParts} w {ok.Count(d => d.Units.Any(r => r.Outcome == Outcome.Dropped && r.Kind == ParagraphKind.PartUnit))} dok.");
        W($"Dokumenty ze zduplikowanym eId jawnej jednostki:           {withDuplicates.Count} (eId: {withDuplicates.Sum(d => d.DuplicatedUnitEIds.Count)})");
        W($"Jawne jednostki w modelu / w eksporcie XML:                {explicitInModel} / {explicitInXml}");
        W($"Jednostki systematyzacyjne w cytatach nowelizacji (XML):   {ok.Sum(d => d.XmlQuotedUnitElements)} w {ok.Count(d => d.XmlQuotedUnitElements > 0)} dok.");
        W();

        W("--- Wynik budowy rozpoznanych oznaczeń (także bez stylu) ---");
        foreach (var outcome in new[] { Outcome.Built, Outcome.Dropped, Outcome.InAmendment, Outcome.AsHeading, Outcome.InAnnex })
            W($"  {outcome,-12} {units.Count(r => r.Outcome == outcome),6}");
        W("  wg rodzaju (zbudowane):");
        foreach (var group in units.Where(r => r.Outcome == Outcome.Built).GroupBy(r => r.Kind).OrderByDescending(g => g.Count()))
            W($"    {group.Key,-16} {group.Count(),6}");
        W("  wg rodziny stylu (rozpoznane):");
        foreach (var group in units.GroupBy(r => r.Family.Length == 0 ? "(bez stylu syst.)" : r.Family).OrderByDescending(g => g.Count()))
            W($"    {group.Key,-22} {group.Count(),6}");
        W();

        W("--- Według kategorii ---");
        W($"{"Kategoria",-16} {"Dok.",6} {"ze stylem",10} {"oznacz.",8} {"rozpozn.",9} {"jawne",7} {"nagł.oczek.",12} {"nagł.przyp.",12}");
        foreach (var group in ok.GroupBy(d => d.Category).OrderBy(g => g.Key))
        {
            var g = group.ToList();
            W($"{group.Key,-16} {g.Count,6} {g.Count(d => d.StyledDesignations.Any()),10} " +
              $"{g.Sum(d => d.StyledDesignations.Count()),8} {g.Sum(d => d.StyledDesignations.Count(r => IsSystematizing(r.Kind))),9} " +
              $"{g.Sum(d => d.ExplicitTotal),7} {g.Sum(d => d.HeadingExpected.Count()),12} {g.Sum(d => d.HeadingExpected.Count(HasHeading)),12}");
        }
        W();

        W("--- Nierozpoznane oznaczenia w stylu szablonu — wg kształtu (cyfry → 9, rzymskie → R) ---");
        foreach (var group in ok.SelectMany(d => d.StyledDesignations.Where(r => !IsSystematizing(r.Kind)).Select(r => (Doc: d, Row: r)))
                     .GroupBy(x => Shape(x.Row.Text)).OrderByDescending(g => g.Count()).Take(40))
        {
            var sample = group.First();
            W($"  {group.Count(),5} | {group.Key,-50} | np. {sample.Doc.File} #{sample.Row.Index}");
        }
        W();

        W("--- Nagłówek nieprzypisany mimo stylu nagłówka po oznaczeniu (pierwsze 25) ---");
        foreach (var x in ok.SelectMany(d => d.HeadingExpected.Where(r => !HasHeading(r)).Select(r => (Doc: d, Row: r))).Take(25))
            W($"  {x.Doc.File} #{x.Row.Index} {Truncate(x.Row.Text, 20),-20} → [{x.Row.Next!.Text.Length} zn.] {Truncate(x.Row.Next.Text, 80)}");
        W($"  Po oznaczeniu blok „(uchylony)” w stylu {RepealedHeadingFamily}: {ok.Sum(d => d.Built.Count(r => r.Next?.Family == RepealedHeadingFamily))} (poza mianownikiem — faza G3)");
        W();

        var lengths = ok.SelectMany(d => d.HeadingLengths).Order().ToList();
        W("--- Długość bloków w stylu nagłówka (znaki) ---");
        if (lengths.Count > 0)
            W($"  n={lengths.Count}  p50={Quantile(lengths, 0.5)}  p90={Quantile(lengths, 0.9)}  p99={Quantile(lengths, 0.99)}  max={lengths[^1]}  >120: {lengths.Count(l => l > 120)}");
        W();

        W("--- Dokumenty z ≥3 oznaczeniami w stylu i 0 jawnych jednostek ---");
        foreach (var doc in codesWithoutUnits.OrderByDescending(d => d.StyledDesignations.Count()).Take(20))
            W($"  {doc.File,-45} oznaczeń {doc.StyledDesignations.Count(),4}  rozpoznanych {doc.StyledDesignations.Count(r => IsSystematizing(r.Kind)),4}  artykułów {doc.Articles,5}");
        W();

        W("--- Dokumenty ze zduplikowanym eId jawnej jednostki (pierwsze 20) ---");
        foreach (var doc in withDuplicates.OrderByDescending(d => d.DuplicatedUnitEIds.Count).Take(20))
            W($"  {doc.File,-45} {Truncate(string.Join(' ', doc.DuplicatedUnitEIds), 100)}");
        W();

        W("--- Komunikaty walidacji jawnych jednostek (rodzaj: liczba w dokumentach) ---");
        foreach (var kind in ok.SelectMany(d => d.UnitMessages.Select(m => (Doc: d, m.Id, m.Message)))
                     .GroupBy(x => $"{x.Message.Level} {MessageKind(x.Message.Message)}").OrderByDescending(g => g.Count()))
        {
            W($"  {kind.Key,-60} {kind.Count(),6} w {kind.Select(x => x.Doc).Distinct().Count()} dok.");
            foreach (var sample in kind.Take(5))
                W($"      {sample.Doc.File} {sample.Id}: {Truncate(sample.Message.Message, 80)}");
        }
        W();

        W("--- Zduplikowane eId w eksporcie XML poza cytatami, wszystkie elementy (informacyjnie — G3/G7) ---");
        W($"  Dokumentów: {ok.Count(d => d.XmlDuplicatedEIds.Count > 0)}");
        foreach (var group in ok.SelectMany(d => d.XmlDuplicatedEIds).GroupBy(kv => kv.Key).OrderByDescending(g => g.Sum(kv => kv.Value)))
            W($"  {group.Key,-22} {group.Sum(kv => kv.Value),6}");
        W();

        if (docs.Count > ok.Count)
        {
            W("--- Błędy ---");
            foreach (var doc in docs.Where(d => d.Error.Length > 0).Take(30))
                W($"  {doc.File}: {Truncate(doc.Error, 140)}");
        }

        return sb.ToString();
    }

    private static string MessageKind(string message)
    {
        var end = message.IndexOfAny([':', '(', '„']);
        return (end > 0 ? message[..end] : message).Trim();
    }

    private static bool HasHeading(BlockInfo row) => !string.IsNullOrWhiteSpace(row.Unit?.Heading);

    private static int Quantile(List<int> sorted, double q) =>
        sorted[Math.Min(sorted.Count - 1, (int)Math.Floor(q * sorted.Count))];

    private static string Cell(string? text) =>
        (text ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');

    private static string Truncate(string text, int max) =>
        text.Length > max ? text[..max] + "…" : text;

    private static string Percent(int part, int total) =>
        total > 0 ? $"{100.0 * part / total:F1}%" : "—";
}
