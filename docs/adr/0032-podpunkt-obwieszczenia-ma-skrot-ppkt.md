# ADR-0032: Jednostki obwieszczenia tekstu jednolitego adresuje się od obwieszczenia: punkt `pkt`, podpunkt `ppkt`

- Status: Accepted
- Data: 2026-10-11

## Kontekst

Obwieszczenie w sprawie ogłoszenia tekstu jednolitego ma własne jednostki (§ 102–104 ZTP): punkty
(„1. Na podstawie …", „2. Podany w załączniku … tekst jednolity nie obejmuje:") i podpunkty („1) ustawą …",
„1) art. 57 ustawy …, który stanowi:"), a po podpunktach bywa część wspólna („oraz zmian wynikających
z przepisów ogłoszonych przed dniem …"). Od [ADR-0031](0031-tekst-jednolity-pod-korzeniem-obwieszczenie.md)
treść ta nie należy do ogłaszanego aktu, więc w eksporcie potrzebuje własnych elementów i własnych adresów.

Schemat ma dla nich `punkt-OTJ`, `podpunkt-OTJ` i `czesc-wspolna-podpunkt-OTJ`. Zamknięta tabela skrótów
z [ADR-0014](0014-skroty-jednostek-w-eid.md) nie zna podpunktu obwieszczenia. Przykład autorów schematu
(`DU20261261_obw_tj.xml`) adresuje te jednostki `DU/2026/1261__pkt_1` i `DU/2026/1261__pkt_1__ppkt_2`.

Obwieszczenie zawiera też przytoczone przepisy innych aktów („„Art. 57. …""), złożone w szczotce stylami
jednostek aktu.

## Decyzja

**Punkt obwieszczenia ma w eId skrót `pkt`, podpunkt — `ppkt` (rozszerzenie tabeli ADR-0014), a adres liczy się
od obwieszczenia, bez segmentu ogłaszanego aktu: `pkt_1__ppkt_2`, część wspólna `pkt_1__czw`.**

- Jednostki buduje `AnnouncementUnits` z samej treści, poza potokiem aktu: wiersz „N." to punkt, „N)" pod
  punktem to podpunkt, akapit tuż za ostatnim podpunktem bez znaku końcowego to część wspólna podpunktów
  (ta sama reguła co w treści aktu — `CommonPartPlacement.LeavesEnumerationOpen`).
- Przytoczenie — od bloku otwartego cudzysłowem do jego domknięcia — oraz tabela są dalszą treścią jednostki,
  za którą stoją (`BaseEntity.Continuation`, w eksporcie `fragment` i `tabela`, [ADR-0029](0029-akapit-bez-oznaczenia-jest-dalsza-trescia-jednostki.md)).
- Formuła podpisu pod obwieszczeniem jest podpisem dokumentu (`LegalDocument.Signature`).
- Jednostki obwieszczenia nie niosą dat wejścia w życie — nie są przepisami ogłaszanego aktu.

## Odrzucone alternatywy

- **Potok aktu pod artykułem zastępczym**, jak wyliczenie w załączniku (ADR-0028). Przytoczony „Art. 57." w stylu
  artykułu stałby się artykułem, a „1)" w środku przytoczenia — podpunktem obwieszczenia; trzeba by wyłączać
  po kolei rozpoznawanie artykułów, nowelizacji i klauzuli wejścia w życie. Obwieszczenie ma trzy rodzaje
  jednostek i jedną regułę przytoczenia — osobny, krótki budowniczy jest prostszy niż potok z wyjątkami.
- **Adres pod ogłaszanym aktem albo skróty `ust`/`pkt`.** Punkt obwieszczenia nie jest ustępem ustawy, a adres
  `ust_1__pkt_2` powtarzałby adresy jednostek ogłaszanego aktu pod tym samym ELI.
- **Przytoczenia od razu jako jednostki** (`tresc-strukt` z artykułami adresowanymi w akcie przytaczanym).
  To docelowy kształt z przykładu, ale wymaga odczytu aktu przytaczanego z treści podpunktu; do tego czasu
  przytoczenie jest dalszą treścią i żaden jego akapit nie ginie.

## Konsekwencje

- Tabela skrótów ADR-0014 ma jedną pozycję więcej; strażnik tabeli (`Adr0014_UnitEIdPrefixes_FollowTheLevelTypeTable`)
  i tabele w `reference-schema.md` znają `ppkt`.
- Punkt obwieszczenia jest w modelu ustępem (`Paragraph`), a podpunkt — punktem (`Point`) z innym
  `EIdPrefix`; korzystają z tego samego podziału na zdania, odnośników i dalszej treści co jednostki aktu.
  Konwerter (`AnnouncementXmlConverter`) nadaje im nazwy elementów obwieszczenia.
- Litery pod podpunktem („a) …") nie są jeszcze jednostkami — zostają dalszą treścią podpunktu.
- Obwieszczenie ogłaszające akt inny niż ustawa ma jednostki w modelu, ale eksport ich nie niesie, dopóki
  korzeniem nie jest `obwieszczenie` (ADR-0031).

## Weryfikacja

- `ArchitectureDecisionTests.Adr0014_UnitEIdPrefixes_FollowTheLevelTypeTable` — `ppkt` w tabeli skrótów.
- `ArchitectureDecisionTests.Adr0032_AnnouncementUnits_AreAddressedFromTheAnnouncement`.
- `AnnouncementTests` — punkty, podpunkty i część wspólna, przytoczenie jedno- i wieloakapitowe, podpis,
  eksport z adresami, brak dat, zgodność ze schematem.
