# ADR-0072: Wersja tekstu jednolitego obowiązuje od ujednolicenia, a dzień stanu prawnego bierze się tylko z obwieszczenia

- Status: Accepted
- Data: 2026-10-14

## Kontekst

Metadane schematu mają dla tekstu jednolitego dwa elementy, których eksport nie wypełniał:
`DataObowiazywaniaWersji` („daty, w których obowiązywała wersja") z odwołaniem `Od` do zdarzenia z `DaneCzasowe`
oraz `DataStanuPrawnegoTJ` („data stanu prawnego dla tekstu jednolitego"). Przykład autorów schematu
(`DU20261261_obw_tj.xml`) ma oba: `Od` wskazuje zdarzenie `ujednolicenie`, a `DataStanuPrawnegoTJ` to dzień
z punktu 1 obwieszczenia — „…, z uwzględnieniem stanu prawnego na dzień 25 sierpnia 2026 r. oraz zmian wprowadzonych:".

ZTP: tekst jednolity ustawy sporządza się według stanu prawnego z dnia określonego w obwieszczeniu, aktu innego niż
ustawa — z dnia wydania obwieszczenia (§ 100a); wzór obwieszczenia podaje ten dzień w punkcie 1 (§ 104). Obwieszczenia
korpusu (2023–2025) są według wcześniejszego wzoru i dnia stanu prawnego nie podają.

## Decyzja

- **Wersja tekstu jednolitego obowiązuje od zdarzenia ujednolicenia** ([ADR-0033](0033-tekst-jednolity-ma-zdarzenie-ujednolicenia.md)):
  `DataObowiazywaniaWersji/Od` wskazuje to zdarzenie (`TemporalDataXmlConverter.VersionValidity`).
- **Dzień stanu prawnego to dzień podany w obwieszczeniu** („stanu prawnego na dzień …"), czytany z treści
  obwieszczenia do `Announcement.LegalStateDate` (`ConsolidatedTextState`) i eksportowany jako `DataStanuPrawnegoTJ`.
  Obwieszczenie, które go nie podaje (albo podaje wykropkowany), nie daje tego elementu.

## Odrzucone alternatywy

- **Dzień stanu prawnego z daty obwieszczenia, gdy go nie podano.** Dla aktu innego niż ustawa § 100a ZTP tak
  stanowi, ale to brzmienie z nowelizacji ZTP z 2026 r. — reguły, według których sporządzono starsze obwieszczenia
  korpusu, w repozytorium nie ma. Element wywiedziony z reguły, której nie da się sprawdzić, byłby zgadywaniem.
- **`Od` wskazujące zdarzenie wejścia w życie ogłaszanego aktu.** Tekst jednolity nie wchodzi w życie — jest
  ujednoliceniem (ADR-0033); jego wersja zaczyna się z obwieszczeniem.

## Konsekwencje

- W korpusie wszystkie 728 tekstów jednolitych dostaje `DataObowiazywaniaWersji` ze zdarzeniem ujednolicenia; `DataStanuPrawnegoTJ` — żaden (obwieszczenia według wcześniejszego wzoru, 2 z dniem wykropkowanym). Poza tym eksport się nie zmienia; zgodnych ze schematem jest nadal 1946 z 2321 (oba walidatory).
- Dla tekstów jednolitych aktów innych niż ustawa dzień stanu prawnego wynika z § 100a pkt 2 ZTP (dzień wydania
  obwieszczenia) — do wypełnienia, gdy będzie wiadomo, od kiedy ta reguła obowiązuje (`docs/backlog.md`).

## Weryfikacja

- `ArchitectureDecisionTests.Adr0072_ConsolidatedText_VersionRunsFromTheConsolidation_AndTheLegalStateIsTheStatedDay`
- `AnnouncementTests` — `Od` przy zdarzeniu ujednolicenia, dzień stanu prawnego podany, wykropkowany i niepodany,
  zgodność ze schematem.
