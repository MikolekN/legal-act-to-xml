# ADR-0013: Moc wsteczna przysłania datę wejścia w życie dopiero przy zapisie, nigdy w modelu

- Status: - (w części o elementach metadanych zastąpiony przez [ADR-0022](0022-daty-aktu-jako-zdarzenia-cyklu-zycia.md))
- Data: 2026-09-03

## Kontekst

Klauzula końcowa może nadać aktowi albo jego poszczególnym przepisom **moc wsteczną**
(„…, z mocą od dnia 1 lipca 2025 r.", § 51 ust. 1 ZTP). Jednostka nią objęta obowiązuje od dnia
wcześniejszego niż dzień, w którym akt wszedł w życie — czyli niesie **dwie** daty, które trzeba
rozróżniać.

W wyniku ma stać **jedna** data na jednostce. Z dwóch dat roztrzygamy i zapisujemy jedną.
Gdyby decydował sam model danych, poprawniej byłoby trzymać obie daty osobno na każdej jednostce.

Rozsyłanie daty po drzewie było już rozstrzygnięte
([ADR-0011](0011-data-ogolna-rozsylana-po-calym-drzewie.md)) i opierało się na tym, że **pusta data
niesie informację** — znaczy „tu jest nierozstrzygnięty wyjątek". Każde rozwiązanie musiało ten
niezmiennik utrzymać.

## Decyzja

Model niesie **obie daty osobno**. `IEntersIntoForce` ma `EffectiveDate` i `RetroactiveFrom`, a
propagacja rozsyła je po drzewie **niezależnymi kanałami** — jednostka może wziąć datę wejścia
w życie z reguły ogólnej, a moc wsteczną z wyjątku wskazującego ją samą.

Wybór jednej z dwóch dat zachodzi **wyłącznie przy zapisie**, w `GoverningDate`:

| stan jednostki | wartość atrybutu `wejscie-w-zycie` |
|---|---|
| sama data wejścia w życie | data wejścia w życie |
| obie daty | **moc wsteczna** |
| sama moc wsteczna | moc wsteczna |
| żadna | atrybutu nie ma |

Reguła patrzy na **obecność** daty, nie na jej położenie w czasie.

Przysłanianie dotyczy **atrybutów jednostek i zdań**. Metadana aktu `DataWejsciaWZycie` zostaje datą
formalną, a moc wsteczna całego aktu idzie do `DatyMocyWstecznej/DataMocyWstecznej`.

## Odrzucone alternatywy

**Nadpisanie `EffectiveDate` datą wsteczną w propagatorze.** Najprostsze: jedna data w modelu, zero
zmian w konwerterach. Odpadło, bo kasuje informację nie do odtworzenia — po nadpisaniu nie da się
odpowiedzieć, czy `2025-07-01` jest dniem wejścia w życie jednostki, czy dniem, od którego działa
ona wstecz. Traci na tym diagnostyka (zrzut kanoniczny przestaje pokazywać, co parser właściwie
rozpoznał) i porównanie z plikami referencyjnymi RCL, a pole `EffectiveDate` przestaje znaczyć to,
co mówi jego nazwa.

**Rozdzielenie dat na jednostce — osobny atrybut mocy wstecznej.** Merytorycznie **najpoprawniejsze**
i tak należałoby to zrobić, gdyby decydował sam model danych. Odbiorca {RCL) wymaga w wyniku jednej daty.

**Wybór daty po chronologii — wygrywa wcześniejsza.** Kuszące, bo moc wsteczna zwykle jest
wcześniejsza. Odpadło, bo nie zawsze: w korpusie jest akt (`354687_3139110`), w którym art. 2,
art. 4 i art. 5 wchodzą w życie z dniem następującym po dniu ogłoszenia, czyli 2025-12-24,
„z mocą od dnia 1 stycznia 2026 r." — moc wsteczna wypada PO ich własnej dacie wejścia w życie,
a przed datą ogólną aktu (2026-06-24). Reguła chronologiczna wybrałaby tam 2025-12-24, czyli dzień,
od którego te przepisy jeszcze nie działają. Obecność daty jest kryterium jednoznacznym, jej
kolejność nie — patrz [backlog § 3b](../backlog.md).

**Przysłanianie także w metadanych aktu.** Dawałoby jedną regułę w całym pliku, bez wyjątków.
Odpadło, bo `DataWejsciaWZycie` odpowiada na pytanie „kiedy akt wszedł w życie", a to jest data
formalna; przysłonięta metadana nie zapisywałaby jej już nigdzie.

## Konsekwencje

- **Wartość domyślna znaczy co innego w każdym z pól.** Pusto na `EffectiveDate` to
  „nierozstrzygnięty wyjątek" (ADR-0011), pusto na `RetroactiveFrom` to „mocy wstecznej nie ma".
  Moc wsteczna jest w klauzuli zawsze datą kalendarzową, więc nie istnieje stan „jest, ale nie
  wiadomo która".
- **Wartość atrybutu liczy jedno miejsce.** `GoverningDate` wołane przez
  `UnitLifecycleXml.EntryDate`. Zdania i części wspólne muszą iść tą samą drogą — zanim je pod nią
  podpięto, każde liczyło datę własną kopią wyrażenia i przysłonięcie ominęłoby je bez śladu.
- **Mapa wyjątków ma dwa niezależne kanały**, bo wyjątek nie musi nadpisywać obu składników:
  „z wyjątkiem art. 2, który wchodzi w życie …" nadpisuje oba (brak mocy wstecznej też jest
  wartością i zdejmuje moc wsteczną reguły ogólnej), a „z tym że art. 2 z mocą od dnia …" wyłącznie
  moc wsteczną, zostawiając jednostce datę reguły ogólnej.
