# ADR-0106: Odnośnik jako miejsce zmiany („w odnośniku nr 1 …") jest celem komendy — jego akapit albo punkt to instrukcja ręczna na odnośniku

- Status: Accepted
- Data: 2026-10-20

## Kontekst

„w odnośniku nr 1 do tytułu ustawy: a) w akapicie drugim w pkt 5 kropkę zastępuje się średnikiem i dodaje się pkt 6 …,
b) akapit trzeci otrzymuje brzmienie: …" (Dz. U. 2025 poz. 1863, plik 354686). Odnośnik był celem tylko wtedy, gdy był
przedmiotem komendy („odnośnik nr 1 otrzymuje brzmienie:" — [ADR-0059](0059-odnosnik-jako-przedmiot-komendy-jest-cytowanym-odnosnikiem.md)).
Jako miejsce nie był rozpoznawany: lit. a wskazywała punkt aktu (`…__pkt_5`), a lit. b wychodziła z `UNKNOWN`. Tabela
`eId` nie ma poziomu akapitu, a numery punktów powtarzają się w akapitach odnośnika.

Ustalenie z 2026-10-19 (pytanie 24, zasada ogólna 4 w `docs/internal/odpowiedzi-autorow-schematu.md`): `element-eId`
najgłębszej adresowalnej jednostki — samego odnośnika (`DU/2022/673__odn_1`); jego akapity i punkty `eId` nie mają, więc
w akcie istniejącym instrukcja ręczna (`zastap-recznie` dla akapitu, `dodaj-recznie` dla punktu). Poziomu akapitu w `eId`
się nie dodaje.

## Decyzja

**Przepis, który zaczyna się od „w odnośniku nr N" (także „… do tytułu ustawy"), ma za cel odnośnik (`odn_N`) — bez
względu na jednostki nazwane dalej; przepisy pod nim dziedziczą ten cel. Instrukcja jest ręczna, chyba że zmienia się
tekst całego odnośnika („w odnośniku nr 2 wyrazy „X” zastępuje się …" → `now:zamien-tekst` na `odn_2`).**

- Odczyt: `LegalReferenceService.ReadAmendmentTargets` — odnośnik nazwany jako miejsce albo odziedziczony z przepisu
  nadrzędnego.
- Instrukcja ręczna: `ManualTargets` ([ADR-0100](0100-cel-w-zalaczniku-bez-adresu-to-instrukcja-reczna.md)); odnośnik
  jako przedmiot komendy (cytowany odnośnik, ADR-0059) zostaje instrukcją automatyczną.
- Jednostka cytowana pod takim celem stoi pod adresem odnośnika (`…__odn_1__pkt_6`), nie jako jednostka aktu.

## Odrzucone alternatywy

- **Jednostka aktu o numerze z komendy** (stan sprzed decyzji). „w pkt 5" w odnośniku wskazywał punkt 5 aktu.
- **Poziom akapitu w `eId`** (`odn_1__akapit_2__pkt_5`). Ustalenie z 2026-10-19 go odrzuca; tabela `eId` takiego poziomu
  nie ma.

## Konsekwencje

- Adres jednostki cytowanej pod odnośnikiem (`odn_1__pkt_6`) dzieli los jednostek cytowanych pod celem w załączniku bez
  adresu — powód braku `eId` jest pytaniem 28 do autorów schematu.

## Weryfikacja

`ArchitectureDecisionTests.Adr0106_FootnoteAsThePlace_IsTheTarget`, `FootnoteAsPlaceTests`.
