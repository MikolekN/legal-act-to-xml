# ADR-0060: Jednostka drukowana w tekście jednolitym dwa razy to dwie wersje czasowe — to samo eId, różny okres

- Status: Accepted (dzień zmiany nazwany przy akcie zmieniającym, w innym odnośniku, jest już czytany — [ADR-0062](0062-termin-zmiany-zapisany-przy-akcie-zmieniajacym-obejmuje-jego-zmiany.md))
- Data: 2026-10-12

## Kontekst

Tekst jednolity zamieszcza jednostkę dwa razy, gdy jej zmiana nie weszła jeszcze w życie (§ 106a ZTP): w brzmieniu
obowiązującym — z odnośnikiem „W tym brzmieniu obowiązuje do wejścia w życie zmiany, o której mowa w odnośniku …",
a przed uchyleniem „Obowiązuje do wejścia w życie zmiany …" — i w brzmieniu przyszłym, z odnośnikiem o zmianie
(§ 106 pkt 2–5: „W brzmieniu ustalonym przez …", „Ze zmianą wprowadzoną przez …", „Dodany przez …", a przy
„(uchylony)" — „Przez …"). Obie kopie mają to samo oznaczenie, więc to samo eId, i wychodziły jako dwie zwykłe
jednostki, niczym nierozróżnione: 591 powtórzonych jednostek w tekstach jednolitych korpusu.

Schemat opisuje ten przypadek wprost (sekcja „CZAS JEDNOSTKI", `IdCzasowyTyp`, grupa atrybutów `wejscie-w-zycie`,
`Zdarzenie`): to samo eId nie jest defektem, kluczem dokumentu jest para (`eId`, `okres`); przedział wskazuje
zdarzenia, nie daty; jest półotwarty, więc koniec wersji obowiązującej i początek przyszłej to **to samo
zdarzenie**; zdarzenie może nie mieć daty; stan jednostki nie ma wartości „obowiązuje".

## Decyzja

**Jednostka tekstu jednolitego niesie wersję czasową (`BaseEntity.Version`), a w eksporcie — `okres` wskazujący
grupę czasową ograniczoną zdarzeniem zmiany.**

