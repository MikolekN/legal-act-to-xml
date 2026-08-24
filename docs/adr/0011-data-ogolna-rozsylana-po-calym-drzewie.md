# ADR-0011: Data reguły ogólnej jest rozsyłana na wszystkie jednostki; brak daty znaczy nierozstrzygnięty wyjątek

- Status: Accepted
- Data: 2026-07-31

## Kontekst

Klauzula wejścia w życie ustala jedną datę dla całego aktu („Ustawa wchodzi w życie po upływie
3 miesięcy od dnia ogłoszenia") i listę wyjątków od niej („…, z wyjątkiem art. 12 pkt 3, który
wchodzi w życie z dniem 1 stycznia 2027 r."). W schemaPL każda jednostka redakcyjna i każde zdanie
mogą nieść atrybut `wejscie-w-zycie`; atrybut jest opcjonalny.

Pierwotnie serwis zapisywał datę **wyłącznie** na jednostkach objętych wyjątkiem, a data ogólna
lądowała tylko na `LegalDocument.EffectiveDate`. Uzasadnienie brzmiało: atrybut służy jednostkom
odbiegającym od daty aktu, więc powielanie daty ogólnej byłoby szumem.

Ten model zderzył się z drugim wymaganiem: jednostka objęta wyjątkiem, którego **nie da się
rozstrzygnąć** („wchodzi w życie z dniem określonym w komunikacie" — data nie istnieje w tekście),
ma zostać bez daty, żeby brak sam sygnalizował problem. Sygnał był jednak nieodróżnialny od tła:
w akcie referencyjnym Dz.U. 2026 poz. 26 parser wypełniał 591 dat na 39 865 jednostek korpusu
(1,5%) i **zero** na 31 798 zdaniach. Brak daty nie znaczył nic, bo bez daty było prawie wszystko.

Wzorcem docelowym jest ręcznie wypełniony plik RCL tego samego aktu: 886 atrybutów
`wejscie-w-zycie`, z czego 295 to data reguły ogólnej — rozsiana także po treści cytatów
nowelizacji (`art_85a`, `rozdz_9a`). Jednostka goszcząca wyjątek **zakresowy** nosi w nim datę
ogólną (`art_1__pkt_9` = 2026-04-13), a data wyjątku siedzi na jednostkach wewnątrz cytatu.

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

Rozstrzyga wyłącznie to, czy znamy **cele** wyjątku — nie to, czy znamy datę. Wyjątek głębszy
wygrywa z płytszym sam z siebie, bo nadpisanie dzieje się w trakcie zejścia; wyjątków nie trzeba
sortować.

Datę niosą wszystkie typy, które implementują `IEntersIntoForce` i są osiągalne z drzewa: jednostki
systematyzacyjne (także niejawne), artykuły, ustępy (także niejawne), punkty, litery, tirety każdej
głębokości, części wspólne, każdy `TextSegment` oraz `Amendment` — nowelizacja wchodzi w życie razem
z jednostką, która ją zarządza. Eksport schemaPL nie jest filtrowany.

## Odrzucone alternatywy

**Poprzednia polityka: rozsyłamy tylko wyjątki.** Odpadła, bo czyni sygnał „brak daty = nie dało się
rozstrzygnąć" niewidocznym — brak daty ma wtedy dwa znaczenia naraz i żadnego z nich nie da się
odczytać. Plik referencyjny RCL też jej nie stosuje.

**Zatrucie ograniczone do jednej jednostki** (potomkowie nierozstrzygalnego wyjątku dostają datę
ogólną). Odpadła, bo ustęp nie może wejść w życie wcześniej niż jego artykuł: skoro klauzula jawnie
wyjęła artykuł spod reguły ogólnej, wpisanie jego ustępom daty ogólnej byłoby twierdzeniem wiadomo
fałszywym.

**Odebranie daty jednostce goszczącej nierozłożony zakres.** Odpadła, bo zakres jest zawężeniem, nie
zaprzeczeniem: „art. 1 pkt 20 w zakresie, w jakim dotyczy planu rozwoju sieci wodorowej" mówi, że
odroczona jest *część* tego, co pkt 20 wprowadza. Przy zakresie rozłożonym host zostaje przy dacie
ogólnej (potwierdzone w pliku RCL); byłoby niespójne, gdyby nieudane rozłożenie tego samego kształtu
zmieniało datę hosta. Sygnałem pozostaje `IsApplicable == false` i komunikat walidacji — ADR-0008
zostaje w mocy.

**Mapa wyjątków kluczowana ścieżką (eId).** Odpadła twardo: eId **nie jest w akcie unikalny** — ta
sama ścieżka istnieje raz w akcie i raz w przytoczonej treści nowelizacji (w pliku referencyjnym
sześć takich kolizji). Klucz po tożsamości referencji jest jedynym poprawnym.

**Adnotacja na encji** (nowe pole w `Saga.Model`, np. `HasExplicitDate`). Odpadła: `Saga.Model` to
czyste DTO; stan algorytmu wyciekłby do modelu, snapshotu i serializacji.

## Konsekwencje

- **Pusta data niesie informację.** To jest kontrintuicyjne i musi zostać zapisane: `default`
  na `EffectiveDate` nie jest wartością domyślną do „posprzątania", tylko wynikiem przebiegu.
- Zapis jest **totalny i idempotentny** — propagator przypisuje datę (albo `default`) każdej
  odwiedzonej jednostce. Zapis warunkowy („pisz tylko gdy jest data") zostawiałby stan poprzedniego
  przebiegu i psuł niezmiennik „przebieg rozstrzyga o każdej jednostce".
- Skala rośnie o dwa rzędy wielkości: na korpusie 242 aktów z 591 dat na 39 865 jednostek do
  kompletu, plus 31 798 zdań. `Saga.Web` zaczyna pokazywać `effectiveDate` przy każdej jednostce.
- Datę pisze **wyłącznie** `EffectiveDatePropagator`. `EntryIntoForceTargetResolver` tylko rozstrzyga
  ścieżki (`Resolve`, `ResolveInAmendment`, `ResolveSegment`) — dawne `ApplyDate` zostało usunięte,
  żeby nie istniały dwa miejsca zapisu.
- Treść cytatu nowelizacji jest poddrzewem **odpiętym** od `RootPart`, więc propagacja musi w nią
  wejść jawnie (`EditorialUnitWalker.Children(unit, includeAmendmentContent: true)`), podczas gdy
  skaner klauzul musi ją omijać (klauzula w cytacie należy do aktu zmienianego).
- `ISystematizingUnit.HeadingEffectiveDate` zostaje nietknięte **świadomie**: parser nie rozpoznaje
  jeszcze poprawnie jednostek systematyzacyjnych, więc data nagłówka nie ma dziś skąd pochodzić.
- Diagnostyka „jednostki nieodnalezione w akcie" pojawia się teraz także dla wyjątków o
  nierozstrzygalnej dacie — wcześniej były pomijane w całości, więc ich brakujące cele milczały.

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
zestawia pary (jednostka, data) z plikiem referencyjnym RCL. Po tej decyzji zgodnych par jest 274
(przed: 50), a par „RCL ma datę, my nie" — zero (przed: 225).
