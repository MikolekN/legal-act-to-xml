# ADR-0096: Adnotacja uchylająca samo oznaczenie jednostki systematyzacyjnej nie obejmuje przepisów za nią

- Status: Accepted (Supersedes ADR-0066 w części: jednostki stojące za adnotacją, która uchyla samo oznaczenie)
- Data: 2026-10-16

## Kontekst

[ADR-0066](0066-adnotacja-nazywajaca-uchylona-jednostke-jest-jej-naglowkiem.md) czyni adnotację tekstu jednolitego,
która nazywa uchyloną jednostkę systematyzacyjną, nagłówkiem tej jednostki, a jednostki stojące dalej stawia pod nią
— „artykuły pod rozdział". To pasuje do adnotacji o uchyleniu całej jednostki („(Rozdział 2 – uchylony)"): jej
przepisów już nie ma, a za adnotacją stoi zwykle następna jednostka.

Adnotacja „(oznaczenie rozdziału 1 i tytuł – uchylone)" mówi co innego: uchylono samo oznaczenie i tytuł, a przepisy
obowiązują dalej — tekst jednolity drukuje je za adnotacją. Postawione pod jednostką, której już nie ma, wyglądały
na jej treść: w ocenie korpusu DOCX z 2026-10-16 16 takich adnotacji w 5 dokumentach obejmowało 128 obowiązujących
artykułów (320891, 348341, 335241 — „(Rozdział 3b – oznaczenie i tytuł rozdziału – uchylone)", 302049 — księga
pierwsza części drugiej Kodeksu postępowania cywilnego).

## Decyzja

**Adnotacja, która uchyla samo oznaczenie (i tytuł) jednostki — mówi o „oznaczeniu" — zostaje nagłówkiem tej
jednostki w układzie aktu (ADR-0066), ale jednostki i przepisy stojące za nią trafiają do jednostki niejawnej tego
samego poziomu (`SystematizingUnitBuilder.LeaveRepealedDesignation`).**

- Jednostka niejawna wychodzi jak każda: `rozdzial-n` pod jawnym działem, a gdy wszyscy przodkowie są niejawni —
  artykuły wprost w akcie, obok jawnych rozdziałów
  ([ADR-0020](0020-eksport-jednostek-systematyzacyjnych.md)).
- Następna jawna jednostka tego poziomu przejmuje jednostkę niejawną, gdy ta nie ma artykułów — adnotacja stojąca
  tuż przed nią nie zostawia pustego węzła.
- Adnotacja o uchyleniu całej jednostki działa jak dotąd.

## Odrzucone alternatywy

- **Przepisy pod jednostką uchyloną** (ADR-0066). Obowiązujący artykuł wyglądał na treść jednostki, której oznaczenie
  i tytuł uchylono.
- **Przepisy dołączone do poprzedniej jednostki tego poziomu.** Nie były jej częścią w akcie ani nie stały się nią
  przez uchylenie oznaczenia.
- **Jednostka uchylona pomijana w układzie.** Ginie adnotacja i miejsce, w którym tekst jednolity ją drukuje.

## Konsekwencje

- eId artykułów nie zmienia się — nie niesie ścieżki systematyzacyjnej
  ([ADR-0017](0017-eid-jednostki-redakcyjnej-bez-sciezki-systematyzacyjnej.md)).
- Jednostka z uchylonym oznaczeniem wychodzi z samym nagłówkiem (adnotacją).

## Weryfikacja

`ArchitectureDecisionTests.Adr0096_ProvisionsAfterARepealedDesignation_StandOutsideTheUnit`,
`SystematizingUnitKnownGapsTests.ProvisionsAfterARepealAnnotation_StandInTheUnit_OnlyWhenTheWholeUnitIsRepealed`,
`SystematizingUnitKnownGapsTests.AnnotationRepealingABooksDesignation_StandsInThePart_AndItsTitlesOutsideTheBook`.
