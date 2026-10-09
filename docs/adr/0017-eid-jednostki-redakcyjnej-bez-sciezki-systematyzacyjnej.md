# ADR-0017: eId jednostki redakcyjnej nie zawiera ścieżki jednostek systematyzacyjnych

- Status: Accepted
- Data: 2026-09-25

## Kontekst

`BaseEntity.Id` budował eId od encji do korzenia i zbierał po drodze każdą jawną jednostkę —
także systematyzacyjną. Artykuł 5 w rozdziale 1 dostawał więc `eId="rozdz_1__art_5"`, a jego
punkt — `rozdz_1__art_5__ust_2__pkt_1`. Tak wychodziło to do eksportu XML (atrybut `eId`) i do
goldenu aktu referencyjnego.

Tymczasem każdy adres **odczytywany z tekstu** — cel nowelizacji (`element-eId`), cel wyjątku
wejścia w życie — ma postać `art_5__ust_2__pkt_1`, bo powołanie nie wymienia rozdziału
(„w art. 5 ust. 2 pkt 1", § 59 ZTP). Nie może go wymieniać: numeracja artykułów jest ciągła
w całej ustawie (§ 57 ust. 1 ZTP), więc numer artykułu wystarcza do identyfikacji, a parser
czytający komendę nowelizacyjną nie zna struktury aktu zmienianego. Dwa napisy dla tej samej
jednostki oznaczały, że `element-eId` nowelizacji nigdy nie trafiał w eId artykułu aktu z rozdziałami.

Przykłady autorów schematu wyjściowego (`przyklady/ustawa_zastaw_rejestrowy.xml` w repozytorium
akn4pl) zapisują to samo: `<rozdzial eId="rozdz_1">` → `<oddzial-n>` → `<artykul eId="art_1">`.

## Decyzja

**eId jednostki redakcyjnej kończy się na niej samej i jej przodkach redakcyjnych** — budowa
zatrzymuje się na pierwszym przodku systematyzacyjnym: `art_5__ust_2__pkt_1`, niezależnie od
rozdziału, działu czy tytułu, w którym artykuł stoi.

**eId jednostki systematyzacyjnej niesie ścieżkę jej jawnych przodków systematyzacyjnych**
(oddział 1 rozdziału 3: `rozdz_3__oddz_1`) — jak dotąd. Jednostki niejawne nie wnoszą segmentu.

Miejsce artykułu w strukturze aktu niesie drzewo (`Parent`) i zagnieżdżenie w eksporcie, nie eId.

## Odrzucone alternatywy

- **Ścieżka w eId artykułu (stan poprzedni).** Adres z powołania nie zna rozdziału, więc wymagałby
  rozstrzygania względem struktury aktu zmienianego, której parser nie ma — przetwarza jeden akt
  naraz. Do tego eId artykułu zmieniałby się przy każdym dodaniu jednostki systematyzacyjnej
  nowelizacją („po rozdziale 2 dodaje się rozdział 2a"), choć sam artykuł się nie zmienił.
- **Pełna ścieżka w `eId`, adres z powołania w drugim atrybucie (`adres` z grupy `eId` w XSD).**
  Dwa identyfikatory tej samej jednostki; konsument musiałby wiedzieć, którego użyć do wiązania
  `element-eId`, a przykłady autorów schematu nie używają tu `adres`.
- **Płaskie eId także jednostek systematyzacyjnych (`rozdz_1` bez działu).** W kodeksach numeracja
  rozdziałów zaczyna się od nowa w każdym dziale (Kodeks pracy: „Dział drugi" / „Rozdział I"),
  więc płaskie eId rozdziałów byłyby zduplikowane w jednym akcie.

## Konsekwencje

- **Zmiana kontraktu wyjścia**: w każdym akcie z jawną jednostką systematyzacyjną zmieniają się eId
  wszystkich jednostek redakcyjnych (`rozdz_1__art_5` → `art_5`). Golden aktu referencyjnego
  przegenerowany — różnica to wyłącznie zdjęty prefiks.
- Umiejscowienia artykułu w strukturze nie da się już odczytać z jego eId. Testy sprawdzają je
  przez `Parent` — `ModelEquivalenceComparer` wypisuje ścieżkę osobno (`in=`), inaczej ekwiwalencja
  DOCX/TXT przestałaby widzieć artykuł w złym rozdziale.
- `SystematizingUnitBuilder.CanClaim` zostaje: artykuł stojący w akcie przed pierwszym rozdziałem
  nie może trafić do tego rozdziału — to kwestia drzewa i eksportu, nie eId.
- Unikalność eId artykułów w akcie opiera się na ciągłej numeracji z § 57 ust. 1 ZTP. Duplikaty,
  które mimo to występują (np. przepisy przytoczone w obwieszczeniu tekstu jednolitego), mają inne
  przyczyny i są osobnym problemem.
- Treść nowelizacji (`AmendmentContent`) nie ma przodków systematyzacyjnych — bez zmian.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0017_EditorialEId_IgnoresSystematizingPath_AndMatchesTheReferenceAddress`
  — eId artykułu i punktu w oddziale rozdziału bez ścieżki, a adres z „w art. 5 ust. 2 pkt 1"
  równy eId wskazanego punktu.
- `SystematizingUnitTests` — umiejscowienie artykułów sprawdzane przez `Parent` (`UnitPath`), eId
  artykułów bez prefiksu.
- `ParserEquivalenceTests.ActWithWrapUpAndSystematizingUnits_DocxAndTxt_AreEquivalent` — strażnik
  `eId=art_2 in=rozdz_2__oddz_1`.
