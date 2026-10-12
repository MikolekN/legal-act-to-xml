# ADR-0058: Załącznik zmieniany odesłaniem do załącznika aktu zmieniającego jest instrukcją z adresami obu załączników

- Status: Accepted
- Data: 2026-10-12

## Kontekst

Nowe brzmienie załącznika akt zmieniający podaje zwykle we własnym załączniku: „załącznik nr 1 do rozporządzenia
otrzymuje brzmienie określone w załączniku do niniejszego rozporządzenia", „dodaje się załączniki nr 3 i 4 do ustawy
w brzmieniu określonym w załącznikach nr 3 i 4 do niniejszej ustawy" (157 zdań korpusu). Przepis nie ma dwukropka
ani cytatu, więc nie powstawała żadna nowelizacja — eksport niósł sam tekst zdania.

Schemat przewiduje dla tego instrukcje `now:zastap` i `now:dodaj-po` z adresem załącznika aktu zmienianego
(`element-eId`) i załącznika aktu zmieniającego (`nowelizujacy-element-eId`, `akt-nowelizujacy`); przykład autorów
schematu (DU/2026/252) używa ich dokładnie tak.

## Decyzja

- **Model:** `Amendment.AnnexInstructions` (`AnnexInstruction`: `Adds`, `Target`, `Source`, `SourceTo`) na
  nowelizacji przepisu, bez treści cytowanej.
- **Odczyt** (`AnnexInstructionReader`): przedmiotem komendy musi być sam załącznik z numerem. Zastąpienie daje
  instrukcję na każdy załącznik; załączniki obu aktów łączą się w pary, gdy jest ich tyle samo, a przedział liczb
  („nr 3–9") rozwija się w pojedyncze. Dodanie daje jedną instrukcję: miejscem jest załącznik wskazany („po
  załączniku nr 2") albo poprzedzający pierwszy dodawany („nr 4" staje po „nr 3", „nr 2a" po „nr 2").
- **Eksport** (`AmendmentXmlConverter.AddAnnexInstructions`, `ActPrefixXml`): `now:zastap` / `now:dodaj-po`
  z `akt`, `element-eId`, `akt-nowelizujacy`, `nowelizujacy-element-eId` (przy dodaniu kilku — także `-do`); akt
  zmieniający i prefiks jego załącznika dokłada przebieg prefiksów.

## Odrzucone alternatywy

- **Skopiować treść załącznika aktu zmieniającego do cytatu.** Załącznik jest w eksporcie raz, na swoim miejscu;
  instrukcja `zastap` istnieje właśnie po to, żeby na niego wskazać.
- **Przedział załączników jako jedna instrukcja z `element-do-eId`.** `now:zastap` ma jeden adres załącznika
  zmieniającego, więc pary „3→5, 4→6, …" trzeba wypisać; numery załączników są liczbami, więc rozwinięcie jest
  pewne (inaczej niż przy jednostkach — ADR-0016).
- **Miejsce dodania tylko wtedy, gdy przepis je podaje.** Przykład autorów schematu wyprowadza je tak samo
  z numeru; załączniki numeruje się kolejno.

## Konsekwencje

- W korpusie przybywa 86 instrukcji `now:zastap` (55 plików) i 20 `now:dodaj-po` (18 plików) — 91 zdań; poza tym eksport się nie zmienia, zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory). W przykładzie autorów schematu DU/2026/252 wszystkie trzy instrukcje załączników są takie same jak nasze, atrybut w atrybut; komendy zgodne z tym przykładem: 336 z 345.
- Załącznik bez numeru („załącznik do rozporządzenia otrzymuje brzmienie określone w załączniku do niniejszego
  rozporządzenia", ok. 50 zdań) nie ma adresu — instrukcji nie ma; to samo pytanie co adres jedynego załącznika
  w ogóle (backlog). Zmiana części załącznika odesłaniem („tabela D otrzymuje brzmienie określone w załączniku",
  ok. 15) też zostaje bez instrukcji.
- Akt bez nagłówka dziennika nie ma własnego prefiksu: `nowelizujacy-element-eId` zostaje bez niego,
  a `akt-nowelizujacy` niesie sam adres bazowy ELI.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0058_AnnexReplacedByReference_IsAnInstructionNamingBothAnnexes`
- `AnnexInstructionTests` — pary załączników, przedział, źródło bez numeru, dodanie z miejscem wskazanym
  i wyprowadzonym, przepisy, które instrukcją nie są, eksport obu instrukcji.
