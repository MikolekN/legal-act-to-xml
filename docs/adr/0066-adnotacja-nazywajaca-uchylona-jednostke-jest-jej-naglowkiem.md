# ADR-0066: Adnotacja tekstu jednolitego nazywająca uchyloną jednostkę systematyzacyjną jest nagłówkiem tej jednostki

- Status: Accepted (adnotacja, która jednostki nie nazywa, zostaje w nagłówku jednostki, pod którą stoi — [ADR-0067](0067-wiersz-pod-tytulem-jednostki-jest-dalsza-trescia-naglowka.md)); Superseded in part by [ADR-0096](0096-adnotacja-uchylajaca-samo-oznaczenie-nie-obejmuje-przepisow-za-nia.md) — jednostki za adnotacją, która uchyla samo oznaczenie, stoją poza uchyloną jednostką
- Data: 2026-10-13

## Kontekst

Tekst jednolity zostawia w miejscu uchylonej jednostki systematyzacyjnej adnotację (§ 106 ZTP). Bywa ona drugim
wierszem po oznaczeniu („Rozdział 1" / „(uchylony)") i wtedy jest tytułem jednostki. Bywa też jedynym wierszem —
sama nazywa jednostkę: „(Rozdział 2 – uchylony)", „(oznaczenie oraz tytuł księgi pierwszej – uchylone)",
„(oznaczenie i tytuł oddziału 1 – uchylone)".

Taka adnotacja nie miała do czego przylgnąć. Stojąc za nagłówkiem jednostki nadrzędnej, przed pierwszym artykułem,
ginęła z eksportu; stojąc za artykułem, stawała się jego dalszą treścią (`fragment`). W obu przypadkach z układu
aktu znikała jednostka — po rozdziale 1 szedł rozdział 3, a tytuły księgi stały wprost pod częścią.

## Decyzja

**Adnotacja, która nazywa uchyloną jednostkę systematyzacyjną, wprowadza tę jednostkę do układu aktu i jest jej
nagłówkiem** (`StructureProcessor.TryEnterRepealedUnit`).

- Rozpoznanie: akapit w nawiasie kończący się słowem „uchylony" (ten sam wzorzec co adnotacja-tytuł), w którym
  stoi nazwa jednostki z oznaczeniem — w mianowniku albo dopełniaczu, liczbą albo liczebnikiem — a przed nią
  najwyżej „oznaczenie (oraz) tytuł". Nazwę i oznaczenie czyta wspólny słownik powołań
  ([ADR-0015](0015-jedno-zrodlo-powolan-jednostek.md)).
- Rozstrzyga treść, nie styl: adnotacja bywa złożona stylem oznaczenia rozdziału albo bez stylu.
- Adnotacja o zawartości jednostki („(zawierający rozdziały 4 i 5 – uchylony)") jednostki nie nazywa i zostaje
  tytułem jednostki, pod której oznaczeniem stoi.
- Jednostki stojące dalej wchodzą pod nią tak, jak weszłyby pod zwykły nagłówek — tytuły pod księgę, artykuły
  pod rozdział.

## Odrzucone alternatywy

- **Dalsza treść poprzedniego artykułu** (stan dotychczasowy za artykułem). Adnotacja nie mówi nic o tym
  artykule; a przed pierwszym artykułem jednostki nie było nawet do czego jej dołączyć.
- **Osobny element adnotacji bez jednostki.** Układ aktu traci poziom, który w akcie był i do którego odwołują
  się inne akty („w księdze pierwszej części drugiej").
- **Tylko styl uchylenia jako sygnał.** Trzy z dwudziestu adnotacji korpusu mają inny styl albo żaden.

## Konsekwencje

- W korpusie zmienia się 7 tekstów jednolitych: 20 adnotacji staje się nagłówkami jednostek (16 rozdziałów, 3 oddziały, 1 księga) — 9 z nich ginęło z eksportu, 11 wychodziło jako `fragment` artykułu, za którym stały. Zgubionych akapitów w tekstach jednolitych zostaje 9 (było 18). Zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory).
- Artykuły stojące za adnotacją trafiają pod jednostkę uchyloną — tak samo jak dotąd pod jednostkę z tytułem
  „(uchylony)". eId artykułów ścieżki systematyzacyjnej nie niesie ([ADR-0017](0017-eid-jednostki-redakcyjnej-bez-sciezki-systematyzacyjnej.md)).
- Jednostka uchylana dopiero w przyszłości („Rozdział 2a[11)]" i dalej „(Rozdział 2a – uchylony)[12)]") wychodzi
  jako dwie jednostki o tym samym eId bez rozróżnienia okresem — wersje jednostek systematyzacyjnych są w backlogu.
- „(Przepis wstępny – uchylone)" nie nazywa jednostki z oznaczeniem i nadal ginie (1 akapit).

## Weryfikacja

- `ArchitectureDecisionTests.Adr0066_AnnotationNamingARepealedUnit_IsTheHeadingOfThatUnit`
- `SystematizingUnitKnownGapsTests` — rozdział, księga między częścią a tytułami, odmiany i szyk adnotacji,
  adnotacje, które jednostki nie nazywają, adnotacja zaraz za oznaczeniem tej samej jednostki.
