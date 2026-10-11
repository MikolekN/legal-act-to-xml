# ADR-0039: Akapit w stylu jednostki bez oznaczenia w treści jest dalszą treścią jednostki, za którą stoi

- Status: Accepted (wykonuje alternatywę odłożoną w [ADR-0024](0024-jednostka-bez-oznaczenia-nie-ma-eid.md))
- Data: 2026-10-11

## Kontekst

Dla ustępu, punktu, litery i tiretu potok przyjmował pojedynczy sygnał: sam styl akapitu wystarczał, żeby
powstała jednostka. Akapit złożony stylem punktu, ale bez „1)" w treści, stawał się punktem bez numeru —
w eksporcie `nie-ztp-punkt` z `brak-eid="jednostka-nieoznaczona"` ([ADR-0024](0024-jednostka-bez-oznaczenia-nie-ma-eid.md)),
a jednostki pod nim dziedziczyły rodzinę `nie-ztp-*`.

ADR-0024 rozstrzygnął tylko, **jak zapisać** taką jednostkę, a wchłonięcie akapitu do poprzedniej jednostki
odłożył: „wymaga modelu treści z kilkoma akapitami, tabelą i wzorem, którego jeszcze nie ma". Ten model już
jest — dalsza treść jednostki ([ADR-0029](0029-akapit-bez-oznaczenia-jest-dalsza-trescia-jednostki.md)) —
ale obejmował tylko akapity bez stylu jednostki.

Po rozpoznaniu załączników aktów wykonawczych w korpusie zostało 1362 jednostek `nie-ztp-*`, z czego ponad
połowa to akapity prozy w stylu jednostki: treść podstawy programowej cytowana w rozporządzeniu zmieniającym
(163 „punkty" w jednym dokumencie), wiersz „w którym poszczególne symbole oznaczają:", „albo" między punktami,
objaśnienie rozdziału klasyfikacji budżetowej.

## Decyzja

**Jednostka potrzebuje oznaczenia w treści. Akapit w stylu ustępu, punktu, litery albo tiretu, którego treść
nie zaczyna się niczym, co wygląda na oznaczenie, jest dalszą treścią jednostki, za którą stoi
(`BaseEntity.Continuation`) — w treści aktu i w treści cytowanej.**

- „Wygląda na oznaczenie" rozstrzyga jedno miejsce — `UnitDesignation.LooksDesignated`: liczba z kropką lub
  nawiasem (także „3-5.", „3.8."), oznaczenie wielopoziomowe bez znaku końcowego („2.1 wykorzystuje …"), do
  trzech liter z nawiasem („A)", „l)"), liczba rzymska, myślnik w każdej postaci (także „‒", U+2012). Akapit
  z takim początkiem zostaje jednostką bez odczytanego oznaczenia, jak w ADR-0024.
- Dalszą treścią akapit zostaje tylko wtedy, gdy jest jednostka do kontynuowania. Pusty ustęp niejawny
  („Art. 5." w osobnym wierszu) bierze akapit jako swoją treść. W treści cytowanej akapit otwierający cytat
  zostaje jednostką — cytat strukturalny nie staje się przez to cytatem tekstu (`zastap` → `zastap-tekst`).
- Klasyfikacja akapitu się nie zmienia: styl nadal prowadzi granice nowelizacji. Reguła działa dopiero przy
  budowie struktury (`ParserOrchestrator.ProcessBlock` za `HandleAmendmentFlow`, `AmendmentBuilder`).
- Artykuł ma regułę ostrzejszą i wcześniejszą: bez „Art." nie jest rozpoznawany w ogóle.
- Budowa jednostek załącznika (`AnnexUnits`) zostaje przy dotychczasowym zachowaniu
  (`ParsingContext.StyleOnlyUnitsStayUnits`): tam jednostka bez oznaczenia powtarza adres i tym samym mówi,
  że treść załącznika nie jest w całości wyliczeniem.

## Odrzucone alternatywy

- **Zostawić jednostki bez oznaczenia** (stan z ADR-0024). Akapit prozy nie jest punktem; jednostka bez
  oznaczenia rozcina wyliczenie, w którym stoi, i wypycha jednostki pod sobą do rodziny `nie-ztp-*`, choć
  same są poprawne.
- **Reguła w klasyfikatorze** (styl bez sygnatury tekstowej daje `Unknown`, jak dla artykułu). Klasyfikacja
  prowadzi granice nowelizacji: akapit w stylu ustawy matki zamyka cytat. Zmiana rodzaju przed tym krokiem
  przesuwała granice — w pierwszym przebiegu druga treść cytowana trafiała do tej samej jednostki co pierwsza
  i ją zastępowała.
- **Każdy akapit bez odczytanego oznaczenia jako dalsza treść**, bez sprawdzania kształtu początku. Litera
  „l)" w miejscu „1)", „A)", „3-5. (uchylone)" i tiret pisany „‒" są jednostkami, których oznaczenia wzorce
  nie znają; zdegradowanie ich do tekstu gubi strukturę (w pierwszym przebiegu znikły tirety 10 dokumentów).
- **W treści cytowanej także akapit otwierający cytat jako tekst.** „pkt 2.1 i 2.2 otrzymują brzmienie"
  stawało się zamianą tekstu zamiast zamiany jednostek — 148 cytatów zmieniało funkcję.

## Konsekwencje

- W korpusie zmienia się 50 dokumentów: znika 744 jednostek bez oznaczenia (603 `nie-ztp-punkt`, 69
  `nie-ztp-ustep`, 55 `nie-ztp-litera`, 12 `nie-ztp-paragraf-kodeksu`, 5 `nie-ztp-tiret`), 614 akapitów
  wychodzi jako `fragment`, a 147 jednostek pod nimi wraca do zwykłych elementów (`punkt`, `ustep`, `litera`).
  Zgodność ze schematem i pokrycie tekstu bez zmian.
- Wyliczenie stojące za takim akapitem należy do jednostki, którą akapit kontynuuje: punkty legendy „1) K – …"
  mają adres `art_1__ust_1__pkt_1`, a nie `art_1__pkt_1`.
- Jednostka niesie jedną nowelizację. Druga treść cytowana pod tą samą jednostką (akapity w stylu zmiany
  rozdzielone dalszą treścią) dołącza do pierwszej (`AmendmentFinalizer.AssignToOwner`) — wcześniej ją
  zastępowała, a ta reguła sprawia, że jednostka częściej zostaje ta sama.
- Wzorzec tiretu nadal zna tylko dywiz i półpauzę; tiret pisany innym myślnikiem rozpoznaje styl, a jego
  znak zostaje w treści (backlog).

## Weryfikacja

- `ArchitectureDecisionTests.Adr0039_UnitStyleWithoutADesignation_ContinuesTheUnitItFollows`
- `ArchitectureDecisionTests.Adr0024_UnitWithoutADesignation_HasNoEId_OnlyAReason` — przykład zmieniony
  świadomie: jednostką bez oznaczenia jest tam teraz pierwszy akapit artykułu przed „2.", nie akapit w stylu
  ustępu.
- `UndesignatedUnitTests` — kształt oznaczenia, treść aktu, tiret z myślnikiem cyfrowym, pusty ustęp niejawny,
  treść cytowana, druga treść cytowana pod tą samą jednostką; `MissingEIdReasonTests`.
