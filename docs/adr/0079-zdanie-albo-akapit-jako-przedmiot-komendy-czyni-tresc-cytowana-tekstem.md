# ADR-0079: Zdanie albo akapit jako przedmiot komendy czyni treść cytowaną tekstem — styl i oznaczenia w cytacie jednostek nie tworzą

- Status: Accepted
- Data: 2026-10-14

## Kontekst

Rodzaj treści cytowanej rozstrzyga odczyt jej akapitów: styl zmiany (`Z/…`) i oznaczenie na ich początku. Gdy
komenda zmienia albo dodaje zdanie lub akapit („w pkt 1.4.1 w akapicie pierwszym zdanie pierwsze otrzymuje
brzmienie:", „akapity czwarty i piąty otrzymują brzmienie:"), cytat jest tekstem — a jednak szablon składa cytowane
zdania stylem części wspólnej (`Z/CZ_WSP_PKT`, `Z_TIR/CZ_WSP_LIT`…), a akapity tekstu załącznika bywają
wyliczeniem („1) Znak stosuje się …"). Eksport dawał wtedy część wspólną albo punkty z adresem, którego zmieniany akt
nie ma (`DU/2019/2311__pkt_1__czw`), choć ten sam dokument bez stylów dawał tekst. Zdanie jako przedmiot
([ADR-0057](0057-zdanie-przytoczone-w-calosci-jest-zdaniem.md)) działało tylko wtedy, gdy odczyt jednostek dał tekst. Odnośnik jako przedmiot
rozstrzygnięto tak samo — przedmiotem komendy ([ADR-0059](0059-odnosnik-jako-przedmiot-komendy-jest-cytowanym-odnosnikiem.md)).

## Decyzja

**Gdy przedmiotem komendy jest zdanie albo akapit — rzeczownik tuż przed „otrzymuje brzmienie:" albo
„w brzmieniu:", a między nimi same liczebniki porządkowe i spójniki — treść cytowana jest tekstem: akapity cytatu
w kolejności, bez odczytu jednostek (`QuotedTextSubject.Read`).**

- Cytowane zdanie, które biegnie przez kilka akapitów (zdanie z wyliczeniem „…: 1) …; 2) …"), zostaje odczytem
  jednostek: pozycje wyliczenia są jednostkami przepisu, który komenda zmienia (§ 56 ZTP).
- Tabela albo wzór w cytacie — zwykły odczyt jednostek.
- Przedmiot mieszany („akapit szósty i rysunek 6.3.2.10a") i miejscownik („w zdaniu pierwszym …") przedmiotu
  tekstowego nie tworzą.

## Odrzucone alternatywy

- **Styl rozstrzyga** (stan dotychczasowy). Styl mówi, jak cytat złożono, przedmiot komendy — co się zmienia;
  przy sporze stylu z treścią rozstrzyga treść.
- **Zdjąć styl i zostawić odczyt jednostek z treści.** Akapity tekstu załącznika zaczynające się „1)" stawały się
  punktami zmienianego aktu.
- **Każde cytowane zdanie jako tekst, także z wyliczeniem.** Pozycje wyliczenia straciłyby adresy jednostek.

## Konsekwencje

- W korpusie zmienia się 8 dokumentów (4 akty, każdy dwa razy): 100 cytatów wychodzi tekstem (`cytat-w-tekscie`) zamiast 122 cytatów strukturalnych z częścią wspólną, tiretem, punktem albo literą, których zmieniany akt nie ma. Zgodnych ze schematem nadal 2278 z 2321 (oba walidatory). Zgodność bez stylów: rozporządzenia 97,38 % → 97,76 % jednostek (w pełni zgodnych 1006 → 1012), ustawy w pełni zgodnych 210 → 211 — różnice znikają, bo mylił się przebieg ze stylami.
- Cel komendy w treści załącznika („w załączniku nr 1 … w pkt 1.4.1 …") nadal czyta się jak jednostki zmienianego
  aktu (`pkt_1`) — osobna luka w backlogu.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0079_SentenceOrParagraphAsTheSubject_MakesTheQuotedContentText_WhateverItsStyle`
- `QuotedTextSubjectTests` — przedmiot tekstowy i inny, zdanie w stylu części wspólnej, akapity z oznaczeniem,
  zdanie z wyliczeniem, eksport i zgodność ze schematem.
