# ADR-0019: Indeks górny zostaje w eId w notacji „[n]"

- Status: Accepted — Supersedes [ADR-0018](0018-postac-oznaczenia-w-eid.md) w części dotyczącej indeksu górnego
- Data: 2026-10-09

## Kontekst

[ADR-0018](0018-postac-oznaczenia-w-eid.md) wyłączył indeks górny z eId (`art_53` dla art. 53¹),
bo wzorzec `EIdTyp` ówczesnej wersji schematu wyjściowego nie dopuszczał nawiasów, a pytanie
o postać indeksu górnego w adresie czekało na autorów schematu.

Odpowiedź przyszła w nowej wersji schematu: `EIdTyp` dopuszcza indeks górny w nawiasach
kwadratowych (`[a-z]{2,5}_[A-Za-z0-9]+((\[[^\]]*\])|(%5B[^%]*%5D))?`), a przykłady autorów schematu,
przygotowane od źródłowego DOCX do gotowego XML (`akn4pl/przyklady/*/schemaPL_no_ref.xml`), zapisują
`art_4[1]`, `art_477[9]__par_2[1]`. Wszystkie walidują się względem tego schematu.

## Decyzja

**Indeks górny jest częścią oznaczenia w eId, w notacji § 89 ust. 6 ZTP: `art_53[1]`**, tak samo
w eId jednostki i w adresie odczytanym z powołania. Oznaczenie z indeksem niesie
`EntityNumber.Designation`; segment składa `EIdSegment.Of` bez zmian w samym oznaczeniu.

Pozostałe reguły ADR-0018 (liczba rzymska i słowna → arabska, sufiks jak w tekście) obowiązują.

## Odrzucone alternatywy

- **eId bez indeksu górnego (ADR-0018).** Niezgodne z przykładami autorów schematu; art. 53 i 53¹
  dzieliły eId, a odbiorca nie odróżniał ich po adresie.
- **Indeks kodowany URL-em (`%5B1%5D`).** Schemat dopuszcza obie postacie, ale przykłady używają
  nawiasów, a nawias to notacja samego ZTP.

## Konsekwencje

- Zmienia wyjście: eId jednostek z indeksem górnym (`art_4` → `art_4[1]`) i `element-eId` celów
  z indeksem górnym (`art_53` → `art_53[1]`).
- art. 53 i art. 53¹ mają znów różne eId.
- Tożsamość celów nowelizacji `LegalReferenceService` liczy nadal po oznaczeniach z tekstu, nie po
  napisie eId — niezależnie od tego, jak postać eId zmieni się w przyszłości.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0019_Superscript_IsInTheEId_InBracketNotation`.
- `ArchitectureDecisionTests.Adr0015_AmendmentTargetAndEntryIntoForce_ReadTheSameAddress`,
  `UnitReferencesTests.Reader_KeepsSuperscriptDesignations` — `art_328[11]__…` w adresie z powołania.
- `EIdTests.AddressReadFromCitation_IsTheEIdOfTheCitedUnit_AtEveryLevel` — eId jednostki
  `art. 216[1]` równy adresowi z powołania.
