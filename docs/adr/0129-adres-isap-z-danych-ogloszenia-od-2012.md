# ADR-0129: Adres ISAP składa eksport z danych ogłoszenia, tylko dla dzienników bez numerów (od 2012 r.)

- Status: Accepted
- Data: 2026-10-28

## Kontekst

Metadane schemaPL mają opcjonalny `AdresISAP` — wewnętrzny identyfikator systemu ISAP (pole `address` API ELI
Sejmu, w schemacie przykład „WDU20200001280”). Aktu nie da się zapytać o ten identyfikator, ale ISAP buduje go
mechanicznie z adresu publikacyjnego: „W”, symbol dziennika (`DU`, `MP`), rok, numer dziennika w trzech cyfrach
i pozycja w czterech. Sprawdzone w API: Dz.U. 2020 poz. 1280 → `WDU20200001280`, M.P. 2024 poz. 1050 →
`WMP20240001050`, Dz.U. 2012 poz. 1 → `WDU20120000001`, Dz.U. 2011 nr 289 poz. 1700 → `WDU20112891700`,
Dz.U. 1997 nr 88 poz. 553 → `WDU19970880553`. Od 2012 r. dzienniki nie mają numerów i w adresie stoi „000”.
Parser zna dziennik, rok i pozycje (`JournalInfo` — z nagłówka publikatora albo z danych ogłoszenia podanych
przy wywołaniu, [ADR-0081](0081-dane-ogloszenia-od-wywolujacego-uzupelniaja-tylko-braki-dokumentu.md)), ale numeru dziennika nie czyta.

## Decyzja

- `JournalInfo.IsapAddress()` składa adres z symbolu dziennika, roku i pierwszej pozycji, gdy rok jest co najmniej
  `JournalInfo.FirstUnnumberedYear` (2012), a pozycja mieści się w czterech cyfrach; inaczej zwraca `null`.
- `LegalDocumentConverter` zapisuje `AdresISAP` po `GUIDwersji` tylko wtedy, gdy adres jest; bez danych ogłoszenia
  (projekt, brak szczotki) elementu nie ma — schemat dopuszcza jego brak.

## Odrzucone alternatywy

- **Pobieranie adresu z API ISAP.** To wiedza spoza parsowanego aktu i zależność od usługi zewnętrznej, a wynik
  jest i tak funkcją danych, które parser zna.
- **„000” jako numer także przed 2012 r.** Daje adres, który wygląda poprawnie, a wskazuje inny akt albo żaden
  (`WDU20110001700` zamiast `WDU20112891700`) — gorsze niż brak elementu.
- **Pominięcie elementu w ogóle.** Pole jest opcjonalne, ale dla każdego aktu ogłoszonego od 2012 r. da się je
  wypełnić bez ryzyka, a ISAP i konsumenci posługują się tym identyfikatorem.

## Konsekwencje

- Akt ogłoszony przed 2012 r. nie ma `AdresISAP`, dopóki model nie przechowa numeru dziennika („Nr 88”) ze szczotki
  — w backlogu (wiersz „Metadane aktu”). Parser przetwarza akty nowo ogłaszane, więc dotyczy to tylko starszych wejść.
- 171 aktów ISAP: `AdresISAP` zgodny z identyfikatorem ISAP w każdym (część sprawdzona wprost w API), reszta
  eksportu i zgodność ze schematem bez zmian.
- Konwencja pochodzi z ISAP, nie z ZTP ani ze schematu; gdyby ISAP ją zmienił, poprawka jest w jednym miejscu.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0129_IsapAddress_IsBuiltFromTheAnnouncement_OnlyForUnnumberedJournals`
