# ADR-0073: Akt bez nagłówka dziennika bierze miejsce ogłoszenia z wiersza, który przyłącza do niego treść

- Status: Accepted
- Data: 2026-10-14

## Kontekst

Miejsce ogłoszenia aktu (dziennik, rocznik, pozycja) eksport czytał wyłącznie z nagłówka strony („DZIENNIK USTAW …
Poz. 1451"). Szczotki i wydruki bez nagłówka zostawały z pustymi `Rok` i `Pozycja` — metadane niezgodne ze schematem —
i bez prefiksu aktu w adresach jednostek ([ADR-0027](0027-prefiks-aktu-dopisuje-eksport.md)).

Akt podaje swój adres jeszcze w jednym miejscu: w wierszu, który przyłącza do niego treść. Tekst jednolity i inne
obwieszczenia: „Załącznik do obwieszczenia Prezesa Rady Ministrów z dnia 24 września 2024 r. (Dz. U. poz. 1451)";
akt z załącznikami: „Załączniki do rozporządzenia Rady Ministrów z dnia 30 kwietnia 2025 r. (Dz. U. poz. 675)".
Rocznik dziennika podaje się „w razie potrzeby" (§ 157 ZTP) — gdy różni się od roku daty aktu
(„z dnia 4 grudnia 2025 r. (Dz. U. z 2026 r. poz. 15)").

## Decyzja

**Dokument bez nagłówka dziennika bierze miejsce ogłoszenia z wiersza przyłączenia**
(`JournalHeaderParser.ApplyFromAttachment`): z wiersza „Załącznik do obwieszczenia …" (`Announcement.AttachedTo`),
a gdy go nie ma — z nagłówka pierwszego własnego załącznika. Rocznik pominięty w wierszu to rok daty aktu
(`JournalReferenceService.ReadFirstAct`).

- Liczy się tylko pierwsze przyłączenie: dalszy wiersz („Załącznik nr 2 do rozporządzenia … z dnia 20 marca 2020 r.
  (Dz. U. poz. 123)") nazywa załącznik aktu zmienianego, nie ten akt.
- Pozycja nieuzupełniona w szczotce („poz. 0", „poz. …", „poz. )") niczego nie wskazuje.
- Nagłówek ma pierwszeństwo; wiersz przyłączenia odczytuje się po budowie modelu, bo wcześniej publikator dokumentu
  zastępuje akt zmieniany w nowelizacji, która własnego publikatora nie podaje.

## Odrzucone alternatywy

- **Zostawić puste `Rok`/`Pozycja`.** Wiersz przyłączenia jest częścią aktu i podaje adres wprost; pusty element
  łamie schemat, a jednostki tracą prefiks aktu.
- **Rocznik z daty dokumentu zawsze.** Akt z grudnia ogłoszony w styczniu ma rocznik następnego roku — wiersz podaje
  go wtedy wprost i ten zapis rozstrzyga.
- **Każdy wiersz z adresem w załącznikach.** W akcie zmieniającym kolejny wiersz nagłówka załącznika nazywa akt
  zmieniany.

## Konsekwencje

- W korpusie 332 dokumenty bez nagłówka dziennika (192 rozporządzenia, 91 tekstów jednolitych, 48 innych obwieszczeń, 1 ustawa) dostają miejsce ogłoszenia z wiersza przyłączenia — w każdym data w tym wierszu jest datą aktu, a na 779 dokumentach z nagłówkiem adres z wiersza zgadza się z nagłówkiem bez wyjątku. Zgodnych ze schematem jest 2278 z 2321 (było 1946; oba walidatory, żaden dokument nie wypadł). Zostaje 41 dokumentów bez nagłówka, których wiersz pozycji nie podaje („poz. 0", „poz. )", „poz. …") albo którego nie ma, i 2 z niezgodną treścią.
- Dzień ogłoszenia (`DataOgloszenia`) wiersz przyłączenia nie podaje — dokument bez nagłówka dalej go nie ma.
- Adresy jednostek i zdarzeń takich dokumentów dostają prefiks aktu, jak w dokumentach z nagłówkiem.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0073_ActWithoutAJournalHeader_TakesItsAddressFromTheAttachmentLine`
- `AnnouncementTests.AnnouncementPoints_AreExportedWithAddressesOfTheAnnouncement`,
  `AnnexTests.Annexes_AreExportedAfterTheSignature_WithHeadingNumberTitleAndContent` — prefiks z wiersza przyłączenia.
