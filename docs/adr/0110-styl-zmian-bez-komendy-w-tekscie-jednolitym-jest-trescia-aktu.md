# ADR-0110: Akapit w stylu zmian (Z…) bez komendy nowelizacyjnej przed nim i bez cudzysłowu jest w tekście jednolitym treścią samego aktu; poza tekstem jednolitym — tylko oznaczenie i tytuł jednostki systematyzacyjnej

- Status: Accepted (rozszerza regułę `ParserOrchestrator.IsMisstyledActUnit` z jednostek systematyzacyjnych na całą treść tekstu jednolitego)
- Data: 2026-10-22

## Kontekst

Styl zmian (`Z/…`, `ZZ…`, `Z_…`) wprowadza `AmendmentStateManager.UpdateState` bezwarunkowo w nowelizację: akapit w tym
stylu jest treścią cytowaną. Teksty jednolite składają jednak stylami zmian jednostki, które do aktu dodała nowelizacja
— redaktor przenosi je z aktu zmieniającego razem ze stylem. Bez komendy przed takim akapitem akt ogłaszany zaczynał
„zmieniać sam siebie”: w tekście jednolitym rozporządzenia o właściwości organów podatkowych (Dz. U. 2026 poz. 152,
plik 352501) „§ 12.” w stylu artykułu nowelizacji znikał w cytacie pod § 11 ust. 2, w tekście jednolitym ustawy
o cenach paliw gazowych (plik 352078) legenda wzoru i wyliczenia miesięcy wychodziły 25 komendami „zastąp” na samym
akcie, a pominięte przepisy zmieniające „Art. 480–524. (pominięte)” (plik 330983) przepadały w pustym oddziale.
Dotychczasowy wyjątek obejmował tylko oznaczenie i tytuł jednostki systematyzacyjnej w stylu Z.

## Decyzja

W tekście jednolitym (`DocumentClassificationResult.IsConsolidatedText`) akapit w stylu zmian, przed którym nie ma
komendy nowelizacyjnej (`AmendmentTriggerDetected`), który nie stoi w trwającej nowelizacji ani w zbieranym cytacie
i nie zaczyna się cudzysłowem, jest treścią samego aktu: klasyfikacja traci znacznik treści cytowanej, a rodzaj
jednostki rozstrzyga treść. Przepis zmieniający zachowany w tekście jednolitym ma komendę, więc jego cytat zostaje
cytatem. W innych aktach reguła obejmuje — jak dotąd — tylko oznaczenie i tytuł jednostki systematyzacyjnej.

## Odrzucone alternatywy

- **Ta sama reguła w każdym akcie.** W akcie zmieniającym styl Z bywa jedynym sygnałem cytatu: komenda, której wzorce
  nie rozpoznały („wprowadza się zmianę polegającą na nadaniu § 2 nowej treści, jak niżej”), nie ustawia wyzwalacza,
  a treść w stylu Z bez cudzysłowu trafiłaby do struktury aktu zmieniającego jako jego własne jednostki. W tekście
  jednolitym takiego ryzyka nie ma — akt nie zmienia sam siebie, a przepisy zmieniające innych aktów stoją w nim
  z komendą albo jako „(pominięte)”.
- **Wyjątki po rodzaju jednostki** (jak dotąd: tylko jednostka systematyzacyjna; dołożyć artykuł i paragraf). Wzór,
  legenda, część wspólna i wyliczenie w legendzie też przychodzą w stylach Z i też nie są cytatem; lista rodzajów
  rosłaby z każdym dokumentem, a granicę wyznacza brak komendy, nie rodzaj akapitu.
- **Zostawić styl Z i oczyścić cytat po fakcie** (komenda „zastąp” bez celu na samym akcie → treść aktu). Cytat zbiera
  już treść do końca nowelizacji i łamie strukturę (§ 12 pod § 11 ust. 2); naprawa po finalizacji musiałaby ją
  odtwarzać.

## Konsekwencje

- Pominięte przepisy zmieniające w stylu Z wracają do tekstu jednolitego jako jednostki aktu (330983: zakres
  artykułów `nr-od`/`nr-do`, który wyprzedza dzisiejszą kopię schematu — ADR-0097).
- W tekście jednolitym komenda, której wzorce nie rozpoznają, zostawi cytowaną treść w stylu Z jako treść aktu.
  Korpus DOCX takiego przypadku nie ma; gdy się pojawi, naprawą jest wzorzec komendy, nie styl.
- Korpus DOCX: 8 tekstów jednolitych zmienionych, ocena 2176 → 2179 z 2321, żaden dokument gorzej.

## Weryfikacja

- `AmendmentStyledActContentTests.BasicUnitInAmendmentStyle_InAConsolidatedText_IsAUnitOfTheAct` i
  `AmendmentStyledActContentTests.FormulaLegendInAmendmentStyles_InAConsolidatedText_StaysInTheProvision` — spadną,
  gdy styl Z znów otworzy nowelizację w tekście jednolitym bez komendy.
- `AmendmentStyledActContentTests.BasicUnitInAmendmentStyle_OutsideAConsolidatedText_StaysTheQuotedContent` — spadnie,
  gdy regułę rozszerzy się na akty zmieniające.
- `AmendmentStyledActContentTests.AmendmentStyleAfterACommand_InAConsolidatedText_IsStillTheAmendment` — spadnie, gdy
  reguła zacznie łamać cytat przepisu zmieniającego zachowanego w tekście jednolitym.
