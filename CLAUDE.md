# CLAUDE.md

Ten plik zawiera wskazówki dla Claude Code (claude.ai/code) dotyczące pracy z tym repozytorium.

## Opis projektu

**SAGA** (System Automatycznego Generowania Aktów) to zestaw narzędzi na platformie .NET 10 do parsowania polskich dokumentów prawnych (aktów prawnych) z formatu DOCX do hierarchicznego modelu obiektowego, a następnie eksportu do XML/XLSX. Obsługuje hierarchiczne struktury dokumentów oraz nowelizacje.

## Polecenia

```bash
# Budowanie całej solucji (Model, Core, Cli, Web, testy)
dotnet build Saga.sln

# Pojedyncze projekty (zadanie VS Code: "build-offline" = Core + Cli)
dotnet build src/Saga.Core/Saga.Core.csproj
dotnet build src/Saga.Cli/Saga.Cli.csproj

# Uruchomienie wszystkich testów
dotnet test --project tests/Saga.Core.Tests/Saga.Core.Tests.csproj

# Uruchomienie pojedynczej klasy testowej
dotnet test --project tests/Saga.Core.Tests/Saga.Core.Tests.csproj --filter-class "*EIdTests"

# Lokalne odtworzenie bramek jakości z CI (te same polecenia co .gitlab-ci.yml)
dotnet format style Saga.sln --verify-no-changes --severity warn   # lint-check
dotnet format whitespace Saga.sln --verify-no-changes              # format-check
dotnet build Saga.sln -c Release -warnaserror                      # type-check
dotnet list Saga.sln package --vulnerable --include-transitive     # dependency-check

# Obraz kontenera (Dockerfile w katalogu głównym; publikuje go pipeline)
docker build --build-arg VERSION=1.0.0 --tag saga-web:local .
```

## CI/CD i wydania

- **Wersja**: jedynym źródłem jest `<Version>` w `Directory.Build.props`. Zadanie `version-check`
  odczytuje ją przez `dotnet msbuild -getProperty:Version`, więc tag gita musi być dokładnie tą
  wartością, bez prefiksu `v`.
- **Publikacja obrazu jest ZAWSZE ręczna** (standard CAISE v3): nic nie trafia do rejestru bez
  kliknięcia — ani z MR, ani z gałęzi domyślnej, ani z tagu. Obraz **zawsze** dostaje tag z wersją,
  bo manifest wdrożenia przypina konkretny tag; sam `latest` nie pozwala ani wdrożyć powtarzalnie,
  ani wrócić do poprzedniego wydania. Gałąź domyślna → `X.Y.Z` + ruchomy `latest`; tag `X.Y.Z` →
  `X.Y.Z`; MR → `X.Y.Z.dev<IID>`, więc build przedwydaniowy nigdy nie nadpisze obrazu wydania.
  Gałęzie boczne uruchamiają tylko etap `quality` i nie publikują niczego.
- **Świadome odstępstwa od standardu v3** (do wskazania przy recenzji): `version-check` waliduje
  format tagu i jego zgodność z `<Version>` — v3 nie sprawdza już ani jednego, ani drugiego;
  na gałęzi domyślnej publikujemy `X.Y.Z` **oraz** `latest`, a nie tylko `latest`; nie ma etapu
  `deployment_validation`, bo walidacja wdrożonej usługi działa spoza tego repozytorium.
- **Progi pokrycia**: `COV_FAIL_UNDER=70` dla całości i `DIFF_FAIL_UNDER=80` dla linii zmienionych
  w MR. Próg różnicowy obejmuje wyłącznie projekty obecne w raporcie pokrycia (dziś `Saga.Core`
  i `Saga.Model`) — strażnik w `diff-coverage` czyta listę z elementów `<package>` i pomija pliki
  z projektów, których żaden test nie ładuje (`Saga.Cli`, `Saga.Web`), wypisując o tym ostrzeżenie.
  Brak dopasowania jest błędem dopiero wtedy, gdy diff rusza projekt **objęty** raportem.

## Architektura

### Graf zależności projektów

