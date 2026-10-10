# Changelog

Wszystkie istotne zmiany w tym projekcie będą dokumentowane w tym pliku.

Format oparty jest na Keep a Changelog,
a wersjonowanie zgodne z Semantic Versioning.

## [Unreleased]
### Dodano
- **Jednostki systematyzacyjne w eksporcie XML**: część, księga, tytuł, dział, rozdział i oddział
  wychodzą jako zagnieżdżone elementy schemaPL z `eId` i nagłówkiem (`nr`, `p`). Pod jawną jednostką
  brakujące poziomy są elementami `-n` z `brak-eid="jednostka-nieoznaczona"`. Akty bez jawnych
  jednostek mają XML bez zmian — [ADR-0020](docs/adr/0020-eksport-jednostek-systematyzacyjnych.md).
- **Jednostki systematyzacyjne w treści nowelizacji**: „dodaje się rozdział 9a w brzmieniu:" daje
  w `cytat-strukt` rozdział z nagłówkiem i artykułami w `oddzial-n`, zamiast oznaczenia i tytułu jako
  cytatu tekstowego obok artykułów.
- **Cele nowelizacji wskazane jednostką systematyzacyjną**: „po rozdziale 9 dodaje się…", „w dziale II
  po rozdziale 9…", „uchyla się rozdziały 4 i 5" dają `element-eId` `rozdz_9`, `dzial_2__rozdz_9`,
  `rozdz_4`/`rozdz_5` zamiast `UNKNOWN`. Wejście w życie takich powołań jeszcze nie czyta.
- **Strefa tytułowa aktu w eksporcie**: po `metadane` stoją `rodzaj-aktu`, `data` i `tytul` z wierszy aktu
  („USTAWA", „z dnia 14 czerwca 2024 r.", „o ochronie sygnalistów"); model trzyma je osobno
  (`LegalDocument.KindLine`, `DateLine`, `Subject`).
- **Walidacja numeracji jednostek systematyzacyjnych**: powtórzone oznaczenie, luka, malejąca
  numeracja, oddział poza rozdziałem i jednostka bez tytułu dostają komunikat walidacji.

### Zmieniono
- **eId jednostek redakcyjnych nie zawiera już ścieżki jednostek systematyzacyjnych**: artykuł
  w rozdziale 1 ma `eId="art_5"` zamiast `rozdz_1__art_5` (tak samo ustępy, punkty, litery
  i tirety). Adres jednostki jest teraz tym samym napisem co `element-eId` odsyłającej do niej
  nowelizacji. Zmienia wyjście XML każdego aktu z rozdziałami, działami lub innymi jawnymi
  jednostkami systematyzacyjnymi — [ADR-0017](docs/adr/0017-eid-jednostki-redakcyjnej-bez-sciezki-systematyzacyjnej.md).
- **Oznaczenie w eId**: jednostki numerowane cyframi rzymskimi albo słownie dostają liczbę arabską
  (`KSIĘGA PIERWSZA` → `ksi_1`, `DZIAŁ II` → `dzial_2`) — [ADR-0018](docs/adr/0018-postac-oznaczenia-w-eid.md).
- **eId jednostek cytowanych w nowelizacji zaczyna się od adresu celu** w akcie zmienianym:
  punkt dodawany „w art. 68 ust. 1" ma `eId="art_68__ust_1__pkt_5"` zamiast `pkt_5` —
  [ADR-0021](docs/adr/0021-eid-jednostki-cytowanej-od-adresu-celu.md).
- **Załącznik aktu nie jest już częścią ostatniego artykułu**: treść za nagłówkiem „Załącznik do
  ustawy z dnia…" nie trafia do struktury aktu (wcześniej jej punkty i rozdziały wchodziły do
  ostatniego artykułu albo stawały się jednostkami aktu).
- **Metadane aktu zgodne ze schematem**: `Rodzaj` niesie kod ze słownika schematu (`USTAWA`, `ROZP`,
  `OBW_TJ`…, wcześniej nazwa wersalikami), `DataAktu` zawiera `DataWydania`, `GUIDwersji` stoi w przestrzeni
  `gd:`, a rola organu to `wydajacy`. Daty wejścia w życie i mocy wstecznej aktu zostają na razie
  w dotychczasowych elementach — schemat trzyma je w `DaneCzasowe`.
- **Atrybuty GUID w przestrzeni `gd:`** (GUIDs.xsd): jednostki i organ — `gd:GUID`, zdanie — `gd:zdGUID`
  (wcześniej `GUID`) i `gd:fragmentGUID`, komendy nowelizacji — `gd:nowGUID`. Korzeń deklaruje `xmlns:gd`.
