# ADR-0026: Treść odnośnika żyje w rejestrze dokumentu, a jego miejsce — w tekście jednostki

- Status: Accepted (znacznik bez odnośnika tego aktu nie zostaje już tekstem — uściśla to [ADR-0047](0047-znacznik-bez-odnosnika-tego-aktu-wychodzi-indeksem-gornym.md); numer znacznika a oznaczenie odnośnika: [ADR-0061](0061-odnosnik-ma-oznaczenie-jak-w-akcie-a-znacznik-numer-jednoznaczny.md))
- Data: 2026-10-10

## Kontekst

Odnośnik (przypis) ma w pliku Word dwie części w dwóch miejscach: odwołanie w treści aktu i treść
w osobnej części pakietu (`word/footnotes.xml`). Parser czytał tylko odwołanie — jako znacznik w kanale
indeksu górnego (`[2)]`), i to z identyfikatorem `w:id` zamiast numeru z aktu. Treści przypisów nie było
ani w modelu, ani w eksporcie, choć w tekstach jednolitych niosą informację normatywną („Dodany przez
art. …"), a przykłady autorów schematu zapisują każdy odnośnik jako element `odnosnik` w miejscu
odwołania: w zdaniu, w tytule aktu.

Tekst jednostki jest w modelu płaskim napisem (`ContentText`, `TextSegment.Text`) — nie ma węzłów,
do których dałoby się przypiąć przypis.

## Decyzja

**Treść odnośnika trzyma rejestr dokumentu (`LegalDocument.Footnotes`), a miejsce odwołania — znacznik
`[N)]` w tekście jednostki, z numerem, pod którym odnośnik stoi w akcie.**

- Adapter DOCX numeruje odwołania w kolejności wystąpień i dołącza treść przypisu do bloku, który się
  do niego odwołuje (`DocumentBlock.Footnotes`, `DocxFootnotes`); orkiestrator przepisuje ją do rejestru.
- Eksport osadza odnośniki osobnym przebiegiem po zbudowaniu treści (`FootnoteXmlConverter.Embed`):
  znacznik w `zdanie`, `tytul` albo `tekst` staje się elementem `odnosnik` (`id-odnosnika`, `symbol`,
  treść w `p`), a to, co stoi między numerami jednego znacznika, zostaje w `sup`.
- Każdy odnośnik trafia pod pierwszy znacznik ze swoim numerem, w kolejności numerów. Kolejny znacznik
  z numerem już osadzonym to `odsylacz-do-odnosnika` — poza cytatem; w cytacie taki znacznik należy
  do aktu zmienianego i zostaje tekstem.

## Odrzucone alternatywy

- **Przypisy jako bloki `BlockRole.FootnoteText` w potoku** (tak oddaje je adapter PDF). Klasyfikator
  dokumentu czyta wszystkie bloki, więc treść przypisów zmieniałaby jego sygnały („zmienia się ustawy",
  „wchodzi w życie" w przypisie), a związek przypisu z miejscem odwołania trzeba by odtwarzać z numerów.
- **Przypis przypięty do jednostki** (`BaseEntity.Footnotes`). Jednostka mówi „mam przypis", ale nie
  „w którym miejscu zdania"; treść nowelizacji budowana jest z zebranych akapitów później, więc przypis
  musiałby wędrować przez `AmendmentCollector`. Znacznik w tekście niesie miejsce bez tej drogi.
- **Tekst jednostki jako drzewo węzłów** (tekst, indeks górny, odnośnik). Docelowo właściwe, ale zmienia
  każde miejsce czytające `ContentText` — klasyfikację, podział na zdania, powołania, cele nowelizacji.

## Konsekwencje

- Zmienia wyjście: znaczniki `[N)]` znikają z tekstu zdań i tytułu, w ich miejscu stoją `odnosnik`
  i `odsylacz-do-odnosnika`; metadane `Tytul` nie niosą znaczników. Korpus: 9389 odnośników i 3380
  odsyłaczy, 85 znaczników zostaje tekstem.
- Numer odnośnika jest jednocześnie kluczem: literał `[7)]` wpisany w treść przed prawdziwym odnośnikiem
  7 przejąłby jego treść. Chroni przed tym tylko kolejność numerów.
- Kod czytający tekst jednostki widzi znaczniki. Powołania je wygaszają (`FootnoteMarkers.Blank`);
  wzorce oznaczeń przyjmują znacznik tuż za numerem.
- Poza zakresem: odnośnik przy nagłówku jednostki systematyzacyjnej (znacznik zostaje w tekście),
  odnośnik o budowie wyliczenia (`punkt-odnosnik` — dziś zdania w jednym `p`), odnośnik cytowany
  w nowelizacji (`odnosnik-cyt`), przypisy z PDF (bloki `FootnoteText` nie trafiają do rejestru)
  i przypisy w załącznikach.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0026_Footnote_LivesInTheRegistry_AndIsExportedAtItsMarker`.
- `FootnoteTests` — model, miejsce w zdaniu i tytule, odsyłacz, znacznik spoza rejestru, zgodność ze
  schematem; `DocxBlockReaderTests` — numer jak w akcie i treść na bloku.
- `CorpusValidator --compare-reference`: liczba `odnosnik` w przykładach autorów schematu (5 i 28)
  zgadza się z eksportem.
