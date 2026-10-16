# ADR-0095: Odwołanie z własnym znakiem jest odnośnikiem; jego znacznik bierze kolejny numer w dokumencie

- Status: Accepted
- Data: 2026-10-16

## Kontekst

Word pozwala odwołaniu do przypisu zamiast numeru z licznika pokazać własny znak (`w:customMarkFollows`): znak
stoi w przebiegu tuż za odwołaniem („3)", „*", „za"; bywa znakiem czcionki Symbol). W korpusie DOCX tak zapisano
67 odwołań w 20 dokumentach — gwiazdki pod tabelą, numeracja odnośników załącznika tekstu jednolitego prowadzona
ręcznie, „za)" po „z)". Czytnik pomijał takie odwołania przy numeracji ([ADR-0061](0061-odnosnik-ma-oznaczenie-jak-w-akcie-a-znacznik-numer-jednoznaczny.md)),
więc treść przypisu nie trafiała do rejestru, a w tekście zostawał identyfikator `w:id` sklejony ze znakiem
(„3." + „5" + „3)" → „3.[53)]", „epidemiologiczne3*"). W ocenie korpusu z 2026-10-16 to 16 dokumentów z zagubioną
treścią odnośnika i 3 ze zniekształconym tekstem.

## Decyzja

**Odwołanie z własnym znakiem jest odnośnikiem: jego oznaczeniem (`Footnote.Symbol`) jest własny znak bez nawiasu
zamykającego, a znacznik w tekście dostaje kolejny numer w dokumencie (`DocxFootnotes.Read` liczy osobno numery
drukowane i numery znaczników).**

- Bez odwołań z własnym znakiem numery znaczników są numerami drukowanymi — jak dotąd.
- Czytnik tekstu (`ParagraphExtensions.GetFullText`) czyta przebieg takiego odwołania jako znacznik „[N)]", także gdy
  przebieg nie ma formatu indeksu górnego; znak za odwołaniem jest tym samym znacznikiem. Znak bez nawiasu („*",
  „za") zabiera nawias z następnego przebiegu indeksu górnego.
- Własny znak wpisany na początku treści przypisu („3)W brzmieniu …", „*)WSSE …") jest oznaczeniem, nie treścią.
- Odnośnik z własnym znakiem stoi poza numeracją Worda, więc w rozstrzyganiu powtórzeń (`FootnoteResolver`) nie
  przełącza bieżącego ciągu numeracji: załącznik w sekcji wznawiającej numerację bywa oznaczony własnym znakiem
  „6)" ciągnącym numerację aktu, a „1)" wpisane w jego tabeli powtarza odnośnik aktu (341275_3063424).

## Odrzucone alternatywy

- **Pomijać takie odwołania** (stan wcześniejszy). Treść przypisu znikała, a tekst niósł identyfikator `w:id`.
- **Znaczniki własnych znaków w osobnym zakresie numerów** (jak kolejne ciągi numeracji: 100 000 i dalej). Znaczniki
  muszą rosnąć w kolejności dokumentu — tak `FootnoteResolver` odróżnia pierwsze wystąpienie od powtórzenia — a
  odwołania z własnym znakiem przeplatają się ze zwykłymi.
- **Wszystkie znaczniki numerowane kolejno, niezależnie od numeru drukowanego.** Daje to samo w dokumentach
  z własnymi znakami, ale w pozostałych zmieniałoby znaczniki, a z nimi `id-odnosnika`, bez potrzeby.

## Konsekwencje

- W dokumencie z własnymi znakami znaczniki zwykłych odnośników za pierwszym takim znakiem przestają być ich
  numerami drukowanymi (`id-odnosnika` ≠ `symbol`); oznaczenie zostaje jak w akcie.
- Własny znak bez treści przypisu w dokumencie zostaje tym, co wydrukowano — bez identyfikatora `w:id`.
- Powtórzenie własnego znaku wpisane tekstem rozstrzyga się jak każde powtórzenie: oznaczeniem w bieżącym ciągu
  numeracji.

## Weryfikacja

`ArchitectureDecisionTests.Adr0095_CustomMarkReference_IsAFootnote_AndOtherMarkersStayPrintedNumbers`,
`FootnoteNumberingTests.CustomMarkReference_IsAFootnote_WithItsOwnMarkAsTheSymbol`,
`FootnoteNumberingTests.CustomMarkWithoutItsParenthesis_TakesTheParenthesisOfTheNextSuperscriptRun`,
`FootnoteNumberingTests.CustomMarkFootnote_DoesNotSwitchTheRunOfRepeatedMarkers`.
