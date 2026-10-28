# ADR-0057: Zdanie przytoczone w całości wychodzi jako `zdanie`; wprowadzenie do wyliczenia zostaje fragmentem tekstu

- Status: Superseded by [ADR-0124](0124-zdanie-i-wprowadzenie-do-wyliczenia-przytoczone-w-calosci-sa-zdaniem.md)
- Data: 2026-10-12

## Kontekst

Treść cytowana bez jednostek („zdanie pierwsze otrzymuje brzmienie: „…"", „tytuł rozdziału otrzymuje brzmienie:",
„wprowadzenie do wyliczenia otrzymuje brzmienie:") wychodziła zawsze jako `cytat-w-tekscie` z elementem `tekst`.
Schemat dopuszcza w nim także `zdanie`, a przykłady autorów schematu używają go dla przytoczonego zdania
(DU/2026/26). Dla wprowadzenia do wyliczenia przykłady nie są zgodne: DU/2026/26 ma `zdanie` (raz), DU/2026/252 —
`tekst` (pięć razy).

## Decyzja

- **Gdy przedmiotem komendy jest zdanie** („zdanie pierwsze otrzymuje brzmienie:", „dodaje się zdanie drugie
  w brzmieniu:", „zdania pierwsze i drugie otrzymują brzmienie:"), treść cytowana to zdania:
  `AmendmentContent.PlainTextIsSentences`, a w eksporcie `zdanie` z numerem — po jednym na każde zdanie tekstu
  (`AmendmentFinalizer.SentenceObjectPattern`, `AmendmentXmlConverter.AddTextCitation`).
- **Wprowadzenie do wyliczenia, tytuł i inne fragmenty zostają elementem `tekst`.** Wprowadzenie kończy dwukropek,
  a domykają dopiero pozycje wyliczenia — zdaniem samo nie jest.
- Miejscownik („w zdaniu pierwszym wyrazy …") wskazuje miejsce, nie przedmiot komendy.

## Odrzucone alternatywy

- **`zdanie` także dla wprowadzenia do wyliczenia** (jak w DU/2026/26). Przykłady mają tę postać raz na sześć,
  a wprowadzenie nie jest pełnym zdaniem; przy zmianie zdania odbiorca zastępuje zdanie, przy zmianie wprowadzenia —
  fragment przed wyliczeniem.
- **Rozstrzygać po budowie tekstu** (wielka litera, kropka na końcu). Tytuł i wyrazy bywają zbudowane jak zdanie;
  o tym, czym jest treść, mówi przedmiot komendy.

## Konsekwencje

- W korpusie 115 cytatów tekstowych w 52 plikach niesie teraz `zdanie` zamiast `tekst` (116 zdań); poza tym eksport się nie zmienia, zgodnych ze schematem jest nadal 1937 z 2321. Przykłady autorów schematu: DU/2026/26 — komendy 103 ze 105, treść zdań 466 z 469; DU/2026/252 — komendy 333 z 345.
- Jedno wprowadzenie do wyliczenia w DU/2026/26 różni się od przykładu rodzajem treści (`tekst` wobec `zdanie`).

## Weryfikacja

- `ArchitectureDecisionTests.Adr0057_QuotedSentenceIsASentence_AnIntroductionIsNot`
- `AmendmentTargetsExportTests` — zdanie zmieniane i dodawane, kilka zdań, wprowadzenie do wyliczenia, tytuł,
  miejscownik.
