# ADR-0016: Wyliczenie celów nowelizacji to osobne komendy; `element-do-eId` znaczy wyłącznie koniec przedziału

- Status: Accepted
- Data: 2026-09-23

## Kontekst

Komenda nowelizacyjna wskazuje jedną jednostkę („w art. 1"), przedział („w art. 1–3") albo wyliczenie
(„w art. 1, 2 i 3", a w komendzie złożonej § 88 ZTP — „użyte w art. 40, w art. 42 w ust. 1 …").

Eksport miał dla komendy parę atrybutów `element-eId` / `element-do-eId` i wypełniał ją pierwszym
i ostatnim elementem `Amendment.Targets` — czyli semantyką przedziału. Wyliczenia nieciągłego
(„§ 1, 2, 4 i 5") nie da się w niej zapisać: przedział 1–5 obejmowałby § 3, którego komenda nie wskazuje.
W praktyce czytnik adresu zwracał tylko pierwszą jednostkę, więc wyliczenie traciło cele, a przedział
w ogóle nie trafiał na wyjście (na korpusie 2320 dokumentów `element-do-eId` nie wystąpił ani razu).

## Decyzja

**Cel to jedna jednostka albo jeden przedział; wyliczenie to osobna komenda na każdy cel.**

- „w art. 1" → `element-eId="art_1"`;
- „w art. 1–3" → jeden cel: `element-eId="art_1"`, `element-do-eId="art_3"`;
- „art. 1, 2 i 3" → trzy komendy, każda z własnym `element-eId` i `nowGUID` (pierwsza zachowuje
  identyfikator nowelizacji).

Model: każdy element `Amendment.Targets` to osobny cel, a koniec przedziału niesie
`StructuralAmendmentReference.RangeEnd`. Eksport nie wyprowadza przedziału z pierwszego i ostatniego
elementu listy.

Treść przy wyliczeniu (`AmendmentXmlConverter`, `AmendmentTargetPairing`):

- uchylenie — instrukcja `uchyl` na każdy cel;
- nowe brzmienie jednostek („ust. 2 i 3 otrzymują brzmienie: „2. … 3. …”") — każda komenda dostaje
  swoje jednostki treści: po oznaczeniu (przedział zabiera jednostki od początku do końca), a gdy
  oznaczenia się rozjeżdżają przy tej samej liczbie celów bez przedziału i jednostek — po kolejności.
  Cytat nadal jest jeden: maska `cudzyslowy` rozkłada się na kolejne komendy (pierwsza otwiera,
  ostatnia zamyka), a interpunkcja po cytacie stoi raz;
- treść bez jednostek (zamiana wyrazów) — każda komenda niesie tę samą treść;
- jednostek nie da się przypisać celom — jedna komenda na pierwszy cel i ostrzeżenie na właścicielu.

Granica wyliczenia w tekście komendy (`UnitAddressReader`): kolejna jednostka jest celem, gdy dołącza
ją separator wyliczenia (z przyimkiem „w" przed jednostką, § 89 ust. 2 ZTP) — także po prozie, która
dookreśla poprzednią („w ust. 5 we wprowadzeniu do wyliczenia, w art. 4"). Nie jest celem, gdy przed
nią stoi czasownik komendy („po pkt 6 dodaje się przecinek i pkt 7") albo sama proza („po art. 5
dodaje się art. 5a"), ani gdy dołącza ją na końcu sam przecinek — wyliczenie zamyka spójnik, a taki
ogon to adres w zmienianym akcie nowelizującym („w art. 22 w pkt 2, w art. 19a uchyla się ust. 3").

## Odrzucone alternatywy

- **Przedział z pierwszego i ostatniego celu listy** (stan dotychczasowy) — przekłamuje wyliczenie
  nieciągłe, a przedziału i tak nie da się odróżnić od wyliczenia dwóch jednostek.
- **Lista adresów w jednym atrybucie** — komenda niesie też treść; przy „ust. 2 i 3 otrzymują
  brzmienie" każdy cel ma swoją jednostkę treści, czego jeden cytat z listą adresów nie oddaje.
- **Przedział rozwinięty w listę celów** (jak w wejściu w życie) — przedział to jedna komenda,
  a rozwinięcie wymaga znajomości jednostek pośrednich („2a", „2b"), których tekst komendy nie wymienia.
- **Cytat, dwukropek i średnik jako granica adresu** — zmierzone: bez skutku na korpusie (to samo
  załatwia reguła czasownika), a cytat jako granica zepsułby wyliczenie z cytowanym dookreśleniem
  („w definicji „X” oraz w art. 7").
- **Liczba czasownika jako sygnał jednego celu** („otrzymuje" wobec „otrzymują") — „uchyla się"
  i „dodaje się" się nie odmieniają; reguła przecinka obejmuje wszystkie komendy.

## Konsekwencje

- Na korpusie: `element-eId` 7310 → 7912 (602 kolejne cele wyliczeń w 119 dokumentach),
  `element-do-eId` 0 → 202; żaden dotychczasowy `element-eId` nie zniknął, `UNKNOWN` bez zmian
  (366), daty wejścia w życie bez zmian.
- Treść nieprzypisywalna do celów zostaje przy jednej komendzie — na korpusie 9 przypadków: przepis
  o wejściu w życie wzięty za nowelizację (2), zmiana wiersza tabeli z celami odziedziczonymi po
  komendzie nadrzędnej (4) i treść z jednostkami spoza komendy (3).
- Zamiana wyrazów w kilku jednostkach powtarza w XML-u ten sam `cytat-w-tekscie` na każdy cel.
- Dodanie bez kotwicy („w art. 86 dodaje się ust. 4a i 4b") ma za cele dodawane jednostki — każdą
  osobno, każdą ze swoją treścią. Że cel dodania to jednostka dodawana, a nie kotwica, jest luką
  sprzed tej decyzji ([backlog § 3](../backlog.md)).
- Encja bez własnego adresu przejmuje wszystkie cele encji nadrzędnej; adres encji podrzędnej buduje
  się na pierwszym z nich.
- Snapshot kanoniczny: `target` przedziału ma atrybut `rangeEnd`.
- Zawęża [ADR-0015](0015-jedno-zrodlo-powolan-jednostek.md) w jednym punkcie: `UnitAddressReader`
  czyta wszystkie cele, nie pierwszy. Źródło słownika i reguł ścieżki bez zmian.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0016_EnumerationIsACommandPerTarget_RangeIsOneCommand` — wyliczenie
  z przedziałem („pkt 31, 32 i 34-36") daje trzy instrukcje, a `element-do-eId` ma tylko przedział.
- `AmendmentTargetsExportTests` — przypisanie treści celom, maska `cudzyslowy` i interpunkcja przy
  kilku komendach, zamiana wyrazów, treść nieprzypisywalna z ostrzeżeniem.
- `UnitReferencesTests` (sekcja ADR-0016) i `LegalReferenceServiceTests.ReadAmendmentTargets_*` —
  granica wyliczenia na dosłownych fragmentach z korpusu.
