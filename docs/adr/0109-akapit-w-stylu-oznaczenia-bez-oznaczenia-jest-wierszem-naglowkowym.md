# ADR-0109: Akapit w stylu oznaczenia jednostki systematyzacyjnej, w którym oznaczenia nie ma, jest wierszem nagłówkowym bez oznaczenia — nie znika

- Status: Accepted (rozszerza [ADR-0067](0067-wiersz-pod-tytulem-jednostki-jest-dalsza-trescia-naglowka.md) na akapity w stylu oznaczenia)
- Data: 2026-10-20

## Kontekst

Tekst jednolity ustawy o ubezpieczeniu społecznym rolników (Dz. U. 2025 poz. 197, plik 315430; także poz. 1770, plik 349480) dzieli rozdział 2 na części
„A. Ubezpieczenie wypadkowe, chorobowe i macierzyńskie" i „B. …", złożone stylem **oznaczenia** rozdziału
(`ROZDZODDZOZN…`). Oznaczeniem nie są — nie ma w nich słowa „Rozdział" ani numeru — więc klasyfikator zwraca dla nich
rodzaj nieznany ze stylem oznaczenia, a gdy styl i treść dają jednostkę systematyzacyjną bez numeru, potok kończył
na gałęzi „nie powinno się zdarzyć" i akapit pomijał. Wiersz pod tytułem rozdziału, przed pierwszym artykułem, nie miał
dokąd trafić i **znikał z eksportu**.

[ADR-0067](0067-wiersz-pod-tytulem-jednostki-jest-dalsza-trescia-naglowka.md) rozstrzygnął ten sam przypadek dla wiersza
w stylu **tytułu** („A. Plany naprawy" pod tytułem rozdziału): jest dalszą treścią nagłówka i wychodzi jako `specjalny`.

## Decyzja

**Akapit w stylu oznaczenia jednostki systematyzacyjnej, w którym oznaczenia odczytać się nie da, jest wierszem
nagłówkowym bez oznaczenia: pod tytułem jednostki, przed jej pierwszym przepisem — dalszą treścią nagłówka
(`specjalny`, ADR-0067); w innym miejscu — tak jak akapit nierozpoznany (dalsza treść przepisu, przy którym stoi).**

- Miejsce: `StructureProcessor.Process`, zanim akapit trafi do budowy jednostki systematyzacyjnej.
- Klasyfikacja się nie zmienia; jednostki bez oznaczenia nie powstają.

## Odrzucone alternatywy

- **Pominąć** (stan sprzed decyzji). Treść aktu ginie bez śladu w eksporcie.
- **Jednostka bez oznaczenia** (`rozdzial-n` itp.). Kontener jednostki niejawnej nie ma nagłówka — tekst i tak nie
  miałby miejsca; jak w ADR-0067.

## Konsekwencje

- W korpusie DOCX: wiersz „A. Ubezpieczenie wypadkowe, chorobowe i macierzyńskie” wraca do eksportu w 2 plikach (jako `specjalny` w nagłówku rozdziału); zgodność ze schematem bez zmian (2013 z 2321).
- Taki wiersz między artykułami („B. …" za art. 15) zostaje dalszą treścią poprzedniego artykułu — tekst jest, ale
  stoi przy przepisie, którego nie dotyczy; nagłówek pośredni w środku jednostki nie ma odpowiednika w schemacie.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0109_DesignationStyleWithoutADesignation_IsAHeadingLine`
- `SystematizingUnitTests.SubheadingInTheDesignationStyleWithoutADesignation_IsFurtherContentOfTheHeading`
