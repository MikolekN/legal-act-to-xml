# ADR-0069: Czasownik uchylenia jest komendą w artykule zmieniającym albo z uchylaną jednostką zaraz za nim

- Status: Accepted
- Data: 2026-10-13

## Kontekst

„Uchyla się" i „skreśla się" to nie tylko zwroty komendy nowelizacyjnej. Należą do zwykłego języka przepisów:
„kto uchyla się od wykonania obowiązku", „decyzję uchyla się, jeżeli…", „tymczasowe zawieszenie uchyla się
niezwłocznie", „studenta skreśla się z listy studentów", „nie uchyla się decyzji w całości".

`AmendmentStateManager` uznawał za uchylenie każdy przepis, w którym stał sam czasownik
(`AmendmentFinalizer.RepealPattern`). Przepis merytoryczny dostawał więc instrukcję `now:uchyl`: z celem
`UNKNOWN` albo — gorzej — z jednostką, którą tylko powoływał („Uchyla się, z zastrzeżeniem ust. 2, przepisy
ustaw szczególnych…" → uchylenie ust. 2 tego samego aktu). Instrukcję dostawało też zdanie stojące obok
w tej samej jednostce. Dotyczyło to prawie wyłącznie tekstów jednolitych, bo tylko one mają w korpusie długie
części merytoryczne — ale przepis merytoryczny może stać w każdym akcie.

ZTP zapisują uchylenie jednym wzorem: „W ustawie … (tytuł ustawy) uchyla się art. …" (§ 85 ust. 3, § 41 ust. 2) —
przepis zmieniający, a w nim czasownik i zaraz za nim to, co się uchyla.

## Decyzja

**Czasownik uchylenia jest komendą, gdy zgadza się z nim drugi sygnał** (`AmendmentStateManager.IsRepealCommand`):

- **miejsce** — przepis stoi w artykule zmieniającym inny akt (`Article.IsAmending`: formuła „W ustawie z dnia …"
  z publikatorem); przedmiotem bywa wtedy także to, czego adresu nie znamy („skreśla się lp. 7", „uchyla się wiersz
  oznaczony lp. 16"), **albo**
- **przedmiot** — zaraz za czasownikiem stoi jednostka aktu z oznaczeniem
  (`AmendmentCommandParser.RepealsNamedUnit`: „uchyla się ust. 3", „uchyla się zdanie drugie", „uchyla się
  rozdział 2a"), czytana wspólnym słownikiem powołań ([ADR-0015](0015-jedno-zrodlo-powolan-jednostek.md)).

**Czasownik w zwykłym znaczeniu nie jest nawet kandydatem** (`AmendmentCommandParser.HasRepealVerb`): „uchyla się
od …" (uchylać się od czegoś), „skreśla się z/ze …" (skreślić kogoś skądś) i przeczenie „nie uchyla się". W komendzie
przedmiot stoi zaraz za czasownikiem; przyimek albo przeczenie znaczą, że komendy tu nie ma — także w artykule
zmieniającym.

Jednostka powołana gdzie indziej w zdaniu („o której mowa w ust. 10") przedmiotem czasownika nie jest. Nazwa
jednostki bez oznaczenia to zwykły rzeczownik („tytuł wykonawczy", „część decyzji").

## Odrzucone alternatywy

- **Wyłączyć komendy w tekstach jednolitych.** Trafia w korpus, nie w przyczynę: przepis merytoryczny z tym
  czasownikiem może stać w ustawie pierwotnej, a tekst jednolity może zawierać przepis uchylający.
- **Lista wyjątków („od", „z listy", „decyzję", „niezwłocznie" …).** Nie ma końca — przedmiotem i okolicznikiem
  może być cokolwiek. Sama lista przyimków zostaje tylko jako to, co o czasowniku wiadomo z języka.
- **Wymagać jednostki za czasownikiem zawsze.** Gubi uchylenia części załączników w aktach zmieniających
  (wiersz tabeli, pozycja, rysunek) — tam o komendzie świadczy miejsce.
- **Uznać za sygnał cele wykryte w przepisie.** Cele czyta się z każdego powołania („o której mowa w art. 113
  ust. 1 i 2"), więc ma je też przepis merytoryczny.

## Konsekwencje

- W korpusie znika 125 fałszywych `now:uchyl` w 55 tekstach jednolitych (m.in. 69 × „uchyla się od …", 21 × „skreśla się z/ze …", 16 w zdaniach stojących obok takiego zdania w tej samej jednostce); 89 z nich miało cel `UNKNOWN`, 36 wskazywało jednostkę tylko powołaną w przepisie. Żadna komenda aktu zmieniającego nie znika: instrukcji `now:uchyl` zostaje 1033 w 897 zdaniach. Zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory).
- Uchylenie części załącznika w artykule, który nie zaczyna się formułą zmieniającą („W załączniku nr 2 do
  rozporządzenia … uchyla się lp. 5"), nie jest komendą, dopóki `JournalReferenceService` tej formuły nie zna.
- Ogólny przepis uchylający bez wskazanej jednostki („Uchyla się … przepisy ustaw szczególnych") nie daje instrukcji —
  nie ma czego wskazać.
- Inne zwroty komend („dodaje się", „otrzymuje brzmienie") w przepisie merytorycznym nie są objęte tą decyzją.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0069_RepealVerbInAnOrdinaryProvision_IsNotACommand`
- `RepealCommandRecognitionTests` — przepisy merytoryczne, zdanie obok, jednostka za czasownikiem poza formułą
  zmieniającą, część załącznika w artykule zmieniającym, czasownik w zwykłym znaczeniu, przedmiot czasownika.
