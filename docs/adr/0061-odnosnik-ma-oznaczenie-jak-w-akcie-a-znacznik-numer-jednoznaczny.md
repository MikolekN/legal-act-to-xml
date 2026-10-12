# ADR-0061: Odnośnik ma oznaczenie jak w akcie, a jego znacznik w tekście — numer jednoznaczny w dokumencie

- Status: Accepted
- Data: 2026-10-12

## Kontekst

Odnośniki numerowaliśmy kolejno w całym dokumencie ([ADR-0026](0026-odnosnik-tresc-w-rejestrze-miejsce-w-tekscie.md)),
a Word numeruje je tak, jak każe sekcja dokumentu: `w:footnotePr/w:numRestart="eachSect"` wznawia numerację,
`w:numFmt` zmienia jej zapis. W plikach Dz.U. jest to reguła — obwieszczenie ma własny odnośnik („a)" albo
„1)"), a ogłaszany akt i jego załączniki liczą od nowa. W 228 dokumentach korpusu odnośniki stoją w więcej niż
jednej sekcji i ich numery wychodziły przesunięte (odnośnik „1)" załącznika jako „12)").

Przesunięcie psuło więcej niż atrybut `symbol`. Odnośnik powołuje się na inny numerem drukowanym („ustawy,
o której mowa w odnośniku 8"), a powtórzony znacznik jest w pliku zwykłym tekstem w indeksie górnym — też
z numerem drukowanym. Oba wskazywały więc inny odnośnik niż ten, o który chodzi.

Numer drukowany nie może jednak zastąpić numeru kolejnego: „1)" stoi w obwieszczeniu, w akcie i w załączniku,
a tekst jednostki nie mówi, w której części dokumentu leży.

## Decyzja

**Odnośnik niesie dwa numery: oznaczenie jak w akcie (`Footnote.Symbol`) i numer, pod którym jego znacznik stoi
w tekście jednostki (`Footnote.Marker`) — jednoznaczny w dokumencie.**

- **Odczyt** (`DocxFootnotes.Read`): oznaczenie liczy się według ustawień sekcji — wznowienie (`numRestart`),
  początek (`numStart`), zapis (`numFmt`: liczby, litery, cyfry rzymskie). Sekcja z własnym `w:footnotePr`
  bierze z niego wszystko (czego nie podaje — wartości domyślne), sekcja bez niego — ustawienia dokumentu.
  Każde wznowienie zaczyna nowy **ciąg numeracji** (`Footnote.Run`).
- **Znacznik**: w pierwszym ciągu to numer drukowany, więc zwykły dokument czyta się jak dotąd; w kolejnych —
  numer drukowany przesunięty o wielokrotność 100 000 („[100002)]"). Takiego numeru akt tekstem nie zapisze,
  więc znacznik wpisany tekstem („[2)]" przytoczonego odnośnika, objaśnienia pod tabelą) nie przejmie odnośnika
  z dalszej sekcji.
- **Rozstrzyganie** (`FootnoteResolver`, wspólne dla eksportu i dla wersji czasowych): pierwsze wystąpienie to
  znacznik o numerze `Marker`, większym od dotąd spotkanych; powtórzenie to oznaczenie drukowane w ciągu,
  w którym stoi ostatnie pierwsze wystąpienie.
- **Eksport**: `id-odnosnika` = `Marker` (tożsamość), `symbol` = `Symbol` (oznaczenie) — schemat rozdziela te dwa
  atrybuty. Znacznik, który wychodzi bez odnośnika, jest zapisywany oznaczeniem (`FootnoteResolver.AsPrinted`).

## Odrzucone alternatywy

- **Numer drukowany w tekście jednostki.** Znacznik czytałby się jak w akcie, ale przestałby wskazywać odnośnik:
  początek nowego ciągu („1)" załącznika) jest nie do odróżnienia od powtórzenia „1)" aktu bez wiedzy
  o położeniu znacznika w dokumencie.
- **Numer kolejny w całym dokumencie jako znacznik** (stan dotychczasowy). Zderza się z numerami wpisanymi
  tekstem: w obwieszczeniu z jednym odnośnikiem przytoczone „[2)]" przejmowało pierwszy odnośnik ogłaszanego aktu.
- **Odnośnik przypisany do części dokumentu** (obwieszczenie / akt / załącznik) i rejestr osobny dla każdej.
  Ciąg numeracji mówi to samo bez przebudowy modelu, a granice sekcji nie zawsze pokrywają się z częściami.
- **Przepisywać w adapterze znaczniki wpisane tekstem na numery odwołań.** Adapter nie wie, czy znacznik
  w cytacie należy do tego aktu, czy do zmienianego.

## Konsekwencje

- W korpusie zmienia się 225 z 2321 dokumentów — te, w których odnośniki stoją w więcej niż jednej sekcji: 3284 odnośniki w 223 plikach dostają oznaczenie jak w akcie. 115 odsyłaczy, które wskazywały odnośnik innej sekcji albo odnośnik przejęty przez znacznik wpisany tekstem (przytoczony odnośnik aktu zmieniającego, objaśnienie pod tabelą), wychodzi indeksem górnym, 9 powtórzonych znaczników zyskuje odsyłacz, a przejęte odnośniki wracają na swoje miejsce (2 dotąd nieosadzone). Wersje czasowe ([ADR-0060](0060-jednostka-drukowana-dwa-razy-to-dwie-wersje-rozrozniane-okresem.md)): okres rozróżnia 479 z 591 jednostek o powtórzonym eId (było 477), `obowiazuje-do` niesie 514 jednostek (było 501). Zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory), pokrycie tekstu bez zmian.
- Numeracja wznawiana co stronę (`numRestart="eachPage"`, 2 dokumenty korpusu) zostaje ciągła — układ stron
  nie jest znany.
- Znacznik literowy wpisany tekstem („[a)]") nie jest rozpoznawany jako znacznik odnośnika.
- W jednym ciągu numeracji znacznik wpisany tekstem nadal może trafić na numer odnośnika, którego znacznik
  jeszcze nie padł (przytoczony odnośnik „2)" w akcie mającym własny „2)" dalej) — jak przed zmianą.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0061_FootnoteCarriesThePrintedSymbol_AndItsMarkerStaysUnique`
- `FootnoteNumberingTests` — odczyt sekcji (wznowienie, zapis literowy, brak ustawień), rozstrzyganie znacznika,
  eksport `symbol` / `id-odnosnika`, powtórzony znacznik w sekcji o wznowionej numeracji.
- `WordingVersionTests.RepeatedMarker_InADocumentThatRestartsFootnoteNumbering_IsReadByItsPrintedNumber`