- **Powrót do dwóch dat na jednostce jest tani i celowo przygotowany.** Wartość atrybutu liczy jedna
  funkcja (`GoverningDate`) mająca jednego wywołującego (`UnitLifecycleXml.EntryDate`). Model i
  propagacja niosą obie daty osobno, więc nie wymagałyby żadnej zmiany.
- **Zrzut kanoniczny zyskał atrybut `retroactiveFrom`**, więc golden snapshoty wymagają
  przegenerowania.
- **`--compare-eif` musi po stronie zrzutu preferować `retroactiveFrom`**, bo w schemaPL atrybut
  niesie już wartość przysłoniętą. Bez tego narzędzie raportuje rozjazd na każdym akcie z mocą
  wsteczną.

## Weryfikacja

Strażnik w `tests/Saga.Core.Tests/ArchitectureDecisionTests.cs`:

- `Adr0013_RetroactiveDate_MasksEntryDateOnlyInOutput` — jeden akt sprawdzany z dwóch stron.
  Pierwsza asercja (model trzyma dwie RÓŻNE daty) pęka po powrocie do nadpisywania `EffectiveDate`
  w propagatorze; druga (atrybut niesie moc wsteczną) — po usunięciu przysłaniania; trzecia
  (metadana niesie datę formalną) — po rozciągnięciu przysłaniania na metadane.

Semantykę pokrywają:

- `EntryIntoForceXmlOutputTests` — sama reguła precedencji na czterech stanach dat jednostki,
  przysłanianie na jednostkach, zdaniach i częściach wspólnych, metadane, zrzut kanoniczny;
- `EffectiveDatePropagatorTests` — niezależność kanałów przy rozsyłaniu;
- `EntryIntoForceParseIntegrationTests` — pełny potok od tekstu klauzuli do dat na jednostkach.

Pomiar zewnętrzny: `dotnet run --project tools/CorpusValidator -- --compare-eif <RCL.xml> <zrzut.xml>`
zestawia pary (jednostka, data) z plikiem referencyjnym RCL. To jedyna weryfikacja, która potwierdza
samą precedencję wobec źródła zewnętrznego — gdyby RCL trzymał w `wejscie-w-zycie` datę formalną
także dla przepisów z mocą wsteczną, decyzja wymagałaby nowego ADR.
