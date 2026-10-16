# ADR-0093: Tabela w TXT to wiersze, w których tabulator rozdziela komórki, a nie stoi za oznaczeniem

- Status: Accepted
- Data: 2026-10-16

## Kontekst

Eksport tekstu LibreOffice zapisuje wiersz tabeli jednym wierszem pliku, z komórkami rozdzielonymi tabulatorem
(„A 01⇥Naruszenie …⇥art. 173⇥15"). Tym samym tabulatorem LibreOffice oddziela jednak oznaczenie jednostki od tekstu
(„1)⇥charakteryzuje …"), wyrównuje wartości w wierszu tekstu („a)⇥kaprala Służby Więziennej⇥⇥– 2 lata,") i zapisuje
legendę wzoru („OpłWyr⇥–⇥opłatę wyrównawczą …"). Dalsze akapity komórki stoją w kolejnych wierszach pliku, a granic
tabeli plik nie ma. Bez rozpoznania tabel wyliczenia w komórkach stawały się jednostkami: w 285 dokumentach z tabelami
TXT miał 2446 jednostek nadmiarowych wobec DOCX.

## Decyzja

**Wiersz pliku jest wierszem tabeli, gdy po złączeniu oznaczeń jednostek z ich tekstem zostają co najmniej dwie
komórki. Kolejne pozycje wyliczenia należą do ostatniej komórki, dopóki ciągną jej numerację. Czytnik TXT oddaje
komórki blokami z `TableCell`, jak DOCX (`PlainTextTables`).**

- Tabulator za oznaczeniem („1)", „a)", „–", „II.", „§ 5.", „Art. 5." — także z przyklejonym znacznikiem odnośnika:
  „42a))") nie rozdziela komórek.
- Nie są wierszem tabeli: legenda wzoru (krótki symbol, myślnik), wiersz z dalszą „komórką" zaczynającą się myślnikiem
  ze spacją (wyrównanie wartości) i wiersz bez sąsiednich wierszy tabeli, który ma mniej niż trzy komórki albo zaczyna się
  oznaczeniem (przepis rozstawiony tabulatorami).

## Odrzucone alternatywy

- **Każdy tabulator rozdziela komórki.** Pierwszy pomiar: 195 dokumentów gorzej (tekst jednolity: „42a))⇥…" i każda
  pozycja wyliczenia z tabulatorem za oznaczeniem stawały się tabelą), jednostki 99,43 % → 99,11 %.
- **Tabele w TXT zostają tekstem** (stan wcześniejszy). Wyliczenia w komórkach były jednostkami — więcej szkody
  niż pojedyncze wiersze z tabulatorem wyrównującym, które reguła jeszcze myli (patrz Konsekwencje).
- **Pełna rekonstrukcja wiersza tabeli** (komórka z kilkoma akapitami, wiersz scalony). Plik nie zapisuje, gdzie kończy
  się komórka z kilkoma akapitami — wiersz „§ 1, 2 lub 2a …⇥⇥15" może być dalszym ciągiem trzeciej komórki albo nowym
  wierszem; czytnik bierze każdy wiersz pliku z komórkami za nowy wiersz tabeli.

## Konsekwencje

- Komórka z kilkoma akapitami bez wyliczenia może stać się kilkoma wierszami tabeli; wiersz nagłówka scalony na całą
  szerokość (bez tabulatora) przerywa tabelę na dwie.
- Tabela jednowierszowa z dwiema komórkami zostaje tekstem.

## Weryfikacja

`ArchitectureDecisionTests.Adr0093_TxtTableRow_IsCutByTabsThatAreNotAfterADesignation`,
`PlainTextTablesTests`.
