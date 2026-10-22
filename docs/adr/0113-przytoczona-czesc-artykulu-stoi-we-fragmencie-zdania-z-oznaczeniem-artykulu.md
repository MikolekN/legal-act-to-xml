# ADR-0113: Przytoczona część artykułu („Art. 9. „2. …”") stoi w osobnym fragmencie zdania jednostki z oznaczeniem artykułu i `tpg:znastepnym`, a w `tresc-strukt` są same ustępy z adresem w artykule

- Status: Accepted — Supersedes [ADR-0075](0075-cudzyslow-za-oznaczeniem-artykulu-otwiera-przytoczenie-jego-ustepu.md) w części o budowie przytoczenia (rozpoznanie zostaje)
- Data: 2026-10-22

## Kontekst

Obwieszczenie tekstu jednolitego przytacza przepisy, których tekst jednolity nie obejmuje
([ADR-0036](0036-przytoczone-artykuly-sa-jednostkami-aktu-przytaczanego.md)). Gdy przytoczona jest tylko część
artykułu, oznaczenie artykułu stoi przed cudzysłowem: „3) art. 9 ust. 2, art. 10 … (Dz. U. poz. 1933), które
stanowią:" i dalej „Art. 9. „2. W latach szkolnych …”". [ADR-0075](0075-cudzyslow-za-oznaczeniem-artykulu-otwiera-przytoczenie-jego-ustepu.md)
czytał taki akapit tak, jakby cudzysłów stał przed oznaczeniem: eksport dawał `artykul` z ustępem. Przy jednym
przytoczonym ustępie (48 z 61 przytoczeń w 44 tekstach jednolitych korpusu) artykuł miał jedyny ustęp, więc ustęp
wychodził jako `ustep-n` bez numeru i adresu — „2." ginęło. `artykul` z jednym `ustep` schemat odrzuca (artykuł
wymaga drugiego ustępu), a samotny `ustep` w `tresc-strukt` gubi „Art. 9." w wizualizacji.

Autor schematu odpowiedział na pytanie 27 (2026-10-22, zapis w `docs/internal/odpowiedzi-autorow-schematu.md`):
dwa fragmenty zdania obok siebie — pierwszy kończy się wprowadzeniem, drugi niesie tekst „Art. 9." z atrybutem
`tpg:znastepnym="true"` (wiersz łączy się z dalszą treścią), a w nim `tresc-strukt` z samotnym `ustep`
(`DU/2024/1933__art_9__ust_2`, `nr` 2). Wymaga to zmiany schematu: `ZdanieTyp` dostaje grupę atrybutów
`tpg:typoSpecjalne`, a atrybuty typografii stają się kwalifikowane. Zmiana nie jest jeszcze opublikowana; właściciel
projektu zdecydował (2026-10-22), że wdraża się ją z wyprzedzeniem, jak zakresy oznaczeń
([ADR-0097](0097-zakres-oznaczen-to-jedna-jednostka-z-nr-od-i-nr-do.md)).

## Decyzja

