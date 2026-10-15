# ADR-0092: Tabela w PDF to siatka z obramowania rysowanego na stronie; tabeli bez obramowania czytnik nie zgaduje

- Status: Accepted
- Data: 2026-10-15

## Kontekst

PDF nie zapisuje tabel: ma litery z położeniem i ścieżki rysowane na stronie. Czytnik składał wiersze z liter
o wspólnej linii bazowej, więc wiersz tabeli z kilkoma kolumnami — każda z tekstem w kilku wierszach — dawał wiersze
strony z tekstem wszystkich kolumn naraz („„1 bez względu Wyroby medyczne … 3 na CN w rozumieniu …"), a wyliczenia
w komórkach („1) charakteryzuje pojęcia …") stawały się jednostkami. W korpusie PDF 285 z 2151 dokumentów ma tabele
w DOCX; dawały 1939 z 5088 różnic jednostek wobec DOCX (wcześniej 828 ginących, 1111 nadmiarowych), a w tekście
jednolitym jeden przemieszany wiersz tabeli w cytacie potrafił unieważnić całe przytoczenie obwieszczenia.
DOCX niesie miejsce akapitu w tabeli (`BlockTableCell`: tabela, wiersz, kolumna, scalenia), a parser składa z takich
bloków tabelę treści, tabelę w cytacie i tabelę załącznika. LibreOffice rysuje obramowanie komórek DOCX odcinkami
(PdfPig: `Page.Paths`).

## Decyzja

**Tabelą w PDF jest siatka odcinków rysowanych na stronie: linie poziome i pionowe, które się przecinają. Litery
w jej polu czytnik czyta komórkami i oddaje każdy akapit komórki blokiem z `TableCell`, jak w DOCX. Bez obramowania
tekst zostaje wierszami — czytnik nie wnioskuje kolumn z położenia tekstu.**

- `PdfTables` grupuje odcinki w składowe połączone przecięciami; składowa z co najmniej dwiema liniami w każdym
  kierunku jest siatką. Pola siatki bez odcinka między sobą są jedną komórką (scalenie w poziomie albo w pionie).
- Litera należy do komórki, w której stoi jej środek. Wiersze komórki buduje ten sam `PdfLineExtractor`, akapity —
  ta sama reguła granic bloku co w treści.
- W porządku czytania tabelę reprezentuje wiersz zastępczy na górnej krawędzi siatki: zamyka akapit przed tabelą,
  a za nią zaczyna nowy. Siatka na początku następnej strony z tymi samymi kolumnami, bez tekstu pomiędzy, to ta sama
  tabela — wiersze numeruje się dalej.

## Odrzucone alternatywy

- **Kolumny z położenia tekstu** (odstęp w wierszu szerszy niż 2 em — `PdfTextLine.MaxGap` — i wyrównanie początków
  fragmentów). Sygnał istnieje i służy już do tego, by wiersze tabeli nie dzieliły się odstępem akapitowym, ale
  granicy wiersza tabeli nie daje: komórka z tekstem w kilku wierszach i sąsiednia jednowierszowa wyglądają jak dwa
  wiersze, a wyrównany odstępami tekst zwykły (wyliczenia z kropkami wiodącymi, podpisy) — jak kolumny. Pomyłka
  zamienia akapity treści w komórki, które parser wyjmuje z rozpoznawania jednostek — gorzej niż tabela czytana
  wierszami.
- **Tabela jako jeden blok w kolejności kolumn** (bez komórek). Przywraca kolejność tekstu w tabeli jednowierszowej,
  ale nie w wielowierszowej, a parser i tak nie wiedziałby, że to tabela — wyliczenia w niej dalej byłyby jednostkami.

## Konsekwencje

- Tabela bez obramowania zostaje wierszami tekstu; wiersz nagłówka powtórzony na kolejnej stronie jest zwykłym
  wierszem, a wiersz przecięty stroną — dwoma wierszami (backlog: „Tabele w PDF i TXT").
- Odwołanie do odnośnika w komórce należy do bloku komórki: strefa przypisów sprawdza odwołania także w komórkach
  (`PdfBlockReader.IsReferencedOnPage`).
- Rozmiar tekstu podstawowego wiersza to największy rozmiar co najmniej ćwierci liter (`PdfLineExtractor.BaseSize`) —
  w krótkiej komórce („2" z odwołaniem „10)") liter indeksu górnego bywa więcej niż liter tekstu.
- Korpus PDF (`--format-parity`): różnice wymienione w CHANGELOG przy tej zmianie.

## Weryfikacja

`ArchitectureDecisionTests.Adr0092_PdfTable_ComesFromItsBorders_NotFromTheTextLayout` — ten sam tekst w dwóch
kolumnach jest komórkami tylko z obramowaniem; bez niego zostaje wierszami.
`PdfBlockReaderTests.BorderedTable_ReadsCellsIntoTheirPlaces`, `BorderedTable_MissingSeparatorMergesCells`,
`BorderedTableContinuedOnTheNextPage_IsOneTable`.
