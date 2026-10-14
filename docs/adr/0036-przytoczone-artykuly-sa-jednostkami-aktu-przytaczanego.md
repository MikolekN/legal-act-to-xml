# ADR-0036: Artykuły przytoczone w obwieszczeniu są jednostkami w `tresc-strukt`, adresowanymi w akcie, z którego pochodzą; przytoczenie, którego nie da się tak zbudować w całości, zostaje tekstem

- Status: Accepted (zastępuje rozstrzygnięcie [ADR-0032](0032-podpunkt-obwieszczenia-ma-skrot-ppkt.md) o przytoczeniu jako dalszej treści) — Superseded in part by [ADR-0084](0084-przytoczony-przepis-zmieniajacy-jest-jednostka-z-wlasnymi-nowelizacjami.md) (przytoczony przepis zmieniający z własnym cytatem)
- Data: 2026-10-11

## Kontekst

Obwieszczenie tekstu jednolitego przytacza przepisy, których tekst jednolity nie obejmuje (§ 104 ZTP):
„2) art. 19 ustawy z dnia 21 listopada 2025 r. … (Dz. U. poz. 1792), który stanowi: „Art. 19. Ustawa wchodzi
w życie …"". [ADR-0032](0032-podpunkt-obwieszczenia-ma-skrot-ppkt.md) zostawił przytoczenie dalszą treścią
jednostki (`fragment`) i wprost odłożył kształt docelowy: „wymaga odczytu aktu przytaczanego z treści podpunktu".

W korpusie to 1717 przytoczeń w 686 obwieszczeniach (ok. 10 tys. akapitów): 1265 zaczyna się od „Art.",
389 od „§" (teksty jednolite aktów wykonawczych), 15 od ustępu albo punktu, 48 to tekst bez jednostki.
Przykład autorów schematu (`DU20261261_obw_tj.xml`) niesie przytoczenie w zdaniu podpunktu jako
`tresc-strukt cudzyslowy="oba"` z jednostkami adresowanymi w akcie przytaczanym (`DU/2025/1792__art_19__pkt_1`).

Do rozstrzygnięcia: czym przytoczenie jest w modelu, skąd adres jego jednostek i co robić, gdy z akapitów
nie da się złożyć jednostek.

## Decyzja

**Przytoczenie złożone z całych artykułów (w akcie opartym na paragrafach — paragrafów) jest w modelu
`Quotation`: jednostki plus akt, z którego pochodzą. Eksport pisze je jako `tresc-strukt` w ostatnim zdaniu
jednostki obwieszczenia, z prefiksem aktu przytaczanego. Każde inne przytoczenie zostaje akapitami tekstu.**

- `Quotation` (jednostki w `AmendmentContent`, `SourceAct`, interpunkcja za cudzysłowem) jest trzecim rodzajem
  `ContentBlock` w dalszej treści jednostki — obok akapitu i tabeli, w kolejności z dokumentu.
- Jednostki buduje `AmendmentBuilder` z samej treści — tymi samymi regułami co treść cytowaną w nowelizacji
  bez stylów; o znaczeniu „§" rozstrzyga rodzaj ogłaszanego aktu ([ADR-0034](0034-paragraf-jest-jednostka-podstawowa-aktu-wykonawczego.md)).
- Wszystko albo nic (`AnnouncementUnits.BuildQuotation`): przytoczenie staje się jednostkami tylko wtedy, gdy
  ma oba cudzysłowy, składa się wyłącznie z artykułów, żaden akapit nie został poza jednostkami i żaden
  akapit poza pierwszym nie otwiera własnego cudzysłowu. W przeciwnym razie zostają akapity tekstu — jak dotąd.
- Akt przytaczany to **jedyny** publikator wymieniony w treści jednostki obwieszczenia
  (`JournalReferenceService.ReadSingleAct`). Gdy nie ma żadnego albo jest więcej niż jeden, jednostki
  zostają bez prefiksu — tak jak cytat, którego aktu zmienianego nie odczytano ([ADR-0027](0027-prefiks-aktu-dopisuje-eksport.md)).
- Prefiks dopisuje `ActPrefixXml`; akt niesie adnotacja elementu (`ActPrefixXml.QuotedAct`), bo schemat nie
  daje `tresc-strukt` atrybutu aktu. Jednostki przytoczone nie niosą dat.

