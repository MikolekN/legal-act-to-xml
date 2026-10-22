# ADR-0114: Tekst narysowany później innym pismem na tekście, którego nie zakryto, przesłania go — adapter PDF czyta tylko tekst wierzchni

- Status: Accepted
- Data: 2026-10-22

## Kontekst

[ADR-0112](0112-adapter-pdf-czyta-tylko-tekst-widoczny-na-stronie.md) odrzuca litery przycięte i zakryte białym
prostokątem — tak wydawca nakłada własny nagłówek na stronę oryginału. W Dz. U. 2026 poz. 1341 wydawca napisał nagłówek
załącznika („Załącznik do rozporządzenia … (Dz. U. poz. 1341)", 8 pt) wprost na nagłówku oryginału („… (Dz. U. poz. …)",
12,5 pt), niczego nie zakrywając: oba są czarne i widoczne, na stronie nachodzą na siebie. W warstwie tekstowej ich
litery tworzą jeden wiersz („Mini[Z]s[a]t[łą]r[c]a[z] [n]I[ik]n …"), wiersz przyłączenia nie był rozpoznany, a cały
załącznik (179 stron) wchodził do ostatniego paragrafu aktu jako jego ustępy i fragmenty.

## Decyzja

**Ciąg tekstu przykryty tekstem narysowanym po nim innym pismem jest niewidoczny.** Ciąg to kolejne operacje pokazujące
tekst jednym pismem bez skoku linii bazowej o więcej niż dwa rozmiary pisma. Ciąg znika, gdy co najmniej 5 jego liter
i co najmniej ćwierć jego liter zderza się z literami ciągu narysowanego później innym pismem (zderzenie: części wspólne
prostokątów liter obejmują co najmniej 30 % mniejszej litery) — `PdfVisibleText.WithoutOverprinted`.

## Odrzucone alternatywy

- **Zostawić oba teksty.** Tak było: wiersz przyłączenia i całe drzewo załącznika przepadały w ostatnim paragrafie.
- **Zostawić większe pismo** (tekst oryginału). Oryginał ma w nagłówku „(Dz. U. poz. …)" — publikator wpisuje dopiero
  wydawca; na wierzchu leży jego tekst i to on jest treścią strony, jak przy zakryciu białym prostokątem.
- **Rozpoznawać sam nagłówek wydawcy po wzorcu** („Załącznik do … (Dz. U. poz. N)"). Węższe, a przyczyna jest ogólna:
  tekst przykryty innym tekstem nie jest czytelny, niezależnie od tego, co zawiera.

## Konsekwencje

- Pogrubienie nadrukiem (ta sama litera tym samym pismem w tym samym miejscu) i indeks górny obok tekstu tego warunku nie
  spełniają — pierwsze ma to samo pismo, drugi z tekstem się nie zderza.
- Korpus PDF (PDF-y z edytora) bez zmian; z 64 aktów ISAP zmienia się jeden — poz. 1341.
- Tekst nachodzący na tekst z innego powodu (komórka tabeli wylewająca się na sąsiednią) traci wcześniejszy ciąg tylko przy
  zderzeniu co najmniej ćwierci jego liter — taki układ jest i tak nieczytelny na stronie.

## Weryfikacja

`PdfBlockReaderTests.TextOverprintedByLaterTextInAnotherSize_IsNotOnThePage`
