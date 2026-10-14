# ADR-0078: Wyliczenie w odnośniku to jego jednostki — granice wyznacza treść, nie styl akapitu przypisu

- Status: Accepted
- Data: 2026-10-14

## Kontekst

Schemat daje treści odnośnika (grupa `tresc-odnosnika` — w `odnosnik` i w cytowanym `odnosnik-cyt`) budowę: pierwszy
`p`, za nim `punkt-odnosnik` z `nr`, `czesc-wspolna-punkt-odnosnik` i `fragment-odnosnik`; w punkcie `litera-odnosnik`
i część wspólna liter, w literze `tiret-odnosnik` i część wspólna tiretów. Eksport składał wszystkie akapity przypisu
w jeden `p` — punkt odnośnika („1)⇥służy stosowaniu rozporządzenia …;") wychodził jako kolejne zdanie z oznaczeniem
i tabulatorem w tekście. Wyliczenie stoi zwykle w odnośniku do tytułu („Niniejsza ustawa służy stosowaniu: 1) …;
2) …"). Akapity przypisu mają w szablonie style (`PKT_ODNOŚNIKA`, `LIT_ODNOŚNIKA`, `CZ_WSP_PKT_ODNOŚNIKA`), ale
w stylu części wspólnej stoi też wiersz „Dane dotyczące ogłoszenia aktów prawa Unii Europejskiej …" — nowe zdanie,
nie część wspólna — a adapter DOCX przenosi treść przypisu bez stylów (`BlockFootnote`).

## Decyzja

**Akapity za pierwszym akapitem odnośnika to jego jednostki, czytane z treści tymi samymi wzorcami co jednostki
aktu (`FootnoteContent.Read`): „1)" — punkt, „a)" — litera punktu, myślnik pod pozycją zakończoną dwukropkiem albo
za tiretem — tiret litery; myślnik za pozycją bez znaku końcowego i akapit od małej litery — część wspólna
wyliczenia, które zamykają (jak w treści aktu, [ADR-0074](0074-przecinek-po-punktach-zapowiada-czesc-wspolna-punktow.md)); akapit od wielkiej litery bez
oznaczenia — `fragment-odnosnik`. Wprowadzenie i jego pozycje to fragmenty jednego zdania, jak w treści aktu
(`FootnoteContentXml`).**

- Odnośnik aktu i odnośnik cytowany ([ADR-0059](0059-odnosnik-jako-przedmiot-komendy-jest-cytowanym-odnosnikiem.md)) dzielą ten odczyt.
- Wyliczenia, którego jednostki odnośnika nie wyrażą — litera bez punktu, tiret poza literą, akapit od małej litery
  bez wyliczenia przed sobą, numer spoza wzoru `nr` — nie dzieli się: treść zostaje jednym `p`, jak dotąd.
- Jednostki odnośnika nie mają eId ani GUID — adresuje się cały odnośnik.

## Odrzucone alternatywy

- **Jeden `p`, kolejne akapity jako jego zdania** (stan dotychczasowy). Punkt odnośnika był zdaniem z oznaczeniem
  w tekście, choć schemat ma dla niego jednostkę z `nr`.
- **Styl akapitu przypisu jako sygnał granic** (`CZ_WSP_PKT_ODNOŚNIKA` → część wspólna). Wiersz o ogłoszeniu aktów Unii
  stoi w tym stylu, a zaczyna nowe zdanie; przy sporze stylu z treścią rozstrzyga treść.
- **Jednostki odnośnika w modelu (`Footnote`)**. Odczyt jest funkcją akapitów, którą eksport woła dla obu rodzajów
  odnośnika; model trzyma treść źródła. Gdy jednostki odnośnika będą potrzebne poza eksportem, odczyt przejdzie do
  modelu bez zmiany reguł.
- **Tiret wprost w odnośniku albo w punkcie jako `tiret-odnosnik`.** Schemat stawia tiret odnośnika tylko w literze;
  takie wyliczenie zostaje akapitem (pytanie do autorów schematu w backlogu).

## Konsekwencje

- W korpusie zmienia się 147 dokumentów: 738 `punkt-odnosnik` (122 dokumenty), 64 `litera-odnosnik` (11), 32 `czesc-wspolna-punkt-odnosnik` (6) i 106 `fragment-odnosnik` (70) — przedtem zdania jednego `p` z oznaczeniem i tabulatorem w tekście. Zgodnych ze schematem jest nadal 2278 z 2321 (oba walidatory).
- Bez stylu granica części wspólnej zależy od znaku końcowego pozycji — tak samo jak w treści aktu.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0078_EnumerationInAFootnote_IsItsUnits_AndTheContentDecidesWhereItEnds`
- `FootnoteEnumerationTests` — odczyt, eksport, fragmenty jednego zdania, zgodność ze schematem;
  `QuotedFootnoteTests.EnumerationInsideAQuotedFootnote_IsExportedAsPointsOfThatFootnote`.
