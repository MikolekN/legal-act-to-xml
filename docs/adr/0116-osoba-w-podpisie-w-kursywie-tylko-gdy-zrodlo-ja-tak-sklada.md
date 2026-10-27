# ADR-0116: Osoba w formule podpisu stoi w `i` tylko wtedy, gdy źródło składa ją kursywą

- Status: Accepted
- Data: 2026-10-27

## Kontekst

Przykłady autorów schematu zapisują formułę podpisu z osobą w kursywie: `<organ-podpisujacy>Prezydent Rzeczypospolitej
Polskiej: <i><osoba podpisujacy="true">…</osoba></i></organ-podpisujacy>` (DU/2024/928, DU/2026/26, DU/2026/252).
`organ-podpisujacy` ma treść mieszaną (`JednostkaTekstowaTyp`), więc `i` wokół `osoba` jest zgodne ze schematem. SAGA
nie przenosiła kursywy w tekście: czytnik DOCX znał tylko przeważającą kursywę wprost całego akapitu
(`BlockLayoutInfo.IsItalic`), bez stylów, a w formule podpisu kursywą jest sama osoba.

W korpusie DOCX wszystkie 2333 formuły podpisu mają osobę kursywą — 1692 przez styl znaku „Kkursywa” szablonu RCL,
641 kursywą wprost; „wz.” i „z up.” (583) oraz nazwa organu nigdy. W PDF Dziennika Ustaw osoba stoi pismem
kursywnym (TimesNewRomanPS-ItalicMT), „wz.” prostym. Użytkownik zastrzegł: `i` tam, gdzie zapis rzeczywiście jest
kursywą.

## Decyzja

- Czytnik podaje fragmenty bloku złożone kursywą (`BlockLayoutInfo.ItalicText`): DOCX — kolejne przebiegi w kursywie
  z dziedziczeniem stylu znaku i akapitu (`DocxItalics`, kursywa jako właściwość przełączana); PDF — kolejne litery
  pisma kursywnego (`Letter.Font.IsItalic`); TXT — nic.
- `ActSignatureParser` uznaje osobę za kursywę (`ActSignature.IsPersonItalic`), gdy jej inicjały i nazwisko stoją
  w całości w jednym fragmencie kursywy; `SignatureXmlConverter` stawia wtedy `i` wokół `osoba`. „wz.”/„z up.”
  zostają przed `i`, bo w źródłach nie są kursywą.

## Odrzucone alternatywy

- **`i` zawsze** (podpis aktu ogłoszonego jest kursywą z zasady wydawniczej). Zgodne z korpusem, ale eksport z TXT
  i z dokumentów spoza szablonu przypisywałby formatowanie, którego źródło nie pokazuje — wbrew zastrzeżeniu użytkownika.
- **Znaczniki kursywy w tekście** (w `DocumentBlock.Text`, jak indeks górny `[x]`). Każdy z ok. 300 wzorców
  rozpoznawania musiałby je omijać. Formatowanie w pozostałej treści (`sub`, `i`, `b`) to osobna decyzja; ta dotyczy
  formuły podpisu, w której osoba jest całym fragmentem kursywy i daje się wskazać po tekście.
- **Kursywa wprost bez stylów** (jak `IsItalic`). Pomija 1692 z 2333 podpisów korpusu (styl znaku „Kkursywa”).

## Konsekwencje

- W eksporcie korpusu DOCX `organ-podpisujacy` dostaje `i` wokół `osoba` wszędzie, gdzie źródło składa osobę kursywą;
  PDF — gdy pismo jest kursywne; TXT — nigdy.
- Kursywa stylów akapitu i znaku jest przełączana: oba style w kursywie dają pismo proste (ECMA-376 § 17.7.3).
- Model (`ActSignature.IsPersonItalic`) i lokalny golden `sygnalisci.snapshot.xml` niosą nową właściwość.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0116_SigningPersonStandsInItalics_OnlyWhenTheSourceSetsItInItalics`
- `ActSignatureTests.PersonSetInItalics_StandsInsideI_AsInTheExamplesOfTheSchemaAuthors`,
  `DocxBlockReaderTests.ItalicText_ResolvesCharacterAndParagraphStyles_AsAToggle`,
  `PdfBlockReaderTests.LettersInItalicType_AreTheItalicTextOfTheBlock`
