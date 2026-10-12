# ADR-0050: Cudzysłowy cytowanej tabeli liczą się w bilansie cytatu, a akapit za tabelą cytowaną wprost jest jej objaśnieniem

- Status: Accepted (cudzysłów stojący w komórce, zostawiony tu w tabeli, zdejmuje z niej [ADR-0051](0051-cudzyslow-graniczny-w-komorce-tabeli-nalezy-do-cytatu.md))
- Data: 2026-10-12

## Kontekst

Cytowaną tabelę redaktorzy ujmują w cudzysłów także w niej samej: cudzysłów otwierający stoi w dodatkowej
komórce przed pierwszym wierszem, a zamykający — razem z interpunkcją komendy („”,", „”.") — w dodatkowej
komórce ostatniego wiersza. Bilans cudzysłowów (`QuoteBalanceTracker`) tabel nie widział: tabela „wyznaczała
swoje granice sama". Skutki:

- bez stylów cytat zamknięty w ostatniej komórce nie domykał się nigdy i wchłaniał kolejne jednostki aktu
  zmieniającego; [ADR-0049](0049-cytat-niedomkniety-do-konca-dokumentu-konczy-ponowny-przebieg.md) odtwarzał wtedy tylko granicę przed kolejnym artykułem,
  a jednostki między tabelą a tym artykułem zostawały w cytacie;
- tabela tuż po komendzie była całą treścią cytowaną, więc objaśnienie pod nią — akapit z cudzysłowem
  zamykającym — wypadało z cytatu jako dalsza treść przepisu zmieniającego, z osieroconym „”,". Także ze stylami,
  bo objaśnienie stylu zwykle nie ma.

## Decyzja

1. **Tabela jest tekstem cytatu.** `AmendmentCollector.AddTable` podaje bilansowi tekst tabeli jako jeden blok,
   na tych samych zasadach co akapit.
2. **Koniec tabeli zamyka cytat tylko wtedy, gdy zamyka go bilans** (`AmendmentStateManager.CollectTable`).
   Tabela tuż po komendzie jest całą treścią cytowaną — chyba że otwiera cudzysłów i go nie zamyka; cytat biegnie
   wtedy dalej, do cudzysłowu zamykającego. Tabela w środku treści cytowanej kończy cytat, gdy cudzysłów w jej
   ostatniej komórce domyka bilans.
3. **Akapit bez oznaczenia za tabelą cytowaną wprost, w tym samym cytacie, jest objaśnieniem pod tą tabelą**
   (`ContentTable.Description`, w eksporcie `opis-tabeli` — w schemacie „legenda/objaśnienie drukowane pod
   tabelą"). Buduje to `AmendmentBuilder.AttachText`.

## Odrzucone alternatywy

- **Liczyć tylko ostatnią komórkę albo wymagać interpunkcji po cudzysłowie.** To reguły szczególne dla komórek;
  cudzysłów otwierający w pierwszej komórce musi liczyć się tak samo. W korpusie na 299 cytowanych tabel
  cudzysłów zamykający w komórce mają 4, otwierający — 20, a znaku powtórzenia zapisanego cudzysłowem nie ma
  żadna.
- **Zostawić to odtwarzaniu granicy (ADR-0049).** Odtwarza tylko granicę przed kolejnym artykułem i zostawia
  ostrzeżenie o niedomkniętym cytacie, choć źródło jest poprawne.
- **Objaśnienie jako osobny tekst cytowany (`cytat-w-tekscie`).** Eksport pisze tekst cytowany przed tabelą,
  więc objaśnienie stanęłoby nad tabelą, do której się odnosi, i jako zastąpienie tekstu.

## Konsekwencje

- W korpusie ze stylami zmienia się jedno rozporządzenie (dwa pliki): 18 objaśnień pod cytowanymi tabelami przechodzi z `fragment` przepisu zmieniającego do `opis-tabeli`; zgodnych ze schematem jest nadal 1937 z 2321, pokrycie tekstu bez zmian. Bez stylów zgodność struktury rozporządzeń rośnie z 97,32 % do 97,35 %: rozporządzenie z cudzysłowem zamykającym w komórce tabeli (dwa pliki) nie gubi już jednostek aktu zmieniającego.
- Znak powtórzenia zapisany w komórce cudzysłowem liczyłby się w bilansie — tak jak liczyłby się w akapicie.
- Prosty cudzysłów w tabeli podlega tej samej regule co w akapicie, liczonej dla całej tabeli: nieparzysta liczba
  i prosty cudzysłów na końcu ostatniej komórki to jedno zamknięcie.
- Zostaje: komórka z samym cudzysłowem zostaje komórką eksportowanej tabeli, a atrybut `cudzyslowy` cytatu nie
  widzi cudzysłowu stojącego w komórce; tytuł nad cytowaną tabelą zostaje tekstem cytowanym (schemat ma na niego
  `tytul-tabeli`); objaśnienia pod tabelą poza cytatem zostają dalszą treścią jednostki.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0050_QuotationMarksOfAQuotedTable_CountInTheBalance`
- `UnitContinuationTests` — cudzysłów zamykający w ostatniej komórce (z przecinkiem, średnikiem, kropką), tabela
  otwierająca cudzysłów z objaśnieniem pod nią, cudzysłów sparowany w komórce, tabela otwierająca i zamykająca
  cudzysłów, eksport `opis-tabeli` i jego zgodność ze schematem.
