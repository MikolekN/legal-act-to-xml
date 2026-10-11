# Changelog

Wszystkie istotne zmiany w tym projekcie będą dokumentowane w tym pliku.

Format oparty jest na Keep a Changelog,
a wersjonowanie zgodne z Semantic Versioning.

## [Unreleased]
### Dodano
- **Jednostki systematyzacyjne w eksporcie XML**: część, księga, tytuł, dział, rozdział i oddział
  wychodzą jako zagnieżdżone elementy schemaPL z `eId` i nagłówkiem (`nr`, `p`). Pod jawną jednostką
  brakujące poziomy są elementami `-n` z `brak-eid="jednostka-nieoznaczona"`. Akty bez jawnych
  jednostek mają XML bez zmian — [ADR-0020](docs/adr/0020-eksport-jednostek-systematyzacyjnych.md).
- **Jednostki systematyzacyjne w treści nowelizacji**: „dodaje się rozdział 9a w brzmieniu:" daje
  w `cytat-strukt` rozdział z nagłówkiem i artykułami w `oddzial-n`, zamiast oznaczenia i tytułu jako
  cytatu tekstowego obok artykułów.
- **Cele nowelizacji wskazane jednostką systematyzacyjną**: „po rozdziale 9 dodaje się…", „w dziale II
  po rozdziale 9…", „uchyla się rozdziały 4 i 5" dają `element-eId` `rozdz_9`, `dzial_2__rozdz_9`,
  `rozdz_4`/`rozdz_5` zamiast `UNKNOWN`.
- **Wyjątek wejścia w życie wskazujący jednostkę systematyzacyjną**: „…, z wyjątkiem art. 25 ust. 1 pkt 8
  i rozdziału 4, które wchodzą w życie…" datuje rozdział 4 i wszystko w nim. Wcześniej gołe „4" czytało się
  jako kolejny punkt — rozdział zostawał z datą ogólną, a punkt 4 dostawał datę wyjątku.
- **Strefa tytułowa aktu w eksporcie**: po `metadane` stoją `rodzaj-aktu`, `data` i `tytul` z wierszy aktu
  („USTAWA", „z dnia 14 czerwca 2024 r.", „o ochronie sygnalistów"); model trzyma je osobno
  (`LegalDocument.KindLine`, `DateLine`, `Subject`).
- **Tytuł zmieniający jako przedmiot aktu** (§ 96 ZTP): „zmieniająca ustawę …" i „zmieniające
  rozporządzenie …" trafiają do tytułu aktu i elementu `tytul`; wcześniej tytuł kończył się na dacie.
- **Walidacja numeracji jednostek systematyzacyjnych**: powtórzone oznaczenie, luka, malejąca
  numeracja, oddział poza rozdziałem i jednostka bez tytułu dostają komunikat walidacji.
- **Podpis aktu**: formuła za ostatnim przepisem („Prezydent Rzeczypospolitej Polskiej: A. Duda") trafia do modelu
  (`LegalDocument.Signature`) i eksportu — `organ-podpisujacy` z `osoba` (`imie`, `nazwisko`) na końcu aktu oraz organ
  o roli `podpisujacy` w metadanych. Wcześniej ginęła jako „pominięty akapit nierozpoznany" ostatniego artykułu.
- **Odnośniki (przypisy)**: treść przypisów z pliku Word trafia do modelu (`LegalDocument.Footnotes`), a eksport
  osadza ją jako `odnosnik` w miejscu odwołania — w zdaniu i w tytule aktu, jak w przykładach autorów schematu.
  Kolejne odwołanie do tego samego odnośnika (częste w tekstach jednolitych) to `odsylacz-do-odnosnika`.
  Znaczniki `[N)]` znikają z treści zdań, tytułu i metadanych `Tytul` —
  [ADR-0026](docs/adr/0026-odnosnik-tresc-w-rejestrze-miejsce-w-tekscie.md).

### Zmieniono
- **eId jednostek redakcyjnych nie zawiera już ścieżki jednostek systematyzacyjnych**: artykuł
  w rozdziale 1 ma `eId="art_5"` zamiast `rozdz_1__art_5` (tak samo ustępy, punkty, litery
  i tirety). Adres jednostki jest teraz tym samym napisem co `element-eId` odsyłającej do niej
  nowelizacji. Zmienia wyjście XML każdego aktu z rozdziałami, działami lub innymi jawnymi
  jednostkami systematyzacyjnymi — [ADR-0017](docs/adr/0017-eid-jednostki-redakcyjnej-bez-sciezki-systematyzacyjnej.md).
- **Oznaczenie w eId**: jednostki numerowane cyframi rzymskimi albo słownie dostają liczbę arabską
  (`KSIĘGA PIERWSZA` → `ksi_1`, `DZIAŁ II` → `dzial_2`) — [ADR-0018](docs/adr/0018-postac-oznaczenia-w-eid.md).
- **eId jednostek cytowanych w nowelizacji zaczyna się od adresu celu** w akcie zmienianym:
  punkt dodawany „w art. 68 ust. 1" ma `eId="art_68__ust_1__pkt_5"` zamiast `pkt_5` —
  [ADR-0021](docs/adr/0021-eid-jednostki-cytowanej-od-adresu-celu.md).
- **Załącznik aktu nie jest już częścią ostatniego artykułu**: treść za nagłówkiem „Załącznik do
  ustawy z dnia…" nie trafia do struktury aktu (wcześniej jej punkty i rozdziały wchodziły do
  ostatniego artykułu albo stawały się jednostkami aktu).
- **Jednostki spoza ZTP pod nazwami schematu**: wyliczenie z jedną pozycją (ustęp z jednym punktem, punkt
  z jedną literą, litera z jednym tiretem) eksportuje się jako `nie-ztp-ustep`, `nie-ztp-punkt`,
  `nie-ztp-litera`, `nie-ztp-tiret` — wcześniej `pato-*`, których aktualny schemat nie zna.
- **Daty aktu jako zdarzenia cyklu życia**: zamiast `DataWejsciaWZycie` i `DatyMocyWstecznej` metadane
  niosą `DaneCzasowe/CyklZyciaAktu` — jedno `Zdarzenie` na każdą datę wejścia w życie (także wyjątków,
  z ich jednostkami i zakresem) i na każdą datę mocy wstecznej. Jednostki, zdania i części wspólne
  wskazują swoją grupę czasową (`GrupaCzasowa`) atrybutem `okres` —
  [ADR-0022](docs/adr/0022-daty-aktu-jako-zdarzenia-cyklu-zycia.md).
- **Metadane aktu zgodne ze schematem**: `Rodzaj` niesie kod ze słownika schematu (`USTAWA`, `ROZP`,
  `OBW_TJ`…, wcześniej nazwa wersalikami), `DataAktu` zawiera `DataWydania`, `GUIDwersji` stoi w przestrzeni
  `gd:`, a rola organu to `wydajacy`.
- **Unikalny `gd:nowGUID` każdego cytatu**: komenda eksportowana kilkoma cytatami (tekstowym i strukturalnym)
  dawała im ten sam GUID; teraz pierwszy niesie GUID komendy, kolejne własne.
- **Atrybuty GUID w przestrzeni `gd:`** (GUIDs.xsd): jednostki i organ — `gd:GUID`, zdanie — `gd:zdGUID`
  (wcześniej `GUID`) i `gd:fragmentGUID`, komendy nowelizacji — `gd:nowGUID`. Korzeń deklaruje `xmlns:gd`.
- **Atrybut `cudzyslowy` z wartościami schematu**: `brak`/`otwierajacy`/`zamykajacy`/`oba` zamiast maski
  0–3. Cytat tekstowy nie niesie już cudzysłowów w treści (tylko w atrybucie, jak w przykładach autorów
  schematu), a interpunkcja po cytacie stoi w zdaniu za nim.
- **Indeks górny w eId** w notacji `[n]`, zgodnie z aktualnym schematem i przykładami jego autorów:
  `art_4[1]` w eId jednostki i `element-eId` celu — [ADR-0019](docs/adr/0019-indeks-gorny-w-eid.md).
- **Odnośnik tuż za numerem jednostki**: „2.[9)] Do wniosku…", „§ 4.[9)] Przepisów…" i „Art. 13ja.[36)] 1. Podmiot…"
  dają numer ustępu (paragrafu), a znacznik odnośnika zostaje na początku treści. Wcześniej numer się nie
  odczytywał: jednostka miała `nr` `UNKNOWN`, eId bez numeru (`art_5__ust`), a oznaczenie stało w treści.
  Dotyczy głównie tekstów jednolitych.
