# ADR-0068: Skreślenie znaku jest zmianą w tekście, a nie uchyleniem jednostki — o operacji na jednostce rozstrzyga zdanie bez zmian w tekście

- Status: Accepted
- Data: 2026-10-13

## Kontekst

ZTP rozdzielają dwa czasowniki: jednostki redakcyjne się *uchyla* (§ 85 ust. 3), a wyrazy, liczby i znaki się
*skreśla* (§ 87 ust. 3 pkt 3). Znak skreślany jest nazywany, nie cytowany: „skreśla się przecinek".

Akty sprzed 2009 r. — wciąż liczne w powołaniach — używały „skreśla się" także dla jednostek („skreśla się ust. 3"),
więc `AmendmentFinalizer.RepealPattern` zna oba czasowniki. Skreślenie wyrazów ma własną komendę
([ADR-0053](0053-skreslenie-wyrazow-to-zamiana-na-pusty-tekst.md)); skreślenie znaku jej nie miało i całe zdanie
było czytane jako uchylenie jednostki, w której znak stoi:

- „w art. 16 skreśla się przecinek i wyrazy „o której mowa w art. 11 ust. 2 pkt 8"" → `now:uchyl` art. 16;
- „w pkt 8 skreśla się przecinek i uchyla się pkt 9–11" → `now:uchyl` pkt 8 zamiast pkt 9–11;
- „w lit. c skreśla się średnik i dodaje się część wspólną w brzmieniu:" → `now:uchyl` lit. c, a cytowana część
  wspólna ginęła z eksportu, bo uchylenie nie niesie treści.

## Decyzja

**„skreśla się" z nazwą znaku jest zmianą w tekście** (`AmendmentCommandParser.DeleteSignPattern`), czytaną przed
czasownikiem uchylenia:

- **znak skreślany sam** („… skreśla się przecinek i uchyla się pkt 9–11", „… skreśla się średnik i dodaje się …
  w brzmieniu:") jest znakiem kończącym jednostkę: `AmendmentCommandKind.ReplaceSign` z pustym tekstem
  zastępującym, `Occurrence = Last` i własnym celem — jak zamiana znaku
  ([ADR-0054](0054-zamiana-znaku-konczacego-jednostke-ma-wlasny-cel.md));
- **znak skreślany razem z wyrazami stojącymi za nim** („skreśla się przecinek i wyrazy „X"") to jedno skreślenie
  tekstu „, X" (`DeleteWords`) — znak jest tym sprzed wyrazów, nie ostatnim w jednostce;
- **znak, w którego miejsce dodaje się wyrazy** („skreśla się przecinek i dodaje się wyrazy „Y"") to zamiana
  ostatniego znaku na te wyrazy.

**O operacji na jednostce (uchylenie, dodanie, zmiana brzmienia) rozstrzyga zdanie bez zapisanych w nim zmian
w tekście** (`AmendmentCommandParser.WithoutTextChanges`) — w obu miejscach, które ją czytają:
`AmendmentStateManager.DetectCommands` i `AmendmentFinalizer.Finalize`. Czasownik zmiany w tekście ani wyrazy
w cudzysłowie nie mogą udawać komendy o jednostce.

## Odrzucone alternatywy

- **Zawęzić `RepealPattern`, żeby „skreśla się" wymagało jednostki za czasownikiem.** Przedmiot komendy bywa przed
  czasownikiem („ust. 3 skreśla się"), a wzorzec służy w kilku miejscach jako sam sygnał komendy. Wiedza o tym, co
  jest zmianą w tekście, siedzi już w rozbiorze komend — i tam zostaje.
- **Znak i wyrazy jako dwie zmiany** (ostatni przecinek + skreślenie wyrazów). Przecinek stoi przed wyrazami,
  nie na końcu jednostki; dwie instrukcje tego nie mówią, jedna z tekstem „, X" — tak.
- **Spacja przed wyrazami dodawanymi w miejsce znaku.** `nowy-tekst` ma typ `xs:token` — wiodący odstęp nie
  przetrwa odczytu.

## Konsekwencje

- W korpusie zmienia się 13 zdań w 10 plikach: przybywa 13 instrukcji `now:zamien-tekst`, znika 10 fałszywych `now:uchyl` jednostki, w której skreślano znak, 2 uchylenia wskazują teraz jednostkę uchylaną („pkt 9–11", „pkt 2"), a 2 cytowane części wspólne wracają do eksportu (jedno rozporządzenie w dwóch plikach). Zgubionych akapitów zostaje w całym korpusie 1 (przedtem 3). Zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory).
- Zdanie z dwiema częściami o różnych jednostkach zachowuje reguły ADR-0054: uchylenie za skreślonym znakiem bierze
  cele z części zdania po komendzie, dodanie zostaje przy jednostce sprzed znaku.
- Skreślenie zdania („skreśla się zdanie drugie") i czasownik w przepisie merytorycznym („Studenta skreśla się
  z listy studentów") nie są częścią tej decyzji — patrz `docs/backlog.md`.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0068_DeletedSign_IsATextChange_AndTheUnitKeepsItsOwnOperation`
- `AmendmentCommandParserTests.DeleteSignCommand_IsATextChange`, `DeletingAUnit_IsNotADeletedSign`;
  `AmendmentTargetsExportTests` — znak z wyrazami, znak przed uchyleniem, znak przed dodaniem treści cytowanej.
