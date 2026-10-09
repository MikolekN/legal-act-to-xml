# ADR-0018: Oznaczenie w eId — liczba arabska, sufiks jak w tekście, bez indeksu górnego

- Status: Accepted — Superseded in part by [ADR-0019](0019-indeks-gorny-w-eid.md) (indeks górny)
- Data: 2026-09-25

## Kontekst

[ADR-0014](0014-skroty-jednostek-w-eid.md) ustala levelType segmentu eId, ale nie postać oznaczenia
po nim. Ta rozjeżdżała się między źródłami adresu:

- jednostki wyższe (część, księga, tytuł, dział) dostawały kanoniczną cyfrę rzymską
  (`ksi_I`, `dzial_II`), rozdziały i oddziały — arabską;
- indeks górny (§ 89 ust. 6 ZTP, „art. 53¹") wypadał z eId jednostki (`art_53`), bo
  `EntityNumber.Value` go nie niesie, ale zostawał w adresie odczytanym z powołania
  (`art_53[1]`) — cel nowelizacji nie trafiał w jednostkę, a nawias łamał wzorzec `EIdTyp`
  schematu wyjściowego (`[a-z]{2,5}_[A-Za-z0-9\-]+`).

Przykłady autorów schematu nie rozstrzygają: ich plik referencyjny zapisuje `art_53[1]`, co ich
własny schemat odrzuca.

## Decyzja

Oznaczenie w eId:

| W akcie | eId | Reguła |
|---|---|---|
| `KSIĘGA I`, `DZIAŁ II`, `TYTUŁ XVII` | `ksi_1`, `dzial_2`, `tyt_17` | liczba rzymska → arabska |
| `KSIĘGA PIERWSZA`, `Dział pierwszy` | `ksi_1`, `dzial_1` | liczebnik słowny → arabska |
| `DZIAŁ IVa`, `Rozdział 2a` | `dzial_4a`, `rozdz_2a` | sufiks dokładnie jak w tekście |
| `Art. 53¹` (`53[1]`) | `art_53` | indeks górny poza eId |

Oznaczenie z aktu nie ginie: niesie je `EntityNumber.RawValue` (dla jednostek systematyzacyjnych
źródło `<nr>`), a indeks górny — `EntityNumber.Superscript` i napis oznaczenia w adresie
odczytanym z powołania.

Reguły mieszkają w dwóch miejscach, każde na swoim wejściu:

- zamiana liczby rzymskiej i słownej na arabską — przy parsowaniu numeru jednostki
  (`ParagraphClassifier.ParseSystematizingNumber`), bo słownik liczebników należy do
  `PolishNumerals` ([ADR-0009](0009-jedno-zrodlo-liczebnikow-polskich.md));
- pominięcie indeksu górnego — w `EIdSegment.Of`, przez który przechodzi każdy segment eId,
  także adresu z powołania.

## Odrzucone alternatywy

- **Cyfra rzymska w eId jednostek wyższych (stan poprzedni).** Dwie konwencje liczbowe w jednym
  adresie (`dzial_II__rozdz_3`) i dodatkowa niespójność ze starymi kodeksami, w których rozdziały
  też są rzymskie.
- **Indeks górny w eId w postaci zgodnej z `EIdTyp` (np. `art_53-1`).** Jednoznaczne, ale to
  konwencja wymyślona przez nas — ani ZTP, ani przykłady autorów schematu jej nie znają.
  Właściciel projektu wybrał eId bez indeksu, przyjmując skutek niżej.
- **Zamiana rzymskich w `EIdSegment.Of` (jedno miejsce na wszystko).** Litery `i`, `v`, `x`, `c`,
  `d`, `l`, `m` to poprawne oznaczenia liter (`lit_i`), więc reguła musiałaby znać poziom; do tego
  liczebnik słowny wymagałby słownika poza `PolishNumerals`.

## Konsekwencje

- **Świadomy koszt: art. 53 i art. 53¹ mają ten sam eId `art_53`** (w tekstach jednolitych całe
  serie). Jednostki rozróżnia `<nr>` i drzewo, nie eId.
- Tożsamości celów nowelizacji nie wolno liczyć po eId: `LegalReferenceService` porównuje
  oznaczenia z tekstu, więc „art. 5 i 5[1]" to nadal dwa cele (dwie komendy o tym samym
  `element-eId`).
- [ADR-0015](0015-jedno-zrodlo-powolan-jednostek.md) bez zmian: odczyt powołań zachowuje indeks
  górny w oznaczeniu (`StructuralReference.Article = "328[11]"`); zmieniła się tylko postać eId
  (`art_328__par_2`), którą asertuje jego strażnik.
- Zmienia wyjście: `element-eId` celów z indeksem górnym.
- Jednostka nazwana bez numeru („CZĘŚĆ OGÓLNA") — osobna, tymczasowa postać eId, poza tą tabelą.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0018_SystematizingDesignation_IsArabicInEId_AsWrittenInRawValue`
  — każdy wiersz tabeli jednostek systematyzacyjnych.
- `ArchitectureDecisionTests.Adr0018_Superscript_StaysOutOfEId_ButTargetsStayDistinct` — indeks
  górny poza eId jednostki i adresu; dwa cele „art. 5 i 5[1]".
- `EIdTests.AddressReadFromCitation_IsTheEIdOfTheCitedUnit_AtEveryLevel` — adres z powołania równy
  eId jednostki, także dla `art. 216[1]`.
- `ArchitectureDecisionTests.Adr0015_AmendmentTargetAndEntryIntoForce_ReadTheSameAddress`,
  `UnitReferencesTests.Reader_KeepsSuperscriptDesignations` — oznaczenie z indeksem zachowane
  w odczycie.
