# ADR-0100: Cel w załączniku, którego `eId` nie zaadresuje, to instrukcja ręczna (`*-recznie`) z adresem najgłębszej adresowalnej jednostki

- Status: Accepted (Supersedes w części ADR-0080: uchylenie i zastąpienie części załącznika bez adresu)
- Data: 2026-10-20

## Kontekst

Komendy w załącznikach wskazują często miejsca, których numeracja treści załącznika nie daje jako jednostek: wiersz
tabeli („uchyla się lp. 7", „po lp. 4 dodaje się lp. 4a"), kolumnę („w kolumnie trzeciej wyrazy …"), część, tabelę,
tytuł załącznika, punkt z numerem z kropką („w pkt 1.4.1"). [ADR-0080](0080-komenda-w-zalaczniku-wskazuje-jednostki-zalacznika.md)
dawał takiemu uchyleniu i zastąpieniu cel nieznany (`element-eId="UNKNOWN"`), bo adres załącznika objąłby cały
załącznik; zmiana w tekście i cytat-tekst dostawały adres załącznika i instrukcję automatyczną — taką, która zmienia
wyrazy w całym załączniku, choć komenda wskazała kolumnę albo wiersz. W korpusie DOCX instrukcje z celem w strukturach
załącznika to 577 instrukcji w 107 dokumentach.

Autorzy schematu (odpowiedź z 2026-10-18 na pytanie 21, zasada ogólna 4 i 5 w `docs/internal/odpowiedzi-autorow-schematu.md`):
cel, którego `eId` nie zaadresuje, wskazuje `GUIDcelu` przy `element-eId` najgłębszej adresowalnej jednostki. W aktach
istniejących GUID elementu aktu zmienianego jest nieznany i opcjonalny, więc instrukcja jest ręczna: `dodaj-recznie`,
`zastap-recznie`, `uchyl-recznie`. Adres poniżej `zal_N` — tylko gdy łatwy i jednoznaczny. Zasada 6 (2026-10-20):
instrukcja automatyczna, która w akcie zmienianym nie trafi, jest gorsza niż ręczna.

## Decyzja

**Cel w treści załącznika poza jego jednostkami — i sam załącznik jako cel komendy, która zmienia coś w nim — jest
celem ręcznym (`StructuralAmendmentReference.IsManual`): `element-eId` wskazuje najgłębszą adresowalną jednostkę
(`…__zal_2`, `…__zal_2__ust_3`), a funkcja instrukcji to `*-recznie` według rodzaju zmiany.**

- Oznacza `ManualTargets.Mark` — dla przepisów aktu (`AmendmentFinalizer`) i przepisów cytowanych (`NestedAmendment`).
- Cytat (`cytat-strukt`, `cytat-w-tekscie`): `dodaj-recznie` dla dodania, `uchyl-recznie` dla uchylenia, inaczej
  `zastap-recznie`.
- Uchylenie bez treści cytowanej („uchyla się lp. 7"): `cytat-w-tekscie funkcja="uchyl-recznie"` z przedmiotem
  uchylenia, jak nazywa go przepis (`<tekst>lp. 7</tekst>`), i `cudzyslowy="brak"` — tekstu do uchylenia akt
  zmieniający nie przytacza, a `cytat-w-tekscie` wymaga treści.
- Zmiana w tekście (`now:zamien-tekst` nie ma funkcji ręcznej): `cytat-w-tekscie funkcja="zastap-recznie"` z nowymi
  wyrazami, a przy skreśleniu — `uchyl-recznie` ze skreślanymi.
- Zostaje automatyczna zmiana w tekście całego załącznika — przepis za miejscem „w załączniku (nr N)" od razu nazywa
  wyrazy („w załączniku nr 2 wyrazy „X” zastępuje się …"), także pod przepisem nadrzędnym, który podał załącznik.
- Załącznik uchylany w całości („uchyla się załącznik nr 4") jest zwykłym celem: `now:uchyl` z `…__zal_4`.

## Odrzucone alternatywy

- **Cel nieznany (`UNKNOWN`)** (ADR-0080). Instrukcja nie wskazywała niczego, choć wiadomo, w którym załączniku zmiana
  leży; autorzy schematu przewidzieli na to instrukcje ręczne.
- **Instrukcja automatyczna z adresem załącznika** (dotychczas dla zmian w tekście). Zmienia wyrazy w całym załączniku
  (`wystapienie` domyślnie `wszystkie`), także tam, gdzie komenda ich nie zmienia.
- **Adresy poniżej `zal_N` dla wierszy i kolumn** (`zal_2__lp_7`). Takich poziomów `eId` nie ma, a autorzy schematu
  zalecili nie rozbudowywać adresów w głąb załącznika.
- **`now:uchyl` z adresem załącznika dla uchylenia wiersza.** Znaczyłoby uchylenie całego załącznika.

## Konsekwencje

- Treść przedmiotu uchylenia w `cytat-w-tekscie` („lp. 7") to propozycja SAGA — przykłady autorów schematu niosą
  w `*-recznie` tylko tekst przytoczony; do potwierdzenia przy kolejnych odpowiedziach.
- Cel, którego `eId` nie zaadresuje poza załącznikiem (tytuł aktu, akapit odnośnika, zdanie bez numeru), zostaje na
  razie `UNKNOWN` — ta sama zasada, osobne kroki.
- Rozpoznanie miejsca w załączniku zostaje po stronie odczytu adresu (`UnitAddressReader`, ADR-0080); miejsce, którego
  odczyt nie zauważy, a przepis nazywa tylko rzeczownikiem w mianowniku („kolumna czwarta otrzymuje brzmienie:"),
  też wychodzi ręcznie, bo przepis nie zaczyna się od wyrazów zmienianych.

## Weryfikacja

`ArchitectureDecisionTests.Adr0100_TargetInAnAnnexWithoutAnAddress_IsAManualInstruction`,
`AnnexCommandTargetTests` — uchylenie wiersza, zmiana w punkcie z numerem z kropką, punkt tabeli, zmiana w tekście
całego załącznika, uchylenie całego załącznika, zgodność instrukcji ręcznych ze schematem;
`RepealCommandRecognitionTests.AnythingRepealedInAnAmendingArticle_IsACommand`.
