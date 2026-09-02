# ADR-0012: Nagłówek aktu

- Status: -
- Data: 2026-08-28

## Kontekst

Data ogłoszenia i publikator (dziennik, rok, pozycja) leżą w nagłówku pierwszej strony aktu
ogłoszonego — „DZIENNIK USTAW / Warszawa, dnia … / Poz. …". W DOCX ten nagłówek jest osobną częścią
pakietu (`word/header1.xml`), a `DocxBlockReader` iterował wyłącznie treść
(`mainPart.Document.Descendants<Paragraph>()`), więc dane nagłówka nie docierały do potoku w ogóle.

Nagłówek w tej formie niosą wyłącznie akty ogłoszone. Projekt ustawy i akt nieogłoszony lub inne
rodzaje aktów mogą nie mieć go wcale lub mieć nagłówek o innym układzie. Odczyt musiał więc być
kanałem opcjonalnym, którego nieobecność ani awaria nie dotykają parsowania treści.

## Decyzja

Nagłówek wchodzi **tą samą listą `DocumentBlock`** co treść, z rolą `BlockRole.JournalHeader`
i `BlockIndex = null`. Dwa filtry roli trzymają go poza aktem: `ParserOrchestrator.ProcessBlock`
(nie jest treścią jednostek redakcyjnych) i `DocumentClassifier.Normalize` (nie jest strefą tytułową
aktu). Konsumuje go `JournalHeaderParser` w przebiegu wstępnym `LegalDocumentParser.ParseBlocks`,
przed pętlą bloków — bo klauzula wejścia w życie liczy terminy względne od daty ogłoszenia.

## Konsekwencje

Każdy nowy konsument listy bloków musi pamiętać o filtrze roli. Dziś są dwa miejsca (orkiestrator i
klasyfikator). Następny konsument dodany bez filtra wciągnie tekst nagłówka po cichu.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0012_PageHeaderText_NeverBecomesActContent` — tekst nagłówka
  udający jednostkę redakcyjną nie buduje artykułu ani nie zmienia tytułu aktu.
- `ArchitectureDecisionTests.Adr0012_PageHeaderText_DoesNotInfluenceClassification` — wabik
  „w sprawie ogłoszenia jednolitego tekstu" w nagłówku nie przestawia rozpoznania rodzaju aktu
  ani numeracji bloków w sygnałach.
- `DocxBlockReaderTests.ReadBlocks_HeaderPrecedesBody_ButBodyIndexingIsUnchanged` — nagłówek idzie
  przed treścią, ale numeracja bloków treści zaczyna się od zera.
