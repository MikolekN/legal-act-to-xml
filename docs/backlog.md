# Backlog — znane luki i świadomie odroczone prace

Rzeczy, o których wiemy, że ich nie ma. Każdy punkt był rozważony i odłożony z podanego powodu —
to nie lista życzeń ani „to do", tylko rejestr świadomych dziur, żeby nikt nie odkrywał ich
od nowa i nie brał za błąd czegoś, co jest decyzją.

Stan zweryfikowany w kodzie 2026-07-25. Uzasadnienia decyzji projektowych: [docs/adr/](adr/README.md).
Reguły ZTP stojące za tymi punktami: [ztp-struktura-aktow.md](ztp-struktura-aktow.md).

## 1. Gałąź bezstylowa (DOCX bez szablonu / PDF / TXT)

Styl Word pozostaje najsilniejszym sygnałem; poniższe zachowania są dostępne **wyłącznie** przy
stylach szablonu i nie mają odpowiednika w rozpoznawaniu z samej treści.

Miarą jest `CorpusValidator --styleless-parity`: ta sama struktura jednostek z bloków pozbawionych stylu.
Stan 2026-10-10: ustawy ogłoszone 99,67 % jednostek zgodnych (111 ginie, 228 nadmiarowych; 199 z 242
dokumentów w pełni zgodnych), teksty jednolite 99,56 % (327 z 460). Bez układu i tabel („sam tekst") wynik
jest prawie ten sam — rozstrzyga treść. Granice cytatu wyznacza treść (cudzysłów w każdym wariancie
z `QuotationMarks`, przecinek po cudzysłowie, cudzysłów po komendzie bez dwukropka — `QuoteBalanceTracker`,
`AmendmentStateManager.UpdateState`), a zagnieżdżenie tiretu — jego znacznik (`TiretMarker`).
Zostaje to, czego rodzaj wynika z przedmiotu komendy, a nie z treści cytatu: część wspólna jako jedyna treść
cytowana („dodaje się część wspólną w brzmieniu:" — bez stylu tiret) i wyliczenie w cytowanym odnośniku
(„odnośnik nr 1 otrzymuje brzmienie:" — bez stylu punkty); do tego legenda wzoru zapisana myślnikami
i część wspólna tiretów. Lista w `styleless-parity-units.tsv`.

| Luka | Stan faktyczny | Dlaczego odłożone |
|---|---|---|
| **Część wspólna tiretów bez stylu** | Ze stylem `CZ_WSP_*` część wspólna jest rozpoznawana zawsze (gałąź `styleType == "WRAPUP"` w [`ParagraphClassifier.Classify`](../src/Saga.Core/Services/Classify/ParagraphClassifier.cs)). Bez stylu — z kontekstu, ale tylko po punkcie albo literze: „– tekst" tuż po jednostce bez żadnego znaku końcowego to część wspólna (ostatni element przed nią nie ma interpunkcji — § 57 ust. 3 ZTP; wyliczenie tiretów otwiera zaś dwukropek — § 56), więc `StructureProcessor.TryHandleStylelessWrapUp` (treść aktu) i `AmendmentBuilder.TryBuildStylelessCommonPart` (treść cytowana) dołączają go jako część wspólną punktów/liter. Regułę trzyma `CommonPartPlacement.LeavesEnumerationOpen`; kropka skrótu roku („… 2026 r.") nie jest znakiem końcowym (w korpusie ze stylami: 47 części wspólnych po takim zakończeniu, żadnego tiretu). Po elemencie zakończonym `,`/`;`/`.` myślnik zostaje tiretem — w korpusie to zwykle legenda wzoru albo pozycja tabeli, ale gubimy tak nieliczne sankcje po niechlujnym „;". Po tirecie „– tekst" nadal staje się kolejnym tiretem, także gdy to część wspólna tiretów. | Po tirecie kontekst nie rozstrzyga: kolejny tiret i część wspólna tiretów zaczynają się tym samym myślnikiem. Pewny sygnał to układ (wcięcie na wysokości wprowadzenia do wyliczenia, § 58 ZTP), a `ClassificationInput` to dziś rekord `(Text, StyleId)` i layout do klasyfikatora nie dochodzi (trafia osobnym parametrem tylko do `StructureProcessor.GetTiretDepth`). Kierunek do zbadania: ta sama reguła „bez znaku końcowego" dla tiretu (§ 57 ust. 6). |
| **Głębokość tiretu bez sygnału** | Trzy poziomy priorytetu: znacznik w treści („– –", „– – –" — `TiretMarker`) → styl (`2TIR`/`3TIR`/`TIR`) → wcięcie z `ParsingContext.OpenTiretIndents` (tolerancja ±120 twips, cap 3). Brak wszystkich → poziom 1. Podwójne tiret bez tiretu nadrzędnego zostaje na poziomie 1, a następne staje się jego dzieckiem. | Znacznik rozstrzyga niemal zawsze (w korpusie każdy akapit w stylu `2TIR`/`3TIR` go ma). Zostaje tiret zagnieżdżony zapisany pojedynczym myślnikiem bez stylu i bez wcięcia — nie ma wtedy żadnego sygnału. |
| **Nowelizacje zagnieżdżone (`ZZ/`)** | Trigger wykryty wewnątrz otwartego cytatu jest tylko flagowany (`QuoteBalanceTracker.MarkNestedTrigger`) — zagnieżdżony obiekt `Amendment` nie jest budowany. | Wymaga rekurencyjnego zbierania w cytacie; bez stylów nie ma sygnału zamknięcia wewnętrznego poziomu. |
| **`CommonPartOf` w nowelizacji** | Ze stylu: właściwość `AmendmentStyleInfo.CommonPartOf` w [`AmendmentStyleDecoder`](../src/Saga.Core/Helpers/AmendmentStyleDecoder.cs), ~240 wpisów `StyleLibraryMapper`. Bez stylu — z kontekstu, tą samą regułą co w treści aktu (pozycja bez znaku końcowego), dla punktów i liter. Część wspólna jako jedyna treść cytowana („dodaje się część wspólną w brzmieniu:") zostaje bez stylu tiretem — sygnał jest w komendzie, nie w treści. | Informacja „część wspólna czego" jest w szablonie zakodowana w nazwie stylu i nie ma odpowiednika w treści. |

Część domkniętą (część wspólna punktów i liter, jednostki systematyzacyjne bez stylu) pilnuje
`ParserEquivalenceTests.ActWithWrapUpAndSystematizingUnits_DocxAndTxt_AreEquivalent`
([plik](../tests/Saga.Core.Tests/ParserEquivalenceTests.cs)) — ten sam akt jako bloki DOCX ze stylami
i jako TXT, łącznie z kontrolą, że prawdziwe tirety po dwukropku zostają tiretami. Na prawdziwym akcie
pilnuje jej `ReferenceActTests.ReferenceAct_WithoutStyles_HasTheSameUnitStructure`
([plik](../tests/Saga.Core.Tests/ReferenceActTests.cs)): akt referencyjny bez stylów ma tę samą strukturę
jednostek co ze stylami. Testy
charakteryzujące w `ParagraphClassifierStylelessTests` utrwalają obecne, częściowo błędne wyniki
klasyfikatora — zmiana któregokolwiek wymaga jawnej aktualizacji z uzasadnieniem ZTP.

## 1a. Podział na zdania — ślady defektów wyżej w potoku

Splitter (`ParsingFactories.SplitIntoSentences`) nie ma znanych luk własnych — reguły i ich
kontrprzykłady są w `SentenceSplittingKnownGapsTests`. Pomiar na korpusie
(`dotnet run --project tools/CorpusValidator -- --sentence-split <korpus>`) nadal zgłasza jednak
błędne granice kategorii „samo oznaczenie jednostki" (311 w lokalnym korpusie, 2026-09-24). Wszystkie
leżą **poza** splitterem:

| Luka | Stan faktyczny | Dlaczego odłożone |
|---|---|---|
| **Numeracja hierarchiczna punktów** | Pozycje numerowane „5.6.", „18.8.1.", „I.1." (tabele kar, wykazy opłat, załączniki) są budowane jako punkty bez rozpoznanego numeru — eId kończy się samym `…__pkt`, a numer zostaje w treści i splitter tnie za nim. | Model numeru (`EntityNumber`) nie zna numeru wieloczłonowego, a ZTP nie przewiduje takiej jednostki redakcyjnej — trzeba najpierw zdecydować, czym te pozycje są w modelu. |
| **Weto na samotne oznaczenie tylko w cudzysłowie** | Oznaczenie stojące samodzielnie na początku zdania („„Art. 59a. 1.") nie kończy zdania wyłącznie w treści cytowanej. Poza cudzysłowem granica zostaje (`SplitIntoSentences_OznaczenieBezCudzyslowu_PozostajeGranica`). | Świadomie: samotny numer bez cudzysłowu to w korpusie zawsze niezdjęty prefiks albo nierozpoznany numer — weto ukryłoby taki defekt w pomiarze, zostawiając zepsuty eId i treść. |

## 2. Klasyfikacja dokumentu

- **Sygnał ujemny „przepisy karne"** — rozporządzenie nie może zawierać przepisów karnych ani
  o administracyjnych karach pieniężnych (§ 117 ZTP), więc sygnatury „podlega karze …",
  „administracyjnej karze pieniężnej" powinny odejmować punkty typowi `Regulation`. Brak wzorca
  w `ZtpPatterns` i brak wartości w `DocumentSignalKind` — sygnał nie istnieje. Materiał źródłowy
  gotowy: [ztp-struktura-aktow.md](ztp-struktura-aktow.md) sekcja 5.7.

## 3. Model danych — luki wobec docelowego eksportu XML

Stan sprawdzony w kodzie 2026-07-25. Istotne, bo profil `akn4pl` (i każdy inny docelowy schemat XML)
wymaga danych, których model dziś nie przechowuje — parser ich nie zbiera, więc eksport nie ma z czego
ich wziąć.

| Luka | Stan |
|---|---|
| **Metadane aktu** | `LegalDocument` ma `Title`, `ActDate`, `AnnouncementDate`, `SourceJournal` (dziennik/rok/pozycja — z nagłówka publikatora, [ADR-0012](adr/0012-naglowek-strony-jako-rola-bloku.md)), `Classification`. Brak: **ELI URI**, **organu wydającego**, **sygnatury**. |
| **`ContentText` jako płaski string** | Tabele, grafiki i wzory matematyczne są spłaszczane do tekstu (TODO w `BaseEntity`). Dotyczy też `cytat-strukt` inline poza kontekstem nowelizacji — w samej nowelizacji strukturę trzyma `Amendment`/`AmendmentContent`. |
| **Brak encji preambuły** | Ani `Preamble`, ani odpowiednika `preambula` — element obecny i w EAP, i w AKN. |
| **Odnośniki — reszta** | Model i eksport odnośników działają dla DOCX ([ADR-0026](adr/0026-odnosnik-tresc-w-rejestrze-miejsce-w-tekscie.md)). Zostają: odnośnik przy nagłówku jednostki systematyzacyjnej (znacznik zostaje w tekście nagłówka — schemat przyjmuje go w `nr`, nie w `p`), odnośnik o budowie wyliczenia (`punkt-odnosnik`, `fragment-odnosnik`; dziś wszystkie akapity przypisu to zdania jednego `p` — 194 przypisy w korpusie), odnośnik cytowany w nowelizacji (`odnosnik-cyt`, cel `odn_1`), przypisy z PDF (bloki `BlockRole.FootnoteText` nie trafiają do rejestru) i przypisy w załącznikach. Sygnał klasyfikacyjny `FootnoteDensity` nadal liczy znaczniki w tekście. |
| **Brak metadanych FRBR** | `FRBRthis`/`FRBRuri`/`FRBRdate`/`FRBRauthor` — wymagane przez `akn4pl.xsd`, w modelu nieobecne. |
| **`LegalActType` bez orzeczeń** | 11 wartości, brak `JudicialDecision` (wyrok TK) — a w lustrze upstreamu leży przykład `przyklady/wyrok_TK.xml`. |
| **Brakujące prefiksy eId** | Słownik skrótów: [ADR-0014](adr/0014-skroty-jednostek-w-eid.md). Model ma `art`/`ust`/`par`/`pkt`/`lit`, tirety `tir`–`pitir` i jednostki systematyzacyjne. Brak: `zdn` (zdanie — tylko w adresach wskazywanych, zdania w eksporcie nie mają eId), `ak` (akapit w prawie UE — spoza tabeli, wymagałby nowego ADR). Tabela zyskała `ppkt` (podpunkt obwieszczenia, ADR-0032); `zal` ma encję (`Annex`). |
| **Cel dodania bez kotwicy to jednostka dodawana** | „po ust. 4 dodaje się ust. 4a" wskazuje kotwicę (`element-eId` = `…__ust_4`, `funkcja="dodaj-po"`), ale „w art. 86 dodaje się ust. 4a i 4b" — bez „po" — daje za cele same dodawane jednostki (`…__ust_4a`, `…__ust_4b`, każda osobną komendą, [ADR-0016](adr/0016-wyliczenie-celow-to-osobne-komendy.md)), wciąż z `funkcja="dodaj-po"`. Dodanie na końcu jednostki nie ma dziś własnej reprezentacji: celem powinna być jednostka nadrzędna (`art_86`) albo ostatnia istniejąca jednostka, której tekst komendy nie wymienia. Na korpusie ok. 890 dodań bez „po …", w tym 71 z kilkoma dodawanymi jednostkami. |
| **Segment jednostki głównej „§"** | W akcie opartym na paragrafach samodzielny „§" to jednostka główna i ma w adresie segment `art` — jednostka (`Article` z `IsSection`, [ADR-0034](adr/0034-paragraf-jest-jednostka-podstawowa-aktu-wykonawczego.md)) tak samo jak jej powołanie ([ADR-0015](adr/0015-jedno-zrodlo-powolan-jednostek.md)). [ADR-0014](adr/0014-skroty-jednostek-w-eid.md) mówi „paragraf → `par`" o paragrafie kodeksu (pod artykułem). Autorzy schematu nie dali przykładu rozporządzenia — do potwierdzenia u nich, czy `<paragraf eId="…__art_5">` to oczekiwany adres; skrót nadaje jedno miejsce (`Article.EIdPrefix`). |
| **Atrybuty typograficzne pominięte** | Założenie „to warstwa prezentacji" **warte podważenia**: w tekstach jednolitych kursywa oznacza akty i przepisy, które utraciły moc oraz zlikwidowane organy (§ 108a–108b ZTP), a pogrubienie — brzmienia przyszłe (§ 106a ust. 4). To znaczenie normatywne, nie ozdoba. Adapter DOCX czyta już `IsBold`/`IsItalic` do `BlockLayoutInfo`, ale model wyjściowy tego nie przechowuje. |

## 3a. Polskie liczebniki — świadomie odłożone

Stan po uwspólnieniu w `Helpers/PolishNumerals.cs` ([ADR-0009](adr/0009-jedno-zrodlo-liczebnikow-polskich.md)).

| Luka | Stan |
|---|---|
| **Sufit 1–99** | Setki i wyżej („po upływie stu dni") nie dostaną operandu. |

## 3b. Wejście w życie — świadomie odłożone

Stan po rozsyłaniu daty ogólnej po drzewie ([ADR-0011](adr/0011-data-ogolna-rozsylana-po-calym-drzewie.md))
i po przysłanianiu jej mocą wsteczną na wyjściu
([ADR-0013](adr/0013-moc-wsteczna-przyslania-date-dopiero-na-wyjsciu.md)).

| Luka | Stan |
|---|---|
| **`HeadingEffectiveDate` nieużywane** | Pole jest w `ISystematizingUnit` i we wszystkich sześciu jednostkach systematyzacyjnych, ale nikt go nie zapisuje ani nie czyta. Jednostki są już rozpoznawane, ale w korpusie nie ma przypadku tytułu jednostki z własną datą wejścia w życie — nie ma czego zapisać. |
| **Moc wsteczna tylko jako data jawna** | Wzorzec wymaga daty kalendarzowej, więc „z mocą od dnia ogłoszenia" nie jest rozpoznawane. Wymagałoby potraktowania mocy wstecznej jak formuły — z własnym rozstrzyganiem względem daty ogłoszenia. |
| **Warianty leksykalne poza § 51** | „ze skutkiem od dnia …", „stosuje się od dnia …" — spoza katalogu ZTP, nierozpoznawane. Dopisanie do alternatywy jest tanie, ale każdy wariant trzeba wpierw zobaczyć w korpusie. |
| **Człon „z tym że" mieszany** | „z tym że art. 5 wchodzi w życie z dniem X, a art. 6 z mocą od dnia Y" idzie ścieżką wyjątku daty i moc wsteczna Y doczepia się do art. 5. Kształt spoza katalogu § 51. |
| **„z tym że" w pozycji listy wyjątków** | Forma z dwukropkiem rozbiera pozycje przez `BuildPartial`; człon „z tym że" wewnątrz pozycji nie jest osobno wydzielany. |
| **Rozdzielenie dat wejścia w życie i mocy wstecznej na jednostce** | Docelowo poprawniejsze niż przysłanianie: jednostka niosłaby w XML obie daty osobno, tak jak niesie je model. Nie robimy tego, bo **odbiorca wymaga w wyniku jednej daty** — patrz [ADR-0013](adr/0013-moc-wsteczna-przyslania-date-dopiero-na-wyjsciu.md). Po naszej stronie przełącznik jest jeden: `GoverningDate` (jedyny wywołujący to `UnitLifecycleXml.EntryDate`, przez które idą wszystkie zapisy — jednostki, zdania, części wspólne). Model i propagacja niosą obie daty osobno i **nie wymagają żadnej zmiany**. Do zrobienia przy przełączaniu: stała nowego atrybutu, drugi parametr w `SentenceContext.ToXml`, odwrócenie asercji w `EntryIntoForceXmlOutputTests`. |
| **Moc wsteczna bywa późniejsza niż data wejścia w życie jednostki — i nie jest to usterka** | Zmierzone na korpusie (`354687_3139110`): akt ogłoszony 2025-12-23 wchodzi w życie po upływie 6 miesięcy (2026-06-24), ale art. 2, art. 4 i art. 5 „wchodzą w życie z dniem następującym po dniu ogłoszenia, z mocą od dnia 1 stycznia 2026 r." — moc wsteczna wypada PO ich własnej dacie wejścia w życie (2025-12-24), a przed datą ogólną aktu. Przysłanianie daje tu wynik poprawny: przepisy działają od 2026-01-01. Wniosek dla ewentualnej walidacji: warunek „moc wsteczna późniejsza niż data jednostki" zapaliłby fałszywy alarm na tym poprawnie zredagowanym akcie, więc punktem odniesienia musi być **data ogólna aktu**. W 241 dokumentach nie ma ani jednego przypadku mocy wstecznej późniejszej niż data ogólna, więc dziś nie ma czego zgłaszać i żaden `ValidationMessage` nie powstał. |
| **Zakres wyjątku bez adresu w akcie zmienianym** | `JednostkaWZakresie` niesie adres jednostki cytowanej albo celu komendy (`DU/2025/501__art_17__ust_1d`). Bez adresu zostają 2 z 76 zakresów korpusu, w dwóch ustawach, których treść cytowana nie złożyła się w jednostki. Teksty jednolite zakresów już nie mają: nie czytają klauzuli wejścia w życie (ADR-0033), a przepisy przytoczone w obwieszczeniu nie należą do aktu (ADR-0031). Zakres ze zdaniem („w zakresie ust. 2 zdanie drugie") ma adres, ale nie prefiks aktu. |
| **Pusty fragment nierozłożony** | Klauzula złożona z samej mocy wstecznej („Ustawa wchodzi w życie z mocą od dnia …", bez formuły) dopisuje do `UnresolvedFragments` pusty string zamiast czytelnego powodu. |

## 3c. Jednostki systematyzacyjne — świadomie odłożone

Stan po pełnym rozpoznaniu i eksporcie jednostek systematyzacyjnych
([ADR-0017](adr/0017-eid-jednostki-redakcyjnej-bez-sciezki-systematyzacyjnej.md)–[ADR-0021](adr/0021-eid-jednostki-cytowanej-od-adresu-celu.md)).
Miarą jest `CorpusValidator --systematizing`; zgodność z przykładami autorów schematu —
`CorpusValidator --compare-reference`.

| Luka | Stan |
|---|---|
| **Akty oparte na paragrafach — reszta** | Rozporządzenie, uchwała i zarządzenie budują jednostki „§" i wychodzą pod własnym korzeniem z `autor-aktu` i `podstawa-prawna` ([ADR-0034](adr/0034-paragraf-jest-jednostka-podstawowa-aktu-wykonawczego.md)); tekst jednolity takiego aktu — pod `obwieszczenie`. W korpusie: 1067 rozporządzeń z 8169 paragrafami, zgodnych ze schematem w całości 823. Treść cytowana w akcie zmieniającym („§ 5 otrzymuje brzmienie: „§ 5. …"") jest jednostką podstawową z ustępami (`AmendmentBuildInput.SectionIsBasicUnit`; 364 cytowane paragrafy w 180 rozporządzeniach). Zostaje: (1) o znaczeniu cytowanego „§" rozstrzyga rodzaj aktu **zmieniającego** — ustawa zmieniająca rozporządzenie (w korpusie brak) przeczyta go jak paragraf kodeksu; akt zmieniany nie jest rozpoznawany po zwrocie „W rozporządzeniu … (Dz. U. …)" — prefiks cytatu bierze wtedy publikator aktu zmieniającego; (2) 228 rozporządzeń niezgodnych tylko w metadanych (brak nagłówka dziennika w pliku: `Rok`, `Pozycja`, `DataAktu`); (3) 14 tekstów jednolitych uchwał i zarządzeń z nagłówkiem rodzaju w nietypowej pisowni („ZARządzenie NR 249") zostaje przy korzeniu `ustawa`; (4) nagłówek bez organu albo bez tytułu w 13 rozporządzeniach; (5) organ wydający w metadanych nadal `UNKNOWN`, choć wiersz organu jest już znany (`LegalDocument.IssuerLine`) — brakuje słownika organów. |
| **Powołania jednostek systematyzacyjnych — reszta** | Cele nowelizacji i wyjątki wejścia w życie czytają już jednostki systematyzacyjne („po rozdziale 9", „…, z wyjątkiem rozdziału 4, który…"). Zostają: przedział rzymski („działy II–IV"), powołanie niejednoznaczne (rozdział 4 w kilku działach — zostaje nierozstrzygnięte, bez zgadywania po kontekście) oraz parser rodzaju komendy (`AmendmentCommandParser`), który nadal zna tylko jednostki redakcyjne. |
| **eId tymczasowy jednostek nazwanych** | „CZĘŚĆ OGÓLNA" → `czesc_TMP1`. Postać docelowa czeka na autorów schematu. |
| **Załączniki — reszta** | Załączniki są w modelu i w eksporcie (`AnnexBuilder`, `AnnexUnits`, `AnnexXmlConverter`, `TableXmlConverter`): 216 w 79 dokumentach korpusu, w 9 z jednostkami. Wobec przykładu DU/2026/252 zostaje: `orientacja` segmentu (układ strony); wiersz nagłówka tabeli bez oznaczenia w pliku (`w:tblHeader`); znacznik odnośnika przy oznaczeniu („Załącznik nr 3[2)]" — `zalacznik-nr` niesie sam numer). Jednostki załącznika niosą `wejscie-w-zycie` bez `okres`, jak w przykładzie (`TemporalDataXmlConverter.AssignPeriods`) — wbrew zdaniu w „Konsekwencjach" ADR-0028, które opisuje stan sprzed tej zmiany. Poza przykładem: wyliczenie w załączniku bez numeru i w treści mieszanej (zostaje akapitami — [ADR-0028](adr/0028-wyliczenie-w-zalaczniku-buduje-potok-aktu.md)); wyjątek wejścia w życie wskazujący załącznik; tytuł bez stylu i bez wyśrodkowania; tabela zagnieżdżona w komórce (rozcina tabelę nadrzędną na dwie); rysunki i obiekty osadzone; typ akapitu komórki ze stylu (`tekst-centr`, `tekst-1`…). |
| **Walidator .NET a jednostki pod `segment-zalacznika`** | `segment-zalacznika` to w schemacie dowolna treść (`xs:any`, `lax`), a `nie-ztp-ustep`, `punkt` i `litera` nie mają deklaracji globalnej, więc pod nim nie są sprawdzane wobec typu. Walidator .NET bierze `gd:GUID` takiego elementu za drugą wartość pola elementu nadrzędnego i zgłasza „The field 'GUID' is expecting at the most one value" (ograniczenie `AktGUID`), a samej unikalności tam nie sprawdza. **To odstępstwo walidatora, nie niezgodność ze schematem**: libxml2 i Xerces (JDK) przyjmują przykład autorów schematu (DU/2026/252) i nasz eksport bez uwag o GUID, a powtórzony GUID w załączniku zgłaszają jako powtórzenie; w .NET wystarcza jedna jednostka z unikalnym GUID, żeby komunikat się pojawił. `SchemaPlValidator.Validate` pomija więc ten komunikat przy elemencie bez deklaracji i sam sprawdza unikalność atrybutu — 9 dokumentów wróciło do zgodnych w całości. Ten sam mechanizm stał za kaskadą komunikatów o `GUID` po błędzie w nagłówku. Opis przyczyny w [ADR-0028](adr/0028-wyliczenie-w-zalaczniku-buduje-potok-aktu.md) („Konsekwencje") jest przez to nieaktualny. | Po stronie schematu nic nie jest konieczne. Do przekazania autorom jako informacja: (1) konsumenci na .NET zobaczą fałszywy komunikat; (2) typowana treść załącznika (np. grupa `tresc-strukturalna-uniwersalna`, której używa `cytat-strukt`) wymaga decyzji, co wolno w załączniku — przykład stawia `punkt` pod `nie-ztp-ustep`, czego typy jednostek nie dopuszczają; (3) `processContents="skip"` niczego nie naprawia w .NET, a w pozostałych walidatorach wyłącza sprawdzanie unikalności. |
| **TJ: „(uchylony)" jako tytuł** | Jednostka uchylona w tekście jednolitym dostaje tytuł „(uchylony)"; schemat przewiduje dla niej `uchylenie-OTJ`. |
| **TJ: obwieszczenie — reszta** | Tekst jednolity ustawy ma korzeń `obwieszczenie` z aktem osadzonym ([ADR-0031](adr/0031-tekst-jednolity-pod-korzeniem-obwieszczenie.md); 419 dokumentów korpusu), a jego punkty, podpunkty, część wspólna podpunktów i podpis są jednostkami w modelu i w eksporcie ([ADR-0032](adr/0032-podpunkt-obwieszczenia-ma-skrot-ppkt.md): 854 `punkt-OTJ`, 1441 `podpunkt-OTJ`). Pokrycie tekstu TJ: 91,1 % akapitów. Zostaje wg `ObwieszczenieTyp` i przykładu autorów schematu (`DU20261261_obw_tj.xml`): (1) przytoczenia jako `tresc-strukt` z jednostkami adresowanymi w akcie przytaczanym (`DU/2025/1792__art_19`) — dziś są dalszą treścią (`fragment`, 9222 akapity); (2) litery pod podpunktem jako jednostki; (3) `DataStanuPrawnegoTJ` z punktu 1 („z uwzględnieniem stanu prawnego na dzień …") i `DataObowiazywaniaWersji` — tekst jednolity ma już jedno zdarzenie `ujednolicenie` i jednostki bez dat ([ADR-0033](adr/0033-tekst-jednolity-ma-zdarzenie-ujednolicenia.md)); (4) kodeks z artykułami wprost w akcie (bez jednostek systematyzacyjnych) zostaje przy elemencie `ustawa` — artykuł stojący wprost w `kodeks` ma w schemacie inną budowę (`paragraf-kodeksu-n` zamiast `ustep-n`); w korpusie każdy kodeks ma jednostki systematyzacyjne i wychodzi jako `kodeks` (13 tekstów jednolitych); (5) tekst jednolity rozporządzenia, uchwały i zarządzenia zostaje przy korzeniu `ustawa` bez treści obwieszczenia i bez podpisu — do czasu budowania jednostek „§". |
| **TJ składany w całości stylami zmian** | Gdy całe rozdziały i artykuły tekstu jednolitego mają style `Z…` (np. `305231_2733472`, `349921_3119995`), zbierają się jako treść nowelizacji. Reguła „jednostka w stylu Z bez komendy" obejmuje dziś tylko oznaczenie i tytuł jednostki, nie artykuły. |
| **Nienumerowane podtytuły** | „Przepis wstępny", „A. Plany naprawy" w stylu tytułu jednostki — ZTP ich nie przewiduje, model nie ma miejsca na grupę artykułów bez oznaczenia; przy dołączaniu tytułu traktowane jak osobne bloki. |
| **Organy w metadanych** | Organ wydający jest znany tylko dla ustaw (`SEJM`); dla rozporządzeń i obwieszczeń stoi w tytule w dopełniaczu („Ministra Finansów") i wychodzi jako `UNKNOWN` (2078 dokumentów korpusu). Słownik zasobów ELI ma dwa wpisy potwierdzone przykładami autorów schematu (Sejm, Prezydent). Imię osoby podpisującej to inicjał z aktu („A."), nie pełne imię jak w przykładach. Podpis czytany jest tylko w jednej formie: organ, dwukropek, inicjały, nazwisko — bez „w z." i „z up.". Rodzaj `OBW_TJ` poza tytułem obwieszczenia opiera się na klasyfikacji dokumentu; 284 obwieszczenia wychodzą jako `OBW`. |
| **Akapit w stylu jednostki bez oznaczenia** | Akapit, którego potok nie rozpoznał, i tabela w treści przepisu są już dalszą treścią jednostki (`fragment`, `tabela` — [ADR-0029](adr/0029-akapit-bez-oznaczenia-jest-dalsza-trescia-jednostki.md)). Akapit **w stylu ustępu, punktu albo litery**, ale bez numeru („albo" między punktami, wiersz w stylu ustępu), nadal buduje się jako jednostka bez oznaczenia: `nie-ztp-*` z `brak-eid` ([ADR-0024](adr/0024-jednostka-bez-oznaczenia-nie-ma-eid.md)), 79 jednostek w korpusie — docelowo też dalsza treść poprzedniej jednostki. Sankcja karna w osobnym akapicie jest dalszym ciągiem zdania przepisu, ale bez elementu `sankcja-karna`, którym oznaczają ją przykłady autorów schematu. Osobno: litera „l)" w miejscu punktu „1)" (`300755_2688666` art. 22a) i oznaczenie przedziałem („3-5. (uchylone)"). |
| **Dalsza treść jednostki — reszta** | Wzór (`wzor-mat`) nie jest czytany: jednostka, której treścią jest sam wzór, nie ma `p`, a jej legenda staje się fragmentem bez `p` (jeden dokument niezgodny). Zdanie w nowym wierszu (styl `ZDANIENAST…`), treść ślubowania (`slubowanie`) i podpis w formie „wz." wychodzą jako `fragment`. W tekście jednolitym bez stylów tytuł aktu za obwieszczeniem może trafić do ostatniego przepisu obwieszczenia. Tabela cytowana w nowelizacji: cel komendy wskazującej wiersz tabeli („lp. 2b", „wiersz oznaczony kodem A 09") nie jest rozpoznawany (`element-eId` z kontekstu albo `UNKNOWN`); treść cytowana za tabelą (objaśnienia) nie należy już do cytatu; tabela zagnieżdżona i tytuł tabeli (`tytul-tabeli`) nie są rozróżniane. |
| **Polska litera w oznaczeniu a schemat** | „Art. 48ł.", „lit. ł" (8 dokumentów korpusu) czytają się do numeru, ale wzorzec `nr` jednostek zwykłych (`[a-z]`) i `EIdTyp` (`[A-Za-z0-9]`) ich nie przyjmują, więc `art_48ł` jest niezgodny ze schematem. ZTP takich liter nie zna (§ 57 ust. 4). Do rozstrzygnięcia z autorami schematu: zapis w eId (transliteracja zderza `ł` z `l`) i element (`nie-ztp-*` ma `nr` bez wzorca). |
| **Pełna walidacja XSD korpusu** | Miara: `CorpusValidator --xml-corpus` (cały dokument w podziale na kategorie i osobno treść aktu). Stan: zgodnych w całości 240 z 241 ustaw ogłoszonych i 410 z 460 tekstów jednolitych z treścią; treść aktu zgodna w 704 z 718 dokumentów. Wynik potwierdzony 2026-10-11 dwoma niezależnymi walidatorami: libxml2 i Xerces (JDK) uznają za zgodne ten sam zbiór 651 z 2321 dokumentów eksportu co .NET. Zostają: (1) polska litera w oznaczeniu (wiersz wyżej), 7 dokumentów; (2) numerowany ustęp bez „§" w artykule z paragrafami („2." zamiast „§ 2.", literówka źródła) wychodzi jako `ustep` w `artykul-kodeksu`, 6 dokumentów; (3) kodeks bez jednostek systematyzacyjnych: artykuł z „§" wprost w akcie jest zgodny tylko pod `kodeks`, a taki akt eksport pisze jako `ustawa` (element `kodeks` dostaje akt, którego artykuły stoją w jednostkach systematyzacyjnych) — w korpusie każdy kodeks je ma, więc niezgodności nie widać; (4) nagłówek bez danych: `DataAktu` bez daty wydania, puste `Rok`/`Pozycja`, gdy dokument nie ma nagłówka publikatora (głównie teksty jednolite i rozporządzenia). Po pierwszym błędzie walidator .NET przestaje sprawdzać dalszą treść wobec deklaracji; komunikaty o polu `GUID`, które wtedy zgłaszał, `SchemaPlValidator` pomija (wiersz „Walidator .NET a jednostki pod `segment-zalacznika`"). |

## 4. Testy, których plan przewidywał, a nie powstały

- **Korpus ekwiwalencji plikowej** — zakładano ten sam akt w czterech wariantach
  (DOCX-szablon / DOCX bez stylów / PDF / TXT) w `DocRepo/equivalence/`. Katalog nie istnieje;
  `ParserEquivalenceTests` porównuje modele z dokumentów budowanych w pamięci.
- **Property-testy ciągłości numeracji** — `NumberingContinuityPropertyTests` (łańcuchy następników
  n→n+1, n→na→nb, wstawki § 89, w tym wieloliterowe Xa→Xaa→Xab) nie istnieją.
- **Smoke-test na samym ZTP** — konwersja [ZTP-2026-300.md](ZTP-2026-300.md) → TXT z oczekiwaniem
  „rozporządzenie, wysoka pewność" i sparsowanymi jednostkami `§`.
- **Testy `Saga.Web` i `Saga.Cli`** — żaden z tych projektów nie ma testu i **nie wchodzi do
  raportu pokrycia**: `TEST_PROJECT` w `.gitlab-ci.yml` wskazuje wyłącznie `Saga.Core.Tests`,
  które referencjonuje tylko `Saga.Core` i `Saga.Model`. Próg różnicowy ich więc nie obejmuje —
  strażnik w `diff-coverage` liczy tylko pliki z projektów obecnych w raporcie (`<package>`)
  i wypisuje ostrzeżenie o pominiętych; sam brak dopasowania przestał być błędem, bo dla projektu
  bez testów nie ma czego dopasować. Konsekwencja: zmiany w `Saga.Web` i `Saga.Cli` przechodzą
  bez żadnej bramki pokrycia. Żeby je objąć, trzeba dopisać testy — (a) `WebApplicationFactory`
  w istniejącym `Saga.Core.Tests` (referencja do `Saga.Web` + `FrameworkReference
  Microsoft.AspNetCore.App` + `public partial class Program`), kod Web wchodzi wtedy do wspólnego
  raportu i CI nie wymaga zmian; albo (b) osobny `tests/Saga.Web.Tests`, czystsze, ale wymaga
  przebudowy uruchamiania testów i scalania pokrycia z dwóch projektów.
  Warte pokrycia w pierwszej kolejności: mapowanie wyjątków parsera na kody 400/415/422,
  sanityzacja nazwy pliku w `Content-Disposition` i to, że `/` oraz `/health` odpowiadają.

## 5. Ryzyka do pilnowania przy zmianach

- **Tiret vs część wspólna** — półpauza na początku wiersza jest niejednoznaczna; każda zmiana
  w tym obszarze rusza gałąź bezstylową i wymaga przejrzenia testów charakteryzujących.
- **Cytaty w części wstępnej obwieszczenia** — obwieszczenie TJ (§ 103–104 ZTP) cytuje przepisy
  zmieniające i pominięte. Od wydzielenia obwieszczenia (`AnnouncementBoundary`) jego treść nie przechodzi
  przez potok aktu ani nowelizacji; gdy przytoczenia będą budowane jako jednostki, bilans cudzysłowów
  (`QuoteBalanceTracker`) musi objąć także je. Fałszywy trigger w cytacie = błędna nowelizacja.

---

Uwagi zewnętrzne (PG) mają własne, niewersjonowane plany w `docs/internal/` — nie dubluj ich tutaj.
