# ADR-0119: Sankcja karna i adres internetowy oznacza eksport z brzmienia tekstu

- Status: Accepted
- Data: 2026-10-27

## Kontekst

Grupa `zawartosc-tekstowa-bez-cytatu` ma elementy semantyczne, które nie wynikają z formatowania źródła. Użytkownik
zdecydował (2026-10-27): robić `sankcja-karna` i `h`; `skrot` i `definicja-legalna` — nie teraz.

Schemat: część wspólna ma atrybut `rola="sankcja-karna"` („zdanie części wspólnej jest sankcją: zwrot wymierzający karę
(§ 79 ZTP), także administracyjną karę pieniężną (§ 81a ZTP) … rola jest atrybutem na elemencie części wspólnej”), a
element `sankcja-karna` (tekst) oznacza sankcję w treści przepisu, „jeżeli nie można jej oznaczyć przez określenie roli
części wspólnej”. Przykład autorów (Dz. U. 2024 poz. 928, art. 54–58) obejmuje elementem zwrot od „podlega” do końca
zdania, bez kropki. Parser rozpoznaje już sankcję w osobnym akapicie (`StructureProcessor.TryHandleSanction`: styl
`S_KARN`, początek „podlega”, niedokończone zdanie — dwa sygnały z trzech) i dołącza ją do zdania albo części wspólnej.
`h` ma typ `xs:anyURI`; adres stoi w tekście aktu („https://www.gov.pl/…”, „www.bip.gov.pl”).

## Decyzja

- Przebieg eksportu `PenalSanctionXml`: część wspólna, której zdanie zawiera zwrot kary, dostaje
  `rola="sankcja-karna"`; poza częścią wspólną zwrot domykający zdanie po przecinku (albo otwierający tekst) stoi
  w `sankcja-karna`, bez kończącej interpunkcji. Zwrotem kary jest „podlega” i najwyżej dwa wyrazy przed „karze” albo
  „grzywnie” — nie „podlega opłacie/zwrotowi/ochronie” i nie kara dyscyplinarna. Element obejmuje tylko zwrot, który
  w całości stoi w jednym węźle tekstu i kończy zdanie.
- Przebieg eksportu `HyperlinkXml`: `https?://…` i `www.…` w tekście treści stoją w `h`; kropka, przecinek, nawias
  i cudzysłów po adresie zostają poza nim.
- Oba przebiegi czytają brzmienie zbudowanej treści — parser i model się nie zmieniają.

## Odrzucone alternatywy

- **Każde „podlega …” jako sankcja.** „Podlega opłacie skarbowej”, „podlega zwrotowi”, „podlega ochronie” są w aktach
  częste i karą nie są.
- **Znacznik sankcji tylko z akapitu `S_KARN`** (cecha modelu ustawiana przez `TryHandleSanction`). Sankcja zapisana
  w jednym akapicie z opisem czynu („Kto …, podlega grzywnie.”), w PDF i w TXT, zostawałaby nieoznaczona.
- **Hiperłącza z pliku Worda (`w:hyperlink`).** To zwykle pozostałości kopiowania z systemów informacji prawnej na
  zwykłych wyrazach; adresem jest tekst, który akt drukuje.

## Konsekwencje

- Sankcja w zdaniu z odnośnikiem w środku zwrotu albo przerwana innym elementem zostaje bez elementu.
- Adres bez „www.” i bez schematu („gov.pl/…”) nie jest rozpoznawany.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0119_SanctionAndAddress_AreReadFromTheWording`
- `PenalSanctionAndLinkTests`
