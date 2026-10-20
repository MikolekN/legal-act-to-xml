# ADR-0108: Część załącznika zmieniana albo dodawana odesłaniem to instrukcja ręczna na załączniku, z częścią nazwaną jak w przepisie

- Status: Accepted (Supersedes in part [ADR-0058](0058-zalacznik-zmieniany-odeslaniem-jest-instrukcja-z-adresami-obu-zalacznikow.md): część załącznika nie zostaje bez instrukcji)
- Data: 2026-10-20

## Kontekst

Akt zmieniający podaje nowe brzmienie części załącznika we własnym załączniku: „w załączniku nr 4 do rozporządzenia
tabela D otrzymuje brzmienie określone w załączniku do niniejszego rozporządzenia" (Dz. U. 2024 poz. 1801, plik 295470),
„w załączniku nr 4 „Wykaz świadczeń …” dodaje się lp. 73 w brzmieniu określonym w załączniku do niniejszego
rozporządzenia" (Dz. U. 2026 poz. 51, plik 346994). [ADR-0058](0058-zalacznik-zmieniany-odeslaniem-jest-instrukcja-z-adresami-obu-zalacznikow.md)
daje instrukcję tylko zmianie całego załącznika; część zostawała bez instrukcji, a ocena korpusu liczyła to jako
komendę bez instrukcji.

Odpowiedź autorów schematu z 2026-10-18 (pytanie 21 w `docs/internal/odpowiedzi-autorow-schematu.md`): część
załącznika wskazuje `GUIDcelu`; w akcie istniejącym GUID jest nieznany, więc instrukcja `zastap-recznie` /
`dodaj-recznie` z `element-eId` najgłębszej adresowalnej jednostki — załącznika — także dla części zmienianej
odesłaniem. `now:zastap` z adresem załącznika aktu zmieniającego zastąpiłby cały załącznik, a funkcje ręczne ma tylko
cytat, który nie przyjmuje atrybutów `akt-nowelizujacy` i `nowelizujacy-element-eId`.

## Decyzja

**Przepis zmieniający albo dodający część załącznika odesłaniem do załącznika aktu zmieniającego daje cytat
`cytat-w-tekscie` z funkcją `zastap-recznie` albo `dodaj-recznie`, `element-eId` załącznika, w którym część stoi,
i `cudzyslowy="brak"`. Cytat niesie część tak, jak ją nazywa przepis („tabela D", „lp. 73").**

- Odczyt: `AnnexInstructionReader` — `AnnexInstruction.Part`; załącznik bez numeru to `zal_1`
  ([ADR-0099](0099-jedyny-zalacznik-bez-numeru-ma-adres-zal-1.md)).
- Zawartość cytatu — jak przy uchyleniu celu bez adresu: przedmiot zmiany zamiast nieprzytoczonego tekstu. Odesłanie
  do załącznika aktu zmieniającego zostaje w zdaniu przepisu, tuż przed instrukcją.
- Wyraz „określone" przeniesiony w źródle ręcznie („okreś- lone") nie przeszkadza w rozpoznaniu — ani części, ani
  całego załącznika.

## Odrzucone alternatywy

- **`now:zastap` z adresami obu załączników.** Mówi „zastąp cały załącznik nr 4 załącznikiem aktu zmieniającego" —
  nieprawda, gdy zmienia się tabela D.
- **Bez instrukcji** (ADR-0058). Zmiana nie zostawia w eksporcie żadnego śladu poza tekstem zdania.
- **Cytat pusty.** Nie mówi, co zmienić; przedmiot zmiany jest w przepisie i nic nie kosztuje.

## Konsekwencje

- Kształt zawartości cytatu (przedmiot zmiany zamiast tekstu) jest wyborem SAGA, jak przy ręcznym uchyleniu — do
  potwierdzenia z autorami schematu.
- W korpusie DOCX: 16 instrukcji ręcznych w 13 plikach (część zmieniana albo dodawana odesłaniem) i 5 instrukcji `now:zastap` w 3 plikach (wyraz „określone” przeniesiony ręcznie); zgodność ze schematem bez zmian (2013 z 2321).

## Weryfikacja

- `ArchitectureDecisionTests.Adr0108_PartOfAnAnnexChangedByReference_IsAManualInstructionOnThatAnnex`
- `AnnexInstructionTests` — część zmieniana i dodawana odesłaniem, tytuł załącznika w cudzysłowie, załącznik bez
  numeru, wyraz przeniesiony ręcznie, eksport, zgodność ze schematem.
