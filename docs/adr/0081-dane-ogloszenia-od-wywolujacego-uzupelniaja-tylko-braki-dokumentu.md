# ADR-0081: Dane ogłoszenia podane przez wywołującego uzupełniają tylko to, czego dokument nie podaje

- Status: Accepted
- Data: 2026-10-14

## Kontekst

Dziennik, rocznik, pozycję i dzień ogłoszenia parser czyta z nagłówka publikatora ([ADR-0012](0012-naglowek-strony-jako-rola-bloku.md))
albo z wiersza przyłączenia ([ADR-0073](0073-akt-bez-naglowka-dziennika-bierze-adres-z-wiersza-przylaczenia.md)). Szczotka przygotowywana przed publikacją ich nie ma
(„Poz. 000", „Warszawa, dnia <data ogłoszenia> r.") albo nie ma nagłówka wcale — a właśnie szczotki konwertuje się
przed ogłoszeniem. Schemat wymaga `Rok` i `Pozycja`, więc eksport takiej szczotki jest niezgodny (41 dokumentów
korpusu), eId nie mają prefiksu aktu, a formuły „po upływie 14 dni od dnia ogłoszenia" zostają bez daty. Pozycję
i dzień ogłoszenia zna dopiero wydawca dziennika — usługa i konsola nie miały jak ich przyjąć.

## Decyzja

**Wywołujący może podać dane ogłoszenia (`ParseOptions.Publication`: dziennik, rocznik, pozycja, dzień ogłoszenia;
w usłudze pola formularza `publisher`, `year`, `position`, `announcementDate`, w konsoli `--publisher`, `--year`,
`--position`, `--announced`). Uzupełniają wyłącznie to, czego dokument nie podaje: nagłówek publikatora i wiersz
przyłączenia wygrywają.**

- Dzień ogłoszenia wchodzi przed budową modelu, więc wejście w życie liczy się od niego; dziennik, rocznik
  i pozycja — po budowie, gdy wiersz przyłączenia miał już szansę je podać. Rocznik bez wartości bierze się z dnia
  ogłoszenia.
- Wartości sprawdza jedna fabryka (`PublicationData.TryCreate`) dla usługi i konsoli: błędna wartość to odpowiedź
  400 z opisem, nie cicha poprawka.
- W konsoli dane ogłoszenia przyjmuje tylko pojedynczy plik — jeden akt ma jedną pozycję.

## Odrzucone alternatywy

- **Dane wywołującego nadpisują dokument.** Pomyłka w formularzu zmieniałaby adres aktu ogłoszonego, którego nagłówek
  podaje go wprost; po ogłoszeniu dokument jest źródłem prawdy.
- **Wartości zastępcze w eksporcie** (rok z daty aktu, „poz. 0"). Zapisany jak fakt adres, którego akt nie ma —
  zgodność ze schematem kosztem prawdy.
- **Dane po parsowaniu, z ponownym przeliczeniem wejścia w życie** (jak data syntetyczna `CorpusValidator`). Drugi
  przebieg dopisuje komunikaty walidacji do pierwszego; dzień podany przed budową daje jeden, spójny przebieg.

## Konsekwencje

- Szczotka z podanymi danymi jest zgodna ze schematem, ma adresy z prefiksem aktu i rozstrzygnięte daty wejścia
  w życie. Miara korpusu (`CorpusValidator --xml-corpus`) danych nie podaje, więc 41 szczotek zostaje niezgodnych —
  to stan wejścia, nie eksportu.
- Bez danych od wywołującego eksport jest taki sam jak przedtem.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0081_GivenPublication_FillsOnlyWhatTheDocumentDoesNotState`
- `PublicationDataTests` — odczyt i walidacja wartości, uzupełnienie szczotki, pierwszeństwo nagłówka, wejście
  w życie, adres i zgodność ze schematem.
