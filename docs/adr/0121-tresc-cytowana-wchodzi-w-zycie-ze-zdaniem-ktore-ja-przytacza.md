# ADR-0121: Treść cytowana nowelizacji wchodzi w życie ze zdaniem, które ją przytacza

- Status: Accepted — Supersedes [ADR-0011](0011-data-ogolna-rozsylana-po-calym-drzewie.md) w części o dacie nowelizacji
- Data: 2026-10-27

## Kontekst

[ADR-0011](0011-data-ogolna-rozsylana-po-calym-drzewie.md) datuje `Amendment` i jednostki jej treści cytowanej datą
rządzącą jednostki zmieniającej („nowelizacja wchodzi w życie razem z jednostką, która ją zarządza"). Eksport dopisuje
cytat do ostatniego zdania jednostki. Dwie rzeczy się z tym nie zgadzały:

- Zdanie przytoczone w `cytat-w-tekscie` wychodziło bez `wejscie-w-zycie` i `okres`, choć przykłady autorów schematu
  dają mu daty zdania, w którym stoi (Dz. U. 2026 poz. 26, art. 11 i art. 12 pkt 7; `ustawa_zmieniajaca.xml`).
- Wyjątek klauzuli może wskazywać zdanie, a nie jednostkę („z wyjątkiem art. 1 pkt 2 zdanie pierwsze, które wchodzi
  w życie …”, „z tym że art. 1 pkt 2 zdanie pierwsze z mocą od dnia …”). Zdanie dostawało wtedy datę wyjątku, a cytat
  w nim — strukturalny i tekstowy — datę jednostki, choć jest częścią tego zdania.

## Decyzja

- Nowelizacja i jednostki jej treści cytowanej dziedziczą obie daty **zdania, które ją przytacza**: ostatniego
  segmentu jednostki (`TextSegments`), do którego eksport dopisuje cytat. Bez wyjątku na zdaniu to te same daty co
  jednostki; wyjątek na tym zdaniu przechodzi na cytat, wyjątek na wcześniejszym zdaniu — nie
  (`EffectiveDatePropagator`, `EditorialUnitWalker.AmendmentContent`).
- Zdanie przytoczone w `cytat-w-tekscie` niesie daty nowelizacji (`AmendmentXmlConverter.AddTextCitation`), więc
  `wejscie-w-zycie` i `okres` ma takie jak zdanie, w którym stoi.
- Wyjątek „w zakresie” wskazujący jednostkę cytatu nadal nadpisuje jej datę, jak w ADR-0011.

## Odrzucone alternatywy

- **Data jednostki zmieniającej dla cytatu** (dotychczasowa reguła). Przy wyjątku wskazującym zdanie cytat
  wchodziłby w życie inaczej niż zdanie, które go przytacza.
- **Kopiowanie atrybutów zdania-gospodarza przy eksporcie zdania przytoczonego.** Naprawiałoby tylko
  `cytat-w-tekscie`: jednostki `cytat-strukt` zostałyby z datą jednostki, a model nie wiedziałby, kiedy cytat wchodzi
  w życie.

## Konsekwencje

- Korpus DOCX: 151 zdań przytoczonych w 62 plikach dostaje daty równe zdaniu, w którym stoją; 19 w tekstach
  jednolitych zostaje bez dat ([ADR-0033](0033-tekst-jednolity-ma-zdarzenie-ujednolicenia.md)). Poza tym eksport się
  nie zmienia. Wyjątku wskazującego zdanie z cytatem korpus nie ma — regułę pilnują testy.
- Dz. U. 2026 poz. 26: daty zgodne z art. 43 aktu i z przykładem autorów na 967 z 968 elementów; brakujące to
  wprowadzenie do wyliczenia w art. 11, które wychodzi jako `tekst` bez dat
  ([ADR-0057](0057-zdanie-przytoczone-w-calosci-jest-zdaniem.md)).
- Wyjątek „w zakresie” wskazujący cel cytatu tekstowego („art. 1 pkt 2 w zakresie art. 127 ust. 2 zdanie drugie”)
  nadal nie trafia: treść tekstowa nie ma jednostek do wskazania (backlog, sekcja 3b).

## Weryfikacja

- `ArchitectureDecisionTests.Adr0121_QuotedContent_EntersIntoForceWithTheSentenceThatQuotesIt`
- `EffectiveDatePropagatorTests.ExceptionOnTheQuotingSentence_ReachesTheAmendmentAndItsQuotedContent_NotTheUnit`,
  `EffectiveDatePropagatorTests.ExceptionOnAnEarlierSentence_LeavesTheQuotedContentWithTheUnit`
- `ActSchemaTests.QuotedSentence_TakesTheDateAndGroupOfTheSentenceItStandsIn`,
  `ActSchemaTests.QuotedSentence_TakesTheRetroactivityOfTheSentenceItStandsIn`
