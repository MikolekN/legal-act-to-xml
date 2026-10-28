# ADR-0127: Część wspólna jako miejsce zmiany ma adres `…__czw`; zdanie w niej liczy się w niej samej

- Status: Accepted — Supersedes [ADR-0102](0102-zdanie-nazwane-w-komendzie-jest-celem.md) w części o zdaniu w części wspólnej
- Data: 2026-10-28

## Kontekst

Komenda „w ust. 1 w części wspólnej wyrazy „X” zastępuje się …” wskazywała cały ustęp: odczyt adresu nie znał części
wspólnej jako miejsca (tokenizer wyłączał „części wspólnej” z jednostek systematyzacyjnych, nic jej nie przejmowało), więc
instrukcja z `wystapienie` „wszystkie” zmieniłaby te same wyrazy także we wprowadzeniu i w punktach. Zdanie w części
wspólnej liczyło się w ustępie („w części wspólnej w zdaniu drugim” → `…__ust_1__zdn_2`), choć ADR-0102 zostawił je
ogólnym regułom adresu. Autorzy schematu (pytanie 7): zmiana w części wspólnej wskazuje część wspólną — `…__czw`, a zdanie
pod nią liczy się w niej (`…__ust_1__czw__zdn_2`). Część wspólna zamykająca wyliczenie ma `eId` gospodarza z `czw`
([ADR-0025](0025-czesc-wspolna-ma-eid-gospodarza-z-czw.md)). Część wspólna jako przedmiot („część wspólna otrzymuje
brzmienie”) była już cytatem strukturalnym ([ADR-0086](0086-czesc-wspolna-bez-wyliczenia-i-bez-komendy-ktora-ja-nazywa-jest-tekstem-cytowanym.md)).

## Decyzja

- **Miejscownik „w części wspólnej” jest poziomem adresu** — między jednostkami a zdaniem (`StructuralReference.CommonPart`,
  `UnitLevel.CommonPart`; `…__czw` przed `__zdn_N`). Wprost za jednostką wskazuje część wspólną jej wyliczenia; dołączony
  spójnikiem do pozycji wyliczenia („w pkt 2 i w części wspólnej”) — część wspólną wyliczenia tej pozycji, obok niej.
  Słowo to czyta tylko odczyt celów nowelizacji (`UnitAddressReader`, wariant tokenizera z miejscami w akcie zmienianym).
- **Miejscem nie jest** za czasownikiem komendy, za spójnikiem po prozie („we wprowadzeniu do wyliczenia i w części
  wspólnej”) ani obok ustępu czy artykułu — cel zostaje, jaki był.
- **Jednostka cytowana stoi przy jednostce, nie przy miejscu w niej**: „w pkt 24 w części wspólnej kropkę zastępuje się
  średnikiem i dodaje się pkt 25” — zamiana w `…__pkt_24__czw`, dodanie po `…__pkt_24` (`AmendmentXmlConverter`,
  `AmendmentTargetPairing` — `StructuralReference.HostUnit`).
- **Część wspólna jednostki załącznika zostaje instrukcją ręczną na jednostce** (`ManualTargets`, ADR-0100): eksport nie
  zna części wspólnej z `eId` pod jednostką załącznika.
- **Miejsce w jednostce odziedziczonej** (część wspólna albo zdanie — „1) w ust. 1 i 2: a) w części wspólnej …”) leży
  w każdej jednostce przepisu nadrzędnego; jednostka nazwana przez przepis niższy — jak dotąd w pierwszej (ADR-0016).

## Odrzucone alternatywy

- **Cel w całej jednostce** (dotychczas). Zmiana objęłaby te same wyrazy poza częścią wspólną.
- **Tylko zdanie w części wspólnej** (przypadek z odpowiedzi 7). Zostawiłby 30 instrukcji na całych jednostkach, a nowy
  poziom adresu byłby użyty połowicznie.
- **Część wspólna jednostki załącznika automatycznie** (`zal_N…__czw`). Spójne z ADR-0028, ale korpus nie ma ani jednej
  takiej części wspólnej z `eId` — nie da się tego sprawdzić.

## Konsekwencje

- Korpus DOCX: ok. 34 instrukcje w 24 plikach dostają adres `…__czw` (w tym 1 zdanie `…__czw__zdn_2`) i 1 cel odzyskany;
  171 aktów ISAP: 5 w 4 aktach.
- `…__czw__zdn_N` nie ma odpowiednika we własnym eksporcie SAGA: część wspólna wychodzi jednym zdaniem (backlog).
- `…__czw` trafia tylko w część wspólną zamykającą wyliczenie; części wspólne przerywające wyliczenie i w odnośnikach `eId`
  nie mają, czego akt zmieniający nie pokazuje.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0127_CommonPartNamedAsAPlace_IsTheTarget`
- `UnitReferencesTests.Reader_CommonPartAsAPlace`, `UnitReferencesTests.CommonPartPlace_IsAKeywordOfTheAmendmentAddressOnly`,
  `UnitReferencesTests.StructuralReference_CommonPart_StandsBetweenUnitsAndSentence`
- `SentenceTargetTests` — zdanie w części wspólnej, zmiana znaku przed dodanym punktem, miejsce w jednostce odziedziczonej,
  część wspólna jednostki załącznika
