# Decyzje architektoniczne (ADR)

Jeden plik = jedna decyzja. Zapis jest **niezmienny**: zaakceptowanego ADR nie edytujemy poza
dopisaniem statusu. Zmiana zdania = nowy ADR ze statusem `Supersedes ADR-NNNN`, a stary dostaje
`Superseded by ADR-NNNN`. Dzięki temu widać nie tylko stan, ale i drogę do niego.

## Kiedy pisać ADR

Tylko wtedy, gdy decyzja spełnia co najmniej jeden warunek:

- **miała odrzuconą alternatywę** — ktoś kiedyś zapyta „dlaczego nie prościej / nie tą biblioteką",
- **jest trudno odwracalna** — wycofanie oznacza przebudowę, nie zmianę jednej linii,
- **jest nieoczywista** — kod wygląda na skomplikowany bez powodu, dopóki nie znasz kontekstu.

Nie piszemy ADR dla wyborów bez alternatywy („używamy xUnit") ani dla konwencji kodu — te są
w [CLAUDE.md](../../CLAUDE.md). Jak coś działa dziś: [architecture.md](../architecture.md).
Czego nie ma i dlaczego odłożone: [backlog.md](../backlog.md).

## Indeks

| ADR | Decyzja | Status | Data |
|---|---|---|---|
| [0001](0001-jeden-ujednolicony-potok-wejscia.md) | Wszystkie formaty schodzą się do `DocumentBlock[]`; jeden potok, nie ścieżka per format | Accepted | 2026-07-16 |
| [0002](0002-adaptery-nie-udaja-styleid.md) | Adaptery PDF/TXT nie zgadują `styleId` — brak stylu zostaje brakiem | Accepted | 2026-07-16 |
| [0003](0003-backbone-warunkiem-uznania-za-akt.md) | Bez silnego sygnału strukturalnego dokument nie jest aktem, choćby zebrał punkty | Accepted | 2026-07-17 |
| [0004](0004-pdfpig-zamiast-itext.md) | PdfPig (Apache-2.0) do odczytu PDF; iText odrzucony z powodu AGPL | Accepted | 2026-07-19 |
| [0005](0005-pdf-tylko-warstwa-tekstowa.md) | PDF wyłącznie z warstwą tekstową; skan odrzucany wyjątkiem, OCR poza zakresem | Accepted | 2026-07-19 |
| [0006](0006-nie-akt-nie-jest-bledem.md) | Nie-akt to wynik z raportem, nie wyjątek; decyzja „czy parsować" u wywołującego | Accepted | 2026-07-20 |
| [0007](0007-klasyfikacja-nie-mutuje-type.md) | Rozpoznany rodzaj aktu ląduje w `Classification`, nigdy w `LegalDocument.Type` | Accepted | 2026-07-20 |
| [0009](0009-jedno-zrodlo-liczebnikow-polskich.md) | Słownik polskich liczebników. Generuje także fragment regexa do ich wyszukiwania | - | 2026-07-29 |
| [0010](0010-jeden-kursor-sciezki-jednostek.md) | Kursor ścieżki jednostek | - | 2026-07-30 |
| [0011](0011-data-ogolna-rozsylana-po-calym-drzewie.md) | Data reguły ogólnej wejścia w życie trafia na wszystkie jednostki. Brak daty znaczy nierozstrzygnięty wyjątek | - | 2026-07-31 |
| [0012](0012-naglowek-strony-jako-rola-bloku.md) | Nagłówek aktu | - | 2026-08-28 |
| [0013](0013-moc-wsteczna-przyslania-date-dopiero-na-wyjsciu.md) | Moc wsteczna przysłania datę wejścia w życie dopiero przy zapisie; model trzyma obie daty osobno | - (w części zastąpiony przez 0022) | 2026-09-03 |
| [0014](0014-skroty-jednostek-w-eid.md) | Skróty jednostek w eId (levelType) wyznacza jedna zamknięta tabela — także w adresach wskazywanych | Accepted | 2026-09-23 |
| [0015](0015-jedno-zrodlo-powolan-jednostek.md) | Powołania jednostek rozbiera jedno źródło (słownik, ścieżka, „§”) — wejście w życie i cele nowelizacji; samodzielny „§” rozstrzyga rodzaj aktu | Accepted | 2026-09-23 |
| [0016](0016-wyliczenie-celow-to-osobne-komendy.md) | Wyliczenie celów nowelizacji to osobne komendy; `element-do-eId` znaczy wyłącznie koniec przedziału | Accepted | 2026-09-23 |
| [0017](0017-eid-jednostki-redakcyjnej-bez-sciezki-systematyzacyjnej.md) | eId jednostki redakcyjnej bez ścieżki jednostek systematyzacyjnych (`art_5`, nie `rozdz_1__art_5`); jednostka systematyzacyjna niesie ścieżkę jawnych przodków | Accepted | 2026-09-25 |
| [0018](0018-postac-oznaczenia-w-eid.md) | Oznaczenie w eId: liczba arabska (także z rzymskiej i słownej), sufiks jak w tekście, bez indeksu górnego | Accepted — w części zastąpiony przez 0019 | 2026-09-25 |
| [0019](0019-indeks-gorny-w-eid.md) | Indeks górny zostaje w eId w notacji „[n]" (`art_53[1]`) — zgodnie ze schematem i przykładami jego autorów | Accepted | 2026-10-09 |
| [0020](0020-eksport-jednostek-systematyzacyjnych.md) | Jednostki systematyzacyjne w eksporcie zagnieżdżone wg schemaPL; niejawny łańcuch nad pierwszą jawną jednostką znika, pod nią niejawne poziomy to `-n` | Accepted | 2026-10-09 |
| [0021](0021-eid-jednostki-cytowanej-od-adresu-celu.md) | eId jednostki cytowanej w nowelizacji zaczyna się od adresu celu w akcie zmienianym (`art_68__ust_1__pkt_5`, nie `pkt_5`) | Accepted | 2026-10-09 |
| [0022](0022-daty-aktu-jako-zdarzenia-cyklu-zycia.md) | Daty aktu w metadanych jako zdarzenia cyklu życia (`DaneCzasowe`): jedno na każdą datę wejścia w życie i mocy wstecznej, wyjątek wymienia swoje jednostki; jednostki wskazują grupę czasową atrybutem `okres` | Accepted | 2026-10-10 |
| [0023](0023-poziom-pominiety-jako-jednostka-niejawna.md) | Poziom pominięty w akcie (litery wprost w ustępie) to jednostka niejawna: bez segmentu w eId (`art_5__ust_1__lit_a`) i bez elementu w eksporcie — jej dzieci stają w najbliższej jednostce jawnej jako `nie-ztp-*` | Accepted | 2026-10-10 |
| [0024](0024-jednostka-bez-oznaczenia-nie-ma-eid.md) | Jednostka bez odczytanego oznaczenia nie ma eId: niesie `brak-eid="jednostka-nieoznaczona"`, pusty `nr`, wychodzi jako `nie-ztp-*` i nie wnosi segmentu do eId jednostek pod sobą (`art_16f__pkt_1`, nie `art_16f__ust__pkt_1`) | Accepted | 2026-10-10 |
| [0025](0025-czesc-wspolna-ma-eid-gospodarza-z-czw.md) | Część wspólna zamykająca wyliczenie ma eId: adres gospodarza i segment `czw` (`art_51__ust_1__czw`), jak w przykładach autorów schematu | Accepted | 2026-10-10 |
| [0026](0026-odnosnik-tresc-w-rejestrze-miejsce-w-tekscie.md) | Treść odnośnika w rejestrze dokumentu (`LegalDocument.Footnotes`), miejsce — znacznik `[N)]` w tekście jednostki; eksport osadza `odnosnik` w miejscu znacznika, kolejne odwołanie to `odsylacz-do-odnosnika` | Accepted | 2026-10-10 |
| [0027](0027-prefiks-aktu-dopisuje-eksport.md) | Prefiks aktu w eId (`DU/2024/928__art_1`) dopisuje eksport jednym przebiegiem; jednostka cytowana i cel komendy niosą prefiks aktu zmienianego; `BaseEntity.Id` zostaje adresem w akcie | Accepted | 2026-10-10 |
| [0028](0028-wyliczenie-w-zalaczniku-buduje-potok-aktu.md) | Wyliczenie w załączniku buduje potok aktu pod artykułem zastępczym zakotwiczonym w adresie załącznika (`zal_4__ust_I__pkt_1`); wszystko albo nic — treść mieszana zostaje akapitami; oznaczenie rzymskie tylko w załączniku | Accepted | 2026-10-10 |
| [0029](0029-akapit-bez-oznaczenia-jest-dalsza-trescia-jednostki.md) | Akapit bez oznaczenia i tabela w treści przepisu są dalszą treścią jednostki, za którą stoją (`BaseEntity.Continuation` → `fragment`, `tabela`); komórki tabeli nie przechodzą przez rozpoznawanie jednostek | Accepted | 2026-10-10 |
| [0030](0030-warianty-cudzyslowu-rozpoznaje-jedno-zrodlo.md) | Warianty cudzysłowu (`„`, `”`, `“`, `ˮ`, `"`) trzyma wyłącznie `QuotationMarks`; tekstu się nie normalizuje — eksport niesie znak ze źródła | Accepted | 2026-10-10 |
| [0031](0031-tekst-jednolity-pod-korzeniem-obwieszczenie.md) | Tekst jednolity dzieli się na wierszu „Załącznik do obwieszczenia …": treść przed nim to obwieszczenie (`LegalDocument.Announcement`), nie ogłaszany akt; tekst jednolity ustawy eksportuje się pod korzeniem `obwieszczenie` z aktem osadzonym | Accepted | 2026-10-11 |
| [0032](0032-podpunkt-obwieszczenia-ma-skrot-ppkt.md) | Jednostki obwieszczenia tekstu jednolitego buduje `AnnouncementUnits` poza potokiem aktu i adresuje od obwieszczenia: punkt `pkt_1`, podpunkt `pkt_1__ppkt_2` (rozszerzenie tabeli ADR-0014 o `ppkt`); przytoczenie jest dalszą treścią jednostki | Accepted | 2026-10-11 |
| [0033](0033-tekst-jednolity-ma-zdarzenie-ujednolicenia.md) | Tekst jednolity nie czyta klauzuli wejścia w życie: ma jedno zdarzenie `ujednolicenie` w dniu ogłoszenia obwieszczenia, a jego jednostki nie niosą `wejscie-w-zycie` ani `okres` | Accepted | 2026-10-11 |
| [0034](0034-paragraf-jest-jednostka-podstawowa-aktu-wykonawczego.md) | W rozporządzeniu, uchwale i zarządzeniu „§ N." jest jednostką podstawową: encja `Article` z `IsSection`, adres `art_5__ust_2`, element `paragraf`, korzeń według rodzaju aktu; rozstrzyga rodzaj aktu z klasyfikatora, nie sam znak | Accepted | 2026-10-11 |
| [0035](0035-paragraf-po-paragrafie-to-kolejny-paragraf.md) | W akcie opartym na paragrafach „§" po paragrafie wskazuje kolejny paragraf (rodzeństwo), nie paragraf kodeksu; po jawnym „art." pozostaje paragrafem kodeksu — uzupełnia regułę 1 z ADR-0015 | Accepted | 2026-10-11 |
| [0036](0036-przytoczone-artykuly-sa-jednostkami-aktu-przytaczanego.md) | Artykuły przytoczone w obwieszczeniu są jednostkami w `tresc-strukt`, adresowanymi w akcie, z którego pochodzą (`Quotation`); przytoczenie, którego nie da się tak zbudować w całości, zostaje tekstem — zastępuje punkt ADR-0032 o przytoczeniu | Accepted | 2026-10-11 |
| [0037](0037-rownanie-worda-wchodzi-do-tekstu-w-zapisie-liniowym.md) | Równanie Worda (`m:oMath`) wchodzi do tekstu akapitu w zapisie liniowym (`E_i`, `x^2`, `a/b`) w miejscu, w którym stoi — treść wzoru nie znika; MathML (`wzor-mat`) jest kształtem docelowym eksportu | Accepted | 2026-10-11 |
| [0038](0038-wzor-w-osobnym-akapicie-to-wzor-mat-z-mathml.md) | Akapit złożony z samego równania jest wzorem: dalsza treść jednostki z MathML obok zapisu liniowego (`ContentBlock.Formula`), w eksporcie `wzor-mat` → `wzor` → `mml:math`; wzór nie jest jednostką, a nawias kwadratowy równania nie zajmuje kanału `[x]` | Accepted | 2026-10-11 |
| [0039](0039-akapit-w-stylu-jednostki-bez-oznaczenia-jest-dalsza-trescia.md) | Akapit w stylu ustępu, punktu, litery albo tiretu bez oznaczenia w treści jest dalszą treścią jednostki, za którą stoi; jednostką bez odczytanego oznaczenia zostaje tylko akapit z oznaczeniem spoza wzorca (`UnitDesignation`) i akapit bez jednostki do kontynuowania — wykonuje alternatywę odłożoną w ADR-0024 | Accepted | 2026-10-11 |
| [0040](0040-warianty-myslnika-z-jednego-zrodla.md) | Znaki myślnika są zdefiniowane raz (`Services/Dashes.cs`): tiret, część wspólna po myślniku, przedział oznaczeń i legenda wzoru rozpoznają każdy wariant (dywiz, myślnik cyfrowy, półpauza, pauza, minus); tekst nie jest normalizowany | Accepted | 2026-10-11 |
| [0041](0041-organ-ktory-sam-podpisuje-jest-jednym-organem-wydajacym.md) | Organ wydający aktu innego niż ustawa to organ ze strefy tytułowej; gdy nazywa go formuła podpisu, jest jednym organem `wydajacy` z osobą podpisującą (nazwa z podpisu), a podpisujący spoza strefy tytułowej — przewodniczący organu kolegialnego, kontrasygnata — osobnym organem `podpisujacy` (`AuthoritiesXmlConverter`) | Accepted | 2026-10-11 |
| [0042](0042-myslnik-bez-wprowadzenia-z-dwukropkiem-nie-otwiera-tiretu.md) | Myślnik pod ustępem bez otwartego wyliczenia otwiera tiret tylko po wprowadzeniu zakończonym dwukropkiem (§ 56 ZTP) albo w stylu tiretu; w innym razie domyka urwane zdanie przepisu albo jest jego dalszą treścią (`StructureProcessor.TryContinueAfterDash`) | Accepted | 2026-10-11 |
| [0043](0043-obwieszczenie-ktore-nie-oglasza-aktu-jest-korzeniem.md) | Obwieszczenie, które nie ogłasza aktu (stawki, wskaźniki, wykazy), jest obwieszczeniem w całości: korzeń `obwieszczenie` bez elementu aktu, `podstawa-prawna`, podpis i załączniki obwieszczenia; czy ogłasza akt, rozstrzyga przedmiot albo rodzaj aktu za wierszem załącznika (`AnnouncementBoundary.Describe`, `Announcement.AnnouncesAct`) | Accepted | 2026-10-11 |
| [0044](0044-tresc-obwieszczenia-bez-punktow-jest-przepisem-bez-oznaczenia.md) | Treść obwieszczenia bez numerowanych punktów jest jednym przepisem bez oznaczenia (`Announcement.Body`, eksport `nie-ztp-artykul` z `brak-eid`), zbudowanym potokiem aktu (`AnnouncementBody`): punkty, litery i tirety adresowane od obwieszczenia; podstawa prawna otwierająca wyliczenie jest tekstem przepisu | Accepted | 2026-10-11 |
| [0045](0045-zwrot-zmiany-brzmienia-zapowiada-tresc-gdy-zostawia-ja-otwarta.md) | Zwrot zmiany brzmienia („otrzymuje brzmienie:", „w brzmieniu:") zapowiada treść cytowaną w kolejnych akapitach tylko wtedy, gdy kończy akapit albo otwiera cytat, którego akapit nie zamyka (`AmendmentFinalizer.LeavesWordingOpen`); przepis z brzmieniem w tym samym akapicie nie jest komendą | Accepted | 2026-10-11 |
| [0046](0046-akapit-mala-litera-za-pozycja-wyliczenia-jest-czescia-wspolna.md) | Akapit bez oznaczenia i bez myślnika, zaczynający się małą literą, tuż za pozycją wyliczenia zakończoną przecinkiem albo bez znaku końcowego zamyka wyliczenie jako część wspólna (`StructureProcessor.TryCloseEnumerationWithoutDash`) — uściśla ADR-0029 | Accepted | 2026-10-11 |
| [0047](0047-znacznik-bez-odnosnika-tego-aktu-wychodzi-indeksem-gornym.md) | Znacznik odnośnika, za którym nie stoi odnośnik tego aktu (akt cytowany, objaśnienie pod tabelą), wychodzi jako `sup`, nie zapisem kanału „[1)]"; dotyczy samego znacznika (`FootnoteMarkers.Bare`) — uściśla ADR-0026 | Accepted | 2026-10-12 |

Nowy ADR: skopiuj [0000-template.md](0000-template.md), nadaj kolejny numer, dopisz wiersz wyżej.
