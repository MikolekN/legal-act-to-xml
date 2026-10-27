# ADR-0117: Formatowanie znakowe idzie obok tekstu bloku, a eksport odnajduje je w źródle

- Status: Accepted
- Data: 2026-10-27

## Kontekst

Grupa `zawartosc-tekstowa-bez-cytatu` schematu dopuszcza w treści formatowanie znakowe: `b`, `i`, `sub`, `sup`,
`wersaliki`, `rozstrzelone`, `petit`. Czytniki spłaszczały akapit do napisu — przetrwał tylko indeks górny (kanał „[x]”,
potrzebny w oznaczeniach jednostek) i wersaliki nadane wprost (jako wielkie litery). W korpusie DOCX tracona była treść:
indeks dolny w 71 dokumentach (ok. 2,9 tys. znaków: „CO2”, zmienne „PIi”, `symbol-txt` „Cs”), kursywa poza podpisem
w ok. 300 dokumentach (łacina, nazwy gatunków, zmienne wzorów, kursywa § 108a i § 108b ZTP w tekstach jednolitych).
Użytkownik zdecydował (2026-10-27): przenosić `sub`, `i` i `b` tam, gdzie źródło rzeczywiście je ma. Autorzy schematu
przenoszą z Worda tylko indeks górny, a kursywę — tylko wokół osoby podpisującej (ADR-0116); rozbieżność jest
w pytaniach 35 i 36 do autorów.

Tekst bloku czyta ok. 300 wzorców rozpoznawania i dziesiątki operacji na znakach (oznaczenia, komendy nowelizacji,
powołania jednostek, podział na zdania); model trzyma treść jednostek jako płaskie napisy (`ContentText`).

## Decyzja

- Czytnik DOCX zapisuje formatowanie jako zakresy obok niezmienionego tekstu bloku (`DocumentBlock.Spans`,
  `InlineSpan`): kursywa, pogrubienie, indeks dolny i grupy indeksu górnego, z rozróżnieniem formatu ze stylu akapitu
  (`InlineSpan.FromParagraphStyle`) od formatu przebiegu. Format przebiegu rozwiązuje `DocxRunFormat` (jak dla
  kursywy osoby podpisującej w ADR-0116): formatowanie wprost, styl znaku i styl akapitu; kursywa i pogrubienie
  przełączane.
  Zakresy zapisuje `GetFullText` tam, gdzie dopisuje tekst przebiegu; przez przepisanie numerów odnośników przenosi
  je `SpanAlignment`.
- Parser tekstu nie widzi formatowania. `LegalDocument.FormattedSources` trzyma tekst i zakresy wszystkich bloków
  treści — rejestr dla eksportu, poza modelem kanonicznym.
- Przebieg eksportu `InlineFormattingXml.Embed` (przed odnośnikami i indeksem górnym) odnajduje tekst każdego węzła
  treści w tekście źródła — bez odstępów i wielkości liter, w kolejności dokumentu (krótki tekst tylko tuż za
  poprzednim; poza kolejnością tylko jednoznaczny; kopia wyrazów zdania w `cytat-w-tekscie` — najpierw w tekście, który
  zdanie właśnie zużyło) — i obejmuje zakresy elementami `b`, `i`, `sub`. Wartość tekstowa węzła się nie zmienia;
  tekst nieodnaleziony zostaje bez formatowania. Indeks górny za formatowaniem („x<sub>1</sub>[2]”) wzorzec kanału
  rozpoznaje po znaku z poprzedniego węzła.
- Format, który cały akapit ma ze swojego stylu (tytuł, nagłówek), jest typografią rodzaju akapitu i do treści nie
  wchodzi. Atrybuty (`now:stary-tekst`, `now:nowy-tekst` — `xs:token`) zostają bez formatowania.
- Elementy włącza się po kolei, każdy z pomiarem korpusu (`InlineFormattingXml.Exported`): najpierw `sub`.

## Odrzucone alternatywy

- **Znaczniki formatowania w tekście bloku** (jak kanał „[x]”). Każdy wzorzec i każda operacja na znakach musiałyby je
  omijać; samo pogrubienie stoi na ok. 64 tys. oznaczeń jednostek („Art. 5.”) czytanych przez klasyfikator.
- **Zakresy przenoszone przez cały parser** (do `ContentText` każdej jednostki). Kilkadziesiąt miejsc składania
  tekstu i zmiana każdej encji modelu; przeoczone przemapowanie przesuwałoby formatowanie po cichu.
- **Fragmenty kursywy dopasowywane po samym tekście** (`BlockLayoutInfo.ItalicText` z ADR-0116). Krótki fragment
  („2” w „CO2”) jest niejednoznaczny bez miejsca w dokumencie; zostaje tylko dla formuły podpisu.
- **Indeks bloku źródła przy każdej encji.** Dokładne, ale wymaga zmian we wszystkich miejscach tworzenia treści;
  zapas na wypadek, gdyby odnajdywanie po tekście zostawiało za dużo nieumieszczonych zakresów.

## Konsekwencje

- Rozpoznawanie i model treści się nie zmieniają; eksport zyskuje elementy formatowania wewnątrz treści, a przebiegi
  odnośników i indeksu górnego schodzą przez nie (`InlineTextXml.Host`, `InlineTextXml.Texts`).
- Tekst przekształcony między źródłem a eksportem (tytuł w zapisie zdaniowym, wyrazy z komend nowelizacji) nie jest
  odnajdywany i zostaje bez formatowania.
- PDF i TXT zakresów jeszcze nie mają; treść odnośników też nie (osobne kroki).

## Weryfikacja

- `ArchitectureDecisionTests.Adr0117_InlineFormatting_TravelsBesideTheText_AndEndsInTheExport`
- `InlineFormattingTests`, `DocxBlockReaderTests.Spans_RecordInlineFormattingNextToTheText`,
  `DocxBlockReaderTests.Spans_MarkFormattingThatTheWholeParagraphHasFromItsStyle`
