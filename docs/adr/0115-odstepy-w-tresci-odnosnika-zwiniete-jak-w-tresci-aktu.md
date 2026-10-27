# ADR-0115: Odstępy w treści odnośnika są zwinięte jak w treści aktu — twarda spacja wychodzi zwykłą spacją

- Status: Accepted
- Data: 2026-10-27

## Kontekst

Szablon RCL wstawia twarde spacje (U+00A0, rzadziej U+202F) po wyrazach jednoliterowych, po skrótach („art.”,
„poz.”, „Dz.”) i między liczbą a „r.” — w korpusie DOCX to ok. 19 % odstępów między wyrazami. Treść jednostek
przechodzi przez `StringExtensions.CollapseWhitespace` (`\s+` → spacja; w .NET `\s` obejmuje `\p{Z}`, więc także
twardą spację), a adapter PDF zapisuje każdy glif odstępu zwykłą spacją. Treść odnośników (`ParserOrchestrator.
RegisterFootnotes`) była jedyną drogą bez zwinięcia: eksport korpusu DOCX niósł 95 940 twardych spacji, 142 wąskie,
117 tabulatorów i ok. 120 podwójnych spacji w `odnosnik`, a z nich także w `now:nowy-tekst` (zagnieżdżony odnośnik)
i w `warunek` (termin czytany z odnośnika). Ten sam akt z DOCX i z PDF różnił się przez to niewidocznymi znakami.

Przykład autorów schematu DU20260000252 (z tego samego aktu co plik korpusu 361913_3209300) ma zwykłe spacje we
wszystkich zdaniach, także w odnośnikach; twarde spacje zostawia tylko w `Tytul`, `data` i `tytul`. Schemat ma
osobny znacznik nierozdzielności wiersza, `<tpg:typografia nieprzenosic="true"/>`; żaden przykład go nie używa.

## Decyzja

- Akapity odnośnika mają odstępy zwinięte tak jak tekst bloku: `CollapseWhitespace().Trim()` w
  `ParserOrchestrator.RegisterFootnotes` — jedynym miejscu, gdzie powstaje `Saga.Model.Footnote` z bloków źródła.
  Obejmuje to DOCX i PDF oraz wszystkich odbiorców rejestru odnośników (`odnosnik`, fragment odnośnika w
  `now:nowy-tekst`, `warunek` z wersji brzmienia).
- Twarda spacja to wskazówka łamania wiersza, nie znak tekstu — jak łącznik nierozdzielający i miękki dywiz, które
  czytniki już zapisują dywizem albo pomijają. Znaki widoczne (półpauza, cudzysłowy) zostają jak w akcie.

## Odrzucone alternatywy

- **Zachować twarde spacje w odnośnikach.** Rozbieżność z treścią aktu tego samego dokumentu, z eksportem PDF i z
  przykładem autorów; `stary-tekst`/`nowy-tekst` to `xs:token`, którego zwijanie nie obejmuje U+00A0, więc wyrazy z
  twardą spacją i ze zwykłą nie są równe przy maszynowym nanoszeniu zmian.
- **Twarda spacja → `<tpg:typografia nieprzenosic="true"/>`.** Element wolno postawić tylko w treści mieszanej — nie
  w atrybutach `stary-tekst`, `nowy-tekst`, `warunek` ani w metadanych; przykład w FAQ schematu stawia go zamiast
  spacji („14<typografia …/>dni”), więc odbiorca pomijający typografię czyta „14dni”. Żaden przykład autorów go nie
  używa, a znaczniki z PDF-u nie są do odczytania. Regułę łamania (wyraz jednoliterowy, skrót, liczba z jednostką)
  odtworzy skład, jak robi to skład Dziennika Ustaw.
- **Zwijanie całego eksportu.** Zniszczyłoby odstępy, które coś znaczą: końce wiersza między akapitami przytoczenia
  w `cytat-w-tekscie` i separator cyfr w `mn` formuł MathML.

## Konsekwencje

- Eksport 1739 z 2321 plików korpusu DOCX zmienia się wyłącznie w białych znakach treści odnośników (i w `nowy-tekst`,
  `warunek` z nich wziętych); werdykty oceny korpusu i zgodność ze schematem bez zmian — wzorce czytające odnośniki
  używają `\s`.
- Tabulator w akapicie odnośnika („1)⇥rozporządzenia …”) staje się spacją przed rozkładem treści odnośnika na jednostki
  (`FootnoteContent`); wzorce oznaczeń jednostek przyjmują dowolny odstęp.
- Metadane (`Tytul`, `data`, `tytul`) mają zwykłe spacje od dawna, choć przykład autorów zostawia tam twarde — pytanie
  do autorów schematu, czy to zamierzone.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0115_FootnoteText_HasItsWhitespaceCollapsed_LikeTheTextOfTheAct`
