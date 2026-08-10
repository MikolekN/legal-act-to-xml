# ADR-0010: Jeden kursor ścieżki jednostek; polityka błędu należy do wywołującego

- Status: Accepted
- Data: 2026-07-30

## Kontekst

Powołania jednostek redakcyjnych rozkładane są w dwóch miejscach: w wyliczeniu głównym wyjątku
(„z wyjątkiem art. 1 pkt 3 i 9, …") oraz wewnątrz zakresu („… w zakresie art. 85a ust. 1
i art. 85c–85j"). Oba potrzebują dokładnie tych samych reguł: dziedziczenia poziomu przez gołe
oznaczenie, rozstrzygnięcia „§ po artykule to paragraf kodeksu", mapowania poziomu na `With*`,
liczebnika porządkowego dla tiretu i zdania, kropkowej ścieżki tiretu zagnieżdżonego oraz leniwej
emisji (powołanie nadrzędne nie jest celem, gdy następuje po nim głębsze).

Fold zakresu powstał później i został **przepisany, nie współdzielony**. Skutek był dokładnie taki,
jakiego można się spodziewać — dwie ciche rozbieżności:

- **liczebnik nie do przeliczenia**: fold główny flagował i przerywał krok, fold zakresu robił
  `NormalizeOrdinal(x) ?? x`, więc „w zakresie … zdanie ostatnie" dawało ścieżkę `zd_ostatnie`
  wyglądającą na kompletną;
- **tiret zagnieżdżony**: fold główny budował ścieżkę `1.2`, fold zakresu zwykłe `WithTiret`, więc
  „w zakresie … tiret pierwsze podwójne tiret drugie" dawało `tir_2` — pierwszy segment ginął.

Do tego samo wyrażenie dziedziczenia poziomu miało cztery kopie, a `FoldState` trzymał ten sam
poziom w dwóch polach (`LastLevel` i `PendingLevelOfReference`) przypisywanych zawsze razem.

Analogiczna rozbieżność wystąpiła w rezolwerze celów: `Resolve` (drzewo dokumentu) i
`ResolveInAmendment` (cytat nowelizacji) miały osobne łańcuchy zejścia, przy czym ten drugi
**w ogóle nie znał poziomu zdania** — zakres wskazujący zdanie zwracał jednostkę nadrzędną,
`ApplyDate` iterowało po pustym zbiorze segmentów i nie robiło nic, a wyjątek wyglądał
na zastosowany. To najgorszy możliwy tryb błędu w parserze aktów.

## Decyzja

**Jeden kursor ścieżki jednostek — `UnitPathCursor` — jest wyłącznym właścicielem reguł powołania.**
Oba foldy przez niego przechodzą.

**Kursor NIE zna listy wyników ani typu celu.** Odpowiada tylko na dwa pytania: czy krok się udał
(`UnitStepStatus`) i co zrobić z kandydatem z poprzedniego kroku (`PendingDisposition`). Emisję
i politykę błędu trzyma wywołujący. Dzięki temu wyliczenie główne (emituje `ScopedUnitReference`)
i zakres (emituje gołe `StructuralReference`) dzielą kursor bez wspólnego typu wyniku.

**Dwie polityki błędu pozostają rozdzielone i widoczne w ciele wywołującego:**

| | polityka |
|---|---|
| wyliczenie główne | oznaczenie nierozłożone flaguje POJEDYNCZY cel, reszta powołania rozkłada się dalej |
| zakres | pierwszy element nierozłożony przerywa CAŁY zakres (`BuildScopeStructures` zwraca null) |

Asymetria jest zamierzona: zakres rozłożony w połowie wygląda na kompletny, więc jest gorszy niż
żaden (ADR-0008).

**Analogicznie w rezolwerze:** jeden łańcuch `DescendFrom*` obsługuje oba korzenie — drzewo dokumentu
i treść cytatu — a różnica sprowadza się do miejsca startu. `ResolveInAmendment` zachowuje na własność
wyłącznie wielopunktowe wejście (próba kolejnych poziomów na korzeniach cytatu), bo cytat zawiera
tylko fragment faktycznie przytoczony.

## Odrzucone alternatywy

**Wspólny fold sparametryzowany polityką błędu i typem wyniku** (generyk albo delegat). Odrzucony
z dwóch powodów. Po pierwsze wymusiłby wspólny typ wyniku, a `ScopedUnitReference` (z zakresem,
flagą przypisywalności i tekstem źródłowym) i `StructuralReference` (goła ścieżka) to różne byty —
sprowadzenie ich do wspólnego mianownika oznaczałoby albo generyk z fabryką, albo wciśnięcie
zakresu do ścieżki. Po drugie, i ważniejsze: schowałby regułę „zakres rozkłada się w całości albo
wcale" za wskaźnikiem do funkcji. Czytający `Take(...)` nie wiedziałby, czy pojedyncza porażka
kosztuje jeden cel czy cały zakres — a to jest właśnie ta decyzja, którą trzeba widzieć.

**Zostawić dwa foldy i tylko zsynchronizować różnice.** Odrzucony — obie rozbieżności powstały
przez „skopiuj i dostosuj" i żadna nie wywaliła builda ani testu. Synchronizowanie zakłada, że
następna osoba znajdzie oba miejsca.

**Wspólny walker/wizytor dla całego projektu.** Poza zakresem: obchodów drzewa jest w `src/`
kilkanaście i dotykają konwerterów XML, rendererów HTML i CLI. To osobna praca z własnym ryzykiem.

## Konsekwencje

- Naprawa **zmienia wyjście** w dwóch przypadkach: zakres z liczebnikiem nie do przeliczenia
  (był cel wyglądający na kompletny, jest oflagowanie) oraz zakres z tiretem zagnieżdżonym
  (były dwa cele, jest jeden ze ścieżką kropkową). Na korpusie 242 aktów żaden z tych kształtów
  nie występuje — zweryfikowane: CSV korpusowy i zrzut aktu referencyjnego **bit w bit identyczne**
  przed i po. To naprawa utajona, pokryta testami jednostkowymi.
- Zakres wskazujący zdanie, którego jednostka nie zawiera, jest teraz **nietrafieniem** i trafia do
  `UnresolvedReason`, zamiast po cichu nie robić nic.
- `BuildScopeStructures` zwraca `List<StructuralReference>?`, gdzie null znaczy „nie da się
  rozłożyć". Wcześniej pusta lista mieszała ten stan ze stanem „zakres bez jednostek".
- Kolejność operacji w `UnitPathCursor.Step` jest nośna: dyspozycja poprzedniego kandydata jest
  liczona i zwracana **także przy porażce kroku**. Tak zachowywał się pierwotny fold; odwrócenie
  kolejności zmienia wynik dla wejść nieparsowalnych.
- `TryApplyTiret` został przeniesiony **dosłownie**, bez próby upraszczania — refaktor miał być
  neutralny, a jego równoważności nie dało się wtedy wykazać inaczej niż kopią znak w znak.
  *Sprostowanie (2026-07-31):* pierwsza wersja tego wiersza mówiła o „kwirku", w którym metoda
  przycina ścieżkę tiretu, mimo że zaraz zwraca false. Analiza przepływu pokazuje, że to
  niemożliwe — pętla przycinająca kończy się dokładnie na głębokości, którą strażnik przepuszcza,
  więc przycięcie i porażka wykluczają się wzajemnie. Kod jest poprawny, mylny był opis.

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

Odwrócenie decyzji wymaga usunięcia którejś z tych asercji — zmiana widoczna w diffie.
