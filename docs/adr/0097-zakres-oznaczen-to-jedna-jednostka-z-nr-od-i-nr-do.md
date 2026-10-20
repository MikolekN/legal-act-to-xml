# ADR-0097: Zakres oznaczeń („Art. 39–51. (pominięte)") to jedna jednostka z pustym `nr` i granicami w `nr-od`/`nr-do`, adresowana jak jej pierwsza jednostka

- Status: Accepted
- Data: 2026-10-20

## Kontekst

Tekst jednolity zachowuje numerację aktu pierwotnego (§ 106 pkt 1 ZTP), więc jednostki, których nie obejmuje, nadal
wymienia z oznaczeniem: uchylone z adnotacją „(uchylony)", pominięte (np. przepisy zmieniające inne ustawy) —
„(pominięty)". Kilka sąsiednich takich jednostek Kancelaria Sejmu drukuje pod jednym oznaczeniem: „Art. 39–51.
(pominięte)", „Art. 74–79. (uchylone)". W korpusie DOCX to 290 zakresów artykułów w 267 tekstach jednolitych; inne
rodzaje dokumentów ich nie mają.

`nr` niesie jedno oznaczenie. Parser czytał więc numer początku zakresu, a resztę („–51. (pominięte)") zostawiał
w treści jedynego ustępu; 12 z 13 artykułów zakresu znikało z oznaczeń eksportu.

Autorzy schematu (odpowiedzi z 2026-10-18 i 2026-10-20 na pytanie 20, zapis w `docs/internal/odpowiedzi-autorow-schematu.md`)
dodają do `JednostkaRedakcyjnaNumerowanaTJTyp` elementy `nr-od` i `nr-do`: `nr` zostaje pusty, `nr-od` 39, `nr-do` 51.
`eId` jednostki zakresu to `eId` jej pierwszej jednostki (`art_39`) — sygnał, że jednostki były w akcie pierwotnym,
a tekst jednolity je pominął (ustalone z autorem 2026-10-20). Zmiana schematu nie jest jeszcze opublikowana
w `legal-doc-schema`; właściciel projektu zdecydował (2026-10-20), że ustalenia uzgodnione z autorami wdraża się
w parserze przed publikacją, a zgodność sprawdza po aktualizacji schematu.

## Decyzja

**Zakres oznaczeń jest jedną jednostką. Pierwsze oznaczenie jest jej numerem (`Number`) i daje adres, ostatnie —
końcem zakresu (`BaseEntity.NumberTo`). Eksport pisze pusty `nr`, a za nim `nr-od` i `nr-do`.**

- Odczyt: za numerem artykułu myślnik w dowolnej postaci, numer i kropka, za nią odstęp, znacznik odnośnika albo
  koniec akapitu (`ParagraphClassifier.DesignationRangeEndCapture`, `ParsingFactories.ReadDesignationRange`).
  Pojedyncze oznaczenie kończy kropka tuż za numerem, więc myślnik z numerem w tym miejscu znaczy zakres; koniec
  mniejszy od początku zakresem nie jest.
- Adnotacji („(pominięte)") zakres nie wymaga: „Art. 266–280." z objaśnieniem w odnośniku (317037) też jest zakresem.
- Ciągłość numeracji (`NumberingHint`) liczy się po zakresie od jego końca.
- Jedno miejsce pisze oznaczenie: `UnitAddressXml.Number`.

## Odrzucone alternatywy

- **Osobna jednostka na każde oznaczenie zakresu** (art. 39, 40, …, 51 z tą samą adnotacją). Tekst jednolity drukuje
  jedną jednostkę; rozwinięcie dopisuje jednostki, których w nim nie ma, a dla zakresów z literami („37b–37ag") nie
  jest pewne.
- **Zakres w `nr`** („39–51"). Wzorzec `nr` zwykłych jednostek go nie przyjmuje, a autorzy schematu wybrali osobne
  elementy.
- **`eId` bez oznaczenia albo z całym zakresem.** `EIdTyp` nie przyjmuje myślnika; autor i właściciel projektu
  wybrali początek zakresu.
- **Czekać z eksportem na publikację schematu.** Decyzja jest uzgodniona; zwłoka zostawiałaby 267 tekstów
  jednolitych z oznaczeniem urwanym w treści.

## Konsekwencje

- Do czasu publikacji zmiany schematu i synchronizacji kopii `schema/` eksport tekstu jednolitego z zakresem jest
  niezgodny z kopią schematu (`nr-od`, `nr-do` nieznane). To stan oczekiwany; po synchronizacji trzeba go sprawdzić.
- Jednostki wewnątrz zakresu (art. 40–50) nie mają własnego `eId`; odwołanie do nich rozstrzyga się przez
  `nr-od`/`nr-do` jednostki zakresu.
- Zakres ustępów („3–5. (uchylone)" — 2 w jednym dokumencie korpusu) nie jest jeszcze rozpoznawany: klasyfikator
  nie czyta go jako oznaczenia ustępu, więc zostaje `nie-ztp-ustep` bez numeru (backlog).

## Weryfikacja

`ArchitectureDecisionTests.Adr0097_RangeOfDesignations_IsOneUnit_WithEmptyNumberAndBothEnds`,
`ArticleRangeTests` — odczyt końca zakresu (litera, odnośnik, wariant myślnika), eksport `nr`/`nr-od`/`nr-do`,
adres pierwszej jednostki, myślnik, który zakresem nie jest.
