# ADR-0076: Cudzysłów otwarty pod przepisem trwa do domknięcia — akapity w nim należą do przytoczenia, bez względu na styl

- Status: Accepted
- Data: 2026-10-14

## Kontekst

Przytoczenie pod przepisem, który niczego nie zmienia, jest treścią tego przepisu
([ADR-0071](0071-tresc-pod-przepisem-ktory-niczego-nie-zmienia-jest-jego-trescia.md)). Rozpoznawało się je jednak tylko wtedy, gdy treść cytowaną zebrał mechanizm
nowelizacji — po stylu zmiany (`Z…`) albo po zwrocie „w brzmieniu:". Oświadczenia złożone stylem cytatu
(„1) oświadczenie następującej treści:", „„Oświadczam, że:" w stylu `CYT`, dalej pozycje oświadczenia) nie były
zbierane wcale: wiersz otwierający zostawał `fragment`, a pozycje oświadczenia stawały się punktami aktu —
z adresami jego punktów („art_5__ust_5__pkt_1" dwa razy). Bywa też, że pozycja oświadczenia ma styl litery albo punktu.

## Decyzja

**Akapit, który zaczyna się cudzysłowem i go nie domyka, pod przepisem kończącym się dwukropkiem i niczego
niezmieniającym, otwiera przytoczenie; kolejne akapity — w dowolnym stylu i z dowolnym oznaczeniem — należą do niego,
dopóki cudzysłów się nie domknie** (`AmendmentStateManager.OpensProvisionQuote`, `ParsingContext.ProvisionQuoteDepth`).
Domknięte przytoczenie przechodzi drogą z ADR-0071 (`tresc-strukt` w zdaniu przepisu).

- Bilans cudzysłowów liczy się z tekstu akapitów, nie ze stylów — w dokumencie ze stylami tracker treści cytowanej
  jest rozbrojony, a styl pozycji bywa pomylony.
- Cytat nie przechodzi przez artykuły: kolejny artykuł kończy przytoczenie, któremu zabrakło cudzysłowu zamykającego.

## Odrzucone alternatywy

- **Styl cytatu (`CYT`) jako sygnał.** Pozycje oświadczeń mają też style punktu i litery; o granicy cytatu mówi
  cudzysłów.
- **Kończyć przytoczenie na pierwszym akapicie w stylu ustawy matki**, jak treść nowelizacji. Pozycja oświadczenia
  w stylu litery ucinała je w połowie.

## Konsekwencje

- W korpusie zmienia się 11 tekstów jednolitych: 25 oświadczeń wychodzi jako `tresc-strukt`, a ich wiersz otwierający przestaje być `fragment` (28 akapitów). Powtórzonych eId w tekstach jednolitych jest 513 (było 559) — pozycje oświadczeń przestały być punktami aktu; nierozróżnionych okresem zostaje 34. Zgodnych ze schematem jest nadal 2278 z 2321 (oba walidatory), pokrycie tekstu bez zmian. Bez stylów: 114 jednostek zgodnych więcej, żadna mniej; w pełni zgodnych tekstów jednolitych 628 z 728 (było 625).
- Przepis zmieniający nie otwiera tej drogi — treść pod nim zostaje nowelizacją (`AmendsAnything`).

## Weryfikacja

- `ArchitectureDecisionTests.Adr0076_QuoteOpenedUnderAProvision_RunsToItsClosingMark_AndItsItemsAreNotUnitsOfTheAct`
- `QuotedTextInProvisionTests` — style mieszane, bez stylów, zgodność ze schematem, cudzysłów niedomknięty do
  kolejnego artykułu.
