# ADR-0082: Cytat zagnieżdżony jest nowelizacją cytowanego przepisu, nie dalszymi jednostkami cytatu zewnętrznego

- Status: Accepted
- Data: 2026-10-14

## Kontekst

Ustawa zmieniająca ustawę zmieniającą cytuje przepis, który sam jest komendą, a za nim jego własny cytat:
„1) w art. 39 pkt 4 otrzymuje brzmienie: „4) po art. 28a dodaje się art. 28b i art. 28c w brzmieniu: „Art. 28b. …”;”;".
Szablon składa taki cytat stylami `ZZ/…`. Zbieracz treści cytowanej zbierał oba poziomy razem, a budowa jednostek
traktowała je jak jeden cytat: artykuł 28b stawał przed punktem 4 jako jednostka cytatu zewnętrznego, z adresem
aktu zmieniającego (`DU/2023/1523__art_28b`), a komenda punktu 4 traciła swoją treść. Pozycje cytatu wewnętrznego
pod punktem wychodziły jako `nie-ztp-*` (litera bez wyliczenia, punkt pod artykułem). Backlog i roadmapa (kandydat 6)
zapowiadały przebudowę zbieracza na rekurencyjny — stos otwartych cytatów w maszynie stanów.

## Decyzja

**Cytat zagnieżdżony rozpoznaje się przy budowie treści cytowanej: akapit zaczynający się cudzysłowem tuż po akapicie
zakończonym komendą („… w brzmieniu:", „… otrzymuje brzmienie:") otwiera cytat zagnieżdżony, który trwa do domknięcia
tego cudzysłowu (`NestedQuotes.Split`). Jego akapity nie są jednostkami cytatu zewnętrznego, tylko treścią nowelizacji
cytowanego przepisu, który go wprowadza (`NestedAmendment.Build`): rodzaj zmiany i cel czyta się z tego przepisu
w kontekście jego cytowanych przodków, a akt zmieniany — z formuły cytowanego artykułu („W ustawie z dnia … (Dz. U.
z 2025 r. poz. 1483) wprowadza się …").**

- Budowa jest rekurencyjna: cytat zagnieżdżony może mieć własny.
- Eksport nie potrzebuje nowej drogi: nowelizacja cytowanej jednostki wychodzi w jej zdaniu, jak nowelizacja
  przepisu aktu — `cytat-strukt` w `cytat-strukt`.
- Akt zmieniany, którego cytat nie nazywa (punkt cytowanego artykułu, którego formuła stoi w akcie zmienianym, nie
  w dokumencie), zostaje nieznany: adres celu jest bez prefiksu aktu.
- Gdy przed cytatem zagnieżdżonym nie stoi cytowany przepis (cytat bez jednostki), jego akapity zostają akapitami
  cytatu zewnętrznego, jak dotąd.

## Odrzucone alternatywy

- **Zbieracz rekurencyjny w maszynie stanów** (stos otwartych cytatów w `AmendmentStateManager`). Zbieracz i tak zbiera
  wszystkie akapity do domknięcia cytatu zewnętrznego; granica cytatu wewnętrznego jest widoczna dopiero w zebranej
  treści, a jej właściciel — dopiero po zbudowaniu cytowanej jednostki. Przebudowa maszyny stanów nic tu nie wnosi.
- **Styl `ZZ/…` jako granica cytatu.** Ten sam cytat bez szablonu ma tylko cudzysłowy, a style bywają pomylone
  (ADR-0076, [ADR-0079](0079-zdanie-albo-akapit-jako-przedmiot-komendy-czyni-tresc-cytowana-tekstem.md)); o granicy mówi cudzysłów.
- **Cytat zagnieżdżony jako tekst cytowanego przepisu.** Traci jednostki — dodawany artykuł 28b ma w akcie zmienianym
  swoje ustępy i punkty.

## Konsekwencje

- W korpusie zmienia się 11 dokumentów — wszystkie 9 ze stylami `ZZ/` i 2 bez nich: 23 cytaty zagnieżdżone wychodzą w zdaniu cytowanego przepisu, 15 punktów i 10 liter przestaje być `nie-ztp-*`, cytowany rozdział dostaje nagłówek; żadna komenda w cytacie nie zostaje bez swojej treści. Zgodnych ze schematem nadal 2279 z 2321 (oba walidatory), pokrycie tekstu bez zmian.
- Zmiany w tekście w cytowanym przepisie („w art. 12 wyrazy „…” zastępuje się wyrazami „…”") zostają tekstem
  cytowanego przepisu — nie są instrukcjami (backlog).

## Weryfikacja

- `ArchitectureDecisionTests.Adr0082_NestedQuote_IsTheAmendmentOfTheQuotedProvision_NotUnitsOfTheOuterQuote`
- `NestedAmendmentTests` — podział cytatu, cytat jednoakapitowy, słowa w cudzysłowie, nowelizacja cytowanej jednostki,
  akt nazwany w cytacie, eksport i zgodność ze schematem.