## Odrzucone alternatywy

- **Przytoczenie jako nowelizacja** (`Amendment` → `cytat-strukt`). Maszyneria jest gotowa, ale przytoczenie
  nie jest instrukcją: nie ma celu, funkcji ani aktu zmienianego, a `cytat-strukt` wymaga `now:akt`,
  `element-eId` i `funkcja`. Schemat ma na to osobny element bez tych atrybutów.
- **Budowanie częściowe** — jednostki z tego, co się rozpoznało, reszta jako `fragment` obok. Akapit spoza
  jednostek stałby za całym `tresc-strukt`, czyli w innym miejscu niż w dokumencie, a błędnie rozpoznany akapit
  zmieniałby budowę przytaczanego artykułu. Reguła „wszystko albo nic" trzyma gwarancję z ADR-0032: żaden
  akapit przytoczenia nie ginie i nie zmienia kolejności (pokrycie tekstu bez zmian).
- **Przytoczony ustęp albo punkt jako jednostka** („„3. W przypadku …""). Bez artykułu adres brzmiałby
  `DU/2025/1792__ust_3` — takiej jednostki w akcie przytaczanym nie ma. Adres stoi w treści podpunktu
  („art. 5 ust. 3 ustawy …") i wymaga osobnego odczytu; do tego czasu takie przytoczenie zostaje tekstem.
- **Przytoczony przepis zmieniający z własnym cytatem jako jednostki.** Budowniczy treści cytowanej jest
  płaski: cytowany w przepisie „„2. Rada gminy …"" stawałby się ustępem przytaczanego artykułu (w pierwszym
  przebiegu 37 jednostek `nie-ztp-*`). To nowelizacja zagnieżdżona — obszar komend nowelizacyjnych.
- **Pierwszy publikator, gdy treść wymienia kilka aktów.** Część przytoczeń dostałaby adres w cudzym akcie;
  brak prefiksu jest widoczny, zły prefiks — nie.

## Konsekwencje

- W korpusie 1625 z 1717 przytoczeń to jednostki (684 obwieszczenia): 1772 artykuły, 567 paragrafów,
  1403 ustępy, 2405 punktów, 229 liter; 6068 akapitów przestało być `fragment`. Z prefiksem aktu
  przytaczanego 1570; bez prefiksu 55 — głównie akty z Monitora Polskiego (`JournalReferenceService` czyta
  tylko „Dz. U."). Zgodność ze schematem i pokrycie tekstu bez zmian.
- Zostaje tekstem 4062 akapitów w 240 obwieszczeniach, w większości w kilkunastu dokumentach przytaczających
  całe przepisy zmieniające (nowelizacje zagnieżdżone).
- `AmendmentContent` jest od teraz także kształtem przytoczenia, nie tylko treści nowelizacji; zmiana jego
  budowy dotyka obu.
- Oznaczenie przytoczonego artykułu stoi w `nr`, więc indeks górny („Art. 59¹.") nie jest już elementem `sup`
  w tekście, tylko częścią oznaczenia ([ADR-0019](0019-indeks-gorny-w-eid.md)).
- Punkt o przytoczeniu w ADR-0032 („dalsza treść jednostki, w eksporcie `fragment`") obowiązuje już tylko
  dla przytoczeń, których nie da się zbudować; reszta ADR-0032 bez zmian.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0036_QuotedArticles_AreUnitsOfTheQuotedAct_OtherQuotationsStayText`
- `ArchitectureDecisionTests.Adr0032_AnnouncementUnits_AreAddressedFromTheAnnouncement` — asercja o przytoczeniu
  zmieniona świadomie: jeden blok `Quotation` zamiast trzech akapitów tekstu.
- `AnnouncementTests` — przytoczenie jedno- i wieloartykułowe, przytoczenia zostające tekstem (ustęp, akapit
  poza jednostkami, przepis zmieniający z cytatem), dwa akty w treści punktu, eksport w zdaniu z interpunkcją,
  zgodność ze schematem; `RegulationTests.SectionQuotedInTheAnnouncement_IsTheBasicUnitOfTheQuotedRegulation`.
