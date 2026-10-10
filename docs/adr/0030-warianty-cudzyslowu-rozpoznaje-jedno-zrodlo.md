# ADR-0030: Warianty cudzysłowu rozpoznaje jedno źródło, tekstu się nie normalizuje

- Status: Accepted
- Data: 2026-10-10

## Kontekst

Cudzysłów decyduje o strukturze: wyznacza granicę treści cytowanej (§ 94 ZTP), domyka bilans cytatu
w dokumencie bez stylów, oddziela wyrazy w komendzie „wyrazy „…" zastępuje się wyrazami „…"", a jako
prefiks oznaczenia („„1. …") odróżnia jednostkę cytowaną od jednostki aktu.

Akty zapisują go kilkoma znakami: otwierający `„` (U+201E), zamykający `”` (U+201D), niedomyślny wariant
edytorów `“` (U+201C), prosty `"` (U+0022) w kanale TXT/PDF, a także `ˮ` (U+02EE) — literę modyfikującą
wstawianą z klawiatury zamiast `”` (5 wystąpień w 4 dokumentach korpusu).

Każde z dziesięciu miejsc, które czytały cudzysłów, miało własny zestaw znaków: bilans cytatu znał z zamykających
`”` i `“`, a zdejmowanie granicy cytatu, podział na zdania i wejście w życie — tylko `”`. Wariantu `ˮ` nie znało
żadne. Skutek: w dwóch ustawach cytat zamknięty tym znakiem bez stylów nie kończył się wcale
(następne przepisy aktu trafiały do cytatu), a ze stylami znak zostawał w treści cytowanej jednostki.

## Decyzja

**Zestawy znaków cudzysłowu trzyma wyłącznie `QuotationMarks` (`Services/QuotationMarks.cs`); kod, który
decyduje o strukturze, bierze je stamtąd. Tekst źródła pozostaje nietknięty.**

- `QuotationMarks.Opening`, `Closing`, `Straight` oraz gotowe fragmenty klas znaków do wzorców
  (`OpeningOrStraight`, `ClosingOrStraight`, `Any`).
- Nowy wariant dopisuje się w jednym miejscu i działa od razu w bilansie cytatu, na granicy cytatu,
  w komendach wyrazowych, w prefiksie oznaczenia i w podziale na zdania.

## Odrzucone alternatywy

- **Normalizacja przy wczytaniu** (zamiana `ˮ` i `“` na `”` w kanale tekstu). Odpada, bo eksport niesie
  treść aktu znak w znak: miara pokrycia tekstu i porównanie z przykładami autorów schematu zestawiają
  zdania co do znaku, a cudzysłów wewnętrzny („zwany dalej „PCOC”") jest częścią przepisu. Normalizacja
  zmieniałaby treść prawa po cichu i nieodwracalnie.
- **Dopisanie `ˮ` w miejscach, w których dziś przeszkadza.** Naprawia dwa dokumenty i zostawia dziesięć
  rozjeżdżających się zestawów — następny wariant powtórzyłby tę samą pracę i te same pominięcia.

## Konsekwencje

- Miejsca, które dotąd znały mniej wariantów, znają teraz wszystkie: granica cytatu schodzi także po `“`
  i `ˮ`, a prefiks oznaczenia jednostki cytowanej dopuszcza każdy cudzysłów.
- Znaki spoza zestawu (`»`, `«`) zostają tam, gdzie były rozpoznawane lokalnie — nie są cudzysłowem
  polskiego aktu i nie wyznaczają granicy cytatu.
- Odczyt bloków (`Ingest`) nie zależy od `Services`: heurystyka łączenia wierszy PDF trzyma swój znak
  otwierający i nie jest objęta strażnikiem.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0030_QuotationMarkCharacters_AreDefinedInExactlyOnePlace` — literał
  znakowy, klasa znaków wzorca albo `\uXXXX` z cudzysłowem typograficznym poza `QuotationMarks.cs`.
- `ArchitectureDecisionTests.Adr0030_EveryClosingVariant_ClosesTheQuotationAndIsTakenOffItsEdge`.
- `AmendmentTextualBoundaryTests.QuoteClosedWithTheModifierLetter_EndsTheAmendment`.