**Akapit z cudzysłowem otwartym tuż za oznaczeniem artykułu albo paragrafu, za którym stoi oznaczony ustęp („Art. 9.
„2. …"), to przytoczenie ustępów tego artykułu, nie artykułu. Oznaczenie zostaje poza cudzysłowem
(`Quotation.ArticleDesignation`) i wychodzi jako kolejny fragment zdania jednostki obwieszczenia: ten sam `nr`
i `gd:zdGUID`, następny numer `fragment`, własny `gd:fragmentGUID`, tekst „Art. 9.", `tpg:znastepnym="true"`.
W nim `tresc-strukt` z samymi ustępami — jawnymi, z numerem i adresem w artykule (`…__art_9__ust_2`), także gdy
przytoczony jest jeden.**

- Rozpoznanie i budowa modelu jak w ADR-0075: ustępy należą do swojego artykułu (`AnnouncementUnits.Build`), więc
  adres liczy się od niego; flagę niesie przytoczenie (`AnnouncementUnits.BuildQuotation`).
- Eksport: `AnnouncementXmlConverter.DesignationFragment` dokłada fragment za zdaniem jednostki, a
  `ParagraphXmlConverter.ToXml(…, quotedWithoutArticle: true)` nie traktuje jedynego ustępu jako niejawnego.
  Dalsze fragmenty zdania (następne podpunkty, część wspólna) liczą się od nowego numeru.
- Kolejne przytoczenia tej samej jednostki („„Art. 10. …”") trafiają do ostatniego fragmentu zdania, czyli za
  „Art. 9."; artykuły przytoczone w tym samym cudzysłowie za ustępami — w tym samym `tresc-strukt`, w całości.
- Za cudzysłowem bez oznaczenia ustępu („Art. 24. „W 2024 r. …") przytoczona jest cała treść artykułu — zostaje
  budowa z ADR-0075 (`artykul`).
- Prefiks przestrzeni `tpg` pisze korzeń dokumentu tylko wtedy, gdy eksport ma atrybut typografii.

## Odrzucone alternatywy

- **`artykul` z przytoczonym ustępem (ADR-0075).** Schemat nie przyjmuje artykułu z jednym `ustep`, a z `ustep-n`
  znika numer przytoczonego ustępu i jego adres.
- **Samotny `ustep` w `tresc-strukt` bez oznaczenia artykułu.** Zgodny ze schematem, ale „Art. 9." znika z tekstu —
  wizualizacja pokazuje samo „2.".
- **Oznaczenie w tym samym fragmencie zdania, co wprowadzenie** („…, które stanowią: Art. 9. <tresc-strukt>").
  Autor schematu wskazał osobny fragment z `tpg:znastepnym`, który mówi wizualizacji, że „Art. 9." i ustęp to jeden
  wiersz.
- **Ten sam numer fragmentu w obu częściach**, jak w przykładzie autora. Właściciel projektu uznał to za przeoczenie
  przykładu (2026-10-22); kolejny numer odpowiada każdemu innemu fragmentowi zdania, a schemat numeru nie sprawdza.
- **Czekać z eksportem na publikację schematu.** Decyzja jest uzgodniona; zwłoka zostawiałaby przytoczone ustępy bez
  numeru w 44 tekstach jednolitych.

## Konsekwencje

- Do czasu publikacji zmiany schematu i synchronizacji kopii `schema/` eksport tekstu jednolitego z takim
  przytoczeniem jest niezgodny z kopią schematu (atrybut `tpg:znastepnym` nieznany przy `zdanie`). To stan
  oczekiwany — ocena korpusu liczy ten błąd jako zapowiedziany; po synchronizacji trzeba go sprawdzić.
- W korpusie DOCX zmienia się 61 przytoczeń w 44 tekstach jednolitych: 48 `ustep-n` bez numeru i 13 artykułów
  z kilkoma ustępami wychodzi jako same ustępy z adresem. Ocena korpusu: 2179 → 2209 dokumentów bez winy (w 30 był
  to jedyny błąd). Zgodnych z kopią schematu 2012 → 1996 — 16 dokumentów różni tylko `tpg:znastepnym`; wobec kopii
  z naniesioną zmianą autora żaden z 44 nie ma błędu, którego nie miał wcześniej.
- Artykuł, którego jedyny ustęp ma numer, przytoczony w całości („„Art. 41a. 1. …"", postać 2 pytania 27) nie jest
  objęty tą decyzją: cudzysłów stoi przed oznaczeniem, więc nadal wychodzi `artykul` z `ustep-n`.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0113_QuotedPartOfAnArticle_StandsInASentenceFragmentWithTheArticleDesignation`
- `AnnouncementTests.QuoteOpenedAfterTheArticleDesignation_LeavesTheDesignationOutsideTheQuotation`
- `AnnouncementTests.QuotationOfAWholeArticle_HasNoDesignationOutsideIt`
- `AnnouncementTests.QuotedParagraphsOfAnArticle_AreExportedInASentenceFragmentAfterItsDesignation`
- `AnnouncementTests.QuotedParagraphOfAnArticle_IsSchemaValid_ExceptForTheAnnouncedTypographyOfTheSentence`
