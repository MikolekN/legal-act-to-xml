# ADR-0054: Zamiana znaku kończącego jednostkę jest zmianą w tekście z własnym celem, a uchylenie za nią dotyczy jednostki nazwanej po komendzie

- Status: Accepted
- Data: 2026-10-12

## Kontekst

Dodanie albo uchylenie ostatniej jednostki wyliczenia poprzedza zamiana znaku kończącego jednostkę przed nią:
„w art. 4 w pkt 20 kropkę zastępuje się średnikiem i dodaje się pkt 21 w brzmieniu:", „w pkt 4 średnik zastępuje
się kropką i uchyla się pkt 5" (ok. 470 w korpusie). Dotąd:

- zamiana znaku nie dawała żadnej instrukcji, choć przykłady autorów schematu zapisują ją jako
  `now:zamien-tekst` z `now:wystapienie="ostatnie"` na jednostce sprzed znaku;
- **uchylenie stojące za zamianą znaku wskazywało złą jednostkę**: cele zdania czytano ze ścieżki sprzed znaku
  („w pkt 4"), więc `now:uchyl` dostawał pkt 4 zamiast pkt 5 (19 zdań korpusu).

Model zmian w tekście ([ADR-0052](0052-zmiana-wyrazow-jest-instrukcja-zamien-tekst.md), [ADR-0053](0053-skreslenie-wyrazow-to-zamiana-na-pusty-tekst.md)) zakładał, że zmiana dotyczy
celów nowelizacji. Tu zdanie ma dwie części o różnych jednostkach.

## Decyzja

- **Zamiana znaku to zmiana w tekście** (`AmendmentCommandKind.ReplaceSign` → `TextChange` ze znakiem
  zastępowanym, zastępującym i `Occurrence = TextOccurrence.Last`). Wyrazy dodawane zaraz za nowym znakiem
  („… zastępuje się przecinkiem i dodaje się wyraz „lub"") należą do tej samej zmiany („." → „, lub").
- **W zdaniu, które zmienia też jednostkę, zamiana znaku ma własny cel** — jednostkę wskazaną przed komendą
  (`TextChange.Targets`) — i czeka w `ParsingContext.PendingTextChanges` na nowelizację tego przepisu, do której
  dołącza ją `AmendmentFinalizer`. W eksporcie stoi przed tekstem zdania, z `now:wystapienie="ostatnie"`.
- **Uchylenie stojące za zamianą znaku bierze cele z części zdania po komendzie**, czytanej w kontekście
  jednostki sprzed znaku (`AmendmentStateManager.KeepSignChanges`): „… i uchyla się pkt 5" → pkt 5 tego samego
  ustępu, „… i uchyla się pkt 2–7" → przedział.
- Dodanie jednostki za zamianą znaku zostaje przy jednostce sprzed znaku jako punkcie odniesienia (`dodaj-po`) —
  jak dotąd i jak w przykładach.

## Odrzucone alternatywy

- **Zmiana w tekście bez własnego celu.** Przy uchyleniu celem nowelizacji jest jednostka uchylana, więc
  instrukcja zamiany znaku wskazałaby pkt 5 zamiast pkt 4.
- **Osobna nowelizacja dla zamiany znaku.** Jednostka niesie jedną nowelizację (`IHasAmendments`).
- **Poprawić sam odczyt celów („uchyla się pkt 5" po „w pkt 4" jako sąsiad).** Ta sama budowa bez zamiany
  znaku znaczy co innego („w pkt 4 uchyla się lit. a" — litera punktu 4); gdzie zaczyna się druga część zdania,
  wie tylko rozbiór komend.

## Konsekwencje

- W korpusie przybywa 463 instrukcji `now:zamien-tekst` w 196 plikach, a 20 zdań z uchyleniem za zamianą znaku wskazuje teraz jednostkę uchylaną (w tym przedziały „pkt 2–7"); poza tym eksport się nie zmienia, zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory). Przykłady autorów schematu: DU/2026/26 — wszystkie 7 różnych par (`stary-tekst`, `nowy-tekst`) i wszystkie 9 instrukcji mają u nas odpowiednik, DU/2026/252 — 81 instrukcji wobec 83.
- `TextOccurrence` zna „wszystkie" i „ostatnie"; „występujący dwukrotnie wyraz" zostaje przy domyślnym.
- Zmiana wyrazów w komendzie złożonej ze zmianą jednostki („wyrazy … zastępuje się …, a ust. 2 otrzymuje
  brzmienie:") dalej ginie z ostrzeżeniem — w korpusie nie występuje.
- Reszta różnic wobec przykładów to wiedza spoza aktu zmieniającego: formy odmienione przy § 88 ZTP i ostatni
  wyraz jednostki przy „dodaje się przecinek".

## Weryfikacja

- `ArchitectureDecisionTests.Adr0054_ReplacedSign_KeepsItsOwnUnit_AndTheRepealAfterItNamesTheNextOne`
- `AmendmentCommandParserTests` — postaci komendy; `AmendmentTargetsExportTests` — zamiana znaku przed dodaniem
  jednostki, przed uchyleniem (pojedynczym i przedziału), samodzielna i z dodanymi wyrazami.
