# ADR-0087: Oznaczenie bez „§", które ciągnie numerację paragrafów kodeksu, to paragraf kodeksu spoza ZTP

- Status: Accepted
- Data: 2026-10-15

## Kontekst

W tekście jednolitym kodeksu (korpus: 307517_2786682) art. 88 wydrukowano jako „§ 1. Pełnomocnikiem może być …"
i zaraz „2. Na zasadach określonych w odrębnej ustawie …" — bez „§". Parser czytał „2." jako ustęp, a artykuł
z paragrafem kodeksu jest w eksporcie `artykul-kodeksu`, w którym schemat ustępu nie dopuszcza. To był jedyny
dokument korpusu niezgodny ze schematem z powodu treści (pozostałe 41 to szczotki bez pozycji w dzienniku).

## Decyzja

**Ustęp z oznaczeniem bez „§", którego numer ciągnie numerację sąsiedniego paragrafu kodeksu („§ 1." i „2."; „1."
i „§ 2."), jest paragrafem kodeksu zapisanym niezgodnie z ZTP (`ParsingFactories.AlignWithCodeParagraphs`, wołane
przy budowie artykułu i treści cytowanej): rola i prefiks eId paragrafu kodeksu (`art_88__par_2`), oznaczenie
takie, jak wydrukowane („2"), w eksporcie `nie-ztp-paragraf-kodeksu`, a na jednostce ostrzeżenie walidacji.**

- Adres zostaje, bo jednostka jest oznaczona — zamknięta lista powodów braku eId (`PowodBrakuEIdTyp`) nie ma
  powodu „oznaczenie innej rodziny"; prefiks `par` idzie za elementem ([ADR-0014](0014-skroty-jednostek-w-eid.md)).
- „N." za paragrafem, które numeracji paragrafów nie ciągnie („§ 6. 1. Proch …", „2. Broń …", „3. …"), to ustępy
  tego paragrafu i zostają ustępami.

## Odrzucone alternatywy

- **Ustęp, jak dotąd.** Eksport niezgodny ze schematem, choć schemat przewiduje dla błędu prawodawcy jednostki
  `nie-ztp-*`.
- **Odczyt „2." jako „§ 2" (paragraf zgodny z ZTP).** Poprawiałby treść aktu; odczyt tolerancyjny ma w schemacie
  zamkniętą listę powodów (`PowodOdczytuTolerancyjnegoTyp`) bez takiego przypadku, więc poprawki nie dałoby się
  oznaczyć.
- **Każdy ustęp w artykule z paragrafami kodeksu.** Pierwsza wersja tej reguły przestawiała też ustępy wewnątrz
  paragrafu („§ 6. 1. …", „2. …") w załączniku rozporządzenia (338565_3124999): ich adresy zderzały się z § 2–4
  i całe wyliczenie załącznika wracało do tekstu ([ADR-0028](0028-wyliczenie-w-zalaczniku-buduje-potok-aktu.md)). Sygnałem literówki jest ciągłość
  numeracji, nie samo sąsiedztwo.

## Konsekwencje

- W korpusie zmienia się 1 dokument (307517_2786682): „2." w art. 88 wychodzi jako `nie-ztp-paragraf-kodeksu` (`art_88__par_2`). Zgodnych ze schematem 2280 z 2321 (oba walidatory; było 2279) — niezgodnych zostaje 41 szczotek bez pozycji w dzienniku; treść aktu zgodna we wszystkich 2321 dokumentach; pokrycie tekstu i zgodność bez stylów bez zmian.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0087_UnsignedDesignationInACodeArticle_IsANonZtpCodeParagraph_WithItsPrintedNumber`
- `CodeParagraphTests.ParagraphPrintedWithoutTheSign_InACodeArticle_IsANonZtpCodeParagraph`,
  `CodeParagraphTests.ParagraphPrintedWithoutTheSign_BeforeTheCodeParagraph_IsAlignedToo`,
  `CodeParagraphTests.ParagraphsNumberedInsideASection_StayParagraphsOfIt`, zgodność ze schematem.
