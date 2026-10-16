# ADR-0094: Odnośnik przy oznaczeniu jednostki systematyzacyjnej stoi za jej numerem

- Status: Accepted
- Data: 2026-10-16

## Kontekst

Teksty jednolite stawiają odnośnik o brzmieniu jednostki systematyzacyjnej przy jej oznaczeniu: „Rozdział 3⁵⁾" —
„Rozdział w brzmieniu ustalonym przez art. 7 pkt 5 ustawy, o której mowa w odnośniku 1.", „Dział IIIa¹⁾" — „Dział
dodany przez …". Parser odcinał znacznik od numeru, a odnośnik nie trafiał nigdzie: w ocenie korpusu DOCX
z 2026-10-16 treść odnośnika ginęła w 27 dokumentach. Schemat ma na to miejsce: nagłówek jednostki
(`NaglowekJednostkiSystematyzacyjnejTyp`, na bazie `JednostkaRedakcyjnaNumerowanaTJTyp`) przyjmuje zaraz za `nr`
dowolnie wiele elementów `odnosnik` i `odsylacz-do-odnosnika`.

## Decyzja

**Znaczniki odnośników stojące przy oznaczeniu jednostki systematyzacyjnej jednostka niesie w modelu
(`ISystematizingUnit.DesignationMarkers`), a eksport stawia odnośniki w nagłówku zaraz za `nr`, przed tytułem.**

- Kolejne odwołanie do odnośnika osadzonego wcześniej wychodzi tam jako `odsylacz-do-odnosnika`.
- Znacznik, za którym nie stoi odnośnik tego aktu, znika: nagłówek nie ma tekstu własnego, więc indeks górny (ADR-0047)
  nie ma w nim miejsca.
- Indeks górny oznaczenia zlany ze znacznikiem w jeden nawias („Oddział 2[1 15)]") zostaje przy numerze; jednostka
  niesie sam znacznik (`FootnoteMarkers.Only`).

## Odrzucone alternatywy

- **Odnośnik na początku tytułu (`p`).** Pierwsza wersja tej zmiany. Ważna wobec schematu, ale przenosi odnośnik
  z oznaczenia do tytułu, choć schemat ma miejsce przy numerze; jednostka bez tytułu dostawała `p` z samym odnośnikiem.
- **Odnośnik wewnątrz `nr`.** W nagłówku jednostki systematyzacyjnej `nr` jest mieszany i przyjmuje jeden odnośnik,
  ale `nr` załącznika (`zalacznik-nr`) to typ prosty — miejsce za numerem jest wspólne dla obu i mieści kilka odnośników.
- **Odnośnik pomijany** (stan wcześniejszy). Treść odnośnika bywa jedyną informacją o brzmieniu jednostki i dacie jej
  wejścia w życie, a znikała z eksportu.

## Konsekwencje

- `InlineTextXml` uznaje za miejsce odnośników także sam nagłówek (`HoldsDesignation`); `FootnoteXmlConverter`
  zostawia tam wyłącznie elementy odnośników.
- `WordingVersions` czyta odnośniki przy jednostkach redakcyjnych; data z odnośnika jednostki systematyzacyjnej
  („wszedł w życie z dniem …") nie nadaje jej okresu.

## Weryfikacja

`ArchitectureDecisionTests.Adr0094_FootnoteAtADesignation_StandsRightAfterTheNumber`,
`FootnoteTests.FootnoteAtAUnitDesignation_StandsRightAfterTheNumber`,
`FootnoteTests.MarkerAtADesignationWithoutAFootnoteOfThisAct_IsDropped`,
`SystematizingUnitKnownGapsTests.SuperscriptMergedWithAFootnoteMarker_StaysWithTheNumber`.