```
src/Saga.Cli   (CLI, exe: saga)  ──► src/Saga.Core ──► src/Saga.Model
src/Saga.Web   (Web API, ASP.NET 10) ──► src/Saga.Core ──► src/Saga.Model
tests/Saga.Core.Tests            ──► src/Saga.Core ──► src/Saga.Model
tools/CorpusValidator (poza sln)  ──► src/Saga.Core ──► src/Saga.Model
```

- `Saga.Model` — czyste DTO, bez logiki biznesowej
- `Saga.Core` — cała logika silnika parsowania; zależy od `DocumentFormat.OpenXml`, `Serilog` i `PdfPig` 0.1.15 (adapter PDF: `src/Saga.Core/Ingest/Pdf/` — `PdfBlockReader`, `PdfLineExtractor`, `PageArtifactFilter`)
- `Saga.Cli` — konwerter wsadowy dokument→XML (`saga <plik|katalog> [--output <katalog>]`, katalog = wszystkie `*.docx` bez rekursji);
  plik wykonywalny nazywa się `saga`. Pomija dokumenty nierozpoznane jako akty prawne
- `Saga.Web` — **web API** (ASP.NET 10) z niej powstaje obraz kontenera.
  Wystawia `POST /v1/convert` (eksport XML przez `LegalDocumentConverter`) i `GET /health` (sonda
  dla platformy, celowo poza wersjonowaniem). Sondy idą za konwencją CAISE: `GET /` to sonda
  **życia** (proces odpowiada), a `GET /health` sonda **gotowości** — uruchamia kontrole
  zarejestrowane przez `AddHealthChecks()` w `Program.cs` i zwraca ich stan. `Unhealthy` daje 503,
  `Degraded` zostaje przy 200 (usługa nadal obsługuje żądania). Dziś nie ma zarejestrowanej żadnej
  kontroli, bo usługa nie zależy od niczego zewnętrznego; nowa zależność = jedno `.AddCheck(...)`. `POST /parse_xml` zostaje jako **przestarzały alias**
  ścieżki sprzed wersjonowania — do usunięcia po migracji konsumentów. Błędy zwraca jako
  `ProblemDetails` (400/413/415/422) z `traceId`. Interfejsem są dwie strony, wystawiane
  bezwarunkowo: `GET /` — wizytówka usługi (szablon `src/Saga.Web/Pages/index.html`,
  render w `LandingPage`) z przyciskiem do dokumentacji, oraz `/scalar` — dokumentacja API
  generowana ze `/openapi/v1.json`. Gdy szablonu brakuje, `/` przekierowuje na `/scalar`.

### Hierarchia modelu dokumentu

**Jednostki redakcyjne** (struktura treści):
```
Article → Paragraph (Ustęp) → Point (Punkt) → Letter (Litera) → Tiret → Tiret (zagnieżdżony)
```
Uwaga: „DoubleTiret" nie jest osobną klasą — to zagnieżdżona lista `Tiret.Tirets: List<Tiret>` w tej samej klasie `Tiret`.

**Jednostki systematyzacyjne** (kontenery organizacyjne):
```
Part → Book → Title → Division → Chapter → Subchapter → [Articles]
```

**Jednostki niejawne (wirtualne)**: jeśli artykuł ma dokładnie jeden ustęp, ten ustęp jest oznaczony jako `IsImplicit = true`. Jednostki niejawne są pomijane w ścieżkach eId i w prezentacji. Przykładowy format eId: `art_5__ust_2__pkt_3__lit_a__tir_1` (podwójny podkreślnik między komponentami, pojedynczy podkreślnik między prefiksem a numerem).

### Potok parsowania (`src/Saga.Core/Services/Parsing/`)

Punkt wejścia: `LegalDocumentParser.Parse(...)` przyjmuje `Stream` lub ścieżkę pliku i zwraca kopertę `ParseResult` (`src/Saga.Core/ParseResult.cs`), nie bezpośrednio `LegalDocument` → wewnętrznie wywołuje `ParserOrchestrator`

