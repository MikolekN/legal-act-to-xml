# ADR-0049: Cytat niedomknięty do końca dokumentu kończy się przed kolejnym artykułem aktu zmieniającego — wskazuje go ponowny przebieg

- Status: Accepted
- Data: 2026-10-12

## Kontekst

Bez stylów granicę treści cytowanej wyznacza bilans cudzysłowów (`QuoteBalanceTracker`). Gdy cudzysłów
otwierający jest, a zamykającego brakuje (zgubiony w źródle, przy rozpoznaniu pisma albo konwersji), bilans
nie domyka się nigdy: cytat trwa do końca dokumentu, a dalsze artykuły aktu zmieniającego — z przepisem
o wejściu w życie i podpisem — stają się treścią cytowaną. Jeden zgubiony znak zabiera resztę aktu.

[ADR-0048](0048-tresc-cytowana-bez-cudzyslowu-konczy-kolejny-artykul-aktu-zmieniajacego.md) kończy treść przed kolejnym artykułem aktu zmieniającego tylko wtedy, gdy
cudzysłowu nie da się śledzić. W cytacie otwartym tej reguły zastosować w biegu nie wolno: cytat kilku
artykułów („art. 1 i 2 otrzymują brzmienie:") prawidłowo zawiera artykuł o numerze kolejnym względem artykułu
z komendą. Czy cytat się domknie, wiadomo dopiero na końcu dokumentu.

## Decyzja

**Dokument, który kończy się w otwartym cytacie, buduje się ponownie — z granicą tego cytatu wskazaną z góry.**

- W czasie przebiegu `OpenQuotationTrail` odnotowuje akapity stojące w otwartym cytacie, które mogą być dalszym
  ciągiem aktu zmieniającego: oznaczone jak artykuł i bez własnego cudzysłowu otwierającego (z numerem
  i głębokością cudzysłowów) oraz formułę podpisu.
- Gdy na końcu dokumentu cytat jest nadal otwarty (`ParserOrchestrator.Finalize`), `OpenQuotationTrail.FindBreak`
  wskazuje granicę: **pierwszy artykuł o numerze kolejnym względem artykułu z komendą, po którym numeracja aktu
  zmieniającego biegnie dalej** — następny artykuł na tej samej albo płytszej głębokości cudzysłowów ma numer
  o jeden większy albo nie ma go wcale. Ten sam numer w cytacie kilku artykułów tego nie spełnia: stoi po nim
  jeszcze raz ten sam numer (cytat przed granicą) albo numer dalszy niż kolejny (cytat za granicą). Gdy żaden
  kandydat warunku nie spełnia, granicą jest pierwszy; gdy kandydatów nie ma — formuła podpisu.
- `LegalDocumentParser.ParseBlocks` buduje dokument jeszcze raz z tym akapitem w `ParsingContext.QuotationBreaks`;
  cytat kończy się przed nim mimo otwartego bilansu. Każdy przebieg dokłada jedną nową granicę albo jest
  ostatni, więc dwa niedomknięte cytaty to trzy przebiegi.
- Jednostka z komendą zachowuje ostrzeżenie o niedomkniętym cytacie — granica jest odtworzona, nie odczytana.

## Odrzucone alternatywy

- **Uciąć cytat w biegu, na pierwszym artykule o numerze kolejnym.** Psuje prawidłowy cytat kilku artykułów,
  który ten numer zawiera (odrzucone już w ADR-0048).
- **Buforować bloki i przetworzyć ogon ponownie w tym samym przebiegu.** Stan parsera jest mutowalny
  i rozproszony (kontekst, kolektor, wskazówki numeracji, odnośniki, tabele); cofnięcie go wymagałoby migawki
  całości. Ponowny przebieg to ten sam potok z jedną daną więcej.
- **Przebieg wstępny, który znajduje niedomknięte cytaty przed parsowaniem.** Musiałby powtórzyć wykrywanie
  komend i uzbrajanie bilansu, czyli stać się drugą, rozjeżdżającą się kopią potoku.
- **Pierwszy albo ostatni kandydat bez sprawdzania numeracji.** Pierwszy myli się, gdy numer kolejny stoi
  w niedomkniętym cytacie; ostatni — gdy stoi w cytacie późniejszej zmiany.

## Konsekwencje

- Koszt ponosi tylko dokument, który kończy się w otwartym cytacie; pozostałe mają jeden przebieg jak dotąd.
  Z obserwatorem (`observeBlock`, narzędzia diagnostyczne) dokument buduje się zawsze dwa razy: obserwator ma
  widzieć wyłącznie przebieg, z którego powstaje wynik.
- W korpusie ze stylami bez zmian (0 z 2321 dokumentów). Bez stylów jedno rozporządzenie (dwa pliki) odzyskuje końcowy „§ 2"; cytat nie domykał się tam, bo cudzysłów zamykający cytowaną tabelę stoi w jej ostatniej komórce, a tabela nie bierze udziału w bilansie cudzysłowów (zgodność struktury rozporządzeń 97,31 % → 97,32 %).
- Ostrzeżenia Serilog z odrzuconego przebiegu trafiają do logu obok ostrzeżeń przebiegu ostatecznego.
- Zostaje przypadek nierozstrzygalny: ostatni artykuł aktu zmieniającego cytuje kilka artykułów z numerem
  kolejnym, bilans cudzysłowów jest zepsuty i żaden artykuł aktu już po nim nie stoi — artykuł cytowany zostanie
  wtedy wzięty za artykuł aktu.
- Nie dotyczy cytatu, w którym pojawia się styl: bilans cudzysłowów jest wtedy rozbrojony i rządzą style.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0049_QuotationLeftOpenToTheEndOfTheDocument_EndsBeforeTheActsNextArticle`
- `UnclosedQuotationTests` — granica przed kolejnym artykułem, numer kolejny w cytacie niedomkniętym
  i w późniejszym, dwa niedomknięte cytaty, podpis, rozporządzenie („§"), cytat domknięty bez zmian, obserwator
  widzi jeden przebieg, `OpenQuotationTrail.FindBreak`.
