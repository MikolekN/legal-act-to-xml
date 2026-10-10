# ADR-0027: Prefiks aktu w eId dopisuje eksport, model trzyma adres w akcie

- Status: Accepted
- Data: 2026-10-10

## Kontekst

Przykłady autorów schematu zapisują każdy adres z identyfikatorem ELI aktu: `DU/2024/928__art_1`.
Jednostka cytowana w nowelizacji i cel komendy (`element-eId`, `element-do-eId`) niosą prefiks aktu
**zmienianego**: `DU/2025/350__art_68__ust_1__pkt_5`. Typ `EIdTyp` dopuszcza adres z prefiksem i bez.

`BaseEntity.Id` składa adres ze ścieżki jednostki w akcie (`art_68__ust_1__pkt_5`). Na tym adresie
opiera się rozpoznawanie: wyjątki wejścia w życie szukają jednostki po ścieżce z klauzuli, a eId jednostki
cytowanej ma być co do znaku celem komendy ([ADR-0021](0021-eid-jednostki-cytowanej-od-adresu-celu.md)).

Bez prefiksu adresy w jednym dokumencie nie były unikalne — `rozdz_1` aktu i cytowany `rozdz_1` aktu
zmienianego, `art_5` cytowany z dwóch różnych ustaw — i nie dawały się zestawić między dokumentami.

Publikator nie należy do treści aktu: ten sam tekst ma go po ogłoszeniu, a jako projekt albo plik bez
nagłówka dziennika — nie.

## Decyzja

**Prefiks aktu dopisuje eksport, jednym przebiegiem po gotowym dokumencie (`ActPrefixXml.Apply`).
`BaseEntity.Id` zostaje adresem w akcie.**

- Jednostka aktu dostaje ELI jego publikatora (`LegalDocument.SourceJournal`).
- Jednostka pod `cytat-strukt` dostaje ELI aktu zmienianego — z atrybutu `akt` tego cytatu; cel komendy
  — z atrybutu `akt` elementu, który go niesie.
- W metadanych: `JednostkaAktu` i `dowod` wskazują jednostki aktu (prefiks własny), `JednostkaWZakresie`
  — jednostkę aktu zmienianego (prefiks odczytany z cytatu albo komendy, w których ten adres stoi).
- Prefiksu się nie zmyśla: akt bez znanego publikatora zostaje przy adresach bez prefiksu, tak samo cytat,
  którego aktu zmienianego nie odczytano.

## Odrzucone alternatywy

- **Prefiks w `BaseEntity.Id`.** Każde miejsce porównujące adresy musiałoby go zdejmować, a adres tej samej
  jednostki zależałby od tego, czy plik ma nagłówek dziennika. Prefiks jednostki cytowanej to akt
  zmieniany, znany dopiero po sparowaniu treści z komendą — `Id` zmieniałoby się w trakcie parsowania.
- **Prefiks pisany przez konwertery jednostek.** Adres piszą `UnitAddressXml`, konwertery jednostek
  systematyzacyjnych i części wspólnych, `AmendmentXmlConverter` i `TemporalDataXmlConverter`; każdy
  potrzebowałby dokumentu i cytatu, w którym stoi. Jedno pominięcie daje dokument mieszany. Przebieg
  po drzewie widzi cytat nadrzędny wprost.
- **Adresy bez prefiksu** (schemat je dopuszcza). Zostawia duplikaty w dokumencie i różni się od przykładów
  w każdym adresie.

## Konsekwencje

- Zmienia wyjście: każdy adres w dokumencie o znanym publikatorze. Korpus: 444 002 eId z prefiksem,
  7560 `element-eId`, 199 `element-do-eId`, 1406 `dowod`; poza prefiksem żaden z 2321 plików się nie
  zmienił. Bez prefiksu zostaje 20 dokumentów, których plik nie ma nagłówka dziennika, oraz cytaty
  jednej ustawy, w której nie odczytano publikatora aktu zmienianego.
- Adres jednostki cytowanej jest tym samym napisem, pod którym ta jednostka stoi w tekście aktu
  ogłoszonym pod tą pozycją (`DU/2025/350__art_68` w nowelizacji i w tekście jednolitym) — to podstawa
  zestawiania zmiany z tekstem zmienianym.
- Nowy atrybut niosący adres trzeba dopisać do `ActPrefixXml`; inaczej wyjdzie bez prefiksu.
- `JednostkaWZakresie` dostaje prefiks tylko wtedy, gdy jej adres jest adresem jednostki cytowanej albo
  celem komendy. Zakres, który został ścieżką z klauzuli („w zakresie ust. 1d" → `ust_1d`), prefiksu nie
  ma — stan w [backlogu](../backlog.md).
- `CorpusValidator --compare-reference` porównuje adresy z prefiksem.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0027_ActPrefix_IsAddedByTheExport_AndQuotedUnitsCarryTheAmendedAct`
  i `Adr0027_ActWithoutAKnownJournal_KeepsAddressesWithoutAPrefix`.
- `CorpusValidator --compare-reference`: adresy z prefiksem zgodne z przykładami autorów schematu
  (411 z 411, 497 z 497, 2239 z 2280 — brakujące to załącznik).
