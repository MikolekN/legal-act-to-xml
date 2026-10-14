# ADR-0077: Segment załącznika to ciąg treści na stronach jednej orientacji — orientację podaje układ strony źródła

- Status: Accepted
- Data: 2026-10-14

## Kontekst

Schemat daje segmentowi załącznika (`segment-zalacznika`, typ `ZalacznikTyp`) opcjonalny atrybut `orientacja`
(`pionowa`, `pozioma`); przykład autorów schematu (DU/2026/252) oznacza nim segmenty swoich załączników. Załącznik
bywa złożony ze stron obu orientacji — wstęp na stronie pionowej, szeroka tabela na poziomych, dalej znów pionowa.
DOCX zapisuje orientację w ustawieniach sekcji (`w:sectPr/w:pgSz`): sekcję kończy akapit niosący `w:sectPr`,
a ostatnią — `w:sectPr` treści dokumentu. Dotąd każdy załącznik miał jeden segment, bez orientacji.

## Decyzja

**Blok niesie orientację strony swojej sekcji (`DocumentBlock.IsLandscape`, `DocxBlockReader.PageOrientation`):
pozioma, gdy sekcja mówi to wprost (`w:orient="landscape"`) albo strona jest szersza niż wyższa; nieznana (null),
gdy źródło rozmiaru strony nie podaje. Treść załącznika dzieli się na segmenty tam, gdzie zmienia się orientacja,
a segment o znanej orientacji niesie `orientacja`** (`AnnexXmlConverter.Runs`).

- Jednostki zbudowane z treści-wyliczenia stoją na stronie pierwszego akapitu wyliczenia.
- PDF i TXT orientacji nie podają — segment wychodzi bez atrybutu.

## Odrzucone alternatywy

- **Jeden segment z orientacją pierwszej strony.** Tabela na stronach poziomych w środku załącznika dostałaby
  orientację wstępu.
- **Orientacja zgadywana z szerokości tabeli** (siatka szersza niż kolumna tekstu strony pionowej). Źródło mówi
  o orientacji wprost; zgadywanie myli tabele zwężone przez autora.
- **`pionowa` domyślnie, gdy źródło układu nie podaje.** Atrybut jest opcjonalny; orientacja wymyślona dla PDF
  i TXT byłaby domysłem zapisanym jak fakt.
- **Segment na każdą sekcję DOCX.** Sekcję zaczyna się też z innych powodów (numeracja przypisów, nagłówki
  stron) — granica segmentu stoi tam, gdzie zmienia się to, co segment opisuje.

## Konsekwencje

- W korpusie wszystkie 722 segmenty załączników (432 dokumenty) niosą `orientacja`, 29 z nich `pozioma`; w 2 dokumentach załącznik dzieli się na trzy segmenty (pionowa — pozioma — pionowa: szeroka tabela w środku), przybywa 4 segmentów. Zgodnych ze schematem jest nadal 2278 z 2321 (oba walidatory). Wobec przykładu DU/2026/252 zgadzają się trzy segmenty; czwarty jest w przykładzie bez atrybutu, u nas `pionowa` — jak strona źródła.
- Granica segmentu zależy od układu strony źródła: ta sama treść z PDF wychodzi jednym segmentem.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0077_AnnexSegment_IsARunOfContentOnPagesOfOneOrientation_TakenFromTheSourceLayout`
- `AnnexTests` — orientacja treści, podział na segmenty, strona szersza niż wyższa, sekcje bez rozmiaru strony,
  źródło bez układu, zgodność ze schematem; `DocxBlockReaderTests.Block_TakesThePageOrientationOfTheSectionItBelongsTo`.
