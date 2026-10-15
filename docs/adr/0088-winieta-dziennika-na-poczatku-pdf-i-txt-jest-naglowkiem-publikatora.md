# ADR-0088: Winieta dziennika na początku PDF i TXT jest nagłówkiem publikatora

- Status: Accepted
- Data: 2026-10-15

## Kontekst

Dane ogłoszenia aktu — dziennik, rocznik, pozycja i data ogłoszenia — stoją w winiecie na pierwszej stronie
(„DZIENNIK USTAW / RZECZYPOSPOLITEJ POLSKIEJ / Warszawa, dnia 12 stycznia 2026 r. / Poz. 26"). W DOCX winieta
jest w nagłówku strony; czytnik oznacza jej akapity rolą `JournalHeader`, a `JournalHeaderParser` czyta z nich
dane. PDF i TXT nie mają części nagłówkowej: w PDF winieta to pierwsze wiersze strony, w TXT — pierwsze wiersze
pliku, jeśli tekst wzięto z wydruku. Dotąd były zwykłą treścią i test
`Parse_PlainTextWithJournalLines_DoesNotInventAnnouncementDate` pilnował tego jako świadomego ograniczenia:
uznanie wierszy treści za nagłówek odłożono jako osobną decyzję.

Skutek ograniczenia, zmierzony miarą `--format-parity` na korpusie przekonwertowanym do PDF: żaden akt PDF nie
dostawał danych ogłoszenia, więc nie miał ELI ani prefiksu eId, wejście w życie liczone od ogłoszenia zostawało
bez dat, a eksport nie był zgodny ze schematem (`Rok`, `Pozycja`, `DataOgloszenia` są wymagane). Wiersze winiety
stały przed tytułem jako treść i psuły klasyfikację obwieszczeń Monitora Polskiego.

## Decyzja

Adaptery PDF i TXT oznaczają rolą `JournalHeader` bloki na samym początku dokumentu, które razem dają winietę
(`JournalMasthead`): nazwę dziennika (Dziennik Ustaw, Monitor Polski), a po niej najwyżej drugi wiersz nazwy
(„RZECZYPOSPOLITEJ POLSKIEJ", „DZIENNIK URZĘDOWY RZECZYPOSPOLITEJ POLSKIEJ"), miejsce i datę wydania numeru oraz
pozycję. Liczy się tekst bloków złączony, bo podział winiety na bloki zależy od składu: PDF łamie dużą nazwę
dziennika („MONITOR" / „POLSKI DZIENNIK URZĘDOWY …"), a datę i pozycję zdarza mu się złożyć w jeden wiersz.
Pierwszy blok, który winiety nie przedłuża, ją kończy. Dalej dane czyta ten sam `JournalHeaderParser` co dla DOCX.

## Odrzucone alternatywy

**Zostawić ograniczenie — dane ogłoszenia dla PDF i TXT tylko od wywołującego (ADR-0081).** Odrzucone: winieta
jest w samym dokumencie i ma stały, urzędowy układ; wymaganie, by wywołujący przepisał z niej rok i pozycję,
przerzuca na niego pracę parsera, a bez tego każdy eksport PDF jest niezgodny ze schematem. Dane od wywołującego
nadal mają pierwszeństwo tam, gdzie dokument ich nie podaje.

**Szukać wierszy winiety w całym początku dokumentu albo pojedynczo** („Warszawa, dnia …" i „Poz. …" gdziekolwiek
przed tytułem). Odrzucone: to zgadywanie struktury z treści, przed którym ostrzegał dotychczasowy test — taki wiersz
bywa treścią pisma, przytoczeniem albo częścią załącznika. Winieta zaczyna się nazwą dziennika i stoi na samym
początku; oba warunki razem są dwoma zgodnymi sygnałami (treść i miejsce).

**Rozpoznawać winietę PDF po geometrii** (największy font na stronie, górna część pierwszej strony). Odrzucone:
TXT geometrii nie ma, a sygnał treści wystarcza obu formatom; drugi mechanizm dla jednego formatu podwajałby
reguły bez zysku.

## Konsekwencje

- PDF i TXT z winietą dają te same dane ogłoszenia co DOCX: ELI, prefiks eId, daty wejścia w życie liczone od
  ogłoszenia, zgodność nagłówka ze schematem.
- TXT bez winiety (eksport Worda, który nagłówka strony nie zapisuje) nadal jej nie ma — dane ogłoszenia podaje
  wywołujący albo wiersz przyłączenia (`JournalHeaderParser.ApplyFromAttachment`).
- Na korpusie przekonwertowanym do PDF (2151 dokumentów) dane ogłoszenia zgodne z DOCX ma 1835 dokumentów
  (wcześniej 109).
- Wiersze dziennika poza samym początkiem dokumentu pozostają treścią; pilnuje tego
  `Parse_PlainTextWithJournalLinesAfterTheAct_DoesNotInventAnnouncementDate`.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0088_JournalMastheadAtTheStartOfPlainText_GivesTheAnnouncementData`
- `JournalMastheadTests` (TXT i PDF, winieta łamana przez skład, wiersze bez nazwy dziennika)
