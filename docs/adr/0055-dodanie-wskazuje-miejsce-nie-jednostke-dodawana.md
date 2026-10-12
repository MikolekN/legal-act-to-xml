# ADR-0055: Komenda dodająca wskazuje miejsce dodania, nie jednostkę dodawaną

- Status: Accepted
- Data: 2026-10-12

## Kontekst

Komenda dodająca jednostkę ma dwie postaci (§ 89 ZTP): z miejscem po jednostce („w art. 13 po ust. 5 dodaje się
ust. 5a w brzmieniu:") i bez niego — jednostka trafia na koniec jednostki nadrzędnej („w art. 13 dodaje się ust. 6
w brzmieniu:"). W pierwszej celem była jednostka, po której się dodaje (`dodaj-po`, ust. 5). W drugiej czytnik
adresu doklejał do ścieżki jednostkę dodawaną i eksport mówił „dodaj po ust. 6" — po jednostce, która dopiero
powstaje. Wyliczenie („dodaje się ust. 12–16") dawało przedział jednostek dodawanych albo po jednym cytacie na
każdą z nich.

Przykłady autorów schematu zapisują drugą postać jako `funkcja="dodaj-w"` z jednostką nadrzędną w `element-eId`
i jednym cytatem na wszystkie dodawane jednostki. Była to największa systematyczna różnica komend między naszym
eksportem a przykładami (23 z 29 różnic).

## Decyzja

- **Jednostki stojące za „dodaje się" są dodawane, nie wskazywane** (`UnitAddressReader`): adres komendy kończy
  się przed czasownikiem. Celem jest jednostka, po której się dodaje, albo — gdy jej nie ma — jednostka, w której
  się dodaje. Komenda bez własnego miejsca („dodaje się ust. 6" pod „w art. 13:") dziedziczy cel komendy
  nadrzędnej.
- **Funkcję rozstrzyga poziom celu wobec jednostki dodawanej** (`AmendmentXmlConverter.AddsInside`): cel, który
  nie sięga poziomu jednostki dodawanej, jest jednostką nadrzędną → `dodaj-w`; cel tego samego poziomu →
  `dodaj-po`. Nie wymaga to nowego pola w modelu.
- Adres jednostki cytowanej w akcie zmienianym (`AmendmentTargetPairing.AnchorFor`) nie zmienia się: jest to cel
  powyżej poziomu jednostki i jej własne oznaczenie („art_13__ust_6").
- Tiret dodawane („dodaje się tiret piąte") nie uzupełnia już celu (`AmendmentFinalizer.EnrichTargetsFromCommand`).

Uściśla to [ADR-0016](0016-wyliczenie-celow-to-osobne-komendy.md): przedział i wyliczenie celów dotyczą jednostek wskazywanych, a jednostki
dodawane celami nie są.

## Odrzucone alternatywy

- **Zostawić jednostkę dodawaną jako cel i zmienić tylko funkcję.** „Dodaj w ust. 6" jest tak samo błędne jak
  „dodaj po ust. 6"; odbiorca potrzebuje miejsca, a jednostkę dodawaną ma w cytacie razem z jej eId.
- **Pole „miejsce dodania" w modelu celu.** Poziom celu wobec jednostki cytowanej rozstrzyga to jednoznacznie,
  więc pole powtarzałoby informację, którą model już niesie.
- **Zatrzymywać adres na każdym czasowniku komendy.** Przy uchyleniu i zmianie brzmienia jednostka za czasownikiem
  jest celem („w art. 5 uchyla się ust. 2 i 3").

## Konsekwencje

- W korpusie zmienia się 487 zdań w 183 plikach: cytatów z funkcją `dodaj-w` jest 554 (było 94), a kilka jednostek dodawanych jedną komendą stoi w jednym cytacie (75 cytatów mniej). Zgodnych ze schematem jest nadal 1937 z 2321. Komendy zgodne z przykładami autorów schematu co do jednostki, elementu, funkcji i celu: DU/2026/26 — 102 z 103 (było 99), DU/2026/252 — 337 z 344 (było 315).
- Dodanie, przy którym żadna jednostka nie jest wskazana ani w komendzie, ani nad nią („dodaje się art. 37a
  w brzmieniu:"), ma cel nieznany (`UNKNOWN`) — przedtem celem była jednostka dodawana (21 komend).
- Zamiana znaku przed dodaniem ([ADR-0054](0054-zamiana-znaku-konczacego-jednostke-ma-wlasny-cel.md)) dziedziczy
  to samo: gdy jednostka ze znakiem nie jest nazwana, zmiana wskazuje jednostkę nadrzędną i jej ostatni znak.
- Reszta różnic wobec przykładów: cytowany odnośnik (`odnosnik-cyt`), instrukcje dla załączników
  („otrzymują brzmienie określone w załącznikach"), wiedza spoza aktu zmieniającego.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0055_Addition_NamesThePlace_NotTheAddedUnit`
- `UnitReferencesTests.Reader_AddedUnit_IsNotTheTarget`, `Reader_AdditionWithoutAPlaceOfItsOwn_NamesNothing`;
  `AmendmentTargetsExportTests` — dodanie w jednostce, kilka jednostek naraz, dodanie po jednostce, pod komendą
  nadrzędną, punkt w ustępie.
