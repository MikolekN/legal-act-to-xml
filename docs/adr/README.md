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

Nowy ADR: skopiuj [0000-template.md](0000-template.md), nadaj kolejny numer, dopisz wiersz wyżej.