- **Rozpoznanie** (`WordingVersions.Assign`, po zbudowaniu dokumentu, tylko dla tekstu jednolitego) łączy trzy
  sygnały:
  1. *miejsce znacznika* — odnośnik o wersji stoi zaraz za oznaczeniem jednostki („1.[7)] …", „(uchylony)[8)]");
     za oznaczeniem artykułu („Art. 5.[7)] 1. …") dotyczy całego artykułu;
  2. *treść odnośnika* — wzór § 106a ust. 2 bez podmiotu to brzmienie obowiązujące; wzór § 106 pkt 2–5 albo
     „Wejdzie w życie …" w czasie przyszłym — brzmienie przyszłe. Odnośnik z podmiotem („Zdanie drugie w tym
     brzmieniu obowiązuje …") mówi o fragmencie jednostki i wersji jednostki nie tworzy;
  3. *układ* — następna jednostka o tym samym oznaczeniu, z własnym odnośnikiem, stojąca po brzmieniu
     obowiązującym, jest brzmieniem przyszłym także wtedy, gdy jej odnośnik o wejściu w życie nie mówi
     („W brzmieniu ustalonym przez art. 2 ustawy, o której mowa w odnośniku 8").

  Jednostki podrzędne dzielą wersję jednostki nadrzędnej. Wersje wiąże sąsiedztwo, nie numer wskazany w odnośniku.
- **Zdarzenie** (`LifecycleEvents`): zmiana jest zdarzeniem `wejscie-w-zycie`, a gdy wersją przyszłą jest
  „(uchylony)" — `uchylenie`; `pochodzenie="odczytane"`, `dowod` = odnośnik, z którego je odczytano. Dzień wchodzi
  do `data` i do identyfikatora (`zdarz_wejscie_2026-01-01_…`); gdy odnośnik dnia nie podaje, zdarzenie nie ma
  daty, w identyfikatorze stoi rozróżnik `odn-N`, a termin zapisany prozą („z dniem wdrożenia rozwiązań
  technicznych …") przechodzi dosłownie do `warunek`.
- **Grupa czasowa**: brzmienie obowiązujące — `okr_obowiazywanie.x.<zdarzenie>` (przedział z samym `do`), przyszłe
  — `okr_obowiazywanie.<zdarzenie>.x` (z samym `od`). `okres` niesie jednostka i cała jej treść. Projekcje na
  daty: `obowiazuje-do` na brzmieniu obowiązującym, `wejscie-w-zycie` na przyszłym; jednostka uchylana ma
  `stan-jednostki="uchylony"` i nie ma `wejscie-w-zycie`.
- Jednostka drukowana raz nadal nie niesie dat, a główną grupą zostaje ujednolicenie
  ([ADR-0033](0033-tekst-jednolity-ma-zdarzenie-ujednolicenia.md)).

## Odrzucone alternatywy

- **Rozróżnić eId** (sufiks wersji). Schemat mówi wprost, że obie kopie mają to samo eId; powołania z innych
  aktów wskazują jednostkę, nie jej wersję.
- **Wiązać wersje numerem z treści odnośnika** („o której mowa w odnośniku 8"). Numer drukowany bywa inny niż
  numer, pod którym odnośnik czytamy (numeracja wznawiana od sekcji dokumentu), a powtórzony znacznik jest
  w źródle zwykłym tekstem.
- **Brać dzień z odnośnika, do którego odsyła odnośnik zmiany** („ustawy, o której mowa w odnośniku 8", a tam
  „…, która wejdzie w życie z dniem …"). To odczyt grafu odnośników; do czasu jego zbudowania zdarzenie zostaje
  bez daty — granica istnieje, dnia nie znamy — co schemat przewiduje.
- **Pogrubienie jako sygnał** (§ 106a ust. 4 każe składać brzmienie przyszłe pogrubioną czcionką). Kanał bloków
  nie niesie kroju; odnośnik i układ wystarczają, a pogrubienie zostaje możliwym trzecim sygnałem.
- **`wejscie-w-zycie` na „(uchylony)".** Jednostka uchylana nie wchodzi w życie; mówią o niej okres i stan.

## Konsekwencje

- W korpusie zmienia się 112 z 728 tekstów jednolitych — wyłącznie atrybuty czasu jednostek i dane czasowe (607 grup czasowych, 362 zdarzenia: 140 z dniem, 99 z terminem zapisanym prozą, 123 bez dnia; 501 jednostek z `obowiazuje-do`, 96 ze `stan-jednostki`). Z 591 jednostek o powtórzonym eId 477 rozróżnia okres. Zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory); treść bez zmian.
- Pole `<akt>` identyfikatora zdarzenia niesie pozycję dziennika tekstu jednolitego, w którym zdarzenie
  odczytano. Przykład w dokumentacji schematu (`zdarz_uchylenie_2026-04-18_DU-2015-1634_odczytane`) stawia tam
  akt sprawczy; jego tożsamość, przepis sprawczy (`podstawa`) i źródło (`zrodlo`) leżą w grafie odnośników —
  identyfikatory zmienią się, gdy go odczytamy.
- Poza zakresem zostają: wersje fragmentu jednostki (zdanie, wprowadzenie do wyliczenia, część wspólna), wersje
  jednostek systematyzacyjnych, trzy wersje jednej jednostki, zmiana oznaczenia („oznacza się jako") oraz kopie,
  przy których źródło nie stawia odnośnika — wychodzą jak dotąd.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0060_UnitPrintedTwice_IsTwoWordingsToldApartByThePeriod`
- `WordingVersionTests` — rozpoznanie (ustęp, artykuł, uchylenie, kopia bez dnia, jednostka dodawana, termin
  prozą, odnośniki niebędące wersją), eksport (okres, grupy, zdarzenia, stan), zgodność ze schematem.
