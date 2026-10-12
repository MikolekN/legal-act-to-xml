# ADR-0059: Odnośnik jako przedmiot komendy jest cytowanym odnośnikiem, nie jednostkami ani tekstem

- Status: Accepted
- Data: 2026-10-12

## Kontekst

Komendy „odnośnik nr 1 do tytułu ustawy otrzymuje brzmienie:" i „do tytułu ustawy dodaje się odnośnik nr 2
w brzmieniu:" cytują odnośnik (przypis) aktu. Treść cytowana zaczyna się jego znacznikiem — „1) Niniejsza ustawa …" —
czyli tak jak punkt. Rodzaj treści wynika więc z przedmiotu komendy, nie z samej treści: raz wychodziła jako tekst
cytowany bez adresu (`cytat-w-tekscie`, cel `UNKNOWN`), raz jako punkty aktu zmienianego (gdy odnośnik miał w środku
wyliczenie).

Schemat ma dla cytowanego odnośnika element `odnosnik-cyt` („składnia jak normalnego odnośnika, bez wydzielania
numeru"), a przykłady autorów schematu (DU/2026/252) stawiają go w cytacie strukturalnym pod adresem `odn_N`.

## Decyzja

- **Rozpoznanie po przedmiocie komendy** (`QuotedFootnoteReader`): „odnośnik nr N … otrzymuje brzmienie:"
  i „dodaje się odnośnik nr N … w brzmieniu:". Treść takiej komendy nie przechodzi przez rozpoznawanie jednostek
  (`AmendmentStateManager.Flush`): jest odnośnikiem — `AmendmentContent.Footnote` — a jej akapity, także pozycje
  wyliczenia, są akapitami odnośnika. Własny znacznik z początku treści jest zdejmowany, także złożony indeksem
  górnym razem z cudzysłowem otwierającym cytat („[„2)] …").
- **Adres:** `StructuralReference.Footnote` → `odn_N`. Celem zmiany brzmienia jest odnośnik zmieniany; celem
  dodania — odnośnik poprzedzający dodawany („nr 2" staje po „nr 1"), a gdy dodawany jest pierwszy — cel nieznany.
- **Eksport** (`AmendmentXmlConverter.AddQuotedFootnote`): `cytat-strukt` (`zastap` / `dodaj-po`) z `odnosnik-cyt`
  o adresie `odn_N`; znacznik stoi w pierwszym zdaniu indeksem górnym.

## Odrzucone alternatywy

- **Rozstrzygać po treści** („1) …" jako punkt albo tekst). Treść odnośnika i punkt wyglądają tak samo; odróżnia
  je tylko komenda.
- **Wyliczenie w odnośniku jako `punkt-odnosnik`.** Schemat to przewiduje, ale odnośniki samego aktu też wychodzą
  u nas jednym akapitem ze zdaniami (ADR-0026) — cytowany odnośnik ma tę samą postać co zwykły.
- **Odnośnik w załączniku pod tym samym adresem.** Jego adres leży pod załącznikiem; przykłady go nie pokazują,
  więc zostaje przy dotychczasowej postaci.

## Konsekwencje

- W korpusie 40 cytowanych odnośników w 28 plikach wychodzi jako `odnosnik-cyt`: 27 było tekstem cytowanym bez adresu, 13 — jednostkami (znika 17 błędnych punktów i liter aktu zmienianego). Zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory), pokrycie tekstu bez zmian. W przykładzie autorów schematu DU/2026/252 oba cytowane odnośniki są takie same jak nasze; komendy zgodne: 338 z 345, a pozostałe 7 wymaga wiedzy spoza aktu zmieniającego.
- Zostają przy dotychczasowej postaci: odnośnik w załączniku, odnośnik bez numeru („oznaczony gwiazdką", „dodaje się
  odnośnik w brzmieniu:"), kilka odnośników jedną komendą („odnośniki nr 1–3 otrzymują brzmienie:") i zmiana punktu
  w odnośniku („w odnośniku nr 1 pkt 2 otrzymuje brzmienie:").

## Weryfikacja

- `ArchitectureDecisionTests.Adr0059_FootnoteNamedByTheCommand_IsAQuotedFootnote`
- `QuotedFootnoteTests` — odczyt komendy, zdjęcie znacznika, model, eksport obu postaci, wyliczenie w odnośniku,
  zgodność ze schematem.
