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
            Assert.Equal(new[] { "art_2" }, events[1].Elements().Select(e => e.Attribute("eId")?.Value));

            // Data z formuły niesie formułę; każda — klauzulę, z której pochodzi.
            Assert.Equal("po upływie 14 dni od dnia ogłoszenia", events[0].Attribute("warunek")?.Value);
            Assert.Null(events[1].Attribute("warunek"));
            Assert.All(events, e => Assert.Equal("art_3", e.Attribute("dowod")?.Value));
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
