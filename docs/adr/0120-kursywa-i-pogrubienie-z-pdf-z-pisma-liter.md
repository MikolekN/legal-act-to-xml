# ADR-0120: Kursywa i pogrubienie z PDF czyta się z pisma liter; indeksu dolnego z PDF nie ma

- Status: Accepted
- Data: 2026-10-27

## Kontekst

ADR-0117 i ADR-0118 przenoszą formatowanie znakowe z DOCX. Adapter PDF składa wiersze z liter (PdfPig), a każda litera
niesie opis fontu: flaga kursywy zgadza się z nazwą fontu na wszystkich 11,2 mln liter 171 aktów ISAP; flagi pogrubienia
PDF-y z LibreOffice (korpus `documents-pdf`) nie ustawiają, choć font nazywa się „…-BoldMT”. Litery obniżone względem
linii bazowej to prawie wyłącznie glify równań (Cambria Math), nie indeks dolny w tekście. Tekst wiersza przechodzi przez
przepisania (NFC, nawiasy kanału, znacznik odnośnika, nagłówek aktu zapisany zdaniowo — `WithTitleCase`), a wiersze
składają się w blok z dzieleniem wyrazów.

## Decyzja

- `PdfLineExtractor` zapisuje zakresy wiersza (`PdfTextLine.Spans`): kursywa z opisu fontu, pogrubienie z opisu albo
  z nazwy fontu (Bold, Black, Heavy, Semibold, Demi), grupy indeksu górnego kanału „[…]”; litery jednego formatu
  rozdzielone odstępem to jeden zakres.
- Przepisania wiersza i bloku przenoszą zakresy przez `SpanAlignment` — zmiana wielkości liter to ten sam znak, długi
  blok wyrównuje się zachłannie. Składanie bloku dopisuje zakresy wiersza w miejscu jego dopisania i skraca zakres na
  usuniętym dywizie przeniesienia; zakresy jednego formatu, które dzieli sam odstęp („de” / „minimis”), łączą się.
- Dalej jak w DOCX: rejestr źródeł, przebieg eksportu i reguła części akapitu (ADR-0118).
- Indeksu dolnego z PDF się nie odczytuje.

## Odrzucone alternatywy

- **Indeks dolny z położenia litery.** Prawie wszystkie obniżone glify należą do równań; ich tekst i tak jest
  spłaszczony, a `sub` na nich byłby szumem.
- **Pogrubienie tylko z flagi fontu.** W PDF z LibreOffice nie byłoby żadnego.

## Konsekwencje

- Zgodność z DOCX tych samych dokumentów (2165 PDF korpusu): kursywa — trafność 0,968, precyzja 0,684 (nadmiar w PDF to
  zmienne wzorów złożone kursywą w tekście i nakładające się warstwy tekstu), pogrubienie — 0,987 i 0,655.
  171 aktów ISAP: 1261 `i`, 1179 `b`; tekst bez zmian.
- Tekst złożony w poprzek strony (przebiegi obrócone) zakresów nie ma.

## Weryfikacja

- `PdfBlockReaderTests.ItalicAndBoldLetters_AreSpansOfTheBlock_EvenAcrossALineBreak`,
  `PdfBlockReaderTests.ItalicFromAPdf_StandsInIInTheExport`,
  `DocxBlockReaderTests.SpanAlignment_TreatsAChangeOfCaseAsTheSameCharacter`