Etapy potoku:
0. Detekcja formatu przez `SourceFormatDetector` (`src/Saga.Core/Ingest/SourceFormatDetector.cs`) i odczyt bloków przez `DocumentBlockReaderFactory`/`IDocumentBlockReader` (adaptery DOCX/PDF/TXT, katalog `src/Saga.Core/Ingest/`), następnie klasyfikacja CAŁEGO dokumentu (rodzaj aktu) przez `DocumentClassifier` (`src/Saga.Core/Services/Classify/Document/DocumentClassifier.cs`), sterowana `ParseOptions.Policy` (`src/Saga.Core/ParseOptions.cs`); dopiero potem budowa modelu przez `ParserOrchestrator`
1. `ParagraphClassifier` (w `Services/Classify/`) — klasyfikuje każdy akapit przy użyciu 3 warstw (patrz niżej)
2. `StructureProcessor` — buduje encje domenowe delegując do klas `*Builder` (`ArticleBuilder`, `ParagraphBuilder`, `PointBuilder`, `LetterBuilder`, `TiretBuilder`, `AmendmentBuilder`, `SystematizingUnitBuilder` — buduje jednostki systematyzacyjne Part/Book/Title/Division/Chapter/Subchapter) — wzorzec kaskadowy; buildery niższego poziomu zapewniają istnienie encji nadrzędnych
3. `AmendmentStateManager` / `AmendmentCollector` / `AmendmentFinalizer` — wykrywają wyzwalacze nowelizacji, buforują treść, finalizują obiekty `Amendment`
4. `NumberingHint` (w `Services/Classify/`) + `ParagraphClassifier` — walidują ciągłość numeracji podczas klasyfikacji; kara `NumberingBreakPenalty` obniża Confidence
5. Stan przechowywany jest w `ParsingContext` przez cały czas parsowania; `ValidationReporter` zbiera komunikaty walidacji

### Klasyfikacja warstwowa (kluczowa zasada)

Klasyfikacja akapitów musi być odporna na błędy. Zawsze stosuj wszystkie trzy warstwy; przy konflikcie preferuj treść/układ nad stylem:

1. **Strukturalna** — style akapitów Word (np. `ART`, `UST`, `Z/*` dla nowelizacji) jako silna wskazówka, nie pewnik. Zawsze sprawdzaj null dla `ParagraphProperties` przed dostępem do stylów.
2. **Syntaktyczna** — wzorce regex dla `Art.`, `§`, `ust.`, `pkt`, `lit.`, znaczników tiretu. Oceniaj treść niezależnie od stylu.
3. **Semantyczna** — spójność hierarchii. Wstawiaj jednostki niejawne, gdy brakuje poziomu.

