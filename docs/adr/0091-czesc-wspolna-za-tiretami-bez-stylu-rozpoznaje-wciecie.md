# ADR-0091: Część wspólną za tiretami bez stylu rozpoznaje wcięcie

- Status: Accepted
- Data: 2026-10-15

## Kontekst

Akapit zaczynający się od myślnika za ostatnim tiretem bywa kolejnym tiretem albo częścią wspólną — tiretów (zamyka
tirety, należy do litery) albo liter (zamyka litery, należy do punktu). W DOCX z szablonem rozstrzyga styl
(`TIR`, `CZ_WSP_TIR`, `CZ_WSP_LIT`). Bez stylu (PDF, DOCX bez szablonu, TXT) parser zostawiał myślnik za tiretem
tiretem — świadomie, bo tekst tego nie rozstrzyga: w korpusie DOCX za tiretem bez znaku końcowego myślnik jest
kolejnym tiretem 341 razy, a częścią wspólną 58 razy (§ 57 ust. 3 ZTP pozwala elementowi bez znaku końcowego stać
także przed kolejnym elementem, gdy wyliczenie tak napisano). Skład Dziennika Ustaw rozstrzyga to jednak położeniem:
tirety stoją 862 twips od lewego marginesu, część wspólna tiretów 782, litery i część wspólna liter 422.

## Decyzja

W akapicie bez stylu, z układem (`BlockLayoutInfo.LeftIndentTwips`), myślnik za tiretem bez znaku końcowego, wcięty
co najmniej 40 twips mniej niż ten tiret, jest częścią wspólną: liter, gdy stoi na wcięciu bieżącej litery (z tą samą
tolerancją), w przeciwnym razie tiretów. Bez układu (TXT) albo z wcięciem tiretu zostaje tiretem, jak dotąd.

## Odrzucone alternatywy

**Rozstrzygać z samego tekstu** (myślnik po tirecie bez znaku końcowego = część wspólna). Odrzucone: korpus pokazuje,
że to częściej kolejny tiret (341 do 58) — reguła psułaby więcej, niż naprawia.

**Użyć tolerancji wcięcia tiretów tego samego poziomu** (`TiretIndentTolerance`, 120 twips). Odrzucone: część wspólna
tiretów stoi tylko 80 twips przed tiretami, a tolerancja tiretów musi znosić rozjazd akapitów jednego poziomu; próg
części wspólnej jest osobny i mniejszy.

**Działać także na akapitach ze stylem.** Odrzucone: styl szablonu rozstrzyga już wprost (ADR-0002 — warstwa stylu
przed treścią i układem), a reguła ma nie zmieniać wyniku DOCX z szablonem.

## Konsekwencje

- PDF i DOCX bez szablonu dostają części wspólne tiretów i liter, które dotąd były kolejnymi tiretami.
- TXT bez zmian — nie ma układu; tam myślnik za tiretem dalej zostaje tiretem.
- Wcięcie DOCX pochodzi tylko z bezpośredniego `w:left` akapitu (nie ze stylu), więc w DOCX bez szablonu reguła
  działa wtedy, gdy autor wciął akapit bezpośrednio.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0091_DashIndentedBeforeTheTirets_IsTheirCommonPart_OnlyWithLayoutAndWithoutStyle`
