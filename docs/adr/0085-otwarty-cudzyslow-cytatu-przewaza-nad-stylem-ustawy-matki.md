# ADR-0085: Otwarty cudzysłów cytatu przeważa nad stylem ustawy matki

- Status: Accepted
- Data: 2026-10-14

## Kontekst

Granicę treści cytowanej w akcie ze stylami wyznaczają style: akapit w stylu `Z/…` jest treścią cytowaną, a akapit
w stylu jednostki aktu (`ART`, `UST`, `PKT`, `LIT`, `TIR`) kończy nowelizację (`AmendmentStateManager.UpdateState`).
Bilans cudzysłowów (`QuoteBalanceTracker`) liczy się tylko przy zbieraniu bez stylów — każdy znaczący styl go rozbraja.

Szablon bywa użyty źle: w rozporządzeniu zmieniającym (korpus: 338265_3166784) cytat po komendzie tiretu
„– w ppkt I dodaje się wariant D w brzmieniu:" złożono stylem punktu aktu („„Wariant D", „1. Wykształcenie …",
„2. Staż pracy … .”,"). Nowelizacja kończyła się na pierwszym akapicie, „1." i „2." stawały ustępami § 1 aktu
zmieniającego, a mieszanka ustępów jawnych i niejawnego rozbijała cały paragraf na jednostki `nie-ztp-*` (62 w akcie).
Cudzysłów otwarty i niedomknięty mówi jednak wyraźnie, że cytat trwa — treść przeważa nad stylem (zasada klasyfikacji
warstwowej).

## Decyzja

**Dopóki zebrana treść zaczyna się cudzysłowem, którego nie domknięto (`AmendmentCollector.LeavesQuoteOpen`, liczone
bez względu na style), akapit w stylu ustawy matki jest treścią cytowaną. Cytat kończy jak dotąd akapit z komendą
(zmiana brzmienia, uchylenie, zmiana wyrazów) albo kolejna jednostka aktu zmieniającego: numer następujący po otwartej
jednostce tego samego rodzaju („3)" po punkcie 2), tiret przy otwartym tirecie i każdy artykuł.**

- Literówka — cytat bez cudzysłowu zamykającego — zatrzymuje się na następnej jednostce aktu zmieniającego, nie
  wchłania jej. Cytat otwarty do końca dokumentu nadal rozstrzyga ponowna budowa
  ([ADR-0049](0049-cytat-niedomkniety-do-konca-dokumentu-konczy-ponowny-przebieg.md)).
- Tekst bez oznaczenia za tiretami stojącymi na początku cytatu jest dalszą treścią ostatniego tiretu
  ([ADR-0029](0029-akapit-bez-oznaczenia-jest-dalsza-trescia-jednostki.md)), nie tekstem przed jednostkami — inaczej zdania spod wyliczenia stawały w eksporcie
  przed tiretami.

## Odrzucone alternatywy

- **Styl rozstrzyga** (stan dotychczasowy). Sprzeczne z zasadą „treść przed stylem"; pomylony styl jednego akapitu
  rozbijał cały przepis aktu zmieniającego.
- **Sam bilans cudzysłowów** — cytat trwa aż do cudzysłowu zamykającego. Literówka wchłaniałaby kolejne przepisy aktu
  zmieniającego aż do następnego artykułu; ponowna budowa z ADR-0049 działa tylko wtedy, gdy cytat zostaje otwarty do
  końca dokumentu.
- **Uzbrojenie `QuoteBalanceTracker` także dla treści ze stylami.** Tracker rozstrzyga też koniec treści bez stylów
  (domknięcie, przecinek, zamknięcie bez otwarcia); zmiana dotknęłaby każdej nowelizacji ze stylami, a pytanie jest
  węższe: czy styl ustawy matki kończy cytat, którego cudzysłów wciąż jest otwarty.

## Konsekwencje

- W korpusie zmienia się 4 dokumenty (dwie pary tego samego aktu). W rozporządzeniu zmieniającym 338265 (i jego kopii) 62 jednostki `nie-ztp-*` stają się jednostkami ZTP: cytat „Wariant D” w stylu punktu nie rozbija już § 1 na ustępy, a 9 zmian w tekście załącznika wskazuje jednostki załącznika (`zal_1__pkt_4`) zamiast punktów rozporządzenia. W 330183 (i kopii) cytowane tirety objaśnień przestają być tiretami aktu zmieniającego. Zgodnych ze schematem nadal 2279 z 2321 (oba walidatory, ten sam zbiór); pokrycie tekstu bez zmian. Zgodność bez stylów rozporządzeń 98,11 % → 98,64 % jednostek (brakujących 548 → 396, nadmiarowych 253 → 101): budowa ze stylami zgadza się teraz z budową z samej treści.
- Zmiany w tekście w treści załącznika („w ppkt I w wariancie B w pkt 2 wyraz …") wskazują teraz najgłębszą znaną
  jednostkę załącznika ([ADR-0080](0080-komenda-w-zalaczniku-wskazuje-jednostki-zalacznika.md)); kroki „ppkt", „wariant" zostają nieodczytane.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0085_OpenQuote_OutweighsTheStyleOfTheAct_UntilACommandOrTheNextUnitOfTheAct`
- `AmendmentQuoteBoundaryTests.Collector_SeesAQuoteLeftOpen_WhateverTheStyle`,
  `AmendmentQuoteBoundaryTests.LinesOfAnOpenQuoteInTheStyleOfTheAct_AreQuotedContent`,
  `AmendmentQuoteBoundaryTests.QuoteLeftOpenByATypo_EndsAtTheNextUnitOfTheAct`,
  `AmendmentBuilderTests.Build_TextAfterStandaloneTirets_ContinuesTheLastTiret_InItsPlace`.
