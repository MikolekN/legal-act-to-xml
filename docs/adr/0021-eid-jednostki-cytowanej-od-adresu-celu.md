# ADR-0021: eId jednostki cytowanej w nowelizacji zaczyna się od adresu celu w akcie zmienianym

- Status: Accepted
- Data: 2026-10-09

## Kontekst

Treść nowelizacji („w art. 68 ust. 1 pkt 5 otrzymuje brzmienie: „5) …”") to jednostki aktu
zmienianego. Parser buduje je jako osobne poddrzewo bez rodzica, więc ich eId był liczony od nich
samych: punkt miał `pkt_5`, ustęp `ust_2`.

Przykłady autorów schematu (`akn4pl/przyklady/*/schemaPL_no_ref.xml`) zapisują jednostkę cytowaną
pod jej adresem w akcie zmienianym: `art_68__ust_1__pkt_5`, `art_2__pkt_4a`, `art_1__ust_5`
(z prefiksem aktu zmienianego, który jest osobną funkcją). W eksporcie „pkt_5" powtarzał się
w każdej nowelizacji, która zmieniała jakikolwiek punkt 5, i nie mówił, którą jednostkę zastępuje.

## Decyzja

**Jednostka najwyższego poziomu w cytacie dostaje adres celu powyżej swojego poziomu jako początek
eId** (`BaseEntity.AddressAnchor`). Jej podjednostki dziedziczą go przez zwykłe składanie ścieżki.

- Punkt dodawany „w art. 68 ust. 1" → `art_68__ust_1__pkt_5`; ustęp „w art. 99 ust. 2" → `art_99__ust_2`.
- Artykuł nie ma adresu nad sobą — zostaje `art_85a`.
- Kotwicę ustala `AmendmentTargetPairing.AnchorContentUnits` przy finalizacji. Jednostki dzieli
  między cele tak samo jak eksport: po oznaczeniu, a przy rozjeździe wszystkie idą pod pierwszy cel.
- Bez rozpoznanego celu jednostka zostaje bez kotwicy (`pkt_5`).

## Odrzucone alternatywy

- **eId względny wobec cytatu (stan sprzed decyzji).** Niezgodny z przykładami autorów schematu.
  Ten sam eId występuje w wielu cytatach, a odbiorca nie odtworzy z niego miejsca zmiany.
- **Kotwica jako rodzic-atrapa w modelu** (sztuczny artykuł 68 nad cytowanym punktem). Atrapa
  trafiałaby do obchodów drzewa (daty wejścia w życie, eksport, walidacja) jako jednostka, której
  w akcie nie ma.
- **Kotwica liczona dopiero w eksporcie.** eId w modelu i w XML byłyby różne, a snapshot
  kanoniczny i diagnostyka pokazywałyby inny adres niż eksport.

## Konsekwencje

- Zmienia wyjście: eId jednostek redakcyjnych w każdym `cytat-strukt` z rozpoznanym celem
  (także w snapshotach kanonicznych).
- Przy zmianie brzmienia eId jednostki cytowanej jest równy `element-eId` komendy.
- Jakość kotwicy zależy od odczytu celu. Błędny cel daje błędny adres, ale nigdy nie zmienia
  struktury cytatu.
- Powołania jednostek systematyzacyjnych („po rozdziale 9") nie są jeszcze odczytywane, więc
  rozdział cytowany zostaje bez kotwicy: `rozdz_9a`, a w akcie zmienianym z działami powinno być
  `dzial_2__rozdz_9a`.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0021_QuotedUnitEId_StartsWithTheTargetAddress`.
- `AmendmentTargetsExportTests.EnumeratedUnits_WithNewWording_GetACitationEach`,
  `AmendmentTargetsExportTests.Range_IsOneCitation_FromItsFirstToItsLastUnit`.
