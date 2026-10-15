# ADR-0089: Indeks górny oznaczenia w TXT odtwarza ciąg numeracji

- Status: Accepted
- Data: 2026-10-15

## Kontekst

Jednostki dodane między istniejące mają oznaczenie z indeksem górnym: „Art. 29²", „§ 2¹", ustęp „2¹.", punkt
„5¹)". DOCX i PDF zapisują indeks górny osobno (DOCX — formatowaniem, PDF — położeniem znaku), a czytniki oddają go
kanałem „[…]" („Art. 29[2]."). Zwykły tekst indeksu górnego nie ma: eksport do TXT (LibreOffice, Word) pisze
„Art. 292.", „§ 21.", „51)". Parser czytał to jako artykuł 292, paragraf 21, punkt 51, więc jednostka trafiała pod
niewłaściwy adres, a jej ustępy i punkty — pod niewłaściwy artykuł. Miara `--format-parity` na korpusie
przekonwertowanym do TXT: 7798 z 18511 jednostek, których TXT nie odtwarzał, to jednostki z indeksem górnym
w adresie (2512 same oznaczone indeksem, reszta w nich zawarta) — niemal wszystkie w tekstach jednolitych.

## Decyzja

Czytnik TXT odtwarza indeks górny z ciągu numeracji (`SuperscriptDesignations`): oznaczenie, które zaczyna się
oznaczeniem poprzedniej jednostki tego samego poziomu, nie jest jej zwykłym następnikiem, a reszta jest liczbą (z
literą) większą od indeksu poprzedniej jednostki, ma tę resztę w indeksie górnym — „Art. 29." → „Art. 292." daje
„Art. 29[2].". Ciągi: artykuły w dokumencie, paragrafy i ustępy w artykule, punkty w jednostce wyliczającej.
Treść cytowana (od „ do ”) jest pomijana i ciągów nie zmienia.

## Odrzucone alternatywy

**Zostawić oznaczenia tak, jak stoją.** Odrzucone: TXT tracił wtedy największą część struktury tekstów jednolitych,
a informacja o indeksie jest w dokumencie — w ciągu numeracji, z którego czyta ją także człowiek.

**Rozstrzygać w parserze, wspólnie dla wszystkich formatów** (podpowiedź numeracji `NumberingHint`). Odrzucone:
DOCX i PDF niosą indeks górny wprost, a reguła z ciągu działałaby także na nie — zmieniłaby wynik DOCX tam, gdzie
akt ma zwykłą lukę w numeracji. Wnioskowanie należy tam, gdzie informacja ginie: do czytnika formatu, który jej
nie ma.

**Każdy skok numeracji czytać jako indeks górny** (np. „Art. 4." → „Art. 41." bez warunku przedrostka). Odrzucone:
luka w numeracji bywa prawdziwa; warunek, że oznaczenie zaczyna się poprzednim i reszta rośnie, odróżnia dalszy
ciąg oznaczenia od zwykłej luki.

**Uwzględniać treść cytowaną.** Odrzucone: przytacza jednostki innego aktu w dowolnym wyborze („pkt 11 i 12"), więc
ciąg nie mówi o nich nic, a pomyłka przeniosłaby indeks do treści nowelizacji.

## Konsekwencje

- Oznaczenie zaczynające się poprzednim przy prawdziwej luce (po „Art. 1." od razu „Art. 12.") zostaje odczytane
  jako „Art. 1[2]." — w akcie z kompletną numeracją (teksty jednolite zachowują jednostki uchylone) to nie występuje.
- Wymaga zrównoważonych cudzysłowów: niezamknięty cudzysłów wyłącza odtwarzanie do końca dokumentu.
- DOCX i PDF bez zmian — reguła działa tylko w czytniku TXT.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0089_TextSuperscripts_AreRestoredFromTheSequence_OnlyOutsideQuotations`
- `SuperscriptDesignationsTests`
