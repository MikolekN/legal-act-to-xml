# ADR-0074: Przecinek po punktach zapowiada część wspólną punktów — myślnik za ostatnią literą zamyka wtedy punkty

- Status: Accepted (pozycja zakończona spójnikiem — „… lub", „… oraz" — prowadzi wyliczenie dalej i nie poprzedza części wspólnej: `CommonPartPlacement.ClosesBeforeDash`, strażnik `Adr0074_TiretEndingWithAConjunction_IsFollowedByATiret_NotByTheCommonPartOfThePoints`)
- Data: 2026-10-14

## Kontekst

Akapit bez stylu zaczynający się od myślnika tuż za pozycją wyliczenia bez znaku końcowego jest częścią wspólną
(§ 57 ust. 3 ZTP; `StructureProcessor.TryHandleStylelessWrapUp`, `AmendmentBuilder.TryBuildStylelessCommonPart`).
Gdy ostatni punkt ma litery, myślnik za jego ostatnią literą trafiał zawsze do liter tego punktu — także wtedy, gdy
zamyka całe wyliczenie punktów. Bez stylów to najczęstsza pomyłka w strukturze tekstów jednolitych: 88 części
wspólnych punktów w 65 dokumentach wychodziło jako część wspólna liter.

ZTP rozróżnia oba przypadki interpunkcją: „Każdy punkt kończy się średnikiem, a ostatni kropką, chyba że wyliczenie
kończy się częścią wspólną odnoszącą się do wszystkich punktów. W takim przypadku (…) każdy punkt, z wyjątkiem
ostatniego, kończy się przecinkiem" (§ 56 ust. 3). Litery i tirety kończą się przecinkiem zawsze — ich interpunkcja
tego nie rozstrzyga.

## Decyzja

**Myślnik za ostatnią pozycją ostatniego punktu (jego ostatnią literą albo tiret), bez znaku końcowego, jest częścią
wspólną punktów, gdy każdy punkt przed ostatnim kończy się przecinkiem** (`CommonPartPlacement.PointsCloseWithCommonPart`;
punkt z literami kończy się tam, gdzie jego ostatnia litera albo jej ostatnie tiret). Inaczej zostaje częścią wspólną
liter, jak dotąd. Ta sama reguła w treści aktu i w treści cytowanej.

Gdy po takiej części wspólnej przychodzi kolejny punkt, zamykała ona litery — przenosi ją tam
`CommonPartPlacement.BeforeNextItem`, jak każdą część wspólną dołączoną o poziom za wysoko.

## Odrzucone alternatywy

- **Wcięcie akapitu.** Rozróżnia poziomy w DOCX bez szablonu, ale nie w PDF i TXT; interpunkcja jest w tekście.
- **Zawsze część wspólna punktów.** Myślnik za literami punktu zakończonego średnikiem zamyka litery — punkty bez
  części wspólnej kończą się średnikiem.

## Konsekwencje

- Bez stylów: w pełni zgodnych tekstów jednolitych jest 625 z 728 (było 599), ustaw 210 z 242 (było 207); części wspólnych punktów gubionych bez stylów jest 281 (było 349), nadmiarowych części wspólnych liter — 39 (było 104). Ze stylami zmienia się 6 plików: 7 myślników bez stylu za ostatnią literą wychodzi jako część wspólna punktów, nie liter. Zgodnych ze schematem jest nadal 2278 z 2321.
- Akt, który nie przestrzega § 56 ust. 3 (punkty ze średnikiem przed częścią wspólną punktów), nadal daje część
  wspólną liter; rozstrzyga wtedy tylko styl.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0074_DashAfterTheLastLetter_ClosesThePoints_WhenThePointsEndWithCommas`
- `ParserOrchestratorCommonPartTests` — przecinek i średnik po punktach, część wspólna za literami środkowego punktu;
  `CommonPartPlacementTests.QuotedDashAfterTheLettersOfTheLastPoint_ClosesThePoints_WhenPointsEndWithCommas`.
