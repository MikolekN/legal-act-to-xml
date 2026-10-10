# ADR-0023: Poziom pominięty w akcie to jednostka niejawna — bez segmentu w eId i bez elementu w eksporcie

- Status: Accepted
- Data: 2026-10-10

## Kontekst

Akty odbiegają od hierarchii ustęp → punkt → litera → tiret (§ 56 ZTP): litery stoją wprost
w ustępie, tirety wprost w ustępie albo w punkcie. W korpusie to 61 dokumentów, z czego 58 to teksty
jednolite. Model trzyma litery tylko w punkcie, a tirety tylko w literze, więc parser wstawiał
w brakujące miejsce punkt albo literę bez numeru i treści. Taka jednostka wychodziła do eksportu jak
każda inna: `nie-ztp-punkt` z `nr` o wartości `UNKNOWN` i bez `p`, a jednostki pod nią dostawały eId
z segmentem bez oznaczenia (`art_5__ust_1__pkt__lit_a`). Ani jedno, ani drugie nie przechodzi
schemaPL.xsd.

Schemat opisuje ten przypadek inaczej: grupa `od-punktu` pozwala postawić `nie-ztp-litera`
i `nie-ztp-tiret` wprost w `nie-ztp-ustep`, a `od-litery` — `nie-ztp-tiret` wprost w `nie-ztp-punkt`.
Powołania w aktach też pomijają brakujący poziom („art. 5 ust. 1 lit. a").

## Decyzja

**Jednostka wstawiona za pominięty poziom jest niejawna (`BaseEntity.IsImplicit`), tak jak jedyny
ustęp artykułu i rozdział aktu bez rozdziałów.** Istnieje tylko w modelu.

- **eId** pomija ją: litera w ustępie ma `art_5__ust_1__lit_a`, tiret w ustępie `art_5__ust_1__tir_1`.
  Adres jest tym samym napisem co powołanie tej jednostki w innym przepisie.
- **Eksport** nie tworzy dla niej elementu. Jej dzieci i część wspólna stają w najbliższej jednostce
  jawnej jako `nie-ztp-*`, a ta jednostka sama jest `nie-ztp-*`.
- **Wprowadzenie do wyliczenia** należy do jednostki jawnej (zdanie ustępu), więc pozycje wyliczenia
  są dalszymi fragmentami tego zdania.
- **Powołanie** bez pominiętego poziomu schodzi przez jednostkę niejawną
  (`EntryIntoForceTargetResolver`).

## Odrzucone alternatywy

- **Jednostka bez oznaczenia w eksporcie (stan sprzed decyzji).** Niezgodna ze schematem (`nr`,
  brak `p`, wzorzec `EIdTyp`) i z tym, jak przepisy powołują takie jednostki.
- **Listy niższych jednostek wprost w wyższej** (`Paragraph.Letters`, `Paragraph.Tirets`,
  `Point.Tirets`). Każdy odbiorca drzewa — obchód, buildery, daty wejścia w życie, rozstrzyganie
  powołań, konwertery — musiałby znać dwie drogi do litery i trzy do tiretu. Jednostka niejawna
  zostawia jedną drogę, a wyjątek zamyka w dwóch miejscach: składaniu eId i eksporcie.
- **Numer nadany przez parser** (punkt „1" dla liter bez punktu). Wymyśla oznaczenie, którego w akcie
  nie ma; powołanie „ust. 1 pkt 1 lit. a" nie istnieje w żadnym przepisie.

## Konsekwencje

- Zmienia wyjście: eId jednostek pod pominiętym poziomem i ich elementy (`nie-ztp-litera` wprost
  w `nie-ztp-ustep` zamiast w pustym `nie-ztp-punkt`).
- Jednostka niejawna i jednostka nad nią mają ten sam eId. Kod, który kluczuje jednostki po eId,
  musi pomijać niejawne.
- Jednostka niejawna nie ma treści. Tekst bez oznaczenia po literze wprost w ustępie trafia do tej
  litery albo do części wspólnej, nigdy do punktu niejawnego.
- Ustęp z jednostką niejawną i jawnymi punktami wychodzi w całości jako `nie-ztp-ustep`: schemat
  dopuszcza litery wprost tylko tam, a w `od-punktu` litery stoją przed punktami — tak jak w modelu,
  bo jednostka niejawna powstaje tylko przed pierwszą jawną.
- Treść nowelizacji nie korzysta z tej reguły: `AmendmentBuilder` stawia cytowaną literę bez punktu
  na najwyższym poziomie cytatu, także gdy cytat otworzył wcześniej ustęp. W korpusie taki cytat nie
  występuje.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0023_SkippedLevel_IsAnImplicitUnit_AbsentFromEIdAndExport`.
- `SkippedLevelTests` — model, eksport, fragmenty zdania, powołanie pomijające poziom, zgodność
  ze schematem.