Reguła decyzyjna: twardy wymóg dwóch zgodnych sygnałów dotyczy głównie rozpoznania Artykułu — sam styl bez sygnatury tekstowej daje `Unknown` (gałąź „tylko styl" w `ParagraphClassifier.BuildResult`); pozostałe jednostki dopuszczają pojedynczy sygnał kosztem obniżonej Confidence (kary `StyleAbsentPenalty`/`SyntaxAbsentPenalty`). Gdy styl konfliktuje z treścią, preferuj treść (`DefaultConflictResolver`). Rejestruj decyzje naprawcze przez Serilog.

### System nowelizacji

- Słowa kluczowe wyzwalające: „otrzymuje brzmienie:", „dodaje się", „uchyla się"
- Style akapitów nowelizacji używają prefiksów `Z/*`, `ZZ*`, `Z_*` (dekodowane przez `AmendmentStyleDecoder`)
- Typy (`src/Saga.Model/AmendmentOperationType.cs`): Modification, Insertion, Repeal — dotyczą udanej klasyfikacji; czwarta wartość Error oznacza błąd przetwarzania
- Wieloetapowy cykl życia: Wykrycie → Zbieranie (`AmendmentCollector`) → Finalizacja (`AmendmentFinalizer`)
- Cele komendy (ADR-0016): jeden → `element-eId`; przedział („art. 1–3") → `element-eId` + `element-do-eId`; wyliczenie („art. 1, 2 i 3") → osobna komenda na każdy cel. `element-do-eId` nigdy nie znaczy „ostatni cel listy"

## Kluczowe pliki

| Plik | Rola |
|---|---|
| `src/Saga.Core/LegalDocumentParser.cs` | Publiczny punkt wejścia |
| `src/Saga.Core/Ingest/SourceFormatDetector.cs` + `DocumentBlockReaderFactory` | Detekcja formatu źródłowego i odczyt bloków (DOCX/PDF/TXT) |
| `src/Saga.Core/Services/Classify/Document/DocumentClassifier.cs` | Klasyfikacja rodzaju aktu (całego dokumentu) |
| `src/Saga.Core/ParseResult.cs` i `src/Saga.Core/ParseOptions.cs` | Koperta wyniku parsowania + polityka parsowania |
| `src/Saga.Core/Services/Parsing/ParserOrchestrator.cs` | Główny potok |
| `src/Saga.Core/Services/Parsing/StructureProcessor.cs` | Buduje encje domenowe (deleguje do Builders) |
| `src/Saga.Core/Services/Parsing/ParsingContext.cs` | Mutowalny stan parsera |
| `src/Saga.Core/Services/Parsing/Builders/` | Buildery encji (wzorzec kaskadowy) |
| `src/Saga.Core/Services/Parsing/Builders/SystematizingUnitBuilder.cs` | Buduje jednostki systematyzacyjne (Part/Book/Title/Division/Chapter/Subchapter) |
| `src/Saga.Core/Services/Classify/ParagraphClassifier.cs` | Logika klasyfikacji (3-warstwowa) |
| `src/Saga.Core/Services/Classify/NumberingHint.cs` | Walidacja ciągłości numeracji |
| `src/Saga.Core/Helpers/ParagraphExtensions.cs` | Bezpieczne helpery OpenXml (używaj rozszerzenia `.StyleId()`) |
| `src/Saga.Core/Services/UnitReferences/` | Jedno źródło powołań jednostek („art. 5 § 2 pkt 3"): słownik, reguły ścieżki, rozstrzygnięcie „§" — dla wejścia w życie i celów nowelizacji (ADR-0015) |
| `src/Saga.Core/Helpers/AmendmentStyleDecoder.cs` | Dekoduje style nowelizacji (`Z/*`, `ZZ*`, `Z_*`) |
| `src/Saga.Model/BaseEntity.cs` | Abstrakcyjna baza dla wszystkich encji domenowych |
| `src/Saga.Model/EntityNumber.cs` | Model numeru encji (NumericPart, LexicalPart, Superscript) |
| `tests/Saga.Core.Tests/` | Testy xUnit; artefakty testowe w podkatalogu `Artifacts/` |

## Konwencje

- **Język**: kod (zmienne, metody, klasy) po **angielsku**; komentarze, teksty UI i komunikaty logów po **polsku**. Jest to celowe.
- **Bezpieczeństwo null w OpenXml**: zawsze sprawdzaj null dla `paragraph.ParagraphProperties` przed dostępem do stylów. Używaj metod rozszerzających z `ParagraphExtensions`: `.StyleId()` (bez argumentu) zwraca `string?` z identyfikatorem stylu, a `.StyleId("PREFIKS")` zwraca `bool?` informujące, czy styl zaczyna się od podanego prefiksu.
- **Wzorce regex**: deklaruj jako `private static readonly Regex`, prekompilowane. Wzorce muszą obsługiwać opcjonalny prefiks cudzysłowu dla treści nowelizacji.
- **Powołania jednostek** („w art. 5 § 2 pkt 3 lit. a") czytaj wyłącznie przez `Services/UnitReferences` (`UnitEnumerationParser`, `UnitAddressReader`) — nie dopisuj własnych wzorców `art.`/`ust.`/`lit.`. Pilnuje tego strażnik ADR-0015.
- **Logowanie**: używaj Serilog (konfigurowanego przez `LoggerConfig.ConfigureLogger()`); minimalny poziom Warning. Logi trafiają do `logs/log.txt` i na konsolę.
- **Komunikaty commitów**: proponuj nazwy commitów po **angielsku** po każdej zmianie (zarówno małej jak i architektonicznej).
- **Dokumentacja i plany**: wersjonowany katalog `docs/` trzyma wyłącznie dokumenty opisujące **stan i uzasadnienia** — `architecture.md` (jak działa dziś), `adr/` (dlaczego tak — patrz niżej), `backlog.md` (znane luki i prace świadomie odroczone), `ztp-struktura-aktow.md` (kondensat ZTP). Plany przebudowy, analizy i raporty robocze powstają w `docs/internal/` (gitignorowane), nigdy w wersjonowanym `docs/`. Po wykonaniu planu wygaś go trójpodziałem: „dlaczego" i zasady twarde → ADR, niezrealizowane/odroczone → `backlog.md`, etapowanie i definicje ukończenia → usuń (historia jest w `git log`). Każde twierdzenie przepisywane z planu **zweryfikuj w kodzie** — plany opisują stan z dnia ich napisania i po drodze dryfują.
- **Odniesienia do kodu w dokumentacji**: cytuj **nazwy symboli** (klasa, metoda, właściwość), nie numery linii — numery pękają przy pierwszej edycji pliku i nic tego nie sygnalizuje. Linki markdown do plików są w porządku (pilnuje ich `DocumentationConsistencyTests`), ale bez zakotwiczeń `#L42`.
- **ADR (`docs/adr/`)**: jeden plik = jedna decyzja, format wg `docs/adr/0000-template.md`, wpis w indeksie `docs/adr/README.md`. Decyzję, którą da się objąć testem, uzbrój strażnikiem w `tests/Saga.Core.Tests/ArchitectureDecisionTests.cs` i wpisz jego nazwę w sekcji „Weryfikacja" — wtedy odwrócenie decyzji wymaga świadomego usunięcia asercji, widocznego w diffie. ADR piszemy tylko dla decyzji, która **miała odrzuconą alternatywę**, jest **trudno odwracalna** albo jest **nieoczywista** (kod wygląda na przekomplikowany, dopóki nie znasz kontekstu); nie dla wyborów bez alternatywy ani dla konwencji kodu — te należą do tego pliku. Zaakceptowanego ADR **nie edytuje się**: zmiana zdania to nowy ADR ze statusem `Supersedes ADR-NNNN`, a stary dostaje `Superseded by ADR-NNNN`. `architecture.md` nie powtarza uzasadnień — trzyma jednozdaniowy skrót i link.
- **Walidacja**: dołączaj obiekty `ValidationMessage` (Info/Warning/Error/Critical) do encji DTO dla akapitów o niepewnej lub naprawionej klasyfikacji.

## Testy

- Projekt testowy: `Saga.Core.Tests`
- Framework: **xUnit v3** (`xunit.v3` 4.0.0) na **Microsoft.Testing.Platform**. Opt-in dla
  `dotnet test` siedzi w `global.json` (`test.runner`); bez niego .NET 10 SDK odmawia
  uruchomienia testów przez nieobsługiwaną już ścieżkę VSTest. Pokrycie (`--coverage`)
  i raport JUnit (`--report-xunit-junit`) są wbudowane w runner — bez dodatkowych pakietów.
- Dokumenty referencyjne DOCX leżą w lokalnym `DocRepo/` (niewersjonowany); `tests/Saga.Core.Tests/Artifacts/` zawiera golden snapshoty oczekiwanych wyników — oba katalogi niewersjonowane
- Test sięgający po te dane oznaczaj `[DocRepoFact("plik.docx")]` (i `RequiredArtifact = "golden.xml"`, jeśli potrzebuje goldenu) zamiast `[Fact]`. Przy braku lokalnych plików test jest wtedy **pomijany z powodem**, a nie czerwony — na czystym klonie nie ma ani `DocRepo/`, ani `Artifacts/`. Pilnuje tego `DocRepoFactGuardTests`.
- Klasy testowe: lista NIE jest tu utrzymywana (dryfowała — brakowało w niej kolejnych klas). Wygeneruj aktualną:
  `grep -rho -E '^\s*public class \w+' tests/Saga.Core.Tests/*.cs | sed -E 's/.*public class //' | sort`

  Pułapka: `ParsingBuildersTests` to NAZWA PLIKU (`ParsingBuildersTests.cs`), nie klasa testowa — plik zawiera klasy `ArticleBuilderTests`, `ParagraphBuilderTests`, `PointBuilderTests`, `LetterBuilderTests`, `TiretBuilderTests` (filtr `--filter-class "*ParsingBuildersTests"` zwraca 0 testów; filtruj po nazwie klasy, np. `--filter-class "*ArticleBuilderTests"`).
- Uruchomienie testów konkretnej klasy: `--filter-class "*NazwaKlasy"`; pojedynczego testu:
  `--filter-method "*NazwaTestu"`. Składnia `--filter "FullyQualifiedName~..."` była z VSTest
  i już nie działa.
