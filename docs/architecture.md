# Architektura projektu SAGA

> Dokument opisuje domenę projektową, moduły, role klas, przepływy danych, zależności między warstwami oraz konwencje stosowane w projekcie SAGA.

> Aktualny na dzień: 2026-07-25

---

## 1. Przegląd domeny

SAGA to toolkit .NET 10 służący do **parsowania polskich aktów prawnych** z dokumentów Word/DOCX, PDF (z warstwą tekstową) oraz TXT do modelu obiektowego (DTO), a następnie do XML (schemaPL). Wejście jest uniwersalne: format źródłowy jest wykrywany sygnaturowo, a dokument jest klasyfikowany wg rodzaju aktu (ZTP) przed (opcjonalnym) zbudowaniem modelu. Domena obejmuje:

- **Hierarchię jednostek redakcyjnych**: `Article → Paragraph → Point → Letter → Tiret → Tiret (zagnieżdżony)` — „podwójny tiret" (2TIR/3TIR) to nie osobna klasa, tylko zagnieżdżona lista `Tiret.Tirets`
- **Hierarchię jednostek systematyzujących**: `Part → Book → Title → Division → Chapter → Subchapter`
- **Klasyfikację rodzaju dokumentu**: rozpoznanie, czy wejście jest aktem prawnym i jakiego rodzaju (ustawa, rozporządzenie, obwieszczenie TJ, uchwała, zarządzenie, akt prawa miejscowego…)
- **Nowelizacje** (amendments): zmiany legislacyjne wewnątrz aktów prawnych
- **Metadane publikatorów**: odniesienia do Dziennika Ustaw (Dz. U.)

---

## 2. Moduły (projekty)

### 2.1 `Saga.Model` — Warstwa modelu danych


Czyste klasy DTO bez logiki biznesowej. Definiuje strukturę drzewa aktu prawnego.

| Podkatalog / Plik | Opis |
|---|---|
| `EditorialUnits/` | Jednostki redakcyjne: `Article`, `Paragraph`, `Point`, `Letter`, `Tiret`, `CommonPart` |
| `SystematizingUnits/` | Jednostki systematyzujące: `Part`, `Book`, `Title`, `Division`, `Chapter`, `Subchapter` |
| `BaseEntity.cs` | Abstrakcyjna klasa bazowa z wspólnymi właściwościami (Guid, Number, ContentText, Parent, eId) |
| `LegalDocument.cs` | Korzeń modelu — wrapper całego aktu prawnego z metadanymi, hierarchią i `Classification` (`DocumentClassificationResult?`, ustawiane po budowie modelu); preambuła ustawy — akapity między tytułem a pierwszą jednostką (`Preamble`, eksport `preambula`) |
| `EntityNumber.cs` | Model numeru encji z rozbiciem na: `NumericPart`, `LexicalPart`, `Superscript` |
| `Amendment.cs` | Model nowelizacji (typ operacji, treść, cel, data wejścia w życie); zmiany w tekście bez przytoczenia brzmienia (`TextChanges` — `TextChange`: tekst zastępowany i zastępujący, wystąpienie i własny cel; w eksporcie `now:zamien-tekst` — [ADR-0052](adr/0052-zmiana-wyrazow-jest-instrukcja-zamien-tekst.md), [ADR-0053](adr/0053-skreslenie-wyrazow-to-zamiana-na-pusty-tekst.md), [ADR-0054](adr/0054-zamiana-znaku-konczacego-jednostke-ma-wlasny-cel.md), skreślenie znaku — [ADR-0068](adr/0068-skreslenie-znaku-jest-zmiana-w-tekscie-nie-uchyleniem-jednostki.md)); obudowanie dotychczasowej treści ustępem (`WrapsContentAs`, `WrappedUnit` — [ADR-0056](adr/0056-obudowanie-dotychczasowej-tresci-jest-instrukcja-na-jednostce.md)); załączniki zmieniane odesłaniem do załączników aktu zmieniającego (`AnnexInstructions` — [ADR-0058](adr/0058-zalacznik-zmieniany-odeslaniem-jest-instrukcja-z-adresami-obu-zalacznikow.md)); odnośnik cytowany jako treść (`AmendmentContent.Footnote`, adres `odn_N` — [ADR-0059](adr/0059-odnosnik-jako-przedmiot-komendy-jest-cytowanym-odnosnikiem.md)) |
| `AmendmentContent.cs` | Treść nowelizacji — hierarchiczny fragment aktu (artykuły, ustępy, punkty...) |
| `StructuralAmendmentReference.cs` | Cel nowelizacji — ścieżka strukturalna do zmienianej jednostki, dla przedziału także jego koniec (`RangeEnd`) |
| `TextSegment.cs` / `TextSegmentType.cs` | Segmenty tekstu (zdania) wewnątrz jednostek redakcyjnych |
| `ValidationMessage.cs` | Komunikaty diagnostyczne (Info/Warning/Error/Critical) |
| `JournalInfo.cs` | Metadane publikatora (Dz. U. — rok, pozycje) |
| `ActSignature.cs` | Formuła podpisu zamykająca akt: organ, osoba i ewentualne „wz." / „z up." (`Capacity`), jak stoją w akcie (`LegalDocument.Signature`; kolejne formuły aktu wydanego wspólnie — `LegalDocument.CoSignatures`) |
| `Annex.cs` | Załącznik aktu (`LegalDocument.Annexes`): wskazanie aktu, oznaczenie i numer (eId `zal_1`; bez numeru — bez eId), tytuł, treść w kolejności dokumentu (`AnnexBlock`: akapit, tabela albo jednostka redakcyjna) |
| `ContentTable.cs` | Tabela w treści: wiersze (znacznik wiersza nagłówka), komórki ze scaleniami i akapitami oraz objaśnienia pod tabelą (`Description`, w eksporcie `opis-tabeli`) |
| `Footnote.cs` | Odnośnik (przypis): numer jak w akcie i akapity treści; rejestr w `LegalDocument.Footnotes`, miejsce odwołania niesie znacznik `[N)]` w tekście jednostki — [ADR-0026](adr/0026-odnosnik-tresc-w-rejestrze-miejsce-w-tekscie.md) |
| `CommonPartType.cs` | Enum: `Intro` / `WrapUp` |
| `LegalActType.cs` | Enum rodzaju aktu: `Statute`, `Bill`, `Regulation`, `Code`, `Ordinance`, `RegulatoryImpactAssessment`, `AmendingStatute`, `Announcement`, `Resolution`, `ExecutiveOrder`, `LocalLegalAct` |
| `DocumentClassificationResult.cs` | Wynik klasyfikacji dokumentu (rodzaj aktu, pewność, sygnały, uzasadnienie) |
| `DocumentSignal.cs` / `DocumentSignalKind.cs` | Pojedynczy dowód klasyfikacji dokumentu (rodzaj sygnału, wynik, dopasowany fragment) |
| `PublisherType.cs` | Enum typu publikatora (Dz. U. / Dz. Urz. Woj. itp.) |

**Interfejsy kontraktowe:**

| Interfejs | Opis |
|---|---|
| `IHasAmendments` | Encja może posiadać nowelizację (`Amendment? Amendment`) |
| `IHasCommonParts` | Encja może posiadać części wspólne (`List<CommonPart>`) |
| `IHasTextSegments` | Encja może być dzielona na segmenty tekstu (`List<TextSegment>`) |
| `ISystematizingUnit` | Kontrakt jednostek systematyzujących (`Heading`, `IsImplicit`) |
| `SystematizingHierarchy` | Jedyne źródło kształtu hierarchii jednostek systematyzacyjnych: kolejność poziomów, prefiks eId, tworzenie jednostki, jej dzieci i dołączanie dziecka |

### 2.2 `Saga.Core` — Warstwa logiki parsowania

Silnik parsujący dokumenty DOCX/PDF (z warstwą tekstową)/TXT. Zależy od `Saga.Model`,
`DocumentFormat.OpenXml` (DOCX), `PdfPig` (PDF) i `Serilog`.

| Ścieżka | Rola |
|---|---|
| `LegalDocumentParser.cs` | **Punkt wejścia** — statyczna metoda `Parse(Stream, fileNameHint?, ParseOptions?)` / `Parse(filePath, ParseOptions?)` → `ParseResult`. `Parse(WordprocessingDocument)` jest `[Obsolete]` (pomija klasyfikację dokumentu i kopertę `ParseResult`) |
| `ParseResult.cs` | Koperta wyniku: `Classification`, `Document` (nullable), `SourceFormat`, `BlockCount` |
| `ParseOptions.cs` | Opcje: `Policy` (`ParsePolicy`), `ForcedFormat` (`SourceFormat?`) |
| `LoggerConfig.cs` | Konfiguracja Serilog (konsola + plik `logs/log.txt`) |
| `Exceptions/` | Wyjątki parsowania (`ParsingException`, `UnsupportedDocumentFormatException`) |
| `Helpers/` | Metody rozszerzające, dekodery styli, helpery |
| `Ingest/` | **Warstwa odczytu formatu** — detekcja formatu (`SourceFormatDetector`), adaptery DOCX/PDF/TXT do wspólnej reprezentacji pośredniej (`DocumentBlock`) |
| `Services/Classify/` | **Warstwa klasyfikacji akapitów** — klasyfikator (`ParagraphClassifier`, jednoprzebiegowy), wzorce regex, system kar, rozwiązywanie konfliktów |
| `Services/Classify/Document/` | **Warstwa klasyfikacji dokumentu** — `DocumentClassifier` rozpoznaje rodzaj aktu (ZTP) na podstawie bloków, przed budową modelu |
| `Services/` | Serwisy domenowe (numeracja, referencje, publikatory) |
| `Services/Parsing/` | **Pipeline parsowania** — orkiestrator, buildery, przetwarzanie struktury, zarządzanie nowelizacjami |
| `Services/Converters/` | Konwertery modelu do XML schemaPL (`LegalDocumentConverter` składa akt) |

### 2.3 `Saga.Cli` — Narzędzie konsolowe (CLI)

Konwerter wsadowy: `saga <plik|katalog> [--output <katalog>]`. Czyta wejście
**read-only**, dla każdego dokumentu zapisuje XML z `LegalDocumentConverter` pod nazwą pliku
źródłowego. Dla pojedynczego pliku przyjmuje dane ogłoszenia szczotki: `--publisher DU|MP`, `--year`,
`--position`, `--announced RRRR-MM-DD` (`ParseOptions.Publication`, [ADR-0081](adr/0081-dane-ogloszenia-od-wywolujacego-uzupelniaja-tylko-braki-dokumentu.md)).

Trzyma domyślną politykę `ParsePolicy.ParseWhenLegalAct`, więc dokument nierozpoznany jako akt
prawny nie ma modelu i zostaje pominięty (z adnotacją i licznikiem w podsumowaniu). Wyjątek przy
jednym pliku nie przerywa przebiegu: plik trafia na listę błędów z typem i treścią wyjątku, a
nieprzewidziany wyjątek (spoza błędów wejścia) dodatkowo z pełnym stosem do logu. Kody wyjścia:
`0` — wszystko przekonwertowane, `1` — błąd wywołania lub wejścia/wyjścia, `2` — przebieg zakończony,
ale część plików pominięto lub nie udało się przetworzyć.

### 2.4 `Saga.Web` — Web API (aktywne)

ASP.NET 10 — usługa HTTP; web API repozytorium. Wystawia `POST /v1/convert` (dokument
DOCX/PDF/TXT → XML aktu prawnego przez `LegalDocumentConverter`; opcjonalne pola formularza `publisher`, `year`,
`position`, `announcementDate` uzupełniają dane ogłoszenia, których dokument nie niesie — [ADR-0081](adr/0081-dane-ogloszenia-od-wywolujacego-uzupelniaja-tylko-braki-dokumentu.md)) oraz `GET /health` — sonda
celowo **poza** wersjonowaniem, bo ma przeżyć każdą zmianę kontraktu.

Sondy idą za konwencją CAISE, która rozdziela życie od gotowości: `GET /` odpowiada za życie
(proces przyjmuje żądania), a `GET /health` za gotowość. Ta druga uruchamia kontrole
zarejestrowane przez `AddHealthChecks()` i zwraca `HealthResponse` — stan zbiorczy plus mapę
`dependencies` ze stanem każdej zależności z osobna. `Unhealthy` daje 503 (platforma ma wycofać
instancję z ruchu), `Degraded` zostaje przy 200, bo usługa nadal obsługuje żądania. Rozdział jest
istotny: awaria zależności nie może zgasić sondy życia, bo wtedy platforma ubija proces zamiast
tylko odciąć go od ruchu. Lista kontroli jest dziś pusta — usługa nie zależy od niczego
zewnętrznego. Odpowiedź nie niesie treści wyjątków, bo sonda jest nieuwierzytelniona; powód
niesprawności podaje autor kontroli w `Description`. `POST /parse_xml` działa
dalej jako przestarzały alias ścieżki sprzed wersjonowania.

Parsuje ze strumienia przez `LegalDocumentParser.Parse(Stream, ...)` z `ParsePolicy.AlwaysParse` —
klasyfikację niesie wynik, nie kod HTTP. Strumień bierze wprost z `IFormFile.OpenReadStream()`:
dane formularza są już zbuforowane, więc strumień jest seekowalny i nie trzeba przepisywać
dokumentu do pamięci. Każda awaria wychodzi jako `ProblemDetails` z `traceId`
(400/413/415/422 oraz 500 z `UseExceptionHandler`); ciało dla 413 dokłada `UseStatusCodePages`,
bo Kestrel odrzuca zbyt duże żądanie przed wejściem do endpointu.

Interfejsem są dwie strony: `/` — wizytówka usługi z odnośnikiem do dokumentacji (szablon
`Pages/index.html` wypełniany danymi wydania raz, przy starcie, przez `LandingPage`) oraz
`/scalar` — dokumentacja API generowana ze `/openapi/v1.json`. Brak szablonu nie kładzie usługi:
`/` wraca wtedy do przekierowania na `/scalar`.

| Ścieżka | Rola |
|---|---|
| `Program.cs` | Kompozycja usługi: rejestracja serwisów, potok middleware, mapowanie endpointów |
| `ConversionEndpoints.cs` | `POST /v1/convert` i jego przestarzały alias — wspólny handler i wspólny kontrakt odpowiedzi |
| `OperationsEndpoints.cs` | `GET /health` — sonda gotowości uruchamiająca kontrole zależności |
| `HealthResponse.cs` | Treść odpowiedzi sondy: stan zbiorczy i mapa `dependencies` |
| `LandingPage.cs` | Strona powitalna pod `/` (zarazem sonda życia) |
| `OpenApiSetup.cs` | Nagłówek dokumentu OpenAPI, opisy grup i odpowiedzi, `traceId` w schemacie `ProblemDetails` |
| `BuildInfo.cs` | Wersja i skrót commita odczytane z atrybutów zestawu |
| `ServiceInfo.cs` | Nazwy, ścieżki i formaty używane w więcej niż jednym miejscu |
| `UploadLimits.cs` | Limit rozmiaru uploadu — jedno źródło dla Kestrela, treści błędu 413 i dokumentacji |


### 2.5 `Saga.Core.Tests` — Testy jednostkowe

Testy xUnit pokrywające kluczowe scenariusze parsowania.

---

## 3. Diagram zależności między projektami

```
SAGA (CLI)        ──►  Saga.Core  ──────►  Saga.Model
Saga.Web (Web API) ──►  Saga.Core  ──────►  Saga.Model
Saga.Core.Tests    ──►  Saga.Core  ──────►  Saga.Model
```

