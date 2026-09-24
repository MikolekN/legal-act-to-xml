# ADR-0015: Powołania jednostek rozbiera jedno źródło — także adresy celów nowelizacji

- Status: Accepted
- Data: 2026-09-23

## Kontekst

Tę samą pracę — rozpoznanie w tekście powołania jednostki („w art. 5 § 2 pkt 3 lit. a") — wykonywały
trzy miejsca, każde z własnym słownikiem:

- `UnitReferenceTokenizer` + `UnitPathCursor` — wyjątki wejścia w życie
  ([ADR-0010](0010-jeden-kursor-sciezki-jednostek.md) uczynił kursor właścicielem reguł powołania,
  ale tylko w obrębie wejścia w życie);
- `LegalReferenceService` — cele nowelizacji: osobne wyrażenia dla `art.`/`ust.`/`pkt`/`lit.`,
  każde dopasowywane niezależnie, w dowolnym miejscu tekstu;
- `AmendmentCommandParser` — własna alternatywa słów kluczowych i mapowanie jednostek.

Rozjazd był mierzalny na korpusie (2320 dokumentów, 7310 adresów celów nowelizacji): poziomy brane
z cytowanych wyrazów („wyrazy „art. 4 ust. 1 pkt 1-2d” zastępuje się…" dawało `…__pkt_1`), artykuł
odziedziczony z kontekstu wygrywał z jawnym adresem komendy, indeksy górne i litery dwuznakowe były
ucinane (`art_4e` zamiast `art_4e[2]`, `lit_z` zamiast `lit_zp`), a w wejściu w życie „lit. i" czytano
jako spójnik — data wyjątku „pkt 13 lit. i" trafiała na cały pkt 13.

Druga sprawa: „§" ma dwa znaczenia. W kodeksie to paragraf — podjednostka artykułu równoważna ustępowi
(§ 55 ust. 3 ZTP); w rozporządzeniu, uchwale czy zarządzeniu — jednostka główna (§ 124 ust. 1 ZTP).
Sam kontekst artykułu tego nie rozstrzyga: w ustawie „w § 2" bez artykułu w kontekście dawało `art_2`.

## Decyzja

**Słownik powołań i reguły ścieżki mieszkają w jednym miejscu — `Services/UnitReferences` — i korzysta
z nich każdy, kto czyta powołania jednostek.**

- `UnitReferenceTokenizer` — jedyna definicja słownika: słowa kluczowe jednostek, kształt oznaczeń
  i przedziałów, przyimek przed jednostką (§ 89 ust. 2 ZTP) i „i" po „lit." jako litera.
- `UnitPathCursor` — reguły ścieżki: dziedziczenie poziomu, kształt oznaczenia na poziomie
  (§ 57 ust. 1–4 ZTP) i rozstrzygnięcie „§".
- Konsumenci mają wyłącznie własną politykę tekstu: `UnitEnumerationParser` (wyliczenia i zakresy
  wejścia w życie) oraz `UnitAddressReader` (adres komendy: fragmenty w cudzysłowie nie wskazują
  jednostek, proza przerywa ciąg oznaczeń; ile celów wskazuje komenda — [ADR-0016](0016-wyliczenie-celow-to-osobne-komendy.md)).
  `LegalReferenceService` i `AmendmentCommandParser` nie mają własnych wzorców jednostek.

**„§" rozstrzyga się dwustopniowo:**

1. po artykule — jawnym albo odziedziczonym z komendy nadrzędnej — zawsze paragraf kodeksu;
2. samodzielny — według rodzaju aktu z klasyfikatora dokumentu (`SectionSignRole`): w akcie opartym
   na paragrafach (rozporządzenie, uchwała, zarządzenie, akt prawa miejscowego; dla obwieszczenia —
   ogłaszany akt według sygnału przewagi jednostki podstawowej) to jednostka główna; w ustawie to
   paragraf kodeksu nieznanego artykułu, czyli adres niepełny — artykułu się nie zgaduje.

Klasyfikacja trafia do dokumentu przed budową modelu, żeby ta decyzja była dostępna w trakcie
parsowania — nadal wyłącznie do `Classification` ([ADR-0007](0007-klasyfikacja-nie-mutuje-type.md)).

## Odrzucone alternatywy

- **To samo zabezpieczenie dopisane w każdym miejscu** (np. ucinanie tekstu na cudzysłowie
  w `LegalReferenceService`) — trzy kopie reguł rozjechałyby się przy najbliższej zmianie, tak jak
  rozjechały się słowniki.
- **„§" rozstrzygany wyłącznie kontekstem artykułu** — zmierzone: w ustawach fałszywe wyzwalacze
  nowelizacji („uchyla się od odbywania kary… w § 2") dawały cele `art_2`. W ustawie „§" nigdy nie jest
  jednostką główną, więc decyduje rodzaj aktu.
- **Ucinanie tekstu komendy na pierwszym cudzysłowie** — zmierzone: tytuł ustawy w cudzysłowie
  („rodzin „Za życiem”") zasłaniał adres stojący za nim. Fragmenty w cudzysłowie się wycina, tekstu nie
  ucina.

## Konsekwencje

- Nowe miejsce czytające powołania korzysta z tokenizera i kursora i nie definiuje własnych wzorców
  `art.`/`ust.`/`lit.` — pilnuje tego strażnik (niżej).
- Na korpusie zmieniło się 369 z 7310 adresów celów nowelizacji w 98 dokumentach (przejrzane
  kategoriami — poprawy) i 44 daty wejścia w życie w jednym dokumencie (poprawka „lit. i"); daty
  pozostałych 383 677 jednostek bez zmian.
- Wiele celów jednej komendy (wyliczenie, przedział) rozstrzyga
  [ADR-0016](0016-wyliczenie-celow-to-osobne-komendy.md).
- Jednostka główna „§" dostaje dziś segment `art`, tak jak w `reference-schema.md`; zgodność
  z [ADR-0014](0014-skroty-jednostek-w-eid.md) („paragraf → `par`") czeka na decyzję
  ([backlog § 3](../backlog.md)).
- Ścieżka „§ jako jednostka główna" nie jest ćwiczona end-to-end, bo parser nie buduje struktury aktów
  opartych na paragrafach (poza zakresem) — chronią ją testy jednostkowe.
- ADR-0010 obowiązuje dalej; ten ADR rozszerza jego zasadę poza wejście w życie.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0015_UnitKeywordVocabulary_IsDefinedInExactlyOnePlace` — żaden plik
  poza tokenizerem nie trzyma wzorców słów kluczowych jednostek.
- `ArchitectureDecisionTests.Adr0015_AmendmentTargetAndEntryIntoForce_ReadTheSameAddress` — ten sam
  adres czytany przez wejście w życie i przez cel nowelizacji daje tę samą ścieżkę.
- `ArchitectureDecisionTests.Adr0015_BareSectionSign_IsDecidedByActType` — samodzielny „§" w ustawie
  nie staje się artykułem, w rozporządzeniu jest jednostką główną.
- `UnitReferencesTests` (każda reguła osobno) oraz `UnitEnumerationParserTests.LetterI_IsALetter_NotAConjunction`,
  `LegalReferenceServiceTests` i `CodeParagraphTests` — te same reguły na fragmentach z korpusu.
