# ADR-0051: Cudzysłów graniczny cytatu w komórce cytowanej tabeli należy do cytatu; kolumna, w której stał sam, znika

- Status: Accepted
- Data: 2026-10-12

## Kontekst

[ADR-0050](0050-cudzyslowy-cytowanej-tabeli-licza-sie-w-bilansie-cytatu.md) liczy cudzysłowy cytowanej tabeli w bilansie cytatu, ale zostawił je w tabeli:
komórka z samym „„" albo „”," wychodziła w eksporcie jako komórka, atrybut `cudzyslowy` cytatu jej nie widział,
a interpunkcja komendy („,", „;") zostawała w komórce zamiast za cytatem. Z akapitów treści cytowanej cudzysłowy
graniczne są zdejmowane od zawsze (`AmendmentStateManager.TrimQuoteBoundaries`).

Redaktorzy stawiają cudzysłów graniczny w osobnej, dodatkowej kolumnie: przed pierwszym wierszem (otwierający)
albo za ostatnim (zamykający), pustej we wszystkich pozostałych wierszach. Tabela aktu zmienianego tej kolumny
nie ma.

## Decyzja

- **Cudzysłów graniczny stojący w komórce jest zdejmowany z tabeli tak jak z akapitu**
  (`QuotedTableBoundaries`): otwierający z pierwszej niepustej komórki tabeli, od której zaczyna się treść
  cytowana, zamykający — z interpunkcją komendy — z ostatniej niepustej komórki tabeli, na której treść się
  kończy. Trafia do `Amendment.HasOpeningQuote` / `HasClosingQuote` / `TrailingPunctuation`.
- **Granicą jest tylko cudzysłów niesparowany w swojej komórce.** Wyrażenie w cudzysłowie zamknięte w tej samej
  komórce („forma prawna „spółka akcyjna"") zostaje treścią tabeli.
- **Kolumna, w której nie było nic oprócz cudzysłowu, znika razem z nim**: skrajna komórka każdego wiersza jest
  pusta i niescalona. Gdy skrajna kolumna ma treść w innym wierszu albo jej komórka jest scalona, zostaje —
  znika sam znak.

## Odrzucone alternatywy

- **Zostawić cudzysłów w komórce.** Eksport pokazuje wtedy tabelę z kolumną, której akt zmieniany nie ma,
  a cytat jako nieujęty w cudzysłów; interpunkcja komendy stoi w treści cytowanej.
- **Zdjąć znak, zostawić pustą kolumnę.** Pusta kolumna jest wyłącznie nośnikiem znaku; kto zastosuje zmianę,
  wstawi do aktu tabelę o jedną kolumnę szerszą.
- **Zdejmować każdy cudzysłów z końca ostatniej komórki.** Większość cytowanych tabel nie niesie cudzysłowu
  cytatu, a ostatnia komórka bywa zakończona wyrażeniem w cudzysłowie — zdjęcie go psułoby treść.

## Konsekwencje

- W korpusie zmieniają się dwa rozporządzenia (cztery pliki): z cytowanych tabel znika 346 komórek kolumn, w których stał wyłącznie cudzysłów, a cytaty dostają w eksporcie cudzysłów otwierający albo zamykający, którego dotąd nie miały. Zgodnych ze schematem jest nadal 1937 z 2321, pokrycie tekstu bez zmian; żadna inna tabela korpusu się nie zmienia.
- Akapity treści cytowanej zostają przy dotychczasowej regule (cudzysłów z końca ostatniego akapitu jest
  zdejmowany bez sprawdzania pary) — ujednolicenie to osobna zmiana.
- Prosty cudzysłów jest dwuznaczny: liczy się, gdy w komórce jest ich nieparzysta liczba, zgodnie z miejscem
  (na początku albo na końcu).

## Weryfikacja

- `ArchitectureDecisionTests.Adr0051_BoundaryQuotationMarkInACell_BelongsToTheQuotation`
- `UnitContinuationTests` — cudzysłów w osobnej kolumnie (otwierający i zamykający), cudzysłów obok tekstu
  komórki, wyrażenie sparowane w ostatniej komórce, kolumna z treścią w innym wierszu, komórka scalona, tabela
  bez cudzysłowów.
