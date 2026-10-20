# ADR-0098: Jednostka nazwana zamiast numerowanej („CZĘŚĆ OGÓLNA") ma nazwę jako oznaczenie w eId, a w nagłówku pusty `nr` i nazwę w `p` przed tytułem

- Status: Accepted
- Data: 2026-10-20

## Kontekst

Starsze kodeksy dzielą się na jednostki nazwane, nie numerowane: „CZĘŚĆ OGÓLNA", „CZĘŚĆ SZCZEGÓLNA", „CZĘŚĆ
WOJSKOWA" (Kodeks karny, Kodeks wykroczeń, Kodeks karny wykonawczy), „TYTUŁ WSTĘPNY" i pod nim „Przepisy ogólne"
(Kodeks postępowania cywilnego). W korpusie DOCX: 9 jednostek w 4 tekstach jednolitych (329088, 318682, 326565, 302049).

Oznaczenie w eId to liczba arabska ([ADR-0018](0018-postac-oznaczenia-w-eid.md)), a nazwa liczbą nie jest. Do czasu
ustaleń z autorami schematu jednostka dostawała więc eId zastępczy `czesc_TMP1`, `czesc_TMP2` (decyzja właściciela
projektu) i nazwę w `nr`. Samo `czesc` wzorzec `EIdTyp` odrzuca — bez oznaczenia stoi tylko `czw`.

Autorzy schematu (odpowiedzi z 2026-10-18 i 2026-10-20 na pytanie 25, `docs/internal/odpowiedzi-autorow-schematu.md`):
nazwa idzie do `p` nagłówka, `nr` zostaje pusty — `<czesc><naglowek-czesci><nr></nr><p>WSPÓLNA</p></naglowek-czesci>…`;
ich walidator ma to zgłaszać jako ostrzeżenie. O `eId` zdecydował właściciel projektu (2026-10-20): nazwa jest
oznaczeniem — `czesc_OGÓLNA`, `tyt_WSTĘPNY`. Klasa znaków `EIdTyp` ma wielkie polskie litery, więc taki adres jest
zgodny (sprawdzone walidacją na czterech kodeksach korpusu).

## Decyzja

**Nazwa jednostki nazwanej jest jej oznaczeniem: w eId wielkimi literami (`czesc_OGÓLNA`, `tyt_WSTĘPNY`, a jednostki
pod nią — `czesc_OGÓLNA__rozdz_1`). W nagłówku `nr` jest pusty, a nazwa stoi w `p` przed tytułem jednostki:
„TYTUŁ WSTĘPNY" / „Przepisy ogólne" → `<p>WSTĘPNY Przepisy ogólne</p>`.**

- Model: `EntityNumber.IsName`; `Value` to nazwa wielkimi literami, `RawValue` — nazwa jak w akcie.
- Lista nazw zostaje zamknięta (`ParagraphClassifier.IsNamedDesignation`): dowolny wyraz („Część budżetowa") jest
  prozą.
- Nazwa i tytuł w jednym `p`: nagłówek jednostki systematyzacyjnej ma jeden `p` i nie ma osobnego elementu tytułu ani
  złamania wiersza — zwykły tytuł („Rozdział I" / „Zakres obowiązywania") też stoi w `p`.
- Powołanie z nazwą w mianowniku („część ogólna") daje ten sam segment adresu (`UnitPathCursor`).

## Odrzucone alternatywy

- **eId zastępczy `czesc_TMP1`** (stan sprzed decyzji). Adres zależał od kolejności w dokumencie, nie od aktu, i nie
  zgodziłby się z żadnym odwołaniem.
- **Samo `czesc`** albo liczba porządkowa części (`czesc_1`). Pierwszego `EIdTyp` nie przyjmuje, drugie nadaje
  oznaczenie, którego w akcie nie ma.
- **Nazwa w `nr`.** Autorzy schematu wybrali `p`; `nr` niesie oznaczenie liczbowe.
- **Tytuł pod nazwą jako dalsza treść nagłówka (`specjalny`).** To tytuł jednostki, tak jak w jednostkach
  numerowanych; nagłówek nie ma dla niego osobnego miejsca.

## Konsekwencje

- Dwie jednostki o tej samej nazwie w jednym dokumencie dostałyby ten sam eId; w korpusie nazwy w obrębie dokumentu się
  nie powtarzają.
- Walidator autorów schematu ma zgłaszać pusty `nr` takiej jednostki jako ostrzeżenie; kopia schematu w `schema/` go
  przyjmuje (`nr` nagłówka nie ma wzorca).
- Zastępuje w części ADR-0018: oznaczenie słowne, które nie jest liczbą, zostaje nazwą.

## Weryfikacja

`ArchitectureDecisionTests.Adr0098_NamedUnit_HasItsNameAsTheDesignation`,
`SystematizingUnitKnownGapsTests.NamedParts_AreRecognized_WithTheirNamesInTheEId`,
`SystematizingUnitKnownGapsTests.NamedTitle_IsRecognized_WithItsNameInTheEId`,
`SystematizingUnitXmlConverterTests.NamedUnit_HasAnEmptyNumber_AndItsNameBeforeTheTitleInOneParagraph`.
