# ADR-0010: Kursor ścieżki jednostek

- Status: -
- Data: 2026-07-30

## Kontekst

Powołania jednostek redakcyjnych rozkładane są w dwóch miejscach: w wyliczeniu głównym wyjątku
(„z wyjątkiem art. 1 pkt 3 i 9, …") oraz wewnątrz zakresu („… w zakresie art. 85a ust. 1
i art. 85c–85j"). Oba potrzebują dokładnie tych samych reguł: dziedziczenia poziomu przez gołe
oznaczenie, rozstrzygnięcia „§ po artykule to paragraf kodeksu", mapowania poziomu na `With*`,
liczebnika porządkowego dla tiretu i zdania, kropkowej ścieżki tiretu zagnieżdżonego oraz leniwej
emisji (powołanie nadrzędne nie jest celem, gdy następuje po nim głębsze).

## Decyzja

**Jeden kursor ścieżki jednostek — `UnitPathCursor` — jest wyłącznym właścicielem reguł powołania.**

**Kursor NIE zna listy wyników ani typu celu.** Odpowiada tylko na dwa pytania: czy krok się udał
(`UnitStepStatus`) i co zrobić z kandydatem z poprzedniego kroku (`PendingDisposition`). Emisję
i politykę błędu trzyma wywołujący. Dzięki temu wyliczenie główne (emituje `ScopedUnitReference`)
i zakres (emituje gołe `StructuralReference`) dzielą kursor bez wspólnego typu wyniku.

## Konsekwencje

- `BuildScopeStructures` zwraca `List<StructuralReference>?`, gdzie null znaczy „nie da się
  rozłożyć". Wcześniej pusta lista mieszała ten stan ze stanem „zakres bez jednostek".

## Weryfikacja

- `ArchitectureDecisionTests.Adr0010_ScopeAndMainEnumeration_ShareTheSameUnitPath` — ten sam ogon
  powołania raz jako wyliczenie główne, raz wewnątrz zakresu; asercja na równość ścieżek.
  Rozwidlenie kursora łamie go natychmiast.
- `UnitEnumerationParserTests.MainAndScope_AgreeOnNestedTiret` — to samo na poziomie jednostkowym.
- `UnitEnumerationParserTests.ScopeNestedTiret_UsesDottedPath`,
  `ScopeSentenceOrdinal_IsNumbered`, `ScopeSentenceThatIsNotANumeral_MakesTheWholeScopeProse` —
  trzy naprawione rozbieżności, każda osobno.
- `EntryIntoForceTargetResolverTests.Amendment_SentenceWithoutMatchingSegment_ReturnsNull` —
  strażnik najgroźniejszego z defektów: ścieżki, która wcześniej była cicho nieaktywna.
- `EntryIntoForceTargetResolverTests.ImplicitParagraphFallback_BehavesIdentically_InDocumentAndInAmendment`
  — parytet obu korzeni na linii, która była skopiowana co do znaku.
