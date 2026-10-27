# ADR-0118: Kursywa i pogrubienie wchodzą do treści, gdy wyróżniają część akapitu

- Status: Accepted
- Data: 2026-10-27

## Kontekst

Użytkownik zdecydował (2026-10-27), że eksport ma przenosić kursywę i pogrubienie tam, gdzie źródło je ma
(ADR-0117 dał mechanizm). Pierwszy pomiar korpusu DOCX z włączonymi `i` i `b` dał 10 288 `b` i 3610 `i`; większość `b`
obejmowała **cały** host: nagłówki kolumn tabel (2700), numery w kolumnie „lp.” (2227), całe zdania (1944 — przepisy
w nowym brzmieniu tekstu jednolitego, § 106a ust. 4 ZTP), wiersze rodzaju aktu, organu i tytułu (ok. 1000 —
dokumenty spoza szablonu RCL, w których pogrubienie nadano wprost, a nie stylem akapitu). Kursywa całego akapitu to
tytuły „WZÓR” i nagłówki załączników. Te same wiersze w dokumentach z szablonu dostają pogrubienie ze stylu akapitu,
które ADR-0117 wyklucza — wynik zależałby od sposobu redagowania pliku, nie od treści. PDF stylów nie zna.

Wyróżnienie w tekście — łacina („de minimis”), tytuł aktu, który utracił moc (§ 108a ZTP), zmienna wzoru, pogrubione
wyrazy — dotyczy części akapitu. Kursywy całego zdania z § 108a w korpusie nie ma.

## Decyzja

- Kursywa i pogrubienie, które obejmują cały akapit źródła (wszystkie jego znaki poza odstępami i grupami kanału
  „[…]”), są typografią wiersza — jak format ze stylu akapitu — i do treści nie wchodzą
  (`InlineFormattingXml.Source`). Do treści wchodzi formatowanie części akapitu.
- Indeks dolny jest treścią zawsze, także na całym akapicie.
- Pogrubienie przepisu w nowym brzmieniu tekstu jednolitego zostaje wyrażone strukturą (`okres`, `wejscie-w-zycie`
  jednostki), nie elementem `b`.

## Odrzucone alternatywy

- **Każde formatowanie przebiegu, także na całym akapicie.** Tysiące `b` na nagłówkach tabel, numerach „lp.”
  i wierszach nagłówka aktu, różne dla tego samego wiersza zależnie od tego, czy pogrubiono go stylem czy wprost;
  w PDF nie do odtworzenia tą samą regułą.
- **Wykluczanie po rodzaju elementu** (bez `b` w `rodzaj-aktu`, `liczba`, nagłówkach tabel). Lista miejsc rosłaby
  z każdym nowym rodzajem akapitu, a pogrubione wyrazy w środku komórki tabeli też by odpadły.

## Konsekwencje

- Akapit w całości kursywą (np. cały przepis) nie dostaje `i`; gdyby autorzy schematu oczekiwali typografii wiersza,
  reguła jest w jednym miejscu (`InlineFormattingXml.ParagraphTypography`).
- Rozbieżność z przykładem autorów (kursywa „de minimis” pominięta w DU 2026/252) zostaje — pytania 35 i 36.

## Weryfikacja

- `InlineFormattingTests.BoldOfTheWholeParagraph_IsItsTypography_NotABElement`,
  `InlineFormattingTests.ItalicAndBoldOfTheRun_StandInIAndB`
