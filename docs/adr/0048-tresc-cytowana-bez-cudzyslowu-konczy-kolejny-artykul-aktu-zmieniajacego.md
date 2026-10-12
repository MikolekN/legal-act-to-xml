# ADR-0048: Treść cytowaną bez cudzysłowu otwierającego i bez stylów kończy kolejny artykuł aktu zmieniającego albo formuła podpisu

- Status: Accepted
- Data: 2026-10-12

## Kontekst

Granicę treści cytowanej w nowelizacji wyznacza styl akapitu albo — bez stylów — bilans cudzysłowów
(`QuoteBalanceTracker`): śledzenie zaczyna cudzysłów otwierający pierwszego akapitu treści, a kończy
cudzysłów, który bilans domyka. Gdy pierwszy akapit treści cudzysłowu otwierającego nie ma (zgubiony
w źródle, w wyniku rozpoznania pisma albo konwersji), bilansu nie ma od czego liczyć. Treść trwała wtedy do
najbliższego akapitu z własnym zwrotem nowelizacyjnym albo do końca dokumentu — kolejne artykuły aktu
zmieniającego, z przepisem o wejściu w życie włącznie, stawały się artykułami cytowanymi.

Dotyczy to dokumentów bez szablonu, plików tekstowych i PDF; ze stylami treść zamyka styl aktu.

## Decyzja

**Treść cytowaną, której cudzysłowu nie da się śledzić (akapit bez stylu, a treść nie zaczęła się od
cudzysłowu otwierającego), kończy to, co do aktu zmienianego należeć nie może:**

- **kolejny artykuł aktu zmieniającego** — akapit bez cudzysłowu otwierającego, oznaczony jak artykuł,
  o numerze o jeden większym od artykułu z komendą i bez litery (`ParserOrchestrator.EndsUntrackedQuotation`);
- **formuła podpisu** („Prezydent Rzeczypospolitej Polskiej: …");
- **cudzysłów zamykający bez otwarcia** na końcu akapitu (`QuoteBalanceTracker.ClosesUnopenedQuote`) — domyka
  treść, której otwarcie zgubiono; wyrażenie w cudzysłowie sparowanym wewnątrz akapitu jej nie zamyka.

Treść, której cudzysłów jest śledzony, zostaje przy bilansie cudzysłowów.

## Odrzucone alternatywy

- **Wymagać cudzysłowu otwierającego**, a bez niego nie wchodzić w treść cytowaną. Treść stałaby się wtedy
  jednostkami aktu zmieniającego („2. …" jako ustęp artykułu z komendą) — błąd w drugą stronę, przy komendzie,
  która treść jednoznacznie zapowiada (ADR-0045).
- **Każdy akapit oznaczony jak artykuł kończy treść.** Artykuł bywa treścią cytowaną („dodaje się art. 15a
  i 15b w brzmieniu:"); kończyć ją może tylko ten, który kontynuuje numerację aktu zmieniającego.
- **To samo dla cudzysłowu otwartego i niedomkniętego.** Cytat kilku artykułów może prawidłowo zawierać artykuł
  o numerze kolejnym względem artykułu z komendą; przerwanie go popsułoby treść dziś rozpoznawaną poprawnie.
  Rozróżnienie wymagałoby wiedzy o dalszej części dokumentu.

## Konsekwencje

- Ze stylami w korpusie nic się nie zmienia. Bez stylów zgodność struktury rozporządzeń rośnie z 97,22 % do
  97,31 % jednostek.
- Artykuł cytowany o numerze akurat o jeden większym od artykułu z komendą, w treści bez cudzysłowów i stylów,
  zostanie wzięty za artykuł aktu zmieniającego. Bez żadnego z trzech sygnałów nic tego nie rozstrzyga;
  alternatywą jest utrata reszty aktu.
- Zostaje: cudzysłów otwarty i niedomknięty — treść trwa do końca dokumentu albo do stylu aktu.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0048_QuotedContentWithoutQuotesOrStyles_DoesNotSwallowTheNextArticleOfTheAmendingAct`
- `AmendmentTriggerTests` — brak cudzysłowu otwierającego, brak obu, podpis, artykuły cytowane o innych
  numerach, cudzysłów zamykający bez otwarcia.
