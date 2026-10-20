# ADR-0080: Komenda w załączniku wskazuje jednostki załącznika — miejsce „w załączniku nr N" stoi nad całą treścią aktu

- Status: Accepted — Superseded in part by [ADR-0099](0099-jedyny-zalacznik-bez-numeru-ma-adres-zal-1.md) (załącznik bez numeru), [ADR-0100](0100-cel-w-zalaczniku-bez-adresu-to-instrukcja-reczna.md) (cel w załączniku bez adresu)
- Data: 2026-10-14

## Kontekst

Odczyt adresu celu ([ADR-0015](0015-jedno-zrodlo-powolan-jednostek.md)) znał jednostki redakcyjne i systematyzacyjne aktu, ale nie
załącznik. Komenda w załączniku aktu zmienianego („1) w załączniku nr 1 do rozporządzenia: a) w części 1 …: – w pkt
1.4.1 …", „w załączniku nr 1 w ust. 2 po pkt 57 dodaje się pkt 57a") dostawała więc adres jednostek aktu: `pkt_1`,
`czesc_2`, `ust_2__pkt_57`. Numeracja treści załącznika bywa wielopoziomowa („pkt 1.4.1", „lp. 1.54") — z niej czytało
się oznaczenie „1". Nasz eksport adresuje jednostki załącznika pod jego adresem (`zal_1__pkt_1`,
[ADR-0028](0028-wyliczenie-w-zalaczniku-buduje-potok-aktu.md)), a załącznik bez numeru adresu nie ma ([ADR-0024](0024-jednostka-bez-oznaczenia-nie-ma-eid.md)).

## Decyzja

**„w załączniku nr N" (miejscownik po przyimku „w") jest w adresie celu miejscem nad całą treścią aktu
(`UnitLevel.Annex`, `StructuralReference.Annex`): jednostki redakcyjne pod nim są jednostkami załącznika
(`zal_1__ust_2__pkt_57`), a jednostki systematyzacyjne i artykuły pod nim — podziałem treści załącznika, nie
jednostkami aktu.**

- Liczba w numeracji wielopoziomowej („1.4.1", „1.54") nie jest oznaczeniem jednostki (§ 56 ZTP).
- Załącznik bez numeru („w załączniku do rozporządzenia") nie ma adresu — miejsce w nim i jednostki pod nim też nie.
- Wzmianka o załączniku nie jest miejscem komendy: „o których mowa w załączniku …", „określone w załączniku …"
  i załącznik aktu zmieniającego („… do niniejszej ustawy").
- Sam załącznik jest celem tylko zmiany w jego tekście (wyrazy, cytat-tekst — [ADR-0079](0079-zdanie-albo-akapit-jako-przedmiot-komendy-czyni-tresc-cytowana-tekstem.md)).
  Uchylenie albo zastąpienie czegoś, czego adresu numeracja treści załącznika nie daje („skreśla się lp. 7",
  „poz. A otrzymuje brzmienie:"), adresu nie ma — adres załącznika objąłby cały załącznik (`AmendmentFinalizer`).

## Odrzucone alternatywy

- **Adres jednostek aktu** (stan dotychczasowy). Wskazywał jednostki, których komenda nie dotyczy.
- **Odrzucić każdy cel w załączniku.** Jednostki załącznika z oznaczeniami ZTP (punkty, ustępy) mają adresy
  w naszym eksporcie — dodany „pkt 57a" dostaje `zal_1__ust_2__pkt_57a`.
- **Adres załącznika także dla uchylenia i zastąpienia jego nieadresowalnej części.** Instrukcja uchylenia
  z adresem `zal_2` znaczyłaby uchylenie całego załącznika.

## Konsekwencje

- W korpusie zmienia się 1010 adresów celu w 121 dokumentach: 85 wskazuje jednostki załącznika (`zal_2__ust_2__pkt_57`), 208 — załącznik albo jego najgłębszą znaną jednostkę jako miejsce zmiany w tekście (127 przedtem z adresem jednostki aktu, 81 bez adresu), a 717 adresów jednostek aktu, których komenda nie dotyczyła, staje się `UNKNOWN` (załącznik bez numeru, wiersze tabel, części i numeracja treści załącznika). Liczba instrukcji bez zmian; zgodnych ze schematem nadal 2278 z 2321 (oba walidatory).
- Załącznik zmieniany odesłaniem („załącznik nr 2 otrzymuje brzmienie określone w załączniku do niniejszego
  rozporządzenia") zostaje instrukcją z [ADR-0058](0058-zalacznik-zmieniany-odeslaniem-jest-instrukcja-z-adresami-obu-zalacznikow.md) — to mianownik, nie miejsce.
- Jednostka cytowana pod celem w załączniku bez numeru zostaje z adresem liczonym od korzenia aktu — backlog.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0080_CommandInsideAnAnnex_AddressesTheAnnex_NotUnitsOfTheAct`
- `AnnexCommandTargetTests` — miejsce i jednostki załącznika, wzmianki, numeracja wielopoziomowa, eksport i zgodność
  ze schematem; `RepealCommandRecognitionTests.AnythingRepealedInAnAmendingArticle_IsACommand` (uchylenie wiersza
  tabeli załącznika bez adresu).
