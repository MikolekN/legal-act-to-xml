# ADR-0101: Tytuł aktu zmienianego jako cel komendy to instrukcja ręczna (`*-recznie`) z adresem samego aktu

- Status: Accepted — Superseded in part by [ADR-0125](0125-komenda-bez-celu-to-instrukcja-reczna-na-akcie.md) (cel bez odczytanego aktu: bez `element-eId`)
- Data: 2026-10-20

## Kontekst

Akt zmieniający zmienia czasem tytuł aktu zmienianego: „tytuł ustawy otrzymuje brzmienie:", „w tytule ustawy w ogólnym
określeniu przedmiotu ustawy skreśla się wyrazy …", „do tytułu ustawy dodaje się odnośnik nr 1 w brzmieniu:". Tytuł aktu
(`tytul` w nagłówku aktu) nie ma `eId` — wzorzec `EIdTyp` wymaga oznaczenia, a `tyt` to tytuł jako jednostka
systematyzacyjna. Eksport dawał takim komendom `element-eId="UNKNOWN"`: w korpusie DOCX 18 komend w 18 dokumentach
(12 zmian tytułu, 6 odnośników dodawanych do tytułu).

Autorzy schematu (odpowiedź z 2026-10-18 na pytanie 6, zasada ogólna 4 w `docs/internal/odpowiedzi-autorow-schematu.md`):
tytułu nie adresuje się przez `eId`; `element-eId` to identyfikator aktu, a `GUIDcelu` — GUID tytułu. W aktach
istniejących GUID tytułu aktu zmienianego jest nieznany, więc instrukcja ręczna — `zastap-recznie`, `dodaj-recznie`,
`uchyl-recznie`.

## Decyzja

**Przepis, który zaczyna się tytułem aktu zmienianego („tytuł ustawy …", „w tytule ustawy …", „do tytułu ustawy …",
„dodaje się odnośnik nr N do tytułu ustawy"), ma cel „tytuł aktu" (`StructuralReference.ActTitle`). Instrukcja jest
ręczna, a `element-eId` to adres samego aktu zmienianego („DU/2021/200").**

- Odczyt: `LegalReferenceService.ActTitleTarget` — gdy przepis nie wskazuje żadnej jednostki, a za formułą aktu
  zmienianego od razu nazywa jego tytuł. Przepisy pod nim dziedziczą ten cel.
- Odnośnik dodawany do tytułu jako pierwszy (nr 1) nie ma odnośnika, po którym mógłby stanąć
  ([ADR-0059](0059-odnosnik-jako-przedmiot-komendy-jest-cytowanym-odnosnikiem.md)) — jego celem jest tytuł.
- Instrukcja ręczna według ADR-0100: cytat z funkcją `*-recznie`, zmiana w tekście — cytat z nowymi wyrazami.
- Adres aktu bierze eksport z aktu zmienianego nowelizacji (jak atrybut `akt`); bez niego cel zostaje nieznany.

## Odrzucone alternatywy

- **`element-eId="UNKNOWN"`** (stan sprzed decyzji). Autorzy schematu wskazali drogę: adres aktu z instrukcją ręczną.
- **Instrukcja automatyczna z adresem aktu** (`now:zamien-tekst element-eId="DU/2021/200"`). Zmieniłaby wyrazy w całym
  akcie, nie w tytule.
- **Segment tytułu w `eId`** (`…__tyt`). `tyt` to tytuł jako jednostka systematyzacyjna, a bez oznaczenia `EIdTyp` go
  nie przyjmuje.

## Konsekwencje

- Odnośnik do tytułu jako miejsce („w odnośniku nr 1 do tytułu ustawy akapit drugi otrzymuje brzmienie:") tytułem nie
  jest — według ustalenia z 2026-10-19 jego celem jest odnośnik (`odn_1`), osobny krok.
- Odnośnik dodawany do tytułu jako kolejny („nr 2") stoi nadal po poprzednim odnośniku (`dodaj-po` na `odn_1`, ADR-0059).

## Weryfikacja

`ArchitectureDecisionTests.Adr0101_ActTitle_IsAManualInstructionOnTheAct`, `ActTitleTargetTests` — zmiana tytułu,
odnośnik dodany do tytułu, zmiana wyrazów w tytule, formuła aktu przed tytułem, inne tytuły, zgodność ze schematem.
