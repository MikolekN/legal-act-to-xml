# ADR-0033: Tekst jednolity ma jedno zdarzenie `ujednolicenie`, a jego jednostki nie niosą dat wejścia w życie

- Status: Accepted (jednostki drukowane w dwóch wersjach niosą okres i mają własne zdarzenia — uściśla to [ADR-0060](0060-jednostka-drukowana-dwa-razy-to-dwie-wersje-rozrozniane-okresem.md))
- Data: 2026-10-11

## Kontekst

Daty jednostek aktu wynikają z klauzuli wejścia w życie i daty ogłoszenia dokumentu
([ADR-0022](0022-daty-aktu-jako-zdarzenia-cyklu-zycia.md)): „po upływie 14 dni od dnia ogłoszenia" liczy się
od daty z nagłówka dziennika.

W tekście jednolitym te dwie rzeczy pochodzą z różnych aktów. Przepis końcowy („Art. 14. Ustawa wchodzi
w życie z dniem ogłoszenia") mówi o ogłoszeniu **ustawy** — sprzed lat — a data w nagłówku dziennika jest
datą ogłoszenia **obwieszczenia**. Potok łączył jedno z drugim: tekst jednolity ogłoszony 11 sierpnia 2025 r.
dostawał zdarzenie „wejście w życie 2025-08-11" i tę datę na każdej jednostce. Dopóki treść obwieszczenia
stała w akcie ([ADR-0031](0031-tekst-jednolity-pod-korzeniem-obwieszczenie.md)), do wyliczenia wchodziły też
przytoczone tam przepisy końcowe ustaw zmieniających (289 zdarzeń w korpusie).

Przykład autorów schematu (`DU20261261_obw_tj.xml`) ma w `DaneCzasowe` jedno zdarzenie
`rodzaj="ujednolicenie"` z datą ogłoszenia obwieszczenia i grupę `okr_ujednolicenie.…`; żadna jednostka
tekstu jednolitego nie ma tam ani `wejscie-w-zycie`, ani `okres`.

## Decyzja

**Dla tekstu jednolitego klauzula wejścia w życie nie jest czytana. Dokument ma jedno zdarzenie
`ujednolicenie` w dniu ogłoszenia obwieszczenia i jedną, główną grupę czasową; jednostki nie niosą dat.**

- Tekst jednolity rozpoznaje `Announcement.IsConsolidatedText` (obwieszczenie „w sprawie ogłoszenia jednolitego
  tekstu …" z aktem w załączniku).
- Identyfikatory jak w przykładzie: zdarzenie `zdarz_ujednolicenie_2025-08-11_DU-2025-1094_domyslne`, grupa
  `okr_ujednolicenie.<zdarzenie>` z przedziałem `rodzaj="ujednolicenie"`.
- Gdy dokument nie ma daty ogłoszenia (brak nagłówka dziennika), nie ma też zdarzenia — daty się nie zgaduje.

## Odrzucone alternatywy

- **Zostawić wyliczenie od daty ogłoszenia obwieszczenia.** Daje datę, której ustawa nigdy nie miała, i to na
  każdej jednostce — błąd niewidoczny w walidacji, bo data jest poprawna formalnie.
- **Odczytać datę ogłoszenia ustawy z odnośnika albo z tytułu** („ustawa z dnia …", „Dz. U. z 2018 r. poz. …")
  i od niej liczyć wejście w życie. Tekst jednolity zbiera przepisy o różnych datach wejścia w życie (każda
  nowelizacja ma własną), a przepisy końcowe nowelizacji są w nim pominięte — jedna data dla całości byłaby
  fałszywa dla większości jednostek, a dane do dat poszczególnych jednostek nie są w dokumencie.
- **Daty tylko tam, gdzie przepis podaje je wprost** („z dniem 1 stycznia 1992 r."). Dawałoby to tekst
  jednolity z datami na części jednostek i bez dat na reszcie, bez reguły, którą dałoby się sprawdzić.

## Konsekwencje

- Jednostki 419 tekstów jednolitych ustaw tracą `wejscie-w-zycie` i `okres`; `DaneCzasowe` tych dokumentów
  ma jedno zdarzenie zamiast zdarzeń wejścia w życie i wyjątków.
- Miara „elementy bez daty" (`CorpusValidator --xml-corpus`) nie liczy tekstów jednolitych — brak daty nie
  jest w nich brakiem; raport podaje ich liczbę osobno.
- `DataStanuPrawnegoTJ` („z uwzględnieniem stanu prawnego na dzień …") i `DataObowiazywaniaWersji` nie są
  jeszcze nadawane (backlog).
- Historia obowiązywania poszczególnych przepisów tekstu jednolitego nie wynika z tego dokumentu — wymaga
  aktów zmieniających, czyli pracy poza parserem jednego pliku.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0033_ConsolidatedText_IsAConsolidationEvent_NotAnEntryIntoForce`.
- `AnnouncementTests.ConsolidatedText_HasOneConsolidationEvent_AndNoUnitDates`,
  `AnnouncementTests.ConsolidatedStatute_IsSchemaValid_AsAWhole`.
