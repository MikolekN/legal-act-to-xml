# ADR-0123: Instrukcje nowelizacyjne `now:` stoją przed tekstem zdania

- Status: Accepted — Supersedes [ADR-0052](0052-zmiana-wyrazow-jest-instrukcja-zamien-tekst.md) w części o położeniu `now:uchyl`
- Data: 2026-10-27

## Kontekst

Instrukcje nowelizacyjne (`now:zamien-tekst`, `now:dodaj-ustep`, `now:dodaj-paragraf`, `now:zastap`, `now:dodaj-po`)
stały przed tekstem zdania, które je zapisuje, a `now:uchyl` — za nim. Przykłady autorów schematu nie są zgodne:
DU/2026/26 stawia uchylenia przed tekstem (7 z 7), DU/2026/252 — najczęściej za nim (36 z 38), a inne przykłady
także przy samym zwrocie komendy. Schemat kolejności w zdaniu nie ustala. Zapytany autor schematu odpowiedział
(2026-10-27), że skłaniałby się do instrukcji przed tekstem, choć położenie chyba nie ma znaczenia.

## Decyzja

- Każda instrukcja `now:` stoi przed tekstem zdania, które ją zapisuje — także `now:uchyl`
  (`AmendmentXmlConverter.AddRepealInstructions`). Kilka uchyleń jednego zdania idzie w kolejności celów (jedna lista,
  ADR-0016); w zdaniu łączącym zmiany przed tekstem stoją kolejno `now:zamien-tekst`, `now:uchyl`, instrukcje
  załącznikowe, `now:dodaj-ustep`/`now:dodaj-paragraf`.
- Cytaty (`cytat-strukt`, `cytat-w-tekscie`, `tresc-strukt`) i cytaty instrukcji ręcznych (`*-recznie`, ADR-0100)
  zostają w toku zdania, za tekstem, który je zapowiada.

## Odrzucone alternatywy

- **`now:uchyl` za tekstem** (dotychczas). Dwa położenia dla jednej grupy elementów schematu, bez żadnego powodu po
  odpowiedzi autora.
- **Instrukcja przy własnym zwrocie komendy**, jak w ręcznie pisanych przykładach. Wymaga cięcia tekstu zdania na
  kilka węzłów, a przebiegi eksportu pracujące na jednym węźle tekstu (sankcja karna, indeks górny, formatowanie
  znakowe) by na tym traciły; model nie zna położenia komendy w zdaniu.
- **Przeniesienie także cytatów `*-recznie` przed tekst.** Niosą widoczny tekst, który odnajduje w źródle przebieg
  formatowania (`InlineFormattingXml`) i reguła pierwszego znacznika odnośnika — ich miejsce jest w toku zdania.

## Konsekwencje

- Korpus DOCX: 936 `now:uchyl` w 242 plikach zmienia położenie (w tym 53 w treści przytoczonej); 171 aktów ISAP: 122
  w 24 aktach. Tekst zdań, liczba elementów i zgodność ze schematem bez zmian.
- Instrukcja stoi przed całym zdaniem, także przed formułą wprowadzającą akt zmieniany („W ustawie … uchyla się
  ust. 3.”), a nie przy swoim zwrocie.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0123_AmendmentInstructions_StandBeforeTheSentenceText_InTheOrderOfTheirTargets`
- `AmendmentTargetsExportTests.RepealedUnit_IsAnInstructionBeforeTheSentenceText`,
  `AmendmentTargetsExportTests.ReplacedWords_AreAnInstructionBeforeTheSentenceText_NotACitation`
