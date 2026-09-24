# ADR-0014: Skróty jednostek w eId (levelType) wyznacza jedna zamknięta tabela

- Status: Accepted
- Data: 2026-09-23

## Kontekst

eId jednostki (`art_5__ust_2__pkt_3`) i `element-eId` w nowelizacji to adresy, po których odbiorca
eksportu wiąże jednostki — przepis zmieniający z przepisem zmienianym, datę wejścia w życie z jej
celem. Segment adresu ma postać `levelType_numer`, więc levelType jest częścią kontraktu wyjścia:
jego zmiana unieważnia każdy wcześniej wydany adres, a build tego nie zauważy.

Skróty powstawały w kilku miejscach niezależnie — konstruktory jednostek w `Saga.Model`
(`EIdPrefix`), `Tiret.PrefixForDepth`, `StructuralReference.ToString` (adresy jednostek
wskazywanych), `ParsingFactories.ApplyCodeParagraphRole` — i zaczęły się rozjeżdżać: dokumentacja
`BaseEntity.EIdPrefix` podawała `cz`/`ks` obok `czesc`/`ksi` w kodzie, komentarz przy
`Paragraph.Role` — `prg` obok `par`, backlog — `zd` obok `zdn`. Specyfikacja wymiany odesłań
([reference-schema.md](../reference-schema.md)) miała w kolumnie „eId prefix" jeszcze inny słownik —
`cz`/`ks`/`dz`/`rozd`/`zd` i jeden `tir` na każdą głębokość tiretu — a obok kolumnę „Istniejący
w BaseEntity", która błędnie twierdziła, że kod już go używa.

Tabela nie pochodzi ze schematu EAP w `docs/eap_schema/` — schemat definiuje nazwy elementów XML
(`rozdzial`, `oddzial`), nie prefiksy eId. Jest słownikiem projektu.

## Decyzja

**levelType każdego segmentu eId bierze się z tej tabeli — i tylko z niej:**

| jednostka | levelType |
|---|---|
| część | `czesc` |
| księga | `ksi` |
| tytuł | `tyt` |
| dział | `dzial` |
| rozdział | `rozdz` |
| oddział | `oddz` |
| załącznik | `zal` |
| artykuł | `art` |
| ustęp | `ust` |
| paragraf | `par` |
| punkt | `pkt` |
| litera | `lit` |
| tiret | `tir` |
| podwójny tiret | `dwtir` |
| potrójny tiret | `trtir` |
| poczwórny tiret | `cztir` |
| pięciokrotny tiret | `pitir` |
| zdanie | `zdn` |

Tabela obowiązuje tak samo w eId jednostek dokumentu, jak w adresach jednostek wskazywanych
(`element-eId` nowelizacji, cele wejścia w życie). Jest zamknięta: nowy poziom (np. akapit w prawie
UE) to nowy wiersz w nowym ADR, a nie skrót wymyślony w kodzie.

Paragraf kodeksu (§ 55 ust. 3 ZTP) ma `par`, choć w modelu to ta sama klasa `Paragraph` co ustęp.
Tiret dostaje skrót według głębokości zagnieżdżenia, a nie jeden `tir` na każdym poziomie.

## Odrzucone alternatywy

- **Skrót ustalany przy każdej jednostce osobno** — stan sprzed tej decyzji. Dawał `cz`/`ks`,
  `prg` i `zd` obok właściwych skrótów, a żadna bramka tego nie łapała: niespójny skrót to wciąż
  poprawny napis.
- **`ust` także dla paragrafu kodeksu** — najprostsze, bo w modelu to jedna klasa (tak było przed
  commitem `44db373`). Odrzucone, bo adres nazywałby jednostkę inaczej niż akt i inaczej niż element
  XML, w którym stoi (`paragraf-kodeksu`).
- **Słownik z `reference-schema.md`** (`cz`, `ks`, `dz`, `rozd`, `zd`, `tir` dla każdej głębokości) —
  spisany jako „tabela konwersji dla przyszłej integracji", ale niewdrożony: model i eksport
  używają `czesc`/`ksi`/`dzial`/`rozdz` i tiretów według głębokości, więc jego przyjęcie zmieniłoby
  wydawane już adresy.

## Konsekwencje

- Skróty żyją w konstruktorach jednostek w `Saga.Model` (`EIdPrefix`), w `Paragraph.CodeParagraphRole`,
  `Tiret.PrefixForDepth` i `StructuralReference.ToString`. Zmiana któregokolwiek bez zmiany tabeli
  łamie strażnika (niżej).
- Paragraf kodeksu w adresie jednostki wskazywanej niesie `StructuralReference.ParagraphIsCodeParagraph`,
  bo sam numer nie odróżnia „§ 2" od „ust. 2". Ustawia go `UnitPathCursor` — wspólne źródło powołań
  dla wejścia w życie i celów nowelizacji ([ADR-0015](0015-jedno-zrodlo-powolan-jednostek.md)) —
  oraz `StructureProcessor.UpdateStructuralReference` (bieżąca pozycja). Kiedy „§" jest paragrafem
  kodeksu, a kiedy jednostką główną aktu, rozstrzyga ADR-0015.
- **Decyzja wyprzedza model** — nie wszystko z tabeli jest dziś realizowane:
  - `zal` — model nie ma encji załącznika.
  - `zdn` — występuje tylko w adresach wskazywanych (`StructuralReference.Sentence`); zdania
    w eksporcie nie mają eId.
- Kolumna „eId prefix" w `reference-schema.md` jest odwzorowaniem tej tabeli na `unitType` formatu
  wymiany odesłań — przy zmianie tabeli aktualizuje się ją razem z nią.
- Segmenty części wspólnej (`intro`, `wrapUp` z `CommonPartType.ToEIdSegment`) nie są jednostkami
  i tabela ich nie obejmuje.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0014_UnitEIdPrefixes_FollowTheLevelTypeTable` — zestawia skróty
  wszystkich modelowanych jednostek z tabelą; zmiana skrótu w kodzie bez zmiany tabeli łamie go.
- `ArchitectureDecisionTests.Adr0014_ReferenceEId_UsesTheSameVocabulary` — ten sam słownik
  w adresie jednostki wskazywanej (`StructuralReference`).
- `ArchitectureDecisionTests.Adr0010_ScopeAndMainEnumeration_ShareTheSameUnitPath` — pośrednio
  pilnuje `tir`/`dwtir` w powołaniach.
- `ArchitectureDecisionTests.Adr0014_CodeParagraphReference_UsesPar` — „art. 5 § 2" daje
  `art_5__par_2`, nie `art_5__ust_2`.
- `ArchitectureDecisionTests.Adr0014_CodeParagraphLevel_SurvivesDescentAndClone` — każda metoda
  `With*` poniżej ustępu i `Clone` przenoszą paragraf kodeksu; nowy artykuł i zwykły ustęp go zerują.
- `UnitEnumerationParserTests.CodeParagraphInScope_UsesPar`,
  `BareDesignationAfterCodeParagraph_StaysCodeParagraph`,
  `LegalReferenceServiceTests.ReadAmendmentTargets_SectionSignOnlyInQuote_IsNotATarget` i
  `CodeParagraphTests.AmendmentTargetInCodeParagraph_IsExportedWithParElementEId` — te same reguły
  na dosłownych fragmentach z korpusu, aż po `element-eId` w eksporcie.
- `zal` nie ma czego pilnować, dopóki model nie ma encji załącznika.
