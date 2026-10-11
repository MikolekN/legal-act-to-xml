# ADR-0035: W akcie opartym na paragrafach „§" po paragrafie wskazuje kolejny paragraf, nie paragraf kodeksu

- Status: Accepted (uzupełnia regułę 1 z [ADR-0015](0015-jedno-zrodlo-powolan-jednostek.md) dla aktów z [ADR-0034](0034-paragraf-jest-jednostka-podstawowa-aktu-wykonawczego.md))
- Data: 2026-10-11

## Kontekst

[ADR-0015](0015-jedno-zrodlo-powolan-jednostek.md) rozstrzyga „§" w powołaniu dwustopniowo: po artykule —
jawnym albo odziedziczonym z komendy nadrzędnej — to zawsze paragraf kodeksu; samodzielny — według rodzaju aktu.
Reguła powstała, zanim rozporządzenie miało model. Od [ADR-0034](0034-paragraf-jest-jednostka-podstawowa-aktu-wykonawczego.md)
jednostka podstawowa rozporządzenia stoi w ścieżce tam, gdzie artykuł (`StructuralReference.Article`), więc kursor
nie odróżniał „art. 5" od „§ 5": każdy kolejny „§" trafiał **pod** poprzedni jako paragraf kodeksu.

W korpusie dawało to 208 celów komend w 122 rozporządzeniach z adresem, którego akt nie ma:

- „po § 3 dodaje się § 3a i § 3b w brzmieniu:" → `art_3__par_3a`, `art_3__par_3b` zamiast `art_3`,
- „§ 6 i § 7 otrzymują brzmienie:" → `art_6__par_7` zamiast `art_6` i `art_7`,
- „w § 6a, w § 12, w § 14 w ust. 1 …" → `art_6a__par_12` zamiast trzech celów.

To samo czytanie obsługuje klauzulę wejścia w życie: „z wyjątkiem § 1 pkt 4, § 3 i § 4, które wchodzą w życie
po upływie 14 dni" dawało wyjątek `art_1__par_3`, więc § 3 i § 4 dostawały datę ogólną.

## Decyzja

`UnitPathCursor` pamięta, **czym** wprowadzono jednostkę główną ścieżki. Gdy wprowadził ją „§" (akt oparty na
paragrafach), kolejny „§" jest następną jednostką główną — rodzeństwem, nie podjednostką. Gdy wprowadziło ją
jawne „art.", „§" pozostaje paragrafem kodeksu, także w rozporządzeniu („art. 5 § 2 Kodeksu …").

Jednostka główna odziedziczona z komendy nadrzędnej („W § 5 wprowadza się następujące zmiany:") jest
w akcie opartym na paragrafach paragrafem — rozstrzyga rodzaj aktu (`SectionSignRole`), tak jak dla
samodzielnego „§". W ustawie nic się nie zmienia.

## Odrzucone alternatywy

- **Osobny poziom ścieżki dla paragrafu rozporządzenia** (`UnitLevel.Section`, osobne pole
  w `StructuralReference`). Rozdzieliłby adres jednostki (`art_5`, ADR-0034) i adres jej powołania, a każde
  miejsce czytające ścieżkę dostałoby drugi przypadek tego samego poziomu.
- **Traktowanie jednostek za czasownikiem komendy jako przedmiotu** („dodaje się § 3a" nie jest celem).
  Naprawia tylko pierwszy z trzech kształtów; „§ 6 i § 7 otrzymują brzmienie" i wyliczenia z przyimkiem nie mają
  czasownika między paragrafami.
- **„§" zawsze jednostką główną w akcie opartym na paragrafach.** Rozporządzenie powołuje kodeksy
  („art. 43zaa § 1 pkt 1") — po jawnym artykule „§" musi zostać paragrafem kodeksu.

## Konsekwencje

- Cele z segmentem `par` w rozporządzeniach: 208 → 6 (pozostałe to jawne „art. … § …"). Wyliczone paragrafy
  są osobnymi komendami ([ADR-0016](0016-wyliczenie-celow-to-osobne-komendy.md)), więc „§ 6 i § 7 otrzymują
  brzmienie" daje dwa cytaty, każdy ze swoim paragrafem.
- Kursor niesie stan (`_articleIsSection`), którego nie ma w `StructuralReference`: ścieżka zapisana i odczytana
  ponownie traci informację, czym wprowadzono jednostkę główną — odtwarza ją rodzaj aktu przy
  `UnitPathCursor.StartingAt`. Komenda rozporządzenia, której cel nadrzędny podano jawnym „art." (w korpusie
  brak), potraktuje „§" w komendzie podrzędnej jako paragraf.
- Reguła 1 z ADR-0015 obowiązuje w brzmieniu: po **artykule** „§" to paragraf kodeksu; po **paragrafie** aktu
  wykonawczego — kolejny paragraf.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0035_SectionSignAfterASection_IsTheNextSection`
- `UnitReferencesTests.SectionSignAfterSection_InSectionBasedAct_IsTheNextMainUnit`,
  `UnitReferencesTests.StartingAt_InheritedSection_InSectionBasedAct_KeepsSectionSignTheMainUnit`
- `LegalReferenceServiceTests.ReadAmendmentTargets_SectionAddedAfterASection_TargetsTheSectionBefore`,
  `LegalReferenceServiceTests.ReadAmendmentTargets_EnumeratedSections_AreSeparateTargets`
