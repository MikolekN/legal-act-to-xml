# ADR-0043: Obwieszczenie, które nie ogłasza aktu, jest w eksporcie korzeniem `obwieszczenie`, a jego załącznik — załącznikiem obwieszczenia

- Status: Accepted
- Data: 2026-10-11

## Kontekst

Obwieszczenie w dziennikach urzędowych bywa dwojakie. Obwieszczenie w sprawie ogłoszenia tekstu jednolitego
niesie w załączniku akt (ADR-0031): korzeniem eksportu jest `obwieszczenie`, a akt stoi w nim jako osobny
element. Pozostałe obwieszczenia — stawki opłat, wskaźniki waloryzacji, wykazy jednostek, wolne stanowiska
asesorskie (284 dokumenty korpusu, 12 %) — aktu nie ogłaszają. Ich budowa jest stała: nagłówek (rodzaj, organ,
data, przedmiot), akapit „Na podstawie art. … ogłasza się …", czasem wyliczenie albo dalsze akapity, podpis,
a w 90 dokumentach załącznik („Załącznik do obwieszczenia Ministra … z dnia … (M.P. poz. …)") z tytułem
i tabelą albo wyliczeniem.

Parser traktował je jak akt: treść sprzed pierwszego artykułu ginęła, a wiersz „Załącznik do obwieszczenia …"
otwierał „ogłaszany akt", którego nie było. Eksport pisał pusty element `ustawa` (niezgodny ze schematem),
bez podpisu i bez załącznika — z 8924 akapitów tych dokumentów zostawało 10 %.

Schemat przewiduje dla `obwieszczenie` strefę tytułową z organem, `podstawa-prawna`, punkty, podpisy
i załączniki; element ogłaszanego aktu jest opcjonalny.

## Decyzja

**Dokument zaczynający się nagłówkiem obwieszczenia, który nie ogłasza aktu i sam nie jest zbudowany
z artykułów ani paragrafów, jest obwieszczeniem w całości: eksport pisze korzeń `obwieszczenie` bez elementu
aktu, a to, co stoi za wierszem „Załącznik do obwieszczenia …", jest załącznikiem obwieszczenia.**

- **Czy obwieszczenie ogłasza akt**, rozstrzyga `AnnouncementBoundary.Describe` z dwóch sygnałów, z których
  wystarcza jeden: przedmiot obwieszczenia („w sprawie ogłoszenia jednolitego tekstu …") albo słowo rodzaju
  aktu tuż za wierszem załącznika („USTAWA", „ROZPORZĄDZENIE …", także w pisowni spoza wzorca).
- **Treść** przechodzi tą samą drogą co treść obwieszczenia tekstu jednolitego (`AnnouncementUnits`), a wynik
  niesie `LegalDocument.Announcement` z `AnnouncesAct = false`.
- **Podstawa prawna**: pierwszy akapit treści zaczynający się od „Na podstawie art. …" trafia do
  `LegalDocument.LegalBasis` i wychodzi jako `podstawa-prawna` — jak w akcie wykonawczym. Akapit, po którym
  od razu stoi pozycja wyliczenia, jest wprowadzeniem do tego wyliczenia i zostaje z nim.
- **Podpis** (także kilka formuł obwieszczenia wydanego wspólnie) i **załączniki** (`AnnexBuilder` ze
  wskazaniem „Załącznik do obwieszczenia …") stoją pod korzeniem.

## Odrzucone alternatywy

- **Potok aktu z dostawionym artykułem** (cała treść jako jeden artykuł bez oznaczenia pod `ustawa`). Rodzaj
  w metadanych mówiłby „obwieszczenie", a korzeń — „ustawa"; schemat nie dałby organu wydającego, podstawy
  prawnej ani podpisu w strefie tytułowej ustawy.
- **Korzeń `inny-akt-prawny`.** W schemacie jest to element aktu ogłaszanego w obwieszczeniu, nie korzeń.
- **Rozpoznanie po samym wierszu „Załącznik do obwieszczenia …"** (stan dotychczasowy: jest wiersz — jest
  akt). Wiersz otwiera zarówno tekst jednolity, jak i wykaz albo tabelę; rozróżnia je dopiero to, co stoi za nim.
- **Rozpoznanie po klasyfikatorze dokumentu** (`IsConsolidatedText`). Klasyfikator waży sygnały całego
  dokumentu i bywa niepewny; granica ma zależeć od dwóch miejsc w tekście, które da się wskazać.

## Konsekwencje

- W korpusie: 280 dokumentów wychodzi pod korzeniem `obwieszczenie` (217 z `podstawa-prawna`, 84 z załącznikami —
  370 tabel); dokumentów zgodnych ze schematem 1685 → 1915 z 2321 (oba walidatory, żaden nie odpadł); pustych
  elementów `ustawa` 304 → 24; pokrycie tekstu obwieszczeń 10 % → 83 % (zgubionych akapitów 8014 → 1542).
- Treść inna niż podstawa prawna — wyliczenie po wprowadzeniu, dalsze akapity, tabela przed podpisem — jest
  w modelu (`Announcement.Content`), ale eksport jej jeszcze nie pisze (63 dokumenty bez `podstawa-prawna`,
  115 z niepełnym tekstem). Dotyczy to także obwieszczenia tekstu jednolitego bez numerowanych punktów.
- Obwieszczenie zbudowane z paragrafów albo artykułów (4 dokumenty) idzie dotychczasową drogą aktu.
- 20 obwieszczeń tekstu jednolitego, których aktu nie da się nazwać, nadal ma korzeń `ustawa`.
- `Announcement` przestaje znaczyć „obwieszczenie tekstu jednolitego": kod pytający o ogłaszany akt czyta
  `AnnouncesAct`, o tekst jednolity — `IsConsolidatedText`.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0043_AnnouncementThatAnnouncesNoAct_IsTheRoot_AndItsAnnexIsNotAnAct`
- `PlainAnnouncementTests` — układ dokumentu (bez załącznika, z załącznikiem, tekst jednolity, obwieszczenie
  z paragrafami), podstawa prawna, podpisy, załącznik, eksport i zgodność ze schematem.
