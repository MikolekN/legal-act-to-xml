# ADR-0025: Część wspólna zamykająca wyliczenie ma eId gospodarza z segmentem `czw`

- Status: Accepted
- Data: 2026-10-10

## Kontekst

Część wspólna po wyliczeniu („– zwany dalej organem.") jest w eksporcie osobnym elementem
(`czesc-wspolna-punkt`, `-litera`, `-tiret`), ale nie miała eId — niosła tylko GUID i daty. Model
nadawał jej wewnętrzny identyfikator z segmentem `wrapUp`, którego eksport nie zapisywał, a
[ADR-0014](0014-skroty-jednostek-w-eid.md) wyłączył segmenty części wspólnej z tabeli skrótów.

Przykłady autorów schematu adresują każdą część wspólną: `art_51__ust_1__czw`, w artykule z jednym
ustępem `art_16__czw`, w cytacie `art_68__ust_2__czw` (pod adresem w akcie zmienianym). Wzorzec
`EIdTyp` wymienia `czw` jako jedyny segment bez oznaczenia.

## Decyzja

**Część wspólna zamykająca wyliczenie ma eId: adres gospodarza i segment `czw`** — w modelu
(`CommonPartType.ToEIdSegment`) i w eksporcie (`CommonPartXmlConverter`).

- Gospodarzem jest jednostka, w której stoi wyliczenie; jednostki niejawne i bez oznaczenia są
  w adresie przezroczyste, jak wszędzie (`art_16__czw`).
- Wprowadzenie do wyliczenia nie jest elementem eksportu, więc eId nie dostaje; w modelu zostaje
  mu wewnętrzny segment `intro`.

## Odrzucone alternatywy

- **Część wspólna bez eId (stan sprzed decyzji).** Odbiorca nie miał jak wskazać części wspólnej
  inaczej niż przez GUID, który zmienia się przy każdym eksporcie; schemat wymaga też nazwania powodu
  braku adresu (`brak-eid`), a żaden z wyliczonych powodów tu nie pasuje.
- **Segment `wrapUp`** (dotychczasowy identyfikator modelu). Nie przechodzi `EIdTyp` — wzorzec zna
  segmenty `skrót_oznaczenie` i `czw` — i różni się od przykładów autorów schematu.

## Konsekwencje

- Zmienia wyjście: elementy `czesc-wspolna-*` dostają `eId`; w snapshotach kanonicznych `wrapUp`
  w eId staje się `czw`.
- Eksport zapisuje jedną część wspólną na gospodarza, więc adres jest jednoznaczny. Część wspólna
  przerywająca wyliczenie albo sama zawierająca wyliczenie (w schemacie `nie-ztp-czesc-wspolna-*`
  z `brak-eid`) nie jest rozpoznawana.
- `czw` nie jest skrótem jednostki i nie wchodzi do tabeli ADR-0014: nie ma oznaczenia i nie może
  mieć jednostek pod sobą.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0025_ClosingCommonPart_IsAddressedByItsHostAndCzw`.
- `IntroCommonPartTests.AttachWrapUp_StripsTiretPrefixAndAddsCommonPart` — eId w modelu.
- `CorpusValidator --compare-reference` zestawia części wspólne z przykładami autorów schematu.