- **Atrybut `cudzyslowy` z wartościami schematu**: `brak`/`otwierajacy`/`zamykajacy`/`oba` zamiast maski
  0–3. Cytat tekstowy nie niesie już cudzysłowów w treści (tylko w atrybucie, jak w przykładach autorów
  schematu), a interpunkcja po cytacie stoi w zdaniu za nim.
- **Indeks górny w eId** w notacji `[n]`, zgodnie z aktualnym schematem i przykładami jego autorów:
  `art_4[1]` w eId jednostki i `element-eId` celu — [ADR-0019](docs/adr/0019-indeks-gorny-w-eid.md).

## [1.0.3] - 2026-09-02
### Dodano
- Parsowanie daty ogłoszenia aktu oraz całości nagłówka znajdującego się w ogłoszonych ustawach.

## [1.0.2] - 2026-08-27

Zmiany od wydania 1.0.1, pogrupowane funkcjami.

### Struktura projektu i wieloformatowe wejście
#### Dodano
- ujednolicony potok wejścia: adaptery DOCX, PDF i TXT sprowadzające dokument do wspólnej
  reprezentacji bloków (`DocumentBlock`), z detekcją formatu po sygnaturze pliku.
- odczyt PDF z warstwy tekstowej (PdfPig) wraz z filtrem artefaktów strony — nagłówków, stopek
  i numeracji. Skan bez warstwy tekstowej jest odrzucany z komunikatem, nie przetwarzany na ślepo.
- klasyfikację całego dokumentu (rodzaj aktu wg ZTP) wykonywaną przed budową modelu; wynik niesie
  koperta `ParseResult`, a zachowaniem steruje `ParseOptions` z `ParsePolicy`.
- katalog `docs/adr/` z decyzjami architektonicznymi wraz z ich uzasadnieniami.
#### Zmieniono
- projekty przemianowane z `WordParser*` i `ModelDto` na `Saga.Model`, `Saga.Core`, `Saga.Cli`
  i `Saga.Web`, w układzie `src/` + `tests/`.

### Wejście w życie aktu
#### Dodano
- rozpoznawanie przepisów o wejściu w życie: skanowanie klauzul, parsowanie formuł, rozwiązywanie
  dat oraz wskazanych jednostek redakcyjnych, wraz z mapą wyjątków od daty ogólnej.
- rozsyłanie daty wejścia w życie po całym drzewie jednostek redakcyjnych.
- atrybut `wejscie-w-zycie` w eksporcie XML.
- model: `EntryIntoForceInfo`, `EntryIntoForceFormula`, `PartialEntryIntoForce`,
  `ScopedUnitReference` oraz interfejs `IEntersIntoForce`.
- polskie liczebniki i daty jako jedno źródło prawdy (`PolishNumerals`, `PolishDates`).

### CI/CD i konteneryzacja
#### Dodano
- pipeline `.gitlab-ci.yml` według standardu CAISE: etapy `test`, `quality`, `build_check`
  i `build_and_publish`.
- bramki jakości: pokrycie 70% całości oraz 80% linii zmienionych w merge requeście, kontrola stylu
  i formatowania, kompilacja z `-warnaserror`, audyt podatności i pakietów wycofanych.
- smoke test obrazu — kontener musi wstać i odpowiedzieć, samo zbudowanie obrazu nie wystarcza.
- `version-check`: tag wydania musi być równy `<Version>` z `Directory.Build.props`; tag spoza
  wzorca `X.Y.Z[.devN]` zatrzymuje pipeline z czytelnym komunikatem.
- `Directory.Build.props` jako jedyne źródło wersji oraz `.editorconfig` dla całego repozytorium.
#### Zmieniono
- `Dockerfile` przepisany na aktualny układ projektów; poprzedni odwoływał się do nieistniejących
  już projektów i nie budował się w ogóle. Obraz startuje jako użytkownik nieuprzywilejowany.
- testy przeniesione z xUnit 2 na xUnit v3 na Microsoft.Testing.Platform — .NET 10 SDK nie wspiera
  już dla nich ścieżki VSTest. Pokrycie i raport JUnit są teraz wbudowane w runner.
- całe drzewo źródeł sformatowane jednorazowo według `.editorconfig` (zmiana wyłącznie białych znaków).

