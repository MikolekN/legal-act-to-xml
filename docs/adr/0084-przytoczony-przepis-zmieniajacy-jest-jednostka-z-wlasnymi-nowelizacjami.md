# ADR-0084: Przytoczony przepis zmieniający jest jednostką z własnymi nowelizacjami, a wiersze cudzysłowu otwartego w środku akapitu są tekstem tego akapitu

- Status: Accepted — Supersedes [ADR-0036](0036-przytoczone-artykuly-sa-jednostkami-aktu-przytaczanego.md) w części o przytoczonym przepisie zmieniającym z własnym cytatem
- Data: 2026-10-14

## Kontekst

[ADR-0036](0036-przytoczone-artykuly-sa-jednostkami-aktu-przytaczanego.md) buduje przytoczenie w obwieszczeniu jako jednostki tylko wtedy, gdy żaden akapit poza
pierwszym nie otwiera własnego cudzysłowu: budowniczy treści cytowanej był płaski, więc cytat w przytoczonym przepisie
zmieniającym („Art. 94. W ustawie … 1) w art. 10f ust. 5 otrzymuje brzmienie: „5. …"") stawałby się jednostkami
przytaczanego artykułu. Odrzucona tam alternatywa czekała na nowelizacje zagnieżdżone — dziś je mamy
([ADR-0082](0082-cytat-zagniezdzony-jest-nowelizacja-cytowanego-przepisu.md)). W korpusie zostawało pod jednostkami obwieszczeń 3836 akapitów `fragment`
w 90 dokumentach, z czego ok. 3100 w 19 obwieszczeniach przytaczających całe przepisy zmieniające („art. 94–130
ustawy …, które stanowią:").

Dwie rzeczy stawały przy tym na drodze. Komendy w przytoczeniach mają potknięcia redakcyjne: „pkt 1 otrzymuje
brzmienie" bez dwukropka, „otrzymuje brzmienie;", „dodaje się pkt 1b brzmieniu:". A cudzysłów otwarty w środku
akapitu i domknięty kilka wierszy dalej — nowe wyrazy rozpisane na wiersze („zastępuje się wyrazami „…:", „1) do dnia
15 lipca – w 2024 r.;", „2) … r”,") albo oświadczenie „o następującej treści: „Oświadczam, że: 1) …; 2) …”" — czytał
budowniczy jako jednostki cytatu: litera dostawała „punkty" spoza ZTP, a ustęp drugi „pkt 1".

## Decyzja

**Przytoczenie, w którym cudzysłów otwiera się w dalszym akapicie, buduje się jednostkami, gdy każdy taki cudzysłów
stoi tuż za komendą cytowanego przepisu (`NestedQuotes.Split`): jest wtedy nowelizacją tego przepisu
([ADR-0082](0082-cytat-zagniezdzony-jest-nowelizacja-cytowanego-przepisu.md)) i wychodzi w jego zdaniu, wewnątrz `tresc-strukt`. Cudzysłów bez komendy przed sobą
(rota, wzór pisma) zostawia całe przytoczenie tekstem — jak dotąd. W treści cytowanej — przytoczenia i nowelizacji —
wiersze cudzysłowu otwartego w środku akapitu i domkniętego w którymś z następnych są dalszą treścią jednostki tego
akapitu, nie jednostkami.**

- Komendę kończącą akapit rozpoznaje się mimo potknięć: bez dwukropka, ze średnikiem, „brzmieniu" bez „w",
  „w następującym brzmieniu". Drugim sygnałem jest cudzysłów otwierający następny akapit.
- Jednostki przytoczonego przepisu mają adres w akcie przytaczanym ([ADR-0036](0036-przytoczone-artykuly-sa-jednostkami-aktu-przytaczanego.md)), a jego nowelizacje —
  cel i akt zmieniany czytane z cytowanego artykułu ([ADR-0082](0082-cytat-zagniezdzony-jest-nowelizacja-cytowanego-przepisu.md)).
- Cudzysłów niedomknięty do końca treści (literówka źródła) nie bierze dalszych wierszy — zostają jednostkami, jak
  dotąd.
- Reszta ADR-0036 bez zmian: wszystko albo nic, akt przytaczany z treści jednostki obwieszczenia.

## Odrzucone alternatywy

- **Przepis zmieniający z cytatem zostaje tekstem** (ADR-0036). Ponad 3 tys. akapitów bez jednostek, choć to zwykłe
  przepisy zmieniające — te same, które parser buduje w ogłoszonej ustawie zmieniającej. Ten sam przepis wychodziłby
  różnie zależnie od tego, gdzie stoi.
- **Nowelizacje w przytoczeniu bez instrukcji** — sama treść cytatu, jak przytoczenie w jednostce aktu
  ([ADR-0071](0071-tresc-pod-przepisem-ktory-niczego-nie-zmienia-jest-jego-trescia.md)). Przytoczony przepis zmieniający jest przepisem, nie tekstem cytowanym jak rota: jego
  komendy mają cel i akt zmieniany, a schemat dopuszcza `cytat-strukt` w zdaniu jednostki `tresc-strukt`.
- **Wiersze nowych wyrazów jako treść zmiany w tekście** (`nowy-tekst` na kilka wierszy). Atrybut nie niesie podziału
  na wiersze; to osobna decyzja (backlog). Tekst jednostki zachowuje każdy wiersz i nie udaje jednostek.

## Konsekwencje

- W korpusie zmienia się 23 dokumenty. W 19 obwieszczeniach tekstu jednolitego przytoczenia przepisów zmieniających są jednostkami: 3102 akapity przestają być `fragment` (pod jednostkami obwieszczeń zostaje 734 w 72 dokumentach, przedtem 3836 w 90), a w przytoczonych przepisach staje 596 `cytat-strukt`, 33 `cytat-w-tekscie`, 152 `now:zamien-tekst` i 41 `now:uchyl`. W 4 aktach 30 wierszy cytowanych oświadczeń i wzorów przestaje być powtórzonymi jednostkami cytatu (drugi „pkt 1") i zostaje tekstem jednostki, która je przytacza. Zgodnych ze schematem nadal 2279 z 2321 (oba walidatory, ten sam zbiór); pokrycie tekstu i zgodność bez stylów bez zmian (tyle samo brakujących i nadmiarowych jednostek).
- Cytowane w treści cytowanej oświadczenia i wzory są tekstem jednostki, nie `tresc-strukt` jak w jednostce aktu —
  następny krok (backlog).

## Weryfikacja

- `ArchitectureDecisionTests.Adr0084_QuotedAmendingProvision_IsAUnitWithItsOwnAmendments`
- `AnnouncementTests.QuotedAmendingProvisionWithItsOwnQuotation_HasItAsItsAmendment`,
  `AnnouncementTests.QuotedAmendingProvision_IsExportedWithItsAmendments_InTheQuotation`,
  `AnnouncementTests.QuotationWithAQuoteThatNoCommandIntroduces_StaysText`, zgodność ze schematem;
  `NestedAmendmentTests.CommandWithAMistakeInItsEnding_StillOpensTheNestedQuote`,
  `NestedAmendmentTests.LinesOfAQuoteOpenedInsideAParagraph_ContinueThatParagraph`,
  `NestedAmendmentTests.LinesOfNewWords_AreTheTextOfTheQuotedProvision_NotItsUnits`.
