# ADR-0112: Adapter PDF czyta tylko tekst widoczny na stronie — litery przycięte i zakryte białym prostokątem odpadają, treść za krawędzią strony zostaje

- Status: Accepted
- Data: 2026-10-22

## Kontekst

Wydawca dziennika nakłada na strony załączników przekazanych przez organ własny nagłówek („Załączniki do obwieszczenia
…”, „Załącznik nr 1”, tytuł załącznika). Treść oryginału zostaje w pliku: przycięta do pasa pod nagłówkiem wydawcy
(`q re W n BT … ET Q`) albo zakryta białym prostokątem narysowanym po niej. Na stronie jej nie widać, ale PdfPig podaje
ją razem z resztą liter i nie przycina ich. W M.P. 2026 poz. 1012 (weryfikacja aktów urzędowych 2026-10-22) wiersze obu
warstw leżą o 1–2 pt od siebie i przeplatały się w jeden („ZałączZnaiłąkcznnrik1nr 1”, „GÓRGNÓERJNEEDJNE…”):
oznaczenie załącznika przestawało być rozpoznawalne, a oba załączniki (17 tabel) trafiały do treści obwieszczenia.
To samo zjawisko psuło tytuł załącznika w Dz. U. 2026 poz. 1320 („złłoożżaa”).

## Decyzja

`PdfVisibleText.Visible` odrzuca przed składaniem wierszy litery, które na stronie nie stoją:

- litera, której rdzeń (od linii bazowej na pół wysokości pisma) leży w całości poza obszarem przycięcia obowiązującym
  przy jej rysowaniu;
- litera, której środek przykrył biały prostokąt narysowany po niej (kolejność w strumieniu treści).

Obszar przycięcia i zakrycia to prostokąt otaczający ścieżkę; stan rysowania (q/Q, cm, W/W*, kolor wypełnienia) śledzi
się po operacjach strumienia strony, a literom przypisuje po numerze `TextSequence`, który jest numerem operacji
pokazującej tekst. Gdy numeracja się nie zgadza (formularz `Do` z własnym tekstem), strona zostaje bez zmian. Litera
poza stroną zostaje — to treść wychodząca za krawędź (wzór szerszy niż strona w PDF-ie z edytora), której nie zastępuje
nic widocznego.

## Odrzucone alternatywy

- **Zostawić tekst niewidoczny i usuwać powtórzone wiersze.** Wiersze obu warstw nie są równe (oryginał łamie tytuł
  w dwa wiersze, wydawca w jeden; „(poz. …)” zamiast „(M.P. poz. 1012)”), a przeplecione litery jednego wiersza nie dają
  się już rozdzielić po tekście.
- **Rozdzielać przeplecione wiersze po foncie albo kolorze.** Warstwy różnią się fontem i przestrzenią barw tylko
  przypadkiem; pogrubienie w obrębie jednej warstwy to też inny font.
- **Pełne renderowanie strony (rasteryzacja, maska widoczności).** Ciężka zależność dla zjawiska opisanego dokładnie
  przez kilka operacji strumienia.
- **Odrzucać także treść za krawędzią strony.** Zgodne z obrazem strony, ale gubi treść, której DOCX nie gubi, a żaden
  nagłówek wydawcy jej nie zastępuje (3 dokumenty korpusu: wzory przycięte prawą krawędzią).

## Konsekwencje

- Tekst ukryty w inny sposób (biały tekst, tryb renderowania 3, przycięcie ścieżką z krzywą, formularz `Do`) zostaje,
  jak dotąd.
- Litera, która obszaru przycięcia dotyka choć skrajem rdzenia, zostaje — także wiersz przecięty krawędzią komórki
  i odstęp przycięty do zerowej szerokości.
- Białe tło komórki narysowane przed tekstem niczego nie zakrywa — rozstrzyga kolejność w strumieniu.

## Weryfikacja

- `PdfBlockReaderTests.TextOutsideItsClip_IsNotOnThePage` — nagłówek oryginału przycięty nad pasem treści.
- `PdfBlockReaderTests.TextCoveredByAWhiteBoxDrawnAfterIt_IsNotOnThePage_ButTextOnAWhiteBackgroundIs` — zakrycie po
  tekście odrzuca, tło przed tekstem nie.
