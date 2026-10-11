using System.Text;
using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Ingest.Pdf;
using Saga.Core.Services;
using Saga.Core.Services.EntryIntoForce;
using Saga.Core.Services.Parsing;
using Saga.Core.Services.UnitReferences;
using Saga.Model;
using Saga.Model.EditorialUnits;
using Saga.Model.EntryIntoForce;
using Saga.Model.SystematizingUnits;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Testy strażnicze decyzji architektonicznych z docs/adr/. Każdy pilnuje jednej decyzji,
    /// której odwrócenie nie zepsułoby builda — przez co dałoby się je wprowadzić niezauważenie.
    /// Czerwony test tutaj znaczy: albo zmiana jest niezamierzona, albo trzeba wystawić nowy ADR
    /// ze statusem Supersedes i świadomie usunąć asercję.
    ///
    /// Decyzje pokryte testami stojącymi w innych miejscach (nie duplikujemy ich tutaj) są
    /// wskazane w sekcji „Weryfikacja" odpowiedniego ADR.
    /// </summary>
    public class ArchitectureDecisionTests
    {
        // ============================================================
        // ADR-0001 — reprezentacja pośrednia nie wycieka do Saga.Model
        // ============================================================

        [Fact]
        public void Adr0001_SagaModel_DoesNotDependOnParserOrIntermediateRepresentation()
        {
            var modelDirectory = Path.Combine(TestFiles.GetRepositoryRoot(), "src", "Saga.Model");

            var leaking = Directory.EnumerateFiles(modelDirectory, "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                               !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                .Where(file =>
                {
                    var content = File.ReadAllText(file);
                    return content.Contains("using Saga.Core", StringComparison.Ordinal) ||
                           content.Contains("DocumentBlock", StringComparison.Ordinal) ||
                           content.Contains("DocumentFormat.OpenXml", StringComparison.Ordinal);
                })
                .Select(Path.GetFileName)
                .ToList();

            Assert.True(leaking.Count == 0,
                "Saga.Model musi zostać czystym modelem wyjściowym (ADR-0001). Pliki sięgające " +
                "do parsera, IR albo OpenXml: " + string.Join(", ", leaking));
        }

        // ============================================================
        // ADR-0002 — adaptery bezstylowe nie zgadują styleId
        // ============================================================

        [Fact]
        public void Adr0002_PdfBlocks_NeverCarryStyleId()
        {
            var pdf = new TestPdfBuilder().AddPage()
                .AddText("USTAWA", 57, 760)
                .AddText("z dnia 5 marca 2024 r.", 57, 740)
                .AddText("o ochronie danych osobowych", 57, 720)
                .AddText("Art. 1. Ustawa reguluje ochronę danych osobowych.", 57, 700)
                .AddText("Art. 2. Organem właściwym jest Prezes Urzędu.", 57, 680)
                .AddText("Art. 3. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.", 57, 660)
                .Build();

            using var stream = new MemoryStream(pdf);
            var blocks = new PdfBlockReader().ReadBlocks(stream);

            Assert.NotEmpty(blocks);
            // Styl zgadnięty z treści awansowałby heurystykę do rangi sygnału szablonu (ADR-0002).
            Assert.All(blocks, block => Assert.Null(block.StyleId));
        }

        // ============================================================
        // ADR-0004 — brak zależności PDF na licencji AGPL
        // ============================================================

        [Fact]
        public void Adr0004_NoAgplPdfLibraryReferenced()
        {
            var repositoryRoot = TestFiles.GetRepositoryRoot();
            string[] agplPackages = { "itext", "iTextSharp" };

            var offending = Directory.EnumerateFiles(repositoryRoot, "*.csproj", SearchOption.AllDirectories)
                .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                .Select(file => (Name: Path.GetFileName(file), Content: File.ReadAllText(file)))
                .Where(project => agplPackages.Any(package =>
                    project.Content.Contains($"\"{package}", StringComparison.OrdinalIgnoreCase)))
                .Select(project => project.Name)
                .ToList();

            Assert.True(offending.Count == 0,
                "Biblioteka PDF na licencji AGPL rozciągnęłaby wymóg udostępnienia źródeł na usługę " +
                "sieciową RCL (ADR-0004). Projekty z taką zależnością: " + string.Join(", ", offending));
        }

        // ============================================================
        // ADR-0007 — klasyfikacja nie mutuje LegalDocument.Type
        // ============================================================

        [Fact]
        public void Adr0007_RecognizedActKind_DoesNotOverwriteDocumentType()
        {
            // Rozporządzenie rozpoznawane przez klasyfikator (por. DocumentClassifierTests.Classify_Regulation_IsRecognized).
            string[] regulationLines =
            {
                "ROZPORZĄDZENIE MINISTRA FINANSÓW",
                "z dnia 12 czerwca 2023 r.",
                "w sprawie szczegółowych zasad rachunkowości",
                "Na podstawie art. 50 ust. 1 ustawy z dnia 29 września 1994 r. o rachunkowości zarządza się, co następuje:",
                "§ 1. Rozporządzenie określa zasady rachunkowości.",
                "§ 2. Ilekroć w rozporządzeniu jest mowa o jednostce, rozumie się przez to podmiot.",
                "§ 3. Rozporządzenie wchodzi w życie z dniem 1 stycznia 2024 r.",
            };

            using var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(string.Join("\n", regulationLines)));

            var result = LegalDocumentParser.Parse(stream, "rozporzadzenie.txt");

            // Klasyfikacja rozpoznaje rodzaj…
            Assert.Equal(LegalActType.Regulation, result.Classification.ActType);
            Assert.NotNull(result.Document);

            // …ale Type pozostaje deklaracją wywołującego, nie obserwacją potoku (ADR-0007).
            Assert.Equal(LegalActType.Statute, result.Document!.Type);
            Assert.Same(result.Classification, result.Document.Classification);
        }

        // ============================================================
        // ADR-0008 — nierozpoznany cel wyjątku jest flagowany, nie zgadywany
        // ============================================================

        [Fact]
        public void Adr0008_UnparseableExceptionScope_IsFlagged_AndBaseClauseStillApplies()
        {
            // Zakres opisany prozą: data wyjątku jest jawna, ale nie da się wskazać, czego dotyczy.
            string[] lines =
            {
                "USTAWA",
                "z dnia 5 marca 2025 r.",
                "o zmianie ustawy o rzeczach różnych",
                "Art. 1. Wprowadza się zmiany porządkowe.",
                "Art. 2. Ustawa wchodzi w życie z dniem 1 stycznia 2026 r., z wyjątkiem art. 1 pkt 20 " +
                "w zakresie, w jakim dotyczy dziesięcioletniego planu rozwoju sieci wodorowej o zasięgu " +
                "unijnym, który wchodzi w życie z dniem 1 stycznia 2027 r.",
            };

            using var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(string.Join("\n", lines)));
            var document = LegalDocumentParser.Parse(stream, "ustawa.txt",
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document;

            Assert.NotNull(document);
            var info = document!.EntryIntoForce;
            Assert.NotNull(info);

            // Reguła ogólna obowiązuje mimo nierozstrzygalnego wyjątku…
            Assert.Equal(new DateTime(2026, 1, 1), info!.ResolvedDate);
            Assert.Equal(new DateTime(2026, 1, 1), document.EffectiveDate);

            // …a wyjątek jest oflagowany, nie przypisany na oślep.
            var partial = Assert.Single(info.Partials);
            Assert.All(partial.Targets, t => Assert.False(t.IsApplicable));
            Assert.False(info.IsComplete);
            Assert.NotEmpty(info.UnresolvedFragments);
            Assert.All(document.Articles, a => Assert.Equal(new DateTime(2026, 1, 1), a.EffectiveDate));
        }

        [Fact]
        public void Adr0008_MissingAnnouncementDate_NeverFallsBackToActDate()
        {
            // Formuła względna bez daty ogłoszenia: data aktu (ActDate) NIE jest substytutem —
            // odstęp między uchwaleniem a ogłoszeniem to zwykle tygodnie.
            string[] lines =
            {
                "USTAWA",
                "z dnia 5 marca 2025 r.",
                "o zmianie ustawy o rzeczach różnych",
                "Art. 1. Wprowadza się zmiany porządkowe.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
            };

            using var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(string.Join("\n", lines)));
            var document = LegalDocumentParser.Parse(stream, "ustawa.txt",
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document;

            Assert.NotNull(document);
            Assert.Equal(new DateTime(2025, 3, 5), document!.ActDate);

            // Formuła rozpoznana, data świadomie nierozstrzygnięta.
            Assert.Equal(EntryIntoForceFormulaKind.AfterDaysFromAnnouncement, document.EntryIntoForce!.Formula.Kind);
            Assert.Null(document.EntryIntoForce.ResolvedDate);
            Assert.Equal(default, document.EffectiveDate);
            Assert.NotEmpty(document.Articles);
            Assert.All(document.Articles, a => Assert.Equal(default, a.EffectiveDate));
            Assert.All(document.Articles.SelectMany(a => a.Paragraphs),
                p => Assert.Equal(default, p.EffectiveDate));
        }

        // ============================================================
        // ADR-0009 — jedno źródło słownika polskich liczebników
        // ============================================================

        /// <summary>
        /// Rdzenie liczebnikowe, których nagromadzenie w KODZIE (nie w komentarzu) świadczy o tym,
        /// że plik trzyma własny słownik. Pojedyncza wzmianka jest niewinna — literał
        /// „pierwszego dnia" w formule wejścia w życie daje 1 sygnał; słownik zawsze przekracza próg.
        /// </summary>
        private static readonly string[] NumeralSignals =
        {
            "pierwsz", "drug", "trzec", "czwart", "piąt", "szóst", "siódm", "ósm", "dziewiąt",
            "dziesiąt", "jedenast", "dwunast", "dwudziest", "trzydziest", "czterdziest",
            "dwóch", "trzech", "czterech", "pięciu", "sześciu", "siedmiu", "ośmiu", "dziewięciu",
        };

        [Fact]
        public void Adr0009_PolishNumerals_AreDefinedInExactlyOnePlace()
        {
            var sourceRoot = Path.Combine(TestFiles.GetRepositoryRoot(), "src");
            var owner = Path.Combine("Helpers", "PolishNumerals.cs");

            var offenders = Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                               !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                               !file.EndsWith(owner, StringComparison.Ordinal))
                .Select(file => (File: Path.GetFileName(file), Signals: CountNumeralSignals(file)))
                .Where(x => x.Signals >= 4)
                .ToList();

            Assert.True(offenders.Count == 0,
                "Słownik polskich liczebników musi mieszkać wyłącznie w Helpers/PolishNumerals.cs (ADR-0009). " +
                "Pliki z własnym zestawem liczebników: " +
                string.Join(", ", offenders.Select(o => $"{o.File} ({o.Signals} sygnałów)")));
        }

        // ============================================================
        // ADR-0010 — jeden kursor ścieżki jednostek
        // ============================================================

        [Fact]
        public void Adr0010_ScopeAndMainEnumeration_ShareTheSameUnitPath()
        {
            // Ten sam ogon powołania raz jako wyliczenie główne, raz wewnątrz zakresu. Dopóki oba
            // idą jednym kursorem, ścieżki są identyczne; rozwidlenie łamie ten test natychmiast.
            const string tail = "art. 5 pkt 2 lit. a tiret pierwsze podwójne tiret drugie";

            var asMain = UnitEnumerationParser.Parse(tail)
                .Targets.Select(t => t.Structure.ToString()).ToArray();

            var asScope = UnitEnumerationParser.Parse($"art. 1 pkt 13 w zakresie {tail}")
                .Targets.Single().ScopeStructures.Select(s => s.ToString()).ToArray();

            Assert.Equal(new[] { "art_5__pkt_2__lit_a__tir_1__dwtir_2" }, asMain);
            Assert.Equal(asMain, asScope);
        }

        // ============================================================
        // ADR-0011 — data reguły ogólnej rozsyłana po całym drzewie
        // ============================================================

        [Fact]
        public void Adr0011_GeneralDate_ReachesEveryUnit_UnresolvedExceptionLeavesItBlank()
        {
            // Trzy kształty naraz: reguła ogólna, wyjątek rozstrzygnięty i wyjątek bez daty.
            // Odwrócenie decyzji (powrót do „rozsyłamy tylko wyjątki") łamie pierwszą asercję,
            // a „wpiszmy coś zamiast pustki" — ostatnią.
            string[] lines =
            {
                "USTAWA",
                "z dnia 5 marca 2025 r.",
                "o zmianie ustawy o rzeczach różnych",
                "Art. 1. Wprowadza się zmiany porządkowe.",
                "Art. 2. Przepis przejściowy.",
                "Art. 3. Przepis o odroczonym skutku.",
                "Art. 4. Ustawa wchodzi w życie z dniem 1 stycznia 2026 r., z wyjątkiem art. 2, " +
                "który wchodzi w życie z dniem 1 marca 2026 r., z wyjątkiem art. 3, który wchodzi " +
                "w życie z dniem określonym w komunikacie.",
            };

            using var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(string.Join("\n", lines)));
            var document = LegalDocumentParser.Parse(stream, "ustawa.txt",
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document;

            Assert.NotNull(document);
            BaseEntity Article(string number) => document!.Articles.Single(a => a.Number?.Value == number);

            // 1. Jednostka bez wyjątku dostaje datę reguły ogólnej…
            Assert.Equal(new DateTime(2026, 1, 1), Article("1").EffectiveDate);
            Assert.Equal(new DateTime(2026, 1, 1), Article("4").EffectiveDate);

            // …i dziedziczy ją w dół, aż do zdań.
            Assert.All(Sentences(Article("1")), s => Assert.Equal(new DateTime(2026, 1, 1), s.EffectiveDate));

            // 2. Wyjątek rozstrzygnięty nadpisuje datę na swoim celu i w dół.
            Assert.Equal(new DateTime(2026, 3, 1), Article("2").EffectiveDate);
            Assert.All(Sentences(Article("2")), s => Assert.Equal(new DateTime(2026, 3, 1), s.EffectiveDate));

            // 3. Wyjątek nierozstrzygnięty ZABIERA datę — brak daty jest tu nośnikiem informacji —
            //    i zabiera ją także poddrzewu, bo ustęp nie wchodzi w życie wcześniej niż artykuł.
            Assert.Equal(default, Article("3").EffectiveDate);
            Assert.All(Sentences(Article("3")), s => Assert.Equal(default, s.EffectiveDate));
            Assert.NotEmpty(Sentences(Article("3")));
        }

        private static List<TextSegment> Sentences(BaseEntity article) =>
            ((Article)article).Paragraphs.SelectMany(p => p.TextSegments).ToList();

        [Fact]
        public void Adr0011_WrittenDates_ComeOnlyFromTheClause()
        {
            // Niezmiennik pochodzenia: każda data zapisana w drzewie musi pochodzić z klauzuli —
            // z reguły ogólnej albo z któregoś wyjątku. Łamie się przy KAŻDEJ pomyłce propagacji
            // (przesunięcie o poziom, zła gałąź cytatu, dziedziczenie w bok).
            string[] lines =
            {
                "USTAWA",
                "z dnia 5 marca 2025 r.",
                "o zmianie ustawy o rzeczach różnych",
                "Art. 1. W ustawie z dnia 13 października 1998 r. o systemie ubezpieczeń społecznych " +
                "wprowadza się następujące zmiany:",
                "1) po rozdziale 9 dodaje się rozdział 9a w brzmieniu:",
                "„Art. 85a. 1. Zadania są realizowane w jednostkach terenowych.",
                "2. Fizjoterapeuta może wydawać orzeczenia.”",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia, z wyjątkiem " +
                "art. 1 pkt 1, który wchodzi w życie z dniem 1 stycznia 2027 r.",
            };

            using var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(string.Join("\n", lines)));

            // Data ogłoszenia przychodzi nagłówkiem publikatora — bez niej formuła
            // „po upływie 14 dni" nie dałaby żadnej daty i niezmiennik byłby pusty.
            var blocks = new List<DocumentBlock>
            {
                new() { Text = "DZIENNIK USTAW", Role = BlockRole.JournalHeader },
                new() { Text = "Warszawa, dnia 29 marca 2026 r.", Role = BlockRole.JournalHeader },
            };
            blocks.AddRange(new PlainTextBlockReader().ReadBlocks(stream));

            var document = LegalDocumentParser.Parse(blocks,
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document;

            Assert.NotNull(document);
            Assert.Equal(new DateTime(2026, 3, 29), document!.AnnouncementDate);
            var info = document.EntryIntoForce!;

            var allowed = new HashSet<DateTime> { default };
            if (info.ResolvedDate is { } general) allowed.Add(general);
            foreach (var partial in info.Partials)
                if (partial.ResolvedDate is { } exception) allowed.Add(exception);

            Assert.Equal(2, allowed.Count - 1);   // reguła ogólna + jeden wyjątek, żadnej daty więcej

            foreach (var unit in document.Parts.SelectMany(Walk))
            {
                Assert.Contains(unit.EffectiveDate, allowed);
                if (unit is IHasTextSegments holder)
                    Assert.All(holder.TextSegments, s => Assert.Contains(s.EffectiveDate, allowed));
            }
        }

        // ============================================================
        // ADR-0012 — nagłówek strony jedzie potokiem jako rola bloku IR
        // ============================================================

        [Fact]
        public void Adr0012_PageHeaderText_NeverBecomesActContent()
        {
            // Nagłówek jedzie TĄ SAMĄ listą bloków co treść, więc jedyną zaporą przed wciągnięciem
            // go do modelu jest filtr roli w ParserOrchestrator. Gdyby zniknął, tekst nagłówka
            // zostałby sklasyfikowany tekstowo i dopisał do aktu jednostkę, której w nim nie ma.
            var docx = DocxFixtures.WithFirstPageHeader(
                new[] { "DZIENNIK USTAW", "Art. 999. Przepis z nagłówka.", "Poz. 26" },
                "USTAWA",
                "z dnia 5 marca 2025 r.",
                "o rzeczach różnych",
                "Art. 1. Przepis pierwszy.");

            using var stream = new MemoryStream(docx);
            var document = LegalDocumentParser.Parse(stream, "ustawa.docx",
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document;

            Assert.NotNull(document);
            var article = Assert.Single(document!.Articles);
            Assert.StartsWith("Art. 1.", article.ContentText);
            Assert.StartsWith("USTAWA", document.Title);

            // Rola „pomijany przez parser" nie znaczy „zignorowany": metadane publikatora
            // z tego samego nagłówka SĄ odczytane — to cały sens wpuszczenia go do potoku.
            Assert.Equal(new[] { 26 }, document.SourceJournal.Positions);
        }

        [Fact]
        public void Adr0012_PageHeaderText_DoesNotInfluenceClassification()
        {
            // Wabik: „w sprawie ogłoszenia jednolitego tekstu" to definitywny sygnał TJ (§ 102).
            // Umieszczony w NAGŁÓWKU nie może przestawić rozpoznania rodzaju aktu — inaczej
            // strefa tytułowa klasyfikatora czytałaby metadane strony jak treść aktu.
            string[] body =
            {
                "USTAWA",
                "z dnia 5 marca 2025 r.",
                "o rzeczach różnych",
                "Art. 1. Przepis pierwszy.",
                "Art. 2. Ustawa wchodzi w życie z dniem ogłoszenia.",
            };

            var plain = Classify(DocxFixtures.WithoutHeader(body));
            var withHeader = Classify(DocxFixtures.WithFirstPageHeader(
                new[] { "DZIENNIK USTAW", "w sprawie ogłoszenia jednolitego tekstu ustawy", "Poz. 26" },
                body));

            Assert.Equal(plain.ActType, withHeader.ActType);
            Assert.Equal(plain.IsConsolidatedText, withHeader.IsConsolidatedText);
            Assert.Equal(plain.Confidence, withHeader.Confidence);

            // Także numeracja bloków w sygnałach: nagłówek nie przesuwa niczego, na czym
            // opierają się goldeny i diagnostyka.
            Assert.Equal(
                plain.Signals.Select(s => (s.Kind, s.BlockIndex)),
                withHeader.Signals.Select(s => (s.Kind, s.BlockIndex)));
        }

        // ============================================================
        // ADR-0013 — moc wsteczna przysłania datę dopiero przy zapisie
        // ============================================================

        [Fact]
        public void Adr0013_RetroactiveDate_MasksEntryDateOnlyInOutput()
        {
            string[] lines =
            {
                "USTAWA",
                "z dnia 5 marca 2025 r.",
                "o zmianie ustawy o rzeczach różnych",
                "Art. 1. Wprowadza się zmiany porządkowe.",
                "Art. 2. Ustawa wchodzi w życie z dniem 1 stycznia 2026 r., z mocą od dnia 1 lipca 2025 r.",
            };

            using var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(string.Join("\n", lines)));
            var document = LegalDocumentParser.Parse(stream, "ustawa.txt",
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document;

            Assert.NotNull(document);
            var article = document!.Articles.First();

            Assert.Equal(new DateTime(2026, 1, 1), article.EffectiveDate);
            Assert.Equal(new DateTime(2025, 7, 1), article.RetroactiveFrom);
            Assert.NotEqual(article.EffectiveDate, article.RetroactiveFrom);

            var xml = Saga.Core.Services.LegalDocumentConverter.Convert(document);

            var unitDates = xml.Descendants()
                .Select(e => e.Attribute("wejscie-w-zycie")?.Value)
                .Where(value => value is not null)
                .ToList();

            Assert.NotEmpty(unitDates);
            Assert.All(unitDates, value => Assert.Equal("2025-07-01", value));

            // Metadane aktu niosą obie daty osobno, jako zdarzenia (ADR-0022).
            var events = xml.Descendants().Where(e => e.Name.LocalName == "Zdarzenie")
                .ToDictionary(e => e.Attribute("rodzaj")!.Value, e => e.Attribute("data")!.Value);

            Assert.Equal("2026-01-01", events["wejscie-w-zycie"]);
            Assert.Equal("2025-07-01", events["stosowanie-z-moca-wsteczna"]);
        }

        // ============================================================
        // ADR-0014 — skróty jednostek w eId (levelType) z jednej zamkniętej tabeli
        // ============================================================

        /// <summary>Tabela z ADR-0014 (bez `zal` — model nie ma encji załącznika).</summary>
        private static readonly SortedDictionary<string, string> LevelTypeTable = new(StringComparer.Ordinal)
        {
            ["część"] = "czesc",
            ["księga"] = "ksi",
            ["tytuł"] = "tyt",
            ["dział"] = "dzial",
            ["rozdział"] = "rozdz",
            ["oddział"] = "oddz",
            ["artykuł"] = "art",
            ["ustęp"] = "ust",
            ["paragraf"] = "par",
            ["punkt"] = "pkt",
            ["litera"] = "lit",
            ["tiret"] = "tir",
            ["podwójny tiret"] = "dwtir",
            ["potrójny tiret"] = "trtir",
            ["poczwórny tiret"] = "cztir",
            ["pięciokrotny tiret"] = "pitir",
            ["załącznik"] = "zal",
            ["podpunkt obwieszczenia"] = "ppkt",
        };

        [Fact]
        public void Adr0014_UnitEIdPrefixes_FollowTheLevelTypeTable()
        {
            var codeParagraph = new Paragraph();
            ParsingFactories.ApplyCodeParagraphRole(codeParagraph, "§ 1. Treść.");

            var actual = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["część"] = new Part().EIdPrefix,
                ["księga"] = new Book().EIdPrefix,
                ["tytuł"] = new Title().EIdPrefix,
                ["dział"] = new Division().EIdPrefix,
                ["rozdział"] = new Chapter().EIdPrefix,
                ["oddział"] = new Subchapter().EIdPrefix,
                ["artykuł"] = new Article().EIdPrefix,
                ["ustęp"] = new Paragraph().EIdPrefix,
                ["paragraf"] = codeParagraph.EIdPrefix,
                ["punkt"] = new Point().EIdPrefix,
                ["litera"] = new Letter().EIdPrefix,
                ["tiret"] = Tiret.PrefixForDepth(1),
                ["podwójny tiret"] = Tiret.PrefixForDepth(2),
                ["potrójny tiret"] = Tiret.PrefixForDepth(3),
                ["poczwórny tiret"] = Tiret.PrefixForDepth(4),
                ["pięciokrotny tiret"] = Tiret.PrefixForDepth(5),
                ["załącznik"] = Annex.EIdPrefix,
                ["podpunkt obwieszczenia"] = Announcement.SubPointEIdPrefix,
            };

            Assert.Equal(LevelTypeTable, actual);
        }

        [Fact]
        public void Adr0014_ReferenceEId_UsesTheSameVocabulary()
        {
            // Adres jednostki wskazywanej (element-eId, cel wejścia w życie) — ten sam słownik co eId.
            var reference = new StructuralReference()
                .WithArticle("5").WithParagraph("2").WithPoint("3").WithLetter("a")
                .WithTiret("1.2").WithSentence("1");

            Assert.Equal("art_5__ust_2__pkt_3__lit_a__tir_1__dwtir_2__zdn_1", reference.ToString());
        }

        [Fact]
        public void Adr0014_CodeParagraphLevel_SurvivesDescentAndClone()
        {
            // Każda metoda With* poniżej poziomu ustępu (i Clone) musi przenieść to, że był to paragraf.
            var reference = new StructuralReference()
                .WithArticle("5").WithCodeParagraph("2").WithPoint("3").WithLetter("a")
                .WithTiret("1.2").WithSentence("1");

            const string expected = "art_5__par_2__pkt_3__lit_a__tir_1__dwtir_2__zdn_1";
            Assert.Equal(expected, reference.ToString());
            Assert.Equal(expected, reference.Clone().ToString());

            // …a nowy artykuł albo zwykły ustęp ją zerują.
            Assert.Equal("art_6", reference.WithArticle("6").ToString());
            Assert.Equal("art_5__ust_4", reference.WithParagraph("4").ToString());
        }

        [Fact]
        public void Adr0014_CodeParagraphReference_UsesPar()
        {
            // „§" po artykule to paragraf kodeksu także w adresie jednostki wskazywanej.
            var target = UnitEnumerationParser.Parse("art. 5 § 2").Targets.Single();

            Assert.Equal("art_5__par_2", target.Structure.ToString());
        }

        // ============================================================
        // ADR-0015 — powołania jednostek rozbiera jedno źródło
        // ============================================================

        [Fact]
        public void Adr0015_UnitKeywordVocabulary_IsDefinedInExactlyOnePlace()
        {
            // „lit\." w kodzie (nie w komentarzu) to odcisk palca własnego słownika powołań: litera
            // występuje wyłącznie w powołaniu jednostki, więc taki wzorzec poza tokenizerem to jego kopia.
            var sourceRoot = Path.Combine(TestFiles.GetRepositoryRoot(), "src");
            var owner = Path.Combine("UnitReferences", "UnitReferenceTokenizer.cs");

            var offenders = Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                               !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                               !file.EndsWith(owner, StringComparison.Ordinal))
                .Where(file => File.ReadLines(file)
                    .Select(line => line.TrimStart())
                    .Where(line => !line.StartsWith("//", StringComparison.Ordinal))
                    .Any(line => line.Contains(@"lit\.", StringComparison.Ordinal)))
                .Select(Path.GetFileName)
                .ToList();

            Assert.True(offenders.Count == 0,
                "Słownik powołań jednostek musi mieszkać wyłącznie w UnitReferences/UnitReferenceTokenizer.cs " +
                "(ADR-0015). Pliki z własnymi wzorcami: " + string.Join(", ", offenders));
        }

        [Fact]
        public void Adr0015_AmendmentTargetAndEntryIntoForce_ReadTheSameAddress()
        {
            // Ten sam adres — paragraf kodeksu, indeks górny, litera „i", tiret — czytany przez wejście
            // w życie i przez cel nowelizacji. Dopóki oba idą jednym źródłem, ścieżki są równe.
            const string address = "art. 328[11] § 2 pkt 3 lit. i tiret drugie";

            var entryIntoForce = UnitEnumerationParser.Parse(address).Targets.Single().Structure;
            var amendmentTarget = new LegalReferenceService()
                .ReadAmendmentTargets(null, $"w {address} otrzymuje brzmienie:").Single();

            // Oznaczenie odczytane wiernie, z indeksem górnym — także w eId (ADR-0019).
            Assert.Equal("328[11]", entryIntoForce.Article);
            Assert.Equal(entryIntoForce.Article, amendmentTarget.Structure.Article);
            Assert.Equal("art_328[11]__par_2__pkt_3__lit_i__tir_2", entryIntoForce.ToString());
            Assert.Equal(entryIntoForce.ToString(), amendmentTarget.ToString());
        }

        [Fact]
        public void Adr0015_BareSectionSign_IsDecidedByActType()
        {
            // W rozporządzeniu samodzielny „§" to jednostka główna; w ustawie — paragraf kodeksu
            // nieznanego artykułu, więc artykułu się nie zgaduje.
            string[] Read(LegalActType actType) =>
                UnitEnumerationParser.Parse("§ 1 pkt 3",
                        SectionSignRole.IsBasicUnit(new DocumentClassificationResult { ActType = actType }))
                    .Targets.Select(t => t.Structure.ToString()).ToArray();

            Assert.Equal(new[] { "art_1__pkt_3" }, Read(LegalActType.Regulation));
            Assert.DoesNotContain(Read(LegalActType.AmendingStatute), path => path.StartsWith("art_", StringComparison.Ordinal));
        }

        [Fact]
        public void Adr0035_SectionSignAfterASection_IsTheNextSection()
        {
            // O znaczeniu kolejnego „§" rozstrzyga to, czym wprowadzono jednostkę główną ścieżki:
            // po paragrafie rozporządzenia — kolejny paragraf, po jawnym artykule — paragraf kodeksu.
            static string[] Read(string text, bool sectionBased) =>
                UnitAddressReader.Read(text, null, sectionBased).Select(address => address.Start.ToString()).ToArray();

            Assert.Equal(new[] { "art_6", "art_7" }, Read("§ 6 i § 7 otrzymują brzmienie:", sectionBased: true));
            Assert.Equal(new[] { "art_3" }, Read("po § 3 dodaje się § 3a i § 3b w brzmieniu:", sectionBased: true));
            Assert.Equal(new[] { "art_5__par_2" }, Read("w art. 5 § 2 otrzymuje brzmienie:", sectionBased: true));
            Assert.Equal(
                new[] { "art_5__par_2", "art_5__par_3" },
                Read("w art. 5 § 2 i § 3 otrzymują brzmienie:", sectionBased: false));
        }

        // ============================================================
        // ADR-0016: wyliczenie celów to osobne komendy, element-do-eId — tylko przedział
        // ============================================================

        [Fact]
        public void Adr0016_EnumerationIsACommandPerTarget_RangeIsOneCommand()
        {
            // Korpus: „uchyla się pkt 31, 32 i 34-36;" — trzy cele, z czego jeden to przedział. Para
            // element-eId/element-do-eId nie może wrócić do znaczenia „pierwszy i ostatni cel listy".
            var amendment = new Amendment
            {
                OperationType = AmendmentOperationType.Repeal,
                Targets = new LegalReferenceService()
                    .ReadAmendmentTargets(new StructuralReference { Article = "2", Paragraph = "1" }, "uchyla się pkt 31, 32 i 34-36;")
                    .ToList(),
            };
            var sentence = new System.Xml.Linq.XElement("zdanie");

            Saga.Core.Services.Converters.AmendmentXmlConverter.ToXml(amendment, sentence);

            var instructions = sentence.Elements().ToList();
            Assert.Equal(
                new[] { "art_2__ust_1__pkt_31", "art_2__ust_1__pkt_32", "art_2__ust_1__pkt_34" },
                instructions.Select(i => i.Attribute("element-eId")?.Value));
            Assert.Equal(
                new string?[] { null, null, "art_2__ust_1__pkt_36" },
                instructions.Select(i => i.Attribute("element-do-eId")?.Value));
        }

        // ============================================================
        // ADR-0017: eId jednostki redakcyjnej bez ścieżki jednostek systematyzacyjnych
        // ============================================================

        [Fact]
        public void Adr0017_EditorialEId_IgnoresSystematizingPath_AndMatchesTheReferenceAddress()
        {
            var document = LegalDocumentParser.ParseBlocks(new[]
            {
                "Rozdział 3", "Przepisy szczegółowe", "Oddział 1", "Zasady",
                "Art. 5. 1. Organ prowadzi rejestr.", "2. Organ wydaje:", "1) decyzję,", "2) postanowienie.",
            }.Select(text => new DocumentBlock { Text = text }).ToList());

            var article = document.Articles.Single();
            var point = article.Paragraphs[1].Points[0];

            // Artykuł stoi w oddziale 1 rozdziału 3 — ta ścieżka należy do jednostek systematyzacyjnych,
            // nie do adresu artykułu.
            Assert.Equal("rozdz_3__oddz_1", article.Parent!.Id);
            Assert.Equal("art_5", article.Id);
            Assert.Equal("art_5__ust_2__pkt_1", point.Id);

            // Powołanie nie wymienia rozdziału — adres odczytany z „w art. 5 ust. 2 pkt 1" musi być
            // dokładnie eId wskazanej jednostki, inaczej element-eId nowelizacji nie trafi w cel.
            var reference = new LegalReferenceService()
                .ReadAmendmentTargets(null, "w art. 5 ust. 2 pkt 1 otrzymuje brzmienie:")
                .Single();
            Assert.Equal(point.Id, reference.Structure.ToString());
        }

        // ============================================================
        // ADR-0018: postać oznaczenia w eId
        // ============================================================

        [Theory]
        [InlineData("KSIĘGA I", "ksi_1", "I")]
        [InlineData("KSIĘGA PIERWSZA", "ksi_1", "PIERWSZA")]
        [InlineData("TYTUŁ XVII", "tyt_17", "XVII")]
        [InlineData("DZIAŁ IVa", "dzial_4a", "IVa")]
        [InlineData("Rozdział 2a", "rozdz_2a", "2a")]
        public void Adr0018_SystematizingDesignation_IsArabicInEId_AsWrittenInRawValue(
            string designation, string eId, string rawValue)
        {
            var document = LegalDocumentParser.ParseBlocks(new[] { designation, "Art. 1. Treść." }
                .Select(text => new DocumentBlock { Text = text }).ToList());

            var unit = document.Articles.Single().Parent!;
            while (((ISystematizingUnit)unit).IsImplicit)
                unit = unit.Parent!;

            Assert.Equal(eId, unit.Id);
            Assert.Equal(rawValue, unit.Number?.RawValue);
        }

        // ============================================================
        // ADR-0019: indeks górny w eId w notacji „[n]"
        // ============================================================

        [Fact]
        public void Adr0019_Superscript_IsInTheEId_InBracketNotation()
        {
            var article = ParsingFactories.ParseArticleNumber("Art. 5[1]. Treść.");
            Assert.Equal("art_5[1]", new Article { Number = article }.Id);

            // „art. 5 i 5[1]" — dwie jednostki, dwa cele, dwa różne adresy.
            var targets = new LegalReferenceService().ReadAmendmentTargets(null, "uchyla się art. 5 i 5[1];");
            Assert.Equal(new[] { "art_5", "art_5[1]" }, targets.Select(t => t.ToString()));
        }

        // ============================================================
        // ADR-0020: eksport jednostek systematyzacyjnych
        // ============================================================

        [Fact]
        public void Adr0020_Export_ElidesTheImplicitChainAboveTheFirstExplicitUnit()
        {
            static XElement Act(params string[] lines) => LegalDocumentConverter.Convert(LegalDocumentParser.ParseBlocks(
                    lines.Select(text => new DocumentBlock { Text = text }).ToList()))
                .Descendants().First(e => e.Name.LocalName == "ustawa");

            // Akt bez jawnych jednostek: artykuły wprost w akcie, żadnych pustych „-n".
            var flat = Act("Art. 1. Treść.");
            Assert.Single(flat.Elements(), e => e.Name.LocalName == "artykul");
            Assert.DoesNotContain(flat.Descendants(), e => e.Name.LocalName is "czesc-n" or "ksiega-n" or "tytul-n-jednostka"
                or "dzial-n" or "rozdzial-n" or "oddzial-n");

            // Pod jawnym rozdziałem niejawny oddział wychodzi jako „-n" bez eId — rozdział nie trzyma artykułów.
            var chapter = Act("Rozdział 1", "Przepisy ogólne", "Art. 1. Treść.")
                .Elements().Single(e => e.Name.LocalName == "rozdzial");
            var subchapter = chapter.Elements().Single(e => e.Name.LocalName == "oddzial-n");
            Assert.Null(subchapter.Attribute("eId"));
            Assert.Equal("jednostka-nieoznaczona", subchapter.Attribute("brak-eid")?.Value);
            Assert.Equal("artykul", subchapter.Elements().Single().Name.LocalName);
        }

        // ============================================================
        // ADR-0021: eId jednostki cytowanej od adresu celu w akcie zmienianym
        // ============================================================

        [Fact]
        public void Adr0021_QuotedUnitEId_StartsWithTheTargetAddress()
        {
            var document = LegalDocumentParser.ParseBlocks(new[]
            {
                "Art. 1. W ustawie z dnia 13 października 1998 r. o systemie (Dz. U. z 2025 r. poz. 350) wprowadza się następujące zmiany:",
                "1) w art. 68 ust. 1 pkt 5 otrzymuje brzmienie:",
                "„5) orzekanie o niezdolności do pracy;”;",
                "2) w art. 2 po pkt 4 dodaje się pkt 4a w brzmieniu:",
                "„4a) nowy punkt;”.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
            }.Select(text => new DocumentBlock { Text = text }).ToList());

            var points = document.Articles.First().Paragraphs[0].Points;
            var modified = points[0].Amendment!;
            var inserted = points[1].Amendment!;

            // Zmiana brzmienia: eId jednostki cytowanej to dokładnie element-eId komendy.
            Assert.Equal("art_68__ust_1__pkt_5", modified.Content!.Points.Single().Id);
            Assert.Equal(modified.Targets.Single().Structure.ToString(), modified.Content.Points.Single().Id);
            Assert.Equal("art_2__pkt_4a", inserted.Content!.Points.Single().Id);
        }

        // ============================================================
        // ADR-0022: daty aktu w metadanych jako zdarzenia cyklu życia
        // ============================================================

        [Fact]
        public void Adr0022_ActDates_AreLifecycleEvents_NotAdHocMetadataElements()
        {
            var blocks = new List<DocumentBlock>
            {
                new() { Text = "DZIENNIK USTAW RZECZYPOSPOLITEJ POLSKIEJ", Role = BlockRole.JournalHeader },
                new() { Text = "Warszawa, dnia 13 stycznia 2026 r.", Role = BlockRole.JournalHeader },
                new() { Text = "Poz. 26", Role = BlockRole.JournalHeader },
            };
            blocks.AddRange(new[]
            {
                "USTAWA", "z dnia 9 stycznia 2026 r.", "o zmianie ustawy o rzeczach różnych",
                "Art. 1. Wprowadza się zmiany porządkowe.",
                "Art. 2. Przepis przejściowy.",
                "Art. 3. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia, z wyjątkiem art. 2, " +
                "który wchodzi w życie z dniem 1 stycznia 2027 r.",
            }.Select(text => new DocumentBlock { Text = text }));

            var document = LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
            var metadata = LegalDocumentConverter.Convert(document).Descendants().First(e => e.Name.LocalName == "metadane");

            Assert.DoesNotContain(metadata.Elements(), e => e.Name.LocalName is "DataWejsciaWZycie" or "DatyMocyWstecznej");

            var events = metadata.Descendants().Where(e => e.Name.LocalName == "Zdarzenie").ToList();
            Assert.Equal(new[] { "zdarz_wejscie_2026-01-28_DU-2026-26_domyslne", "zdarz_wejscie_2027-01-01_DU-2026-26_odczytane" },
                events.Select(e => e.Attribute("id")?.Value));

            // Reguła ogólna nie wymienia jednostek; wyjątek wymienia swoje.
            Assert.Empty(events[0].Elements());
            Assert.Equal(new[] { "DU/2026/26__art_2" }, events[1].Elements().Select(e => e.Attribute("eId")?.Value));

            // Data z formuły niesie formułę; każda — klauzulę, z której pochodzi.
            Assert.Equal("po upływie 14 dni od dnia ogłoszenia", events[0].Attribute("warunek")?.Value);
            Assert.Null(events[1].Attribute("warunek"));
            Assert.All(events, e => Assert.Equal("DU/2026/26__art_3", e.Attribute("dowod")?.Value));
        }

        // ============================================================
        // ADR-0023: poziom pominięty w akcie to jednostka niejawna
        // ============================================================

        [Fact]
        public void Adr0023_SkippedLevel_IsAnImplicitUnit_AbsentFromEIdAndExport()
        {
            var document = LegalDocumentParser.ParseBlocks(new[]
            {
                "Art. 1. 1. Ustawa określa:",
                "a) zasady ochrony,",
                "b) środki ochrony.",
                "2. Przepisy stosuje się do pracodawców.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
            }.Select(text => new DocumentBlock { Text = text }).ToList());

            // Model: litery stoją w punkcie, ale punktu nie ma w tekście — jest niejawny i bez oznaczenia.
            var paragraph = document.Articles.First().Paragraphs[0];
            var point = Assert.Single(paragraph.Points);
            Assert.True(point.IsImplicit);
            Assert.Null(point.Number);

            // eId: adres litery jest powołaniem z innego przepisu („art. 1 ust. 1 lit. a").
            Assert.Equal(new[] { "art_1__ust_1__lit_a", "art_1__ust_1__lit_b" }, point.Letters.Select(l => l.Id));

            // Eksport: punkt nie ma elementu, litery stoją wprost w ustępie spoza ZTP.
            var exported = LegalDocumentConverter.Convert(document).Descendants()
                .Single(e => e.Attribute("eId")?.Value == "art_1__ust_1");
            Assert.Equal("nie-ztp-ustep", exported.Name.LocalName);
            Assert.Equal(new[] { "nr", "p", "nie-ztp-litera", "nie-ztp-litera" }, exported.Elements().Select(e => e.Name.LocalName));
        }

        // ============================================================
        // ADR-0024: jednostka bez odczytanego oznaczenia nie ma eId
        // ============================================================

        [Fact]
        public void Adr0024_UnitWithoutADesignation_HasNoEId_OnlyAReason()
        {
            // Pierwszy akapit artykułu, po którym stoi od razu „2." — oznaczenia nie ma, a jednostką jest.
            // (Akapit w stylu ustępu bez oznaczenia za inną jednostką jest od ADR-0039 jej dalszą treścią.)
            var document = LegalDocumentParser.ParseBlocks(
            [
                new() { Text = "Art. 1. Opłatę oblicza się od:" },
                new() { Text = "1) kwoty pożyczki;" },
                new() { Text = "2) liczby dni w roku." },
                new() { Text = "2. Opłaty nie pobiera się od osób fizycznych." },
            ]);

            // Model: ustęp bez numeru jest w adresie przezroczysty — jego punkty adresują się od artykułu.
            var unnumbered = document.Articles.Single().Paragraphs[0];
            Assert.Null(unnumbered.Number);
            Assert.Equal("art_1", unnumbered.Id);
            Assert.Equal(new[] { "art_1__pkt_1", "art_1__pkt_2" }, unnumbered.Points.Select(p => p.Id));

            // Eksport: bez eId i bez udawanego oznaczenia, za to z nazwanym powodem.
            var exported = LegalDocumentConverter.Convert(document).Descendants()
                .Single(e => e.Name.LocalName == "nie-ztp-ustep");
            Assert.Null(exported.Attribute("eId"));
            Assert.Equal("jednostka-nieoznaczona", exported.Attribute("brak-eid")?.Value);
            Assert.Equal(string.Empty, exported.Elements().First(e => e.Name.LocalName == "nr").Value);
        }

        // ============================================================
        // ADR-0039: akapit w stylu jednostki bez oznaczenia jest dalszą treścią jednostki, za którą stoi
        // ============================================================

        [Fact]
        public void Adr0039_UnitStyleWithoutADesignation_ContinuesTheUnitItFollows()
        {
            // Styl mówi „ustęp", treść nie ma oznaczenia: to dalszy akapit ustępu 1, nie ustęp bez numeru.
            // Wyliczenie za nim należy do ustępu 1 i tak jest adresowane.
            var document = LegalDocumentParser.ParseBlocks(
            [
                new() { Text = "Art. 1. 1. Opłatę oblicza się według wzoru." },
                new() { Text = "w którym poszczególne symbole oznaczają:", StyleId = "UST" },
                new() { Text = "1) K - kwotę pożyczki;" },
                new() { Text = "2) R - liczbę dni w roku." },
                new() { Text = "2. Opłaty nie pobiera się od osób fizycznych." },
            ]);

            var paragraphs = document.Articles.Single().Paragraphs;
            Assert.Equal(new[] { "1", "2" }, paragraphs.Select(paragraph => paragraph.Number?.Value));
            Assert.Equal("w którym poszczególne symbole oznaczają:", Assert.Single(paragraphs[0].Continuation).Text);
            Assert.Equal(new[] { "art_1__ust_1__pkt_1", "art_1__ust_1__pkt_2" }, paragraphs[0].Points.Select(point => point.Id));

            var exported = LegalDocumentConverter.Convert(document);
            Assert.DoesNotContain(exported.Descendants(), e => e.Name.LocalName.StartsWith("nie-ztp-", StringComparison.Ordinal));

            // Oznaczenie spoza wzorca to nadal jednostka — bez odczytanego oznaczenia (ADR-0024).
            var irregular = LegalDocumentParser.ParseBlocks(
            [
                new() { Text = "Art. 1. 1. Opłatę wnosi:" },
                new() { Text = "1) osoba fizyczna;" },
                new() { Text = "A) osoba prawna.", StyleId = "PKT" },
            ]);
            Assert.Equal(2, irregular.Articles.Single().Paragraphs[0].Points.Count);
        }

        // ============================================================
        // ADR-0025: część wspólna zamykająca wyliczenie ma eId gospodarza z segmentem „czw"
        // ============================================================

        [Fact]
        public void Adr0025_ClosingCommonPart_IsAddressedByItsHostAndCzw()
        {
            var document = LegalDocumentParser.ParseBlocks(new[]
            {
                "Art. 1. 1. Sygnalistą jest osoba, która:",
                "1) zgłasza naruszenie prawa,",
                "2) ujawnia naruszenie prawa",
                "– w kontekście związanym z pracą.",
                "2. Przepis stosuje się odpowiednio.",
                "Art. 2. Organem właściwym jest:",
                "1) minister,",
                "2) wojewoda",
                "– zwany dalej organem.",
            }.Select(text => new DocumentBlock { Text = text }).ToList());

            var closing = LegalDocumentConverter.Convert(document).Descendants()
                .Where(e => e.Name.LocalName == "czesc-wspolna-punkt")
                .Select(e => e.Attribute("eId")?.Value);

            // W artykule z jednym ustępem gospodarzem w adresie jest artykuł — ustęp nie ma oznaczenia.
            Assert.Equal(new[] { "art_1__ust_1__czw", "art_2__czw" }, closing);
        }

        // ============================================================
        // ADR-0026: treść odnośnika w rejestrze dokumentu, miejsce w tekście jednostki
        // ============================================================

        [Fact]
        public void Adr0026_Footnote_LivesInTheRegistry_AndIsExportedAtItsMarker()
        {
            using var stream = new MemoryStream(DocxFixtures.WithFootnotes(
                ["Zmiany wymienionej ustawy zostały ogłoszone w Dz. U. poz. 2."],
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o ochronie sygnalistów",
                "Art. 1. Ustawa (Dz. U. poz. 1, z późn. zm.{1}) określa zasady.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia."));
            var document = LegalDocumentParser.Parse(stream, "ustawa.docx",
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

            // Model: treść w rejestrze, miejsce w tekście jednostki — znacznik z numerem jak w akcie.
            Assert.Equal("1", Assert.Single(document.Footnotes).Symbol);
            Assert.Contains("z późn. zm.[1)])", document.Articles.First().Paragraphs.Single().ContentText);

            // Eksport: element w miejscu znacznika, a znacznika w tekście już nie ma.
            var sentence = LegalDocumentConverter.Convert(document).Descendants()
                .First(e => e.Name.LocalName == "zdanie");
            Assert.Equal("odnosnik", Assert.Single(sentence.Elements()).Name.LocalName);
            Assert.DoesNotContain("[", string.Concat(sentence.Nodes().OfType<XText>().Select(t => t.Value)));
        }

        // ============================================================
        // ADR-0027: prefiks aktu w eId dopisuje eksport, model trzyma adres w akcie
        // ============================================================

        private static readonly string[] Adr0027Act =
        [
            "USTAWA", "z dnia 9 stycznia 2026 r.", "o zmianie ustawy o świadczeniach",
            "Art. 1. W ustawie z dnia 25 czerwca 1999 r. o świadczeniach (Dz. U. z 2025 r. poz. 350) wprowadza się następujące zmiany:",
            "1) w art. 68 ust. 1 pkt 5 otrzymuje brzmienie:",
            "„5) orzekanie o niezdolności do pracy;”;",
            "2) uchyla się art. 70.",
            "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
        ];

        [Fact]
        public void Adr0027_ActPrefix_IsAddedByTheExport_AndQuotedUnitsCarryTheAmendedAct()
        {
            var blocks = new List<DocumentBlock>
            {
                new() { Text = "DZIENNIK USTAW RZECZYPOSPOLITEJ POLSKIEJ", Role = BlockRole.JournalHeader },
                new() { Text = "Warszawa, dnia 13 stycznia 2026 r.", Role = BlockRole.JournalHeader },
                new() { Text = "Poz. 26", Role = BlockRole.JournalHeader },
            };
            blocks.AddRange(Adr0027Act.Select(text => new DocumentBlock { Text = text }));
            var document = LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

            // Model: adres w akcie — taki sam bez względu na to, gdzie akt ogłoszono.
            var point = document.Articles.First().Paragraphs[0].Points[0];
            Assert.Equal("art_1__pkt_1", point.Id);
            Assert.Equal("art_68__ust_1__pkt_5", point.Amendment!.Content!.Points.Single().Id);

            var xml = LegalDocumentConverter.Convert(document);
            string?[] Values(string element, string attribute) => xml.Descendants()
                .Where(e => e.Name.LocalName == element)
                .Select(e => e.Attribute(attribute)?.Value)
                .ToArray();

            // Eksport: jednostki aktu niosą jego prefiks…
            Assert.Equal(new[] { "DU/2026/26__art_1", "DU/2026/26__art_2" }, Values("artykul", "eId"));

            // …a jednostka cytowana i cele komend — prefiks aktu zmienianego.
            Assert.Equal(new[] { "DU/2025/350__art_68__ust_1__pkt_5" }, Values("cytat-strukt", "element-eId"));
            Assert.Equal(new[] { "DU/2025/350__art_70" }, Values("uchyl", "element-eId"));
            Assert.Equal("DU/2025/350__art_68__ust_1__pkt_5",
                xml.Descendants().Single(e => e.Name.LocalName == "cytat-strukt").Elements().Single().Attribute("eId")?.Value);
        }

        [Fact]
        public void Adr0027_ActWithoutAKnownJournal_KeepsAddressesWithoutAPrefix()
        {
            // Projekt albo tekst bez nagłówka dziennika: nie ma czym poprzedzić adresu — prefiksu się nie zmyśla.
            var document = LegalDocumentParser.ParseBlocks(
                Adr0027Act.Select(text => new DocumentBlock { Text = text }).ToList());

            var xml = LegalDocumentConverter.Convert(document);

            Assert.Equal(new[] { "art_1", "art_2" },
                xml.Descendants().Where(e => e.Name.LocalName == "artykul").Select(e => e.Attribute("eId")?.Value));
            // Akt zmieniany jest znany z powołania publikatora, więc cytat ma prefiks także tutaj.
            Assert.Equal("DU/2025/350__art_68__ust_1__pkt_5",
                xml.Descendants().Single(e => e.Name.LocalName == "cytat-strukt").Attribute("element-eId")?.Value);
        }

        // ============================================================
        // ADR-0028: wyliczenie w załączniku buduje potok aktu, pod artykułem zastępczym
        // ============================================================

        [Fact]
        public void Adr0028_AnnexEnumeration_IsBuiltByTheActPipeline_OrLeftAsParagraphs()
        {
            string[] act =
            [
                "USTAWA", "z dnia 23 stycznia 2026 r.", "o krajowym systemie cyberbezpieczeństwa",
                "Art. 1. Wymogi określają załączniki do ustawy.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
                "Załączniki do ustawy z dnia 23 stycznia 2026 r.",
                "Załącznik nr 1",
                "I. System zarządzania obejmuje co najmniej:",
                "1) inwentaryzację produktów;",
                "2) kontrolowanie wersji.",
                "Załącznik nr 2",
                "Wykaz sektorów ustala się odrębnie.",
                "1) energia;",
                "2) transport.",
            ];
            var document = LegalDocumentParser.ParseBlocks(act.Select(text => new DocumentBlock { Text = text }).ToList());

            // Treść będąca w całości wyliczeniem: jednostki adresowane od załącznika, oznaczenie rzymskie jako ustęp.
            var unit = Assert.Single(document.Annexes[0].Content).Unit!;
            Assert.Equal("zal_1__ust_I", unit.Id);
            Assert.Equal(new[] { "zal_1__ust_I__pkt_1", "zal_1__ust_I__pkt_2" }, unit.Points.Select(p => p.Id));

            // Wszystko albo nic: akapit spoza wyliczenia zostawia całą treść akapitami — nic nie ginie.
            Assert.Equal(
                new[] { "Wykaz sektorów ustala się odrębnie.", "1) energia;", "2) transport." },
                document.Annexes[1].Content.Select(block => block.Text));

            // Załącznik nie wchodzi w strukturę aktu.
            Assert.Equal(new[] { "art_1", "art_2" }, document.Articles.Select(a => a.Id));
        }

        // ============================================================
        // ADR-0029: akapit bez oznaczenia i tabela są dalszą treścią jednostki
        // ============================================================

        [Fact]
        public void Adr0029_UndesignatedParagraphAndTable_ContinueTheirUnit()
        {
            static string P(string text) => $"<w:p><w:r><w:t>{text}</w:t></w:r></w:p>";
            static string Row(params string[] cells) =>
                $"<w:tr>{string.Concat(cells.Select(cell => $"<w:tc><w:tcPr/>{P(cell)}</w:tc>"))}</w:tr>";

            var body =
                P("USTAWA") + P("z dnia 14 czerwca 2024 r.") + P("o opłatach") +
                P("Art. 1. 1. Wysokość opłaty oblicza się według wzoru:") +
                P("gdzie poszczególne symbole oznaczają:") +
                P("2. Stawki opłat określa tabela:") +
                $"<w:tbl>{Row("1.", "Rozpatrzenie wniosku", "1500")}{Row("2.", "Wydanie potwierdzenia", "500")}</w:tbl>" +
                P("Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

            using var stream = new MemoryStream(DocxFixtures.WithBodyXml(body));
            var document = LegalDocumentParser.Parse(stream, "ustawa.docx",
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

            // Ani akapit bez oznaczenia, ani komórki tabeli nie stają się jednostkami i nie giną.
            var paragraphs = document.Articles.First().Paragraphs;
            Assert.Equal(new[] { "art_1__ust_1", "art_1__ust_2" }, paragraphs.Select(p => p.Id));
            Assert.Equal("gdzie poszczególne symbole oznaczają:", Assert.Single(paragraphs[0].Continuation).Text);
            Assert.Equal(2, Assert.Single(paragraphs[1].Continuation).Table!.Rows.Count);

            // W eksporcie dalsza treść stoi tuż za zdaniami jednostki.
            var exported = LegalDocumentConverter.Convert(document).Descendants()
                .Where(e => e.Name.LocalName == "ustep")
                .Select(e => string.Join(",", e.Elements().Select(child => child.Name.LocalName)))
                .ToList();
            Assert.Equal(new[] { "nr,p,fragment", "nr,p,tabela" }, exported);
        }

        // ============================================================
        // ADR-0030: warianty cudzysłowu rozpoznaje jedno źródło
        // ============================================================

        [Fact]
        public void Adr0030_QuotationMarkCharacters_AreDefinedInExactlyOnePlace()
        {
            // Cudzysłów typograficzny jako literał znakowy, w klasie znaków wzorca albo jako \uXXXX to własny
            // zestaw wariantów — kolejny wariant (jak U+02EE) trzeba by wtedy dopisywać w każdym z osobna.
            var ownSet = new System.Text.RegularExpressions.Regex(
                @"'[„”“ˮ]'|\[[^\]]*[„”“ˮ][^\]]*\]|\\u(?:201[CDEcde]|02[Ee][Ee])");
            var servicesRoot = Path.Combine(TestFiles.GetRepositoryRoot(), "src", "Saga.Core", "Services");

            var offenders = Directory.EnumerateFiles(servicesRoot, "*.cs", SearchOption.AllDirectories)
                .Where(file => Path.GetFileName(file) != "QuotationMarks.cs")
                .Where(file => File.ReadLines(file)
                    .Select(line => line.TrimStart())
                    .Where(line => !line.StartsWith("//", StringComparison.Ordinal))
                    .Any(line => ownSet.IsMatch(line)))
                .Select(Path.GetFileName)
                .ToList();

            Assert.True(offenders.Count == 0,
                "Warianty cudzysłowu muszą pochodzić z Services/QuotationMarks.cs (ADR-0030). " +
                "Pliki z własnymi zestawami: " + string.Join(", ", offenders));
        }

        [Fact]
        public void Adr0030_EveryClosingVariant_ClosesTheQuotationAndIsTakenOffItsEdge()
        {
            // Tekstu się nie normalizuje, więc każdy wariant musi działać sam: zamknąć bilans cudzysłowów
            // i zejść z granicy cytatu razem z interpunkcją.
            foreach (var closing in QuotationMarks.Closing)
            {
                var tracker = new QuoteBalanceTracker();
                tracker.Observe($"„1. Nowe brzmienie ustępu.{closing};", hasStyle: false);
                Assert.True(tracker.ClosureReached, $"U+{(int)closing:X4} nie zamyka bilansu");

                var collector = new AmendmentCollector();
                collector.AddParagraph($"„1. Nowe brzmienie ustępu.{closing},", null);
                var quotes = AmendmentStateManager.TrimQuoteBoundaries(collector);

                Assert.True(quotes.HasClosing, $"U+{(int)closing:X4} zostaje w treści");
                Assert.Equal(",", quotes.Punctuation);
                Assert.Equal("1. Nowe brzmienie ustępu.", collector.Paragraphs[0].Text);
            }
        }

        // ============================================================
        // ADR-0040: warianty myślnika z jednego źródła
        // ============================================================

        [Fact]
        public void Adr0040_DashCharacters_AreDefinedInExactlyOnePlace()
        {
            // Myślnik typograficzny w klasie znaków wzorca, jako literał znakowy albo kodem w nawiasie alternatywy
            // to własny zestaw wariantów — kolejny wariant trzeba by wtedy dopisywać w każdym miejscu z osobna.
            var typographic = "[" + string.Concat(Dashes.Characters.Where(dash => dash != '-')) + "]";
            var ownSet = new System.Text.RegularExpressions.Regex(
                @"\[[^\]\n]*" + typographic + @"[^\]\n]*\]|'" + typographic + @"'|'\\u(?:201[0-5]|2212)'|\(\\u201[0-5]\|");
            var servicesRoot = Path.Combine(TestFiles.GetRepositoryRoot(), "src", "Saga.Core", "Services");

            var offenders = Directory.EnumerateFiles(servicesRoot, "*.cs", SearchOption.AllDirectories)
                .Where(file => Path.GetFileName(file) != "Dashes.cs")
                .Where(file => File.ReadLines(file)
                    .Select(line => line.TrimStart())
                    .Where(line => !line.StartsWith("//", StringComparison.Ordinal))
                    .Any(line => ownSet.IsMatch(line)))
                .Select(Path.GetFileName)
                .ToList();

            Assert.True(offenders.Count == 0,
                "Warianty myślnika muszą pochodzić z Services/Dashes.cs (ADR-0040). " +
                "Pliki z własnymi zestawami: " + string.Join(", ", offenders));
        }

        [Fact]
        public void Adr0040_EveryDashVariant_MarksATiret()
        {
            // Tekstu się nie normalizuje, więc każdy wariant musi działać sam: otworzyć tiret i zejść z jego treści.
            foreach (var dash in Dashes.Characters)
            {
                var text = $"{dash} treść tiretu,";
                Assert.True(Saga.Core.Services.Classify.ParagraphClassifier.IsTiretByText(text), $"U+{(int)dash:X4} nie otwiera tiretu");
                Assert.Equal("treść tiretu,", Saga.Core.Services.Classify.TiretMarker.Strip(text));
                Assert.True(Dashes.IsDash(dash));
            }
        }

        // ============================================================
        // ADR-0041: organ, który sam podpisuje akt, jest jednym organem wydającym
        // ============================================================

        [Fact]
        public void Adr0041_AuthorityThatIssuesAndSigns_IsOneIssuingAuthority()
        {
            static List<(string? Role, string Name, bool Signs)> Authorities(string issuer, string signature)
            {
                string[] lines =
                [
                    "ROZPORZĄDZENIE",
                    issuer,
                    "z dnia 14 czerwca 2024 r.",
                    "w sprawie wzoru zgłoszenia",
                    "Na podstawie art. 5 ustawy z dnia 1 lutego 2021 r. o zgłoszeniach (Dz. U. poz. 200) zarządza się, co następuje:",
                    "§ 1. Rozporządzenie określa wzór zgłoszenia.",
                    "§ 2. Rozporządzenie wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
                    signature,
                ];
                var document = LegalDocumentParser.Parse(
                    lines.Select(line => new DocumentBlock { Text = line }).ToList(),
                    new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
                return LegalDocumentConverter.Convert(document).Descendants()
                    .Where(e => e.Name.LocalName == "Organ")
                    .Select(e => (
                        e.Attribute("rola")?.Value,
                        e.Elements().Single(child => child.Name.LocalName == "Nazwa").Value,
                        e.Elements().Any(child => child.Name.LocalName == "OsobaPodpisujaca")))
                    .ToList();
            }

            // Organ ze strefy tytułowej podpisuje sam: jeden organ wydający z osobą, nie para wydający–podpisujący.
            Assert.Equal(
                new (string?, string, bool)[] { ("wydajacy", "Minister Finansów", true) },
                Authorities("MINISTRA FINANSÓW", "Minister Finansów: A. Domański"));

            // Organ kolegialny podpisuje jego przewodniczący: dwa organy, a wydającym nie zostaje podpisujący.
            Assert.Equal(
                new (string?, string, bool)[] { ("wydajacy", "Rada Ministrów", false), ("podpisujacy", "Prezes Rady Ministrów", true) },
                Authorities("RADY MINISTRÓW", "Prezes Rady Ministrów: D. Tusk"));
        }

        // ============================================================
        // ADR-0042: myślnik bez wprowadzenia z dwukropkiem nie otwiera tiretu
        // ============================================================

        [Fact]
        public void Adr0042_DashWithoutAColonIntroduction_ContinuesTheParagraph_UnlessStyledAsATiret()
        {
            static Saga.Model.EditorialUnits.Paragraph Parsed(string introduction, string? dashStyle)
            {
                DocumentBlock[] blocks =
                [
                    new() { Text = "USTAWA" }, new() { Text = "z dnia 14 czerwca 2024 r." }, new() { Text = "o opłatach" },
                    new() { Text = "Art. 1. 1. " + introduction },
                    new() { Text = "– wymierza się karę pieniężną w wysokości 1000 zł.", StyleId = dashStyle },
                    new() { Text = "2. Karę uiszcza się w terminie 14 dni." },
                ];
                return LegalDocumentParser.Parse(blocks.ToList(), new ParseOptions { Policy = ParsePolicy.AlwaysParse })
                    .Document!.Articles.Single().Paragraphs.First();
            }

            // Bez dwukropka i bez stylu tiretu: dalszy ciąg zdania — żadnego dostawionego punktu ani litery.
            var continued = Parsed("Za niedołączenie dokumentów, o których mowa w art. 5 ust. 7", null);
            Assert.Empty(continued.Points);
            Assert.EndsWith("– wymierza się karę pieniężną w wysokości 1000 zł.", continued.ContentText);

            // Wprowadzenie z dwukropkiem (§ 56 ZTP) albo styl tiretu: tiret.
            Assert.Single(Parsed("Za niedołączenie dokumentów:", null).Points);
            Assert.Single(Parsed("Za niedołączenie dokumentów, o których mowa w art. 5 ust. 7", "TIRtiret").Points);
        }

        // ============================================================
        // ADR-0043: obwieszczenie, które nie ogłasza aktu, jest korzeniem
        // ============================================================

        [Fact]
        public void Adr0043_AnnouncementThatAnnouncesNoAct_IsTheRoot_AndItsAnnexIsNotAnAct()
        {
            string[] lines =
            [
                "OBWIESZCZENIE",
                "MINISTRA INFRASTRUKTURY",
                "z dnia 25 lipca 2025 r.",
                "w sprawie wysokości stawki opłaty legalizacyjnej",
                "Na podstawie art. 190 ust. 9 ustawy z dnia 20 lipca 2017 r. – Prawo wodne (Dz. U. z 2025 r. poz. 960) ogłasza się stawkę opłaty w załączniku do obwieszczenia.",
                "Minister Infrastruktury: P. Koperski",
                "Załącznik do obwieszczenia Ministra Infrastruktury z dnia 25 lipca 2025 r. (M.P. poz. 702)",
                "Stawka opłaty legalizacyjnej wynosi 1125,64 zł.",
            ];
            var document = LegalDocumentParser.Parse(
                lines.Select(line => new DocumentBlock { Text = line }).ToList(),
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

            // Załącznik nie zaczyna się rodzajem aktu, a przedmiot nie mówi o tekście jednolitym: aktu nie ma.
            Assert.False(document.Announcement!.AnnouncesAct);
            Assert.Single(document.Annexes);

            var root = LegalDocumentConverter.Convert(document).Elements().Single();
            Assert.Equal("obwieszczenie", root.Name.LocalName);
            Assert.Equal(
                new[] { "metadane", "rodzaj-aktu", "autor-aktu", "data", "tytul", "podstawa-prawna", "organ-podpisujacy", "zalacznik" },
                root.Elements().Select(e => e.Name.LocalName));
        }

        // ============================================================
        // ADR-0031: tekst jednolity pod korzeniem „obwieszczenie"
        // ============================================================

        [Fact]
        public void Adr0031_ConsolidatedStatute_IsExportedUnderTheAnnouncement_WithoutItsQuotedProvisions()
        {
            // Przepis przytoczony w obwieszczeniu („…, który stanowi: „Art. 57. …"") nie jest artykułem
            // ogłaszanego aktu, a akt stoi pod obwieszczeniem z własną strefą tytułową.
            string[] lines =
            [
                "Obwieszczenie",
                "Marszałka Sejmu Rzeczypospolitej Polskiej",
                "z dnia 6 sierpnia 2025 r.",
                "w sprawie ogłoszenia jednolitego tekstu ustawy o odbudowie obiektów budowlanych",
                "1. Podany w załączniku do niniejszego obwieszczenia tekst jednolity ustawy nie obejmuje art. 57 ustawy z dnia 1 października 2024 r. o zmianie ustawy (Dz. U. poz. 1473), który stanowi:",
                "„Art. 57. Ustawa wchodzi w życie z dniem następującym po dniu ogłoszenia.”",
                "Załącznik do obwieszczenia Marszałka Sejmu Rzeczypospolitej Polskiej z dnia 6 sierpnia 2025 r. (Dz. U. poz. 1094)",
                "USTAWA",
                "z dnia 11 sierpnia 2001 r.",
                "o odbudowie obiektów budowlanych",
                "Art. 1. Ustawa reguluje zasady odbudowy.",
            ];
            var document = LegalDocumentParser.Parse(
                lines.Select(line => new DocumentBlock { Text = line }).ToList(),
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

            Assert.Equal(new[] { "1" }, document.Articles.Select(article => article.Number?.Value));
            Assert.Equal("Obwieszczenie", document.KindLine);
            Assert.Equal("USTAWA", document.Announcement!.ActKindLine);

            var announcement = Assert.Single(LegalDocumentConverter.Convert(document).Elements());
            Assert.Equal("obwieszczenie", announcement.Name.LocalName);
            var act = Assert.Single(announcement.Elements(), element => element.Name.LocalName == "ustawa");
            Assert.Equal(
                new[] { "zalacznik-do", "rodzaj-aktu", "data", "tytul", "artykul" },
                act.Elements().Select(element => element.Name.LocalName));
        }

        // ============================================================
        // ADR-0032: jednostki obwieszczenia adresowane od obwieszczenia
        // ============================================================

        [Fact]
        public void Adr0032_AnnouncementUnits_AreAddressedFromTheAnnouncement()
        {
            // Punkt i podpunkt obwieszczenia nie są ustępem ani punktem ogłaszanego aktu: mają własne elementy
            // i adresy bez segmentu aktu, a przytoczony „Art. 57." z „1)" w środku nie staje się jednostką.
            string[] lines =
            [
                "Obwieszczenie",
                "Marszałka Sejmu Rzeczypospolitej Polskiej",
                "z dnia 6 sierpnia 2025 r.",
                "w sprawie ogłoszenia jednolitego tekstu ustawy o odbudowie obiektów budowlanych",
                "1. Podany w załączniku do niniejszego obwieszczenia tekst jednolity ustawy nie obejmuje:",
                "1) art. 57 ustawy z dnia 1 października 2024 r. o zmianie ustawy (Dz. U. poz. 1473), który stanowi:",
                "„Art. 57. Przepisy stosuje się do:",
                "1) odbudowy,",
                "2) remontu.”;",
                "2) art. 9 ustawy z dnia 9 maja 2025 r. o zmianie ustawy (Dz. U. poz. 680), który stanowi:",
                "„Art. 9. Ustawa wchodzi w życie z dniem następującym po dniu ogłoszenia.”.",
                "Załącznik do obwieszczenia Marszałka Sejmu Rzeczypospolitej Polskiej z dnia 6 sierpnia 2025 r. (Dz. U. poz. 1094)",
                "USTAWA",
                "z dnia 11 sierpnia 2001 r.",
                "o odbudowie obiektów budowlanych",
                "Art. 1. Ustawa reguluje zasady odbudowy.",
            ];
            var document = LegalDocumentParser.Parse(
                lines.Select(line => new DocumentBlock { Text = line }).ToList(),
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

            var point = Assert.Single(document.Announcement!.Points);
            Assert.Equal("pkt_1", point.Id);
            Assert.Equal(new[] { "pkt_1__ppkt_1", "pkt_1__ppkt_2" }, point.Points.Select(subPoint => subPoint.Id));

            // Przytoczenie jest dalszą treścią podpunktu — od ADR-0036 jako jednostki, nie akapity tekstu.
            Assert.NotNull(Assert.Single(point.Points[0].Continuation).Quotation);

            var exported = LegalDocumentConverter.Convert(document).Descendants()
                .Where(element => element.Name.LocalName is "punkt-OTJ" or "podpunkt-OTJ")
                .Select(element => $"{element.Name.LocalName} {element.Attribute("eId")?.Value}");
            Assert.Equal(new[] { "punkt-OTJ pkt_1", "podpunkt-OTJ pkt_1__ppkt_1", "podpunkt-OTJ pkt_1__ppkt_2" }, exported);
        }

        // ============================================================
        // ADR-0036: przytoczone artykuły to jednostki adresowane w akcie przytaczanym
        // ============================================================

        [Fact]
        public void Adr0036_QuotedArticles_AreUnitsOfTheQuotedAct_OtherQuotationsStayText()
        {
            static XElement Export(params string[] quoted)
            {
                string[] lines =
                [
                    "Obwieszczenie",
                    "Marszałka Sejmu Rzeczypospolitej Polskiej",
                    "z dnia 6 sierpnia 2025 r.",
                    "w sprawie ogłoszenia jednolitego tekstu ustawy o odbudowie obiektów budowlanych",
                    "1. Podany w załączniku do niniejszego obwieszczenia tekst jednolity ustawy nie obejmuje:",
                    "1) art. 57 ustawy z dnia 1 października 2024 r. o zmianie ustawy (Dz. U. poz. 1473), który stanowi:",
                    .. quoted,
                    "Załącznik do obwieszczenia Marszałka Sejmu Rzeczypospolitej Polskiej z dnia 6 sierpnia 2025 r. (Dz. U. poz. 1094)",
                    "USTAWA",
                    "z dnia 11 sierpnia 2001 r.",
                    "o odbudowie obiektów budowlanych",
                    "Art. 57. Ustawa reguluje zasady odbudowy.",
                ];
                var document = LegalDocumentParser.Parse(
                    lines.Select(line => new DocumentBlock { Text = line }).ToList(),
                    new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
                return LegalDocumentConverter.Convert(document).Elements().Single();
            }

            // Cały artykuł: jednostka w zdaniu podpunktu, z adresem w akcie, z którego pochodzi — nie myli się
            // z art. 57 ogłaszanego aktu ani z jednostkami obwieszczenia.
            var whole = Export("„Art. 57. Przepisy stosuje się do odbudowy.”.");
            var quoted = Assert.Single(whole.Descendants(), e => e.Name.LocalName == "tresc-strukt");
            Assert.Equal("zdanie", quoted.Parent!.Name.LocalName);
            Assert.Equal("DU/2024/1473__art_57", Assert.Single(quoted.Elements()).Attribute("eId")!.Value);
            Assert.DoesNotContain(whole.Descendants(), e => e.Name.LocalName == "fragment");

            // Przytoczenie, którego nie da się zbudować w całości z artykułów, zostaje tekstem — nic nie ginie.
            var partial = Export("„3. W przypadku odbudowy przepisu nie stosuje się.”.");
            Assert.DoesNotContain(partial.Descendants(), e => e.Name.LocalName == "tresc-strukt");
            Assert.Equal(
                "„3. W przypadku odbudowy przepisu nie stosuje się.”.",
                Assert.Single(partial.Descendants(), e => e.Name.LocalName == "fragment").Value);
        }

        // ============================================================
        // ADR-0037: równanie Worda wchodzi do tekstu w zapisie liniowym
        // ============================================================

        [Fact]
        public void Adr0037_Equation_IsPartOfTheParagraphText_InLinearForm()
        {
            // Treść równania nie może zniknąć z tekstu: zdanie zostawałoby z dziurą, a wiersz legendy bez symbolu
            // zaczynałby się od myślnika i stawał tiretem.
            const string namespaces =
                "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" " +
                "xmlns:m=\"http://schemas.openxmlformats.org/officeDocument/2006/math\"";
            var paragraph = new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                $"<w:p {namespaces}>" +
                "<w:r><w:t xml:space=\"preserve\">kwotę wydatków </w:t></w:r>" +
                "<m:oMath><m:sSub><m:e><m:r><m:t>WYD</m:t></m:r></m:e><m:sub><m:r><m:t>n-1</m:t></m:r></m:sub></m:sSub></m:oMath>" +
                "<w:r><w:t>, o której mowa w art. 112aa</w:t></w:r>" +
                "</w:p>");

            Assert.Equal("kwotę wydatków WYD_(n-1), o której mowa w art. 112aa", paragraph.GetFullText());
        }

        // ============================================================
        // ADR-0038: wzór w osobnym akapicie to wzor-mat z MathML
        // ============================================================

        [Fact]
        public void Adr0038_FormulaParagraph_IsExportedAsWzorMat_NotAsAUnitOrAFragment()
        {
            // Wzór nie ma oznaczenia: nawet w stylu ustępu jest dalszą treścią jednostki, za którą stoi,
            // i wychodzi raz — jako wzor-mat z wyrażeniem MathML, nie jako tekst.
            const string mathMl =
                "<math xmlns=\"http://www.w3.org/1998/Math/MathML\" display=\"block\"><mi>K</mi><mo>=</mo><mi>A</mi></math>";
            DocumentBlock[] blocks =
            [
                new() { Text = "USTAWA" },
                new() { Text = "z dnia 14 czerwca 2024 r." },
                new() { Text = "o opłatach" },
                new() { Text = "Art. 1. 1. Wysokość kary oblicza się według wzoru:" },
                new() { Text = "K=A", StyleId = "USTustnpkodeksu", FormulaMathMl = mathMl },
                new() { Text = "2. Karę uiszcza się w terminie 14 dni." },
            ];
            var document = LegalDocumentParser.Parse(blocks.ToList(), new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

            var article = document.Articles.Single();
            Assert.Equal(new[] { "1", "2" }, article.Paragraphs.Select(paragraph => paragraph.Number?.Value));
            var formula = Assert.Single(article.Paragraphs[0].Continuation);
            Assert.Equal("K=A", formula.Text);
            Assert.Equal(mathMl, formula.Formula!.MathMl);

            var first = LegalDocumentConverter.Convert(document).Descendants().First(e => e.Name.LocalName == "ustep");
            Assert.Equal(new[] { "nr", "p", "wzor-mat" }, first.Elements().Select(e => e.Name.LocalName));
            Assert.Equal("math", first.Elements().Last().Elements().Single().Elements().Single().Name.LocalName);
        }

        // ============================================================
        // ADR-0033: tekst jednolity ma zdarzenie ujednolicenia, nie wejścia w życie
        // ============================================================

        [Fact]
        public void Adr0033_ConsolidatedText_IsAConsolidationEvent_NotAnEntryIntoForce()
        {
            // Przepis końcowy tekstu jednolitego mówi o ogłoszeniu ustawy; data dokumentu jest datą ogłoszenia
            // obwieszczenia. Wyliczenie z nich wejścia w życie dawało datę, której ustawa nigdy nie miała.
            var blocks = new List<DocumentBlock>
            {
                new() { Text = "DZIENNIK USTAW RZECZYPOSPOLITEJ POLSKIEJ", Role = BlockRole.JournalHeader },
                new() { Text = "Warszawa, dnia 11 sierpnia 2025 r.", Role = BlockRole.JournalHeader },
                new() { Text = "Poz. 1094", Role = BlockRole.JournalHeader },
            };
            string[] lines =
            [
                "Obwieszczenie",
                "Marszałka Sejmu Rzeczypospolitej Polskiej",
                "z dnia 6 sierpnia 2025 r.",
                "w sprawie ogłoszenia jednolitego tekstu ustawy o odbudowie obiektów budowlanych",
                "1. Na podstawie art. 16 ust. 1 ustawy z dnia 20 lipca 2000 r. o ogłaszaniu aktów normatywnych ogłasza się w załączniku do niniejszego obwieszczenia jednolity tekst ustawy.",
                "Załącznik do obwieszczenia Marszałka Sejmu Rzeczypospolitej Polskiej z dnia 6 sierpnia 2025 r. (Dz. U. poz. 1094)",
                "USTAWA",
                "z dnia 11 sierpnia 2001 r.",
                "o odbudowie obiektów budowlanych",
                "Art. 1. Ustawa reguluje zasady odbudowy.",
                "Art. 2. Ustawa wchodzi w życie z dniem ogłoszenia.",
            ];
            blocks.AddRange(lines.Select(line => new DocumentBlock { Text = line }));
            var document = LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

            Assert.Null(document.EntryIntoForce);
            Assert.Equal(default, document.EffectiveDate);

            var xml = LegalDocumentConverter.Convert(document);
            var lifecycleEvent = Assert.Single(xml.Descendants(), element => element.Name.LocalName == "Zdarzenie");
            Assert.Equal("ujednolicenie", lifecycleEvent.Attribute("rodzaj")!.Value);
            Assert.Equal("2025-08-11", lifecycleEvent.Attribute("data")!.Value);
            Assert.DoesNotContain(xml.Descendants(), element => element.Attribute("wejscie-w-zycie") != null);
        }

        // ============================================================
        // ADR-0034: „§" jest jednostką podstawową aktu opartego na paragrafach
        // ============================================================

        [Fact]
        public void Adr0034_SectionSign_IsTheBasicUnit_OnlyInASectionBasedAct()
        {
            // Ten sam znak, dwa znaczenia: w rozporządzeniu „§ 1." to jednostka podstawowa (encja artykułu,
            // adres „art", element „paragraf"), w ustawie — paragraf kodeksu pod artykułem.
            static LegalDocument Parse(params string[] lines) => LegalDocumentParser.Parse(
                lines.Select(line => new DocumentBlock { Text = line }).ToList(),
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

            var regulation = Parse(
                "ROZPORZĄDZENIE",
                "MINISTRA FINANSÓW",
                "z dnia 11 maja 2017 r.",
                "w sprawie wykazu osób prawnych",
                "Na podstawie art. 12 ust. 3 ustawy z dnia 15 grudnia 2016 r. o Prokuratorii Generalnej (Dz. U. poz. 2261) zarządza się, co następuje:",
                "§ 1. 1. Rozporządzenie określa wykaz osób prawnych.",
                "2. Wykaz ogłasza się w Biuletynie Informacji Publicznej.",
                "§ 2. Rozporządzenie wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

            Assert.All(regulation.Articles, unit => Assert.True(unit.IsSection));
            Assert.Equal(new[] { "art_1", "art_2" }, regulation.Articles.Select(unit => unit.Id));
            Assert.Equal(new[] { "art_1__ust_1", "art_1__ust_2" }, regulation.Articles.First().Paragraphs.Select(p => p.Id));

            var exported = Assert.Single(LegalDocumentConverter.Convert(regulation).Elements());
            Assert.Equal("rozporzadzenie", exported.Name.LocalName);
            Assert.Equal(2, exported.Elements().Count(element => element.Name.LocalName == "paragraf"));

            var statute = Parse(
                "USTAWA",
                "z dnia 6 czerwca 1997 r.",
                "Kodeks karny",
                "Art. 1. § 1. Odpowiedzialności karnej podlega ten tylko, kto popełnia czyn zabroniony.",
                "§ 2. Nie stanowi przestępstwa czyn zabroniony, którego społeczna szkodliwość jest znikoma.");

            var article = Assert.Single(statute.Articles);
            Assert.False(article.IsSection);
            Assert.Equal(new[] { "art_1__par_1", "art_1__par_2" }, article.Paragraphs.Select(p => p.Id));
        }

        private static DocumentClassificationResult Classify(byte[] docx)
        {
            using var stream = new MemoryStream(docx);
            return LegalDocumentParser.Parse(stream, "ustawa.docx",
                new ParseOptions { Policy = ParsePolicy.ClassifyOnly }).Classification;
        }

        /// <summary>
        /// Obchód drzewa NIEZALEŻNY od produkcyjnego <c>EditorialUnitWalker</c> — gdyby test chodził
        /// tym samym kodem co propagator, wspólny błąd obchodu byłby dla niego niewidoczny.
        /// </summary>
        private static IEnumerable<BaseEntity> Walk(BaseEntity unit)
        {
            yield return unit;

            IEnumerable<BaseEntity> children = unit switch
            {
                Part part => part.Books,
                Book book => book.Titles,
                Title title => title.Divisions,
                Division division => division.Chapters,
                Chapter chapter => chapter.Subchapters,
                Subchapter subchapter => subchapter.Articles,
                Article article => article.Paragraphs,
                Paragraph paragraph => paragraph.CommonParts.Concat<BaseEntity>(paragraph.Points),
                Point point => point.CommonParts.Concat<BaseEntity>(point.Letters),
                Letter letter => letter.CommonParts.Concat<BaseEntity>(letter.Tirets),
                Tiret tiret => tiret.Tirets,
                _ => Array.Empty<BaseEntity>(),
            };

            if (unit is IHasAmendments { Amendment.Content: { } content })
                children = children
                    .Concat(content.Articles).Concat(content.Paragraphs).Concat<BaseEntity>(content.Points)
                    .Concat(content.Letters).Concat<BaseEntity>(content.Tirets).Concat(content.CommonParts);

            foreach (var child in children)
                foreach (var descendant in Walk(child))
                    yield return descendant;
        }

        /// <summary>
        /// Liczy RÓŻNE rdzenie liczebnikowe w kodzie pliku, pomijając wiersze komentarza — inaczej
        /// dokumentacja („tiret pierwsze", „zdanie drugie") dawałaby fałszywy alarm.
        /// </summary>
        private static int CountNumeralSignals(string file)
        {
            var code = string.Join('\n', File.ReadLines(file)
                .Select(line => line.TrimStart())
                .Where(line => !line.StartsWith("//", StringComparison.Ordinal) &&
                               !line.StartsWith("*", StringComparison.Ordinal)));

            return NumeralSignals.Count(signal => code.Contains(signal, StringComparison.OrdinalIgnoreCase));
        }
    }
}
