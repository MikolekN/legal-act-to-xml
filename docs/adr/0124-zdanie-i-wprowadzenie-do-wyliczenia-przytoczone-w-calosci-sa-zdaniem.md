# ADR-0124: Zdanie i wprowadzenie do wyliczenia przytoczone w całości są `zdaniem`; reszta tekstu cytowanego — `tekstem`

- Status: Accepted — Supersedes [ADR-0057](0057-zdanie-przytoczone-w-calosci-jest-zdaniem.md); supersedes w części [ADR-0086](0086-czesc-wspolna-bez-wyliczenia-i-bez-komendy-ktora-ja-nazywa-jest-tekstem-cytowanym.md) (wprowadzenie do wyliczenia nie chroni części wspólnej)
- Data: 2026-10-27

## Kontekst

`cytat-w-tekscie` przyjmuje treść jako `zdanie` albo `tekst`.
[ADR-0057](0057-zdanie-przytoczone-w-calosci-jest-zdaniem.md) dawał `zdanie` tylko komendzie, której przedmiotem jest
zdanie, a wprowadzenie do wyliczenia zostawiał tekstem — przykłady autorów schematu nie były tu zgodne (DU/2026/26:
`zdanie`, DU/2026/252: `tekst`). Zapytany autor schematu odpowiedział (2026-10-27): „zdanie" — gdy zastępowane jest
całe zdanie albo cały fragment zdania; „tekst" — gdy kilka wyrazów. W schemaPL fragment zdania to właśnie `zdanie`
z numerem fragmentu: wprowadzenie do wyliczenia jest zdaniem nr 1, fragmentem 1, a pozycje wyliczenia ciągną kolejne
fragmenty (§ 56–57 ZTP).

Wprowadzenie złożone w treści cytowanej stylem części wspólnej wychodziło dodatkowo częścią wspólną
(`cytat-strukt` z `czesc-wspolna-*` pod adresem `…__czw`): [ADR-0086](0086-czesc-wspolna-bez-wyliczenia-i-bez-komendy-ktora-ja-nazywa-jest-tekstem-cytowanym.md)
liczył „wprowadzenie do wyliczenia" w komendzie jako nazwę części wspólnej, choć schemat opisuje część wspólną jako
domykającą wyliczenie. A gdy komenda zmieniała wprowadzenie razem z pozycjami („wprowadzenie do wyliczenia i pkt 1
otrzymują brzmienie:"), cytat wprowadzenia dostawał adres pierwszej pozycji.

## Decyzja

- **Treść `cytat-w-tekscie` jest `zdaniem` wtedy i tylko wtedy, gdy przedmiotem komendy jest zdanie albo wprowadzenie
  do wyliczenia** (rzeczownik tuż przed „otrzymuje brzmienie" / „w brzmieniu", także z pozycjami, które wprowadza —
  `AmendmentFinalizer.SentenceObjectPattern`, `AmendmentContent.PlainTextIsSentences`). Wprowadzenie to jedno zdanie
  nr 1, fragment 1, z datami zdania, które je przytacza (ADR-0121). Miejscownik („w zdaniu pierwszym …”,
  „we wprowadzeniu do wyliczenia …”) przedmiotu nie nazywa.
- **Wszystko inne zostaje `tekstem`**: wyrazy, liczby i znaki (ręczne zamiany, ADR-0103), przedmiot instrukcji ręcznej
  nazwany bez cytatu, tytuły i nagłówki, akapity, podpisy, komórki, odnośniki.
- **Wprowadzenie do wyliczenia jest tekstem cytowanym niezależnie od stylu** (`QuotedTextSubject`, jeden akapit) i nie
  chroni części wspólnej (`AmendmentBuilder.CommonPartSubject` nazywa już tylko „część wspólną”). Część wspólna
  nazwana komendą zostaje `cytat-strukt` z `czesc-wspolna-*`.
- **Wprowadzenie zmieniane razem z pozycjami** ma adres jednostki, której wyliczenie otwiera
  (`AmendmentContent.PlainTextIsIntroduction`, `AmendmentTargetPairing.IntroductionOwner`): „w ust. 1 wprowadzenie do
  wyliczenia i pkt 1 …” — tekst na `…__ust_1`, punkt na `…__ust_1__pkt_1`.

## Odrzucone alternatywy

- **Wprowadzenie jako `tekst`** (ADR-0057). Sprzeczne z odpowiedzią autora i z budową zdania w schemaPL, gdzie
  wprowadzenie jest zdaniem z numerem fragmentu.
- **`zdanie` także dla akapitów.** Akapit tekstu załącznika bywa kilkoma zdaniami albo częścią mieszanego przedmiotu
  („akapit drugi i rysunek …”); jego postać w `cytat-w-tekscie` albo `cytat-strukt` to osobne pytanie do autorów.
- **Rozstrzyganie po elemencie schematu, w którym stoi zmieniana treść.** Tytuł jednostki ma w schemacie też `zdanie`,
  a zdaniem nie jest; o rodzaju treści mówi przedmiot komendy.

## Konsekwencje

- Korpus DOCX (87 plików): 142 wprowadzenia w 76 plikach zmieniają `tekst` na `zdanie` (3 z nich, zmieniane z pozycjami,
  dostają adres swojej jednostki), 17 wprowadzeń w 11 plikach przestaje być częścią wspólną (15) albo jednostką
  `nie-ztp-punkt` (2). Poprawnych dokumentów w ocenie korpusu 2211 → 2213; zgodnych ze schematem nadal 1996.
  171 aktów ISAP: 23 wprowadzenia w 13 aktach.
- Przykład DU/2026/26 (art. 11) jest odtworzony; przykład DU/2026/252 podaje wprowadzenia tekstem — to rozbieżność
  przykładu z odpowiedzią autora.
- Wprowadzenie i pozycje zmieniane razem stoją w dwóch cytatach, a każdy zaczyna numerację fragmentów od nowa.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0124_QuotedSentenceAndIntroduction_AreSentences_OtherTextIsNot`
- `AmendmentTargetsExportTests.QuotedIntroduction_IsExportedAsAFragmentOfASentence`,
  `AmendmentTargetsExportTests.IntroductionChangedWithItsPoints_IsAddressedToTheUnitWhoseEnumerationItOpens`
- `QuotedTextSubjectTests.QuotedIntroductionInACommonPartStyle_IsText_NotACommonPart`,
  `AmendmentBuilderTests.LoneCommonParts_UnderACommandNotNamingThem_AreQuotedText`
