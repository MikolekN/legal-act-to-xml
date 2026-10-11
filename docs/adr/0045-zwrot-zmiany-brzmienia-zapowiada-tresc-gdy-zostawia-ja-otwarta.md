# ADR-0045: Zwrot zmiany brzmienia zapowiada treść cytowaną w kolejnych akapitach tylko wtedy, gdy zostawia brzmienie otwarte

- Status: Accepted
- Data: 2026-10-11

## Kontekst

Treść nowelizacji zaczyna się za zwrotem zmiany brzmienia („otrzymuje brzmienie:", „w brzmieniu:",
„zastępuje się:"). `AmendmentStateManager.DetectTrigger` uzbrajał wyzwalacz, gdy zwrot stał **gdziekolwiek**
w akapicie; kolejny akapit bez stylu aktu stawał się wtedy treścią cytowaną, a wyjście z niej dawał dopiero
styl aktu albo domknięcie cudzysłowu.

Takim samym zwrotem posługują się przepisy, które niczego nie zmieniają: „Agencja posługuje się nazwą
w języku angielskim w brzmieniu: Polish Air Navigation Services Agency", „składa ślubowanie w brzmieniu:
„Ślubuję …"", wzór adnotacji w protokole („„Treść ustaleń na str. … otrzymuje brzmienie: ………"").
Skutki:

- bez stylów (DOCX bez szablonu, TXT, PDF) taki przepis otwierał cytat, którego nic nie zamykało — do końca
  aktu. W trzech tekstach jednolitych korpusu ginęło w ten sposób 804, 219 i 189 jednostek;
- ze stylami cytat zamykał pierwszy akapit w stylu aktu, ale gdy dalsze akapity stylu nie miały, ginęły tak
  samo — w jednym rozporządzeniu dziesięć paragrafów i załącznik.

## Decyzja

**Zwrot zmiany brzmienia uzbraja wyzwalacz tylko wtedy, gdy zostawia brzmienie otwarte na kolejne akapity**
(`AmendmentFinalizer.LeavesWordingOpen`):

- zwrot kończy akapit („… otrzymuje brzmienie:") — komenda kończy się dwukropkiem, a nowe brzmienie stoi
  za nią w cudzysłowie (§ 85–89 ZTP), albo
- za zwrotem, w tym samym akapicie, otwiera się cytat, którego akapit nie zamyka.

Zwrot, po którym brzmienie mieści się w tym samym akapicie — bez cudzysłowu albo w cudzysłowie domkniętym —
niczego za sobą nie zapowiada. Rozstrzyga ostatni zwrot w akapicie.

## Odrzucone alternatywy

- **Wymóg cudzysłowu otwierającego w następnym akapicie.** Zgodny z ZTP, ale odrzuca treść, której cudzysłów
  zgubiono w źródle, i tabelę stojącą za komendą; a samej przyczyny — wyzwalacza z przepisu, który komendą
  nie jest — nie usuwa.
- **Ograniczenie zasięgu cytatu strukturą** (koniec przy „Art. N+1" bez cudzysłowu). Cytowany artykuł
  o kolejnym numerze wygląda tak samo jak następny artykuł aktu; granica byłaby zgadywana.
- **Lista zwrotów, które komendą nie są** („posługuje się nazwą …", „ślubowanie …"). Dopasowanie do
  przykładów z korpusu; kolejny przepis tego rodzaju wymagałby kolejnego wpisu.

## Konsekwencje

- Ze stylami zmienia się 5 dokumentów korpusu: trzy teksty jednolite odzyskują ustęp, który był brany za treść
  cytowaną, a rozporządzenie — dziesięć paragrafów i załącznik. Bez stylów zgodność struktury tekstów jednolitych
  rośnie z 99,55 % do 99,81 % jednostek (ginących 2093 → 881).
- Komenda z brzmieniem w tym samym akapicie („ust. 2 otrzymuje brzmienie: „2. …"." w jednym akapicie) nie
  uzbraja wyzwalacza; jej treść i tak nie była budowana z kolejnych akapitów.
- Zostaje: komenda kończąca się dwukropkiem, po której treść nie ma cudzysłowu ani stylu — cytat trwa wtedy
  do stylu aktu albo do końca dokumentu.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0045_ModificationPhrase_ArmsTheTrigger_OnlyWhenTheWordingIsLeftOpen`
- `AmendmentTriggerTests` — zwroty otwarte i zamknięte, przepis z brzmieniem w akapicie, wzór z cytowaną komendą,
  komenda zakończona dwukropkiem.
