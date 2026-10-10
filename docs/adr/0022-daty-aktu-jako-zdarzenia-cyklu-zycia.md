# ADR-0022: Daty aktu stoją w metadanych jako zdarzenia cyklu życia (`DaneCzasowe`)

- Status: Accepted — Supersedes [ADR-0013](0013-moc-wsteczna-przyslania-date-dopiero-na-wyjsciu.md) w części o elementach metadanych
- Data: 2026-10-10

## Kontekst

[ADR-0013](0013-moc-wsteczna-przyslania-date-dopiero-na-wyjsciu.md) zapisywał datę wejścia w życie aktu
w metadanych jako `DataWejsciaWZycie`, a moc wsteczną jako `DatyMocyWstecznej/DataMocyWstecznej`.
Aktualny schemat wyjściowy nie ma żadnego z tych elementów: `MetadaneAktuTyp` trzyma dane czasowe
w `DaneCzasowe/CyklZyciaAktu` jako elementy `Zdarzenie`. Dokument z dawnymi elementami nie przechodzi
walidacji, a po pierwszym błędzie nagłówka walidator nie sprawdza już rzetelnie reszty.

Metadane niosły przy tym tylko daty reguły ogólnej. Wyjątki („z wyjątkiem art. 2, który wchodzi
w życie z dniem…") były widoczne wyłącznie jako daty na jednostkach — bez wskazania, z której części
klauzuli pochodzą.

Przykłady autorów schematu (`akn4pl/przyklady/*/schemaPL_no_ref.xml`) zapisują jedno zdarzenie na
każdą różną datę wejścia w życie: z identyfikatorem (`zdarz_wejscie_2026-01-27_DU-2026-26_domyslne`),
pochodzeniem (`domyslne-z-aktu` dla daty wyliczonej z formuły, `odczytane` dla daty podanej wprost),
formułą (`warunek`), klauzulą (`podstawa`, `dowod`) i jednostkami wyjątku (`JednostkaAktu`,
z `JednostkaWZakresie` dla zakresu). Każde zdarzenie ma grupę czasową
(`GrupaCzasowa id="okr_obowiazywanie.<zdarzenie>.x"`), a jednostka, zdanie i część wspólna wskazują
swoją grupę atrybutem `okres`. Schemat pilnuje tych powiązań kluczami: `okres` musi wskazywać
istniejącą grupę, a przedział grupy — istniejące zdarzenie.

## Decyzja

**Daty aktu eksportujemy jako zdarzenia cyklu życia** (`LifecycleEvents`, `TemporalDataXmlConverter`):

- jedno zdarzenie `wejscie-w-zycie` na każdą różną datę wejścia w życie — reguły ogólnej i wyjątków;
- jedno zdarzenie `stosowanie-z-moca-wsteczna` na każdą datę mocy wstecznej;
- zdarzenie reguły ogólnej nie wymienia jednostek, zdarzenie wyjątku wymienia swoje (z zakresem);
- wyjątki o tej samej dacie i pochodzeniu trafiają do jednego zdarzenia; wyjątek o dacie reguły
  ogólnej nic do niej nie dodaje;
- akt bez żadnej rozstrzygniętej daty nie ma `DaneCzasowe` wcale.

**Każdy datowany element wskazuje grupę czasową atrybutem `okres`** (`TemporalDataXmlConverter.AssignPeriods`):

- grupa jednostki z samą datą wejścia w życie: `okr_obowiazywanie.<zdarzenie>.x` z przedziałem
  `obowiazywanie` od zdarzenia — co do znaku jak w przykładach autorów schematu;
- grupa jednostki z mocą wsteczną: przedział `stosowanie-z-moca-wsteczna` od zdarzenia mocy wstecznej
  do zdarzenia wejścia w życie, potem `obowiazywanie`;
- główna (`glowna="true"`) jest grupa reguły ogólnej;
- o grupie decydują **obie** daty jednostki, choć atrybut `wejscie-w-zycie` niesie jedną (ADR-0013).
  Konwertery zostawiają je przy elemencie jako adnotację (`UnitDates`), a grupy przypisuje jeden
  przebieg po gotowym drzewie.

Elementy `DataWejsciaWZycie` i `DatyMocyWstecznej` znikają. Reszta ADR-0013 obowiązuje: model niesie
obie daty osobno, a jednostka i zdanie dostają w `wejscie-w-zycie` jedną — moc wsteczną, jeśli jest.

## Odrzucone alternatywy

- **Zostawić dawne elementy obok `DaneCzasowe`.** Dokument nadal nie przechodziłby walidacji, a ta sama
  data stałaby w dwóch miejscach.
- **Tylko zdarzenie reguły ogólnej.** Zgodne ze schematem i najprostsze, ale wyjątki zostałyby poza
  metadanymi, choć model je zna (`EntryIntoForceInfo.Partials`), a przykłady autorów schematu je niosą.
- **Moc wsteczna jako atrybut zdarzenia wejścia w życie.** Schemat nie ma takiego atrybutu; ma osobny
  rodzaj zdarzenia i osobny rodzaj przedziału (`stosowanie-z-moca-wsteczna`).

## Konsekwencje

- Zmienia wyjście: metadane każdego aktu z rozstrzygniętą datą.
- Powołania jednostek w zdarzeniach niosą `eId` — ten sam napis co jednostka w treści. Kto zbiera
  elementy po `eId`, musi pominąć metadane.
- `podstawa` i `dowod` wskazują jednostkę z klauzulą wejścia w życie, tę samą dla wszystkich zdarzeń.
  Przykłady autorów schematu wskazują dokładniej — punkt klauzuli, z którego pochodzi wyjątek
  („art. 43 pkt 2"); model nie zna dziś miejsca pojedynczego wyjątku.
- Wyjątek bez rozstrzygniętej daty („z dniem określonym w komunikacie…") nie ma zdarzenia — zostaje
  w `EntryIntoForceInfo.UnresolvedFragments` i jako brak daty na jednostkach (ADR-0011).
- Zdarzenie i grupa mocy wstecznej nie mają odpowiednika w przykładach autorów schematu — kształt
  i identyfikator (`okr_moc-wsteczna.<zdarzenie mocy>.obowiazywanie.<zdarzenie wejścia>.x`) wynikają
  ze słownika schematu, nie ze wzorca.
- Jednostka, której data nie odpowiada żadnemu zdarzeniu, nie dostaje `okres` — zostaje jej sama data.
- Elementy z datą powstają w wielu konwerterach, a `okres` w jednym przebiegu na końcu. Nowy konwerter
  musi pisać datę przez `UnitLifecycleXml.Apply`, inaczej jego element nie dostanie grupy.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0022_ActDates_AreLifecycleEvents_NotAdHocMetadataElements`.
- `ActSchemaTests` — cały dokument z wyjątkami, zakresem i mocą wsteczną przechodzi walidację XSD
  (z kluczami `okres` → grupa → zdarzenie); identyfikator grupy reguły ogólnej równy temu z przykładu
  autorów schematu; wyjątki z własnymi grupami.
- `CorpusValidator --compare-reference` — sekcja „Daty i okresy jednostek" wobec przykładów autorów schematu.
- `ArchitectureDecisionTests.Adr0013_RetroactiveDate_MasksEntryDateOnlyInOutput` — obie daty aktu
  osobno w metadanych.
