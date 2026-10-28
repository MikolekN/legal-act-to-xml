# ADR-0130: Rodzaj i organ aktu w eksporcie mają stałą wielkość liter, niezależną od zapisu w źródle

- Status: Accepted
- Data: 2026-10-28

## Kontekst

Wiersze rodzaju aktu i organu wydającego składa styl (`OZN_RODZ_AKTU…`) wersalikami, więc w DOCX litery wpisane
pod nim są dowolne i niewidoczne: DU/2026/26 ma „Ustawa”, DU/2026/252 — „ustawa”, a w korpusie są też „USTAWA”,
„RozporządzeniE” i „zaRZĄDZENIE nr 53”. Model trzyma te wiersze tak, jak je zapisano (`LegalDocument.KindLine`,
`IssuerLine`; odczyt celowo nie stosuje wersalików ze stylu), a eksport przepisywał je wprost. Skutek: `rodzaj-aktu`
w pięciu zapisach, `Tytul` zaczynający się od „ustawa” albo „ROZPORZĄDZENIE MINISTRA FINANSÓW”, `autor-aktu`
wersalikami. Przykłady autorów schematu piszą „Ustawa” w `rodzaj-aktu` i „Ustawa z dnia …” w `Tytul`; ISAP —
„Rozporządzenie Ministra Finansów z dnia …”.

## Decyzja

- Eksport normalizuje wielkość liter (`ActHeadingText`); model zostaje przy zapisie ze źródła.
- Rodzaj aktu (`rodzaj-aktu`, też aktu ogłaszanego w obwieszczeniu): małe litery z wielką pierwszą — „Ustawa”,
  „Rozporządzenie”, „Zarządzenie nr 272”; liczba rzymska oznaczenia zostaje („Uchwała nr XII/123/2025”).
- Organ (`autor-aktu` i organ w tytule): każde słowo wielką literą poza spójnikami, przyimkami i „spraw” po „do”
  („Ministra Spraw Wewnętrznych i Administracji”, „Komitetu do spraw Pożytku Publicznego”), jak w tytułach ISAP;
  słowo wersalikami bez samogłoski to skrót i zostaje („Zarządu NBP”; „Rozwoju Wsi” — jak w ISAP). Dotyczy też wierszy wpisanych
  mieszanie („MINISTRA FINANSÓW i gospodarki”, „PRezesa RAdy Ministrów” — w korpusie kilkanaście): pod wersalikami
  stylu takie litery są tak samo przypadkowe.
- `Tytul` w metadanych: część przed datą aktu to rodzaj i organ (jak wyżej), reszta zostaje.

## Odrzucone alternatywy

- **Zapis ze źródła** (dotychczas). Wielkość liter pod stylem wersalików nie niesie informacji, a ten sam rodzaj
  wychodził w pięciu postaciach.
- **Wersaliki, jak w druku** („USTAWA”). Zgodne z wyglądem aktu, ale niezgodne z przykładami autorów schematu
  i z tytułami w ISAP.
- **Normalizacja przy odczycie.** Zmieniłaby model, z którego korzystają rozpoznawanie rodzaju i organu
  (`ZtpPatterns`, `ActKindXml`) — to decyzja o zapisie w eksporcie, nie o treści aktu.

## Konsekwencje

- Organ z nietypowym zapisem słowa (skrót z samogłoską jak „ZUS”, słowo, które ISAP pisze małą literą, a lista
  go nie zna) wyjdzie wielkimi literami słów; listę słów pisanych małą literą trzyma `ActHeadingText`.
- Sprawdzone z ISAP: początek `Tytul` (rodzaj i organ) w 56 aktach korpusu o różnych nagłówkach zgadza się z tytułem
  w API ELI co do znaku.
- Pole `Nazwa` organu w metadanych (`Organy`) buduje osobno `AuthoritiesXmlConverter` (nazwa w mianowniku) i tej
  zmiany nie dotyczy.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0130_KindAndIssuer_AreWrittenLikeInTheExamples_WhateverTheTypedCase`
