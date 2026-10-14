# ADR-0071: Treść pod przepisem, który niczego nie zmienia, jest treścią tego przepisu — tekst w cudzysłowie to przytoczenie (`tresc-strukt`)

- Status: Accepted
- Data: 2026-10-14

## Kontekst

Treść cytowaną zbiera ten sam mechanizm, który zbiera treść nowelizacji: akapity w stylu zmiany (`Z…`) albo po zwrocie
„w brzmieniu:". Szablon aktów używa tych stylów (i stylu cytatu `CYT`) także dla tekstu, który nowelizacją nie jest:
oświadczenia („pisemne oświadczenie następującej treści: „Oświadczam, że: 1) … 2) …"") , tekstu dołączanego do
protokołu, wzorów i ich objaśnień w tekstach jednolitych.

Taka treść stawała się nowelizacją przepisu, który niczego nie zmienia: `cytat-strukt funkcja="zastap"` albo
`cytat-w-tekscie funkcja="zastap-tekst"` z celem `UNKNOWN` albo jednostką tylko powołaną w przepisie. Wiersz
otwierający cytat w stylu `CYT` lądował za zdaniem jako `fragment` (kolejność odwrócona), a cudzysłów zamykający
wyraz w ostatnim wierszu („klucz ogólny do działania „n",") był ucinany jak koniec cytatu.

Schemat ma na to element: `tresc-strukt` — „ustrukturyzowane elementy tekstu objęte cudzysłowem np. tekst ślubowania".
Przykład autorów schematu (`przyklady_kancelaria_sejmu.xml`): „Składa się następujące ślubowanie:" i w tym zdaniu
`tresc-strukt cudzyslowy="oba"` z akapitem i punktami bez eId.

## Decyzja

**Zebrana treść jest nowelizacją tylko pod przepisem, który coś zmienia** (`AmendmentStateManager.AmendsAnything`):
stoi w artykule zmieniającym inny akt (`Article.IsAmending`), czekają na niego zmiany odczytane z jego tekstu, albo on
sam lub przepis nad nim (do artykułu) niesie czasownik komendy — „otrzymuje brzmienie", „dodaje się", „zastępuje się",
„oznacza się jako", „wprowadza się zmiany" albo czasownik uchylenia ([ADR-0069](0069-czasownik-uchylenia-jest-komenda-w-artykule-zmieniajacym-albo-przy-jednostce.md))
(`AmendmentCommandParser.HasChangeVerb`).

**Pod przepisem, który niczego nie zmienia, treść należy do niego** (`AmendmentStateManager.TryKeepAsContentOfTheProvision`):

- **tekst w cudzysłowie** to przytoczenie — `ContentBlock.Quotation` w dalszej treści przepisu (ten sam model co
  przytoczenia obwieszczenia, z `Quotation.HasOpeningQuote` / `HasClosingQuote`). W eksporcie: `tresc-strukt`
  w ostatnim zdaniu przepisu, z treścią złożoną jak cytat nowelizacji, ale bez atrybutów instrukcji i bez eId —
  przytoczony tekst nie jest jednostką żadnego aktu (`QuotationXmlConverter`);
- **wiersz przepisu, który otwiera cudzysłów niedomknięty przed treścią** („„Oświadczam, że:"), należy do
  przytoczenia — rozstrzyga para cudzysłowów, nie styl;
- **cudzysłów na końcu ostatniego wiersza, otwarty w tym samym wierszu**, cytatu nie zamyka i zostaje w tekście;
- **tekst bez cudzysłowu i bez jednostek** to dalsza treść przepisu (`fragment`, wzór z objaśnieniem), w kolejności
  z aktu — jak akapit bez stylu zmiany ([ADR-0029](0029-akapit-bez-oznaczenia-jest-dalsza-trescia-jednostki.md));
- **jednostki bez cudzysłowu** zostają, czym były: to najpewniej jednostki aktu złożone stylem zmiany (ust. 12 w stylu
  zmiany ustępu), a to osobna decyzja.

## Odrzucone alternatywy

- **Rozstrzygać stylem (`CYT` = cytat, `Z…` = nowelizacja).** Style `Z…` niosą i nowelizacje, i przytoczenia; styl
  jest wskazówką, nie pewnikiem. O nowelizacji mówi przepis — jego miejsce i czasownik.
- **`cytat-strukt` bez atrybutów instrukcji.** Typ cytatu strukturalnego służy do tworzenia tekstu nowelizacji
  i niesie atrybuty instrukcji; przytoczenie ma w schemacie własny element.
- **Zachować eId jednostek przytoczenia.** Adres w akcie byłby fałszywy — przytoczony tekst nie należy do żadnego aktu
  (przykład autorów schematu też ich nie nadaje).
- **Jednostki bez cudzysłowu jako `tresc-strukt cudzyslowy="brak"`.** Ukryłoby jednostki aktu złożone stylem zmiany
  i odebrało im adres.

## Konsekwencje

- W korpusie zmienia się 20 plików, wszystkie to teksty jednolite: znika 49 fałszywych cytatów nowelizacji (20 `cytat-strukt`, 29 `cytat-w-tekscie`), przybywa 30 `tresc-strukt` (13 z nich ma tylko cudzysłów otwierający — klauzula „Świadomy odpowiedzialności karnej…" bez wykrytego zamknięcia), 13 wierszy otwierających („Oświadczam, że:") przechodzi z `fragment` do swojego przytoczenia, a wzory i objaśnienia złożone stylem zmiany stają się dalszą treścią (+20 `fragment`, 5 `wzor-mat` z 21 opisami symboli). Zgodnych ze schematem jest nadal 1946 z 2321 (oba walidatory), pokrycie tekstu bez zmian.
- Zostaje 7 cytatów nowelizacji w 6 tekstach jednolitych: jednostki bez cudzysłowu w stylu zmiany pod przepisem bez
  komendy — `docs/backlog.md`.
- Przytoczenie stoi w ostatnim zdaniu przepisu; akapit bez stylu zmiany stojący przed nim w akcie (dalsza treść) wychodzi
  za zdaniem — schemat nie daje miejsca na przytoczenie w `fragment`.
- Czasownik komendy w zwykłym znaczeniu („do kwoty dodaje się odsetki") nadal czyni przepis zmieniającym — treść pod nim
  zostaje nowelizacją, jak dotąd.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0071_TextUnderAProvisionThatAmendsNothing_IsContentOfTheProvision`
- `QuotedTextInProvisionTests` — oświadczenie z wierszem otwierającym (model, eksport, zgodność ze schematem),
  cudzysłów niedomknięty, cudzysłów sparowany w ostatnim wierszu, kontrprzykład pod przepisem zmieniającym;
  `UndesignatedUnitTests.LinesInAmendmentStyle_UnderAProvisionThatAmendsNothing_AreItsFurtherContent_InOrder`.
