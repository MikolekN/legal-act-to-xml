# ADR-0029: Akapit bez oznaczenia i tabela są dalszą treścią jednostki

- Status: Accepted
- Data: 2026-10-10

## Kontekst

W treści przepisu stoją akapity, które nie są jednostkami redakcyjnymi: legenda wzoru („gdzie poszczególne
symbole oznaczają:"), treść oświadczenia albo ślubowania, zdanie w nowym wierszu, a także tabele. Potok
rozpoznaje jednostki po oznaczeniu, więc z takim akapitem nie miał co zrobić:

- akapit nierozpoznany był **pomijany** — zostawał po nim tylko komunikat walidacji (3945 akapitów
  w korpusie, w tym 11 w ustawach ogłoszonych);
- komórki tabeli przechodziły przez rozpoznawanie jak zwykłe akapity: „1." w kolumnie liczby porządkowej
  stawało się ustępem, „Art. 5" — artykułem (jedno rozporządzenie zyskiwało tak 186 artykułów),
  a reszta komórek ginęła.

Schemat przewiduje dla jednostki, za jej zdaniami (`p`), a przed jednostkami podrzędnymi, grupę
`kontynuacja-akapitu`: `fragment` („tekst, który musi być w XML-u, ale nie pasuje do żadnej nazwanej
jednostki redakcyjnej"), tabelę, grafikę, wzór.

## Decyzja

**Akapit bez oznaczenia i tabela stojące w treści przepisu należą do jednostki, za którą stoją — jako jej
dalsza treść (`BaseEntity.Continuation`), w kolejności z aktu.**

- Akapit, którego potok nie rozpoznał, trafia do najgłębszej otwartej jednostki (tiret, litera, punkt,
  ustęp) i wychodzi jako `fragment`.
- Bloki tabeli stojącej w treści przepisu omijają rozpoznawanie jednostek: są zbierane do końca tabeli
  i w całości stają się tabelą w dalszej treści tej jednostki (`tabela`).
- Tabela należy w całości tam, gdzie się zaczęła. Zaczęta w treści nowelizacji jest treścią cytowaną:
  tuż po komendzie („…dodaje się lp. 2b w brzmieniu:") jest całym cytatem i zamyka ją jej koniec — takie
  tabele nie mają cudzysłowów; w środku cytowanych jednostek należy do jednostki, za którą stoi.
- Wiersz strefy tytułowej innego aktu („Załącznik do obwieszczenia…", „USTAWA", „z dnia …") nie jest
  dalszą treścią ostatniego przepisu — zostaje pominięty jak dotąd.

## Odrzucone alternatywy

- **Pomijać z komunikatem walidacji** (stan dotychczasowy). Tekst aktu ginie bez śladu w wyjściu.
- **Jednostka bez oznaczenia** (`nie-ztp-*` z `brak-eid`). Tak buduje się dziś akapit, który ma styl
  jednostki; legenda wzoru nie jest jednak ustępem, a jednostka bez oznaczenia rozcina wyliczenie, w którym
  stoi. Schemat ma na to osobny element.
- **Dopisać do tekstu jednostki** (`ContentText`). Zmienia podział na zdania i ich numerację — a po
  zdaniach adresują nowelizacje („zdanie drugie") i wyjątki wejścia w życie.
- **Tabela jako ciąg akapitów** przez rozpoznawanie. Komórki wyglądają jak jednostki i nimi zostają.

## Konsekwencje

- Zmienia wyjście 159 dokumentów korpusu: 1156 akapitów wychodzi jako `fragment`, 22 tabele jako `tabela`;
  znikają jednostki zbudowane z komórek tabel. Zgodność ze schematem bez zmian.
- Dalsza treść stoi w eksporcie zawsze tuż za `p` jednostki — tak wymaga schemat. Akapit bez oznaczenia
  stojący za wyliczeniem trafia więc do ostatniej jednostki tego wyliczenia, nie za nie.
- Jednostka, której jedyną treścią jest wzór (blok bez tekstu), nie ma `p`, a `fragment` bez `p` jest
  niezgodny ze schematem (jeden dokument korpusu) — do czasu obsługi `wzor-mat`.
- Akapit **w stylu jednostki**, ale bez oznaczenia, nadal buduje się jako jednostka bez oznaczenia
  ([ADR-0024](0024-jednostka-bez-oznaczenia-nie-ma-eid.md)) — przeniesienie go do dalszej treści to osobny
  krok ([backlog](../backlog.md)).
- Tabela cytowana wprost wychodzi w jednym cytacie strukturalnym, także gdy komenda ma kilka celów
  („ust. 26 i 27 otrzymują brzmienie:") — wierszy nie da się rozdać między cele.
- Podpis w formie, której `ActSignatureParser` nie czyta („Marszałek Sejmu: wz. …"), staje się dalszą
  treścią ostatniego przepisu zamiast zniknąć.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0029_UndesignatedParagraphAndTable_ContinueTheirUnit`.
- `UnitContinuationTests` — najgłębsza jednostka, kolejność w eksporcie, strefa tytułowa innego aktu,
  tabela w przepisie, tabela po komendzie i w środku cytowanych jednostek, zgodność treści aktu ze schematem.
