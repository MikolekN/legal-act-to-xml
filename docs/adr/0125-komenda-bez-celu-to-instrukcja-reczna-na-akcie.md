# ADR-0125: Komenda bez celu to instrukcja ręczna (`*-recznie`) na samym akcie zmienianym; bez odczytanego aktu — bez `element-eId`

- Status: Accepted — Supersedes [ADR-0101](0101-tytul-aktu-jako-cel-to-instrukcja-reczna-z-adresem-aktu.md) w części o celu bez odczytanego aktu
- Data: 2026-10-28

## Kontekst

Komenda dodająca podaje miejsce: „po art. X dodaje się art. Xa” (§ 89 ust. 1 i 4 ZTP) albo „w (jednostce) dodaje się …”
(§ 89 ust. 2, § 89a). Część aktów pisze samo „dodaje się art. 37a w brzmieniu:” pod formułą „W ustawie … wprowadza się
następujące zmiany:”. Jednostka za „dodaje się” jest dodawana, nie wskazywana
([ADR-0055](0055-dodanie-wskazuje-miejsce-nie-jednostke-dodawana.md)), więc nowelizacja nie ma celu, a finalizator
ostrzega. Eksport dawał automatyczne `dodaj-po` z `element-eId="UNKNOWN"`: w korpusie DOCX 4 komendy (334848, 338276,
354686, 354692), w aktach ISAP 1 (Dz. U. 2026 poz. 1347 art. 10 pkt 1 — cytowane „– dodaje się pkt 4”, bez miejsca i bez
aktu). Tą samą drogą szłyby uchylenie, zmiana brzmienia i zmiana w tekście bez miejsca. Autorzy schematu: cel bez adresu
wskazuje najgłębsza adresowalna jednostka „albo sam akt”, a instrukcja jest ręczna (zasada 4, 2026-10-18); automatycznie
tylko to, co wynika z samego aktu zmieniającego (zasada 6, 2026-10-20).

## Decyzja

- **Komenda, której cel nie ma adresu** — ani przepis, ani przepisy nad nim nie wskazują miejsca, albo z miejsca nie
  powstał żaden segment `eId` — **jest instrukcją ręczną**: dodanie → `dodaj-recznie`, uchylenie → `uchyl-recznie`
  (cytat z przedmiotem uchylenia z tekstu zdania, nigdy `now:uchyl` na akcie), zmiana brzmienia i zmiana w tekście →
  `zastap-recznie`. Treść cytatu i adresy jednostek cytowanych bez zmian.
- **`element-eId` to identyfikator aktu zmienianego** („DU/2023/1329”), ten sam co w `now:akt`, jak w ADR-0101. Aktu
  nieodczytanego się nie zgaduje: wtedy instrukcja nie ma `element-eId` — także dla tytułu aktu.
- Rozstrzyga brak adresu celu (`AmendmentXmlConverter.Command.Address`), nie brak atrybutu: pierwszy załącznik dodawany
  odesłaniem (`now:dodaj-po` bez `element-eId`, ADR-0058) zostaje. Eksport nie pisze `UNKNOWN`.

## Odrzucone alternatywy

- **`UNKNOWN` z funkcją automatyczną** (dotychczas). To nie adres: „dodaj po UNKNOWN” jest niewykonalne.
- **Miejsce wyprowadzone z numeru według § 89** („37a” → po art. 37, „12aa” → po art. 12a), jak dla załączników
  w ADR-0058. Załączniki to płaski ciąg; artykuły stoją w jednostkach systematyzacyjnych, a akt, który pominął miejsce,
  mógł pominąć i „w rozdziale N” (§ 89a). Poprzednik zależy od numeracji aktu zmienianego, której akt zmieniający nie
  pokazuje (w 3 z 4 aktów poprzednik nie pada), a błąd automatu przeszedłby po cichu (zasada 6).
- **Bez `element-eId` także przy znanym akcie.** Rozjechałoby się z ADR-0101 i z literą zasady 4; to zapasowa odpowiedź,
  gdyby autorzy schematu odrzucili sam akt w `element-eId` (pytanie 51) — przełącznik jest jeden
  (`AmendmentXmlConverter.AddTargetAttributes`).

## Konsekwencje

- Korpus DOCX: 4 instrukcje `dodaj-po`/`UNKNOWN` stają się `dodaj-recznie` z adresem aktu; 171 aktów ISAP: 1 —
  `dodaj-recznie` bez `element-eId`. Zgodność ze schematem bez zmian.
- `UNKNOWN` przestaje sygnalizować nieodczytany cel; miarą zostaje ostrzeżenie finalizatora i liczba instrukcji ręcznych
  na samym akcie poza tytułem.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0125_CommandWithoutATarget_IsAManualInstructionOnTheAct`
- `AmendmentTargetsExportTests.AdditionWithoutAPlace_IsAManualInstructionOnTheAmendedAct`,
  `AmendmentTargetsExportTests.CommandWithoutATarget_GetsTheManualFunctionOfItsOperation`,
  `AmendmentTargetsExportTests.CommandWithoutATargetOrAct_IsAManualInstructionWithoutAnAddress`
