# ADR-0063: Zdarzenie zmiany w tekście jednolitym należy do aktu, który zmianę wprowadza

- Status: Accepted
- Data: 2026-10-12

## Kontekst

Zdarzenie, na które czeka jednostka tekstu jednolitego ([ADR-0060](0060-jednostka-drukowana-dwa-razy-to-dwie-wersje-rozrozniane-okresem.md)), miało w identyfikatorze pozycję
dziennika samego tekstu jednolitego — dokumentu, w którym je odczytano — i nie mówiło, kto zmianę wprowadza.
Schemat ma na to atrybuty `zrodlo` („ELI aktu sprawczego. Stan bez pochodzenia jest bezwartościowy — nie da się
odpowiedzieć «kto i czym to zmienił»") i `podstawa` („przepis sprawczy odczytany z odnośnika"), a przykład
identyfikatora w jego dokumentacji stawia w polu aktu akt zmieniający:
`zdarz_uchylenie_2026-04-18_DU-2015-1634_odczytane` to uchylenie art. 479(45) Kodeksu postępowania cywilnego przez
ustawę z 2015 r. — przypadek z korpusu.

Odnośnik nazywa akt zmieniający w pełni tylko raz, a dalej odsyła („ustawy, o której mowa w odnośniku 3"); to
odesłanie czytamy od [ADR-0062](0062-termin-zmiany-zapisany-przy-akcie-zmieniajacym-obejmuje-jego-zmiany.md).

## Decyzja

**Zdarzenie zmiany należy do aktu, który ją wprowadza: jego adres stoi w identyfikatorze zdarzenia i w `zrodlo`.**

- **Akt** (`WordingVersion.ChangeAct`): nazwany w odnośniku datą i publikatorem („ustawy z dnia 14 kwietnia 2023 r.
  … (Dz. U. poz. 818)"). Rocznik bierze się z daty aktu, gdy publikator go pomija (§ 158 ust. 7 ZTP), a pozycją
  jest pierwsza wypisana — dalsze to zmiany tego aktu (`JournalReferenceService.ReadFirstAct`). Gdy odnośnik nazywa
  akt odesłaniem, akt czyta się tam, dokąd odesłanie prowadzi, nigdy z innych publikatorów tego samego odnośnika.
- **Identyfikator**: `zdarz_<rodzaj>_<dzień albo odn-N>_<akt zmieniający>_odczytane`. Zmiany dwóch aktów tego
  samego dnia to dwa zdarzenia. Gdy aktu nie nazwano publikatorem, w polu zostaje dokument, w którym zdarzenie
  odczytano, a `zrodlo` nie ma.
- **`podstawa`**: przepisy aktu zmieniającego, które na to zdarzenie czekają, tak jak nazywają je odnośniki
  (`WordingVersion.ChangeProvision` — tekst między „przez" a nazwą aktu), w kolejności dokumentu, rozdzielone
  średnikiem.
- **`dowod`** zostaje odnośnikiem, w którym stoi termin zdarzenia (ADR-0062).

## Odrzucone alternatywy

- **Pozycja tekstu jednolitego w polu aktu** (stan dotychczasowy). Tekst jednolity zmiany nie wprowadza, tylko
  ją zapisuje; zdarzenia różnych aktów tego samego dnia zlewały się w jedno.
- **`podstawa` = przepis z odnośnika-dowodu.** Odnośnik, w którym stoi termin, bywa odnośnikiem innej zmiany
  tego aktu (termin całego aktu, ADR-0062) — jego przepis nie sprawia uchylenia, które na ten termin czeka.
- **`podstawa` = pierwszy przepis** albo **tylko wtedy, gdy jest jeden.** Pierwszy wygląda na jedyny; pominięcie
  gubi informację przy większości nowelizacji. Lista jest pełna i prawdziwa, choć bywa długa.
- **Zdarzenie na każdy przepis.** Schemat wyprowadza identyfikator z rodzaju, terminu i aktu — jednostki
  o tym samym przedziale mają wskazywać tę samą grupę.

## Konsekwencje

- W korpusie zmienia się 107 tekstów jednolitych — identyfikatory zdarzeń i grup oraz `okres` jednostek. Z 268 zdarzeń zmiany 238 nazywa akt sprawczy (`zrodlo`), a 30 zostaje przy dokumencie; `podstawa` ma 254 zdarzenia (160 — jeden przepis, 71 — od dwóch do pięciu, 23 — więcej, najwięcej 50). Zdarzeń jest o 13 więcej niż przedtem (255): zmiany dwóch aktów tego samego dnia to dwa zdarzenia. Zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory); treść bez zmian.
- Bez aktu zostają zdarzenia, których odnośnik odsyła inną postacią („ustawy, o której mowa w zdaniu pierwszym
  odnośnika 33", „ustawy wymienionej jako druga w odnośniku 11") albo mówi o samej ustawie („Wejdzie w życie
  z dniem … na podstawie art. 169 pkt 4 niniejszej ustawy").
- Pytanie do autorów schematu: `podstawa` jest jednym tekstem, a zdarzenie wyprowadzone z rodzaju, terminu i aktu
  zbiera kilka przepisów — czy lista przepisów jest tym, czego atrybut oczekuje.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0063_ChangeEvent_BelongsToTheActThatMakesTheChange`
- `WordingVersionTests` — akt i przepis w modelu, `zrodlo` / `podstawa` / `dowod` zdarzenia, dwa akty jednego
  dnia, akt nazwany odesłaniem, kilka przepisów jednego zdarzenia, akt nienazwany, pierwsza pozycja publikatora.
