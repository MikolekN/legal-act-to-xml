# ADR-0011: Data reguły ogólnej wejścia w życie trafia na wszystkie jednostki. Brak daty znaczy nierozstrzygnięty wyjątek

- Status: - (w części o dacie nowelizacji zastąpiony przez [ADR-0121](0121-tresc-cytowana-wchodzi-w-zycie-ze-zdaniem-ktore-ja-przytacza.md))
- Data: 2026-07-31

## Kontekst

Klauzula wejścia w życie ustala jedną datę dla całego aktu („Ustawa wchodzi w życie po upływie
3 miesięcy od dnia ogłoszenia") i listę wyjątków od niej („…, z wyjątkiem art. 12 pkt 3, który
wchodzi w życie z dniem 1 stycznia 2027 r.").

## Decyzja

Data wejścia w życie jest **kolorowaniem drzewa**. Każda jednostka ma datę rządzącą, dziedziczoną
od rodzica; korzeń startuje z datą reguły ogólnej. Wyjątek wskazujący jednostkę nadpisuje jej datę
rządzącą — także nadpisaniem „nie wiadomo", które propaguje się w dół.

| sytuacja | jednostka | jej potomkowie |
|---|---|---|
| brak wyjątku | data ogólna | dziedziczą |
| wyjątek rozstrzygnięty | data wyjątku | dziedziczą datę wyjątku |
| wyjątek nierozstrzygnięty (cel znany, data nie) | **bez daty** | **bez daty** |
| wyjątek zakresowy (rozłożony lub nie) | data ogólna — host nietknięty | jednostki wskazane zakresem dostają datę wyjątku |
| nierozstrzygnięta reguła ogólna | bez daty | bez daty |

Datę niosą wszystkie typy, które implementują `IEntersIntoForce` i są osiągalne z drzewa: jednostki
systematyzacyjne (także niejawne), artykuły, ustępy (także niejawne), punkty, litery, tirety każdej
głębokości, części wspólne, każdy `TextSegment` oraz `Amendment` — nowelizacja wchodzi w życie razem
z jednostką, która ją zarządza.

## Konsekwencje

- **Pusta data niesie informację.** `default` na `EffectiveDate` nie jest wartością domyślną do
  „posprzątania", tylko wynikiem przebiegu.
- Datę pisze **wyłącznie** `EffectiveDatePropagator`. `EntryIntoForceTargetResolver` tylko rozstrzyga
  ścieżki (`Resolve`, `ResolveInAmendment`, `ResolveSegment`).
- Treść cytatu nowelizacji jest poddrzewem **odpiętym** od `RootPart`, więc propagacja musi w nią
  wejść jawnie (`EditorialUnitWalker.Children(unit, includeAmendmentContent: true)`), podczas gdy
  skaner klauzul musi ją omijać (klauzula w cytacie należy do aktu zmienianego).
- `ISystematizingUnit.HeadingEffectiveDate` zostaje nietknięte **świadomie**: parser nie rozpoznaje
  poprawnie jednostek systematyzacyjnych, więc data nagłówka nie ma dziś skąd pochodzić.

## Weryfikacja

Strażnicy w `tests/Saga.Core.Tests/ArchitectureDecisionTests.cs`:

- `Adr0011_GeneralDate_ReachesEveryUnit_UnresolvedExceptionLeavesItBlank` — jeden akt z trzema
  kształtami naraz. Powrót do „rozsyłamy tylko wyjątki" łamie pierwszą asercję; „wpiszmy cokolwiek
  zamiast pustki" — ostatnią.
- `Adr0011_WrittenDates_ComeOnlyFromTheClause` — niezmiennik pochodzenia: każda data w drzewie
  należy do zbioru `{reguła ogólna} ∪ {daty wyjątków}`. Łamie się przy każdej pomyłce propagacji
  (przesunięcie o poziom, zła gałąź cytatu, dziedziczenie w bok). Test chodzi po drzewie **własnym**
  obchodem, nie `EditorialUnitWalker` — wspólny błąd obchodu byłby dla niego niewidoczny.
- `Adr0008_UnparseableExceptionScope_IsFlagged_AndBaseClauseStillApplies` — dodana asercja: host
  zakresu opisanego prozą zachowuje datę ogólną.
- `Adr0008_MissingAnnouncementDate_NeverFallsBackToActDate` — dodana asercja: nierozstrzygnięta
  reguła ogólna nie daje daty żadnej jednostce. Rozsyłanie nie jest furtką do zgadywania.

Semantykę samego przebiegu pokrywa `EffectiveDatePropagatorTests` (dziedziczenie, zatrucie w dół,
odtrucie głębszym wyjątkiem, zdanie bez ruszania jednostki, treść cytatu, idempotencja, kolizje).

Pomiar zewnętrzny: `dotnet run --project tools/CorpusValidator -- --compare-eif <RCL.xml> <zrzut.xml>`
zestawia pary (jednostka, data) z plikiem referencyjnym RCL.