### Web API
#### Dodano
- `POST /v1/convert` — konwersja przesłanego dokumentu DOCX, PDF lub TXT do XML aktu prawnego.
- `GET /health` — sonda dla platformy wdrożeniowej, celowo poza wersjonowaniem ścieżki.
- dokumentację OpenAPI wraz z interfejsem Scalar jako jedyny interfejs usługi; `GET /` przekierowuje
  do dokumentacji, a sekcja `info` niesie wersję i commit wdrożonego builda oraz środowisko.
- `ProblemDetails` z `traceId` dla każdej awarii, łącznie z przekroczeniem limitu rozmiaru pliku
  i wyjątkami spoza obsługi endpointu.
#### Zmieniono
- `POST /parse_xml` działa dalej jako przestarzały alias ścieżki sprzed wersjonowania.
#### Usunięto
- interfejs HTML: renderery, szablony, `POST /parse` oraz widok wymuszonego parsowania.
- planowany projekt `Saga.Api` z dokumentacji — `Saga.Web` jest web API tego repozytorium.

### CLI
#### Zmieniono
- `saga` jest konwerterem wsadowym: `saga <plik|katalog> [--output <katalog>] [--pattern <wzorzec>]`.
  Katalog przetwarza bez rekursji, domyślnie `*.docx`, a dokumenty nierozpoznane jako akty prawne
  pomija i wykazuje w podsumowaniu przebiegu.
#### Usunięto
- drukowanie modelu na konsolę, raport klasyfikacji, tryb zgodności `--docx` oraz `--dump`.

## [1.0.1] - 2026-07-06
### Dodano
- rozróżnianie paragrafów kodeksu od ustępów.
- odnośniki (przypisy dolne).
- podpisy pod aktem.

## [0.2.15] - 2026-06-29
### Dodano
- instrukcje nowelizacyjne typu uchyl.
- pliki wygenerowane przez konwerter w wersji 0.2.15. Wykorzystane zostały dokumenty ustaw dostarczone przez RCL.
### Zmieniono
- otaczanie przypisów dolnych nawiasami kwadratowymi w taki sposób, żeby brać pod uwagę białe znaki. Wewnątrz nawiasu wchodzi tylko treść a białe znaki trafiają poza nawias.
- wykrywanie numeru jednostki redakcyjnej, tak żeby pojawiający się znak paragrafu § lub przypisy po numerze nie uniemożliwiały identyfikacji numeru jednostki.
- klasyfikowanie jednostek redakcyjnych w trudych miejscach. Dodano większą wartość rozpoznawaniu na podstawie treści w porównianiu z klasyfikowaniem na podstawie stylu. Daje to większą trafność w przypadkach, w których błędne rozpoznanie numeru jednostki wcześniej uniemożliwiało poprawne zaklasyfikowanie jednostki.

## [0.2.14] - 2026-06-18
### Zmieniono
- refactor kodu konwerteru przygotowujący kod na pojawienie się nowych wartości z parsera. Każda jednostka otrzymała komentarza z gotowymi liniami kodu, które przypiszą zmienną do odpowiedniego atrybutu XML.
- przeniesiono cytat-strukt do zdania, w którym znajduje się cytat.

## [0.2.13] - 2026-06-17
### Dodano
- parsowanie tytułu ustawy oraz dat wydania i ogłoszenia. Kod jest tymczasowy, napisany jako proof of concept.
### Zmieniono
- wprowadzono zmiany zgdone ze zmianami schema:
    - atrybut ELI zamieniono na eId
    - dodano atrybut GUID do każdej jednostki redakcyjnej
    - elementy zdanie i zdanie-ref ujednolicono
- poprawiono błąd w logice budowania zdań

## [0.2.12] - 2026-05-22
### Dodano
- klasę `CommonPartXmlConverter`, która ujednolica przetwarzanie części wspólnych dla wsszystkich jednostek redakcyjnych.
- klasę `XLinqWrappers` dodającą przestrzeń nazw do tworzonych elementów xml i zastępującą wcześniej występującą pętlę po wszystkich wytworzonych elementach.

### Zmieniono
- zmniejszono widoczność klas z public na internal.
- poprawiono i uproszczono konwertery.

## [0.2.11] - 2026-05-20
### Dodano
- autoryzację.

## [0.2.10] - 2026-05-19
### Dodano
- części wspólne wyliczeń.

### Zmieniono
- cytat w tekście wpisywany jest w zakresie elementu zdania,
- poprawki zgodne z nową wersją schema (0.1.13).

## [0.2.9] - 2026-05-18
### Dodano
- przetwarzanie kontekstu zdań. Rozpoznawane jest kiedy fragment jest kontynuacją zdania i fragmenty są numerowane w odpowiedniej kolejności.

