# ADR-0131: Punkty, litery i tirety jednego cytatu to fragmenty jednego zdania, numerowane od 1

- Status: Accepted
- Data: 2026-10-28

## Kontekst

Pozycje wyliczenia są fragmentami jednego zdania: w treści aktu punkty ustępu ciągną łańcuch fragmentów jego
wprowadzenia (`SentenceContext`). W `cytat-strukt` każdą przytoczoną jednostkę eksport budował osobno, więc
„po pkt 4 dodaje się pkt 4a–4l w brzmieniu: …” (DU/2026/252 art. 1) dawało dwanaście zdań, każde z własnym
`zdGUID` i `fragment="1"`. W korpusie DOCX cytat z co najmniej dwiema pozycjami wyliczenia wystąpił 405 razy
w 169 plikach. Przykład autorów schematu dla DU/2026/252 numeruje te punkty 10–21, ciągnąc łańcuch zdania
nowelizującego — wbrew ich odpowiedzi na pytanie 11 (2026-10-16): nowelizacja numeruje zdania osobno od zdań poza
nią i zaczyna od fragmentu 1.

## Decyzja

- Przytoczone punkty, litery i tirety jednego `cytat-strukt` dzielą kontekst zdania: jeden `zdGUID`, `nr="1"`
  i kolejne fragmenty od 1 (`AmendmentXmlConverter.AddStructuredContentItems`). Jednostki niższego poziomu,
  części wspólne i zdania wewnątrz pozycji ciągną ten łańcuch tak jak w treści aktu.
- Przytoczone artykuły i ustępy mają, jak dotąd, własne zdania.

## Odrzucone alternatywy

- **Każda pozycja osobnym zdaniem** (dotychczas). Przeczy budowie wyliczenia: pozycje nie są zdaniami.
- **Ciąg dalszy łańcucha zdania nowelizującego**, jak w przykładzie (10–21). Wiąże numerację tekstu aktu
  zmienianego z miejscem, w którym go przytoczono, i przeczy odpowiedzi na pytanie 11.

## Konsekwencje

- Różnica wobec przykładu DU/2026/252 w numerach fragmentów cytowanych punktów jest zamierzona.
- Łańcuch zaczyna się od 1 w każdym `cytat-strukt`, także gdy wprowadzenie do wyliczenia przytoczono osobno,
  w `cytat-w-tekscie` ([ADR-0124](0124-zdanie-i-wprowadzenie-do-wyliczenia-przytoczone-w-calosci-sa-zdaniem.md)) —
  pozycje nie kontynuują wtedy jego fragmentu.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0131_QuotedEnumerationItems_AreFragmentsOfOneSentence_StartingAtOne`
