# ADR-0070: Uchylane zdanie jest adresowane zdaniem (`zdn_N`), a nie jednostką, w której stoi

- Status: Accepted — Superseded in part by [ADR-0102](0102-zdanie-nazwane-w-komendzie-jest-celem.md) (zmiany inne niż uchylenie zdania)
- Data: 2026-10-13

## Kontekst

„W ust. 2 skreśla się zdanie drugie", „w art. 10 w ust. 5 uchyla się zdanie drugie" — uchylane jest jedno zdanie
jednostki. Eksport dawał `now:uchyl element-eId="…ust_2"`: instrukcję uchylenia całego ustępu.

Cele nowelizacji od początku gubią poziom zdania (`LegalReferenceService.ReadAmendmentTargets`), i dla komend
z treścią jest to zgodne z przykładami autorów schematu: „w art. 126 zdanie pierwsze otrzymuje brzmienie:" ma
`element-eId="…art_126"`, a o które zdanie chodzi, mówi `zdanie nr="1"` w treści cytowanej; zmiana w tekście
(„w ust. 3 w zdaniu pierwszym … wyrazy … zastępuje się …") ma `now:zamien-tekst` z adresem jednostki.
Uchylenie treści nie niesie — bez zdania w adresie nie ma gdzie go zapisać.

Schemat przewiduje na `now:uchyl` atrybut `gd:zdGUID` („zdanie, które podlega nowelizacji") — identyfikator
zdania z zapisu aktu zmienianego. Akt zmieniający go nie zawiera, a parser aktu zmienianego nie widzi.
Przykłady nie mają ani jednego uchylenia zdania.

## Decyzja

**Gdy przedmiotem uchylenia jest zdanie, adres celu zachowuje poziom zdania**: `element-eId="…__ust_2__zdn_2"`
(skrót `zdn` — [ADR-0014](0014-skroty-jednostek-w-eid.md)).

- Rozstrzyga przedmiot czasownika (`AmendmentCommandParser.RepealsSentence`: zdanie stoi zaraz za „uchyla się" /
  „skreśla się" — [ADR-0069](0069-czasownik-uchylenia-jest-komenda-w-artykule-zmieniajacym-albo-przy-jednostce.md)),
  a adres musi nazywać jednostkę: zdanie bez jednostki („w opisie koszuli skreśla się zdanie szóste") niczego nie
  adresuje i cel zostaje nieznany.
- Wyliczenie („uchyla się zdania drugie i trzecie") daje cel na każde zdanie, przedział — `element-do-eId`
  ([ADR-0016](0016-wyliczenie-celow-to-osobne-komendy.md)).
- Szyk „uchyla się drugie zdanie" tokenizator powołań sprowadza do „zdanie drugie"
  (`UnitReferenceTokenizer`), więc czyta go każdy odbiorca wspólnego słownika.
- Pozostałe komendy zostają przy jednostce, jak w przykładach: zmiana brzmienia zdania, zmiana w tekście zdania,
  dodanie zdania po zdaniu.

## Odrzucone alternatywy

- **Zostawić adres jednostki.** Instrukcja mówi wtedy co innego niż przepis: uchyla cały ustęp.
- **Nie dawać instrukcji.** Komenda jest w pełni czytelna; gubić ją tylko dlatego, że adres ma o jeden poziom
  więcej, to strata bez powodu.
- **`gd:zdGUID`.** Wymaga zapisu aktu zmienianego — wiedzy spoza aktu zmieniającego.
- **Zdanie w każdym adresie celu.** Zmienia komendy, które dziś zgadzają się z przykładami (treść cytowana niesie
  numer zdania, zmiana w tekście wskazuje jednostkę).

## Konsekwencje

- W korpusie zmienia się 28 zdań w 21 plikach: `now:uchyl`, który wskazywał całą jednostkę („w ust. 2 skreśla się zdanie drugie" → `…ust_2`), wskazuje teraz zdanie (`…ust_2__zdn_2`). Poza tym eksport się nie zmienia; zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory).
- Zdania w eksporcie nie mają eId (ADR-0014), więc `zdn_N` nie jest identyfikatorem do odszukania, tylko adresem:
  N-te zdanie wskazanej jednostki. Schemat opisuje `element-eId` jako identyfikator jednostki redakcyjnej —
  pytanie do autorów schematu, jak adresować zdanie z samego aktu zmieniającego, jest w `docs/backlog.md`.
- Zmiana w tekście ograniczona do zdania („w zdaniu drugim wyrazy …") nadal wskazuje całą jednostkę.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0070_RepealedSentence_IsAddressedBySentence_NotByItsUnit`
- `LegalReferenceServiceTests` — zdanie uchylane (także „drugie zdanie", paragraf kodeksu, wyliczenie), zdanie
  nieuchylane, zdanie bez jednostki; `UnitReferencesTests` — liczebnik przed słowem „zdanie";
  `AmendmentTargetsExportTests` — adres i przedział w eksporcie.
