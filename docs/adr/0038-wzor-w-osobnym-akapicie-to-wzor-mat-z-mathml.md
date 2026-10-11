# ADR-0038: Wzór w osobnym akapicie jest dalszą treścią jednostki z wyrażeniem w MathML i wychodzi jako `wzor-mat`

- Status: Accepted
- Data: 2026-10-11

## Kontekst

Od [ADR-0037](0037-rownanie-worda-wchodzi-do-tekstu-w-zapisie-liniowym.md) treść równania Worda nie znika:
wchodzi do tekstu akapitu w zapisie liniowym. Wzór stojący w osobnym akapicie („I_oze=(I_e∙U_oze)+I_b") był
więc akapitem tekstu — `fragment` w jednostce albo, gdy złożono go stylem ustępu czy punktu, jednostką bez
oznaczenia (`nie-ztp-ustep`, `nie-ztp-punkt`). Schemat ma na wzór osobny element: `wzor-mat` z wyrażeniem
`wzor` → `mml:math` i legendą (`wprowadzenie-do-opisu`, `opis`); przykład autorów schematu stawia go tuż za `p`
jednostki, do której wzór należy.

W korpusie 207 z 1206 równań stoi w osobnym akapicie; pozostałe to symbole w zdaniu i na początku wiersza
legendy. Konwersja OMML → MathML (`OfficeMathMl`) daje dla wszystkich wyrażenie zgodne ze schematem.

Zapis liniowy ujawnił przy okazji kolizję: nawias kwadratowy równania („KMOG = [(CC – CCW) × … (CM/12)]")
trafiał do kanału indeksu górnego i znacznika odnośnika (`[x]`, `[12)]`), więc wzór dostawał odwołanie do
odnośnika 12 i elementy `sup`.

## Decyzja

**Akapit złożony z samego równania jest wzorem: czytnik oddaje go z wyrażeniem w MathML obok zapisu liniowego
(`DocumentBlock.FormulaMathMl`), potok dołącza go jako dalszą treść jednostki, za którą stoi
(`ContentBlock.Formula`), a eksport pisze `wzor-mat` → `wzor` → `mml:math`.**

- Wzorem jest akapit, w którym poza równaniami stoi co najwyżej interpunkcja zdania („…/I ,") — ta wchodzi
  do wyrażenia. Akapit z tekstem obok równania (wiersz legendy, zdanie z symbolem) wzorem nie jest i zostaje
  tekstem w zapisie liniowym. Kilka równań jednego akapitu to wiersze jednego wzoru (`mtable`).
- Wzór nie ma oznaczenia, więc nie jest jednostką — także gdy złożono go stylem jednostki, w akcie i w treści
  cytowanej. Zachowuje przy tym sygnały stylu, które prowadzą nowelizację (styl `Z/…`).
- Blok wzoru niesie oba zapisy: `Text` (liniowy — dla granic cytatu, miar i każdego konsumenta tekstu)
  i `Formula.MathMl` (dla eksportu). Wzór wychodzi raz: jako `wzor-mat`, nie drugi raz jako `fragment`.
- W zapisie liniowym nawias kwadratowy równania to znaki „［" i „］" — zwykły `[x]` jest w tekście bloku
  kanałem indeksu górnego i znacznika odnośnika. W MathML nawias pozostaje zwykłym znakiem.

## Odrzucone alternatywy

- **MathML w tekście zdania** (element w `zdanie` zamiast bloku). Schemat nie przewiduje wyrażenia MathML
  w treści zdania; wzór jest tam elementem dalszej treści jednostki, obok `fragment` i tabeli.
- **Wzór jako osobny rodzaj jednostki w potoku** (`ParagraphKind.Formula`, własny budowniczy). Wzór nie ma
  oznaczenia, adresu ani dzieci; wszystko, czego potrzebuje, daje dalsza treść jednostki ([ADR-0029](0029-akapit-bez-oznaczenia-jest-dalsza-trescia-jednostki.md)),
  tak jak tabeli.
- **Sam MathML, bez zapisu liniowego w `Text`.** Blok bez tekstu byłby „pusty" dla bilansu cudzysłowów,
  granic nowelizacji i miary pokrycia tekstu — tych samych miejsc, w których puste równania psuły wynik
  przed ADR-0037.
- **Zawężenie wzorca znacznika odnośnika** zamiast zmiany nawiasu w zapisie liniowym. Wzorzec jest celowo
  szeroki („[2), 3)]", „[1 15)]", „[13).]" — [ADR-0026](0026-odnosnik-tresc-w-rejestrze-miejsce-w-tekscie.md));
  kanał `[x]` należy do odczytu tekstu, więc to zapis równania ma go nie zajmować.

## Konsekwencje

- W korpusie 189 wzorów w 45 dokumentach wychodzi jako `wzor-mat` (183 `fragment` mniej); znika 14 jednostek
  `nie-ztp-*` zbudowanych ze wzoru w stylu jednostki oraz 8 fałszywych odwołań do odnośników i 12 fałszywych
  `sup`. Zgodność ze schematem (1685 z 2321) i pokrycie tekstu bez zmian.
- Legenda wzoru („gdzie poszczególne symbole oznaczają:", „I_e – ilość energii …") zostaje za wzorem jako
  `fragment`; jej miejsce w `wzor-mat` (`wprowadzenie-do-opisu`, `opis` z `symbol-mat` i `objasnienie`) jest
  w backlogu.
- Wzór w załączniku, w punkcie obwieszczenia i przed pierwszą jednostką (18 równań) zostaje tekstem w zapisie
  liniowym — te ścieżki nie niosą jeszcze bloku wzoru.
- Dokument ze wzorem deklaruje przestrzeń nazw MathML (przedrostek `mml`) na elemencie `wzor-mat`.
- Miara pokrycia tekstu czyta `mroot` w kolejności zapisu liniowego (stopień przed wyrażeniem).

## Weryfikacja

- `ArchitectureDecisionTests.Adr0038_FormulaParagraph_IsExportedAsWzorMat_NotAsAUnitOrAFragment`
- `FormulaTests` — który akapit jest wzorem, interpunkcja we wzorze, wzór w stylu jednostki, eksport za
  zdaniami jednostki, treść cytowana, zgodność ze schematem.
- `OfficeMathMlTests` — budowa MathML; `OfficeMathTextTests.SquareBracketsOfAnEquation_AreNotReadAsAFootnoteMarker`.
