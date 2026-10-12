# ADR-0052: Zmiana wyrazów jest instrukcją `now:zamien-tekst`, nie cytatem

- Status: Accepted (sprostowanie do konsekwencji: położenie instrukcji w zdaniu nie jest w przykładach ustalone — DU/2026/252 stawia je najczęściej za tekstem zdania; skreślenie wyrazów i dalsze postaci: [ADR-0053](0053-skreslenie-wyrazow-to-zamiana-na-pusty-tekst.md), [ADR-0054](0054-zamiana-znaku-konczacego-jednostke-ma-wlasny-cel.md))
- Data: 2026-10-12

## Kontekst

Komenda „wyrazy „X" zastępuje się wyrazami „Y"" (§ 87–88 ZTP) zmienia tekst jednostki, nie przytaczając jej
brzmienia. Wychodziła jako `cytat-w-tekscie` z funkcją `zastap-tekst` i samymi nowymi wyrazami: wyrazy
zastępowane gubił już model (`AmendmentContent.PlainText` niosło tylko nowe), więc odbiorca nie mógł zastosować
zmiany bez ponownego czytania tekstu zdania.

Schemat ma na to instrukcję `now:zamien-tekst` (`stary-tekst`, `nowy-tekst`) — „zastąpienie pewnego tekstu przez
inny, gdy w tekście zmieniającym nie został użyty cytat-strukt" — a przykłady autorów schematu używają jej
dla zamiany wyrazów, dodania wyrazów po wskazanych i zamiany znaku interpunkcyjnego (DU/2026/26: 9 instrukcji).
W korpusie jest ok. 2500 komend zmieniających tekst (zamiana wyrazów 1215, dodanie po wyrazach 504, skreślenie
349, zamiana znaku 454); ok. 1300 z nich nie dawało żadnej instrukcji.

## Decyzja

- **Model:** zmiana w tekście to `TextChange` (`OldText`, `NewText`) na liście `Amendment.TextChanges`.
  Nowelizacja będąca samą zmianą wyrazów nie ma treści cytowanej (`AmendmentStateManager.HandleReplaceWords`).
- **Eksport:** każda zmiana wychodzi jako `now:zamien-tekst` — po jednej na każdy cel komendy
  ([ADR-0016](0016-wyliczenie-celow-to-osobne-komendy.md)) — przed tekstem zdania, które zostaje w całości,
  z cudzysłowami (`AmendmentXmlConverter.AddTextChangeInstructions`).
- Ta decyzja obejmuje postać „wyrazy „X" zastępuje się wyrazami „Y""; pozostałe postaci zmian w tekście mają
  korzystać z tego samego modelu.

## Odrzucone alternatywy

- **Zostać przy `cytat-w-tekscie` i dołożyć wyrazy zastępowane.** Cytat nie ma na nie miejsca; schemat
  i przykłady wskazują instrukcję.
- **Zostawić nowe wyrazy także w `PlainText`.** Dwa źródła tej samej informacji, a eksport musiałby zgadywać,
  czy tekst jest cytatem, czy powtórzeniem zmiany.
- **Osobna nowelizacja na każdą zmianę.** Jednostka niesie jedną nowelizację (`IHasAmendments`), a jedno zdanie
  łączy zmianę w tekście ze zmianą strukturalną („kropkę zastępuje się średnikiem i dodaje się pkt 21
  w brzmieniu:") — lista zmian na nowelizacji to mieści.

## Konsekwencje

- W korpusie 1481 cytatów `cytat-w-tekscie` w 307 plikach staje się instrukcjami `now:zamien-tekst`; poza tym eksport się nie zmienia, a zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory). W przykładach autorów schematu każda nasza para (`stary-tekst`, `nowy-tekst`) ma odpowiednik: DU/2026/26 — 5 z 5 (przykład ma 9 instrukcji), DU/2026/252 — 35 z 35 różnych par (przykład ma 83 instrukcje); brakujące to postaci jeszcze nieobsługiwane. Indeks górny w wyrazach stoi w atrybucie zapisem kanonicznym („art. 406[1]"), bo atrybut nie niesie znaczników (5 w korpusie).
- `now:uchyl` nadal stoi za tekstem zdania, a `now:zamien-tekst` przed nim — przykłady stawiają przed tekstem
  obie instrukcje.
- Zostają postaci wypisane w `backlog.md`: dodanie wyrazów po albo przed wskazanymi, skreślenie wyrazów, zamiana
  znaku interpunkcyjnego (`now:wystapienie`), kilka zmian w jednym zdaniu i zmiana wyrazów w komendzie złożonej
  ze zmianą strukturalną.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0052_WordReplacement_IsAnInstructionWithBothWordings`
- `AmendmentTargetsExportTests` — instrukcja przed tekstem zdania, po jednej na cel, cel odziedziczony z komendy
  nadrzędnej; `AmendmentTextualBoundaryTests` — model zmiany; `ParserEquivalenceTests` — DOCX i TXT dają tę samą
  zmianę; `ActSchemaTests` — zgodność ze schematem.
