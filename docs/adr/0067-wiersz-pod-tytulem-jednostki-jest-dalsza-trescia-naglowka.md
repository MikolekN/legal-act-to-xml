# ADR-0067: Wiersz pod tytułem jednostki systematyzacyjnej, który nie jest ani tytułem, ani jednostką, jest dalszą treścią nagłówka

- Status: Accepted
- Data: 2026-10-13

## Kontekst

Pod tytułem jednostki systematyzacyjnej, a przed jej pierwszym przepisem, akty stawiają czasem jeszcze jeden wiersz
w stylu tytułu: nagłówek pośredni bez oznaczenia („Przepis wstępny" pod „DZIAŁ I / Właściwość sądu"; „A. Plany
naprawy" pod tytułem rozdziału), drugą część tytułu złożoną osobnym akapitem („Podatnik akcyzy." / „Właściwość
organów podatkowych"), drugie brzmienie tytułu albo adnotację, która nie nazywa jednostki („(Przepis wstępny –
uchylone)").

Wiersz zaczynający się wielką literą nie jest doklejany do tytułu — to osobny nagłówek, nie ciąg dalszy
(`SystematizingUnitTests.SubheadingStartingWithCapital_IsNotJoinedToTheTitle`). Nie miał jednak dokąd trafić: nie
jest jednostką, a przed pierwszym artykułem nie ma przepisu, którego dalszą treścią mógłby zostać — ginął z eksportu.

Schemat nie zna nagłówka pośredniego. Nagłówek jednostki ma jeden akapit `p`, a za nim grupę
`rozszerzenie-kontynuacji` — elementy `specjalny`, „treść specjalną w dowolnym miejscu ciągłości tekstu, bez
łamania struktury głównego modelu".

## Decyzja

**Wiersz w stylu tytułu albo adnotacja o uchyleniu nienazywająca jednostki, stojące pod tytułem jednostki
systematyzacyjnej przed jej pierwszym przepisem, są dalszą treścią jej nagłówka** (`BaseEntity.Continuation`
jednostki) **i wychodzą w nagłówku jako `specjalny`**, w kolejności z aktu.

- Tytuł zostaje taki, jaki jest w akcie — bez doklejania (wyjątek jak dotąd: ciąg dalszy od małej litery).
- Adnotacja, która nazywa jednostkę z oznaczeniem, jest nagłówkiem tej jednostki
  ([ADR-0066](0066-adnotacja-nazywajaca-uchylona-jednostke-jest-jej-naglowkiem.md)) i ma pierwszeństwo.
- Odnośnik stojący w takim wierszu jest osadzany jak w każdym innym tekście.

## Odrzucone alternatywy

- **Dokleić do tytułu.** Tytuł działu brzmiałby „Właściwość sądu Przepis wstępny" — czego w akcie nie ma.
- **Osobna jednostka bez oznaczenia.** Schemat daje jednostce niejawnej (`rozdzial-n`) kontener bez nagłówka,
  a jednostce z nagłówkiem — wymagany numer. Nagłówek pośredni nie mieści się w żadnej.
- **Dalsza treść pierwszego przepisu jednostki.** Wiersz stoi przed nim i nic o nim nie mówi.

## Konsekwencje

- W korpusie zmienia się 5 tekstów jednolitych: 8 wierszy stojących pod tytułem jednostki wychodzi w jej nagłówku jako `specjalny`; 6 z nich ginęło z eksportu. Zgubionych akapitów zostaje w całym korpusie 3 (1 w tekście jednolitym uchwały, 2 w jednym rozporządzeniu) — przedtem 11, z czego 2 były artefaktem miary. Zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory).
- Nagłówek pośredni stojący dalej, między przepisami („B. Wczesna interwencja"), nadal wychodzi jako `fragment`
  przepisu, za którym stoi — schemat nie ma na niego miejsca między jednostkami redakcyjnymi.
- Drugie brzmienie tytułu jednostki (2 dokumenty) zachowuje tekst i odnośnik, ale nie ma jeszcze wersji czasowej.
- Miara pokrycia tekstu (`TextCoverageReport`) zdejmuje oznaczenie artykułu także wtedy, gdy zapisano je bez kropki
  („Art 64.") — tak jak czyta je parser.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0067_LineUnderTheTitleOfAUnit_IsFurtherContentOfItsHeading`
- `SystematizingUnitTests` — nagłówek pośredni, tytuł w dwóch akapitach, adnotacja nienazywająca jednostki,
  eksport `nr` / `p` / `specjalny`, zgodność ze schematem; kontrprzykłady: tytuł nie jest doklejany, wiersz
  w stylu tytułu stojący dalej nie wraca do wcześniejszego nagłówka.
