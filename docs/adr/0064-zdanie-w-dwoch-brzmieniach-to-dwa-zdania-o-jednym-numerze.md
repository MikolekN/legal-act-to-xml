# ADR-0064: Zdanie w dwóch brzmieniach to dwa zdania o jednym numerze i różnym okresie

- Status: Accepted
- Data: 2026-10-12

## Kontekst

Tekst jednolity drukuje w dwóch brzmieniach nie tylko całe jednostki ([ADR-0060](0060-jednostka-drukowana-dwa-razy-to-dwie-wersje-rozrozniane-okresem.md)), ale i pojedyncze
zdania: „Odbierający potwierdza odbiór … doręczenia.[26)] Jeżeli ustawa nie stanowi inaczej, odbierający potwierdza
odbiór … doręczenia.[27)]", z odnośnikami „Zdanie drugie w tym brzmieniu obowiązuje do wejścia w życie zmiany,
o której mowa w odnośniku 27" i „Zdanie drugie w brzmieniu ustalonym przez …; wejdzie w życie z dniem …".
Oba brzmienia wychodziły jako kolejne zdania jednostki — drugie i trzecie — bez informacji, że to jedno zdanie
w dwóch wersjach, a zdania za nimi dostawały numer o jeden za wysoki.

Schemat daje zdaniu tę samą grupę atrybutów czasu co jednostce (`okres`, `wejscie-w-zycie`, `obowiazuje-do`).

## Decyzja

**Zdanie wydrukowane w dwóch brzmieniach to dwa zdania o tym samym numerze i różnym `okres`; wersję niesie zdanie
(`TextSegment.Version`), nie jednostka.**

- **Miejsce znacznika**: odnośnik o zdaniu stoi **za** zdaniem, inaczej niż odnośnik o jednostce, który stoi za jej
  oznaczeniem. Podział na zdania zostawia znacznik przy zdaniu, które zamyka.
- **Treść odnośnika**: podmiot „Zdanie …" / „Zdania … i …", a po nim te same wzory co dla jednostki (§ 106
  i § 106a ZTP) — rozpoznaje je ta sama logika po odjęciu podmiotu.
- **Układ**: zdanie z takim odnośnikiem, stojące zaraz po brzmieniu obowiązującym, jest brzmieniem przyszłym tego
  samego zdania; dostaje jego numer (`TextSegment.Order`), a dalsze zdania liczą się dalej.
- Zdanie dodawane („Zdanie trzecie i czwarte dodane przez …; wejdzie w życie …") jest brzmieniem przyszłym bez
  obowiązującego.
- Termin, akt zmieniający i zdarzenie — jak dla jednostek (ADR-0062, ADR-0063).

## Odrzucone alternatywy

- **Wersja całej jednostki.** Jednostka jest wydrukowana raz; okres na niej objąłby zdania, które się nie zmieniają.
- **Kolejne numery zdań** (drugie i trzecie). Powołania mówią „zdanie drugie" o obu brzmieniach, a zdanie stojące
  za nimi jest w akcie trzecie.
- **Rozpoznawać po liczebniku z odnośnika** („drugie"). Miejsce znacznika mówi to samo bez czytania liczebnika,
  a liczebnik bywa złożony („trzecie i czwarte").

## Konsekwencje

- W korpusie zmienia się 6 tekstów jednolitych: 21 zdań niesie okres brzmienia obowiązującego, a 23 — przyszłego (wcześniej żadne). Zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory); treść bez zmian.
- Wychodzą jak dotąd: zdanie, którego drugie brzmienie stoi w osobnym akapicie (tak samo drukowane są
  wprowadzenie do wyliczenia i część wspólna w dwóch brzmieniach — dziś `fragment`), oraz zdanie uchylane bez
  drugiej kopii („Zdanie drugie uchylone przez …; obowiązuje do wejścia w życie zmiany …").

## Weryfikacja

- `ArchitectureDecisionTests.Adr0064_SentencePrintedTwice_IsOneSentenceInTwoWordings`
- `WordingVersionTests` — model (numery, wersje), eksport (`nr`, `okres`, projekcje, zdarzenie), zdania dodawane,
  odnośnik o jednostce za zdaniem, zgodność ze schematem.
