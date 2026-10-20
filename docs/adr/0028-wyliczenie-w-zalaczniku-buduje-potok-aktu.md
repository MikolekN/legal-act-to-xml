# ADR-0028: Wyliczenie w załączniku buduje potok aktu, pod artykułem zastępczym

- Status: Accepted — Superseded in part by [ADR-0099](0099-jedyny-zalacznik-bez-numeru-ma-adres-zal-1.md) (załącznik bez numeru)
- Data: 2026-10-10

## Kontekst

Treść załącznika bywa wyliczeniem zbudowanym jak treść aktu: „I. System… obejmuje: 1) … a) …". Przykład
autorów schematu (DU/2026/252, załącznik nr 4) zapisuje je jednostkami redakcyjnymi adresowanymi od
załącznika: `nie-ztp-ustep` z `eId="…zal_4__ust_I"`, pod nim `punkt` `…zal_4__ust_I__pkt_1` i `litera`.

Załącznik nie przechodzi przez potok aktu: jego bloki są zbierane za nagłówkiem i składane osobno
(`AnnexBuilder`), bo zwykle to tabele, wzory i tekst bez budowy jednostek. Potok buduje jednostki pod
artykułem — w załączniku artykułu nie ma. Akapitu, którego nie rozpozna, potok nie wbudowuje wcale
(zostaje po nim komunikat walidacji), a w załączniku takich akapitów jest dużo.

Oznaczenie liczbą rzymską („I.") jest spoza ZTP i w treści aktu nie jest rozpoznawane.

## Decyzja

**Załącznik, którego cała treść to akapity wyliczenia, jest budowany tym samym potokiem co treść aktu —
pod artykułem zastępczym, niejawnym, zakotwiczonym w adresie załącznika (`AnnexUnits`).**

- Artykuł zastępczy jest niejawny (`IsImplicit`), więc nie wnosi segmentu do eId, a jego `AddressAnchor`
  to adres załącznika — stąd `zal_4__ust_I__pkt_1` bez osobnej reguły adresowania.
- Wszystko albo nic: jeśli choć jeden akapit nie jest jednostką, w treści jest tabela albo potok coś
  pominął, załącznik zostaje akapitami i tabelami, jak przed tą decyzją. Tak samo, gdy dwa adresy
  w załączniku by się powtórzyły albo treść weszła w tryb nowelizacji.
- Oznaczenie rzymskie rozpoznaje wyłącznie `AnnexUnits` i tylko jako ustęp; w eksporcie taki ustęp to
  `nie-ztp-ustep`, a wyliczenie pod nim zostaje zwykłe.
- Załącznik bez numeru nie ma adresu ([ADR-0024](0024-jednostka-bez-oznaczenia-nie-ma-eid.md)), więc jego
  treść zostaje akapitami: `pkt_1` bez adresu załącznika wyglądałby jak punkt samego aktu.
- Jednostki załącznika dostają daty reguły ogólnej aktu.

## Odrzucone alternatywy

- **Osobny parser wyliczeń dla załączników.** Druga implementacja rozpoznawania punktów, liter, tiretów
  i części wspólnych, która rozjechałaby się z pierwszą przy każdej zmianie wzorców.
- **Załącznik w głównym biegu potoku** (bez zbierania bloków). Treść załącznika trafiała wtedy do
  ostatniego artykułu aktu, a akapity nierozpoznane ginęły.
- **Jednostki dla każdego ciągu akapitów wyliczenia, reszta akapitami.** Dwa wyliczenia w jednym
  załączniku dają powtórzone adresy (`zal_1__lit_a` dwa razy), a granica między wyliczeniem a tekstem
  wymaga zgadywania, do której jednostki należy akapit bez oznaczenia.
- **Oznaczenie rzymskie także w treści aktu.** „I.", „C.", „D.", „M." to tam inicjały („D. Tusk");
  w korpusie żadna jednostka aktu nie ma oznaczenia rzymskiego.

## Konsekwencje

- Zmienia wyjście dla załączników będących wyliczeniem: 9 dokumentów korpusu, 252 jednostki. Przykład
  DU/2026/252 zgadza się we wszystkich jednostkach (2280 z 2280).
- **Pełna walidacja takich dokumentów zgłasza „The field 'GUID' is expecting at the most one value"** —
  raz na każdą jednostkę w załączniku. `segment-zalacznika` jest w schemacie dowolną treścią
  (`xs:any`, `lax`), więc jednostki pod nim nie mają typu, a ograniczenie `AktGUID` obejmuje każdy
  element. Przykład autorów schematu daje te same 45 komunikatów co nasz eksport tego aktu. Liczba
  dokumentów zgodnych w całości spada przez to o 9 (ustawy 239 z 241, teksty jednolite 402 z 460);
  treść aktu sprawdzana bez nagłówka pozostaje zgodna. Do zgłoszenia autorom schematu — wpis w
  [backlogu](../backlog.md).
- Przykład nie nadaje jednostkom załącznika atrybutu `okres`, choć niosą `wejscie-w-zycie`. Eksport
  nadaje oba, jak każdej jednostce z datą ([ADR-0022](0022-daty-aktu-jako-zdarzenia-cyklu-zycia.md)).
- Wyjątek klauzuli wejścia w życie wskazujący załącznik („załącznik nr 2 wchodzi w życie…") nie jest
  jeszcze rozpoznawany — jednostki załącznika mają zawsze datę reguły ogólnej.
- Artykuł zastępczy zostaje rodzicem jednostek (niesie adres), ale nie jest ich artykułem
  (`Paragraph.Article` jest puste) — inaczej jedyny ustęp załącznika uchodziłby za niejawny.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0028_AnnexEnumeration_IsBuiltByTheActPipeline_OrLeftAsParagraphs`.
- `AnnexTests` — adresy od załącznika, treść mieszana, załącznik bez numeru, eksport z datą, zgodność
  treści aktu ze schematem.
- `CorpusValidator --compare-reference`: jednostki przykładu DU/2026/252 zgodne 2280 z 2280; sekcja
  „Załączniki".
