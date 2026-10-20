# ADR-0107: Przytoczony odnośnik w obwieszczeniu jest cytowanym odnośnikiem aktu przytaczanego (`odnosnik-cyt` pod `odn_N`)

- Status: Accepted (Supersedes in part [ADR-0036](0036-przytoczone-artykuly-sa-jednostkami-aktu-przytaczanego.md): przytoczony odnośnik nie zostaje tekstem)
- Data: 2026-10-20

## Kontekst

Obwieszczenie tekstu jednolitego aktu wykonawczego przytacza obok pominiętych paragrafów aktu zmieniającego jego
odnośnik: „2. Podany w załączniku … tekst jednolity rozporządzenia nie obejmuje odnośnika nr 2 oraz § 2 i § 3
rozporządzenia … (Dz. U. poz. 425), które stanowią: „²⁾ Niniejsze rozporządzenie służy stosowaniu: 1) …; 2) ….”
„§ 2. … § 3. ….”" (Dz. U. 2025 poz. 1358, plik 334858). Źródło ma na to osobny styl (`ODNONIKWOTJ…`), ale bywa
i bez niego.

[ADR-0036](0036-przytoczone-artykuly-sa-jednostkami-aktu-przytaczanego.md) buduje jednostki tylko z przytoczenia
złożonego z całych artykułów; każde inne zostawało akapitami tekstu. Przytoczony odnośnik wychodził więc jako
`fragment` za zdaniem punktu, a jego znacznik „²⁾" trafiał do rejestru odnośników dokumentu: wiązał się z własnym
odnośnikiem obwieszczenia albo aktu o tym samym numerze i wstawiał jego treść w przytoczenie (w 297258 treść
odnośnika nr 1 tekstu jednolitego stała w obwieszczeniu, a w akcie — tylko odsyłacz).

Odnośnik cytowany komendą nowelizacji jest już `odnosnik-cyt` pod adresem `odn_N`
([ADR-0059](0059-odnosnik-jako-przedmiot-komendy-jest-cytowanym-odnosnikiem.md)), z wyliczeniem jako
`punkt-odnosnik` ([ADR-0078](0078-wyliczenie-w-odnosniku-to-jego-jednostki.md)). `tresc-strukt` dopuszcza
`odnosnik-cyt` (grupa `cytat-wspolne`).

## Decyzja

**Przytoczenie, które zaczyna się znacznikiem odnośnika wymienionego w treści jednostki obwieszczenia („nie obejmuje
odnośnika nr 2 …"), jest tym odnośnikiem: `Quotation` z `AmendmentContent.Footnote`, w eksporcie `tresc-strukt`
z `odnosnik-cyt` pod adresem `odn_N` aktu przytaczanego.**

- Rozpoznanie: `QuotedFootnoteReader.Cited` — dwa zgodne sygnały: odnośnik nazwany w treści jednostki i znacznik
  o tym numerze na początku przytoczenia (także złożony indeksem górnym razem z cudzysłowem). Akapit z oznaczeniem
  artykułu albo paragrafu w środku przytoczenia je wyklucza.
- Treść buduje to samo co w ADR-0059 (`QuotedFootnoteReader.Build`: znacznik zdjęty z treści), eksport — ten sam
  element (`FootnoteContentXml.Quoted`). Akt przytaczany i prefiks adresu — jak dla przytoczonych artykułów
  (`JournalReferenceService.ReadSingleAct`, `ActPrefixXml`).
- Przytoczony odnośnik i przytoczone za nim paragrafy to osobne przytoczenia, każde we własnym `tresc-strukt`,
  w kolejności z dokumentu.

## Odrzucone alternatywy

- **Zostawić tekstem** (ADR-0036). Treść odnośnika zostaje, ale bez adresu, a znacznik wiąże się z cudzym odnośnikiem
  i przenosi jego treść w złe miejsce.
- **Rozpoznanie po samym znaczniku albo po samym stylu.** „2)" na początku przytoczenia to równie dobrze przytoczony
  punkt; styl `ODNONIKWOTJ…` ma tylko część źródeł. Odnośnik wymieniony w treści jednostki rozstrzyga, czym jest
  przytoczenie.

## Konsekwencje

- W korpusie DOCX 57 przytoczonych odnośników w 49 plikach wychodzi jako `odnosnik-cyt` (20 z wyliczeniem jako
  `punkt-odnosnik`); przytoczeń zostawionych tekstem jest 244 akapity w 20 obwieszczeniach zamiast 321 w 68. W 5 plikach
  własny odnośnik obwieszczenia albo aktu wraca na swój znacznik. Zgodność ze schematem bez zmian.
- Przytoczony odnośnik bez numeru („odnośnik oznaczony gwiazdką") i kilka odnośników wymienionych jednym zwrotem
  („odnośników nr 1 i 2") zostają tekstem.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0107_FootnoteQuotedInAnAnnouncement_IsAQuotedFootnoteOfTheQuotedAct`
- `AnnouncementTests` — przytoczony odnośnik w modelu i w eksporcie, znacznik z cudzysłowem w indeksie górnym,
  znacznik innego odnośnika i przytoczony punkt bez wymienionego odnośnika zostają tekstem, zgodność ze schematem.
