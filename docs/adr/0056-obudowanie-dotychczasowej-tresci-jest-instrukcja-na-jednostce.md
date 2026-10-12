# ADR-0056: Obudowanie dotychczasowej treści ustępem jest instrukcją na jednostce, w której powstaje nowy poziom

- Status: Accepted
- Data: 2026-10-12

## Kontekst

Dodanie drugiego ustępu do artykułu bez ustępów poprzedza zwrot „w art. 24 dotychczasową treść oznacza się jako
ust. 1 i dodaje się ust. 2 w brzmieniu:" (w kodeksie: „jako § 1"). Cytat z dodawanym ustępem wychodził poprawnie
(`dodaj-po`, ust. 1), ale samo obudowanie treści — zmiana struktury artykułu — nie zostawiało w eksporcie śladu.

Schemat ma na to instrukcje `now:dodaj-ustep` i `now:dodaj-paragraf` („obudowująca dotychczasową treść jednostki
redakcyjnej dodatkowym poziomem struktury"), z wymaganym adresem jednostki, a przykłady autorów schematu stawiają
ją na artykule, obok cytatu z dodawanym ustępem — także wtedy, gdy przepis niczego nie dodaje („…oznacza się jako
ust. 1 i:" przed wyliczeniem dalszych zmian).

## Decyzja

- **Model:** `Amendment.WrapsContentAs` (`ContentWrap.Paragraph` albo `CodeParagraph`) i `Amendment.WrappedUnit` —
  jednostka, w której powstaje nowy poziom: cel przepisu bez poziomu ustępu.
- **Rozpoznanie** (`AmendmentStateManager.NoteContentWrap`): zwrot „dotychczasową treść oznacza się jako ust. 1"
  („jako § 1" → paragraf kodeksu). Obudowanie czeka w `ParsingContext.PendingContentWraps` na nowelizację tego
  przepisu; gdy przepis nie niesie żadnej innej zmiany, jest nowelizacją samo w sobie — bez treści cytowanej.
- **Eksport** (`AmendmentXmlConverter.AddContentWrapInstruction`): `now:dodaj-ustep` / `now:dodaj-paragraf`
  z adresem jednostki, przed tekstem zdania. Bez adresu instrukcji nie ma — schemat go wymaga.

## Odrzucone alternatywy

- **Wyprowadzać obudowanie w eksporcie z samego cytatu** („dodaj po ust. 1" w artykule bez ustępów). Eksport nie
  zna aktu zmienianego, a ten sam cytat powstaje, gdy artykuł ustępy już ma.
- **Osobna nowelizacja dla obudowania.** Jednostka niesie jedną nowelizację; obudowanie i dodanie stoją w jednym
  zdaniu i dotyczą tego samego artykułu.

## Konsekwencje

- W korpusie przybywa 47 instrukcji `now:dodaj-ustep` (38 plików) i 6 `now:dodaj-paragraf` (5 plików) — wszystkie 53 zdania z tym zwrotem; poza tym eksport się nie zmienia, zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory). Komendy zgodne z przykładami autorów schematu (`CorpusValidator --compare-reference`): DU/2026/26 — 102 ze 105, DU/2026/252 — 333 z 345.
- Zwrot odwrotny („oznaczenie ust. 1 skreśla się" → `now:usun-ustep`) nie jest rozpoznawany.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0056_MarkingExistingContentAsParagraphOne_IsAnInstructionOnTheArticle`
- `AmendmentTargetsExportTests` — obudowanie z dodaniem ustępu, paragraf kodeksu, obudowanie bez innej zmiany,
  przepis bez obudowania.