- **Indeks górny w tekście akapitu**: odnośnik wstawiony polem (`NOTEREF`) jest jednym znacznikiem (`[8)]`,
  wcześniej `[8][)]`), a odstęp sformatowany jako indeks górny zostaje zwykłym odstępem (`4. W sprawach`
  zamiast `4.[ ]W sprawach`, `art. 4[1] w brzmieniu` zamiast `art. 4[1 ]w[ ]brzmieniu`).
- **Poziom pominięty w akcie** (litery wprost w ustępie, tirety wprost w ustępie albo punkcie): jednostki
  pod nim mają eId bez pustego segmentu (`art_5__ust_1__lit_a` zamiast `art_5__ust_1__pkt__lit_a`) i stoją
  w eksporcie wprost w najbliższej jednostce jawnej (`nie-ztp-litera` w `nie-ztp-ustep`), a nie w pustym
  `nie-ztp-punkt` z `nr` `UNKNOWN`. Pozycje takiego wyliczenia są dalszymi fragmentami zdania ustępu. Wyjątek
  wejścia w życie wskazujący „art. 5 ust. 1 lit. a" trafia w tę literę —
  [ADR-0023](docs/adr/0023-poziom-pominiety-jako-jednostka-niejawna.md).
- **Oznaczenia spoza ZTP czytane w całości**: indeks górny z literą („Art. 130[1a].", „§ 2[1a].", „8d[2a].")
  i polska litera („Art. 48ł.", „1ł.", „ł)") należą do numeru jednostki. Wcześniej art. 130[1a] był drugim
  `art_130`, art. 48ł drugim `art_48`, a reszta oznaczenia stała w treści. Powołania („art. 48ł", „lit. ł")
  i kontrola ciągłości numeracji (l → ł → m) znają te same litery.
- **Artykuł z paragrafami („§") w oddziale to `artykul-kodeksu`**: schemat rozróżnia tam artykuł ustawy
  i artykuł kodeksu, więc `artykul` z `paragraf-kodeksu` był w rozdziale niezgodny. Dotyczy kodeksów
  i rozdziałów kodeksu cytowanych w nowelizacji; wprost w akcie i na szczycie cytatu element zostaje `artykul`.
- **Jednostka bez odczytanego oznaczenia nie ma eId**: zamiast `nr` `UNKNOWN` i eId z pustym segmentem
  (`art_16f__ust`) wychodzi jako `nie-ztp-*` z pustym `nr` i `brak-eid="jednostka-nieoznaczona"`, a jednostki
  pod nią adresują się od najbliższego oznaczonego przodka (`art_16f__pkt_1`). Akapit bez oznaczenia obok
  paragrafów („§") to `nie-ztp-paragraf-kodeksu` —
  [ADR-0024](docs/adr/0024-jednostka-bez-oznaczenia-nie-ma-eid.md).
- **`brak-eid` na jedynym ustępie artykułu**: `ustep-n` i `paragraf-kodeksu-n` nazywają powód braku eId
  (`jednostka-nieoznaczona`), jak jednostki systematyzacyjne `-n` i przykłady autorów schematu.
- **eId części wspólnej**: `czesc-wspolna-punkt`, `-litera` i `-tiret` dostają adres gospodarza z segmentem
  `czw` (`art_51__ust_1__czw`, w artykule z jednym ustępem `art_16__czw`), jak w przykładach autorów schematu —
  [ADR-0025](docs/adr/0025-czesc-wspolna-ma-eid-gospodarza-z-czw.md).
- **Część wspólna, po której wyliczenie biegnie dalej**: stojąca tuż za literami punktu, a przed kolejnym
  punktem, zamyka litery (`czesc-wspolna-litera` w punkcie), choć styl akapitu mówi „część wspólna punktów";
  w pozostałych przypadkach przerywa wyliczenie i wychodzi w swoim miejscu jako `nie-ztp-czesc-wspolna-punkt`
  / `-litera` z `brak-eid="nie-ztp-wielokrotna-czesc-wspolna"`. Wcześniej trafiała na koniec wyliczenia,
  a druga część wspólna tego samego ustępu ginęła albo stawała się tiretem.
- **Część wspólna jako treść nowelizacji** („dodaje się część wspólną w brzmieniu:") jest cytatem strukturalnym
  z `czesc-wspolna-*` pod adresem celu (`art_18zg__ust_2__czw`) i funkcją `dodaj-w`, a nie cytatem tekstowym.
- **Numery odnośników jak w akcie**: znacznik odnośnika w treści niesie numer, pod którym przypis stoi w akcie
  (`[1)]`, `[2)]`…), a nie identyfikator przypisu z pliku Word, który zwykle jest o jeden większy — tytuł
  ustawy o ochronie sygnalistów kończył się `[2), 3)]` zamiast `[1), 2)]`.
- **Odnośnik przy powołaniu nie jest oznaczeniem jednostki**: w „…, z wyjątkiem art. 217[22)] oraz art. 227…"
  znacznik odnośnika czytał się jako kolejny artykuł i dostawał datę wyjątku. Powołania pomijają znaczniki
  odnośników, zachowując indeks górny oznaczenia („art. 4[1]").
- **Łącznik nierozdzielający i przejście do nowego wiersza w treści**: Word zapisuje je osobnymi elementami,
  które kanał tekstowy pomijał — „Służby Celno-Skarbowej" wychodziło jako „CelnoSkarbowej", a wyrazy rozdzielone
  wymuszonym przejściem wiersza zlewały się. Łącznik jest teraz dywizem, przejście wiersza odstępem.
- **Półpauza zostaje półpauzą**: treść jednostek zachowuje „–" z aktu (wcześniej każda półpauza stawała się
  dywizem „-"). Rozpoznawanie rodzaju aktu i nagłówka publikatora nadal sprowadza ją do dywizu.
- **Myślnik w treści części wspólnej**: `czesc-wspolna-*` zaczyna się myślnikiem jak w akcie („– chyba że …"),
  tak jak w przykładach autorów schematu; w tirecie myślnik pozostaje oznaczeniem i do treści nie wchodzi.
- **Indeks górny w treści zdania jako `sup`**: „art. 477[9] § 3[1]" wychodzi jako tekst z elementami `sup`
  (także jednostki miary — „m[2]"). W `nr` i w eId zostaje notacja z nawiasem.
- **Sankcja karna w osobnym akapicie** („Kto …, || podlega grzywnie.") jest dalszym ciągiem zdania przepisu,
  a po wyliczeniu — jego częścią wspólną; tak samo w treści nowelizacji. Wcześniej akapit w stylu sankcji
  ginął jako „pominięty akapit nierozpoznany" (przepis karny zostawał bez kary), a sankcja po myślniku stawała
  się tiretem. W korpusie to 1199 akapitów w 105 dokumentach.
- **ELI aktu zmienianego z publikatorem**: `now:akt` cytatu ma postać `https://eli.gov.pl/eli/DU/2025/350`;
  wcześniej brakowało segmentu publikatora (`…/eli//2025/350`), bo powołanie „Dz. U. z 2025 r. poz. 350"
  nie zapisywało, że chodzi o Dziennik Ustaw.
- **Rodzaj aktu w metadanych z nagłówka aktu**: `Rodzaj` to `USTAWA`, `ROZP`, `OBW_TJ`, `OBW`, `UCHW` albo `ZARZĄD`
  według nagłówka („ROZPORZĄDZENIE MINISTRA…", „OBWIESZCZENIE…"); wcześniej każdy dokument miał `USTAWA`, bo
  `LegalDocument.Type` nie jest ustawiany przez potok. Jawnie ustawiony `Type` nadal wygrywa.
- **Organy w metadanych**: organem wydającym ustawy jest `SEJM` z zasobem ELI, organ podpisujący „Prezydent
  Rzeczypospolitej Polskiej" dostaje swój zasób ELI. Organ nieodczytany zostaje `UNKNOWN`, ale bez atrapy
  `eliURI="URI"`.
- **Prefiks aktu w adresach** ([ADR-0027](docs/adr/0027-prefiks-aktu-dopisuje-eksport.md)): `eId` jednostki
  aktu ma postać `DU/2024/928__art_1`, a jednostka cytowana w nowelizacji i cel komendy (`element-eId`,
  `element-do-eId`) niosą prefiks aktu zmienianego (`DU/2025/350__art_68__ust_1__pkt_5`) — jak w przykładach
  autorów schematu. W metadanych prefiks dostają `JednostkaAktu` i `dowod`. Akt bez znanego publikatora
  zostaje przy adresach bez prefiksu. **Zmienia każdy adres w eksporcie**; model (`BaseEntity.Id`) bez zmian.
- **Zakres wyjątku wejścia w życie adresem w akcie zmienianym**: `JednostkaWZakresie` niesie adres jednostki
  cytowanej (`DU/2025/501__art_17__ust_1d` zamiast `ust_1d`), a gdy przepis jej nie cytuje (uchylenie, zmiana
  wyrazów) — cel jego komendy. Zakres, którego przepis nie wskazuje, zostaje ścieżką z klauzuli.
- **Wyjątek z zakresem dla artykułu bez ustępów**: „art. 5 w zakresie ust. 1d", gdy art. 5 sam jest przepisem
  zmieniającym, datuje wskazaną jednostkę cytowaną; wcześniej zakres nie był odnajdywany, bo nowelizację
  niesie ustęp niejawny artykułu.
- **Podstawa zdarzenia wyjątku**: `podstawa` i `dowod` zdarzenia wskazują pozycję wyliczenia wyjątków, która
  je zapisuje (`art. 43 pkt 2`, `DU/2026/26__art_43__pkt_2`), a nie całą klauzulę. Reguła ogólna i zdarzenie
  zapisane w kilku pozycjach nadal wskazują klauzulę. `DaneCzasowe` trzech przykładów autorów schematu
  zgadzają się w całości; `CorpusValidator --compare-reference` porównuje je osobną sekcją.
- **Rocznik publikatora bez roku**: „(Dz. U. poz. 1688)" przy ustawie „z dnia 7 lipca 2023 r." daje
  `DU/2023/1688` — rok z daty aktu zmienianego (§ 158 ust. 7 ZTP). Wcześniej brakujący rocznik zastępował
  bieżący rok kalendarzowy, więc identyfikator ELI aktu zmienianego zależał od dnia konwersji (w korpusie
  107 aktów zmienianych w 67 dokumentach). Publikator bez roku i bez daty aktu nie daje identyfikatora.
- **Miejsce bloku w tabeli**: blok z akapitu w tabeli DOCX niesie `DocumentBlock.TableCell` — numer tabeli,
  wiersz, kolumnę siatki, scalenia (`ColumnSpan`, `RowSpan`, kontynuacja komórki scalonej w pionie) i znacznik
  wiersza nagłówka. Wcześniej akapity tabel były nieodróżnialne od zwykłych. Nic tego jeszcze nie czyta —
  to podstawa pod tabele w załącznikach i jednostkach; eksport bez zmian.
- **Załączniki w modelu**: treść za nagłówkiem „Załącznik(i) do ustawy z dnia …" nie jest już pomijana —
  `LegalDocument.Annexes` niesie każdy załącznik z wskazaniem aktu, oznaczeniem („Załącznik nr 1"), numerem,
  tytułem i treścią w kolejności dokumentu: akapitami i tabelami (`ContentTable`: wiersze, komórki, scalenia,
  wiersz nagłówka). Odnośniki z załączników trafiają do rejestru dokumentu. Eksport bez zmian — załącznik
  nie ma jeszcze elementu w XML; wyliczenia w załączniku zostają akapitami.
- **Załączniki w eksporcie**: każdy załącznik wychodzi jako `zalacznik` za podpisem aktu — `zalacznik-do`,
  `zalacznik-nr`, `tytul-zalacznika` i `segment-zalacznika` z treścią w kolejności dokumentu. Tabela to
  `tabela`/`tbody`/`tr`/`td` z `naglowek`, `rowspan` i `colspan`; akapit komórki to `tytul-kolumny` (wiersz
  nagłówka), `liczba`, `wyliczenie-N` albo `tekst-zwykly`. Załącznik z numerem ma `eId` (`DU/2026/252__zal_1`),
  bez numeru — `brak-eid`. Odnośniki w treści załącznika są osadzane jak w treści aktu. Wyliczenia w załączniku
  wychodzą jeszcze jako akapity, nie jednostki.
- **Znacznik w treści odnośnika**: indeks górny własnego znacznika przypisu rozciągnięty na dalszy tekst nie
  zostawia już nawiasów w treści („[) Zmiany … i] 1914" → „Zmiany … i 1914").
- **Wersaliki nadane wprost**: tekst przebiegu sformatowanego wersalikami (`w:caps`) jest czytany wielkimi
  literami, jak stoi w akcie — tytuł załącznika „sektory ważne" to „SEKTORY WAŻNE". Dzięki temu nagłówek
  „Rozporządzenie Rady Ministrów" zapisany małymi literami z wersalikami jest rozpoznawany jak wydrukowany
  (8 dokumentów korpusu zyskało strefę tytułową, jeden — właściwy `Rodzaj`). Wersaliki wynikające ze stylu
  zostają tekstem, jak go zapisano („Ustawa") — tak też mają przykłady autorów schematu.
- **Formuła zmieniająca z określeniem ustawy**: „W ustawie budżetowej na rok 2024 z dnia 18 stycznia 2024 r.
  (Dz. U. poz. 122) wprowadza się następujące zmiany" jest przepisem zmieniającym — wcześniej formuła musiała
  brzmieć dosłownie „W ustawie z dnia", więc cytaty takiego przepisu nie wskazywały aktu zmienianego.
- **Wyliczenie w załączniku jako jednostki** ([ADR-0028](docs/adr/0028-wyliczenie-w-zalaczniku-buduje-potok-aktu.md)):
  załącznik, którego cała treść to wyliczenie („I. … 1) … a) …"), wychodzi jednostkami redakcyjnymi adresowanymi
  od załącznika (`DU/2026/252__zal_4__ust_I__pkt_1`) z datą wejścia w życie aktu; ustęp z oznaczeniem rzymskim
  to `nie-ztp-ustep`. Treść mieszana i załącznik bez numeru zostają akapitami. Przykład DU/2026/252 zgadza się
  we wszystkich 2280 jednostkach. **Pełna walidacja 9 takich dokumentów zgłasza teraz ograniczenie `AktGUID`**
  — tak samo jak przykład autorów schematu (dowolna treść `segment-zalacznika`); szczegóły w ADR.
- **Dalsza treść jednostki** ([ADR-0029](docs/adr/0029-akapit-bez-oznaczenia-jest-dalsza-trescia-jednostki.md)):
  akapit bez oznaczenia stojący w przepisie (legenda wzoru, treść oświadczenia) nie jest już pomijany — należy
  do jednostki, za którą stoi (`BaseEntity.Continuation`), i wychodzi jako `fragment` tuż za jej `p`. Tabela
  w treści przepisu wychodzi jako `tabela` w tym samym miejscu, a jej komórki nie przechodzą przez
  rozpoznawanie jednostek („1." w kolumnie nie jest już ustępem). **Zmienia wyjście 159 dokumentów**
  (1156 fragmentów, 22 tabele); zgodność ze schematem bez zmian. Tabela w treści nowelizacji idzie
  jeszcze dotychczasową drogą.
- **Tabela w treści nowelizacji**: wiersze tabeli cytowane po komendzie („po lp. 2a dodaje się lp. 2b
  w brzmieniu:") są treścią cytowaną (`AmendmentContent.Tables`) i wychodzą jako `cytat-strukt` z `tabela`;
  tabela w środku cytowanego artykułu należy do jednostki, za którą stoi. Wcześniej pierwsza kolumna stawała
  się jednostkami cytatu albo tekstem, a reszta ginęła lub trafiała do następnego przepisu (16 tabel
  w 9 ustawach korpusu).
- **Miara pokrycia tekstu** (`CorpusValidator --text-coverage`): dla każdego akapitu źródła sprawdza, czy jego
  tekst doszedł do eksportu (porównanie na literach i cyfrach, bez oznaczenia jednostki i znaczników
  odnośników); raport wg rodzaju aktu i lista niedoniesionych akapitów. Ustawy ogłoszone: 100 % akapitów.
- **Rodzaj aktu bez rozpoznanego nagłówka**: gdy nagłówek nie ma rozpoznawalnej postaci („ROZPORZĄDZENIE MINISTRA
  zDROWIA", „Rozporządzenie Ministra Finansów i Gospodarki"), `Rodzaj` bierze się z klasyfikatora dokumentu zamiast
  domyślnego `USTAWA`. Nagłówek rodzaju ze znacznikiem odnośnika na końcu („Obwieszczenie Ministra …[1)]") jest
  rozpoznawany jak bez niego, a odnośnik staje się elementem w `rodzaj-aktu`. W korpusie 11 dokumentów przestało
  uchodzić za ustawy (i za wydane przez Sejm); wszystkie 242 dokumenty z `Rodzaj` = `USTAWA` to ustawy.
- **Porównanie dwóch eksportów** (`CorpusValidator --compare-exports <przed> <po>`): dla dwóch przebiegów
  `--xml-corpus` podaje zmienione pliki, zmianę liczby wystąpień każdego elementu i próbkę zmienionych wierszy,
  z pominięciem losowych identyfikatorów GUID — miara „co ta zmiana zrobiła z wyjściem".
- **Miara zgodności bez stylów** (`CorpusValidator --styleless-parity`): każdy dokument jest parsowany ze stylami,
  bez stylów i z samego tekstu, a struktura jednostek z eksportu jest zestawiana z wynikiem ze stylami — ile zostaje
  z samej treści, gdy wejściem jest DOCX bez szablonu, PDF albo TXT. Stan wyjściowy: ustawy 93,8 % jednostek
  (136 z 242 dokumentów w pełni zgodnych), teksty jednolite 99,5 %.

- **Treść cytowana bez stylów**: granice cytatu i części wspólne rozpoznawane z samej treści, gdy dokument nie
  niesie stylów szablonu (DOCX bez szablonu, PDF, TXT).
  - Cytat zamknięty przecinkiem („…”,) kończy się przed następną jednostką aktu; trwa tylko wtedy, gdy kolejny blok
    otwiera następny cytat (komenda mnoga „art. 5 i 6 otrzymują brzmienie:"). Wcześniej następna litera wyliczenia
    zmian była wchłaniana do cytatu.
  - Myślnik po punkcie albo literze bez znaku końcowego jest w treści cytowanej częścią wspólną — tak jak w treści
    aktu (§ 57 ust. 3 ZTP). Reguła „pozycja bez znaku końcowego" ma jedno źródło
    (`CommonPartPlacement.LeavesEnumerationOpen`) i nie bierze kropki skrótu roku („… 2026 r.") za znak końcowy.
  - Komenda bez dwukropka („ust. 1 otrzymuje brzmienie") i blok w cudzysłowie otwierającym po niej to treść
    cytowana, a nie ustęp artykułu zmieniającego — przepis aktu nie zaczyna się od cudzysłowu. Decyzja trafia jako
    ostrzeżenie na jednostkę z komendą.
  - Cudzysłów otwierający w indeksie górnym („[„2)] …", cytowany odnośnik) otwiera cytat.

  Zgodność struktury bez stylów (`--styleless-parity`): ustawy ogłoszone 93,8 % → 98,37 % jednostek, w pełni
  zgodnych 136 → 191 z 242 dokumentów; teksty jednolite 313 → 322 z 460. W eksporcie dokumentów ze
  stylami zmienił się 1 dokument: część wspólna bez stylu w treści cytowanej (wcześniej tiret w osobnym cytacie).

- **Znacznik tiretu jako źródło głębokości**: zagnieżdżenie tiretu stoi w treści — „– –" to podwójne tiret,
  „– – –" potrójne — więc `TiretMarker` czyta je z samego tekstu, przed stylem (`2TIR`/`3TIR`) i wcięciem; w korpusie
  taki znacznik ma 433 z 437 akapitów w stylu `2TIR` (pozostałe 4 nie mają myślnika) i wszystkie 8 w stylu `3TIR`,
  a żaden w stylu `TIR`.
  Znacznik schodzi z treści w całości: tekst podwójnego tiretu nie zaczyna się już od myślnika (142 zdania
  w 32 dokumentach). Tirety zagnieżdżone w treści cytowanej są zagnieżdżone także w modelu i w eksporcie
  (`podw-tiret` zamiast kolejnego `tiret`; 25 jednostek w 4 dokumentach). Zgodność struktury bez stylów:
  ustawy ogłoszone 98,37 % → 98,72 % jednostek (197 z 242 dokumentów w pełni zgodnych), teksty jednolite 327 z 460.

- **Warianty cudzysłowu z jednego źródła** (ADR-0030): zestawy znaków cudzysłowu, dotąd różne w dziesięciu
  miejscach, trzyma `QuotationMarks`. Doszedł wariant `ˮ` (U+02EE), którym bywa zapisany cudzysłów zamykający:
  cytat zamknięty tym znakiem kończy się teraz także bez stylów, a ze stylami znak schodzi z treści cytowanej
  jednostki jak każdy cudzysłów graniczny (2 dokumenty, 3 zdań). Tekstu się nie normalizuje.
  Zgodność struktury bez stylów: ustawy ogłoszone 98,72 % → 99,41 % jednostek (199 z 242 dokumentów w pełni
  zgodnych), teksty jednolite 327 z 460.

- **Komenda złożona otwiera cytat**: przepis łączący komendę wyrazową z dodaniem jednostki („… dodaje się wyraz
  „lub” oraz dodaje się pkt 4 w brzmieniu:") kończy się zwrotem zapowiadającym treść cytowaną, więc bez stylów
  następny blok jest cytatem — wcześniej komenda wyrazowa gasiła zwrot i cytowany punkt stawał się punktem
  artykułu zmieniającego (4 przepisy w 2 ustawach). Eksport dokumentów ze stylami bez zmian; zgodność struktury
  bez stylów: ustawy ogłoszone 99,41 % → 99,67 % jednostek.

- **Strażnik zgodności bez stylów na akcie referencyjnym**: `ReferenceActTests.ReferenceAct_WithoutStyles_HasTheSameUnitStructure`
  parsuje akt referencyjny z bloków pozbawionych stylu i wymaga tej samej struktury jednostek co ze stylami
  (element i eId każdej jednostki) — wynik miary `--styleless-parity` przestaje zależeć od ręcznego przebiegu.

- **Walidacja: odstępstwo walidatora .NET przy treści dowolnej**: komunikat „The field 'GUID' is expecting at the
  most one value" przy jednostkach załącznika nie był niezgodnością ze schematem. Walidator .NET bierze atrybut
  elementu bez deklaracji (treść `xs:any` w `segment-zalacznika`) za drugą wartość pola elementu nadrzędnego
  i unikalności tej wartości nie sprawdza; libxml2 i Xerces przyjmują taki dokument — także przykład autorów
  schematu — i unikalność sprawdzają. `SchemaPlValidator.Validate` pomija ten komunikat przy elemencie bez
  deklaracji i sam zgłasza powtórzoną wartość atrybutu. Zgodnych w całości: ustawy ogłoszone 239 → 240 z 241,
  teksty jednolite 402 → 410 z 460; eksport bez zmian.

- **Jednostki załącznika bez odwołania do grupy czasowej**: jednostki i zdania pod `segment-zalacznika` niosą
  `wejscie-w-zycie`, ale już nie `okres` — tak jak w przykładzie autorów schematu. Element w treści dowolnej nie ma
  deklaracji, więc jego `okres` nie ma dla walidatora wartości i Xerces zgłaszał niespełnione odwołanie
  `AktOkresJednostki`. Zmienia eksport 8 dokumentów (387 elementów); przykład DU/2026/252 zgadza się teraz we
  wszystkich datach i okresach (2207 z 2207, było 2166), a eksport tego aktu przyjmują .NET, libxml2 i Xerces.

- **Obwieszczenie tekstu jednolitego w modelu**: treść sprzed wiersza „Załącznik do obwieszczenia …" — punkty
  obwieszczenia, przytoczone przepisy, podpis — należy do obwieszczenia (`LegalDocument.Announcement`), a nie do
  aktu, który ono ogłasza. Wcześniej przytoczone przepisy budowały się jak artykuły ogłaszanego aktu: w 420
  tekstach jednolitych znika 2216 obcych artykułów (z ustępami i punktami), 690 cytatów nowelizacyjnych, 523
  jednostki `nie-ztp-*` i 289 zdarzeń cyklu życia wyprowadzonych z cudzych przepisów końcowych. Granica jest
  w korpusie jednoznaczna: każde z 728 obwieszczeń tekstu jednolitego ma ten wiersz dokładnie raz. **Eksport nie
  niesie jeszcze treści obwieszczenia** (korzeń `obwieszczenie` — w backlogu), więc pokrycie tekstu tekstów
  jednolitych spada przejściowo z 90,3 % do 88,0 % akapitów; liczba dokumentów zgodnych ze schematem bez zmian.
- **Miara pokrycia tekstu a cudzysłów `ˮ`**: U+02EE jest w Unicode literą, więc miara brała go za treść i trzy
  akapity ustaw z cytatem zamkniętym tym znakiem uchodziły za zgubione; cudzysłowy z `QuotationMarks` nie są treścią.

- **Tekst jednolity ustawy pod korzeniem `obwieszczenie`** (ADR-0031): eksport tekstu jednolitego ma korzeń
  `obwieszczenie` ze strefą tytułową obwieszczenia (`rodzaj-aktu`, `autor-aktu`, `data`, `tytul`), a ogłaszana
  ustawa stoi w nim jako `ustawa` z własną strefą tytułową (`zalacznik-do`, `rodzaj-aktu`, `data`, `tytul`),
  jednostkami i załącznikami — jak w przykładzie autorów schematu. Zmienia korzeń 419 dokumentów korpusu; liczba
  dokumentów zgodnych ze schematem bez zmian (651 z 2321, tak samo wg libxml2), a odnośnik przy tytule ogłaszanego
  aktu trafia do `tytul`. Tekst jednolity rozporządzenia, uchwały i zarządzenia zostaje przy dotychczasowym korzeniu.
  Przedmiot aktu zaczynający się nazwą własną („o Policji", „o Państwowej Straży Pożarnej") jest rozpoznawany —
  wcześniej taki tytuł ginął (49 tekstów jednolitych).

- **Jednostki obwieszczenia tekstu jednolitego** (ADR-0032): punkty obwieszczenia („1. Na podstawie …"), podpunkty
  („1) ustawą …") i część wspólna podpunktów są jednostkami — w eksporcie `punkt-OTJ`, `podpunkt-OTJ`
  i `czesc-wspolna-podpunkt-OTJ` z adresami liczonymi od obwieszczenia (`DU/2025/1094__pkt_1__ppkt_2`; nowy skrót
  `ppkt`), bez dat. Przytoczony przepis („…, który stanowi: „Art. 57. …"") jest dalszą treścią jednostki, za którą
  stoi, a formuła podpisu pod obwieszczeniem — podpisem dokumentu (`organ-podpisujacy`, `osoba`, organ w metadanych).
  W 419 tekstach jednolitych: 854 punkty, 1441 podpunktów, 181 części wspólnych. Pokrycie tekstu tekstów
  jednolitych: 91,1 % akapitów (88,0 % po wydzieleniu obwieszczenia, 90,3 % przed nim); liczba dokumentów zgodnych
  ze schematem bez zmian (651 z 2321, tak samo wg libxml2).

- **Tekst jednolity: zdarzenie `ujednolicenie` zamiast wejścia w życie** (ADR-0033): przepis końcowy tekstu
  jednolitego mówi o ogłoszeniu ustawy, a data dokumentu jest datą ogłoszenia obwieszczenia — wyliczone z nich
  „wejście w życie" było datą, której ustawa nigdy nie miała. Tekst jednolity ma teraz w `DaneCzasowe` jedno
  zdarzenie `rodzaj="ujednolicenie"` w dniu ogłoszenia obwieszczenia i grupę `okr_ujednolicenie.…`, a jego
  jednostki nie niosą `wejscie-w-zycie` ani `okres` — jak w przykładzie autorów schematu. W korpusie: zdarzeń
  cyklu życia 1118 → 1114, w tym 727 ujednoliceń — po jednym na tekst jednolity (379 tekstów jednolitych miało wcześniej daty na jednostkach); raport `--xml-corpus` nie liczy tekstów jednolitych do „elementów bez daty".

- **Rozporządzenia, uchwały i zarządzenia: „§" jako jednostka podstawowa** (ADR-0034): w akcie opartym na
  paragrafach „§ 5. 1. …" jest tym, czym „Art. 5. 1. …" w ustawie — jednostką z ustępami, punktami i literami,
  budowaną tym samym potokiem i z adresem `art_5__ust_1`. O znaczeniu znaku rozstrzyga rodzaj aktu z klasyfikatora
  dokumentu; w ustawie „§" zostaje paragrafem kodeksu. Eksport ma korzeń według rodzaju aktu (`rozporzadzenie`,
  `uchwala`, `zarzadzenie`), jednostkę `paragraf` i strefę tytułową z `autor-aktu` oraz `podstawa-prawna`;
  tekst jednolity takiego aktu stoi pod `obwieszczenie`, jak tekst jednolity ustawy.
  - Organ wydający jest rozpoznawany po miejscu w strefie tytułowej — między rodzajem aktu a datą — w dowolnej
    pisowni i z odnośnikiem („Ministra Finansów[1)]"). Wcześniej taki wiersz zamykał strefę tytułową, więc
    w większości rozporządzeń ginęły też data i tytuł aktu (a z nimi `DataAktu` w metadanych).
  - W korpusie: 1067 rozporządzeń przestaje być pustych (8169 paragrafów, 20 tys. punktów), a 287 tekstów
    jednolitych aktów wykonawczych zyskuje treść. Zgodnych ze schematem w całości: 651 → 1648 z 2321 dokumentów
    (tak samo wg libxml2; żaden wcześniej zgodny nie przestał nim być) — rozporządzenia 823 z 1067, obwieszczenia
    584 z 708, ustawy 240 z 241; treść aktu zgodna w 1984 z 2017. Pokrycie tekstu: rozporządzenia 4 % → 99,6 %
    akapitów, teksty jednolite 99,7 %.

- **Kodeks jako `kodeks`**: ustawa o tytule „Kodeks …" wychodzi pod elementem `kodeks` — wprost albo osadzona
  w obwieszczeniu tekstu jednolitego (13 tekstów jednolitych kodeksów w korpusie; zgodność ze schematem bez zmian).
  Ustawa o zmianie kodeksu pozostaje `ustawa`, tak samo kodeks bez jednostek systematyzacyjnych (artykuł wprost
  w `kodeks` ma w schemacie inną budowę).

- **Cytowany paragraf w rozporządzeniu zmieniającym**: treść „§ 5 otrzymuje brzmienie: „§ 5. 1. …"" jest
  jednostką podstawową (`paragraf`) z ustępami, tak jak poza cytatem — wcześniej wychodziła jako `paragraf-kodeksu`,
  a oznaczenie pierwszego ustępu („1.") zostawało zdaniem treści. W korpusie: 364 cytowane paragrafy w 180
  rozporządzeniach, 144 odzyskane ustępy; zgodność ze schematem bez zmian (1648 z 2321, tak samo wg libxml2).
  W ustawie zmieniającej kodeks cytowany „§ 2." pozostaje paragrafem kodeksu.

- **„§" po paragrafie w rozporządzeniu** ([ADR-0035](docs/adr/0035-paragraf-po-paragrafie-to-kolejny-paragraf.md)):
  w powołaniu „§ 6 i § 7", „po § 3 dodaje się § 3a i § 3b", „w § 6a, w § 12, w § 14 w ust. 1" kolejny „§" jest
  następnym paragrafem, a nie paragrafem kodeksu pod poprzednim. Cele komend z adresem `art_3__par_3a` w
  rozporządzeniach: 208 → 6 (zostają jawne „art. … § …"); wyliczone paragrafy dostają osobne cytaty, każdy ze swoją
  treścią. Ta sama reguła czyta klauzulę wejścia w życie — „z wyjątkiem § 1 pkt 4, § 3 i § 4" przestaje gubić
  § 3 i § 4 (w korpusie jeden akt, 5 jednostek z poprawioną datą). Zgodność ze schematem bez zmian.

- **Akt zmieniany w rozporządzeniu zmieniającym**: formuła „W rozporządzeniu Ministra … z dnia … (Dz. U. …)"
  wskazuje akt zmieniany tak jak „W ustawie z dnia …" — jego publikator jest prefiksem celu komendy, cytowanej
  jednostki i adresem `now:akt`. Wcześniej cele 531 rozporządzeń wskazywały na samo rozporządzenie zmieniające
  (3478 komend), a w 43 dokumentach bez nagłówka dziennika adres aktu był pusty (184); teraz 3654 komendy w 570
  rozporządzeniach adresują akt zmieniany. Zgodność ze schematem bez zmian (1648 z 2321, tak samo wg libxml2).

- **Przytoczone przepisy w obwieszczeniu tekstu jednolitego** ([ADR-0036](docs/adr/0036-przytoczone-artykuly-sa-jednostkami-aktu-przytaczanego.md)):
  artykuły przytoczone w obwieszczeniu („…, który stanowi: „Art. 19. …"") są jednostkami — `tresc-strukt`
  w zdaniu punktu albo podpunktu, z adresem w akcie, z którego pochodzą (`DU/2025/1792__art_19__pkt_1`),
  jak w przykładzie autorów schematu. Model: `Quotation` w dalszej treści jednostki (`ContentBlock.Quotation`).
  Przytoczenie, którego nie da się zbudować w całości z artykułów (ustęp, akapit poza jednostkami, przepis
  zmieniający z własnym cytatem), zostaje tekstem (`fragment`) jak dotąd.
  - W korpusie: 1625 z 1717 przytoczeń w 684 obwieszczeniach (1772 artykuły, 567 paragrafów, 1403 ustępy,
    2405 punktów, 229 liter; 6068 akapitów mniej jako `fragment`), 1570 z prefiksem aktu przytaczanego.
    Zgodność ze schematem (1648 z 2321, tak samo wg libxml2) i pokrycie tekstu bez zmian.

- **Publikator w Monitorze Polskim**: `JournalReferenceService` czyta „M.P. poz. 1029" i „M.P. z 2018 r. poz. 756"
  tak jak „Dz. U." — akt dostaje ELI z segmentem `MP` (`MP/2023/1029`), a pozycje scala się w obrębie dziennika
  i rocznika. W korpusie: 23 przytoczenia z uchwał i zarządzeń w 11 obwieszczeniach zyskują prefiks aktu
  przytaczanego (1593 z 1625 przytoczeń z prefiksem). Zgodność ze schematem bez zmian.

- **Podpis w zastępstwie i podpisy aktu wydanego wspólnie**: formuła „Minister Finansów: wz. J. Neneman"
  i „Minister Obrony Narodowej: z up. P. Bejda" jest podpisem aktu (`ActSignature.Capacity` niesie skrót, eksport
  pisze go przed osobą), tak samo organ z przecinkiem w nazwie („Minister Rodziny, Pracy i Polityki Społecznej").
  Akt wydany wspólnie zachowuje każdą formułę (`LegalDocument.CoSignatures`) — wcześniej druga nadpisywała pierwszą.
  - W korpusie: 545 dokumentów (głównie rozporządzenia) zyskuje `organ-podpisujacy`, 583 organy podpisujące
    w metadanych; 539 formuł przestaje być `fragment` pod ostatnią jednostką, 8 nadpisywanych wraca do eksportu.
    Zgodność ze schematem bez zmian (1648 z 2321, tak samo wg libxml2).

- **Strefa tytułowa w pisowni spoza wzorca**: nagłówek rodzaju pisany inaczej niż wzorzec („rozporządzenie",
  „ZARządzenie NR 249", „ROZPORZĄDZENIE MINISTRA zDROWIA") jest rozpoznawany, gdy potwierdza go drugi sygnał —
  styl wiersza rodzaju albo miejsce pierwszego wiersza aktu w załączniku do obwieszczenia; organ pisany jak nazwa
  własna w wierszu rodzaju („Rozporządzenie Ministra Finansów") i organ aktu wydanego wspólnie w dwóch wierszach
  są częścią strefy tytułowej; tytuł „uchylające rozporządzenie …" oraz tytuł w stylu tytułu aktu są przedmiotem.
  Rodzaj nazwany przez nagłówek czyta jedno miejsce (`ZtpPatterns.KindOfHeading`).
  - W korpusie: 16 tekstów jednolitych rozporządzeń i zarządzeń przechodzi spod korzenia `ustawa` pod
    `obwieszczenie`, 13 rozporządzeń odzyskuje organ, datę albo tytuł. Zgodnych ze schematem w całości:
    1648 → 1675 z 2321 (tak samo wg libxml2; żaden wcześniej zgodny nie przestał nim być) — obwieszczenia 598
    z 708, rozporządzenia 836 z 1067; treść aktu zgodna w 1998 z 2017.

- **Strefa tytułowa — dwa ostatnie przypadki**: wiersz rodzaju obwieszczenia z organem małą literą
  („Obwieszczenie marszałka Sejmu Rzeczypospolitej Polskiej") rozpoznaje styl wiersza obwieszczenia, a data
  bez odstępu przed dniem („z dnia17 marca 2025 r.") jest datą aktu. Zgodnych ze schematem w całości: 1677 z 2321
  (obwieszczenia 600 z 708; tak samo wg libxml2).

- **Równania Worda w tekście** ([ADR-0037](docs/adr/0037-rownanie-worda-wchodzi-do-tekstu-w-zapisie-liniowym.md)):
  treść równania (`m:oMath`) wchodzi do tekstu akapitu w zapisie liniowym — „I_oze=(I_e∙U_oze)+I_b",
  „WYD_(n-1)", „(a+b)/c" — w miejscu, w którym stoi. Wcześniej znikała: zdanie zostawało z dziurą („kwoty wydatków ,
  o której mowa"), a wiersz legendy bez symbolu stawał się tiretem.
  - Artykuł w treści cytowanej wymaga oznaczenia w treści — akapit w stylu artykułu bez „Art." (wzór) nie jest
    już artykułem bez numeru.
  - W korpusie: 1206 równań w 56 dokumentach; 319 fałszywych `nie-ztp-tiret` mniej; zgodnych ze schematem
    w całości 1677 → 1685 z 2321 (tak samo wg libxml2, żaden zgodny nie odpadł); zgubionych akapitów w tekstach
    jednolitych 1402 → 1215, w rozporządzeniach 154 → 127. Eksport nie pisze jeszcze `wzor-mat` (MathML).

- **Akapit bez oznaczenia w treści cytowanej**: wzór, wiersz legendy i każdy inny akapit bez oznaczenia stojący
  za cytowaną jednostką jest jej dalszą treścią (`fragment` w tej jednostce) — jak poza cytatem (ADR-0029) i jak
  tabela w cytacie. Wcześniej taki akapit trafiał do tekstu cytatu **przed** jego jednostkami. Bez jednostki przed
  sobą pozostaje tekstem cytowanym wprost. Ta sama reguła obowiązuje w przytoczeniach obwieszczenia.
  - W korpusie: 26 dokumentów, 293 akapity na swoim miejscu (43 bloki tekstu przed cytatem mniej), 2 kolejne
    przytoczenia zbudowane jako jednostki; zgodność ze schematem i pokrycie tekstu bez zmian.

- **Konwersja równań Worda do MathML** (`OfficeMathMl`) — fundament pod `wzor-mat`: indeksy, ułamek (zwykły,
  ukośny, w wierszu), nawiasy ze znakami równania, operator z granicami, pierwiastek, akcent, macierz i układ
  równań; tekst przebiegu jako identyfikatory, liczby i znaki działań. Eksport jeszcze z niej nie korzysta.
  - Nowa miara `CorpusValidator --equations <root> <katalog>`: liczba równań (wzór w osobnym akapicie / symbol
    w zdaniu), zgodność MathML ze schematem, konstrukcje OMML bez odwzorowania. W korpusie: 1206 równań w 56
    dokumentach (207 wzorów, 999 symboli), wszystkie zgodne ze schematem, żadnej konstrukcji bez odwzorowania.

- **Wzór jako `wzor-mat`** ([ADR-0038](docs/adr/0038-wzor-w-osobnym-akapicie-to-wzor-mat-z-mathml.md)): akapit
  złożony z samego równania Worda jest wzorem — dalszą treścią jednostki, za którą stoi, z wyrażeniem w MathML
  (`ContentBlock.Formula`, `DocumentBlock.FormulaMathMl`); eksport pisze `wzor-mat` → `wzor` → `mml:math` tuż za
  `p` jednostki, także w treści cytowanej. Wzór złożony stylem ustępu albo punktu nie jest już jednostką.
  - Nawias kwadratowy równania w zapisie liniowym to znaki „［ ］" — zwykły `[x]` jest kanałem indeksu górnego
    i znacznika odnośnika (wzór „[(CM/12)]" dostawał odwołanie do odnośnika 12).
  - W korpusie: 189 wzorów w 45 dokumentach (183 `fragment` mniej), 14 jednostek `nie-ztp-*` mniej, 8 fałszywych
    odwołań do odnośników i 12 fałszywych `sup` mniej; zgodność ze schematem (1685 z 2321, tak samo wg libxml2)
    i pokrycie tekstu bez zmian. Legenda wzoru zostaje na razie jako `fragment`.

- **Legenda wzoru w `wzor-mat`**: wiersze tuż za wzorem — wprowadzenie („gdzie poszczególne symbole oznaczają:")
  i opisy symboli („I_e – ilość energii …") — należą do wzoru (`ContentFormula.LegendIntroduction`, `Legend`)
  i wychodzą jako `wprowadzenie-do-opisu` oraz `opis` z symbolem (`symbol-mat` z MathML, gdy symbol jest równaniem;
  `symbol-txt`, gdy zapisano go tekstem) i objaśnieniem podzielonym na zdania — jak w przykładzie autorów schematu.
  Wiersz, który legendą nie jest, zamyka ją i zostaje dalszą treścią jednostki.
  - W korpusie: 148 wprowadzeń i 725 opisów symboli (356 z MathML) w 40 dokumentach, 873 `fragment` mniej;
    zgodność ze schematem (1685 z 2321, tak samo wg libxml2) i pokrycie tekstu bez zmian.

- **Załączniki aktów wykonawczych**: nagłówek załącznika rozporządzenia, uchwały i zarządzenia ma między rodzajem
  aktu a datą organ (i numer): „Załącznik do rozporządzenia Ministra Finansów z dnia …", „Załącznik do uchwały
  nr 113 Rady Ministrów z dnia …". Wcześniej rozpoznawany był tylko zapis ustawowy („do ustawy z dnia"), więc treść
  załącznika — tabele, wzory formularzy, wykazy — trafiała do ostatniego paragrafu aktu jako jego jednostki
  i dalsza treść.
  - W korpusie: 815 załączników w 477 dokumentach (465 rozporządzeń); spod ostatniego paragrafu znika 2350
    `fragment`, 1337 punktów, 858 ustępów, 506 tiretów, 505 liter i 183 paragrafy, które były treścią załączników;
    jednostek `nie-ztp-*` 1787 → 1362. Pokrycie tekstu: rozporządzenia 127 → 15 zgubionych akapitów (99,97 %),
    teksty jednolite 1215 → 1136. Zgodność ze schematem bez zmian (1685 z 2321, tak samo wg libxml2).

- **Akapit w stylu jednostki bez oznaczenia** ([ADR-0039](docs/adr/0039-akapit-w-stylu-jednostki-bez-oznaczenia-jest-dalsza-trescia.md)):
  akapit złożony stylem ustępu, punktu, litery albo tiretu, którego treść nie zaczyna się niczym, co wygląda na
  oznaczenie, jest dalszą treścią jednostki, za którą stoi — w treści aktu i w treści cytowanej. Wcześniej stawał
  się jednostką bez numeru (`nie-ztp-*` z `brak-eid`), a jednostki pod nim też wychodziły jako `nie-ztp-*`.
  Akapit z oznaczeniem spoza wzorca („l)", „A)", „3.8.", „2.1 …", tiret pisany „‒") zostaje jednostką.
  - Druga treść cytowana pod tą samą jednostką dołącza do pierwszej nowelizacji — wcześniej ją zastępowała.
  - W korpusie: 50 dokumentów; 744 jednostki bez oznaczenia mniej (603 `nie-ztp-punkt`), 614 akapitów jako
    `fragment`, 147 jednostek wraca do zwykłych elementów. Zgodność ze schematem (1685 z 2321, tak samo wg
    libxml2) i pokrycie tekstu bez zmian.

- **Warianty myślnika z jednego źródła** ([ADR-0040](docs/adr/0040-warianty-myslnika-z-jednego-zrodla.md)):
  tiret, część wspólna zaczynająca się od myślnika, przedział oznaczeń („art. 1–3") i legenda wzoru rozpoznają
  każdy myślnik, którym piszą szczotki — także myślnik cyfrowy (U+2012) i minus, nie tylko dywiz i półpauzę
  (`Services/Dashes.cs`). Tekst nie jest normalizowany.
  - W korpusie: 84 części wspólne i 17 tiretów więcej (65 części wspólnych ginęło z eksportu), znak znika z treści
    67 tiretów; zgubionych akapitów w tekstach jednolitych 1136 → 1072. Zgodność ze schematem bez zmian.

## [1.0.3] - 2026-09-02
### Dodano
- Parsowanie daty ogłoszenia aktu oraz całości nagłówka znajdującego się w ogłoszonych ustawach.

## [1.0.2] - 2026-08-27

Zmiany od wydania 1.0.1, pogrupowane funkcjami.

### Struktura projektu i wieloformatowe wejście
#### Dodano
- ujednolicony potok wejścia: adaptery DOCX, PDF i TXT sprowadzające dokument do wspólnej
  reprezentacji bloków (`DocumentBlock`), z detekcją formatu po sygnaturze pliku.
- odczyt PDF z warstwy tekstowej (PdfPig) wraz z filtrem artefaktów strony — nagłówków, stopek
  i numeracji. Skan bez warstwy tekstowej jest odrzucany z komunikatem, nie przetwarzany na ślepo.
- klasyfikację całego dokumentu (rodzaj aktu wg ZTP) wykonywaną przed budową modelu; wynik niesie
  koperta `ParseResult`, a zachowaniem steruje `ParseOptions` z `ParsePolicy`.
- katalog `docs/adr/` z decyzjami architektonicznymi wraz z ich uzasadnieniami.
#### Zmieniono
- projekty przemianowane z `WordParser*` i `ModelDto` na `Saga.Model`, `Saga.Core`, `Saga.Cli`
  i `Saga.Web`, w układzie `src/` + `tests/`.

### Wejście w życie aktu
#### Dodano
- rozpoznawanie przepisów o wejściu w życie: skanowanie klauzul, parsowanie formuł, rozwiązywanie
  dat oraz wskazanych jednostek redakcyjnych, wraz z mapą wyjątków od daty ogólnej.
- rozsyłanie daty wejścia w życie po całym drzewie jednostek redakcyjnych.
- atrybut `wejscie-w-zycie` w eksporcie XML.
- model: `EntryIntoForceInfo`, `EntryIntoForceFormula`, `PartialEntryIntoForce`,
  `ScopedUnitReference` oraz interfejs `IEntersIntoForce`.
- polskie liczebniki i daty jako jedno źródło prawdy (`PolishNumerals`, `PolishDates`).

### CI/CD i konteneryzacja
#### Dodano
- pipeline `.gitlab-ci.yml` według standardu CAISE: etapy `test`, `quality`, `build_check`
  i `build_and_publish`.
- bramki jakości: pokrycie 70% całości oraz 80% linii zmienionych w merge requeście, kontrola stylu
  i formatowania, kompilacja z `-warnaserror`, audyt podatności i pakietów wycofanych.
- smoke test obrazu — kontener musi wstać i odpowiedzieć, samo zbudowanie obrazu nie wystarcza.
- `version-check`: tag wydania musi być równy `<Version>` z `Directory.Build.props`; tag spoza
  wzorca `X.Y.Z[.devN]` zatrzymuje pipeline z czytelnym komunikatem.
- `Directory.Build.props` jako jedyne źródło wersji oraz `.editorconfig` dla całego repozytorium.
#### Zmieniono
- `Dockerfile` przepisany na aktualny układ projektów; poprzedni odwoływał się do nieistniejących
  już projektów i nie budował się w ogóle. Obraz startuje jako użytkownik nieuprzywilejowany.
- testy przeniesione z xUnit 2 na xUnit v3 na Microsoft.Testing.Platform — .NET 10 SDK nie wspiera
  już dla nich ścieżki VSTest. Pokrycie i raport JUnit są teraz wbudowane w runner.
- całe drzewo źródeł sformatowane jednorazowo według `.editorconfig` (zmiana wyłącznie białych znaków).

### Web API
#### Dodano
- `POST /v1/convert` — konwersja przesłanego dokumentu DOCX, PDF lub TXT do XML aktu prawnego.
- `GET /health` — sonda dla platformy wdrożeniowej, celowo poza wersjonowaniem ścieżki.
- dokumentację OpenAPI wraz z interfejsem Scalar jako jedyny interfejs usługi; `GET /` przekierowuje
  do dokumentacji, a sekcja `info` niesie wersję i commit wdrożonego builda oraz środowisko.
- `ProblemDetails` z `traceId` dla każdej awarii, łącznie z przekroczeniem limitu rozmiaru pliku
  i wyjątkami spoza obsługi endpointu.
#### Zmieniono
- `POST /parse_xml` działa dalej jako przestarzały alias ścieżki sprzed wersjonowania.
#### Usunięto
- interfejs HTML: renderery, szablony, `POST /parse` oraz widok wymuszonego parsowania.
- planowany projekt `Saga.Api` z dokumentacji — `Saga.Web` jest web API tego repozytorium.

### CLI
#### Zmieniono
- `saga` jest konwerterem wsadowym: `saga <plik|katalog> [--output <katalog>] [--pattern <wzorzec>]`.
  Katalog przetwarza bez rekursji, domyślnie `*.docx`, a dokumenty nierozpoznane jako akty prawne
  pomija i wykazuje w podsumowaniu przebiegu.
#### Usunięto
- drukowanie modelu na konsolę, raport klasyfikacji, tryb zgodności `--docx` oraz `--dump`.

## [1.0.1] - 2026-07-06
### Dodano
- rozróżnianie paragrafów kodeksu od ustępów.
- odnośniki (przypisy dolne).
- podpisy pod aktem.

## [0.2.15] - 2026-06-29
### Dodano
- instrukcje nowelizacyjne typu uchyl.
- pliki wygenerowane przez konwerter w wersji 0.2.15. Wykorzystane zostały dokumenty ustaw dostarczone przez RCL.
### Zmieniono
- otaczanie przypisów dolnych nawiasami kwadratowymi w taki sposób, żeby brać pod uwagę białe znaki. Wewnątrz nawiasu wchodzi tylko treść a białe znaki trafiają poza nawias.
- wykrywanie numeru jednostki redakcyjnej, tak żeby pojawiający się znak paragrafu § lub przypisy po numerze nie uniemożliwiały identyfikacji numeru jednostki.
- klasyfikowanie jednostek redakcyjnych w trudych miejscach. Dodano większą wartość rozpoznawaniu na podstawie treści w porównianiu z klasyfikowaniem na podstawie stylu. Daje to większą trafność w przypadkach, w których błędne rozpoznanie numeru jednostki wcześniej uniemożliwiało poprawne zaklasyfikowanie jednostki.

## [0.2.14] - 2026-06-18
### Zmieniono
- refactor kodu konwerteru przygotowujący kod na pojawienie się nowych wartości z parsera. Każda jednostka otrzymała komentarza z gotowymi liniami kodu, które przypiszą zmienną do odpowiedniego atrybutu XML.
- przeniesiono cytat-strukt do zdania, w którym znajduje się cytat.

## [0.2.13] - 2026-06-17
### Dodano
- parsowanie tytułu ustawy oraz dat wydania i ogłoszenia. Kod jest tymczasowy, napisany jako proof of concept.
### Zmieniono
- wprowadzono zmiany zgdone ze zmianami schema:
    - atrybut ELI zamieniono na eId
    - dodano atrybut GUID do każdej jednostki redakcyjnej
    - elementy zdanie i zdanie-ref ujednolicono
- poprawiono błąd w logice budowania zdań

## [0.2.12] - 2026-05-22
### Dodano
- klasę `CommonPartXmlConverter`, która ujednolica przetwarzanie części wspólnych dla wsszystkich jednostek redakcyjnych.
- klasę `XLinqWrappers` dodającą przestrzeń nazw do tworzonych elementów xml i zastępującą wcześniej występującą pętlę po wszystkich wytworzonych elementach.

### Zmieniono
- zmniejszono widoczność klas z public na internal.
- poprawiono i uproszczono konwertery.

## [0.2.11] - 2026-05-20
### Dodano
- autoryzację.

## [0.2.10] - 2026-05-19
### Dodano
- części wspólne wyliczeń.

### Zmieniono
- cytat w tekście wpisywany jest w zakresie elementu zdania,
- poprawki zgodne z nową wersją schema (0.1.13).

## [0.2.9] - 2026-05-18
### Dodano
- przetwarzanie kontekstu zdań. Rozpoznawane jest kiedy fragment jest kontynuacją zdania i fragmenty są numerowane w odpowiedniej kolejności.

## [0.2.8] - 2026-05-12
### Dodano
- zdania wewnątrz elementów <p>, bez rozpoznawania czy dane zdanie jest kontynuacją zdania występującego we wcześniejszym elemencie <p>

## [0.2.7] - 2026-05-11
### Dodano
- endpoint umożliwiający konwersję dokumentu DOCX do formatu XML.
- Dockerfile
- swagger

### Zmieniono
- interfejs webowy został rozszerzony o przycisk wywołujący konwersję wybranego dokumentu do XML oraz umożliwiający pobranie wygenerowanego pliku XML.

## [0.2.6] - 2026-05-06
### Dodano
- patojednostki. Każda jednostka staje się wersją patologiczną jeśli jednostka nadrzędna jest patologiczna lub liczba podjednostek (np. liczba liter w punkcie) jest równa 1. Pominięto sytuację, w której nie jest zachowana struktura jednostek (ustęp skłąd się z liter, a nie z punktów), ponieważ nie jest ona jeszcze przewidywana przez parser.

## [0.2.5] - 2026-05-04
### Dodano
- nowe typy cytatów (nowelizacji) zamiast wcześniej wykorzystywanego cytat-strukt. Wprowadzono:
    - cytat-strukt - odnoszący się wyłącznie do cytatów zawierających strukturę (jednostki),
    - cytat-w-tekscie - cytat pojawiający się w zdaniu,
    - cytat-blok - cytat blokowy wykorzystywany do tabeli, grafik, wzorów mat.

## [0.2.4] - 2026-04-29
### Dodano
- pliki wygenerowane przez konwerter w wersji 0.2.3. Wykorzystane zostały dokumenty ustaw dostarczone przez RCL.

## [0.2.3] - 2026-04-27
### Dodano
- tymczasowe przetwarzanie nagłówka dokumentu ustaw. Na jego podstawie usatlany jest ciąg ELI dokumentu. Jest tymczasowe, ponieważ:
    - działa wyłącznie w przypadku poprawnie sformatowanych dokumentów ustaw (np. w projektach nagłówek jest zupełnie pomijany),
    - kod parsera nagłówka będzie musiał w przyszłości zostać wydzielony do osobnego pliku/modułu, żeby rozdzielić odpowiedzialność przetwarzania nagłówka od parsowania całego dokumentu.
- pole Publisher opisujące nazwę dziennika urzędowego.

## [0.2.2] - 2026-04-20
### Zmieniono
- usunięto flagę `generateGuids`, która w tej wersji nie była wykorzystywana i nie pełniła żadnej funkcji. W razie potrzeby może zostać łatwo przywrócona w przyszłości.
- przeorganizowano i pogrupowano stałe konwerterów. Wraz ze wzrostem ich liczby pojawiły się trudności w nawigacji, dlatego wprowadzono logiczne grupowanie oraz strukturę ułatwiającą dostęp do stałych.
- przygotowano konwertery do obsługi metadanych w generowanych plikach XML – dodano odpowiednie miejsca w kodzie pod przyszłe uzupełnienie o metadane.

## [0.2.1] - 2026-04-15
### Dodano
- pliki wygenerowane przez konwerter w wersji 0.1.0. Wykorzystane zostały dokumenty ustaw dostarczone przez RCL.

### Zmieniono
- rozszerzono opisy w CHANGELOG.md

## [0.2.0] - 2026-04-14
### Zmieniono
- dopasowano strukturę generowanych plików XML bliżej do XML Schema poprzez:
    - przypisanie istniejących wartości do metadanych
    - zmianę nazw elementów i atrybutów
    - przebudowę struktury nowelizacji

## [0.1.1] - 2026-04-09
### Zmieniono
- poprawiono kod wykrywający pojawienie się w tekście nowelizacji. Frazy wewnątrz tekstu były błędnie rozpoznawane jako zwroty rozpoczynające nowelizację kiedy nie było to ich celem.

## [0.1.0] - 2026-04-01
### Dodano
- opcjonalną flagę `--xml` umożliwiającą zapis odczytanego pliku DOCX do formatu XML.
- konwerter modelu danych do XML - zaimplementowano mechanizm przekształcający wewnętrzny model dokumentu legislacyjnego na strukturę XML, odwzorowując hierarchię jednostek redakcyjnych (np. artykuły, ustępy) oraz obsługując poprawki (amendments)

## [0.0.2] - 2026-03-31
### Dodano
- mirror [projektu rcl]
- CHANGELOG.md

## [0.0.1] - 2026-03-26
### Dodano
- utworzono projekt na gitlabie

[projektu rcl]: https://github.com/kujawiak/word-parser
