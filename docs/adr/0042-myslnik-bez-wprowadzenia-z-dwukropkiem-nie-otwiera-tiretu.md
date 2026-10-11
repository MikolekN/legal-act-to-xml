# ADR-0042: Myślnik pod ustępem bez wprowadzenia zakończonego dwukropkiem jest dalszym ciągiem przepisu, nie tiretem

- Status: Accepted
- Data: 2026-10-11

## Kontekst

Akapit zaczynający się od myślnika klasyfikator czyta jako tiret. Gdy stoi wprost pod ustępem — bez punktu
i litery — buildery dostawiają oba brakujące poziomy (kaskada), a eksport oznacza ustęp i tiret jako
jednostki spoza ZTP (`nie-ztp-ustep`, `nie-ztp-tiret`).

W korpusie taki układ ma 17 ustępów i są to dwie różne rzeczy:

- **8 wyliczeń** (2–17 myślników) po wprowadzeniu zakończonym dwukropkiem („… w dotychczasowym stopniu:",
  „… jeżeli stwierdzono:") — rzeczywiste wyliczenie myślnikami z pominięciem punktów i liter;
- **9 pojedynczych akapitów**, które wyliczeniem nie są: dalszy ciąg zdania przeniesiony do nowego wiersza
  („2d. Za niewykonanie obowiązku, o którym mowa w art. 13ia ust. 10 i art. 13ib ust. 2" i w nowym wierszu
  „– wymierza się karę pieniężną w wysokości 1000 zł.") albo zdanie za wzorem, legendą lub tabelą („– gdzie
  poszczególne symbole oznaczają:", „– gdzie x oznacza cenę hurtową …"). Żaden nie ma stylu tiretu — stoją
  w stylach ustępu, litery, części wspólnej i legendy wzoru.

Ta sama konstrukcja złożona stylem sankcji (`S_KARN`) jest już dalszym ciągiem zdania
(`StructureProcessor.TryHandleSanction`); o tym, czy akapit został tiretem, decydował więc styl, w jakim
redaktor go złożył.

## Decyzja

**Akapit zaczynający się od myślnika, stojący pod ustępem bez otwartego punktu i litery, otwiera tiret tylko
wtedy, gdy poprzedza go wprowadzenie zakończone dwukropkiem (§ 56 ZTP) albo gdy sam ma styl tiretu. W innym
razie jest dalszym ciągiem przepisu** (`StructureProcessor.TryContinueAfterDash`):

- gdy zdanie ustępu jest urwane (bez znaku końcowego) i nic po nim nie stoi — akapit je domyka, jak sankcja;
- gdy stoi za wzorem — przechodzi przez `FormulaLegend` (wprowadzenie do opisu symboli albo opis symbolu);
- w pozostałych przypadkach (za legendą, tabelą, zdaniem zakończonym kropką) — jest dalszą treścią ustępu.

„Poprzedza" znaczy: ostatni tekst przed akapitem — treść ustępu albo ostatni wiersz jego dalszej treści
(wiersz legendy „Sw – oznacza:" otwiera wyliczenie myślnikami tak samo jak wprowadzenie ustępu).

## Odrzucone alternatywy

- **Zwinięcie pojedynczego tiretu po zbudowaniu ustępu** („wyliczenie jednoelementowe nie jest wyliczeniem").
  Rozstrzyga liczba, nie przyczyna: wyliczenie z brakującym dwukropkiem zostałoby tiretami, gdy ma dwie
  pozycje, i tekstem, gdy ma jedną. Wymaga też cofania zbudowanego modelu (adresy, cele nowelizacji).
- **Czwarty sygnał sankcji** („zaczyna się od myślnika"). Sankcję rozpoznaje sformułowanie i styl; myślnik za
  wzorem albo tabelą sankcją nie jest, a reguła musiałaby objąć i jego.
- **Zostawić tiret** (stan dotychczasowy). Dostawia dwa poziomy, których w akcie nie ma, i oznacza zwykły
  ustęp jako jednostkę spoza ZTP z powodu złamania wiersza.
- **Ta sama reguła na każdym poziomie** (myślnik po literze bez dwukropka). Po punkcie i literze bez znaku
  końcowego myślnik jest już częścią wspólną (§ 57 ust. 3 ZTP); po pozycji zakończonej przecinkiem albo
  średnikiem bywa legendą lub pozycją tabeli i kontekst tego nie rozstrzyga — tam zostaje, jak było.

## Konsekwencje

- W korpusie zmienia się 9 dokumentów: 9 ustępów przestaje być `nie-ztp-ustep`, znika 9 `nie-ztp-tiret`;
  w 4 dokumentach akapit „– gdzie poszczególne symbole oznaczają:" staje się wprowadzeniem do opisu symboli,
  a 12 wierszy za nim — opisami symboli (`opis`) zamiast `fragment`. Jednostek `nie-ztp-*`: 618 → 600.
  Zgodność ze schematem bez zmian. Osiem wyliczeń myślnikami pozostaje tiretami.
- Wyliczenie myślnikami wprost w ustępie, którego wprowadzenie nie kończy się dwukropkiem i które nie ma
  stylu tiretu, wyjdzie jako dalsza treść ustępu — tekst zostaje, struktura nie. W korpusie takiego nie ma.
- Reguła nie dotyczy treści cytowanej w nowelizacji (`AmendmentBuilder` ma własny tok) ani myślnika pod
  punktem i literą.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0042_DashWithoutAColonIntroduction_ContinuesTheParagraph_UnlessStyledAsATiret`
- `DashWithoutEnumerationTests` — urwane zdanie (w stylu ustępu, litery i bez stylu), wyliczenie po dwukropku,
  styl tiretu, myślnik za wzorem i za wierszem legendy z dwukropkiem, zdanie zakończone, część wspólna po
  punkcie.
