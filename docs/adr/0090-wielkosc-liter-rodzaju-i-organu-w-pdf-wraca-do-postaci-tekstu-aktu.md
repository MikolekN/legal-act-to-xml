# ADR-0090: Wielkość liter rodzaju i organu w PDF wraca do postaci tekstu aktu

- Status: Accepted
- Data: 2026-10-15

## Kontekst

Skład Dziennika Ustaw drukuje nagłówek rodzaju i organ wielkimi literami („ROZPORZĄDZENIE MINISTRA FINANSÓW
I GOSPODARKI"). W DOCX to najczęściej formatowanie: tekst akapitu to „Rozporządzenie", „Ministra Finansów
i Gospodarki", a wielkie litery nakłada styl. PDF ma już tylko to, co wydrukowano — wielkie litery. Przykłady autorów
schematu (`legal-doc-schema/przyklady`) mają w `Tytul` i `rodzaj-aktu` postać tekstu („Ustawa", „Ustawa z dnia …").
Eksporty DOCX korpusu: „Rozporządzenie" 935, „ROZPORZĄDZENIE" 411, „Obwieszczenie" 825, „OBWIESZCZENIE" 187; organ
z małymi literami w 2101 aktach, wielkimi w 286. Miara `--format-parity`: tytuł PDF różny od DOCX w 1798 z 2151
dokumentów, niemal zawsze tylko wielkością liter.

## Decyzja

Czytnik PDF przywraca wielkość liter bloku nagłówka rodzaju w strefie tytułowej (nagłówek rodzaju i organ), gdy blok
nie ma żadnej małej litery: rodzaj z wielką literą na początku, w organie każdy wyraz wielką literą poza spójnikami
i przyimkami („i", „oraz", „w", „z", „do", „na", „dla", „nr" …; „spraw" po „do"). Znacznik odnośnika w nawiasie
zostaje, jak stoi. Blok z małymi literami ma wielkość liter tekstu i zostaje bez zmian.

## Odrzucone alternatywy

**Zostawić wielkie litery, jak wydrukowano.** Odrzucone: to postać druku, nie tekstu aktu — tytuł w metadanych
i `rodzaj-aktu` różniłyby się od przykładów schematu i od większości eksportów DOCX tylko formatowaniem, którego PDF
nie przenosi.

**Ujednolicać wielkość liter w parserze dla wszystkich formatów.** Odrzucone: DOCX niesie tekst aktu wprost — gdy
autor wpisał „USTAWA", to tak stoi w tekście, a zmiana dotknęłaby wyniku DOCX. Wnioskowanie należy do czytnika
formatu, który tę informację traci.

**Słownik nazw organów.** Odrzucone na teraz: reguła wyrazów małą literą obejmuje nazwy organów z korpusu; słownik
trzeba by utrzymywać, a skrótowce w nazwie organu (np. „NBP") są w tytułach rzadkie.

## Konsekwencje

- Skrótowiec w nazwie organu zapisany w tytule wielkimi literami dostaje wielką tylko pierwszą literę.
- Akt, w którego DOCX autor wpisał rodzaj wielkimi literami, w PDF ma postać „Ustawa" — inną niż jego DOCX, a zgodną
  z przykładami schematu.
- DOCX i TXT bez zmian (eksport tekstu ma postać tekstu, nie druku).

## Weryfikacja

- `PdfBlockReaderTests.TitleZone_KindAndIssuerInCapitals_GetTheCaseOfTheActText`
- `PdfBlockReaderTests.TitleZone_ResolutionNumber_AndHeaderWithLowercase_AreKeptRight`
