# ADR-0031: Tekst jednolity ustawy eksportuje się pod korzeniem `obwieszczenie`, a treść obwieszczenia nie jest treścią ogłaszanego aktu

- Status: Accepted
- Data: 2026-10-11

## Kontekst

Tekst jednolity to dwa akty w jednym dokumencie: obwieszczenie (§ 102–104 ZTP) i akt, który ono ogłasza
w załączniku. Obwieszczenie ma własny nagłówek („Obwieszczenie / Marszałka Sejmu … / z dnia … / w sprawie
ogłoszenia jednolitego tekstu ustawy …"), punkty („1. Na podstawie art. 16 …", „2. Podany w załączniku …
tekst jednolity nie obejmuje: 1) art. 57 ustawy …, który stanowi: „Art. 57. …"") i podpis. Dopiero za nim
stoi wiersz „Załącznik do obwieszczenia … (Dz. U. poz. …)" i ogłaszany akt z własną strefą tytułową
(„USTAWA / z dnia … / o …").

Potok traktował cały dokument jak jeden akt. Skutki w korpusie (460 tekstów jednolitych z treścią):

- przepisy przytoczone w obwieszczeniu budowały się jak artykuły ogłaszanego aktu — 2216 obcych artykułów
  w 420 dokumentach, główne źródło powtórzonych eId i 523 jednostek `nie-ztp-*`;
- z cudzych przepisów końcowych („Art. 74. Ustawa wchodzi w życie …" ustawy zmieniającej) powstawało 289
  zdarzeń cyklu życia, a z nich daty jednostek tekstu jednolitego;
- korzeniem eksportu była `ustawa` ze strefą tytułową obwieszczenia („rodzaj-aktu: Obwieszczenie"), a strefa
  tytułowa samego aktu ginęła.

Schemat ma na to osobny korzeń: `obwieszczenie` (`ObwieszczenieTyp`) z `autor-aktu`, punktami
`punkt-OTJ`, podpisem i aktem osadzonym (`ustawa` typu `TJUstawyTyp`: `zalacznik-do`, `rodzaj-aktu`, `data`,
`tytul`, jednostki). Tak zbudowany jest przykład autorów schematu (`DU20261261_obw_tj.xml`).

## Decyzja

**Dokument, który zaczyna się nagłówkiem obwieszczenia i zawiera wiersz „Załącznik do obwieszczenia …",
dzieli się na tym wierszu: wszystko przed nim jest obwieszczeniem, wszystko za nim — ogłaszanym aktem.**

- Granicę wyznacza `AnnouncementBoundary` z samej treści. Bloki sprzed niej omijają potok aktu: wiersze
  nagłówka czyta kolektor metadanych, reszta trafia do `LegalDocument.Announcement`.
- Nagłówek dokumentu (`KindLine`, `IssuerLine`, `DateLine`, `Subject`, `ActDate`, tytuł w metadanych) pozostaje
  nagłówkiem obwieszczenia. Strefę tytułową ogłaszanego aktu niesie `Announcement` (`AttachedTo`,
  `ActKindLine`, `ActDateLine`, `ActSubject`).
- Gdy ogłaszanym aktem jest ustawa, korzeniem eksportu jest `obwieszczenie`, a akt stoi w nim jako `ustawa`
  z własną strefą tytułową. Jednostki aktu zachowują prefiks aktu z ELI obwieszczenia (`DU/2025/1094__art_1`)
  — jak w przykładzie.

## Odrzucone alternatywy

- **Zostawić jeden akt i naprawiać skutki** (cytaty przez bilans cudzysłowów, deduplikacja eId). Przytoczenia
  nadal stałyby w treści aktu, a strefa tytułowa aktu nadal by ginęła; każda naprawa byłaby łataniem jednej
  konsekwencji błędnego założenia.
- **Granica ze stylów szablonu** (`PKTOTJ…`, `TEKSTZ…`). Styl jest wskazówką, nie warunkiem: 92 z 728
  obwieszczeń korpusu nie ma stylu na wierszu granicy, a wiersz „Załącznik do obwieszczenia" ma każde
  i dokładnie raz.
- **Granica z klasyfikacji dokumentu** (rodzaj `OBW_TJ`). Klasyfikator mówi, że to tekst jednolity, ale nie
  gdzie kończy się obwieszczenie; a obwieszczenie z załącznikiem innego rodzaju też ma tę granicę.
- **Eksport samego ogłaszanego aktu.** Metadane dokumentu (ELI, tytuł, data) opisują obwieszczenie, a jego
  punkty mówią, czego tekst jednolity nie obejmuje — to treść prawna, nie opakowanie.

## Konsekwencje

- Zmienia korzeń eksportu 419 dokumentów korpusu z `ustawa` na `obwieszczenie`; konsumenci, którzy szukają
  aktu pod `akt-prawny/ustawa`, znajdą go o poziom głębiej.
- Tekst jednolity rozporządzenia, uchwały albo zarządzenia zostaje przy dotychczasowym korzeniu, dopóki potok
  nie buduje jednostek „§" — eksport nie udaje, że ma akt, którego nie zbudował.
- Treść obwieszczenia jest w modelu jako akapity; jej jednostki (`punkt-OTJ`, `podpunkt-OTJ`), przytoczenia
  i podpis w eksporcie są osobną pracą (backlog). Do tego czasu pokrycie tekstu tekstów jednolitych jest
  niższe niż przed wydzieleniem obwieszczenia.
- Daty jednostek tekstu jednolitego nie mogą już pochodzić z przepisów przytoczonych w obwieszczeniu. Przykład
  autorów schematu nie nadaje ich wcale i ma jedno zdarzenie `ujednolicenie` — to kolejna praca (backlog).
- Obwieszczenie bez wiersza „Załącznik do obwieszczenia" nie ma granicy i przechodzi przez potok jak dotąd.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0031_ConsolidatedStatute_IsExportedUnderTheAnnouncement_WithoutItsQuotedProvisions`.
- `AnnouncementTests` — granica, nagłówek dokumentu i nagłówek ogłaszanego aktu, korzeń dla ustawy, dotychczasowy
  korzeń dla aktu ogłoszonego wprost i dla tekstu jednolitego rozporządzenia, zgodność treści ze schematem.
