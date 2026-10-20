# ADR-0105: Odnośnik tego aktu w nowych wyrazach zmiany w tekście to zakodowany fragment XML odnośnika w `nowy-tekst`

- Status: Accepted
- Data: 2026-10-20

## Kontekst

„wyrazy „(Dz. Urz. UE L 20 z 31.01.2022, str. 95)” zastępuje się wyrazami „(Dz. Urz. UE L 20 z 31.01.2022, str. 95, z późn.
zm.¹⁾)”" (Dz. U. 2025 poz. 43, plik 296120) — przy „z późn. zm." § 158 ust. 7 ZTP wymaga odnośnika, a akt zmieniający
stawia go w nowych wyrazach. Odnośnik należy do nowego brzmienia aktu zmienianego. `nowy-tekst` instrukcji
`now:zamien-tekst` to `xs:token`, więc elementu nie przyjmie. Eksport zdejmował znacznik z wyrazów (treść odnośnika stała
w zdaniu komendy); w korpusie DOCX 7 instrukcji w 4 dokumentach (296120, 302046, 308909, 353666).

Pytanie 23, ustalenie z 2026-10-20 (`docs/internal/odpowiedzi-autorow-schematu.md`): autor schematu zaproponował wpisanie
do `nowy-tekst` fragmentu XML odnośnika, który przekleja się w miejsce docelowe razem z wyrazami; właściciel projektu
wybrał tę drogę zamiast treści elementu (`<now:nowy-tekst>` z odnośnikiem — czystsze, bo schemat sprawdzałby odnośnik, ale
niepotrzebne). Próba wklejenia (2026-10-20): fragment bez przedrostka przestrzeni nazw trafia do domyślnej przestrzeni
aktu, w który go wklejono, a przedrostek `gd:` deklaruje korzeń tego aktu; `id-odnosnika` musi być jednoznaczny w akcie,
więc wklejający nadaje odnośnikowi nowy numer.

## Decyzja

**W `nowy-tekst` pod znacznik odnośnika tego aktu trafia jego fragment XML (`<odnosnik id-odnosnika="1" symbol="1"
gd:GUID="…"><p><zdanie …>…</zdanie></p></odnosnik>`) bez deklaracji przestrzeni nazw. Znak `<` i `&` koduje zapis
atrybutu (`&lt;`, `&amp;amp;`).**

- Znacznik zostaje w nowych wyrazach modelu (`TextChangeWording.DropOwnFootnoteMarkers` zdejmuje go tylko z wyrazów
  zastępowanych), a fragment podstawia eksport (`FootnoteXmlConverter.EmbedInNewWords`) — z tego samego odnośnika, który
  zdanie komendy niesie jako odnośnik aktu zmieniającego.
- Znacznik, którego numer nie jest odnośnikiem tego aktu, zostaje znacznikiem.

## Odrzucone alternatywy

- **Znacznik „[1)]" w `nowy-tekst`** (wcześniej) albo **wyrazy bez znacznika**. Akt zmieniany dostałby wyrazy bez
  odnośnika, którego wymaga ZTP.
- **Treść elementu `now:nowy-tekst` z odnośnikiem.** Wymaga zmiany schematu; właściciel projektu wybrał fragment
  w atrybucie.
- **Fragment z deklaracjami przestrzeni nazw.** Po wklejeniu niósłby deklaracje aktu zmieniającego zamiast przejąć
  przestrzeń aktu docelowego.

## Konsekwencje

- Odnośnik stoi w eksporcie dwa razy: jako element w zdaniu komendy (odnośnik aktu zmieniającego) i jako tekst
  w atrybucie (dla aktu zmienianego). Atrybut nie jest elementem, więc jednoznaczność `id-odnosnika` w akcie zmieniającym
  nie cierpi.

## Weryfikacja

`ArchitectureDecisionTests.Adr0105_FootnoteInNewWords_IsAnXmlFragmentInTheAttribute`,
`AmendmentTargetsExportTests.NewWords_CarryTheFootnoteOfThisAct_AsAnXmlFragment`,
`AmendmentTargetsExportTests.ReplacedWords_DoNotCarryAFootnoteOfThisAct_NorASoftHyphen`.
