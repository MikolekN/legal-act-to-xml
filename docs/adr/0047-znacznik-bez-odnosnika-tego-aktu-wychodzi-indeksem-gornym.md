# ADR-0047: Znacznik odnośnika, za którym nie stoi odnośnik tego aktu, wychodzi indeksem górnym

- Status: Accepted (uściśla [ADR-0026](0026-odnosnik-tresc-w-rejestrze-miejsce-w-tekscie.md))
- Data: 2026-10-12

## Kontekst

Miejsce odnośnika niesie w tekście jednostki znacznik `[N)]` — zapis indeksu górnego w kanale tekstowym
(ADR-0026). Eksport zamienia go na `odnosnik` albo `odsylacz-do-odnosnika`, gdy numer jest w rejestrze
odnośników dokumentu. ADR-0026 postanowił, że znacznik bez takiego odnośnika „zostaje tekstem" — i wychodził
z nawiasami kanału: `odnośnik [2)] otrzymuje brzmienie`.

Znaczników bez odnośnika jest w dokumentach kilka rodzajów: odnośnik aktu cytowanego w nowelizacji,
objaśnienie wpisane akapitem pod tabelą („¹⁾ Zgodnie z …"), numer objaśnienia w komórce tabeli, znacznik
w treści odnośnika. W korpusie to 214 znaczników w 70 dokumentach.

Nawias kwadratowy wokół znacznika nie jest znakiem aktu — w akcie stoi indeks górny. Jest zapisem roboczym
parsera, a ten nie powinien trafiać do wyniku, tak jak nie trafia tam indeks „477[9]" (wychodzi jako `sup`).

## Decyzja

**Znacznik, którego żaden numer nie jest odnośnikiem tego aktu, wychodzi jako `sup` z treścią znacznika
(„2)", „1), 2)") — tak, jak stoi w akcie. Dotyczy tylko samego znacznika** (`FootnoteMarkers.Bare`: numery
z nawiasami, rozdzielone i zakończone najwyżej interpunkcją); nawias kwadratowy z innym tekstem pozostaje
tekstem.

- Nie powstaje `odnosnik` ani `odsylacz-do-odnosnika`: rejestr dokumentu takiego odnośnika nie zna.
- Reguła działa w tych samych miejscach, w których osadzane są odnośniki (`InlineTextXml`), także w dokumencie
  bez żadnego odnośnika w rejestrze.

## Odrzucone alternatywy

- **Zostawić zapis kanału** (ADR-0026). Eksport niósł znak, którego w akcie nie ma, i dwa zapisy tego samego
  zjawiska: `sup` dla indeksu przy liczbie, nawias dla znacznika.
- **`odsylacz-do-odnosnika` bez odnośnika.** Odsyłacz wskazuje odnośnik po identyfikatorze; bez odnośnika
  byłby wskazaniem donikąd, a dla odnośnika aktu cytowanego — wskazaniem na cudzy numer.
- **Każdy nawias pasujący do wzorca znacznika jako `sup`.** Wzorzec przyjmuje też nawias kwadratowy z tekstem
  („[D_2022 x ((D_2018 + D_2019)/n)]" — wzór wpisany w treść); to nawias, nie indeks.

## Konsekwencje

- W korpusie 214 znaczników w 70 dokumentach wychodzi jako `sup`; zgodność ze schematem i pokrycie tekstu
  bez zmian.
- Zapis kanału zostaje tylko tam, gdzie schemat go wymaga albo tekst nie jest osadzany: w `nr` i eId
  (ADR-0026), w symbolu legendy wzoru i w nawiasie zlanym z indeksem oznaczenia („[1 15)]").
- Objaśnienie wpisane akapitem pod tabelą nadal nie jest odnośnikiem: jego numer jest indeksem górnym na
  początku akapitu, a powiązanie z miejscem odwołania w tabeli nie powstaje.
- Miara pokrycia tekstu (`CorpusValidator --text-coverage`) liczy taki `sup` jak znacznik, nie jak treść.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0047_MarkerWithoutAFootnote_IsExportedAsASuperscript_NeverAsTheChannelNotation`
- `FootnoteTests` — znacznik bez przypisu, kształt samego znacznika, nawias z innym tekstem.
