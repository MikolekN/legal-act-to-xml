# ADR-0020: Jednostki systematyzacyjne w eksporcie zagnieżdżone wg schemaPL; łańcuch niejawny nad pierwszą jawną jednostką znika

- Status: Accepted
- Data: 2026-10-09

## Kontekst

Model trzyma pełny łańcuch `Part → Book → Title → Division → Chapter → Subchapter → Article`.
Brakujące poziomy wstawia jako jednostki niejawne (`IsImplicit`), więc artykuł aktu bez rozdziałów
też siedzi w sześciu niejawnych kontenerach.

Schemat wyjściowy (`schemaPL.xsd`) narzuca kształt drzewa:

- `UstawaTyp` przyjmuje grupę jednostek systematyzacyjnych albo `artykul` wprost;
- `RozdzialTyp` trzyma wyłącznie `oddzial`/`oddzial-n`, nie artykuły;
- jednostka bez oznaczenia to element `-n` (`rozdzial-n`, `oddzial-n`…) bez `eId`,
  z atrybutem `brak-eid`;
- na poziomie aktu element `tytul` jest już tytułem aktu, więc jednostka nazywa się tam
  `tytul-jednostka` (`tytul-n-jednostka`).

Przykłady autorów schematu (`akn4pl/przyklady/*/schemaPL_no_ref.xml`) zapisują rozdział jako
`rozdzial[eId] → naglowek-rozdzialu (nr, p) → oddzial-n[brak-eid="jednostka-nieoznaczona"] → artykul`.

Przed tą decyzją eksport w ogóle nie emitował jednostek systematyzacyjnych — artykuły szły płasko
do aktu.

## Decyzja

**Eksport odtwarza zagnieżdżenie jednostek, ale niejawny łańcuch bez jawnego przodka nie jest
emitowany** (`SystematizingUnitXmlConverter`):

- Niejawna jednostka, której wszyscy przodkowie są niejawni, znika — jej dzieci stają wprost
  w akcie. Akt bez jawnych jednostek eksportuje się więc jak dotąd: płaska lista artykułów.
- Pod jawną jednostką niejawne poziomy pośrednie wychodzą jako `-n`
  z `brak-eid="jednostka-nieoznaczona"`, GUID-em i datami cyklu życia, bez nagłówka. Schemat
  ich wymaga (rozdział → `oddzial-n` → artykuł).
- Jawna jednostka dostaje `eId`, GUID, daty i `naglowek-*` z `nr` (oznaczenie jak w akcie, z indeksem
  górnym: `IV[1]`) oraz `p` z tytułem — tylko jeśli tytuł jest.
- Tytuł stojący wprost w akcie to `tytul-jednostka`, pod księgą — `tytul`.
- Każda część (`LegalDocument.Parts`) jest osobnym rodzeństwem na poziomie aktu.

## Odrzucone alternatywy

- **Pełny łańcuch zawsze, niejawne poziomy jako `-n`.** Zgodne ze schematem, ale każdy akt bez
  rozdziałów dostałby sześć pustych kontenerów `czesc-n/…/oddzial-n` — inny XML dla 90% korpusu,
  bez żadnej informacji, i niezgodny z przykładami autorów schematu.
- **Pomijanie wszystkich jednostek niejawnych, także pod jawną.** Artykuł trafiałby wprost do
  `rozdzial`, czego `RozdzialTyp` nie dopuszcza — XML nie przechodzi walidacji.
- **Osobny konwerter na każdy rodzaj jednostki** (stan sprzed decyzji). Sześć kopii tej samej
  logiki; reguła „niejawne pod jawnym → `-n`" musiałaby żyć w każdej z nich.

## Konsekwencje

- Akty bez jawnych jednostek mają XML bez zmian; akty z rozdziałami, działami itd. dostają
  zagnieżdżenie.
- Artykuł przed pierwszą jawną jednostką (np. przed „Rozdział 1") zostaje wprost w akcie, bo
  jego łańcuch jest w całości niejawny.
- Nazwę elementu wybiera poziom (`actLevel`), nie sam typ jednostki. Kto doda jednostkę, która
  może stać w akcie, musi sprawdzić, czy schemat nie przemianowuje jej na tym poziomie.
- `brak-eid` ma dziś jedną wartość, `jednostka-nieoznaczona`. Inne powody braku eId ze schematu
  nie są używane.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0020_Export_ElidesTheImplicitChainAboveTheFirstExplicitUnit`.
- `SystematizingUnitXmlConverterTests` — kształt nagłówka, `-n` z `brak-eid`, `tytul-jednostka`
  vs `tytul`, rodzeństwo części.
