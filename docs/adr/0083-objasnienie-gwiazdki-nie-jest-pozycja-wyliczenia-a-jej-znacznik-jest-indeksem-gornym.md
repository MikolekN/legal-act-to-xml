# ADR-0083: Objaśnienie gwiazdki nie jest pozycją wyliczenia, a jej znacznik jest indeksem górnym

- Status: Accepted
- Data: 2026-10-14

## Kontekst

Załączniki i tabele objaśniają gwiazdką: tytuł „DROGI KRAJOWE KLASY A I S*)", a pod wyliczeniem akapit „*) Zgodnie
z § 11 …"; w tabelach „(*)", „(**)". Znacznik bywa w indeksie górnym w całości („[(*)]") albo tylko nawias („*[)]").
Szablon składa objaśnienie stylem części wspólnej punktów, więc ze stylem stawało się ono częścią wspólną wyliczenia
załącznika, a bez stylu — akapitem, przez który cała treść załącznika przestawała być wyliczeniem
([ADR-0028](0028-wyliczenie-w-zalaczniku-buduje-potok-aktu.md)) i zostawała akapitami (jeden akt: 107 jednostek mniej bez stylów). Znaczniki zostawały
w eksporcie w zapisie kanału tekstowego („[*)]", „[(**)]", „[I)]") — [ADR-0047](0047-znacznik-bez-odnosnika-tego-aktu-wychodzi-indeksem-gornym.md) rozstrzygnął to tylko
dla numerów.

## Decyzja

**Akapity na końcu treści załącznika zaczynające się znacznikiem gwiazdki („*)", „*[)]", „[*)]", „**)") są
objaśnieniami: wyliczenie załącznika buduje się z akapitów przed nimi, a one zostają akapitami za jednostkami
(`AnnexBuilder.StructureContent`). Znacznik objaśnienia — gwiazdka, liczba rzymska odnośnika do odnośnika („[I)]")
i sam nawias przy gwiazdce zwykłej — wychodzi w tekście jako `sup` (`InlineTextXml.EmbedSuperscripts`).**

- Objaśnienie nie jest łączone ze znacznikiem w tytule jak odnośnik — to akapit treści, nie przypis Worda.
- Nawias kwadratowy z innym tekstem („[zł/GJ]", „[%]" w nagłówkach kolumn) zostaje tekstem.

## Odrzucone alternatywy

- **Styl rozstrzyga** (część wspólna). Objaśnienie gwiazdki nie domyka zdania wprowadzenia do wyliczenia; przy sporze
  stylu z treścią rozstrzyga treść.
- **Objaśnienie jako odnośnik (`odnosnik`) przy znaczniku w tytule.** Schemat ma odnośnik dla przypisu aktu; objaśnienie
  wpisane akapitem jest treścią załącznika, a znacznik w tytule nie wskazuje go jednoznacznie, gdy gwiazdek jest kilka.

## Konsekwencje

- W korpusie zmienia się 64 dokumenty: 509 znaczników objaśnień wychodzi jako `sup` zamiast zapisu kanału tekstowego („[(*)]”, „[*)]”, „[I)]”) i w jednym akcie dwa objaśnienia przestają być częścią wspólną punktów. Zgodność bez stylów: rozporządzenia 97,74 % → 98,11 % jednostek (brakujących 655 → 548). Zgodnych ze schematem nadal 2279 z 2321 (oba walidatory).

## Weryfikacja

- `ArchitectureDecisionTests.Adr0083_AsteriskNote_IsNotAnItemOfTheEnumeration_AndItsMarkerIsASuperscript`
- `AnnexTests.AsteriskNoteAfterTheEnumeration_StaysText_AfterTheUnits`,
  `AnnexTests.AsteriskMarkers_AreExportedAsSuperscripts_NotInTheTextChannelNotation`.
