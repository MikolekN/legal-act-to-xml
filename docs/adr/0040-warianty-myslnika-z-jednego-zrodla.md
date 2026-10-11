# ADR-0040: Warianty myślnika pochodzą z jednego źródła, a tekst nie jest normalizowany

- Status: Accepted
- Data: 2026-10-11

## Kontekst

Myślnik decyduje o strukturze w kilku miejscach: otwiera tiret („– treść"), otwiera część wspólną zamykającą
wyliczenie („– jeżeli są spełnione warunki …"), rozdziela przedział oznaczeń („art. 1–3") i symbol od
objaśnienia w legendzie wzoru. Każde z tych miejsc miało własny zestaw znaków — zwykle dywiz i półpauzę
(`[-–]`), czasem z pauzą.

Szczotki piszą ten znak także inaczej. W korpusie 61 tiretów i 85 części wspólnych zaczyna się myślnikiem
cyfrowym (U+2012, w edytorze nie do odróżnienia od półpauzy), a kilka — minusem (U+2212). Skutki:

- tiret pisany takim znakiem rozpoznawał tylko styl, a znak zostawał w jego treści; bez stylu nie był tiretem,
- część wspólna zaczynająca się od takiego myślnika nie była częścią wspólną — wychodziła jako `fragment`,
  a w 65 przypadkach ginęła z eksportu,
- przedział „art. 1‒3" nie był przedziałem.

To ta sama wada, którą dla cudzysłowów usunął [ADR-0030](0030-warianty-cudzyslowu-rozpoznaje-jedno-zrodlo.md).

## Decyzja

**Znaki myślnika są zdefiniowane raz — w `Services/Dashes.cs` — i każde miejsce, w którym myślnik decyduje
o strukturze, bierze je stamtąd. Tekst pozostaje nietknięty: eksport niesie znak ze źródła.**

- `Dashes.Any`: dywiz (U+002D), dywiz typograficzny i nierozdzielający (U+2010, U+2011), myślnik cyfrowy
  (U+2012), półpauza (U+2013), pauza (U+2014), kreska pozioma (U+2015), minus (U+2212).
- `Dashes.Long`: myślniki, które nie bywają częścią oznaczenia ani symbolu (bez dywizów i minusa) — dla
  legendy wzoru, gdzie „n-1" jest symbolem, a „–" rozdziela go od objaśnienia.
- Korzystają z nich: wzorzec tiretu i `TiretMarker`, rozpoznanie części wspólnej po myślniku, zdejmowanie
  myślnika z części wspólnej, sankcja po myślniku, znacznik wyliczenia w komórce tabeli, przedział oznaczeń
  w powołaniach (`UnitReferenceTokenizer`), `UnitDesignation` i `FormulaLegend`.
- Znaki są w kodzie zapisane kodami (`\u2012`), nie literałami — w edytorze trzy z nich wyglądają tak samo.

## Odrzucone alternatywy

- **Normalizacja przy wejściu** (każdy myślnik → półpauza). Najmniej kodu, ale eksport przestałby nieść znak
  ze źródła, a minus w treści („–5 °C", wzory) stałby się półpauzą. `StringExtensions.Sanitize` robi coś
  takiego dla jednej ścieżki i zostaje, czym jest — nie staje się regułą odczytu.
- **Dopisanie brakującego znaku w miejscu, w którym zabolało.** Tak powstały obecne zestawy: każdy inny.
- **Rozpoznanie po kategorii Unicode** (`\p{Pd}`). Obejmuje znaki, które myślnikiem redakcyjnym nie są
  (np. myślniki pism wschodnich), a nie obejmuje minusa (kategoria `Sm`), którym szczotki bywają pisane.

## Konsekwencje

- W korpusie zmienia się 36 dokumentów: 84 części wspólne i 17 tiretów więcej, znak znika z treści 67 tiretów
  (jest ich znacznikiem), 34 `fragment` mniej. Pokrycie tekstu: teksty jednolite 1136 → 1072 zgubionych
  akapitów. Zgodność bez stylów: 577 z 708 tekstów jednolitych i 200 z 242 ustaw w pełni zgodnych.
- Część wspólna niesie myślnik w treści tak jak dotąd („– jeżeli …"), także gdy jest to inny wariant.
- Wzorce artefaktów strony w adapterze PDF (`PageArtifactFilter`, `PdfBlockReader`) mają własne zestawy —
  dotyczą składu strony, nie struktury aktu, i nie da się ich zmierzyć na korpusie DOCX.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0040_DashCharacters_AreDefinedInExactlyOnePlace`
- `ArchitectureDecisionTests.Adr0040_EveryDashVariant_MarksATiret`
- `DashVariantTests` — tiret i jego głębokość dla każdego wariantu, tiret i część wspólna z myślnikiem
  cyfrowym, przedział oznaczeń.
