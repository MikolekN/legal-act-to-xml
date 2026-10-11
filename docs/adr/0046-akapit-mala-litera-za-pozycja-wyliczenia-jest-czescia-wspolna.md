# ADR-0046: Akapit bez oznaczenia zaczynający się małą literą tuż za pozycją wyliczenia zakończoną przecinkiem albo bez znaku jest częścią wspólną

- Status: Accepted
- Data: 2026-10-11

## Kontekst

Część wspólną zamykającą wyliczenie rozpoznawały dotąd dwa sygnały: styl akapitu (`CZ_WSP_*`) albo myślnik
na początku po pozycji bez znaku końcowego (§ 57 ust. 3 ZTP). Starsze akty piszą ją inaczej — pozycje kończą
się przecinkami, a część wspólna stoi bez myślnika:

    W przypadku:
    1) zaniechania użytkowania gruntów,
    2) niewykonania rekultywacji gruntów w wyznaczonym terminie,
    stosuje się przepisy niniejszej ustawy.

Bez stylu taki akapit był dalszą treścią ostatniej pozycji (ADR-0029) — zdanie wprowadzenia zostawało
niedomknięte, a jego orzeczenie przyklejone do punktu 2. Dokument bez szablonu, plik tekstowy i PDF nie mają
stylu, który by to rozstrzygnął; w korpusie tak zapisanych części wspólnych jest ok. 160.

## Decyzja

**Akapit bez oznaczenia i bez myślnika, zaczynający się małą literą, stojący tuż za pozycją wyliczenia
zakończoną przecinkiem albo bez znaku końcowego, zamyka to wyliczenie jako część wspólna**
(`StructureProcessor.TryCloseEnumerationWithoutDash`).

- Mała litera na początku akapitu znaczy dalszy ciąg zdania, nie nowe zdanie.
- Pozycja zakończona przecinkiem (albo bez znaku) jest domkniętym członem wyliczenia — jej własne zdanie nie
  ma czego ciągnąć, więc akapit ciągnie zdanie wprowadzenia.
- „Tuż za" znaczy: pozycja nie ma jeszcze dalszej treści (wzoru, tabeli, akapitu).
- Po pozycji zakończonej średnikiem, kropką albo dwukropkiem reguła nie działa; akapit zostaje dalszą treścią
  pozycji (ADR-0029). Uściśla to ADR-0029, nie zastępuje go.

## Odrzucone alternatywy

- **Zostawić styl jedynym sygnałem.** Rozpoznanie zależałoby od szablonu edytora; ten sam akt w PDF albo bez
  stylów miałby inną strukturę.
- **Każdy akapit małą literą za pozycją** (także po średniku i kropce). Po średniku akapit bywa dopiskiem do
  jednej pozycji, a po kropce — osobnym zdaniem zaczętym małą literą przez pomyłkę; tu sygnał jest jeden.
- **Rozstrzygnięcie poziomu wcięciem** (część wspólna liter czy punktów). Wcięcia zależą od szablonu; ich progi
  byłyby dopasowaniem do jednego źródła. Poziom rozstrzyga miejsce: akapit zamyka najgłębsze otwarte wyliczenie,
  a pomyłkę o poziom koryguje `CommonPartPlacement` przy następnej pozycji.

## Konsekwencje

- Ze stylami zmienia się 10 dokumentów korpusu: 11 akapitów wychodzi jako część wspólna zamiast `fragment`
  pod ostatnią pozycją (akapity bez stylu części wspólnej). Bez stylów w pełni zgodnych z wynikiem stylowym
  jest 591 zamiast 562 tekstów jednolitych.
- Poziom części wspólnej za wyliczeniem zagnieżdżonym bywa bez stylu inny niż ze stylem (ok. 100 przypadków:
  część wspólna liter zamiast punktów) — bez stylu i bez wcięcia nie da się tego odróżnić.
- Akapit, który rzeczywiście ciągnie ostatnią pozycję po przecinku i zaczyna się małą literą (pozycja złamana
  na dwa akapity), zostanie częścią wspólną. Źródła łamią tak pozycję rzadko; tekst w obu odczytach zostaje.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0046_LowercaseParagraphRightAfterAnItem_ClosesTheEnumeration`
- `CommonPartWithoutDashTests` — po przecinku i bez znaku, po literach, wielka litera, średnik i kropka.
