# ADR-0034: W akcie opartym na paragrafach „§" jest jednostką podstawową — tą samą encją i tym samym poziomem adresu co artykuł

- Status: Accepted
- Data: 2026-10-11

## Kontekst

Rozporządzenie, uchwała i zarządzenie dzielą się na paragrafy (§ 124 ZTP): „§ 5. 1. …" jest tam tym, czym
„Art. 5. 1. …" w ustawie — jednostką podstawową z ustępami, punktami i literami pod sobą. W ustawie ten sam
znak oznacza co innego: paragraf kodeksu, jednostkę **pod** artykułem („Art. 1. § 1. …").

Potok znał tylko drugie znaczenie. „§ 1." na początku akapitu był ustępem z rolą paragrafu kodeksu, a ustęp
poza artykułem jest odrzucany — 1061 z 1067 rozporządzeń korpusu nie miało ani jednej jednostki redakcyjnej,
choć klasyfikator dokumentu rozpoznawał je poprawnie.

Część pracy była już rozstrzygnięta: `SectionSignRole.IsBasicUnit` mówi z rodzaju aktu, czy samodzielny „§"
jest jednostką główną, a powołania jednostek ([ADR-0015](0015-jedno-zrodlo-powolan-jednostek.md)) adresują
ją wtedy segmentem `art` („§ 1 pkt 3" w rozporządzeniu → `art_1__pkt_3`).

Schemat ma dla tych aktów korzenie `rozporzadzenie`, `uchwala`, `zarzadzenie` (wszystkie typu
`RozporzadzenieTyp`) z `autor-aktu`, `podstawa-prawna` i jednostką `paragraf` o tej samej budowie co artykuł.

## Decyzja

**Gdy rodzaj aktu wskazuje akt oparty na paragrafach, akapit zaczynający się od „§ N." jest jednostką
podstawową: encją `Article` z `IsSection = true`, budowaną tym samym potokiem co artykuł.**

- Rozstrzyga rodzaj aktu z klasyfikatora dokumentu (`SectionSignRole.IsBasicUnit` →
  `ClassificationInput.SectionIsBasicUnit`), nie sam znak. W ustawie „§" zostaje paragrafem kodeksu.
- Adres jednostki ma segment `art` (`art_5__ust_2__pkt_1`) — ten sam, którym powołania jednostek i cele
  wejścia w życie wskazują jednostkę podstawową tego aktu.
- W eksporcie jednostka to `paragraf`, a korzeń wynika z rodzaju aktu (`ActKindXml.RootElement`): `ROZP` →
  `rozporzadzenie`, `UCHW` → `uchwala`, `ZARZĄD` → `zarzadzenie`. Strefa tytułowa niesie `autor-aktu`
  (`LegalDocument.IssuerLine`) i `podstawa-prawna` (`LegalDocument.LegalBasis`, § 121 ZTP).
- Korzeń aktu opartego na paragrafach dostaje tylko dokument, w którym takie jednostki są; bez nich zostaje
  dotychczasowy.

## Odrzucone alternatywy

- **Osobna encja i osobny budowniczy paragrafu rozporządzenia.** Druga kopia logiki artykułu — pierwszy ustęp
  z ogona oznaczenia, ustęp niejawny, wyliczenia, części wspólne, nowelizacje, jednostki systematyzacyjne —
  która rozjechałaby się z pierwszą przy każdej zmianie. Różnica między artykułem a paragrafem aktu
  wykonawczego to oznaczenie i nazwa elementu, nie budowa.
- **Rozpoznanie po samym znaku** („§ N." poza artykułem to jednostka podstawowa). W kodeksie i w treści
  cytowanej „§" bez artykułu przed sobą jest paragrafem kodeksu; o znaczeniu znaku mówi rodzaj aktu, który
  klasyfikator zna z całego dokumentu.
- **Segment `par` w adresie** (`par_5__ust_2`), zgodnie z brzmieniem tabeli [ADR-0014](0014-skroty-jednostek-w-eid.md)
  „paragraf → `par`". Ta pozycja tabeli opisuje paragraf kodeksu — jednostkę pod artykułem (`art_1__par_2`).
  Jednostka podstawowa rozporządzenia stoi w hierarchii tam, gdzie artykuł, a powołania jednostek (ADR-0015)
  i `reference-schema.md` adresują ją już jako `art`; drugi skrót dla tego samego poziomu rozdzieliłby adres
  jednostki i adres jej powołania. Autorzy schematu nie dali przykładu rozporządzenia — pytanie do nich jest
  w backlogu, a skrót nadaje jedno miejsce (`Article.EIdPrefix`), więc zmiana byłaby lokalna.
- **Zamiana „§" na „Art." przy wejściu.** Najmniej kodu, ale akt wychodziłby z oznaczeniem, którego nie ma
  w tekście, a „§" w treści cytowanej wymagałby wyjątków.

## Konsekwencje

- 1067 rozporządzeń korpusu przestaje być pustych. Uchwały i zarządzenia idą tą samą drogą.
- Treść cytowana w rozporządzeniu zmieniającym („§ 5 otrzymuje brzmienie: „§ 5. …"") jest nadal czytana jak
  paragraf kodeksu — rodzaj aktu **zmienianego** nie jest jeszcze przekazywany do budowniczego nowelizacji
  (backlog).
- Tekst jednolity rozporządzenia zostaje przy dotychczasowym korzeniu, dopóki obwieszczenie nie osadza aktu
  innego niż ustawa ([ADR-0031](0031-tekst-jednolity-pod-korzeniem-obwieszczenie.md); backlog).
- `Article.IsSection` trzeba uwzględniać wszędzie, gdzie nazwa jednostki trafia do tekstu albo eksportu
  (dziś: `ArticleXmlConverter`).

## Weryfikacja

- `ArchitectureDecisionTests.Adr0034_SectionSign_IsTheBasicUnit_OnlyInASectionBasedAct`.
- `ArchitectureDecisionTests.Adr0015_BareSectionSign_IsDecidedByActType` — powołania jednostek tym samym
  segmentem.
- `RegulationTests` — jednostki, ustępy i punkty, nagłówek z organem i podstawą prawną, paragraf kodeksu
  w ustawie, korzeń `rozporzadzenie`/`uchwala`, zgodność treści i całego dokumentu ze schematem.
