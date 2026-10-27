# ADR-0122: Komendy w osobnych zdaniach jednostki to osobne nowelizacje

- Status: Accepted
- Data: 2026-10-27

## Kontekst

Jednostka aktu zmieniającego niesie jedną nowelizację (`IHasAmendments.Amendment`), a każdy krok komendy czytał całą
treść jednostki: rodzaj operacji (`AmendmentFinalizer.DetectOperationType`), cele (`StructureProcessor`), zdanie jako
cel (`SentenceTargets`), cel ręczny (`ManualTargets`), przedmiot cytatu (`QuotedTextSubject`). ZTP rozdziela zmiany na
jednostki wyliczenia (§ 94 ust. 2), ale dwie krótkie komendy w dwóch zdaniach jednego punktu są poprawnym zapisem:
„2) art. 127 uchyla się. W art. 128 zdanie drugie otrzymuje brzmienie: „…”.". Parser brał wtedy cały punkt za jedno
uchylenie: „uchyla się" rozstrzygało rodzaj operacji, słowo „zdanie" z drugiego zdania robiło z celu instrukcję ręczną
(`uchyl-recznie` na `art_127` z tekstem obu zdań), a druga komenda przepadała — jej cytat zostawał elementem `fragment`
za punktem.

W korpusie DOCX (2321 aktów) takiego zapisu nie ma: 599 wierszy niesie kilka komend w jednym zdaniu, żaden — w osobnych
zdaniach (trafienia wzorca to proza tekstów jednolitych: „uchyla się od zawarcia umowy"). Błąd znalazły próby
syntetyczne.

## Decyzja

- Gdy jednostka ma kilka zdań z komendą (czasownik zmiany albo uchylenia poza cudzysłowem,
  `AmendmentCommandParser.HasChangeVerb`), a każda komenda przed ostatnią kończy się w swoim zdaniu — zmiana w tekście
  albo uchylenie rozpoznane jak w całej jednostce (`AmendmentStateManager.IsRepealCommand`), bez zwrotu zapowiadającego
  brzmienie — każde takie zdanie jest osobną komendą (`SentenceCommands.Read`). Zdania są te same co w eksporcie
  (`ParsingFactories.SplitIntoSentences`); granica wewnątrz cudzysłowu zdania komendy nie kończy.
- Cele każdej komendy pochodzą z jej zdania, czytanego w miejscu wskazanym zdaniem poprzednim, a dla pierwszego —
  przepisem nadrzędnym (`StructureProcessor.DetectAmendmentTargets`): „W art. 5 ust. 2 uchyla się. Ust. 3 otrzymuje
  brzmienie:" daje `art_5__ust_2` i `art_5__ust_3`. Reguły celów się nie zmieniają: wyliczenie i przedział
  ([ADR-0016](0016-wyliczenie-celow-to-osobne-komendy.md)), zdanie nazwane
  ([ADR-0102](0102-zdanie-nazwane-w-komendzie-jest-celem.md)), cel bez adresu — `*-recznie`
  ([ADR-0100](0100-cel-w-zalaczniku-bez-adresu-to-instrukcja-reczna.md)).
- Komendy wcześniejszych zdań przechodzą zwykłą drogę komendy z tekstem i celami swojego zdania. Powstałe nowelizacje
  trafiają do `IHasAmendments.EarlierSentenceAmendments` z numerem zdania (`Amendment.CommandSentence`). Eksport stawia
  ich instrukcje w tym zdaniu (`AmendmentXmlConverter.ToXml` dla listy nowelizacji), a daty wejścia w życie biorą
  z niego (`EffectiveDatePropagator`).
- Komenda ostatniego zdania zostaje nowelizacją jednostki (`Amendment`). Jej cele, dziedziczone też przez jednostki
  niższe, i tekst komendy (`AmendmentCollector.CommandText`, `ParsingContext.CommandTexts`) pochodzą tylko z tego zdania.
- Gdy któraś wcześniejsza komenda nie kończy się w swoim zdaniu (np. podaje brzmienie w cudzysłowie), jednostka zostaje
  jedną komendą, jak dotąd.

## Odrzucone alternatywy

- **Lista nowelizacji zamiast `Amendment`.** Dotyka każdego miejsca, które czyta nowelizację jednostki: konwerterów,
  dat, walidacji i około 190 użyć w testach. Treść cytowaną może mieć tylko komenda ostatniego zdania, więc pojedyncza
  właściwość zachowuje swoje znaczenie.
- **Wcześniejsza komenda dołączona do nowelizacji jednostki**, jak zamiana znaku z własnym celem
  ([ADR-0054](0054-zamiana-znaku-konczacego-jednostke-ma-wlasny-cel.md)). Działa tylko dla zmian w tekście: uchylenie
  i zmiana brzmienia nie zmieszczą się w jednym `OperationType`.
- **Ostrzeżenie zamiast podziału**, jak przy komendzie złożonej w jednym zdaniu
  ([ADR-0052](0052-zmiana-wyrazow-jest-instrukcja-zamien-tekst.md)). Gubi poprawnie zapisaną komendę, choć tekst
  jednoznacznie ją wyznacza.
- **Podział na zdania każdej jednostki przed rozpoznaniem komend.** Zmieniałby drogę każdej jednostki, także tych
  z jedną komendą i ze zdaniami bez komend. Podział zachodzi tylko tam, gdzie zdań z komendą jest kilka.

## Konsekwencje

- Kilka nowelizacji jednostka niesie tylko przy komendach w osobnych zdaniach. W jednym zdaniu nadal jest jedna
  nowelizacja (komenda złożona, ADR-0052 i ADR-0054).
- Kod, który czyta nowelizacje jednostki, musi uwzględniać też `EarlierSentenceAmendments`. Dziś robią to konwertery
  jednostek, `EffectiveDatePropagator`, `TextChangeWording`, `AnnexUnits`, `AnnouncementBody` i
  `CanonicalDtoXmlSerializer`. Wcześniejsze nowelizacje nie mają treści cytowanej, więc obchód jej jednostek
  (`EditorialUnitWalker`) się nie zmienia.
- Zmiany załączników odesłaniem i obudowanie treści (ADR-0056, ADR-0058) czyta się ze zdania ostatniej komendy.
  Wcześniejsza komenda tego rodzaju nie kończy się w swoim zdaniu, więc nie wywołuje podziału.
- Korpus DOCX: eksport bez zmian (poza losowymi GUID-ami), zgodnych ze schematem nadal 1996, werdykty wyroczni bez
  zmian. Regułę pilnują testy.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0122_CommandsInSeparateSentences_AreSeparateAmendments_EachInItsSentence`
- `SentenceCommandsTests` (m.in. `RepealAndReplacedSentence_AreTwoCommands_EachInItsSentence`,
  `NextSentence_IsReadInThePlaceOfThePreviousOne`, `EarlierCommand_EntersIntoForceWithItsSentence`,
  `Read_KeepsTheUnitOneCommand_UnlessEveryEarlierCommandEndsInItsSentence`)
- `ActSchemaTests.CommandsInSeparateSentences_AreSchemaValid`