## [0.2.8] - 2026-05-12
### Dodano
- zdania wewnątrz elementów <p>, bez rozpoznawania czy dane zdanie jest kontynuacją zdania występującego we wcześniejszym elemencie <p>

## [0.2.7] - 2026-05-11
### Dodano
- endpoint umożliwiający konwersję dokumentu DOCX do formatu XML.
- Dockerfile
- swagger

### Zmieniono
- interfejs webowy został rozszerzony o przycisk wywołujący konwersję wybranego dokumentu do XML oraz umożliwiający pobranie wygenerowanego pliku XML.

## [0.2.6] - 2026-05-06
### Dodano
- patojednostki. Każda jednostka staje się wersją patologiczną jeśli jednostka nadrzędna jest patologiczna lub liczba podjednostek (np. liczba liter w punkcie) jest równa 1. Pominięto sytuację, w której nie jest zachowana struktura jednostek (ustęp skłąd się z liter, a nie z punktów), ponieważ nie jest ona jeszcze przewidywana przez parser.

## [0.2.5] - 2026-05-04
### Dodano
- nowe typy cytatów (nowelizacji) zamiast wcześniej wykorzystywanego cytat-strukt. Wprowadzono:
    - cytat-strukt - odnoszący się wyłącznie do cytatów zawierających strukturę (jednostki),
    - cytat-w-tekscie - cytat pojawiający się w zdaniu,
    - cytat-blok - cytat blokowy wykorzystywany do tabeli, grafik, wzorów mat.

## [0.2.4] - 2026-04-29
### Dodano
- pliki wygenerowane przez konwerter w wersji 0.2.3. Wykorzystane zostały dokumenty ustaw dostarczone przez RCL.

## [0.2.3] - 2026-04-27
### Dodano
- tymczasowe przetwarzanie nagłówka dokumentu ustaw. Na jego podstawie usatlany jest ciąg ELI dokumentu. Jest tymczasowe, ponieważ:
    - działa wyłącznie w przypadku poprawnie sformatowanych dokumentów ustaw (np. w projektach nagłówek jest zupełnie pomijany),
    - kod parsera nagłówka będzie musiał w przyszłości zostać wydzielony do osobnego pliku/modułu, żeby rozdzielić odpowiedzialność przetwarzania nagłówka od parsowania całego dokumentu.
- pole Publisher opisujące nazwę dziennika urzędowego.

## [0.2.2] - 2026-04-20
### Zmieniono
- usunięto flagę `generateGuids`, która w tej wersji nie była wykorzystywana i nie pełniła żadnej funkcji. W razie potrzeby może zostać łatwo przywrócona w przyszłości.
- przeorganizowano i pogrupowano stałe konwerterów. Wraz ze wzrostem ich liczby pojawiły się trudności w nawigacji, dlatego wprowadzono logiczne grupowanie oraz strukturę ułatwiającą dostęp do stałych.
- przygotowano konwertery do obsługi metadanych w generowanych plikach XML – dodano odpowiednie miejsca w kodzie pod przyszłe uzupełnienie o metadane.

## [0.2.1] - 2026-04-15
### Dodano
- pliki wygenerowane przez konwerter w wersji 0.1.0. Wykorzystane zostały dokumenty ustaw dostarczone przez RCL.

### Zmieniono
- rozszerzono opisy w CHANGELOG.md

## [0.2.0] - 2026-04-14
### Zmieniono
- dopasowano strukturę generowanych plików XML bliżej do XML Schema poprzez:
    - przypisanie istniejących wartości do metadanych
    - zmianę nazw elementów i atrybutów
    - przebudowę struktury nowelizacji

## [0.1.1] - 2026-04-09
### Zmieniono
- poprawiono kod wykrywający pojawienie się w tekście nowelizacji. Frazy wewnątrz tekstu były błędnie rozpoznawane jako zwroty rozpoczynające nowelizację kiedy nie było to ich celem.

## [0.1.0] - 2026-04-01
### Dodano
- opcjonalną flagę `--xml` umożliwiającą zapis odczytanego pliku DOCX do formatu XML.
- konwerter modelu danych do XML - zaimplementowano mechanizm przekształcający wewnętrzny model dokumentu legislacyjnego na strukturę XML, odwzorowując hierarchię jednostek redakcyjnych (np. artykuły, ustępy) oraz obsługując poprawki (amendments)

## [0.0.2] - 2026-03-31
### Dodano
- mirror [projektu rcl]
- CHANGELOG.md

## [0.0.1] - 2026-03-26
### Dodano
- utworzono projekt na gitlabie

[projektu rcl]: https://github.com/kujawiak/word-parser
