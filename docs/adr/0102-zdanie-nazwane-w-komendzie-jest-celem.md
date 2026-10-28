# ADR-0102: Zdanie nazwane w komendzie z numerem jest celem (`…__zdn_N`) każdej zmiany, nie tylko uchylenia

- Status: Accepted (Supersedes w części ADR-0070: zmiany inne niż uchylenie zdania) — Superseded in part by [ADR-0127](0127-czesc-wspolna-jako-miejsce-zmiany-ma-adres-czw.md) (zdanie w części wspólnej)
- Data: 2026-10-20

## Kontekst

[ADR-0070](0070-uchylane-zdanie-jest-adresowane-zdaniem-nie-jednostka.md) zostawił zdanie w adresie celu tylko przy
uchyleniu („w ust. 2 skreśla się zdanie drugie" → `…__ust_2__zdn_2`). Zastąpienie zdania („zdanie drugie otrzymuje
brzmienie:"), dodanie zdania („dodaje się zdanie trzecie") i zmiana wyrazów w zdaniu („w zdaniu pierwszym wyrazy …
zastępuje się …") wskazywały całą jednostkę — jak w części przykładów autorów schematu. W korpusie DOCX 258 komend
w 92 dokumentach, w 49 z nich to jedyna wada.

Cel w całej jednostce nie jest tylko nieprecyzyjny: `wystapienie` ma domyślnie wartość `wszystkie`, więc zmiana wyrazów
trafia też w zdania, których komenda nie wymienia. W korpusie wyrazy „ust. 3b pkt 1 lub 2," stoją w obu zdaniach art. 8
ust. 3c zmienianej ustawy (302298), a komenda dotyczy zdania pierwszego (308909).

Ustalenie z 2026-10-19 (pytanie 7 w `docs/internal/odpowiedzi-autorow-schematu.md`, wyprowadzone z zasady autorów
schematu o celach, których `eId` nie zaadresuje; `gd:zdGUID` wymaga zapisu aktu zmienianego): gdy komenda podaje numer
zdania, numer idzie do `eId` (`…__zdn_N`); gdy nie podaje — instrukcja `*-recznie` z `eId` jednostki.

## Decyzja

**Zdanie nazwane w komendzie z numerem zostaje w celu każdej zmiany. Szczegóły rozstrzyga `SentenceTargets`:**

- zastąpienie i zmiana wyrazów w zdaniu — `…__ust_2__zdn_2` (odczyt adresu zostawia zdanie: `LegalReferenceService`);
- zdanie dodawane („w ust. 7 dodaje się zdanie trzecie") — `dodaj-po` na zdaniu poprzednim (`…__ust_7__zdn_2`);
  „po zdaniu pierwszym dodaje się zdanie" niesie miejsce w samym adresie; „dodaje się zdanie drugie i trzecie" — jeden
  cytat po zdaniu pierwszym;
- kilka sąsiednich zdań jednej jednostki zastąpionych jednym cytatem („w § 1 zdanie drugie i trzecie otrzymują
  brzmienie:") — jedna instrukcja z zakresem `element-eId`/`element-do-eId`;
- zdania niesąsiednie („w zdaniu pierwszym i trzecim wyrazy …") — instrukcja na każde zdanie, nigdy zakres;
- zdanie bez numeru („w zdaniu ostatnim", „dodaje się zdanie w brzmieniu:") i zdanie pierwsze dodawane — instrukcja
  ręczna (`*-recznie`, [ADR-0100](0100-cel-w-zalaczniku-bez-adresu-to-instrukcja-reczna.md)) z adresem jednostki.

`zdn_N` to N-te zdanie jednostki w rachubie prawodawcy, więc eksport aktu zmienianego musi numerować zdania tak samo.

## Odrzucone alternatywy

- **Cel w jednostce, numer zdania tylko w treści cytowanej** (ADR-0070 dla zmian innych niż uchylenie). Zmiana wyrazów
  dotykała zdań niewymienionych, a dodanie zdania nie mówiło, po którym.
- **Instrukcja na każde z sąsiednich zdań z cytatem podzielonym według kolejności.** Też poprawna (uznana w ustaleniu),
  ale zakłada tyle samo zdań nowych co starych; prawodawca zastępuje ciągły fragment jednym tekstem.
- **`gd:zdGUID`.** Wymaga zapisu aktu zmienianego — droga dla przyszłych aktów, nie dla konwertera aktu zmieniającego.

## Konsekwencje

- Zdanie w adresie celu wskazuje miejsce zmiany, nie gospodarza cytowanych jednostek — adresy jednostek w cytacie
  liczą się bez niego (`AmendmentTargetPairing`).
- Zdanie w części wspólnej i w jednostce bez `eId` (część załącznika) idzie za ogólnymi regułami odczytu adresu
  i ADR-0100.

## Weryfikacja

`ArchitectureDecisionTests.Adr0102_NamedSentence_IsTheTargetOfEveryChange`, `SentenceTargetTests` — zastąpienie, zmiana
wyrazów, dodanie, zakres sąsiednich zdań, zdania niesąsiednie, zdanie bez numeru, zgodność ze schematem;
`LegalReferenceServiceTests.ReadAmendmentTargets_NamedSentences_StayInTheTarget`,
`LegalReferenceServiceTests.ReadAmendmentTargets_SentenceNamedByTheCommand_StaysInTheTarget`.