Zależności zewnętrzne:
- `DocumentFormat.OpenXml` — parsowanie dokumentów DOCX (tylko w `Saga.Core`)
- `PdfPig` — odczyt warstwy tekstowej PDF (tylko w `Saga.Core`; UWAGA: paczka NuGet
  „UglyToad.PdfPig" to obcy fork — używana jest `PdfPig`)
- `Serilog` — strukturalne logowanie (w `Saga.Core`)
- `xUnit` — framework testowy (w `Saga.Core.Tests`)

---

## 4. Hierarchia modelu danych

### 4.1 Jednostki systematyzujące (organizacyjne)

```
Part (Część)
 └── Book (Księga)
      └── Title (Tytuł)
           └── Division (Dział)
                └── Chapter (Rozdział)
                     └── Subchapter (Oddział)
                          └── [Articles...]
```

Każdy dokument ma **pełną minimalną hierarchię** — jednostki nieobecne w tekście mają `IsImplicit = true` i są pomijane w generowaniu eId.

Rozpoznanie i budowa:

- **Oznaczenie** („Rozdział 3", „DZIAŁ II", „KSIĘGA PIERWSZA") rozpoznaje `ParagraphClassifier` z treści,
  a style szablonu (`ROZDZ(ODDZ)_OZN`, `TYT(DZ)_OZN`, `CZ(KSIĘGA)`) wskazują rodzinę dwóch jednostek.
  Sam styl bez oznaczenia w treści nie tworzy jednostki (tabele, załączniki). Odczyt spoza § 62 ZTP
  (rozdział rzymski, sufiks wersalikiem, dział arabski, „IvA") jest tolerowany z komunikatem `Info`.
- **Tytuł** to pierwszy akapit po oznaczeniu: ze stylu `*_PRZEDM` albo bez stylu — akapit od wielkiej litery,
  który nie kończy się jak zdanie ani wprowadzenie do wyliczenia (kropka, dwukropek, średnik, przecinek);
  długość nie rozstrzyga. Miejsce tytułu zajmuje też adnotacja tekstu jednolitego o uchyleniu — akapit w nawiasie
  kończący się słowem „uchylony" albo „pominięty" („(uchylony)", „(zawierający art. 19–21 – uchylony)").
  Adnotacja, która sama nazywa uchyloną jednostkę („(Rozdział 2 – uchylony)", „(oznaczenie oraz tytuł księgi
  pierwszej – uchylone)"), wprowadza tę jednostkę i jest jej nagłówkiem ([ADR-0066](adr/0066-adnotacja-nazywajaca-uchylona-jednostke-jest-jej-naglowkiem.md)).
  Kolejny wiersz w stylu tytułu od wielkiej litery (nagłówek pośredni „Przepis wstępny") i adnotacja, która
  jednostki nie nazywa, są dalszą treścią nagłówka — `specjalny` za jego `p` ([ADR-0067](adr/0067-wiersz-pod-tytulem-jednostki-jest-dalsza-trescia-naglowka.md)).
  Tytuł złamany na kilka bloków dokleja się tylko, gdy kolejny blok zaczyna się małą literą.
- **Części nazwane** („CZĘŚĆ OGÓLNA", „TYTUŁ WSTĘPNY") z zamkniętej listy dostają eId tymczasowy
  `czesc_TMP1`. Taka część zaraz po oznaczeniu niższej jednostki jest jej tytułem (KC, KKS).
- **Wiele części**: `LegalDocument.Parts`; część, której nie da się przejąć, staje obok jako
  rodzeństwo z własnym łańcuchem niejawnym (`SystematizingUnitBuilder.EnterPart`).
- **Tekst jednolity**: oznaczenie i tytuł w stylu zmian (`Z…`) bez komendy nowelizacyjnej przed nimi
  to jednostka aktu (`ParserOrchestrator`).
- **Załącznik**: nagłówek „Załącznik do ustawy z dnia…" po pierwszym artykule kończy strukturę aktu.
- **Walidacja** na gotowym drzewie (`SystematizingUnitValidator`): powtórzone oznaczenie, luka,
  malejąca numeracja, oddział poza rozdziałem, brak tytułu — tylko komunikaty, bez napraw.
- **W treści nowelizacji** jednostki budują się w `AmendmentContent.SystematizingUnits` (patrz 8.1).

### 4.2 Jednostki redakcyjne (treściowe)

```
Article (art.)           — kontener; zawsze ≥1 Paragraph
 └── Paragraph (ust.)   — może być implicit (jedyny ustęp w artykule); w kodeksie „§" (Role = "par")
      ├── CommonPart     — intro (przed listą) lub wrapUp (po liście)
      └── Point (pkt)
           ├── CommonPart
           └── Letter (lit.)
                ├── CommonPart
                └── Tiret (tir.)
                     └── Tiret (podwójny tiret / 2TIR)
```

### 4.3 Budowanie identyfikatorów (eId)

Format: `art_5__ust_2__pkt_3__lit_a__tir_1`

- Skróty jednostek (levelType) — wyłącznie z tabeli [ADR-0014](adr/0014-skroty-jednostek-w-eid.md)
- Separator segmentów: `__` (double underscore)
- Separator prefiks-numer: `_` (single underscore)
- Jednostki implicit (`IsImplicit = true`) są **pomijane** w eId
- Jednostka bez odczytanego oznaczenia też nie wnosi segmentu; w eksporcie nie ma `eId`, tylko
  `brak-eid="jednostka-nieoznaczona"` — [ADR-0024](adr/0024-jednostka-bez-oznaczenia-nie-ma-eid.md)
- eId jednostki redakcyjnej nie zawiera ścieżki jednostek systematyzacyjnych (`art_5`, nie
  `rozdz_1__art_5`); jednostka systematyzacyjna niesie ścieżkę jawnych przodków (`rozdz_3__oddz_1`)
  — [ADR-0017](adr/0017-eid-jednostki-redakcyjnej-bez-sciezki-systematyzacyjnej.md)
- Oznaczenie w eId: liczba arabska (także z rzymskiej i słownej — `ksi_1`, `dzial_2`), sufiks jak
  w tekście ([ADR-0018](adr/0018-postac-oznaczenia-w-eid.md)), indeks górny w notacji `[n]`
  (`art_53[1]`, [ADR-0019](adr/0019-indeks-gorny-w-eid.md)); segmenty składa `EIdSegment`
- `Id` w modelu to adres w akcie; prefiks aktu (`DU/2024/928__art_5`, w cytacie prefiks aktu zmienianego)
  dopisuje dopiero eksport — [ADR-0027](adr/0027-prefiks-aktu-dopisuje-eksport.md)
- Paragraf kodeksu (§ 55 ust. 3 ZTP) to `Paragraph` z prefiksem `par` zamiast `ust` (`art_2__par_1`),
  eksportowany jako `paragraf-kodeksu`; rolę nadaje `ParsingFactories.ApplyCodeParagraphRole` po znaku „§".
- Logika w `BaseEntity.Id` (getter) — buduje od bieżącej encji do korzenia, odwraca kolejność

### 4.4 Klasa bazowa `BaseEntity`

Wspólne cechy wszystkich encji:

| Właściwość | Typ | Opis |
|---|---|---|
| `Guid` | `Guid` | Unikalny identyfikator |
| `UnitType` | `UnitType` | Typ semantyczny (Article, Paragraph, Point...) |
| `DisplayLabel` | `string` | Etykieta wyświetlana ("art.", "ust.", "pkt"...) |
| `EIdPrefix` | `string` | Prefiks eId ("art", "ust", "par", "pkt", "lit", "tir") |
| `Number` | `EntityNumber?` | Numer encji z rozbiciem na komponenty |
| `ContentText` | `string` | Pełny tekst jednostki |
| `Parent` | `BaseEntity?` | Referencja do rodzica |
| `ValidationMessages` | `List<ValidationMessage>` | Diagnostyka parsowania |
| `Continuation` | `List<ContentBlock>` | Dalsza treść akapitu jednostki: akapity bez oznaczenia (wzór niesie też `Formula` z MathML), tabele i przytoczenia (`Quotation`) za jej zdaniami — [ADR-0029](adr/0029-akapit-bez-oznaczenia-jest-dalsza-trescia-jednostki.md) |
| `Id` | `string` (virtual) | Hierarchiczny eId budowany dynamicznie |

---

## 5. Pipeline parsowania — przepływ danych

### 5.1 Punkt wejścia (uniwersalne wejście — DOCX/PDF/TXT)

```
LegalDocumentParser.Parse(Stream, fileNameHint?, ParseOptions?)  →  ParseResult
    │
    ├── Strumień nieseekowalny → buforowany w pamięci (MemoryStream), read-only
    ├── Detekcja formatu:
    │     ├── options.ForcedFormat, jeśli podany i != Unknown
    │     └── w przeciwnym razie SourceFormatDetector.Detect(stream, fileNameHint) — sniffing sygnatur
    │         (ZIP/OOXML → Docx; nagłówek „%PDF-" → Pdf; heurystyka BOM/NUL → PlainText;
    │          rozszerzenie pliku rozstrzyga wyłącznie przypadki niekonkluzywne)
    ├── DocumentBlockReaderFactory.Create(format) → IDocumentBlockReader
    │     (DocxBlockReader / PdfBlockReader / PlainTextBlockReader)
    ├── reader.ReadBlocks(stream) → IReadOnlyList<DocumentBlock>
    │     (format-agnostyczna reprezentacja pośrednia — patrz 5.1b)
    └── Parse(blocks, options, format) — rdzeń:
          ├── DocumentClassifier.Classify(blocks) → DocumentClassificationResult
          │     (rodzaj aktu wg ZTP, IsLegalAct, pewność, sygnały — patrz 6.1c)
          ├── Decyzja o budowie modelu wg options.Policy (ParsePolicy):
          │     ├── AlwaysParse        → zawsze buduj
          │     ├── ClassifyOnly       → nigdy nie buduj (Document = null)
          │     └── ParseWhenLegalAct  → buduj tylko gdy classification.IsLegalAct (domyślna)
          ├── jeśli budowa → ParseBlocks(blocks) (rdzeń niezależny od formatu, patrz 5.2)
          │     i przypisanie document.Classification = classification
          └── zwraca ParseResult { Classification, Document, SourceFormat, BlockCount }

LegalDocumentParser.Parse(filePath, ParseOptions?)
    → otwiera plik read-only (File.OpenRead) i deleguje do Parse(Stream, ...)

LegalDocumentParser.Parse(IReadOnlyList<DocumentBlock>, ParseOptions?)
    → wejście z gotowych bloków (wywołujący sam przeprowadził odczyt adapterem);
      ten sam rdzeń, SourceFormat w kopercie = Unknown

[Obsolete] LegalDocumentParser.Parse(WordprocessingDocument)
    → ścieżka wsteczna: czyta bloki przez DocxBlockReader i zwraca LegalDocument
      BEZPOŚREDNIO, z pominięciem klasyfikacji dokumentu i koperty ParseResult
```

### 5.1b Warstwa odczytu formatu (`Ingest/`)

Każdy adapter formatu (`IDocumentBlockReader`) zamienia dokument źródłowy na listę `DocumentBlock` —
format-agnostyczny odpowiednik akapitu Word, wspólny dla DOCX/PDF/TXT:

| Właściwość `DocumentBlock` | Opis |
|---|---|
| `Text` | Tekst bloku (kanał indeksu górnego `[x]`, tabulatory `\t`, tiret zapisany symbolem (`w:sym` F02D czcionki Symbol) jako półpauza, łącznik nierozdzielający jako dywiz, przejście wiersza jako odstęp, wersaliki nadane przebiegowi wprost jako wielkie litery — wersaliki ze stylu zostają, jak zapisano; **nie** trymowany/sanityzowany) |
| `StyleId` | Identyfikator stylu Word — tylko DOCX; `null` dla PDF/TXT |
| `Layout` | Opcjonalne metadane układu (`BlockLayoutInfo` — wcięcia, pogrubienie, kursywa) — z DOCX/PDF |
| `Source` | Położenie w źródle (`BlockSourceLocation`) — diagnostyka |
| `Role` | `BlockRole` (`Body`, `FootnoteText`, `JournalHeader`, …) — bloki spoza głównego biegu treści są filtrowane przez orkiestrator i klasyfikator |
| `TableCell` | Komórka tabeli, w której stoi blok (`BlockTableCell`: numer tabeli, wiersz, kolumna siatki, `ColumnSpan`, `RowSpan`, kontynuacja scalenia pionowego, wiersz nagłówka) — tylko DOCX (`DocxTables`); `null` poza tabelą. Dziś nikt jej nie czyta: potok traktuje akapity tabel jak zwykłe |
| `Footnotes` | Przypisy, do których odwołuje się blok (`BlockFootnote`: oznaczenie jak w akcie, numer znacznika, ciąg numeracji i akapity treści) — tylko DOCX, gdzie treść przypisu leży w osobnej części pakietu (`DocxFootnotes`); w `Text` stoi znacznik `[N)]` z numerem `Marker`, jednoznacznym także wtedy, gdy sekcja dokumentu wznawia numerację ([ADR-0061](adr/0061-odnosnik-ma-oznaczenie-jak-w-akcie-a-znacznik-numer-jednoznaczny.md)) |
| `IsLandscape` | Czy blok stoi na stronie poziomej — z ustawień sekcji DOCX (`w:pgSz`: `w:orient` albo strona szersza niż wyższa); `null` dla PDF/TXT i sekcji bez rozmiaru strony. Czyta je eksport załączników ([ADR-0077](adr/0077-segment-zalacznika-to-ciag-stron-jednej-orientacji.md)) |
| `IsEmpty` | `true` gdy `Text` jest pusty/białe znaki |

`PdfBlockReader` (PdfPig) składa bloki z geometrii linii tekstu (`PdfLineExtractor`, `BlockAssembler`,
`PageArtifactFilter`) — bez stylów Word, więc klasyfikacja akapitu opiera się tam głównie na
warstwie syntaktycznej (regex) i układzie (wcięcia).

`DocxBlockReader` czyta — poza treścią — także nagłówki stron i emituje je jako bloki
`BlockRole.JournalHeader` bez `BlockIndex`. Emituje **wszystkie** części kandydujące, w kolejności
pierwszeństwa: najpierw podpięte jako `First` (Word zakłada osobny nagłówek pierwszej strony tylko
przy włączonej „innej stronie tytułowej"), potem `Default`. Adapter nie rozstrzyga, która część
„jest tą właściwą" — to należy do `JournalHeaderParser`, który bierze pierwsze dopasowanie każdej
wartości; dzięki temu winieta wygrywa z żywą paginą, a część pusta po prostu nic nie wnosi.
To jedyny kanał, którym metadane publikatora aktu ogłoszonego docierają do potoku; awaria odczytu
jest łapana i sprowadzana do pustej listy — treść aktu czyta się dalej.

### 5.2 Rdzeń budowy modelu (`LegalDocumentParser.ParseBlocks`, `internal`)

Niezależny od formatu źródłowego — działa wyłącznie na `IReadOnlyList<DocumentBlock>`:

```
ParseBlocks(blocks)
    │
    ├── Tworzy LegalDocument z domyślną hierarchią systematyzującą
    ├── JournalHeaderParser.Apply(blocks, document) — metadane publikatora z bloków JournalHeader
    │     (bez nagłówka: po budowie modelu JournalHeaderParser.ApplyFromAttachment — adres z wiersza
    │      „Załącznik do obwieszczenia … (Dz. U. poz. N)" albo z nagłówka załącznika, ADR-0073)
    ├── Tworzy ParsingContext (stan parsowania)
    ├── Tworzy ParserOrchestrator() — domyślny ParagraphClassifier
    ├── Iteruje po blocks (pomijając elementy null)
    │   └── ParserOrchestrator.ProcessBlock(block, context)
    └── ParserOrchestrator.Finalize(context)
          ├── flush bufora nowelizacji, jeśli dokument kończy się w jej trakcie
          └── context.Metadata.ApplyTo(document) — zapis Title/ActDate ze strefy tytułowej
```

`ParserOrchestrator.ProcessParagraph(Paragraph, context)` pozostaje jako cienki adapter
OpenXml → `DocumentBlock` (wsteczna kompatybilność testów i dotychczasowych wywołujących);
deleguje do `ProcessBlock`.

### 5.3 Orkiestrator — `ParserOrchestrator.ProcessBlock`

Jednoprzebiegowy pipeline ze stanem. Dla każdego bloku:

```
1. Jeśli block.IsEmpty → pomiń
2. Jeśli block.Role == FootnoteText albo JournalHeader → pomiń (przypisy ani nagłówek strony nie są
   treścią jednostek redakcyjnych; czytają je osobni konsumenci z pełnej listy bloków —
   klasyfikator dokumentu i JournalHeaderParser)
3. Zwinięcie odstępów (block.Text.CollapseWhitespace().Trim(), znaki jak w akcie) i odczyt StyleId (block.StyleId)
4. Budowanie NumberingHint (oczekiwana numeracja na bieżącym poziomie)
5. Klasyfikacja (ParagraphClassifier.Classify) → ClassificationResult (Kind, Confidence,
   IsAmendmentContent, StyleType, Penalties)
6. HandleAmendmentFlow (metoda prywatna orkiestratora):
   ├── AmendmentStateManager.UpdateState — aktualizuje InsideAmendment/trigger
   ├── jeśli właśnie wyszliśmy z nowelizacji → Flush()
   ├── ShouldExitForNewParentLawTrigger — wykrywa nowy trigger ustawy matki w trakcie
   │   zbierania (z uwzględnieniem QuoteBalanceTracker dla nowelizacji bez stylów)
   └── jeśli IsAmendmentContent lub InsideAmendment → Collect() i STOP (blok skonsumowany)
7. W przeciwnym razie: StructureProcessor.Process(context, classification, text, styleId, layout)
   ├── jednostki systematyzacyjne (Part/Book/Title/Division/Chapter/Subchapter) → SystematizingUnitBuilder
   ├── Article  → ArticleBuilder.Build()
   ├── Unknown  → wnioskowanie z tekstu / zbieranie metadanych strefy tytułowej / diagnostyka
   ├── Paragraph → ParagraphBuilder.Build()
   ├── Point    → PointBuilder.Build()
   ├── Letter   → LetterBuilder.Build()
   ├── Tiret    → TiretBuilder.Build() (głębokość ze znacznika „– –", stylu 2TIR/3TIR lub z wcięcia layoutu)
   └── WrapUp   → AttachWrapUpCommonPart (dla CZ_WSP_*)
8. Jeśli StructureProcessor zwrócił true → AmendmentStateManager.DetectTrigger (szuka
   "otrzymuje brzmienie:", "dodaje się", "uchyla się")
```

Aktualizacja referencji strukturalnej (`LegalReferenceService`), wykrywanie celów nowelizacji
i parsowanie publikatorów (`JournalReferenceService`, tylko dla artykułów) odbywają się
wewnątrz `StructureProcessor.Process`, nie jako osobne kroki orkiestratora (patrz 6.1).

### 5.4 Kontekst parsowania (`ParsingContext`)

Stan mutowalny przechowujący bieżącą pozycję w drzewie:

| Pole | Opis |
|---|---|
| `Document` | Aktualny `LegalDocument` (korzeń) |
| `Subchapter` | Bieżący oddział (kontener artykułów) |
| `CurrentPart/CurrentBook/CurrentTitle/CurrentDivision/CurrentChapter` | Bieżąca ścieżka jednostek systematyzujących, aktualizowana przez `SystematizingUnitBuilder` |
| `PendingHeadingUnit` | Jednostka systematyzacyjna oczekująca na tytuł (drugi wiersz wzorca dwuwierszowego, § 60 ZTP) |
| `CurrentArticle` | Ostatnio przetworzony artykuł |
| `CurrentParagraph` | Bieżący ustęp |
| `CurrentPoint` | Bieżący punkt |
| `CurrentLetter` | Bieżąca litera |
| `TiretStack` | Stos tiretów — `List<DtoTiret>` wspierający zagnieżdżone tirety (1TIR/2TIR/3TIR); `CurrentTiret` jako property `TiretStack[^1]` |
| `OpenTiretIndents` | Lista równoległa do `TiretStack` — wcięcia lewe (twips) tiretów otwartych na stosie, zasila wnioskowanie głębokości z układu (§ 58 ZTP) gdy nie rozstrzyga ani znacznik tiretu (`TiretMarker`: „– –" to poziom drugi), ani styl 2TIR/3TIR |
| `Metadata` | `DocumentMetadataCollector` — zbiera rodzaj/datę/przedmiot aktu ze strefy tytułowej (§ 16-19 ZTP) |
| `InsideAmendment` | Flaga: czy jesteśmy wewnątrz treści nowelizacji |
| `AmendmentTriggerDetected` | Flaga: czy wykryto zwrot "otrzymuje brzmienie:" |
| `AmendmentCollector` | Bufor akapitów nowelizacji (z wbudowanym `QuoteBalanceTracker` dla nowelizacji bez stylów) |
| `AmendmentOwner` | Encja-właściciel bieżącej nowelizacji |
| `ReferenceService` | Serwis referencji strukturalnych |
| `CurrentStructuralReference` | Bieżąca pozycja (art/ust/pkt/lit/tir) — aktualizowana przez `StructureProcessor` |
| `DetectedAmendmentTargets` | Słownik: `Guid` → lista `StructuralAmendmentReference` — cele nowelizacji detektowane przez `StructureProcessor` (wyliczenie to kilka celów, przedział jeden — [ADR-0016](adr/0016-wyliczenie-celow-to-osobne-komendy.md)) |

---

## 6. Role poszczególnych klas

### 6.1 Warstwa parsowania (`Services/Parsing/`)

| Klasa | Rola |
|---|---|
| **`ParserOrchestrator`** | Orkiestrator — jednoprzebiegowy pipeline sterujący całym procesem parsowania. Konstruktor: `(IParagraphClassifier? classifier = null)` z DI. Metoda `ProcessBlock(DocumentBlock, ParsingContext)` (format-agnostyczna); `ProcessParagraph(Paragraph, context)` to cienki adapter OpenXml → `ProcessBlock`. Koordynuje klasyfikację, zarządzanie nowelizacjami i budowanie struktury. |
| **`StructureProcessor`** (`internal sealed`) | Przetwarzanie struktury — buduje encje na podstawie `ClassificationResult`. Obsługuje mapowanie `Kind` na typ buildera (w tym jednostki systematyzacyjne przez `SystematizingUnitBuilder`), tworzenie implicit encji, obsługę `WrapUp`, wnioskowanie `Unknown` (tekst / metadane strefy tytułowej), aktualizację `CurrentStructuralReference` i wykrywanie celów nowelizacji (`DetectAmendmentTargets`). |
| **`SystematizingUnitBuilder`** (w `Builders/`) | Buduje jednostki systematyzacyjne (Część→Księga→Tytuł→Dział→Rozdział→Oddział, § 60-62 ZTP) i utrzymuje bieżącą ścieżkę w `ParsingContext`. |
| **`DocumentMetadataCollector`** | Zbiera metadane aktu (rodzaj, organ, data, przedmiot) ze strefy tytułowej (§ 16-19, § 102, § 120 ZTP); karmiony akapitami `Unknown` sprzed pierwszego artykułu; `ApplyTo(document)` zapisuje `Title`/`ActDate` przy finalizacji. Wiersz rozpoznaje wzorzec tekstowy; słowo rodzaju w pisowni spoza wzorca i tytuł o nietypowym początku wymagają drugiego sygnału — stylu wiersza (`OZNRODZAKTU…`, `TEKSTOBWIESZCZENIE…`, `TYTUAKTU…`) albo miejsca (`ExpectKindFirst`: pierwszy wiersz aktu w załączniku do obwieszczenia). Organ aktu wydanego wspólnie składa z dwóch wierszy. |
| **`JournalHeaderParser`** | Czyta metadane publikatora (dziennik, data ogłoszenia, pozycja) z bloków `BlockRole.JournalHeader`; przebieg wstępny w `ParseBlocks`, przed pętlą bloków. Wymaga rozpoznanego publikatora i co najmniej jednej z wartości data/pozycja — nierozpoznany nagłówek daje brak metadanych, nigdy wyjątek ([ADR-0012](adr/0012-naglowek-strony-jako-rola-bloku.md)). |
| **`AmendmentStateManager`** | Zarządzanie cyklem życia nowelizacji — `UpdateState()`, `Collect()`, `Flush()`, `DetectTrigger()`. Wyodrębniony z orkiestratora dla lepszej separacji concerns. Wyzwalacz treści cytowanej uzbraja tylko zwrot zmiany brzmienia, który zostawia brzmienie otwarte — kończy akapit albo otwiera cytat niedomknięty w akapicie (`AmendmentFinalizer.LeavesWordingOpen`, [ADR-0045](adr/0045-zwrot-zmiany-brzmienia-zapowiada-tresc-gdy-zostawia-ja-otwarta.md)); przepis z brzmieniem w tym samym akapicie komendą nie jest. Treść cytowaną bez stylu i bez cudzysłowu otwierającego kończy kolejny artykuł aktu zmieniającego, formuła podpisu albo cudzysłów zamykający bez otwarcia ([ADR-0048](adr/0048-tresc-cytowana-bez-cudzyslowu-konczy-kolejny-artykul-aktu-zmieniajacego.md)). Cytat otwarty i niedomknięty do końca dokumentu kończy się przed artykułem, od którego numeracja aktu zmieniającego biegnie dalej — wskazuje go `OpenQuotationTrail`, a `LegalDocumentParser.ParseBlocks` buduje dokument ponownie ([ADR-0049](adr/0049-cytat-niedomkniety-do-konca-dokumentu-konczy-ponowny-przebieg.md)). Czasownik uchylenia („uchyla się", „skreśla się") jest komendą dopiero z drugim sygnałem — w artykule zmieniającym inny akt albo z uchylaną jednostką zaraz za nim (`IsRepealCommand`, [ADR-0069](adr/0069-czasownik-uchylenia-jest-komenda-w-artykule-zmieniajacym-albo-przy-jednostce.md)); w przepisie merytorycznym („uchyla się od wykonania obowiązku") komendą nie jest. Treść zebrana pod przepisem, który niczego nie zmienia, nie jest nowelizacją, tylko treścią tego przepisu: tekst w cudzysłowie — przytoczeniem (`ContentBlock.Quotation` → `tresc-strukt` przez `QuotationXmlConverter`), tekst bez cudzysłowu — dalszą treścią (`TryKeepAsContentOfTheProvision`, [ADR-0071](adr/0071-tresc-pod-przepisem-ktory-niczego-nie-zmienia-jest-jego-trescia.md)). Cudzysłów otwarty akapitem pod takim przepisem i niedomknięty zbiera kolejne akapity bez względu na styl, aż do domknięcia (`OpensProvisionQuote`, [ADR-0076](adr/0076-cudzyslow-otwarty-pod-przepisem-trwa-do-domkniecia.md)). Otwarty cudzysłów zebranej treści przeważa nad stylem ustawy matki ([ADR-0085](adr/0085-otwarty-cudzyslow-cytatu-przewaza-nad-stylem-ustawy-matki.md)). |
| **`QuoteBalanceTracker`** | Bilans cudzysłowów dla nowelizacji bez stylów `Z/*` (§ 94 ZTP) — wyznacza koniec cytowanej treści przez parzystość cudzysłowów, gdy brak sygnału stylowego. |
| **`QuotationMarks`** | Jedyne źródło wariantów cudzysłowu (`„`, `”`, `“`, `ˮ`, `"`) dla bilansu cytatu, granicy cytatu, komend wyrazowych i prefiksu oznaczenia; tekst pozostaje nietknięty ([ADR-0030](adr/0030-warianty-cudzyslowu-rozpoznaje-jedno-zrodlo.md)). |
| **`Dashes`** | Jedyne źródło wariantów myślnika (dywiz, dywiz typograficzny i nierozdzielający, myślnik cyfrowy, półpauza, pauza, kreska pozioma, minus) dla znacznika tiretu, części wspólnej po myślniku, przedziału oznaczeń i legendy wzoru; tekst pozostaje nietknięty — [ADR-0040](adr/0040-warianty-myslnika-z-jednego-zrodla.md). |
| **`AmendmentCommandParser`** | Rozpoznaje rodzaj komendy nowelizacyjnej z treści (`AmendmentCommandKind`: Change/Add/Repeal/ReplaceWords) — źródło niższego priorytetu niż mapa stylów. |
| **`ParsingContext`** | Stan parsowania — przechowuje bieżącą pozycję w drzewie (w tym jednostki systematyzacyjne i metadane aktu). Mutowany przez orkiestrator/`StructureProcessor`. |
| **`ParsingFactories`** | Fabryki statyczne — tworzenie encji, parsowanie numerów, usuwanie prefiksów numeracyjnych, podział tekstu na zdania (`SplitIntoSentences`). |
| **`AmendmentCollector`** | Bufor nowelizacji — zbiera akapity treści nowelizacji od momentu wejścia (trigger) do momentu powrotu do stylu ustawy matki. |
| **`AmendmentFinalizer`** | Finalizator nowelizacji — wykrywa typ operacji (Modification/Insertion/Repeal), tworzy obiekt `Amendment`, łączy z JournalInfo, przypisuje do encji-właściciela. Ostrzega, gdy jednostek treści nie da się przypisać wyliczonym celom. |
| **`AmendmentTargetPairing`** | Przypisuje jednostki treści nowelizacji jej celom — po oznaczeniu, a przy rozjeździe oznaczeń i równej liczbie po kolejności. Korzystają z niego eksport (komenda na cel) i walidacja finalizatora. |
| **`CommonPartPlacement`** | Rozstrzyga część wspólną, po której przychodzi kolejna pozycja tego samego wyliczenia: zamyka niższe wyliczenie albo przerywa bieżące (`CommonPart.InterruptsAfter`). Wołany przez `StructureProcessor` i `AmendmentBuilder`. |
| **`ActSignatureParser`** | Rozpoznaje formułę podpisu za ostatnim przepisem („Prezydent Rzeczypospolitej Polskiej: A. Duda") — organ (także z przecinkiem w nazwie), inicjały, nazwisko; podpis w zastępstwie albo z upoważnienia („Minister Finansów: wz. J. Neneman", „…: z up. P. Bejda") zachowuje skrót w `ActSignature.Capacity`. Formuła, po której stoi kolejny artykuł, podpisem aktu nie jest; kolejna formuła tuż za pierwszą to podpis współwydającego (`LegalDocument.CoSignatures`). |
| **`AnnexBuilder`** | Składa załączniki z bloków zebranych za nagłówkiem „Załącznik(i) do ustawy z dnia …" albo — w akcie wykonawczym — „Załącznik do rozporządzenia Ministra … z dnia …", „Załącznik do uchwały nr 113 Rady Ministrów z dnia …" (`ParsingContext.AnnexBlocks`): wskazanie aktu z wierszem „(Dz. U. …)", oznaczenie w osobnym wierszu (ze znacznikiem odnośnika albo dopiskiem „(uchylony)"), tytuł (styl tytułu albo wyśrodkowany pierwszy wiersz), treść. Treści nie rozkłada na jednostki — załącznik nie przechodzi przez klasyfikator akapitów. Tytułem jest wiersz w stylu tytułu albo — bez stylu — pierwszy wiersz wyśrodkowany lub o budowie tytułu (`TitleForm`: wielka litera, bez kropki, dwukropka, średnika i przecinka na końcu), który nie jest pozycją wyliczenia. Objaśnienia gwiazdki na końcu treści nie wchodzą do wyliczenia ([ADR-0083](adr/0083-objasnienie-gwiazdki-nie-jest-pozycja-wyliczenia-a-jej-znacznik-jest-indeksem-gornym.md)). |
| **`AnnouncementBoundary`** | Tekst jednolity: dokument zaczyna się nagłówkiem obwieszczenia, a ogłaszany akt — wierszem „Załącznik do obwieszczenia …" ([ADR-0031](adr/0031-tekst-jednolity-pod-korzeniem-obwieszczenie.md)). Bloki sprzed tego wiersza omijają potok aktu (`ParserOrchestrator.CollectAnnouncementBlock`): wiersze nagłówka czyta kolektor metadanych, reszta trafia do `LegalDocument.Announcement` (akapity i tabele). Na granicy kolektor jest wymieniany (`ParsingContext.BeginAnnouncedAct`), więc strefa tytułowa ogłaszanego aktu („USTAWA", data, przedmiot) trafia do `Announcement`, a nagłówkiem dokumentu pozostaje nagłówek obwieszczenia. Nagłówek aktu tuż za tym wierszem nazywa jego rodzaj (`Layout.AnnouncedKind`): rozporządzenie, uchwała i zarządzenie mają paragraf za jednostkę podstawową także wtedy, gdy klasyfikator dokumentu — ważący liczbę jednostek — tego nie stwierdził (krótki akt). Czy obwieszczenie ogłasza akt, rozstrzyga `Describe` — przedmiot („w sprawie ogłoszenia jednolitego tekstu …") albo rodzaj aktu tuż za wierszem załącznika; obwieszczenie, które aktu nie ogłasza i nie jest zbudowane z artykułów, jest obwieszczeniem w całości (`Layout.IsPlainAnnouncement`, `Announcement.AnnouncesAct = false`): jego treść przechodzi przez `AnnouncementUnits`, pierwszy akapit „Na podstawie art. …" staje się podstawą prawną (`AnnouncementUnits.TakeLegalBasis`), a bloki za wierszem załącznika — załącznikami obwieszczenia; gdy takie obwieszczenie samo jest zbudowane z paragrafów albo artykułów (`Layout.HasProvisions`), jego przepisy buduje potok aktu, a paragraf jest jednostką podstawową z samej treści (`Layout.HasSections`) ([ADR-0043](adr/0043-obwieszczenie-ktore-nie-oglasza-aktu-jest-korzeniem.md)). |
| **`AnnouncementBody`** | Treść obwieszczenia, które nie ogłasza aktu i nie ma numerowanych punktów ([ADR-0044](adr/0044-tresc-obwieszczenia-bez-punktow-jest-przepisem-bez-oznaczenia.md)): podpisy, podstawa prawna (gdy nie otwiera wyliczenia ani tabeli) i jeden przepis bez oznaczenia — pierwszy akapit jest jego tekstem, reszta przechodzi przez potok aktu pod artykułem zastępczym, więc punkty, litery, tirety, część wspólna i dalsza treść są zwykłymi jednostkami modelu (`Announcement.Body`). Gdy potok nie złoży jednego przepisu, treść buduje `AnnouncementUnits`, a to, co nie stało się jednostką (`Announcement.Content`), eksport pisze płasko jako przepis bez oznaczenia z dalszą treścią (`AnnouncementXmlConverter.LooseContent`) — nic nie ginie. |
| **`AnnouncementUnits`** | Jednostki obwieszczenia z samej treści, poza potokiem aktu ([ADR-0032](adr/0032-podpunkt-obwieszczenia-ma-skrot-ppkt.md)): „N." to punkt (`Paragraph` z `EIdPrefix` `pkt`), „N)" pod punktem — podpunkt (`Point` z `ppkt`), akapit tuż za ostatnim podpunktem bez znaku końcowego — część wspólna podpunktów. Przytoczenie (od cudzysłowu otwierającego do domknięcia) i tabela są dalszą treścią jednostki, za którą stoją; formuła podpisu trafia do `LegalDocument.Signature`. Przytoczenie złożone z całych artykułów (paragrafów aktu wykonawczego) buduje `AmendmentBuilder` i trafia do jednostki jako `Quotation` z aktem przytaczanym odczytanym z jej treści (`JournalReferenceService.ReadSingleAct`); inne — w tym przytoczony ustęp — zostaje akapitami tekstu ([ADR-0036](adr/0036-przytoczone-artykuly-sa-jednostkami-aktu-przytaczanego.md)). Przytoczony przepis zmieniający ma własne cytaty jako nowelizacje ([ADR-0084](adr/0084-przytoczony-przepis-zmieniajacy-jest-jednostka-z-wlasnymi-nowelizacjami.md)); tabela w przytoczeniu jest dalszą treścią jednostki albo — tuż za komendą — treścią nowelizacji, a cudzysłowy w jej komórkach liczą się do bilansu przytoczenia. |
| **`FootnoteContent`** | Treść odnośnika za pierwszym akapitem jako jednostki odnośnika — punkty, litery, tirety, części wspólne i dalsze akapity — czytane z treści wzorcami jednostek aktu; wyliczenie, którego schemat nie wyraża, zostaje akapitem ([ADR-0078](adr/0078-wyliczenie-w-odnosniku-to-jego-jednostki.md)) |
| **`QuotedTextSubject`** | Komenda o zdaniu albo akapicie („zdanie pierwsze otrzymuje brzmienie:", „akapity czwarty i piąty otrzymują brzmienie:"): treść cytowana jest tekstem, bez odczytu jednostek i bez względu na styl; zdanie biegnące przez wyliczenie zostaje jednostkami ([ADR-0079](adr/0079-zdanie-albo-akapit-jako-przedmiot-komendy-czyni-tresc-cytowana-tekstem.md)) |
| **`NestedQuotes`** / **`NestedAmendment`** | Cytat zagnieżdżony w treści cytowanej (akapit z cudzysłowem po przepisie-komendzie) jest nowelizacją cytowanego przepisu: `AmendmentBuilder` buduje go rekurencyjnie, a rodzaj, cel i akt zmieniany czyta z cytowanego przepisu i jego przodków ([ADR-0082](adr/0082-cytat-zagniezdzony-jest-nowelizacja-cytowanego-przepisu.md)). Wiersze cudzysłowu otwartego w środku akapitu są dalszą treścią jego jednostki ([ADR-0084](adr/0084-przytoczony-przepis-zmieniajacy-jest-jednostka-z-wlasnymi-nowelizacjami.md)) |
| **`ContentTables`** | Składa `ContentTable` z bloków jednej tabeli źródłowej według `DocumentBlock.TableCell`; dalszy ciąg komórki scalonej w pionie nie jest osobną komórką |
| **`AnnexUnits`** | Wyliczenie w treści załącznika jako jednostki redakcyjne: akapity przechodzą przez potok aktu pod artykułem zastępczym (niejawnym, z `AddressAnchor` = adres załącznika), a ustęp z oznaczeniem rzymskim („I.") powstaje tu wprost. Zwraca jednostki tylko wtedy, gdy cała treść jest wyliczeniem i potok niczego nie pominął — [ADR-0028](adr/0028-wyliczenie-w-zalaczniku-buduje-potok-aktu.md) |
| **Dalsza treść jednostki** (`StructureProcessor.HandleUnknown`, `ParserOrchestrator.FlushTable`) | Akapit, którego potok nie rozpoznał, trafia do `Continuation` najgłębszej otwartej jednostki (`StructureProcessor.DeepestUnit`); bloki tabeli stojącej w treści przepisu omijają klasyfikację, są zbierane w `ParsingContext.TableBlocks` i na końcu tabeli stają się jej `ContentTable`. Tabela zaczęta w treści nowelizacji (`ParsingContext.TableInAmendment`) jest treścią cytowaną: `AmendmentStateManager.CollectTable` dokłada ją do kolektora, a `AmendmentBuilder` przypina do jednostki, za którą stoi, albo — gdy cytat nie ma jednostek — do `AmendmentContent.Tables` |
| **`ValidationReporter`** | Reporter diagnostyczny — statyczne metody do rejestrowania ostrzeżeń o konfliktach styl/treść i brakujących stylach na encjach DTO. |

### 6.1b Warstwa klasyfikacji akapitów (`Services/Classify/`)

Wyodrębniona warstwa zajmująca się klasyfikacją akapitów i oceną pewności klasyfikacji.
Klasyfikator jest **jednoprzebiegowy** (`ParagraphClassifier`, `sealed class`) — nie ma osobnych klas
warstwowych ani katalogu `Classification/`; "warstwy" (styl/regex/numeracja/konflikt) to etapy
jednej metody `Classify`, nie osobne obiekty.

| Klasa | Rola |
|---|---|
| **`IParagraphClassifier`** | Interfejs klasyfikatora — `Classify(ClassificationInput) → ClassificationResult`. Umożliwia DI i testowanie. |
| **`ParagraphClassifier`** | Jedyna implementacja klasyfikatora — łączy sygnały: styl OpenXml + regex + NumberingHint, obsługuje rozwiązywanie konfliktów. |
| **`ClassificationInput`** | Record wejściowy klasyfikatora: `(Text: string, StyleId: string?, NumberingHint: NumberingHint?)`. |
| **`ClassificationResult`** | Record wyniku klasyfikacji: `Kind` (enum), `Confidence` (1–100), `IsAmendmentContent: bool`, `StyleType: string?`, `Penalties: IReadOnlyList<ClassificationPenalty>`. |
| **`ParagraphKind`** | Enum typów akapitów (14 wartości): jednostki redakcyjne — `Article`, `Paragraph`, `Point`, `Letter`, `Tiret`, `WrapUp`; jednostki systematyzacyjne (§ 60-62 ZTP) — `PartUnit`, `BookUnit`, `TitleUnit`, `DivisionUnit`, `ChapterUnit`, `SubchapterUnit`, `UnitHeading`; oraz `Unknown`. |
| **`ClassificationPenalty`** | Model kary: `(Reason: string, Value: int)` — obniża Confidence. |
| **`ConfidencePenaltyConfig`** | Statyczne konfiguracje kar: `StyleAbsentPenalty`, `SyntaxAbsentPenalty`, `StyleSyntaxConflictPenalty`, `NumberingBreakPenalty`. |
| **`NumberingHint`** | Model wskazówki numeracji — `ExpectedKind`, `ExpectedNumber?`, `IsContinuous(actual)`, `GetNextLetterValue()`. Obliczany przez orkiestrator (`BuildNumberingHint`) przed klasyfikacją; kara `NumberingBreakPenalty` stosowana wewnątrz `ParagraphClassifier.BuildResult`, gdy rozpoznany `Kind == ExpectedKind`, ale numer nie jest ciągły. |
| **`IConflictResolver`** | Interfejs rozwiązywania konfliktów styl↔treść. |
| **`DefaultConflictResolver`** | Domyślna implementacja: syntaktyka (regex) wygrywa nad stylem. |

### 6.1c Warstwa klasyfikacji dokumentu (`Services/Classify/Document/`)

Rozpoznaje **rodzaj całego dokumentu** (nie akapitu) wg ZTP, zanim (opcjonalnie) zbudowany zostanie
model strukturalny. Wywoływana przez `LegalDocumentParser.Parse(blocks, options, format)` — patrz 5.1.

| Klasa | Rola |
|---|---|
| **`IDocumentClassifier`** | Interfejs — `Classify(IReadOnlyList<DocumentBlock>) → DocumentClassificationResult`. Nie ocenia normatywności ani nie decyduje o parsowaniu — wyłącznie raportuje. |
| **`DocumentClassifier`** | Implementacja dwufazowa: (A) strefa tytułowa — pierwsze 25 niepustych bloków (nagłówek rodzaju aktu, organ, data, przedmiot); (B) statystyka korpusu (formuła kompetencyjna, dominacja jednostki podstawowej Art./§, wejście w życie, markery TJ, komendy nowelizacyjne). Punktacja rozdziela sygnały różnicujące typ od bonusu „aktowości"; wymaga silnego sygnału strukturalnego (`hasBackbone`) i wyniku ≥ progu (40), inaczej `IsLegalAct = false`. |
| **`ZtpPatterns`** | Skompilowane wzorce regex sygnałów dokumentu (nagłówki rodzajów aktu, formuła TJ, formuła kompetencyjna, wejście w życie, komendy nowelizacyjne, publikator woj., markery TJ, organ JST…). |
| **`DocumentClassificationResult`** (Saga.Model) | `ActType: LegalActType?`, `IsLegalAct`, `IsConsolidatedText`, `IsAmending`, `Confidence` (1–100), `Signals: IReadOnlyList<DocumentSignal>`, `Justification`. |
| **`DocumentSignal`** / **`DocumentSignalKind`** (Saga.Model) | Pojedynczy dowód klasyfikacji: rodzaj sygnału, wynik, indeks bloku, dopasowany fragment, opis. |

`ParseResult`/`ParseOptions`/`ParsePolicy` (w `Saga.Core`, poza `Services/`) spinają tę warstwę
z rdzeniem budowy modelu — patrz 5.1.

---

### 6.2 Buildery encji (`Services/Parsing/Builders/`)

Pattern: `IEntityBuilder<TInput, TResult>` — wspólny kontrakt.

| Builder | Input | Output | Opis |
|---|---|---|---|
| `ArticleBuilder` | `ArticleBuildInput(Subchapter, text)` | `ArticleBuildResult(Article, Paragraph)` | Tworzy artykuł i pierwszy ustęp z ogona tekstu "Art." |
| `ParagraphBuilder` | `ParagraphBuildInput(...)` | `Paragraph` | Tworzy ustęp; metoda `EnsureForPoint()` tworzy implicit ustęp jeśli brak |
| `PointBuilder` | `PointBuildInput(...)` | `Point` | Tworzy punkt; `EnsureForLetter()` tworzy implicit punkt jeśli brak |
| `LetterBuilder` | `LetterBuildInput(...)` | `Letter` | Tworzy literę; `EnsureForTiret()` tworzy implicit literę jeśli brak |
| `TiretBuilder` | `TiretBuildInput(...)` | `Tiret` | Tworzy tiret z indeksem sekwencyjnym |
| `AmendmentBuilder` | `AmendmentCollector` | `AmendmentContent` | Buduje hierarchiczną treść nowelizacji z zebranych akapitów; akapit bez oznaczenia i tabela są dalszą treścią jednostki, za którą stoją (`Continuation`, [ADR-0029](adr/0029-akapit-bez-oznaczenia-jest-dalsza-trescia-jednostki.md)), a bez jednostki przed sobą — tekstem albo tabelą cytowaną wprost (akapit za taką tabelą jest jej objaśnieniem, a cudzysłowy w jej komórkach liczą się w bilansie cytatu — [ADR-0050](adr/0050-cudzyslowy-cytowanej-tabeli-licza-sie-w-bilansie-cytatu.md); cudzysłów graniczny stojący w komórce zdejmuje z tabeli `QuotedTableBoundaries` — [ADR-0051](adr/0051-cudzyslow-graniczny-w-komorce-tabeli-nalezy-do-cytatu.md)); artykuł wymaga oznaczenia w treści (sam styl go nie czyni), a akapit w stylu innej jednostki bez oznaczenia jest dalszą treścią jednostki przed nim (ADR-0039) |

Poza wzorcem `IEntityBuilder<TInput, TResult>` (jednostki redakcyjne) w tym samym katalogu żyje:

| Builder | Rola |
|---|---|
| `SystematizingUnitBuilder` | Buduje jednostki systematyzacyjne (Część/Księga/Tytuł/Dział/Rozdział/Oddział) i utrzymuje bieżącą ścieżkę w `ParsingContext` (`Enter(ctx, kind, number)`) — patrz 6.1 |

Kaskadowe tworzenie encji implicit:
- Punkt wymaga ustępu → `ParagraphBuilder.EnsureForPoint()`
- Litera wymaga punktu → `PointBuilder.EnsureForLetter()`
- Tiret wymaga litery i punktu → kaskadowe `Ensure*()`

### 6.3 Serwisy domenowe (`Services/`)

| Serwis | Rola |
|---|---|
| **`EntityNumberService`** | Parsowanie numerów encji z tekstu na `EntityNumber` (rozbicie na `NumericPart` / `LexicalPart` / `Superscript`) i formatowanie zwrotne. |
| **`DesignationAlphabet`** | Litery w oznaczeniach jednostek — łacińskie i polskie („art. 48ł", „lit. ł") — jako jedno źródło dla wzorców klasyfikatora, powołań (`UnitReferenceTokenizer`) i ciągłości numeracji (`NumberingHint`: l → ł → m). |
| **`FootnoteMarkers`** | Znacznik odnośnika w kanale tekstowym (`[9)]`): wzorzec dla klasyfikatora i wygaszanie znaczników przed czytaniem powołań, żeby numer odnośnika nie stał się oznaczeniem jednostki. |
| **`LegalReferenceService`** | Cele nowelizacji z treści encji („w art. 5 § 2 pkt 3") jako lista `StructuralAmendmentReference`, bez zdania — chyba że samo zdanie jest uchylane („skreśla się zdanie drugie" → `…ust_2__zdn_2`, [ADR-0070](adr/0070-uchylane-zdanie-jest-adresowane-zdaniem-nie-jednostka.md)); kontekstem jest cel komendy nadrzędnej. Sam nie rozbiera powołań — deleguje do `UnitAddressReader` (6.3a). |
| **`JournalReferenceService`** | Parsowanie publikatorów (Dz. U. i M.P. — dziennik jest częścią tożsamości aktu: `DU/2025/1792`, `MP/2023/1029`) z treści artykułów nowelizujących i uzupełnianie listy `JournalInfo`; `ReadSingleAct` czyta jedyny akt wskazany w dowolnym tekście (przytoczenia w obwieszczeniu). Przepis zmieniający poznaje po formule „W ustawie … z dnia" albo „W rozporządzeniu <organ> z dnia" na początku jednostki. Rocznik pominięty w publikatorze to rok z daty aktu, którego tytuł go poprzedza (§ 158 ust. 7 ZTP); bez tej daty publikator jest pomijany. |

### 6.3a Powołania jednostek (`Services/UnitReferences/`)

Jedno źródło wiedzy o powołaniach jednostek dla wejścia w życie i celów nowelizacji —
[ADR-0015](adr/0015-jedno-zrodlo-powolan-jednostek.md). Nowe miejsce czytające powołania korzysta
z tych klas i nie definiuje własnych wzorców `art.`/`ust.`/`lit.`.

| Klasa | Rola |
|---|---|
| **`UnitReferenceTokenizer`** | Jedyna definicja słownika: słowa kluczowe jednostek, kształt oznaczeń i przedziałów (wystawione jako wzorce dla parsera komend). Przyimek przed słowem kluczowym („w art. 5 w ust. 2") to proza, „i" zaraz po „lit." to litera. Normalizuje też szyk „liczebnik przed słowem kluczowym": „tiret drugie pierwsze podwójne tiret" to jedna ścieżka `tir_2__dwtir_1`. |
| **`DesignationRange`** | Rozwija przedziały oznaczeń („85c–85j", „19-21", „a–c", „czwarte–szóste"). Przedział niejednoznaczny („5a"–„7b") zwraca null, żeby wywołujący go oflagował zamiast zgadywać. |
| **`UnitPathCursor`** | Reguły ścieżki: dziedziczenie poziomu przez gołe oznaczenie, kształt oznaczenia na poziomie (§ 57 ZTP) i rozstrzygnięcie „§": po artykule — paragraf kodeksu; samodzielny — według `SectionSignRole`; po paragrafie aktu opartego na paragrafach — kolejny paragraf („§ 6 i § 7", „po § 3 dodaje się § 3a"; [ADR-0035](adr/0035-paragraf-po-paragrafie-to-kolejny-paragraf.md)). Może startować od znanej ścieżki (kontekst komendy nadrzędnej). |
| **`SectionSignRole`** | Czy samodzielny „§" jest w dokumencie jednostką główną — według rodzaju aktu z klasyfikatora dokumentu (dla obwieszczenia: ogłaszany akt wg sygnału przewagi jednostki podstawowej). W ustawie nie jest: to paragraf kodeksu nieznanego artykułu, którego się nie zgaduje. |
| **`UnitAddressReader`** | Czyta miejsce w załączniku („w załączniku nr 1 …") jako `StructuralReference.Annex`: jednostki pod nim są jednostkami załącznika, a wzmianka („o których mowa w załączniku …") miejscem nie jest ([ADR-0080](adr/0080-komenda-w-zalaczniku-wskazuje-jednostki-zalacznika.md)). Czyta także jednostki systematyzacyjne („po rozdziale 9", „w dziale II", „uchyla się rozdziały 4 i 5") jako `StructuralReference.Systematizing` (`dzial_2__rozdz_9`); w adresie komendy jednostka systematyzacyjna jest prefiksem artykułu, który zeruje tę ścieżkę (ADR-0017). Adres z tekstu komendy: fragmenty w cudzysłowie nie wskazują jednostek, oznaczenie liczy się tylko w ciągu rozpoczętym słowem kluczowym. Zwraca wszystkie cele: jednostkę, przedział albo wyliczenie — to ostatnie trwa, dopóki kolejną jednostkę dołącza separator wyliczenia ([ADR-0016](adr/0016-wyliczenie-celow-to-osobne-komendy.md)). |

### 6.3b Wejście w życie (`Services/EntryIntoForce/`)

Uruchamiany raz, po zbudowaniu modelu — wymaga kompletnego drzewa, wraz z nowelizacjami.
Punktem odniesienia dla formuł względnych („po upływie 14 dni od dnia ogłoszenia") jest
`LegalDocument.AnnouncementDate`. Parser nie odczytuje jej z dokumentu, więc te formuły
pozostają nierozstrzygnięte — patrz [backlog § 3b](backlog.md).

Jednostka niesie DWIE daty: datę wejścia w życie i moc wsteczną („z mocą od dnia …", § 51 ZTP).
Rozsyłane są niezależnie, a przysłonięcie jednej drugą zachodzi dopiero przy zapisie —
patrz [ADR-0013](adr/0013-moc-wsteczna-przyslania-date-dopiero-na-wyjsciu.md).

| Klasa | Rola |
|---|---|
| **`EntryIntoForceService`** | Orkiestracja: skan → reguła ogólna → wyjątki → człony „z tym że" → mapa wyjątków → propagacja. Reguła ogólna kończy się na pierwszym znaczniku zmieniającym adresata — bez tego moc wsteczna nadana jednej jednostce zostaje odczytana jako moc wsteczna aktu. |
| **`EntryIntoForceClauseScanner`** | Znajduje kandydatów na klauzulę w kolejności dokumentu, z filtrami negatywnymi (klauzula w cytacie, w artykule nowelizującym, wystąpienie rzeczownikowe). |
| **`EntryIntoForceFormulaParser`** + **`EntryIntoForcePatterns`** | Rozpoznanie brzmienia z zamkniętego katalogu § 45 ZTP; operand liczbowy albo słowny (przez `PolishNumerals`). Odcina też moc wsteczną (`SplitRetroactivity`) i rozbija człon „z tym że" na pary (jednostki, moc wsteczna) — jeden człon niesie ich czasem kilka. |
| **`EntryIntoForceDateResolver`** | Arytmetyka dat dla rozpoznanej formuły. |
| **`UnitEnumerationParser`** | Rozkład powołania jednostek („art. 1 pkt 9 w zakresie art. 85a ust. 1") na ścieżki na wspólnym źródle powołań (6.3a) — patrz [ADR-0010](adr/0010-jeden-kursor-sciezki-jednostek.md). Granica zakresu: wewnątrz „w zakresie…" wolno zostać na poziomie albo zejść głębiej, ale powrót W GÓRĘ kończy zakres i wraca do wyliczenia głównego („…w zakresie uchylanego pkt 5 i art. 5"). |
| **`EntryIntoForceTargetResolver`** | Odnajduje encję wskazaną ścieżką — w akcie i w przytoczonej treści nowelizacji (`AmendmentOf`: artykuł bez ustępów niesie nowelizację w ustępie niejawnym). Jednostkę systematyzacyjną („z wyjątkiem rozdziału 4") znajduje po końcu jej eId; gdy pasuje kilka, nie wskazuje żadnej. Rozstrzyga ścieżki, dat NIE zapisuje. |
| **`EditorialUnitWalker`** | Jedyne miejsce, które wie, co jest czyim dzieckiem; treść cytatu odwiedzana tylko na żądanie. |
| **`EntryDates`** | Para (data wejścia w życie, moc wsteczna) chodząca po propagacji razem — dwa gołe `DateTime?` w sygnaturze dałyby się zamienić miejscami. |
| **`EntryIntoForceExceptionMap`** | Rozstrzygnięcia wyjątków przypięte do encji (klucz po tożsamości, bo eId nie jest w akcie unikalny). Dwa NIEZALEŻNE kanały — daty wejścia w życie i mocy wstecznej — bo wyjątek nie musi nadpisywać obu składników. W kanale daty `null` = cel znany, data nie; w kanale mocy wstecznej `null` = mocy wstecznej nie ma. |
| **`EffectiveDatePropagator`** | Kolorowanie drzewa PARĄ dat: reguła ogólna trafia na wszystkie jednostki i zdania, wyjątek nadpisuje ją w dół, a **brak daty znaczy nierozstrzygnięty wyjątek** — patrz [ADR-0011](adr/0011-data-ogolna-rozsylana-po-calym-drzewie.md). Składniki dziedziczą się osobno: jednostka może wziąć datę z reguły ogólnej, a moc wsteczną z wyjątku wskazującego ją samą. |
| **`LifecycleEvents`** | Zdarzenia cyklu życia aktu z rozłożonej klauzuli: jedno na każdą różną datę wejścia w życie i jedno na każdą datę mocy wstecznej; wyjątek niesie swoje jednostki, reguła ogólna żadnej — [ADR-0022](adr/0022-daty-aktu-jako-zdarzenia-cyklu-zycia.md). Zdarzenie wyjątku pamięta pozycję klauzuli, która je zapisuje (`PartialEntryIntoForce.SourceUnitEId`) — to jego podstawa i dowód; zapisane w kilku pozycjach wskazuje całą klauzulę. Wyznacza też grupę czasową jednostki z pary jej dat (`GroupFor`). Eksportuje je `TemporalDataXmlConverter` do `metadane/DaneCzasowe`. |
| **`GoverningDate`** | Która z dwóch dat trafia do atrybutu `wejscie-w-zycie`: moc wsteczna przysłania datę wejścia w życie. JEDYNE miejsce liczące tę wartość — patrz [ADR-0013](adr/0013-moc-wsteczna-przyslania-date-dopiero-na-wyjsciu.md). |

### 6.4 Helpery (`Helpers/`)

| Klasa | Rola |
|---|---|
| **`ParagraphExtensions`** | Metody rozszerzające `Paragraph` (OpenXml) — bezpieczne pobieranie `StyleId` z null-checkiem na `ParagraphProperties`; `GetFullText` składa tekst akapitu z przebiegów i równań w kolejności dokumentu. |
| **`OfficeMathMl`** | Równanie Worda (`m:oMath`) jako MathML prezentacyjny (`math` z `msub`, `msup`, `mfrac`, `mrow` z nawiasami, `munderover`, `msqrt`/`mroot`, `mtable`); tekst przebiegu dzieli na `mi`, `mn`, `mo`, przebieg tekstu zwykłego to `mtext`. Wszystkie 1206 równań korpusu dają wyrażenie zgodne ze schematem (miara `CorpusValidator --equations`). `FormulaOf` rozstrzyga, czy akapit jest wzorem (same równania, co najwyżej z interpunkcją) — czytnik DOCX zapisuje wtedy MathML w `DocumentBlock.FormulaMathMl` ([ADR-0038](adr/0038-wzor-w-osobnym-akapicie-to-wzor-mat-z-mathml.md)). |
| **`UnitDesignation`** | Czy akapit zaczyna się czymś, co wygląda na oznaczenie jednostki — także spoza wzorców klasyfikatora („l)", „A)", „3-5.", „2.1 …", myślnik w każdej postaci). Rozstrzyga o akapicie w stylu jednostki, którego oznaczenia wzorce nie odczytały: z takim początkiem zostaje jednostką bez odczytanego oznaczenia (ADR-0024), bez niego jest dalszą treścią jednostki, za którą stoi ([ADR-0039](adr/0039-akapit-w-stylu-jednostki-bez-oznaczenia-jest-dalsza-trescia.md)) — w `ParserOrchestrator.ProcessBlock` (za granicami nowelizacji) i w `AmendmentBuilder`. |
| **`FormulaLegend`** | Legenda wzoru: wiersze tuż za wzorem — wprowadzenie („gdzie poszczególne symbole oznaczają:", „w którym …:", „przy czym:") i opisy symboli („I_e – ilość energii …") — trafiają do `ContentFormula.LegendIntroduction` i `Legend`, nie do dalszej treści jednostki. Dwa sygnały: miejsce (ostatnim elementem dalszej treści jest wzór) i kształt wiersza; symbol będący równaniem niesie MathML (`DocumentBlock.LeadingSymbol`). Wiersz, który legendą nie jest, zamyka ją. Wołany z `StructureProcessor.HandleUnknown` i `AmendmentBuilder.AttachText`. |
| **`OfficeMathText`** | Równanie Worda (`m:oMath`) jako tekst w zapisie liniowym: `E_i`, `x^2`, `a/b`, nawiasy z właściwości równania, operator z granicami (`∑_(i=u)^j`), pierwiastek (`√x`, `√(8&x)`) — [ADR-0037](adr/0037-rownanie-worda-wchodzi-do-tekstu-w-zapisie-liniowym.md). |
| **`StringExtensions`** | Metody rozszerzające `string` — `CollapseWhitespace()` dla treści jednostek (odstępy zwinięte, półpauza zostaje) i `Sanitize()` dla rozpoznawania rodzaju aktu i nagłówka publikatora (dodatkowo półpauza → dywiz). |
| **`PolishDates`** | Nazwy miesięcy i parsowanie dat w formie długiej („1 stycznia 2025 r."). Metody `Try*` nie rzucają — data niemożliwa („31 lutego") zwraca `false`. Alternatywa miesięcy jest `const`, bo leksykon jest zamknięty. |
| **`PolishNumerals`** | JEDYNE źródło prawdy o polskich liczebnikach: porządkowe (rdzeń + końcówki, wszystkie rodzaje), główne w dopełniaczu (formy dokładne), mnożne („podwójne"), składanie 1–99 z dwóch wyrazów. Wystawia też GENEROWANE ze słownika fragmenty regexa do wyszukiwania — patrz [ADR-0009](adr/0009-jedno-zrodlo-liczebnikow-polskich.md). |
| **`RomanNumeralConverter`** | Wyłącznie cyfry rzymskie (`RomanToInt`/`ToRoman`) dla jednostek systematyzacyjnych; oznaczenie rzymskie i słowne trafia do eId jako liczba arabska („KSIĘGA PIERWSZA" i „KSIĘGA I" → `ksi_1`, [ADR-0018](adr/0018-postac-oznaczenia-w-eid.md)). Liczebniki słowne rozstrzyga `PolishNumerals`. |
| **`StyleLibraryMapper`** | Statyczna mapa styli dokumentów prawnych — mapuje StyleId na czytelne nazwy. Zawiera `AmendmentStyleInfoMap` z metadanymi styli nowelizacyjnych. |
| **`AmendmentStyleDecoder`** | Dekoder styli nowelizacyjnych — rozpoznaje instrument zmiany (`AmendmentInstrument`), typ celu (`AmendmentTargetKind`), kontekst nadrzędny. |

### 6.5 Konwertery XML (`Services/Converters/`)

| Konwerter | Rola |
|---|---|
| `SystematizingUnitXmlConverter` | Jednostki systematyzacyjne zagnieżdżone wg schemaPL; niejawny łańcuch bez jawnego przodka znika (artykuły wprost w akcie), pod jawną jednostką niejawne poziomy wychodzą jako `-n` z `brak-eid` — [ADR-0020](adr/0020-eksport-jednostek-systematyzacyjnych.md) |
| `EditorialUnits/*XmlConverter` | Artykuł, ustęp, punkt, litera, tiret; `nr` = oznaczenie z indeksem górnym. Artykuł z paragrafami („§") stojący w oddziale to `artykul-kodeksu`, poza oddziałem `artykul`. Jednostka odbiegająca od ZTP (jedna pozycja wyliczenia, poziom pominięty) wychodzi jako `nie-ztp-*`, a punkt i litera niejawne nie mają elementu ([ADR-0023](adr/0023-poziom-pominiety-jako-jednostka-niejawna.md)) |
| `AmendmentXmlConverter` | Komendy nowelizacji i treść zmiany (`cytat-strukt`) |
| `CommonPartXmlConverter` | Części wspólne wyliczeń (eId gospodarza z segmentem `czw` — [ADR-0025](adr/0025-czesc-wspolna-ma-eid-gospodarza-z-czw.md)) |
| `EmbeddedContentXmlConverter` | Dalsza treść jednostki tuż za jej `p`: akapit bez oznaczenia jako `fragment`, tabela jako `tabela` ([ADR-0029](adr/0029-akapit-bez-oznaczenia-jest-dalsza-trescia-jednostki.md)), wzór w osobnym akapicie jako `wzor-mat` → `wzor` → `mml:math` z legendą (`wprowadzenie-do-opisu`, `opis` → `symbol-mat` albo `symbol-txt` + `objasnienie` podzielone na zdania) ([ADR-0038](adr/0038-wzor-w-osobnym-akapicie-to-wzor-mat-z-mathml.md)) |
| `TemporalDataXmlConverter` | `DaneCzasowe` w metadanych: zdarzenia wejścia w życie i mocy wstecznej z identyfikatorem, pochodzeniem, formułą, klauzulą i jednostkami wyjątku (zakres wyjątku — adresem jednostki cytowanej albo celu komendy w akcie zmienianym); po zbudowaniu treści nadaje elementom z datą atrybut `okres` i dopisuje grupy czasowe ([ADR-0022](adr/0022-daty-aktu-jako-zdarzenia-cyklu-zycia.md)) |
| `InlineTextXml` | Elementy wewnątrz tekstu powstają w eksporcie, przebiegiem po zbudowanej treści: wskazuje węzły tekstowe, które je przyjmują (`zdanie`, `tytul`, `tekst`, `autor-aktu`, `podstawa-prawna`, tytuł jednostki systematyzacyjnej w `naglowek-*` → `p` oraz tekst załącznika i komórek tabel), i zamienia indeks górny z kanału tekstowego („477[9]") na `sup` |
| `FootnoteXmlConverter` | Przebieg po zbudowaniu treści: znacznik odnośnika w węźle wskazanym przez `InlineTextXml` (zdanie, tytuł, organ wydający, podstawa prawna, tytuł jednostki systematyzacyjnej, …) → `odnosnik` z treścią, kolejne odwołanie → `odsylacz-do-odnosnika` ([ADR-0026](adr/0026-odnosnik-tresc-w-rejestrze-miejsce-w-tekscie.md)); który odnośnik stoi za numerem, rozstrzyga `FootnoteResolver` ([ADR-0061](adr/0061-odnosnik-ma-oznaczenie-jak-w-akcie-a-znacznik-numer-jednoznaczny.md)); sam znacznik bez odnośnika tego aktu → `sup` ([ADR-0047](adr/0047-znacznik-bez-odnosnika-tego-aktu-wychodzi-indeksem-gornym.md)); treść odnośnika — także cytowanego — składa `FootnoteContentXml`: pierwszy akapit jako `p`, wyliczenie i dalsze akapity jako jednostki odnośnika (`FootnoteContent`, [ADR-0078](adr/0078-wyliczenie-w-odnosniku-to-jego-jednostki.md)) |
| `ActPrefixXml` | Ostatni przebieg po gotowym dokumencie: poprzedza adresy prefiksem aktu — własnym dla jednostek aktu, `JednostkaAktu` i `dowod`, aktu zmienianego dla jednostek pod `cytat-strukt`, celów komend i `JednostkaWZakresie`, aktu przytaczanego dla jednostek pod `tresc-strukt` (adnotacja `ActPrefixXml.QuotedAct`) — [ADR-0027](adr/0027-prefiks-aktu-dopisuje-eksport.md), [ADR-0036](adr/0036-przytoczone-artykuly-sa-jednostkami-aktu-przytaczanego.md) |
| `AnnexXmlConverter` | Załącznik: `zalacznik` z `eId` albo `brak-eid`, `zalacznik-do`, `zalacznik-nr`, `tytul-zalacznika` i `segment-zalacznika` z treścią w kolejności dokumentu — nowy segment przy zmianie orientacji strony, z `orientacja`, gdy źródło ją podaje ([ADR-0077](adr/0077-segment-zalacznika-to-ciag-stron-jednej-orientacji.md)); załączniki stoją za podpisem aktu |
| `TableXmlConverter` | Tabela treści: `tabela`/`tbody`/`tr`/`td` ze scaleniami i wierszem nagłówka; akapit komórki jako `tytul-kolumny`, `liczba`, `wyliczenie-N` (poziom liczony w komórce) albo `tekst-zwykly`; pusta komórka niesie pusty akapit |
| `ActKindXml` | Rodzaj aktu w metadanych (`Rodzaj`): jawny `LegalDocument.Type` albo — przy wartości domyślnej — nagłówek aktu, a gdy nie da się go rozpoznać, rodzaj z klasyfikatora dokumentu; organ wydający wynikający z rodzaju (ustawa — Sejm) i zasoby ELI znanych organów Element aktu ogłaszanego w obwieszczeniu (`AnnouncedActElement`) wynika z rodzaju nazwanego jego nagłówkiem, bez względu na budowę jednostek; nagłówek, który rodzaju nie nazywa, daje `inny-akt-prawny` — obwieszczenie zawsze zostaje korzeniem. |
| `SignatureXmlConverter` | Podpis aktu: `organ-podpisujacy` z `osoba` (`imie`, `nazwisko`) za ostatnią jednostką i organ z formuły podpisu w `Organy` — o roli `podpisujacy` albo `wydajacy` (rozstrzyga `AuthoritiesXmlConverter`) |
| `AuthoritiesXmlConverter` | `Organy` w metadanych ([ADR-0041](adr/0041-organ-ktory-sam-podpisuje-jest-jednym-organem-wydajacym.md)): ustawa — Sejm i podpisujący; pozostałe akty — organ ze strefy tytułowej (`LegalDocument.IssuerLine`). Gdy nazywa go formuła podpisu, jest jednym organem wydającym z osobą podpisującą (nazwa z podpisu); podpisujący spoza strefy tytułowej (przewodniczący organu kolegialnego, kontrasygnata) stoi osobno, a organ wydający dostaje nazwę ze strefy tytułowej w mianowniku. `UNKNOWN` tylko bez organu w strefie tytułowej |
| `UnitAddressXml` | Jedyne miejsce piszące adres jednostki redakcyjnej: `eId` albo `brak-eid` z powodem, oraz `nr` (pusty, gdy oznaczenia nie odczytano) — [ADR-0024](adr/0024-jednostka-bez-oznaczenia-nie-ma-eid.md) |
| `UnitLifecycleXml` | Jedyne miejsce piszące `wejscie-w-zycie`; zostawia przy elemencie obie daty jednostki (`UnitDates`), z których powstaje `okres` |
| `SchemaPlValidator` | Walidacja eksportu wobec lokalnego `schema/schemaPL.xsd` (narzędzia i testy `[SchemaPlFact]`): cały dokument albo treść aktu bez nagłówka, element po elemencie wobec deklaracji w typie aktu. Koryguje jedno odstępstwo walidatora .NET: komunikat o polu ograniczenia unikalności przy elemencie bez deklaracji (jednostki pod `segment-zalacznika`) pomija i sam sprawdza unikalność atrybutu |

---

## 7. Klasyfikacja warstwowa akapitów — system pewności i kar

Parser stosuje wielowarstwowe rozpoznawanie typu akapitu z oceną pewności (`Confidence` 1–100) i systemem kar (`Penalties`).

### Warstwy klasyfikacji

#### Warstwa 1 — Treść tekstowa (regex)

Skompilowane wzorce regex w `ParagraphClassifier`:

| Wzorzec | Typ | Przykład dopasowania |
|---|---|---|
| `ArticlePattern` | Artykuł | `Art. 5`, `Art.10a` |
| `ParagraphPattern` | Ustęp | `1. Tekst`, `2a. Tekst` |
| `PointPattern` | Punkt | `1) tekst`, `3a) tekst` |
| `LetterPattern` | Litera | `a) tekst`, `ab) tekst` |
| `TiretPattern` | Tiret | `– tekst` (en-dash + spacja) |

Wzorce obsługują opcjonalny prefiks cytatu (`„`, `"`, `"`) dla treści nowelizacji. Jednostki
systematyzacyjne (§ 60-62 ZTP: Część/Księga/Tytuł/Dział/Rozdział/Oddział) mają własne wzorce
(`PartUnitPattern`…`SubchapterUnitPattern`), sprawdzane przed powyższymi — patrz 6.1b/6.1c.
WrapUp **nie** ma osobnego wzorca regex — rozpoznawany jest po stylu (`CZ_WSP_*`), a tekst
(początek półpauzą/dywizem + spacja) jedynie potwierdza lub obniża pewność (`IsWrapUpByText`).

#### Warstwa 2 — Style OpenXml

Mapowanie prefiksów StyleId:

| Prefiks | Typ |
|---|---|
| `ART*` | Artykuł |
| `UST*` | Ustęp |
| `PKT*` | Punkt |
| `LIT*` | Litera |
| `TIR*` | Tiret |
| `CZ_WSP_*` | WrapUp (część wspólna) |
| `Z/*`, `ZZ*`, `Z_*` | Treść nowelizacji |
| `ROZDZODDZOZN*`, `TYTDZOZN*`, `CZKSIGA*` | Oznaczenie jednostki systematyzacyjnej (rodzina dwóch jednostek; którą — rozstrzyga treść) |
| `ROZDZODDZPRZEDM*`, `TYTDZPRZEDM*` | Tytuł jednostki systematyzacyjnej (`UnitHeading`) |

#### Warstwa 3 — Ciągłość numeracji

`NumberingHint` weryfikuje spójność numeracji (przeprowadzane jako kara), **wyłącznie gdy
rozpoznany `Kind` jest równy `hint.ExpectedKind`** (inaczej hint po prostu nie ma zastosowania —
np. hint dla poziomu "punkt", a bieżący akapit rozpoznano jako "artykuł"):
- Oczekiwany numer (`ExpectedNumber`): jeśli sparsowany numer nie jest oczekiwanym następnikiem
  (`IsContinuous(actual)` zwraca `false`), kara `NumberingBreakPenalty`
- Metody: `IsContinuous(actual)`, `GetNextLetterValue()` (inkrementacja liter w stylu arkusza)

#### Warstwa 4 — Rozstrzyganie konfliktów

Gdy styl i treść się nie zgadzają:
- Używa `IConflictResolver` (domyślnie `DefaultConflictResolver`)
- **Reguła**: syntaktyka (regex) wygrywa nad stylem
- Konflikt generuje `ClassificationPenalty` z `StyleSyntaxConflictPenalty` i `ValidationMessage` na encji

### System kar (Confidence Penalties)

Każda kara obniża `Confidence`:

| Kara | Wartość domyślna | Warunki |
|---|---|---|
| `StyleAbsentPenalty` | 10 | Brak StyleId/stylu rozpoznanego — użyto fallback regex |
| `SyntaxAbsentPenalty` | 15 | Brak dopasowania regex — typ ustalony wyłącznie ze stylu |
| `StyleSyntaxConflictPenalty` | 25 | Konflikt: styl mówi X, treść mówi Y |
| `NumberingBreakPenalty` | 10 | Numeracja nieciągła (zła sekwencja), tylko gdy `Kind == ExpectedKind` |

**Wynik**: `Confidence = Math.Clamp(100 - sum(penalties), 1, 100)`. Gdy brak obu sygnałów (styl i
regex), `Kind = Unknown` i `Confidence = 1` wprost (nie przez odjęcie kar).

### Diagnozy klasyfikacji

- `ValidationReporter.AddClassificationWarning()` — rejestruje ostrzeżenia o konfliktach na encji DTO
- `ValidationMessage.Level = Warning` — dla niskich Confidence lub konfliktów styl/treść

---

## 8. System nowelizacji (amendments)

### 8.1 Cykl życia nowelizacji

```
1. Orkiestrator przetwarza akapit ustawy matki (np. ustęp)
2. Detekcja triggera: treść zawiera "otrzymuje brzmienie:" / "dodaje się"
   → AmendmentTriggerDetected = true, AmendmentOwner = bieżąca encja
3. Następne akapity ze stylem Z/* lub bezstylowe
   → InsideAmendment = true
   → AmendmentCollector.Begin(owner, targets)
   → AmendmentCollector.AddParagraph(text, styleId)
4. Powrót do stylu ustawy matki (ART/UST/PKT/LIT/TIR)
   → InsideAmendment = false
   → AmendmentStateManager.Flush():
       0) TrimQuoteBoundaries zdejmuje graniczne „ ” cytatu (§ 94 ZTP) z pierwszego i ostatniego
          akapitu; ich obecność i interpunkcję po cytacie niesie Amendment, a konwerter oddaje je
          w atrybucie `cudzyslowy`
       a) AmendmentBuilder buduje AmendmentContent (hierarchiczna treść; oznaczenie „Rozdział 9a"
          otwiera jednostkę systematyzacyjną w `SystematizingUnits`, następny akapit to jej tytuł,
          a artykuły trafiają do niej przez niejawne poziomy pośrednie)
       b) AmendmentFinalizer tworzy Amendment, wykrywa typ operacji,
          łączy z JournalInfo, przypisuje do właściciela (IHasAmendments) i kotwiczy jednostki cytatu
          w akcie zmienianym: punkt dodawany „w art. 68 ust. 1" ma eId `art_68__ust_1__pkt_5`
          ([ADR-0021](adr/0021-eid-jednostki-cytowanej-od-adresu-celu.md))
       c) AmendmentCollector.Reset()
```

### 8.2 Typy operacji nowelizacyjnych

| `AmendmentOperationType` | Trigger w tekście |
|---|---|
| `Modification` | "otrzymuje brzmienie", "zastępuje się" |
| `Insertion` | "dodaje się" |
| `Repeal` | "uchyla się" |

### 8.3 Dekodowanie styli nowelizacyjnych

`AmendmentStyleDecoder` + `StyleLibraryMapper.AmendmentStyleInfoMap` rozpoznaje:

- **Instrument** (`AmendmentInstrument`): czym jest dokonywana zmiana
  - `Z/` → artykułem/punktem
  - `Z_LIT/` → literą
  - `Z_TIR/` → tiretem
  - `Z_2TIR/` → podwójnym tiretem
  - `ZZ/` → zmiana zmiany (zagnieżdżona)
- **Cel** (`AmendmentTargetKind`): co jest zmieniane (ART, UST, PKT, LIT, TIR, CommonPart, Fragment...)
- **Kontekst** (`ParentContext`): np. `TIR_w_LIT` → tiret wewnątrz litery

`AmendmentBuilder.ClassifyAmendmentParagraph` stawia cel ze stylu przed regexem, z jednym wyjątkiem:
gdy styl i treść wskazują **różne jednostki redakcyjne**, wygrywa treść (ta sama zasada co
`DefaultConflictResolver`), a decyzja trafia jako `ValidationMessage` na zbudowaną encję. Cele
nieredakcyjne ze stylu (`Fragment`, `CommonPart`, …) wygrywają zawsze — regex ich nie rozpozna.

### 8.4 Nowelizacje bez stylów `Z/*` (dokumenty bezstylowe / PDF / TXT)

Gdy dokument nie niesie stylów Word (np. wejście PDF/TXT), granica nowelizacji jest wyznaczana
inaczej niż powrotem do stylu `ART/UST/PKT/LIT/TIR`:

- **`QuoteBalanceTracker`** — bilansuje cudzysłowy cytowanej treści (§ 94 ZTP: nowe brzmienie
  ujęte w „…"); uzbraja się, gdy pierwszy zbierany blok jest bezstylowy i zaczyna się cudzysłowem
  otwierającym „; koniec cytatu (`ClosureReached`) kończy zbieranie nowelizacji zamiast zmiany stylu.
  Trigger nowelizacyjny wykryty wewnątrz otwartego cytatu jest traktowany jako nowelizacja
  zagnieżdżona (`ZZ`) — cytat trwa (`MarkNestedTrigger`). Cytat zamknięty przecinkiem („…",) rozstrzyga
  początek następnego bloku (`ClosesBefore`): kolejny cudzysłów otwierający ciągnie komendę mnogą, cokolwiek
  innego jest już jednostką aktu. Cudzysłów otwierający liczy się także w indeksie górnym („[„2)] …").
- **`AmendmentStateManager.UpdateState`** — blok w cudzysłowie otwierającym po komendzie bez dwukropka
  („ust. 1 otrzymuje brzmienie", `AmendmentFinalizer.ModificationWithoutColonPattern`) jest treścią cytowaną:
  przepis aktu nie zaczyna się od cudzysłowu, więc „„1. …" nie staje się ustępem artykułu zmieniającego.
  Sam cudzysłów nie wystarcza — obwieszczenie tekstu jednolitego przytacza artykuły („…, które stanowią:").
- **`AmendmentBuilder.TryBuildStylelessCommonPart`** — myślnik po punkcie albo literze bez znaku końcowego
  jest częścią wspólną także w treści cytowanej; regułę dzieli z treścią aktu
  (`CommonPartPlacement.LeavesEnumerationOpen`).
- **`AmendmentCommandParser`** — rozpoznaje rodzaj komendy nowelizacyjnej (`AmendmentCommandKind`:
  `Change`/`Add`/`Repeal`/`ReplaceWords`) z samej treści triggera, gdy mapa stylów nie rozstrzyga.
  Źródło niższego priorytetu niż styl — styl zawsze dominuje, gdy jest dostępny.

---

## 9. Walidacja i diagnostyka

### 9.1 `ValidationMessage`

Każda encja (`BaseEntity`) przechowuje listę komunikatów diagnostycznych:

| Poziom | Znaczenie |
|---|---|
| `Info` | Informacja diagnostyczna |
| `Warning` | Potencjalny problem, struktura zrozumiała |
| `Error` | Problem z parsowaniem, jednostka częściowo użyteczna |
| `Critical` | Struktura zniekształcona, jednostka nieprzydatna |

### 9.2 `NumberingHint` — logika ciągłości numeracji

Walidacja numeracji odbywa się poprzez `NumberingHint` w warstwie klasyfikacji (z karą `NumberingBreakPenalty`):

- Oczekiwany typ (`ExpectedKind`): kara stosowana wyłącznie gdy rozpoznany `Kind == ExpectedKind`
  (hint dla innego poziomu hierarchii nie ma zastosowania)
- Oczekiwany numer (`ExpectedNumber`): sprawdzany przez `IsContinuous(actual)`, sprawdza:
  - Ten sam `NumericPart` dozwolony (warianty: 2 → 2a)
  - Kolejny `NumericPart` (prev + 1) dozwolony
  - Litery: ciągłość `LexicalPart` (a → b → c), inkrementacja przez `GetNextLetterValue()`
- Reset hierarchiczny: nowy artykuł → zerowanie oczekiwanego numeru dla podrzędnych poziomów

Numerację jednostek systematyzacyjnych sprawdza osobno `SystematizingUnitValidator` po zbudowaniu
drzewa (rodzeństwo pod jednym rodzicem: ten sam numer z innym sufiksem albo następny).

### 9.3 `ValidationReporter`

Rejestruje ostrzeżenia klasyfikacyjne:
- Brak stylu — użyto reguły tekstowej
- Konflikt styl↔treść — użyto treści (także w treści nowelizacji, przez `AmendmentBuilder`)

---

## 10. Konwencje projektowe

### 10.1 Język

- **Kod** (zmienne, metody, klasy): angielski
- **Komentarze, XML-doc, UI, diagnostyka**: polski
- **Commity**: angielski (format: `feat: ...`, `fix: ...`, `refactor: ...`)

### 10.2 Encje implicit

Encja implicit (niejawna) to jednostka, która fizycznie nie występuje w tekście, ale jest wymagana przez hierarchię.
Flagę `IsImplicit` niesie `BaseEntity`, więc każdy poziom pyta o nią tak samo:
- Artykuł zawsze ma ≥1 ustęp → jedyny ustęp jest `IsImplicit = true` (i jako jedyna jednostka niejawna ma treść)
- Litera wymaga punktu → litery wprost w ustępie stoją w punkcie `IsImplicit = true`
- Tiret wymaga litery → tirety wprost w ustępie albo punkcie stoją w literze `IsImplicit = true`
- Jednostki systematyzacyjne nieobecne w tekście (pkt 4.1)

Encje implicit:
- Są **pomijane** w generowaniu eId (`art_5__ust_1__lit_a` dla litery wprost w ustępie)
- Są **pomijane** w walidacji numeracji
- Są **pomijane** w warstwie prezentacji
- W eksporcie: jedyny ustęp to `ustep-n`; punkt i litera za pominięty poziom nie mają elementu, a ich
  dzieci stają w najbliższej jednostce jawnej jako `nie-ztp-*` —
  [ADR-0023](adr/0023-poziom-pominiety-jako-jednostka-niejawna.md); jednostki systematyzacyjne wg
  [ADR-0020](adr/0020-eksport-jednostek-systematyzacyjnych.md)
- Powołanie pomijające poziom („art. 5 ust. 1 lit. a") schodzi przez jednostkę niejawną
  (`EntryIntoForceTargetResolver`)

### 10.3 Części wspólne (CommonPart)

`CommonPart` to wirtualna jednostka redakcyjna reprezentująca tekst przed (`Intro`) lub po (`WrapUp`) liście elementów wyliczeniowych:
- Tworzone automatycznie przez `ParsingFactories.AttachIntroCommonPart()` przed dodaniem pierwszego elementu do listy
- Są rodzeństwem elementów wyliczeniowych (nie ich dziećmi)
- W eksporcie: wprowadzenie zostaje zdaniem gospodarza (pozycje wyliczenia są jego dalszymi fragmentami), a część
  wspólna po wyliczeniu to element `czesc-wspolna-punkt` / `-litera` / `-tiret` z eId gospodarza i segmentem `czw`
  — [ADR-0025](adr/0025-czesc-wspolna-ma-eid-gospodarza-z-czw.md)
- Część wspólna, po której stoi kolejna pozycja tego samego wyliczenia, jest rozstrzygana przy tej pozycji
  (`CommonPartPlacement`, w treści aktu i nowelizacji): jeśli tuż przed nią stało niższe wyliczenie bez zamknięcia,
  zamyka je (styl wskazał o poziom za wysoko); inaczej przerywa wyliczenie (`CommonPart.InterruptsAfter`) i wychodzi
  w tym miejscu jako `nie-ztp-czesc-wspolna-*` z `brak-eid="nie-ztp-wielokrotna-czesc-wspolna"`
- Akapit w stylu części wspólnej (`CZ_WSP_*`) tuż za wyliczeniem poziomu, który styl wskazuje, zamyka je także bez
  myślnika na początku — styl i miejsce to dwa zgodne sygnały (`StructureProcessor.TryHandleWrapUp`); kolejny taki
  akapit za wyliczeniem już zamkniętym jest dalszą treścią części wspólnej (`fragment` w `czesc-wspolna-*`),
  a akapit, który niczego nie zamyka, przechodzi drogą akapitu bez oznaczenia — nic nie ginie
- Bez stylu i bez myślnika część wspólną rozpoznaje treść i miejsce: akapit małą literą tuż za pozycją wyliczenia
  zakończoną przecinkiem albo bez znaku końcowego (`StructureProcessor.TryCloseEnumerationWithoutDash`) —
  [ADR-0046](adr/0046-akapit-mala-litera-za-pozycja-wyliczenia-jest-czescia-wspolna.md)
- Akapit ustawy stojący za przedmiotem aktu, przed pierwszą jednostką (także systematyzacyjną), jest preambułą
  (`StructureProcessor.TryAddToPreamble` → `LegalDocument.Preamble`); akt wykonawczy ma w tym miejscu podstawę
  prawną, a akapit z numerem albo adnotacja w nawiasie preambułą nie są
- Sankcja karna w osobnym akapicie (styl `S_KARN`, „podlega …") domyka zdanie przepisu, a po wyliczeniu jest jego częścią
  wspólną (`StructureProcessor.TryHandleSanction`; w cytacie `AmendmentBuilder`) — rozpoznają ją dwa zgodne sygnały z trzech:
  styl, początek „podlega" i niedokończone zdanie przed nią
- Myślnik pod ustępem bez otwartego punktu i litery otwiera tiret tylko po wprowadzeniu zakończonym dwukropkiem
  (§ 56 ZTP) albo w stylu tiretu; w innym razie domyka urwane zdanie przepisu albo jest jego dalszą treścią — za wzorem
  jako wprowadzenie do opisu symboli (`StructureProcessor.TryContinueAfterDash`) —
  [ADR-0042](adr/0042-myslnik-bez-wprowadzenia-z-dwukropkiem-nie-otwiera-tiretu.md)
- Część wspólna jako jedyna treść nowelizacji („dodaje się część wspólną w brzmieniu:") to cytat strukturalny
  z `czesc-wspolna-*` pod adresem celu (`art_18zg__ust_2__czw`), funkcja `dodaj-w`

### 10.4 Null safety w OpenXml

Zawsze sprawdzaj null przed dostępem do styli:
```csharp
paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.ToString()
```
Centralnie obsługiwane przez `ParagraphExtensions.StyleId()`.

### 10.5 Wzorce regex

- Wszystkie wzorce są `RegexOptions.Compiled` i przechowywane jako `static readonly`
- Współdzielone między `ParagraphClassifier` i `ParsingFactories`
- Obsługują opcjonalny prefiks cytatu (`OptionalQuotePrefix`)

### 10.6 Logowanie

- Serilog konfigurowany przez `LoggerConfig.ConfigureLogger()`
- Poziom minimalny: `Warning`
- Wyjście: konsola + plik `logs/log.txt` (rolling daily)
- Kluczowe decyzje parsera (niepewne klasyfikacje, naprawy) logowane przez Serilog

### 10.7 Testy

- Framework: xUnit (`Saga.Core.Tests`)
- Konwencja nazewnictwa plików: `*Tests.cs` (np. `EIdTests.cs`, `ParagraphClassifierTests.cs`)
- Każda zmiana w logice parsowania wymaga aktualizacji lub dodania testów
- Dokumenty referencyjne (DOCX) w lokalnym `DocRepo/`, golden snapshoty w `Saga.Core.Tests/Artifacts/` — oba niewersjonowane; testy na nich oznaczone `[DocRepoFact]` pomijają się, gdy danych brak

### 10.8 Buildery z wzorcem Input/Result

Buildery encji stosują konwencję:
- `*BuildInput` — sealed record z danymi wejściowymi
- `*BuildResult` — sealed class z wynikami (jeśli >1 wartość zwracana)
- Interfejs `IEntityBuilder<TInput, TResult>`

---

## 11. Mapa plików — szybki przegląd

```
src/Saga.Model/
├── BaseEntity.cs                    # Klasa bazowa wszystkich encji
├── LegalDocument.cs                 # Korzeń modelu aktu prawnego
├── EntityNumber.cs                  # Model numeru encji
├── Amendment.cs                     # Model nowelizacji
├── AmendmentContent.cs              # Hierarchiczna treść nowelizacji
├── AmendmentOperationType.cs        # Enum: Modification/Insertion/Repeal
├── AmendmentObjectType.cs           # Enum: Article/Paragraph/Point/...
├── StructuralAmendmentReference.cs  # Cel nowelizacji (ścieżka strukturalna)
├── TextSegment.cs                   # Segment tekstu (zdanie)
├── TextSegmentType.cs               # Enum: Sentence/...
├── ValidationMessage.cs             # Komunikat diagnostyczny
├── JournalInfo.cs                   # Metadane publikatora (Dz.U.)
├── ActSignature.cs                  # Formuła podpisu aktu (organ, osoba)
├── Annex.cs                         # Załącznik aktu: wskazanie aktu, oznaczenie, tytuł, treść
├── ContentTable.cs                  # Tabela w treści (wiersze, komórki, scalenia)
├── ContentBlock.cs                  # Element treści bez oznaczenia: akapit (także wzór), tabela albo przytoczenie
├── ContentFormula.cs                # Wzór w osobnym akapicie: wyrażenie w MathML, legenda (FormulaSymbol)
├── Quotation.cs                     # Przytoczenie przepisów innego aktu jako jednostki (obwieszczenie TJ)
├── Footnote.cs                      # Odnośnik (przypis): numer i treść
├── PublisherType.cs                 # Enum typu publikatora
├── LegalActType.cs                  # Enum rodzaju aktu (11 wartości, w tym AmendingStatute/Announcement/Resolution/ExecutiveOrder/LocalLegalAct)
├── DocumentClassificationResult.cs  # Wynik klasyfikacji dokumentu (rodzaj aktu, pewność, sygnały)
├── DocumentSignal.cs                # Pojedynczy dowód klasyfikacji dokumentu
├── DocumentSignalKind.cs            # Enum rodzajów sygnałów klasyfikacji dokumentu
├── CommonPartType.cs                # Enum: Intro/WrapUp
├── IHasAmendments.cs                # Interfejs: encja z nowelizacją
├── IHasCommonParts.cs               # Interfejs: encja z częściami wspólnymi
├── IHasTextSegments.cs              # Interfejs: encja z segmentami tekstu
├── EditorialUnits/
│   ├── Article.cs                   # Artykuł (art.)
│   ├── Paragraph.cs                 # Ustęp (ust.) — może być implicit
│   ├── Point.cs                     # Punkt (pkt)
│   ├── Letter.cs                    # Litera (lit.)
│   ├── Tiret.cs                     # Tiret (tir.) — wspiera zagnieżdżanie
│   └── CommonPart.cs                # Część wspólna (intro/wrapUp)
└── SystematizingUnits/
    ├── ISystematizingUnit.cs        # Interfejs jednostek organizacyjnych
    ├── Part.cs                      # Część
    ├── Book.cs                      # Księga
    ├── Title.cs                     # Tytuł
    ├── Division.cs                  # Dział
    ├── Chapter.cs                   # Rozdział
    └── Subchapter.cs                # Oddział (kontener artykułów)

src/Saga.Core/
├── LegalDocumentParser.cs           # PUNKT WEJŚCIA — Parse(Stream/filePath) → ParseResult; Parse(WordprocessingDocument) [Obsolete]
├── ParseResult.cs                   # Koperta wyniku: Classification, Document?, SourceFormat, BlockCount
├── ParseOptions.cs                  # Opcje: Policy (ParsePolicy), ForcedFormat
├── LoggerConfig.cs                  # Konfiguracja Serilog
├── Exceptions/
│   ├── ParsingException.cs          # Wyjątek bazowy parsowania
│   └── UnsupportedDocumentFormatException.cs  # Nierozpoznany format wejścia
├── Helpers/
│   ├── OfficeMathMl.cs              # Równanie Worda (m:oMath) jako MathML prezentacyjny
│   ├── OfficeMathText.cs            # Równanie Worda (m:oMath) jako tekst w zapisie liniowym
│   ├── ParagraphExtensions.cs       # Bezpieczne StyleId() z null-check; GetFullText() z obsługą superscriptu i równań
│   ├── StringExtensions.cs          # CollapseWhitespace(), Sanitize()
│   ├── StyleLibraryMapper.cs        # Mapa styli dokumentów prawnych
│   └── AmendmentStyleDecoder.cs     # Dekoder styli nowelizacyjnych
├── Ingest/                          # WARSTWA ODCZYTU FORMATU (DOCX/PDF/TXT → DocumentBlock)
│   ├── SourceFormat.cs              # Enum: Docx/Pdf/PlainText/Unknown
│   ├── SourceFormatDetector.cs      # Detekcja formatu po sygnaturze (nie po rozszerzeniu)
│   ├── DocumentBlockReaderFactory.cs  # Format → IDocumentBlockReader
│   ├── IDocumentBlockReader.cs      # Kontrakt adaptera formatu
│   ├── DocumentBlock.cs             # Format-agnostyczny odpowiednik akapitu (Text/StyleId/Layout/Source/Role)
│   ├── BlockRole.cs                 # Enum: Body/FootnoteText/JournalHeader/…
│   ├── BlockLayoutInfo.cs           # Metadane układu (wcięcia, pogrubienie, kursywa)
│   ├── BlockSourceLocation.cs       # Położenie bloku w źródle (diagnostyka)
│   ├── BlockAlignment.cs / BlockAssembler.cs  # Składanie bloków z linii (PDF)
│   ├── DocxBlockReader.cs           # Adapter DOCX (OpenXml Paragraph → DocumentBlock)
│   ├── DocxFootnotes.cs             # Przypisy DOCX: oznaczenie jak w akcie (sekcje), numer znacznika i treść z word/footnotes.xml
│   ├── DocxTables.cs                # Tabele DOCX: komórka (tabela, wiersz, kolumna, scalenia) akapitu
│   ├── BlockTableCell.cs            # Miejsce bloku w tabeli źródłowej
│   ├── PlainTextBlockReader.cs      # Adapter TXT
│   ├── TextLine.cs / TextNormalizer.cs  # Pomocnicze dla adaptera TXT
│   └── Pdf/
│       ├── PdfBlockReader.cs        # Adapter PDF (PdfPig) — warstwa tekstowa
│       ├── PdfLineExtractor.cs      # Ekstrakcja linii z geometrii PDF
│       ├── PdfTextLine.cs           # Linia tekstu PDF z pozycją
│       └── PageArtifactFilter.cs    # Filtrowanie artefaktów strony (nagłówki/stopki/numery stron)
└── Services/
    ├── EntityNumberService.cs       # Parsowanie/formatowanie numerów encji
    ├── DesignationAlphabet.cs       # Litery w oznaczeniach jednostek (także polskie)
    ├── FootnoteMarkers.cs           # Znacznik odnośnika w kanale tekstowym
    ├── LegalReferenceService.cs     # Adres celu nowelizacji (przez UnitAddressReader)
    ├── JournalReferenceService.cs   # Parsowanie publikatorów (Dz.U.)
    ├── Classify/                    # WARSTWA KLASYFIKACJI AKAPITÓW (jednoprzebiegowa)
    │   ├── IParagraphClassifier.cs  # Interfejs klasyfikatora
    │   ├── ParagraphClassifier.cs   # Jedyna implementacja klasyfikatora
    │   ├── ClassificationInput.cs   # Record wejściowy klasyfikatora
    │   ├── ClassificationResult.cs  # Record wyniku klasyfikacji
    │   ├── ParagraphKind.cs         # Enum (14 wartości): jednostki redakcyjne + systematyzacyjne + WrapUp/Unknown
    │   ├── ClassificationPenalty.cs # Model kary
    │   ├── ConfidencePenaltyConfig.cs  # Konfiguracja kar: Style/Syntax/StyleSyntaxConflict/NumberingBreak
    │   ├── NumberingHint.cs         # Logika ciągłości numeracji
    │   ├── IConflictResolver.cs     # Interfejs rozwiązywania konfliktów
    │   ├── DefaultConflictResolver.cs  # Domyślna implementacja: syntaktyka wygrywa
    │   └── Document/                # WARSTWA KLASYFIKACJI DOKUMENTU (rodzaj aktu wg ZTP)
    │       ├── IDocumentClassifier.cs   # Interfejs klasyfikatora dokumentu
    │       ├── DocumentClassifier.cs    # Implementacja (strefa tytułowa + statystyka korpusu)
    │       └── ZtpPatterns.cs           # Wzorce regex sygnałów dokumentu
    ├── Parsing/
    │   ├── ParserOrchestrator.cs    # Orkiestrator parsowania (pipeline) z DI konstruktorem; ProcessBlock/ProcessParagraph
    │   ├── StructureProcessor.cs    # Przetwarzanie struktury — budowanie encji z ClassificationResult (internal sealed)
    │   ├── TitleForm.cs               # Budowa tytułu jednostki systematyzacyjnej i załącznika (bez stylu)
    │   ├── AmendmentStateManager.cs # Zarządzanie cyklem życia nowelizacji (UpdateState, Collect, Flush, DetectTrigger)
    │   ├── QuoteBalanceTracker.cs   # Bilans cudzysłowów dla nowelizacji bez stylów (§ 94 ZTP)
    │   ├── AmendmentCommandParser.cs  # Rozpoznanie rodzaju komendy nowelizacyjnej z treści
    │   ├── DocumentMetadataCollector.cs  # Zbiera rodzaj/datę/przedmiot aktu ze strefy tytułowej
    │   ├── FormulaLegend.cs           # Wprowadzenie i opisy symboli tuż za wzorem należą do wzoru
    │   ├── ActSignatureParser.cs      # Formuła podpisu za ostatnim przepisem
    │   ├── AnnexBuilder.cs            # Załączniki z bloków za nagłówkiem załącznika
    │   ├── AnnouncementBody.cs        # Treść obwieszczenia bez numerowanych punktów: przepis bez oznaczenia
    │   ├── AnnouncementBoundary.cs    # Układ obwieszczenia: granica ogłaszanego aktu albo obwieszczenie w całości
    │   ├── AnnouncementUnits.cs       # Punkty i podpunkty obwieszczenia, przytoczenia, podpis
    │   ├── AnnexUnits.cs              # Wyliczenie w załączniku jako jednostki (potok aktu pod artykułem zastępczym)
    │   ├── ContentTables.cs           # Tabela treści z bloków tabeli źródłowej
    │   ├── JournalHeaderParser.cs    # Metadane publikatora z nagłówka strony (akty ogłoszone)
    │   ├── ParsingContext.cs        # Stan parsowania (mutowalny)
    │   ├── ParsingFactories.cs      # Fabryki: numery, prefiksy, zdania
    │   ├── AmendmentCollector.cs    # Bufor akapitów nowelizacji
    │   ├── AmendmentFinalizer.cs    # Finalizator: typ operacji, Assignment
    │   ├── AmendmentTargetPairing.cs  # Przypisanie jednostek treści do celów nowelizacji
    │   ├── CommonPartPlacement.cs     # Część wspólna, po której wyliczenie biegnie dalej
    │   ├── ValidationReporter.cs    # Reporter diagnostyczny (statyczne metody)
    │   └── Builders/
    │       ├── IEntityBuilder.cs    # Kontrakt buildera
    │       ├── ArticleBuilder.cs    # Builder artykułu + pierwszy ustęp
    │       ├── ParagraphBuilder.cs  # Builder ustępu + EnsureForPoint()
    │       ├── PointBuilder.cs      # Builder punktu + EnsureForLetter()
    │       ├── LetterBuilder.cs     # Builder litery + EnsureForTiret()
    │       ├── TiretBuilder.cs      # Builder tiretu (z obsługą ParentTiret dla zagnieżdżenia)
    │       ├── AmendmentBuilder.cs  # Builder treści nowelizacji
    │       └── SystematizingUnitBuilder.cs  # Builder jednostek systematyzacyjnych (§ 60-62 ZTP)
    ├── UnitReferences/              # POWOŁANIA JEDNOSTEK — jedno źródło (ADR-0015)
    │   ├── UnitReferenceTokenizer.cs  # Słownik: słowa kluczowe, kształt oznaczeń, przedziały
    │   ├── DesignationRange.cs      # Rozwijanie przedziałów oznaczeń
    │   ├── UnitPathCursor.cs        # Reguły ścieżki i rozstrzygnięcie „§"
    │   ├── SectionSignRole.cs       # Samodzielny „§": jednostka główna czy paragraf kodeksu (rodzaj aktu)
    │   └── UnitAddressReader.cs     # Adres z tekstu komendy (cele nowelizacji)
    └── Converters/
        └── …                        # Eksport modelu do XML schemaPL (wywołuje LegalDocumentConverter) — sekcja 6.5
```

---

## 12. Podsumowanie przepływu danych

```
  Stream (DOCX / PDF z warstwą tekstową / TXT) + fileNameHint?
       │
       ▼
  LegalDocumentParser.Parse(stream, fileNameHint, options)
       ├── SourceFormatDetector.Detect() — sniffing sygnatur (lub options.ForcedFormat)
       ├── DocumentBlockReaderFactory.Create(format) → IDocumentBlockReader
       ├── reader.ReadBlocks(stream) → IReadOnlyList<DocumentBlock>
       │
       ├── DocumentClassifier.Classify(blocks) → DocumentClassificationResult
       │     (rodzaj aktu wg ZTP: Statute/Regulation/Announcement/Resolution/
       │      ExecutiveOrder/AmendingStatute/LocalLegalAct..., IsLegalAct, Confidence, Signals)
       │
       ├── options.Policy decyduje, czy budować model:
       │     ParseWhenLegalAct (domyślna) → tylko gdy IsLegalAct
       │     AlwaysParse                  → zawsze
       │     ClassifyOnly                 → nigdy (Document = null)
       │
       ├── [jeśli budowa modelu] ParseBlocks(blocks):
       │     │
       │     ├── JournalHeaderParser.Apply() — publikator i data ogłoszenia z bloków JournalHeader
       │     ├── Iteracja po DocumentBlock[]
       │     │     │
       │     │     ▼
       │     │   ParserOrchestrator.ProcessBlock(block, context)
       │     │     ├── block.IsEmpty / block.Role == FootnoteText | JournalHeader → pomiń
       │     │     ├── StringExtensions.CollapseWhitespace() — zwinięcie odstępów
       │     │     ├── BuildNumberingHint() — oczekiwana numeracja na bieżącym poziomie
       │     │     ├── ParagraphClassifier.Classify() → ClassificationResult
       │     │     │   ├── styl (StyleId, gdy DOCX) → styleKind
       │     │     │   ├── regex (syntaktyka, w tym jednostki systematyzacyjne) → syntacticKind
       │     │     │   ├── IConflictResolver — rozwiązanie konfliktu styl↔regex
       │     │     │   ├── NumberingHint — kara przy nieciągłości (gdy Kind == ExpectedKind)
       │     │     │   └── Rezultat: Kind, Confidence, IsAmendmentContent, StyleType, Penalties[]
       │     │     │
       │     │     ├── HandleAmendmentFlow() — zarządzanie nowelizacją
       │     │     │   ├── AmendmentStateManager.UpdateState() / Flush() przy wyjściu
       │     │     │   ├── ShouldExitForNewParentLawTrigger() (+ QuoteBalanceTracker dla nowelizacji bez stylów)
       │     │     │   └── Collect() i STOP, jeśli IsAmendmentContent / InsideAmendment
       │     │     │
       │     │     ├── StructureProcessor.Process() — budowanie struktury (jeśli nie STOP)
       │     │     │   ├── jednostki systematyzacyjne → SystematizingUnitBuilder.Enter()
       │     │     │   ├── *Builder.Build() → encja DTO (Article/Paragraph/Point/Letter/Tiret/WrapUp)
       │     │     │   ├── Unknown → wnioskowanie z tekstu / DocumentMetadataCollector.Observe() / diagnostyka
       │     │     │   ├── Kaskadowe tworzenie implicit encji
       │     │     │   ├── Obsługa CommonPart (intro/wrapUp)
       │     │     │   ├── UpdateStructuralReference() + DetectAmendmentTargets()
       │     │     │   ├── ValidationReporter.AddClassificationWarning() — diagnostyka
       │     │     │   └── JournalReferenceService.ParseJournalReferences() — publikatory (tylko Article)
       │     │     │
       │     │     └── AmendmentStateManager.DetectTrigger() — jeśli encja zbudowana
       │     │
       │     └── ParserOrchestrator.Finalize()
       │           ├── flush ostatniego bufora nowelizacji (jeśli aktywny)
       │           └── context.Metadata.ApplyTo(document) — Title/ActDate ze strefy tytułowej
       │
       ▼
  ParseResult { Classification, Document?, SourceFormat, BlockCount }
       │
       └── Document (LegalDocument, gdy zbudowany)
             ├── Classification — kopia DocumentClassificationResult
             ├── Articles[] → Paragraphs[] → Points[] → Letters[] → Tirets[]
             ├── CommonParts[] (intro/wrapUp)
             ├── ValidationMessages (diagnostyka klasyfikacji)
             ├── Journals (publikatory z Dz.U.)
             └── Amendments (nowelizacje z operacjami)
                   │
                   ▼
             LegalDocumentConverter → XML (schemaPL)
```

---

## 13. Decyzje architektoniczne

Uzasadnienia decyzji — wraz z odrzuconymi alternatywami i konsekwencjami — żyją w osobnych plikach
w [docs/adr/](adr/README.md). Tutaj tylko skrót z odesłaniem; ta sekcja nie jest miejscem na „dlaczego".

| ADR | Decyzja |
|---|---|
| [0001](adr/0001-jeden-ujednolicony-potok-wejscia.md) | Wszystkie formaty schodzą się do `DocumentBlock[]` — jeden potok, nie ścieżka per format. Stąd też: IR nie trafia do `Saga.Model`, wcięcia w twipach, `ProcessParagraph` utrzymywany jako adapter. |
| [0002](adr/0002-adaptery-nie-udaja-styleid.md) | Adaptery PDF/TXT nie zgadują `styleId` — brak stylu zostaje brakiem i obniża pewność karą, zamiast udawać sygnał szablonu. |
| [0003](adr/0003-backbone-warunkiem-uznania-za-akt.md) | Bez silnego sygnału strukturalnego (`hasBackbone`) dokument nie jest aktem, choćby przekroczył próg punktowy. |
| [0004](adr/0004-pdfpig-zamiast-itext.md) | PdfPig (Apache-2.0) do odczytu PDF; iText odrzucony — AGPL rozciągnąłby wymóg udostępnienia źródeł na usługę sieciową. |
| [0005](adr/0005-pdf-tylko-warstwa-tekstowa.md) | PDF wyłącznie z warstwą tekstową; skan odrzucany `ScannedPdfException`, OCR poza zakresem projektu. |
| [0006](adr/0006-nie-akt-nie-jest-bledem.md) | Nie-akt to `ParseResult` z raportem klasyfikacji, nie wyjątek; decyzja „czy parsować" należy do wywołującego (`ParseOptions.Policy`). |
| [0007](adr/0007-klasyfikacja-nie-mutuje-type.md) | Rozpoznany rodzaj aktu ląduje w `Classification`; `LegalDocument.Type` pozostaje pod kontrolą wywołującego. |

### Rozstrzygnięcia bez osobnego ADR

**Akt oparty na paragrafach.** W rozporządzeniu, uchwale i zarządzeniu „§ N." jest jednostką podstawową:
klasyfikator dostaje `ClassificationInput.SectionIsBasicUnit` (z `SectionSignRole.IsBasicUnit`, czyli z rodzaju
aktu), a `ArticleBuilder` buduje `Article` z `IsSection` — tym samym potokiem co artykuł, z adresem `art_5__ust_2`.
W eksporcie to `paragraf` pod korzeniem `rozporzadzenie` / `uchwala` / `zarzadzenie` (`ActKindXml.RootElement`),
ze strefą tytułową `rodzaj-aktu`, `autor-aktu` (`LegalDocument.IssuerLine`), `data`, `tytul`, `podstawa-prawna`
(`LegalDocument.LegalBasis`). W ustawie „§" zostaje paragrafem kodeksu
([ADR-0034](adr/0034-paragraf-jest-jednostka-podstawowa-aktu-wykonawczego.md)). To samo rozstrzygnięcie
obowiązuje w treści cytowanej: `AmendmentStateManager.Flush` przekazuje rodzaj aktu przez
`AmendmentBuildInput.SectionIsBasicUnit`, a `AmendmentBuilder` buduje z cytowanego „§ 5. 1. …" `Article`
z `IsSection` i ustępami — w ustawie zmieniającej kodeks cytowany „§ 2." pozostaje paragrafem kodeksu.

**Strefa tytułowa: dwa sygnały dla pisowni spoza wzorca.** Szczotki bywają pisane małymi literami i składane
wersalikami przez styl („rozporządzenie", „ZARządzenie NR 249", „ROZPORZĄDZENIE MINISTRA zDROWIA"). Wzorce
nagłówka rodzaju (`ZtpPatterns.*HeaderPattern`) zostają ścisłe — pasują do całego wiersza i nie mylą nagłówka
z prozą („Rozporządzenie wchodzi w życie …"). Słowo rodzaju w dowolnej pisowni (`ZtpPatterns.ActKindKeywordPattern`)
czyni wiersz nagłówkiem dopiero z drugim sygnałem: stylem wiersza rodzaju albo miejscem pierwszego wiersza aktu
ogłaszanego w załączniku (`DocumentMetadataCollector`). Rodzaj nazwany przez zebrany nagłówek czyta jedno miejsce —
`ZtpPatterns.KindOfHeading` (korzeń eksportu, kod rodzaju), a podział „rodzaj / organ w tym samym wierszu" —
`ZtpPatterns.KindWithIssuerPattern`. Odrzucone: wzorce bez rozróżniania wielkości liter (każdy akapit „uchwała"
w treści stawałby się nagłówkiem) i stosowanie wersalików ze stylu przy odczycie (`ParagraphExtensions.GetFullText`
czyta tekst tak, jak go zapisano; wersaliki stosuje tylko dla formatowania nadanego wprost).

**Korzeń eksportu.** `LegalDocumentConverter.Convert` pisze `ustawa`, `kodeks` (ustawa o tytule „Kodeks …",
której artykuły stoją w jednostkach systematyzacyjnych) albo korzeń aktu opartego na paragrafach, a dla tekstu
jednolitego —
`obwieszczenie` (z `autor-aktu`), w nim punkty obwieszczenia (`AnnouncementXmlConverter`: `punkt-OTJ`,
`podpunkt-OTJ`, `czesc-wspolna-podpunkt-OTJ`; przytoczone artykuły jako `tresc-strukt` w zdaniu jednostki —
[ADR-0036](adr/0036-przytoczone-artykuly-sa-jednostkami-aktu-przytaczanego.md)), podpis i ogłaszany akt (`ustawa`, `kodeks` albo `rozporzadzenie` /
`uchwala` / `zarzadzenie` — `ActKindXml.AnnouncedActElement`) z własną strefą tytułową (`zalacznik-do`,
`rodzaj-aktu`, `data`, `tytul`), jednostkami i załącznikami ([ADR-0031](adr/0031-tekst-jednolity-pod-korzeniem-obwieszczenie.md),
[ADR-0032](adr/0032-podpunkt-obwieszczenia-ma-skrot-ppkt.md)).
Tekst jednolity nie czyta klauzuli wejścia w życie: `LifecycleEvents.Of` daje mu zdarzenie `ujednolicenie`
w dniu ogłoszenia obwieszczenia, a jednostki drukowane raz nie niosą dat ([ADR-0033](adr/0033-tekst-jednolity-ma-zdarzenie-ujednolicenia.md)).
Jednostka drukowana dwa razy — w brzmieniu obowiązującym i przyszłym (§ 106a ZTP) — dostaje wersję czasową
(`WordingVersions.Assign` → `BaseEntity.Version`): obie kopie mają to samo eId i różny `okres`, a zmiana jest
jednym zdarzeniem, które zamyka jedną grupę czasową i otwiera drugą ([ADR-0060](adr/0060-jednostka-drukowana-dwa-razy-to-dwie-wersje-rozrozniane-okresem.md)).
Termin zmiany stoi w jej odnośniku albo — gdy ten nazywa akt zmieniający odesłaniem — przy pełnej nazwie aktu
w innym odnośniku ([ADR-0062](adr/0062-termin-zmiany-zapisany-przy-akcie-zmieniajacym-obejmuje-jego-zmiany.md)). Zdarzenie należy do aktu, który zmianę wprowadza:
jego adres stoi w identyfikatorze zdarzenia i w `zrodlo`, a `podstawa` wymienia przepisy sprawcze
([ADR-0063](adr/0063-zdarzenie-zmiany-nalezy-do-aktu-ktory-ja-wprowadza.md)).
Tak samo czytane jest zdanie wydrukowane w dwóch brzmieniach — wersję niesie zdanie (`TextSegment.Version`),
a oba brzmienia mają jeden numer ([ADR-0064](adr/0064-zdanie-w-dwoch-brzmieniach-to-dwa-zdania-o-jednym-numerze.md)).
Drugie brzmienie wydrukowane osobnym akapitem (wprowadzenie do wyliczenia, zdanie, część wspólna) wraca do swojego
fragmentu zamiast wychodzić jako `fragment` ([ADR-0065](adr/0065-drugie-brzmienie-w-osobnym-akapicie-nalezy-do-tego-samego-fragmentu.md)).

**Pierwszy nagłówek rodzaju aktu wygrywa.** Załącznik do obwieszczenia o tekście jednolitym zawiera
własną linię „USTAWA", więc sygnały tytułowe liczone są tylko ze strefy tytułowej (pierwsze bloki),
a kolejny nagłówek jest ignorowany z sygnałem informacyjnym `IgnoredSecondaryHeader`. Bez tego każdy
tekst jednolity klasyfikowałby się jako ustawa.

Znane luki i prace świadomie odroczone: [backlog.md](backlog.md).
