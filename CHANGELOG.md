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
- **Dane ogłoszenia dla szczotki** ([ADR-0081](docs/adr/0081-dane-ogloszenia-od-wywolujacego-uzupelniaja-tylko-braki-dokumentu.md)): usługa przyjmuje pola formularza
  `publisher` (DU/MP), `year`, `position`, `announcementDate` (RRRR-MM-DD), konsola — opcje `--publisher`, `--year`,
  `--position`, `--announced`, biblioteka — `ParseOptions.Publication`. Uzupełniają dane ogłoszenia, których dokument
  nie niesie (szczotka przed publikacją, akt bez nagłówka publikatora): eksport ma wtedy `Rok` i `Pozycja`, adresy
  z prefiksem aktu i daty wejścia w życie liczone od dnia ogłoszenia. Dane z dokumentu wygrywają; błędna wartość to
  odpowiedź 400 z opisem.

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
- **Organ wydający w metadanych** ([ADR-0041](docs/adr/0041-organ-ktory-sam-podpisuje-jest-jednym-organem-wydajacym.md)):
  rozporządzenie, uchwała, zarządzenie i obwieszczenie mają w `Organy` organ ze strefy tytułowej zamiast `UNKNOWN`.
  Organ, który sam podpisuje akt, jest jednym organem wydającym z osobą podpisującą — nazwa w mianowniku pochodzi
  z formuły podpisu („Minister Finansów"), jak w przykładzie autorów schematu. Organ kolegialny i podpisujący go
  przewodniczący („Rada Ministrów" — „Prezes Rady Ministrów") oraz kontrasygnata to osobne organy; akt wydany
  wspólnie ma organ wydający dla każdego podpisującego. Bez odczytanego podpisu nazwę daje strefa tytułowa,
  sprowadzona do mianownika i pisowni nazwy (`AuthoritiesXmlConverter`). Marszałek Sejmu dostaje zasób ELI
  z przykładu autorów schematu.
  - W korpusie: organ wydający `UNKNOWN` w 2079 → 0 dokumentów; zmiany wyłącznie w `Organy`, zgodność ze
    schematem bez zmian.
- **Myślnik bez wprowadzenia z dwukropkiem nie otwiera tiretu** ([ADR-0042](docs/adr/0042-myslnik-bez-wprowadzenia-z-dwukropkiem-nie-otwiera-tiretu.md)):
  akapit zaczynający się od myślnika, stojący pod ustępem bez punktów i liter, jest tiretem tylko po wprowadzeniu
  zakończonym dwukropkiem (§ 56 ZTP) albo w stylu tiretu. W innym razie domyka urwane zdanie przepisu („Za
  niewykonanie obowiązku … – wymierza się karę pieniężną …") albo jest jego dalszą treścią — za wzorem jako
  wprowadzenie do opisu symboli („– gdzie poszczególne symbole oznaczają:"). Przedtem dostawiany był punkt i litera,
  a ustęp wychodził jako jednostka spoza ZTP.
  - W korpusie: 9 dokumentów; 9 `nie-ztp-ustep` staje się zwykłym ustępem, 12 wierszy legendy wzoru wychodzi jako
    `opis` zamiast `fragment`; jednostek `nie-ztp-*` 618 → 600. Zgodność ze schematem bez zmian.
- **Odnośniki w strefie tytułowej, podstawie prawnej i tytule jednostki systematyzacyjnej**: znacznik odnośnika
  przy organie wydającym („MINISTRA FINANSÓW¹⁾" — z jakiego przepisu minister kieruje działem), w podstawie
  prawnej („… z późn. zm.²⁾") i w tytule rozdziału albo działu staje się elementem `odnosnik` z treścią przypisu
  (`InlineTextXml`). Przedtem te odnośniki nie trafiały do eksportu, a w tekście zostawał zapis „[1)]"; kolejne
  odwołania do nich też zostawały tekstem.
  - W korpusie: 1233 odnośniki więcej w 951 dokumentach (12 325 → 13 558) i 121 odsyłaczy do nich; znaczników
    zostających w tekście 1564 → 246. Zgodność ze schematem bez zmian.
- **Obwieszczenie, które nie ogłasza aktu** ([ADR-0043](docs/adr/0043-obwieszczenie-ktore-nie-oglasza-aktu-jest-korzeniem.md)):
  obwieszczenie o stawkach, wskaźnikach, wykazach albo wolnych stanowiskach wychodzi pod korzeniem `obwieszczenie`
  z organem wydającym, podstawą prawną („Na podstawie art. … ogłasza się …"), podpisem i własnymi załącznikami
  („Załącznik do obwieszczenia Ministra … z dnia …"). Przedtem eksport pisał pusty element `ustawa`, a wiersz
  załącznika otwierał „ogłaszany akt", którego nie było. O tym, czy obwieszczenie ogłasza akt, rozstrzyga jego
  przedmiot albo rodzaj aktu tuż za wierszem załącznika (`AnnouncementBoundary.Describe`).
  - W korpusie: 280 dokumentów; zgodnych ze schematem 1685 → 1915 z 2321; pokrycie tekstu obwieszczeń 10 % → 83 %.
    Treść poza podstawą prawną (wyliczenie, dalsze akapity) nie jest jeszcze eksportowana.
- **Treść obwieszczenia bez numerowanych punktów** ([ADR-0044](docs/adr/0044-tresc-obwieszczenia-bez-punktow-jest-przepisem-bez-oznaczenia.md)):
  wyliczenie po wprowadzeniu („… ogłasza się następujące stawki:" i „1) …", z literami i tiretami), dalsze akapity
  i tabela przed podpisem wychodzą jako jeden przepis bez oznaczenia (`nie-ztp-artykul`), zbudowany tym samym
  potokiem co treść aktu (`AnnouncementBody`); punkty adresują się od obwieszczenia (`pkt_1__lit_a`). Wiersz
  „Załączniki do obwieszczenia …" w liczbie mnogiej otwiera załączniki.
  - W korpusie: 108 obwieszczeń, 500 punktów, 158 liter, 42 tirety; pokrycie tekstu obwieszczeń 83 % → 99 %.
    Zgodność ze schematem bez zmian. Jednostek `nie-ztp-*` 600 → 1408 — schemat nie przyjmuje pod takim
    przepisem jednostek zwykłych.
- **Jednostka podstawowa ogłaszanego aktu z jego nagłówka**: w tekście jednolitym o tym, że jednostką podstawową
  jest paragraf, mówi także nagłówek aktu stojący za wierszem „Załącznik do obwieszczenia …" („ROZPORZĄDZENIE",
  „UCHWAŁA", „ZARZĄDZENIE"), nie tylko klasyfikator dokumentu, który waży liczbę jednostek — krótkie rozporządzenie
  ma ich za mało i wychodziło bez żadnego paragrafu (`AnnouncementBoundary.Layout.AnnouncedKind`).
  - W korpusie: 20 tekstów jednolitych rozporządzeń odzyskuje treść (64 paragrafy); zgodnych ze schematem
    1915 → 1933 z 2321; zgubionych akapitów w tekstach jednolitych 1072 → 230.
- **Obwieszczenie zbudowane z paragrafów**: obwieszczenie, które nie ogłasza aktu, a samo ma przepisy („Na podstawie
  art. … ogłasza się, co następuje:" i „§ 1. …", „§ 2. …"), wychodzi pod korzeniem `obwieszczenie` z `podstawa-prawna`,
  paragrafami jako jednostkami podstawowymi, podpisem i załącznikami obwieszczenia. Przepisy buduje potok aktu;
  o tym, że jednostką podstawową jest paragraf, mówi sama treść (`AnnouncementBoundary.Layout.HasSections`).
  - W korpusie: 4 dokumenty; zgodnych ze schematem 1933 → 1937 z 2321. Żaden dokument korpusu nie wychodzi już
    bez treści.
- **Część wspólna w stylu części wspólnej bez myślnika**: akapit w stylu CZ_WSP_* stojący tuż za wyliczeniem zamyka je
  także wtedy, gdy nie zaczyna się od myślnika („1) urlopu bezpłatnego, … 5) nieusprawiedliwionej nieobecności"
  i w nowym wierszu „ulega proporcjonalnemu obniżeniu …") — styl i miejsce to dwa zgodne sygnały. Kolejny taki akapit
  za wyliczeniem już zamkniętym (drugie brzmienie w tekście jednolitym) jest dalszą treścią części wspólnej, a akapit,
  który niczego nie zamyka — dalszą treścią przepisu. Przedtem wszystkie trzy ginęły z modelu i eksportu.
  - W korpusie: 70 dokumentów; 90 części wspólnych i 20 akapitów dalszej treści więcej, 10 odnośników odzyskanych;
    zgubionych akapitów w tekstach jednolitych 230 → 137. Zgodność ze schematem bez zmian.
- **Preambuła ustawy**: akapity między tytułem a pierwszą jednostką („W trosce o …", „– uznając, że …", „uchwala się,
  co następuje:") trafiają do `LegalDocument.Preamble` i wychodzą jako `preambula` tuż za tytułem — jeden `p` na
  akapit, jak w przykładzie autorów schematu; w tekście jednolitym preambuła stoi w ogłaszanym akcie. Przedtem
  ginęły jako akapity sprzed pierwszego artykułu.
  - W korpusie: 23 teksty jednolite ustaw, 61 akapitów; zgubionych akapitów w tekstach jednolitych 137 → 79.
    Zgodność ze schematem bez zmian.
- **Nagłówek obwieszczenia w pisowni spoza wzorca**: pierwszy wiersz dokumentu zaczynający się słowem „Obwieszczenie"
  jest nagłówkiem obwieszczenia także wtedy, gdy organ stoi w nim małą literą („Obwieszczenie marszałka Sejmu
  Rzeczypospolitej Polskiej") — pisownię potwierdza miejsce w dokumencie (`AnnouncementBoundary`). Zdanie
  zaczynające się tym słowem nagłówkiem nie jest.
  - W korpusie: jeden tekst jednolity odzyskuje obwieszczenie (punkty, przytoczony przepis, podpis).
- **Zwrot zmiany brzmienia, który niczego nie zapowiada** ([ADR-0045](docs/adr/0045-zwrot-zmiany-brzmienia-zapowiada-tresc-gdy-zostawia-ja-otwarta.md)):
  przepis podający brzmienie w tym samym akapicie („posługuje się nazwą w brzmieniu: Polish Air Navigation Services
  Agency", „ślubowanie w brzmieniu: „Ślubuję …"", wzór z cytowanymi słowami „otrzymuje brzmienie: …") nie jest
  komendą nowelizacyjną. Wyzwalacz treści cytowanej uzbraja tylko zwrot kończący akapit albo cytat otwarty za nim
  i niedomknięty (`AmendmentFinalizer.LeavesWordingOpen`). Przedtem taki przepis otwierał cytat, który — zwłaszcza
  w dokumencie bez stylów — połykał dalszą część aktu.
  - W korpusie: 5 dokumentów ze stylami (rozporządzenie odzyskuje dziesięć paragrafów i załącznik); bez stylów
    zgodność struktury tekstów jednolitych 99,55 % → 99,81 % jednostek.
- **Miara zgodności bez stylów liczy wszystkie jednostki** (`CorpusValidator --styleless-parity`,
  `--compare-reference`): także paragraf aktu wykonawczego, jednostki spoza ZTP (`nie-ztp-*`), punkty obwieszczenia
  i preambułę. Przedtem miara ich nie widziała i zawyżała wynik rozporządzeń (97,96 % → rzeczywiste 97,22 %).
- **Część wspólna bez stylu i bez myślnika** ([ADR-0046](docs/adr/0046-akapit-mala-litera-za-pozycja-wyliczenia-jest-czescia-wspolna.md)):
  akapit bez oznaczenia zaczynający się małą literą, tuż za pozycją wyliczenia zakończoną przecinkiem albo bez
  znaku końcowego, zamyka wyliczenie jako część wspólna („… 2) niewykonania rekultywacji gruntów," i w nowym wierszu
  „stosuje się przepisy niniejszej ustawy."). Przedtem rozpoznawał ją tylko styl akapitu; bez niego akapit był
  dalszą treścią ostatniej pozycji.
  - W korpusie: 10 dokumentów ze stylami (11 akapitów); bez stylów w pełni zgodnych z wynikiem stylowym jest
    591 zamiast 562 tekstów jednolitych.
  - Ta sama reguła obowiązuje w treści cytowanej w nowelizacji (`AmendmentBuilder`) — nowe brzmienie ustępu
    z wyliczeniem i częścią wspólną bez myślnika. W korpusie bez zmian.
- **Znacznik odnośnika bez odnośnika wychodzi indeksem górnym** ([ADR-0047](docs/adr/0047-znacznik-bez-odnosnika-tego-aktu-wychodzi-indeksem-gornym.md)):
  znacznik, za którym nie stoi odnośnik tego aktu — odnośnik aktu cytowanego, objaśnienie wpisane akapitem pod
  tabelą — wychodzi jako `sup` („²⁾"), tak jak stoi w akcie. Przedtem eksport niósł zapis roboczy parsera („[2)]").
  Nawias kwadratowy z innym tekstem (wzór wpisany w treść) pozostaje tekstem.
  - W korpusie: 214 znaczników w 70 dokumentach; zgodność ze schematem i pokrycie tekstu bez zmian.
- **Treść obwieszczenia, z której nie powstały jednostki, nie ginie**: akapity i tabele obwieszczenia, których potok
  nie złożył w przepis (np. cytat wieloakapitowy w obwieszczeniu o sprostowaniu błędów) albo które stoją przed
  pierwszym punktem, wychodzą jako przepis bez oznaczenia o płaskiej budowie — pierwszy akapit jest jego tekstem,
  reszta dalszą treścią w kolejności dokumentu (`AnnouncementXmlConverter.LooseContent`). Struktury się tam nie
  zgaduje; przedtem ta treść była w modelu, ale nie w eksporcie.
  - W korpusie: jedno obwieszczenie (35 akapitów); pokrycie tekstu obwieszczeń 100 %.
- **Tytuł jednostki systematyzacyjnej bez progu długości**: akapit bez stylu tuż za oznaczeniem („Rozdział 2") jest
  tytułem jednostki, gdy zaczyna się wielką literą i nie kończy jak zdanie ani wprowadzenie do wyliczenia (kropka,
  dwukropek, średnik, przecinek) — bez względu na długość. Dotychczasowy próg 200 znaków pochodził z rozkładu
  długości tytułów w korpusie i gubił długie tytuły rozdziałów aktów wykonawczych; dwukropek na końcu nie chronił
  dotąd przed wzięciem wprowadzenia do wyliczenia za tytuł.
  - W korpusie: 9 dokumentów, 22 tytuły rozdziałów odzyskane; żaden dotychczasowy tytuł nie zniknął.
- **Adnotacja o uchyleniu w miejscu tytułu jednostki**: akapit w nawiasie kończący się słowem „uchylony" albo
  „pominięty", stojący tuż za oznaczeniem jednostki systematyzacyjnej, zajmuje miejsce jej tytułu także wtedy, gdy
  podaje zakres („(zawierający art. 19–21 – uchylony)") — dotąd rozpoznawane było samo „(uchylony)", a dłuższa
  adnotacja ginęła albo doklejała się do poprzedniego przepisu.
  - W korpusie: 22 adnotacje w 5 tekstach jednolitych; zgubionych akapitów w tekstach jednolitych 62 → 40.
- **Tytuł załącznika bez stylu i bez wyśrodkowania**: pierwszy wiersz załącznika o budowie tytułu (wielka litera,
  bez znaku końca zdania ani wprowadzenia do wyliczenia), który nie jest pozycją wyliczenia, jest jego tytułem —
  ta sama reguła co dla tytułu jednostki systematyzacyjnej (`TitleForm`). Dotąd tytuł poznawał tylko styl albo
  wyśrodkowanie, więc w dokumencie bez szablonu tytuł był akapitem treści, a wyliczenie za nim nie stawało się
  jednostkami.
  - W korpusie: 58 tytułów w 50 dokumentach ze stylami (wiersze bez stylu tytułu); bez stylów wyliczenia
    w załącznikach odzyskują 397 jednostek — zgodność struktury tekstów jednolitych 99,83 % → 99,90 %, ustaw
    99,67 % → 99,80 %.
- **Akt ogłaszany w obwieszczeniu nazywa jego rodzaj, a obwieszczenie nie znika**: element aktu ogłaszanego
  w załączniku wynika z rodzaju nazwanego jego nagłówkiem (`ustawa`, `kodeks`, `rozporzadzenie`, `uchwala`,
  `zarzadzenie`), a gdy nagłówek rodzaju nie nazywa — `inny-akt-prawny`. Nie zależy już od tego, czy akt ma jednostki
  „§": uchwała zbudowana z artykułów (regulamin izby) jest uchwałą. Przedtem w takim dokumencie eksport wracał do
  korzenia `ustawa`, gubiąc całe obwieszczenie (punkty, przytoczone przepisy, podpis).
  - W korpusie: jeden tekst jednolity (Regulamin Senatu) odzyskuje obwieszczenie; zgubionych akapitów w tekstach
    jednolitych 40 → 18. Zgodność ze schematem bez zmian.
- **Głębokość tiretu zapisana myślnikami bez limitu szablonu**: zagnieżdżenie zapisane w treści („– – – –") jest
  poziomem tiretu takim, jak je zapisano. Dotąd było przycinane do trzech poziomów — tylu, ile stylów tiretu ma
  szablon — i czwarty poziom stawał się kolejnym tiretem trzeciego. Limit zostaje tylko dla głębokości
  wnioskowanej z wcięcia.
- **Treść cytowana bez cudzysłowu otwierającego nie połyka reszty aktu** ([ADR-0048](docs/adr/0048-tresc-cytowana-bez-cudzyslowu-konczy-kolejny-artykul-aktu-zmieniajacego.md)):
  w dokumencie bez stylów treść nowelizacji, której cudzysłów otwierający zgubiono, kończy kolejny artykuł aktu
  zmieniającego (numer o jeden większy od artykułu z komendą), formuła podpisu albo cudzysłów zamykający bez
  otwarcia. Przedtem trwała do końca dokumentu, a dalsze artykuły ustawy zmieniającej stawały się cytowanymi.
  - W korpusie ze stylami bez zmian; bez stylów zgodność struktury rozporządzeń 97,22 % → 97,31 %.
- **Cytat niedomknięty nie wchłania reszty aktu zmieniającego** ([ADR-0049](docs/adr/0049-cytat-niedomkniety-do-konca-dokumentu-konczy-ponowny-przebieg.md)):
  gdy w dokumencie bez stylów zabraknie cudzysłowu zamykającego, cytat trwał do końca dokumentu — z przepisem
  o wejściu w życie i podpisem w środku. Dokument, który kończy się w otwartym cytacie, buduje się teraz ponownie,
  a cytat kończy się przed artykułem, od którego numeracja aktu zmieniającego biegnie dalej. Artykuł o tym samym
  numerze w cytacie kilku artykułów zostaje treścią cytowaną. Jednostka z komendą zachowuje ostrzeżenie.
  - W korpusie ze stylami bez zmian (0 z 2321 dokumentów). Bez stylów jedno rozporządzenie (dwa pliki) odzyskuje końcowy „§ 2"; cytat nie domykał się tam, bo cudzysłów zamykający cytowaną tabelę stoi w jej ostatniej komórce, a tabela nie bierze udziału w bilansie cudzysłowów (zgodność struktury rozporządzeń 97,31 % → 97,32 %).
- **Cudzysłowy cytowanej tabeli liczą się w bilansie cytatu** ([ADR-0050](docs/adr/0050-cudzyslowy-cytowanej-tabeli-licza-sie-w-bilansie-cytatu.md)):
  cytowaną tabelę zamyka cudzysłów w jej ostatniej komórce („”,"), a tabela, która cudzysłów otwiera i go nie
  zamyka, nie kończy cytatu — biegnie on dalej, do cudzysłowu zamykającego. Przedtem tabela w bilansie się nie
  liczyła: bez stylów cytat nie domykał się i wchłaniał kolejne jednostki aktu zmieniającego.
  - **Objaśnienie pod cytowaną tabelą** (`ContentTable.Description`, w eksporcie `opis-tabeli`): akapit bez
    oznaczenia za tabelą cytowaną wprost należy do niej. Przedtem wypadał z cytatu jako `fragment` przepisu
    zmieniającego, z osieroconym cudzysłowem zamykającym.
  - W korpusie ze stylami zmienia się jedno rozporządzenie (dwa pliki): 18 objaśnień pod cytowanymi tabelami przechodzi z `fragment` przepisu zmieniającego do `opis-tabeli`; zgodnych ze schematem jest nadal 1937 z 2321, pokrycie tekstu bez zmian. Bez stylów zgodność struktury rozporządzeń rośnie z 97,32 % do 97,35 %: rozporządzenie z cudzysłowem zamykającym w komórce tabeli (dwa pliki) nie gubi już jednostek aktu zmieniającego.
- **Cudzysłów graniczny w komórce cytowanej tabeli należy do cytatu** ([ADR-0051](docs/adr/0051-cudzyslow-graniczny-w-komorce-tabeli-nalezy-do-cytatu.md)):
  „„" w pierwszej i „”," w ostatniej komórce są zdejmowane z tabeli jak z akapitu — cytat ma w eksporcie
  `cudzyslowy="oba"`, a interpunkcja komendy stoi za cytatem. Kolumna, w której nie było nic oprócz cudzysłowu,
  znika razem z nim. Wyrażenie w cudzysłowie zamknięte w tej samej komórce zostaje treścią tabeli.
  - W korpusie zmieniają się dwa rozporządzenia (cztery pliki): z cytowanych tabel znika 346 komórek kolumn, w których stał wyłącznie cudzysłów, a cytaty dostają w eksporcie cudzysłów otwierający albo zamykający, którego dotąd nie miały. Zgodnych ze schematem jest nadal 1937 z 2321, pokrycie tekstu bez zmian; żadna inna tabela korpusu się nie zmienia.
- **Zmiana wyrazów jako instrukcja `now:zamien-tekst`** ([ADR-0052](docs/adr/0052-zmiana-wyrazow-jest-instrukcja-zamien-tekst.md)):
  „wyrazy „X" zastępuje się wyrazami „Y"" wychodzi instrukcją z wyrazami zastępowanymi i zastępującymi
  (`stary-tekst`, `nowy-tekst`), przed tekstem zdania — jak w przykładach autorów schematu. Przedtem był to
  `cytat-w-tekscie` z samymi nowymi wyrazami; wyrazów zastępowanych nie było ani w modelu, ani w eksporcie.
  - Model: `Amendment.TextChanges` (`TextChange`: `OldText`, `NewText`); zmiana wyrazów nie ma treści cytowanej
    (`Content.PlainText` nie powtarza już nowych wyrazów).
  - W korpusie 1481 cytatów `cytat-w-tekscie` w 307 plikach staje się instrukcjami `now:zamien-tekst`; poza tym eksport się nie zmienia, a zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory). W przykładach autorów schematu każda nasza para (`stary-tekst`, `nowy-tekst`) ma odpowiednik: DU/2026/26 — 5 z 5 (przykład ma 9 instrukcji), DU/2026/252 — 35 z 35 różnych par (przykład ma 83 instrukcje); brakujące to postaci jeszcze nieobsługiwane. Indeks górny w wyrazach stoi w atrybucie zapisem kanonicznym („art. 406[1]"), bo atrybut nie niesie znaczników (5 w korpusie).
- **Dodanie wyrazów jako zmiana w tekście** ([ADR-0052](docs/adr/0052-zmiana-wyrazow-jest-instrukcja-zamien-tekst.md)): „po wyrazach „X" dodaje się
  wyrazy „Y"" i „przed wyrazami „X" dodaje się wyrazy „Y"" wychodzą instrukcją `now:zamien-tekst`, w której
  wyrazy wskazane zastępuje się nimi samymi razem z dodanymi (`stary-tekst="X"`, `nowy-tekst="X Y"`) — schemat nie
  ma osobnej instrukcji dodania wyrazów, a tak zapisują to przykłady jego autorów. „Dodaje się przecinek i wyrazy"
  dokłada przecinek; dodane wyrazy zaczynające się znakiem przestankowym stoją bez odstępu
  (`AmendmentCommandParser.JoinWords`). Przedtem komenda nie dawała żadnej instrukcji, tylko ostrzeżenie.
  - W korpusie przybywa 512 instrukcji `now:zamien-tekst` w 119 plikach; poza tym eksport się nie zmienia, a zgodnych ze schematem jest nadal 1937 z 2321. W przykładzie autorów schematu DU/2026/252 mamy teraz 66 z 83 instrukcji i każda z 39 naszych różnych par (`stary-tekst`, `nowy-tekst`) ma tam odpowiednik.
- **Skreślenie wyrazów jako zmiana w tekście i wszystkie zmiany jednego zdania**
  ([ADR-0053](docs/adr/0053-skreslenie-wyrazow-to-zamiana-na-pusty-tekst.md)): „skreśla się wyrazy „X"" wychodzi instrukcją `now:zamien-tekst` z pustym
  `nowy-tekst` (przedtem: żadnej instrukcji, tylko ostrzeżenie), a zdanie z kilkoma zmianami w tekście („wyrazy
  „A" zastępuje się wyrazami „B" oraz po wyrazach „C" dodaje się wyrazy „D"") daje instrukcję dla każdej,
  w kolejności zapisu (przedtem: tylko dla pierwszej).
  - W korpusie przybywa 379 instrukcji `now:zamien-tekst` w 127 plikach; poza tym eksport się nie zmienia, a zgodnych ze schematem jest nadal 1937 z 2321. Przykłady autorów schematu: DU/2026/26 — 7 z 9 instrukcji, DU/2026/252 — 70 z 83; poza dwoma skreśleniami (inna postać w DU/2026/26, dodany odstęp w DU/2026/252) każda nasza para (`stary-tekst`, `nowy-tekst`) ma w przykładzie odpowiednik.
- **Zamiana znaku kończącego jednostkę** ([ADR-0054](docs/adr/0054-zamiana-znaku-konczacego-jednostke-ma-wlasny-cel.md)): „w pkt 20 kropkę zastępuje się
  średnikiem i dodaje się pkt 21 w brzmieniu:" daje instrukcję `now:zamien-tekst` (`stary-tekst="."`,
  `nowy-tekst=";"`, `now:wystapienie="ostatnie"`) na jednostce sprzed znaku — obok cytatu z dodawaną jednostką,
  jak w przykładach autorów schematu. Wyrazy dodawane zaraz za nowym znakiem należą do tej samej zmiany.
  - **Naprawione uchylenie złej jednostki**: w zdaniu „w pkt 4 średnik zastępuje się kropką i uchyla się pkt 5"
    `now:uchyl` wskazywał pkt 4 — jednostkę, w której zmienia się znak. Teraz wskazuje jednostkę nazwaną po
    komendzie (pkt 5, także przedział „pkt 2–7").
  - Model: `TextChange.Occurrence` (`TextOccurrence.Last`) i `TextChange.Targets` — własny cel zmiany, gdy jest
    inny niż cele nowelizacji.
  - W korpusie przybywa 463 instrukcji `now:zamien-tekst` w 196 plikach, a 20 zdań z uchyleniem za zamianą znaku wskazuje teraz jednostkę uchylaną (w tym przedziały „pkt 2–7"); poza tym eksport się nie zmienia, zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory). Przykłady autorów schematu: DU/2026/26 — wszystkie 7 różnych par (`stary-tekst`, `nowy-tekst`) i wszystkie 9 instrukcji mają u nas odpowiednik, DU/2026/252 — 81 instrukcji wobec 83.
- **Wyrazy z wyrażeniem w cudzysłowie**: wyrazy zastępowane, dodawane i skreślane, które same zawierają
  wyrażenie w cudzysłowie („po wyrazach „zwanej dalej „ustawą","" dodaje się wyrazy …"), są czytane do własnego
  cudzysłowu zamykającego (`AmendmentCommandParser.Quoted`). Przedtem komenda z takim brzmieniem nie dawała żadnej
  instrukcji.
  - W korpusie przybywa 28 instrukcji `now:zamien-tekst` w 23 plikach; poza tym eksport się nie zmienia, zgodnych
    ze schematem jest nadal 1937 z 2321.
- **Komenda dodająca wskazuje miejsce dodania** ([ADR-0055](docs/adr/0055-dodanie-wskazuje-miejsce-nie-jednostke-dodawana.md)): „w art. 13 dodaje się
  ust. 6 w brzmieniu:" wychodzi jako `funkcja="dodaj-w"` z art. 13 w `element-eId` — jak w przykładach autorów
  schematu. Przedtem celem była jednostka dodawana („dodaj po ust. 6"), a kilka jednostek dodawanych jedną komendą
  dawało przedział albo osobne cytaty. Komenda z miejscem („po ust. 5 dodaje się ust. 5a") zostaje przy `dodaj-po`.
  - W korpusie zmienia się 487 zdań w 183 plikach: cytatów z funkcją `dodaj-w` jest 554 (było 94), a kilka jednostek dodawanych jedną komendą stoi w jednym cytacie (75 cytatów mniej). Zgodnych ze schematem jest nadal 1937 z 2321. Komendy zgodne z przykładami autorów schematu co do jednostki, elementu, funkcji i celu: DU/2026/26 — 102 z 103 (było 99), DU/2026/252 — 337 z 344 (było 315).
- **Miara komend nowelizacyjnych wobec przykładów** (`CorpusValidator --compare-reference`): nowa sekcja zestawia
  cytaty i instrukcje przepisów aktu z plikiem referencyjnym — jednostka, element, funkcja, cel, rodzaj treści
  cytatu, a przy zmianie w tekście tekst zastępowany, zastępujący i wystąpienie. Położenie instrukcji w zdaniu nie
  bierze udziału (przykłady nie są w tym zgodne między sobą).
- **Obudowanie dotychczasowej treści ustępem** ([ADR-0056](docs/adr/0056-obudowanie-dotychczasowej-tresci-jest-instrukcja-na-jednostce.md)): „w art. 24 dotychczasową
  treść oznacza się jako ust. 1 i dodaje się ust. 2 w brzmieniu:" daje instrukcję `now:dodaj-ustep` na art. 24
  (w kodeksie „jako § 1" → `now:dodaj-paragraf`), obok cytatu z dodawanym ustępem — także gdy przepis niczego nie
  dodaje („…jako ust. 1 i:"). Model: `Amendment.WrapsContentAs`, `Amendment.WrappedUnit`.
  - W korpusie przybywa 47 instrukcji `now:dodaj-ustep` (38 plików) i 6 `now:dodaj-paragraf` (5 plików) — wszystkie 53 zdania z tym zwrotem; poza tym eksport się nie zmienia, zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory). Komendy zgodne z przykładami autorów schematu (`CorpusValidator --compare-reference`): DU/2026/26 — 102 ze 105, DU/2026/252 — 333 z 345.
- **Zdanie przytoczone w całości jako `zdanie`** ([ADR-0057](docs/adr/0057-zdanie-przytoczone-w-calosci-jest-zdaniem.md)): gdy przedmiotem komendy
  jest zdanie („zdanie pierwsze otrzymuje brzmienie:", „dodaje się zdanie drugie w brzmieniu:"), `cytat-w-tekscie`
  niesie elementy `zdanie` z numerami, jak w przykładzie autorów schematu. Wprowadzenie do wyliczenia, tytuł
  i wyrazy zostają elementem `tekst`. Model: `AmendmentContent.PlainTextIsSentences`.
  - W korpusie 115 cytatów tekstowych w 52 plikach niesie teraz `zdanie` zamiast `tekst` (116 zdań); poza tym eksport się nie zmienia, zgodnych ze schematem jest nadal 1937 z 2321. Przykłady autorów schematu: DU/2026/26 — komendy 103 ze 105, treść zdań 466 z 469; DU/2026/252 — komendy 333 z 345.
- **Załącznik zmieniany odesłaniem do załącznika aktu zmieniającego** ([ADR-0058](docs/adr/0058-zalacznik-zmieniany-odeslaniem-jest-instrukcja-z-adresami-obu-zalacznikow.md)):
  „załącznik nr 1 do rozporządzenia otrzymuje brzmienie określone w załączniku nr 2 do niniejszego rozporządzenia"
  daje instrukcję `now:zastap`, a „dodaje się załączniki nr 3 i 4 … w brzmieniu określonym w załącznikach …" —
  `now:dodaj-po`, każda z adresem załącznika aktu zmienianego i zmieniającego. Przedtem przepis nie dawał żadnej
  nowelizacji. Model: `Amendment.AnnexInstructions`.
  - W korpusie przybywa 86 instrukcji `now:zastap` (55 plików) i 20 `now:dodaj-po` (18 plików) — 91 zdań; poza tym eksport się nie zmienia, zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory). W przykładzie autorów schematu DU/2026/252 wszystkie trzy instrukcje załączników są takie same jak nasze, atrybut w atrybut; komendy zgodne z tym przykładem: 336 z 345.
- **Cytowany odnośnik** ([ADR-0059](docs/adr/0059-odnosnik-jako-przedmiot-komendy-jest-cytowanym-odnosnikiem.md)): „odnośnik nr 1 do tytułu ustawy otrzymuje
  brzmienie:" i „dodaje się odnośnik nr 2 w brzmieniu:" dają cytat strukturalny z `odnosnik-cyt` pod adresem `odn_N`
  — jak w przykładach autorów schematu. Treść odnośnika nie jest już budowana jako punkty (zaczyna się znacznikiem
  „1)") ani eksportowana jako tekst bez adresu. Model: `AmendmentContent.Footnote`, `StructuralReference.Footnote`.
  - W korpusie 40 cytowanych odnośników w 28 plikach wychodzi jako `odnosnik-cyt`: 27 było tekstem cytowanym bez adresu, 13 — jednostkami (znika 17 błędnych punktów i liter aktu zmienianego). Zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory), pokrycie tekstu bez zmian. W przykładzie autorów schematu DU/2026/252 oba cytowane odnośniki są takie same jak nasze; komendy zgodne: 338 z 345, a pozostałe 7 wymaga wiedzy spoza aktu zmieniającego.
- **Wersje czasowe jednostek tekstu jednolitego** ([ADR-0060](docs/adr/0060-jednostka-drukowana-dwa-razy-to-dwie-wersje-rozrozniane-okresem.md)): jednostka drukowana
  dwa razy — w brzmieniu obowiązującym („W tym brzmieniu obowiązuje do wejścia w życie zmiany …") i przyszłym —
  wychodzi z tym samym eId i różnym `okres`. Zmiana jest jednym zdarzeniem (`wejscie-w-zycie` albo `uchylenie`),
  które zamyka jedną grupę czasową i otwiera drugą; zdarzenie bez dnia nie ma `data`, a termin zapisany prozą
  trafia do `warunek`. Jednostki niosą projekcje `obowiazuje-do` / `wejscie-w-zycie`, a uchylana —
  `stan-jednostki="uchylony"`. Model: `BaseEntity.Version` (`WordingVersion`).
  - W korpusie zmienia się 112 z 728 tekstów jednolitych — wyłącznie atrybuty czasu jednostek i dane czasowe (607 grup czasowych, 362 zdarzenia: 140 z dniem, 99 z terminem zapisanym prozą, 123 bez dnia; 501 jednostek z `obowiazuje-do`, 96 ze `stan-jednostki`). Z 591 jednostek o powtórzonym eId 477 rozróżnia okres. Zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory); treść bez zmian.
- **Oznaczenie odnośnika jak w akcie** ([ADR-0061](docs/adr/0061-odnosnik-ma-oznaczenie-jak-w-akcie-a-znacznik-numer-jednoznaczny.md)): numeracja odnośników wznawiana
  od sekcji dokumentu (`w:footnotePr/w:numRestart`) i jej zapis (`w:numFmt`) są odczytywane — odnośnik „1)"
  załącznika nie wychodzi już jako „12)", a odnośnik obwieszczenia jako „a)". `symbol` niesie oznaczenie,
  `id-odnosnika` — numer jednoznaczny w dokumencie. Powtórzony znacznik (w pliku zwykły tekst z numerem
  drukowanym) wskazuje odnośnik swojej sekcji, a znacznik wpisany tekstem nie przejmuje odnośnika dalszej sekcji.
  Model: `Footnote.Marker`, `Footnote.Run`; wspólne rozstrzyganie znacznika: `FootnoteResolver`.
  - W korpusie zmienia się 225 z 2321 dokumentów — te, w których odnośniki stoją w więcej niż jednej sekcji: 3284 odnośniki w 223 plikach dostają oznaczenie jak w akcie. 115 odsyłaczy, które wskazywały odnośnik innej sekcji albo odnośnik przejęty przez znacznik wpisany tekstem (przytoczony odnośnik aktu zmieniającego, objaśnienie pod tabelą), wychodzi indeksem górnym, 9 powtórzonych znaczników zyskuje odsyłacz, a przejęte odnośniki wracają na swoje miejsce (2 dotąd nieosadzone). Wersje czasowe ([ADR-0060](docs/adr/0060-jednostka-drukowana-dwa-razy-to-dwie-wersje-rozrozniane-okresem.md)): okres rozróżnia 479 z 591 jednostek o powtórzonym eId (było 477), `obowiazuje-do` niesie 514 jednostek (było 501). Zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory), pokrycie tekstu bez zmian.
- **Termin zmiany zapisany przy akcie zmieniającym** ([ADR-0062](docs/adr/0062-termin-zmiany-zapisany-przy-akcie-zmieniajacym-obejmuje-jego-zmiany.md)): odnośnik tekstu
  jednolitego, który nazywa akt odesłaniem („Dodany przez art. 5 pkt 2 ustawy, o której mowa w odnośniku 3"),
  bierze termin tego aktu z odnośnika, w którym akt nazwano w pełni („…, która wejdzie w życie z dniem …").
  Jednostka dodana takim aktem jest rozpoznawana jako brzmienie przyszłe, a zmiany jednego aktu czekają na jedno
  zdarzenie. Model: `WordingVersion.TermFootnote`.
  - W korpusie zmienia się 35 tekstów jednolitych. Zdarzeń zmiany bez dnia i bez terminu zostaje 7 (było 123), z dniem jest 149 (było 140), a wszystkich 255 (było 362) — zmiany jednego aktu czekają na jedno zdarzenie. Okres wersji niesie 3218 jednostek (było 2904): doszły jednostki dodane albo zmienione aktem, którego termin stoi w innym odnośniku; dzień wejścia w życie ma 1867 jednostek przyszłych (było 1366), `obowiazuje-do` — 738 (było 514). Zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory); treść bez zmian.
- **Akt, który wprowadza zmianę** ([ADR-0063](docs/adr/0063-zdarzenie-zmiany-nalezy-do-aktu-ktory-ja-wprowadza.md)): zdarzenie zmiany w tekście jednolitym
  nazywa akt zmieniający — w identyfikatorze (`zdarz_uchylenie_2026-04-18_DU-2015-1634_odczytane`, jak w dokumentacji
  schematu) i w `zrodlo` — oraz przepisy sprawcze w `podstawa`. Akt czyta się z odnośnika, który nazywa go datą
  i publikatorem, także przez odesłanie „ustawy, o której mowa w odnośniku …". Model: `WordingVersion.ChangeAct`,
  `WordingVersion.ChangeProvision`.
  - W korpusie zmienia się 107 tekstów jednolitych — identyfikatory zdarzeń i grup oraz `okres` jednostek. Z 268 zdarzeń zmiany 238 nazywa akt sprawczy (`zrodlo`), a 30 zostaje przy dokumencie; `podstawa` ma 254 zdarzenia (160 — jeden przepis, 71 — od dwóch do pięciu, 23 — więcej, najwięcej 50). Zdarzeń jest o 13 więcej niż przedtem (255): zmiany dwóch aktów tego samego dnia to dwa zdarzenia. Zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory); treść bez zmian.
- **Znacznik odnośnika za kropką kończy zdanie**: „… od dnia otrzymania wniosku.[7)] Brak zgody …" to dwa zdania
  — znacznik przyklejony do kropki należy do zdania, które zamyka (w tekstach jednolitych tak stoi odnośnik o zmianie
  zdania), więc `SplitIntoSentences` szuka odstępu dopiero za nim. Znacznik odsunięty odstępem i weta lewego
  kontekstu (rok, skrót, publikator) rozstrzygają jak dotąd.
  - W korpusie w 69 tekstach jednolitych przybywa 145 zdań, które były zlane z poprzednim; zgodnych ze schematem
    jest nadal 1937 z 2321 (oba walidatory).
- **Zdanie w dwóch brzmieniach** ([ADR-0064](docs/adr/0064-zdanie-w-dwoch-brzmieniach-to-dwa-zdania-o-jednym-numerze.md)): zdanie wydrukowane w tekście jednolitym
  dwa razy („Zdanie drugie w tym brzmieniu obowiązuje do …" i „Zdanie drugie w brzmieniu ustalonym przez …") wychodzi
  jako dwa `zdanie` o tym samym `nr` i różnym `okres`, z projekcjami `obowiazuje-do` / `wejscie-w-zycie`; zdania za nimi
  nie dostają już numeru o jeden za wysokiego. Model: `TextSegment.Version`.
  - W korpusie zmienia się 6 tekstów jednolitych: 21 zdań niesie okres brzmienia obowiązującego, a 23 — przyszłego (wcześniej żadne). Zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory); treść bez zmian.
- **Drugie brzmienie w osobnym akapicie** ([ADR-0065](docs/adr/0065-drugie-brzmienie-w-osobnym-akapicie-nalezy-do-tego-samego-fragmentu.md)): wprowadzenie do wyliczenia,
  zdanie i część wspólna, których drugie brzmienie tekst jednolity drukuje osobnym akapitem, nie wychodzą już jako
  `fragment`. Zdanie i wprowadzenie to dwa `zdanie` o jednym numerze (jedno wyliczenie pod oboma brzmieniami),
  część wspólna — dwie części wspólne o tym samym eId i różnym `okres`.
  - **Naprawiono:** eksport brał tylko pierwszą część wspólną zamykającą wyliczenie; kolejne ginęły z wyjścia.
  - W korpusie zmienia się 17 dokumentów. W tekstach jednolitych 24 akapity wychodzące dotąd jako `fragment` stają się drugim brzmieniem: 16 — zdania jednostki (wprowadzenie do wyliczenia albo zdanie), 8 — części wspólnej. Eksport wszystkich części wspólnych zamykających wyliczenie przywraca też 8 akapitów cytowanych części wspólnych jednego rozporządzenia, które dotąd ginęły (pokrycie tekstu rozporządzeń: 2 zgubione akapity zamiast 10). Zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory).
- **Adnotacja nazywająca uchyloną jednostkę systematyzacyjną** ([ADR-0066](docs/adr/0066-adnotacja-nazywajaca-uchylona-jednostke-jest-jej-naglowkiem.md)):
  „(Rozdział 2 – uchylony)", „(oznaczenie oraz tytuł księgi pierwszej – uchylone)" i podobne wprowadzają nazwaną
  jednostkę do układu aktu i są jej nagłówkiem. Przedtem ginęły (przed pierwszym artykułem jednostki nadrzędnej)
  albo wychodziły jako `fragment` poprzedniego artykułu.
  - W korpusie zmienia się 7 tekstów jednolitych: 20 adnotacji staje się nagłówkami jednostek (16 rozdziałów, 3 oddziały, 1 księga) — 9 z nich ginęło z eksportu, 11 wychodziło jako `fragment` artykułu, za którym stały. Zgubionych akapitów w tekstach jednolitych zostaje 9 (było 18). Zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory).
- **Wiersz pod tytułem jednostki systematyzacyjnej** ([ADR-0067](docs/adr/0067-wiersz-pod-tytulem-jednostki-jest-dalsza-trescia-naglowka.md)): nagłówek pośredni
  bez oznaczenia („Przepis wstępny", „A. Plany naprawy"), druga część tytułu w osobnym akapicie i adnotacja o uchyleniu,
  która nie nazywa jednostki, zostają w nagłówku jednostki jako `specjalny`. Przedtem — stojąc przed pierwszym
  przepisem jednostki — ginęły z eksportu.
  - Miara pokrycia tekstu nie liczy już jako zgubionego artykułu zapisanego bez kropki („Art 64.").
  - W korpusie zmienia się 5 tekstów jednolitych: 8 wierszy stojących pod tytułem jednostki wychodzi w jej nagłówku jako `specjalny`; 6 z nich ginęło z eksportu. Zgubionych akapitów zostaje w całym korpusie 3 (1 w tekście jednolitym uchwały, 2 w jednym rozporządzeniu) — przedtem 11, z czego 2 były artefaktem miary. Zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory).
- **Skreślenie znaku nie jest uchyleniem jednostki** ([ADR-0068](docs/adr/0068-skreslenie-znaku-jest-zmiana-w-tekscie-nie-uchyleniem-jednostki.md)): „skreśla się przecinek
  i wyrazy „X"", „w pkt 8 skreśla się przecinek i uchyla się pkt 9–11", „w lit. c skreśla się średnik i dodaje się
  część wspólną w brzmieniu:" dają instrukcję `now:zamien-tekst`. Przedtem całe zdanie było uchyleniem jednostki,
  w której znak stoi, a treść dodawana za skreślonym znakiem ginęła z eksportu.
  - O operacji na jednostce rozstrzyga zdanie bez zapisanych w nim zmian w tekście
    (`AmendmentCommandParser.WithoutTextChanges`).
  - W korpusie zmienia się 13 zdań w 10 plikach: przybywa 13 instrukcji `now:zamien-tekst`, znika 10 fałszywych `now:uchyl` jednostki, w której skreślano znak, 2 uchylenia wskazują teraz jednostkę uchylaną („pkt 9–11", „pkt 2"), a 2 cytowane części wspólne wracają do eksportu (jedno rozporządzenie w dwóch plikach). Zgubionych akapitów zostaje w całym korpusie 1 (przedtem 3). Zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory).
- **Czasownik uchylenia w przepisie merytorycznym nie jest komendą** ([ADR-0069](docs/adr/0069-czasownik-uchylenia-jest-komenda-w-artykule-zmieniajacym-albo-przy-jednostce.md)):
  „kto uchyla się od wykonania obowiązku", „decyzję uchyla się, jeżeli…", „studenta skreśla się z listy studentów"
  nie dają już instrukcji `now:uchyl`. Komendą czasownik jest w artykule zmieniającym inny akt albo wtedy, gdy zaraz
  za nim stoi uchylana jednostka („uchyla się ust. 3").
  - **Naprawione fałszywe uchylenie jednostki powołanej w przepisie**: „Uchyla się, z zastrzeżeniem ust. 2, przepisy
    ustaw szczególnych…" wskazywało ust. 2 tego samego aktu.
  - W korpusie znika 125 fałszywych `now:uchyl` w 55 tekstach jednolitych (m.in. 69 × „uchyla się od …", 21 × „skreśla się z/ze …", 16 w zdaniach stojących obok takiego zdania w tej samej jednostce); 89 z nich miało cel `UNKNOWN`, 36 wskazywało jednostkę tylko powołaną w przepisie. Żadna komenda aktu zmieniającego nie znika: instrukcji `now:uchyl` zostaje 1033 w 897 zdaniach. Zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory).
- **Uchylenie zdania nie jest uchyleniem jednostki** ([ADR-0070](docs/adr/0070-uchylane-zdanie-jest-adresowane-zdaniem-nie-jednostka.md)): „w ust. 2 skreśla się
  zdanie drugie" daje `now:uchyl` z adresem zdania (`…ust_2__zdn_2`). Przedtem instrukcja wskazywała cały ustęp.
  - Szyk „uchyla się drugie zdanie" jest czytany jak „zdanie drugie" (`UnitReferenceTokenizer`).
  - W korpusie zmienia się 28 zdań w 21 plikach: `now:uchyl`, który wskazywał całą jednostkę („w ust. 2 skreśla się zdanie drugie" → `…ust_2`), wskazuje teraz zdanie (`…ust_2__zdn_2`). Poza tym eksport się nie zmienia; zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory).
- **Tekst w cudzysłowie pod przepisem, który niczego nie zmienia** ([ADR-0071](docs/adr/0071-tresc-pod-przepisem-ktory-niczego-nie-zmienia-jest-jego-trescia.md)):
  oświadczenie „następującej treści", tekst dołączany do protokołu i podobne przytoczenia wychodzą jako `tresc-strukt`
  w zdaniu przepisu — z wierszem otwierającym („Oświadczam, że:") i punktami, bez eId — a nie jako nowelizacja
  (`cytat-strukt funkcja="zastap"` z celem `UNKNOWN`). Wzory i objaśnienia złożone stylem zmiany pod takim przepisem
  są jego dalszą treścią, w kolejności z aktu.
  - Model: przytoczenie (`ContentBlock.Quotation`) wie, czy otwiera i zamyka cudzysłów (`Quotation.HasOpeningQuote`,
    `HasClosingQuote`).
  - W korpusie zmienia się 20 plików, wszystkie to teksty jednolite: znika 49 fałszywych cytatów nowelizacji (20 `cytat-strukt`, 29 `cytat-w-tekscie`), przybywa 30 `tresc-strukt` (13 z nich ma tylko cudzysłów otwierający — klauzula „Świadomy odpowiedzialności karnej…" bez wykrytego zamknięcia), 13 wierszy otwierających („Oświadczam, że:") przechodzi z `fragment` do swojego przytoczenia, a wzory i objaśnienia złożone stylem zmiany stają się dalszą treścią (+20 `fragment`, 5 `wzor-mat` z 21 opisami symboli). Zgodnych ze schematem jest nadal 1946 z 2321 (oba walidatory), pokrycie tekstu bez zmian.
- **Daty wersji tekstu jednolitego** ([ADR-0072](docs/adr/0072-wersja-tekstu-jednolitego-od-ujednolicenia-stan-prawny-z-obwieszczenia.md)): metadane tekstu jednolitego mają
  `DataObowiazywaniaWersji` z `Od` wskazującym zdarzenie ujednolicenia, a — gdy obwieszczenie podaje „stan prawny na dzień …"
  (wzór z § 104 ZTP) — także `DataStanuPrawnegoTJ` (`Announcement.LegalStateDate`), jak w przykładzie autorów schematu.
  - W korpusie wszystkie 728 tekstów jednolitych dostaje `DataObowiazywaniaWersji` ze zdarzeniem ujednolicenia; `DataStanuPrawnegoTJ` — żaden (obwieszczenia według wcześniejszego wzoru, 2 z dniem wykropkowanym). Poza tym eksport się nie zmienia; zgodnych ze schematem jest nadal 1946 z 2321 (oba walidatory).
- **Miejsce ogłoszenia aktu bez nagłówka dziennika** ([ADR-0073](docs/adr/0073-akt-bez-naglowka-dziennika-bierze-adres-z-wiersza-przylaczenia.md)): dziennik, rocznik
  i pozycja z wiersza „Załącznik do obwieszczenia … (Dz. U. poz. 1451)" albo z nagłówka załącznika aktu, gdy dokument
  nie ma nagłówka strony. Przedtem `Rok` i `Pozycja` zostawały puste, a jednostki bez prefiksu aktu.
  - W korpusie 332 dokumenty bez nagłówka dziennika (192 rozporządzenia, 91 tekstów jednolitych, 48 innych obwieszczeń, 1 ustawa) dostają miejsce ogłoszenia z wiersza przyłączenia — w każdym data w tym wierszu jest datą aktu, a na 779 dokumentach z nagłówkiem adres z wiersza zgadza się z nagłówkiem bez wyjątku. Zgodnych ze schematem jest 2278 z 2321 (było 1946; oba walidatory, żaden dokument nie wypadł). Zostaje 41 dokumentów bez nagłówka, których wiersz pozycji nie podaje („poz. 0", „poz. )", „poz. …") albo którego nie ma, i 2 z niezgodną treścią.
- **Część wspólna punktów za literami ostatniego punktu** ([ADR-0074](docs/adr/0074-przecinek-po-punktach-zapowiada-czesc-wspolna-punktow.md)): bez stylów myślnik
  za ostatnią literą ostatniego punktu zamyka całe wyliczenie punktów, gdy punkty przed ostatnim kończą się przecinkiem
  (§ 56 ust. 3 ZTP). Przedtem stawał się zawsze częścią wspólną liter.
  - Bez stylów: w pełni zgodnych tekstów jednolitych jest 625 z 728 (było 599), ustaw 210 z 242 (było 207); części wspólnych punktów gubionych bez stylów jest 281 (było 349), nadmiarowych części wspólnych liter — 39 (było 104). Ze stylami zmienia się 6 plików: 7 myślników bez stylu za ostatnią literą wychodzi jako część wspólna punktów, nie liter. Zgodnych ze schematem jest nadal 2278 z 2321.
- **Pozycja zakończona spójnikiem prowadzi wyliczenie dalej** ([ADR-0074](docs/adr/0074-przecinek-po-punktach-zapowiada-czesc-wspolna-punktow.md)): tiret
  „– … lub" w ostatnim punkcie nie poprzedza części wspólnej punktów — myślnik za nim to kolejne tiret
  (`CommonPartPlacement.ClosesBeforeDash`). W korpusie bez zmian (zabezpieczenie reguły z ADR-0074).
- **Przytoczony ustęp w obwieszczeniu** ([ADR-0075](docs/adr/0075-cudzyslow-za-oznaczeniem-artykulu-otwiera-przytoczenie-jego-ustepu.md)): „Art. 78. „1. …" — cudzysłów za
  oznaczeniem artykułu — zaczyna przytoczenie, a ustęp należy do swojego artykułu w akcie przytaczanym. Przedtem akapit
  zostawał tekstem, a punkty i kolejne ustępy przytoczenia stawały się podpunktami i punktami obwieszczenia.
  - W korpusie zmienia się 45 tekstów jednolitych: przybywa 62 przytoczeń zbudowanych z jednostek (`tresc-strukt`), znika 75 akapitów zostawionych tekstem (`fragment`), 73 fałszywe podpunkty i 16 fałszywych punktów obwieszczenia (punkty i ustępy przytoczonego przepisu brane za jednostki obwieszczenia). Powtórzonych eId w tekstach jednolitych jest 559 (było 594). Zgodnych ze schematem jest nadal 2278 z 2321 (oba walidatory), pokrycie tekstu bez zmian.
- **Oświadczenie w stylu cytatu** ([ADR-0076](docs/adr/0076-cudzyslow-otwarty-pod-przepisem-trwa-do-domkniecia.md)): „1) oświadczenie następującej treści:"
  i „„Oświadczam, że: 1) … 2) …”" złożone stylem cytatu jest przytoczeniem pod punktem (`tresc-strukt`) — do domknięcia
  cudzysłowu, bez względu na style pozycji. Przedtem pozycje oświadczenia stawały się punktami aktu.
  - W korpusie zmienia się 11 tekstów jednolitych: 25 oświadczeń wychodzi jako `tresc-strukt`, a ich wiersz otwierający przestaje być `fragment` (28 akapitów). Powtórzonych eId w tekstach jednolitych jest 513 (było 559) — pozycje oświadczeń przestały być punktami aktu; nierozróżnionych okresem zostaje 34. Zgodnych ze schematem jest nadal 2278 z 2321 (oba walidatory), pokrycie tekstu bez zmian. Bez stylów: 114 jednostek zgodnych więcej, żadna mniej; w pełni zgodnych tekstów jednolitych 628 z 728 (było 625).
- **Orientacja stron załącznika** ([ADR-0077](docs/adr/0077-segment-zalacznika-to-ciag-stron-jednej-orientacji.md)): segment załącznika niesie `orientacja`
  (`pionowa`, `pozioma`) z ustawień sekcji DOCX, a zmiana orientacji w załączniku zaczyna nowy segment. Źródło bez
  układu strony (PDF, TXT) daje segment bez atrybutu.
  - W korpusie wszystkie 722 segmenty załączników (432 dokumenty) niosą `orientacja`, 29 z nich `pozioma`; w 2 dokumentach załącznik dzieli się na trzy segmenty (pionowa — pozioma — pionowa: szeroka tabela w środku), przybywa 4 segmentów. Zgodnych ze schematem jest nadal 2278 z 2321 (oba walidatory). Wobec przykładu DU/2026/252 zgadzają się trzy segmenty; czwarty jest w przykładzie bez atrybutu, u nas `pionowa` — jak strona źródła.
- **Numer przypisu wpisany tekstem nie jest jego treścią** (`DocxFootnotes.WithoutTypedOwnNumber`): treść przypisu
  otwarta jego własnym numerem w indeksie górnym („[1)]\tNiniejsza ustawa …") zamiast znaku odwołania Worda traci ten
  numer, jak przy znaku odwołania. Inny numer w indeksie górnym na początku treści zostaje — to odwołanie.
  - W korpusie zmienia się 67 odnośników w 62 dokumentach (przy tytule, organie i w treści aktu). Zgodnych ze schematem
    nadal 2278 z 2321.
- **Wyliczenie w odnośniku** ([ADR-0078](docs/adr/0078-wyliczenie-w-odnosniku-to-jego-jednostki.md)): „Niniejsza ustawa służy stosowaniu: 1) …;
  2) …" wychodzi jako `punkt-odnosnik` z `nr`, litery i tirety pod nimi, części wspólne i dalsze akapity jako
  `fragment-odnosnik` — w odnośniku aktu i w cytowanym (`odnosnik-cyt`). Wprowadzenie i pozycje to fragmenty jednego
  zdania. Przedtem każdy akapit przypisu był kolejnym zdaniem jednego `p`.
  - W korpusie zmienia się 147 dokumentów: 738 `punkt-odnosnik` (122 dokumenty), 64 `litera-odnosnik` (11), 32 `czesc-wspolna-punkt-odnosnik` (6) i 106 `fragment-odnosnik` (70) — przedtem zdania jednego `p` z oznaczeniem i tabulatorem w tekście. Zgodnych ze schematem jest nadal 2278 z 2321 (oba walidatory).
- **Zdanie albo akapit jako przedmiot komendy** ([ADR-0079](docs/adr/0079-zdanie-albo-akapit-jako-przedmiot-komendy-czyni-tresc-cytowana-tekstem.md)): „zdanie pierwsze otrzymuje
  brzmienie:", „akapity czwarty i piąty otrzymują brzmienie:" cytują tekst — nie część wspólną ze stylu, którym szablon
  składa cytowane zdania, i nie punkty z oznaczenia na początku akapitu tekstu załącznika.
  - W korpusie zmienia się 8 dokumentów (4 akty, każdy dwa razy): 100 cytatów wychodzi tekstem (`cytat-w-tekscie`) zamiast 122 cytatów strukturalnych z częścią wspólną, tiretem, punktem albo literą, których zmieniany akt nie ma. Zgodnych ze schematem nadal 2278 z 2321 (oba walidatory). Zgodność bez stylów: rozporządzenia 97,38 % → 97,76 % jednostek (w pełni zgodnych 1006 → 1012), ustawy w pełni zgodnych 210 → 211 — różnice znikają, bo mylił się przebieg ze stylami.
- **Komenda w załączniku** ([ADR-0080](docs/adr/0080-komenda-w-zalaczniku-wskazuje-jednostki-zalacznika.md)): „w załączniku nr 1 w ust. 2 po pkt 57 dodaje się
  pkt 57a" wskazuje `zal_1__ust_2__pkt_57`, a nie punkt aktu; numeracja treści załącznika („pkt 1.4.1", „lp. 1.54") nie
  daje oznaczenia „1"; uchylenie wiersza tabeli załącznika nie wskazuje całego załącznika.
  - W korpusie zmienia się 1010 adresów celu w 121 dokumentach: 85 wskazuje jednostki załącznika (`zal_2__ust_2__pkt_57`), 208 — załącznik albo jego najgłębszą znaną jednostkę jako miejsce zmiany w tekście (127 przedtem z adresem jednostki aktu, 81 bez adresu), a 717 adresów jednostek aktu, których komenda nie dotyczyła, staje się `UNKNOWN` (załącznik bez numeru, wiersze tabel, części i numeracja treści załącznika). Liczba instrukcji bez zmian; zgodnych ze schematem nadal 2278 z 2321 (oba walidatory).
- **Jednostka z samym oznaczeniem ma akapit** (`SentenceContext.UnitParagraph`): punkt, litera, tiret i ustęp, których
  akapit źródła niesie samo oznaczenie („1)", a za nim wzór w osobnym akapicie), wychodzą z akapitem o pustym zdaniu —
  schemat wymaga `p` przed dalszą treścią jednostki. Przedtem `wzor-mat` stał zaraz za `nr`.
  - W korpusie zmienia się 1 dokument (2 punkty); zgodnych ze schematem 2279 z 2321 (oba walidatory).
- **Nowelizacje zagnieżdżone** ([ADR-0082](docs/adr/0082-cytat-zagniezdzony-jest-nowelizacja-cytowanego-przepisu.md)): cytowany przepis, który sam jest komendą
  („4) po art. 28a dodaje się art. 28b w brzmieniu:"), ma własną nowelizację z cytatem, który po nim stoi — w eksporcie
  `cytat-strukt` w zdaniu cytowanego przepisu, z celem i aktem zmienianym czytanymi z cytatu. Przedtem jednostki cytatu
  wewnętrznego stawały przed przepisem jako jednostki cytatu zewnętrznego, z adresem aktu zmieniającego.
  - W korpusie zmienia się 11 dokumentów — wszystkie 9 ze stylami `ZZ/` i 2 bez nich: 23 cytaty zagnieżdżone wychodzą w zdaniu cytowanego przepisu, 15 punktów i 10 liter przestaje być `nie-ztp-*`, cytowany rozdział dostaje nagłówek; żadna komenda w cytacie nie zostaje bez swojej treści. Zgodnych ze schematem nadal 2279 z 2321 (oba walidatory), pokrycie tekstu bez zmian.
- **Komendy bez cytatu w cytowanym przepisie** ([ADR-0082](docs/adr/0082-cytat-zagniezdzony-jest-nowelizacja-cytowanego-przepisu.md)): cytowany przepis ustawy
  zmieniającej, który zmienia wyrazy („w art. 12 wyrazy „…” zastępuje się wyrazami „…”") albo uchyla nazwaną jednostkę
  („uchyla się art. 11a"), ma własną nowelizację — w eksporcie instrukcje w jego zdaniu. Przepis cytowany bez miejsca zmiany
  na początku zostaje tekstem.
  - W korpusie zmienia się 7 dokumentów: w cytowanych przepisach staje 40 instrukcji `now:zamien-tekst` i 7 `now:uchyl`. Zgodnych ze schematem nadal 2279 z 2321 (oba walidatory).
- **Objaśnienia gwiazdek** ([ADR-0083](docs/adr/0083-objasnienie-gwiazdki-nie-jest-pozycja-wyliczenia-a-jej-znacznik-jest-indeksem-gornym.md)): akapit „*) Zgodnie z …" za wyliczeniem załącznika
  zostaje akapitem za jednostkami — także bez stylów, gdzie przedtem psuł budowę całego wyliczenia — a znaczniki objaśnień
  („(*)", „*)", „I)") wychodzą jako `sup`, nie w zapisie kanału tekstowego.
  - W korpusie zmienia się 64 dokumenty: 509 znaczników objaśnień wychodzi jako `sup` zamiast zapisu kanału tekstowego („[(*)]”, „[*)]”, „[I)]”) i w jednym akcie dwa objaśnienia przestają być częścią wspólną punktów. Zgodność bez stylów: rozporządzenia 97,74 % → 98,11 % jednostek (brakujących 655 → 548). Zgodnych ze schematem nadal 2279 z 2321 (oba walidatory).
- **Przytoczone przepisy zmieniające** ([ADR-0084](docs/adr/0084-przytoczony-przepis-zmieniajacy-jest-jednostka-z-wlasnymi-nowelizacjami.md)): przytoczenie w obwieszczeniu tekstu
  jednolitego, które cytuje przepis zmieniający z własnymi cytatami („art. 94–130 ustawy …, które stanowią: „Art. 94.
  W ustawie … 1) w art. 10f ust. 5 otrzymuje brzmienie: „5. …""), wychodzi jednostkami w `tresc-strukt`, a cytaty —
  jako nowelizacje przytoczonych przepisów. Komenda bez dwukropka, ze średnikiem albo z „brzmieniu" bez „w" też otwiera
  cytat zagnieżdżony. W treści cytowanej wiersze cudzysłowu otwartego w środku akapitu („zastępuje się wyrazami „…:",
  „1) …;", „2) … r”,") są dalszą treścią jego jednostki, nie jednostkami.
  - W korpusie zmienia się 23 dokumenty. W 19 obwieszczeniach tekstu jednolitego przytoczenia przepisów zmieniających są jednostkami: 3102 akapity przestają być `fragment` (pod jednostkami obwieszczeń zostaje 734 w 72 dokumentach, przedtem 3836 w 90), a w przytoczonych przepisach staje 596 `cytat-strukt`, 33 `cytat-w-tekscie`, 152 `now:zamien-tekst` i 41 `now:uchyl`. W 4 aktach 30 wierszy cytowanych oświadczeń i wzorów przestaje być powtórzonymi jednostkami cytatu (drugi „pkt 1") i zostaje tekstem jednostki, która je przytacza. Zgodnych ze schematem nadal 2279 z 2321 (oba walidatory, ten sam zbiór); pokrycie tekstu i zgodność bez stylów bez zmian (tyle samo brakujących i nadmiarowych jednostek).
- **Tabela w przytoczeniu**: tabela w środku przytoczenia w obwieszczeniu tekstu jednolitego jest dalszą treścią jednostki,
  za którą stoi, a tabela otwierająca cudzysłów tuż za komendą przytoczonego przepisu („w załączniku nr 1 po lp. 2 dodaje
  się lp. 2a w brzmieniu:") — treścią jego nowelizacji ([ADR-0050](docs/adr/0050-cudzyslowy-cytowanej-tabeli-licza-sie-w-bilansie-cytatu.md),
  [ADR-0084](docs/adr/0084-przytoczony-przepis-zmieniajacy-jest-jednostka-z-wlasnymi-nowelizacjami.md)). Przedtem tabela dzieliła przytoczenie i całe zostawało tekstem.
  Cudzysłowy w komórkach tabeli liczą się do bilansu przytoczenia: przytoczenie domknięte w tabeli nie wchłania już
  dalszych podpunktów i podpisu obwieszczenia.
  - W korpusie zmienia się 4 dokumenty: 413 akapitów przestaje być `fragment` (pod jednostkami obwieszczeń zostaje 321
    w 68 dokumentach), staje 78 `cytat-strukt` i 51 `now:zamien-tekst`; w dwóch obwieszczeniach podpunkt i podpis wracają
    na swoje miejsca, a znaczniki odnośników przytoczonej tabeli są indeksem górnym zamiast odsyłaczy do odnośnika
    obwieszczenia. Zgodnych ze schematem nadal 2279 z 2321 (oba walidatory, ten sam zbiór); pokrycie tekstu i zgodność
    bez stylów bez zmian.
- **Otwarty cudzysłów przed stylem** ([ADR-0085](docs/adr/0085-otwarty-cudzyslow-cytatu-przewaza-nad-stylem-ustawy-matki.md)): akapit w stylu jednostki aktu, który stoi
  w cytacie o niedomkniętym cudzysłowie („„Wariant D", „1. Wykształcenie …" w stylu punktu), jest treścią cytowaną, a nie
  ustępem aktu zmieniającego. Cytat kończy akapit z komendą albo kolejna jednostka aktu zmieniającego, więc literówka
  w cudzysłowie nie wchłania następnych przepisów. Tekst za tiretami na początku cytatu jest dalszą treścią ostatniego
  tiretu i zostaje na swoim miejscu.
  - W korpusie zmienia się 4 dokumenty (dwie pary tego samego aktu). W rozporządzeniu zmieniającym 338265 (i jego kopii) 62 jednostki `nie-ztp-*` stają się jednostkami ZTP: cytat „Wariant D” w stylu punktu nie rozbija już § 1 na ustępy, a 9 zmian w tekście załącznika wskazuje jednostki załącznika (`zal_1__pkt_4`) zamiast punktów rozporządzenia. W 330183 (i kopii) cytowane tirety objaśnień przestają być tiretami aktu zmieniającego. Zgodnych ze schematem nadal 2279 z 2321 (oba walidatory, ten sam zbiór); pokrycie tekstu bez zmian. Zgodność bez stylów rozporządzeń 98,11 % → 98,64 % jednostek (brakujących 548 → 396, nadmiarowych 253 → 101): budowa ze stylami zgadza się teraz z budową z samej treści.
- **Rota i oświadczenie w jednym akapicie** ([ADR-0071](docs/adr/0071-tresc-pod-przepisem-ktory-niczego-nie-zmienia-jest-jego-trescia.md),
  [ADR-0076](docs/adr/0076-cudzyslow-otwarty-pod-przepisem-trwa-do-domkniecia.md)): akapit, który w całości jest cytatem („„Ślubuję uroczyście … .”"), pod
  przepisem kończącym się dwukropkiem i niczego niezmieniającym („… składa ślubowanie według następującej roty:") jest
  przytoczeniem — `tresc-strukt` w zdaniu przepisu, jak przytoczenie na kilka akapitów. Wyrażenie w cudzysłowie na
  początku akapitu („„Komornik” oznacza …") cytatem nie jest.
  - W korpusie zmienia się 35 dokumentów: 44 roty i oświadczenia przestają być `fragment` i wychodzą jako `tresc-strukt`.
    Zgodnych ze schematem nadal 2279 z 2321 (oba walidatory, ten sam zbiór); pokrycie tekstu i zgodność bez stylów bez zmian.
- **Żądanie bez formularza nie daje 500**: `POST /v1/convert` bez ciała (albo bez typu treści) kończyło się błędem
  wiązania parametrów, który `UseExceptionHandler` zamieniał na 500. Teraz odpowiedź niesie kod z wyjątku (400) i polski
  opis: dokument trzeba wysłać formularzem `multipart/form-data` w polu `documentFile`.

### Usunięto
- **Nieużywane helpery `SpreadsheetHelper` i `EnumExtensions`** (`ToDescription()`, `[EnumDescription]`) —
  żaden kod ich nie wołał. Dokumentacja nie zapowiada już eksportu XLSX, którego nie ma; jedynym formatem
  wyjściowym jest XML.
- **Nieaktualne dokumenty z `docs/`**: `parsing-flow.md`, `classification-algorithm.md`,
  `classification-algorithm-layered.md`, `onboarding-xml-converters.md`, `notatka_stan_AKN4PL.md`,
  `analiza_AK4PL.md`, `analiza_AK4PL_uzupelnienie.md`. Opisywały klasyfikator warstwowy z warstwą AI, którego
  już nie ma, układ trzech osobnych repozytoriów i stan modelu z marca; dwie analizy były zapisem rozmowy
  z asystentem. To, co nadal prawdziwe, a nieopisane — przeciążenie `Parse(IReadOnlyList<DocumentBlock>)`
  i tiret zapisany symbolem `w:sym` F02D — przeszło do `architecture.md`. Treść zostaje w historii gita.
- **Diagramy PlantUML modelu** (`src/Saga.Model/uml/*.puml`): nieaktualne — brakowało w nich m.in. załączników,
  obwieszczenia, bloków treści, podpisu i odnośników — i nic się do nich nie odwoływało. Hierarchię modelu
  opisuje `architecture.md` (sekcja 4).

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
