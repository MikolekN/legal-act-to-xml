# ADR-0104: Znak dodawany bez wskazania miejsca w tekście („w pkt 4 dodaje się przecinek") to instrukcja ręczna ze znakiem

- Status: Accepted
- Data: 2026-10-20

## Kontekst

Przed dodaniem jednostki prawodawca dodaje znak na końcu jednostki poprzedniej: „w ust. 2 w pkt 5 dodaje się przecinek
i pkt 6 w brzmieniu:", „w pkt 3 na końcu dodaje się średnik i dodaje się pkt 4", „na końcu dodaje się przecinek i wyraz
„lub”". Komenda nie podaje słowa, po którym znak staje — to ostatnie słowo jednostki, a stoi ono tylko w akcie zmienianym.
Eksport nie dawał żadnej instrukcji, a zmiana ginęła: w korpusie DOCX 50 komend w 31 dokumentach. Zamiana znaku
kończącego jednostkę („kropkę zastępuje się średnikiem") jest już automatyczna — `now:zamien-tekst` z `wystapienie="ostatnie"`
([ADR-0054](0054-zamiana-znaku-konczacego-jednostke-ma-wlasny-cel.md)) — bo znak zastępowany komenda podaje.

Autorzy schematu (odpowiedź z 2026-10-20 na pytanie 10): ich przykład wpisuje ostatnie słowo jednostki z aktu zmienianego
(`now:zamien-tekst stary-tekst="Finansowego" nowy-tekst="Finansowego,"`); nie wszystkie instrukcje da się zapisać
automatycznie. Zasada ogólna 6: bez aktu zmienianego — instrukcja `*-recznie`.

## Decyzja

**Znak dodawany na końcu jednostki bez wskazania miejsca w tekście jest zmianą w tekście (`AmendmentCommandKind.AddSign`)
z celem w jednostce nazwanej przed komendą i wychodzi jako `cytat-w-tekscie funkcja="dodaj-recznie"` ze znakiem (i wyrazami
dodawanymi razem z nim: `, lub`).**

- Cel bierze się jak przy zamianie znaku: z jednostki nazwanej przed komendą, także w komendzie złożonej ze zmianą
  jednostki (`AmendmentStateManager.KeepSignChanges`).
- Znak dodawany po wskazanych wyrazach („po wyrazach „X” dodaje się przecinek") ma miejsce — zostaje zmianą automatyczną
  (`now:zamien-tekst` „X" → „X,").

## Odrzucone alternatywy

- **Brak instrukcji** (stan sprzed decyzji). Zmiana znika z eksportu.
- **`now:zamien-tekst` z pustym `stary-tekst`.** Instrukcja nie wskazuje, gdzie znak wstawić; udaje automatyczną.
- **Ostatnie słowo jednostki z aktu zmienianego** (jak w przykładzie autorów). Konwerter widzi tylko akt zmieniający
  (zasada 6).

## Konsekwencje

- Instrukcja ręczna niesie sam dodawany tekst; reszta komendy zostaje w zdaniu.

## Weryfikacja

`ArchitectureDecisionTests.Adr0104_MarkAddedWithoutAPlace_IsAManualAddition`, `MarkAddedWithoutPlaceTests`.
