# ADR-0053: Skreślenie wyrazów to zamiana na pusty tekst, a każda zmiana w tekście zdania ma własną instrukcję

- Status: Accepted
- Data: 2026-10-12

## Kontekst

[ADR-0052](0052-zmiana-wyrazow-jest-instrukcja-zamien-tekst.md) zapisuje zmianę wyrazów instrukcją `now:zamien-tekst`. Komenda „skreśla się wyrazy
„X"" (340 w korpusie) nie dawała żadnej instrukcji — tylko ostrzeżenie, że model jej nie odwzorowuje.

Przykłady autorów schematu zapisują skreślenie na dwa sposoby: DU/2026/252 — `now:zamien-tekst` z pustym
`nowy-tekst` (4 razy), DU/2026/26 — `cytat-w-tekscie` z funkcją `uchyl-tekst` obejmujący skreślane wyrazy
w tekście zdania (raz).

Kilkanaście skreśleń stoi w jednym zdaniu z inną zmianą w tekście („skreśla się wyrazy „X" i po wyrazie „Y"
dodaje się wyrazy „Z""), a rozpoznawana była tylko pierwsza zmiana zdania.

## Decyzja

- **Skreślenie wyrazów jest zmianą w tekście na pusty tekst**: `TextChange` z pustym `NewText`, w eksporcie
  `now:zamien-tekst` z `nowy-tekst=""`. Kolejne wyrazy w cudzysłowie dołączone spójnikiem („skreśla się wyrazy
  „X" oraz „Y"") to kolejne skreślenia.
- **Każda zmiana w tekście zapisana w zdaniu ma własną instrukcję**, w kolejności zapisu
  (`AmendmentCommandParser.TextChangeCommands`, `AmendmentStateManager.HandleTextChanges`); wszystkie dotyczą
  celów wskazanych w zdaniu.
- Zmiana, której zapisać się nie da (wyrazy dodawane bez wskazanego miejsca), nie blokuje pozostałych: zostają
  rozpoznane, a jednostka dostaje ostrzeżenie.

## Odrzucone alternatywy

- **`cytat-w-tekscie` z `uchyl-tekst` wokół skreślanych wyrazów.** Wymaga pocięcia tekstu zdania wokół cytatu
  i przeniesienia cudzysłowów do atrybutu; skreślenie miałoby inny model niż zamiana i dodanie, choć wszystkie
  trzy zmieniają tekst jednostki. Przykłady używają tej postaci raz, pustego `nowy-tekst` — cztery razy.
- **Osobna nowelizacja albo osobny cel dla każdej zmiany zdania.** W korpusie zmiany jednego zdania dotyczą tej
  samej jednostki (między komendami nie stoi żadne wskazanie jednostki).

## Konsekwencje

- W korpusie przybywa 379 instrukcji `now:zamien-tekst` w 127 plikach; poza tym eksport się nie zmienia, a zgodnych ze schematem jest nadal 1937 z 2321. Przykłady autorów schematu: DU/2026/26 — 7 z 9 instrukcji, DU/2026/252 — 70 z 83; poza dwoma skreśleniami (inna postać w DU/2026/26, dodany odstęp w DU/2026/252) każda nasza para (`stary-tekst`, `nowy-tekst`) ma w przykładzie odpowiednik.
- Wyrazy skreślane są przepisywane dosłownie z cudzysłowu. Przykład DU/2026/252 ma w jednym miejscu dodany
  odstęp („ i 3"), którego w źródle nie ma — tego nie odtwarzamy.
- Gdyby zmiany jednego zdania wskazywały różne jednostki („w ust. 1 wyrazy …, a w ust. 2 wyrazy …"), każda
  instrukcja dostałaby wszystkie cele zdania; `TextChange` nie ma własnego celu.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0053_DeletedWordsAndEveryTextChangeOfTheSentence_AreInstructions`
- `AmendmentCommandParserTests` — skreślenie, wszystkie zmiany akapitu w kolejności; `AmendmentTargetsExportTests`
  — pusty `nowy-tekst`, kilka instrukcji z jednego zdania, kilka skreślanych fraz, ostrzeżenie przy zmianie
  niezapisywalnej.
