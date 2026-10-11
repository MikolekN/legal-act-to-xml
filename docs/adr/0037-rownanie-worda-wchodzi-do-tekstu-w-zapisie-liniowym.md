# ADR-0037: Równanie Worda wchodzi do tekstu akapitu w zapisie liniowym, dopóki eksport nie pisze MathML

- Status: Accepted
- Data: 2026-10-11

## Kontekst

Wzory i symbole w szczotkach są równaniami Worda (`m:oMath`): osobnym akapitem („I_oze=(I_e∙U_oze)+I_b"),
symbolem otwierającym wiersz legendy („I_e – ilość energii …") albo symbolem w środku zdania („do obliczenia
kwoty wydatków WYD_n, o której mowa …"). Odczyt tekstu akapitu przeglądał wyłącznie przebiegi `w:r`, więc treść
równania znikała bez śladu: zdanie zostawało z dziurą („kwoty wydatków , o której mowa"), wiersz legendy zaczynał
się od myślnika i stawał się tiretem (319 jednostek `nie-ztp-tiret`), a jednostka z samym wzorem nie miała `p`
(niezgodność ze schematem). Miara pokrycia tekstu tego nie widziała — czyta źródło tym samym odczytem.

W korpusie: 1206 równań w 56 dokumentach (47 obwieszczeń tekstu jednolitego, 4 rozporządzenia, 5 ustaw).

Schemat ma na wzór element `wzor-mat` z wyrażeniem w MathML i legendą (`wprowadzenie-do-opisu`, `opis` z
`symbol-mat`). Dla symbolu w zdaniu nie ma elementu z MathML — treść zdania to tekst z `sup`, `sub`, `i`, `b`.

## Decyzja

**`ParagraphExtensions.GetFullText` czyta równanie w miejscu, w którym stoi w akapicie, jako tekst w zapisie
liniowym (`OfficeMathText`): indeks dolny `E_i`, górny `x^2`, ułamek `a/b`, nawiasy i znaki działań jak
w równaniu; argument złożony z więcej niż jednego wyrazu stoi w nawiasie (`WYD_(n-1)`, `(a+b)/c`), pierwiastek
wyższego stopnia to `√(8&x)`.** Reszta potoku widzi równanie jak każdy inny tekst.

To rozstrzygnięcie o **odczycie**, nie o kształcie docelowym: wzór w osobnym akapicie ma w eksporcie stać się
`wzor-mat` z MathML (backlog), a zapis liniowy zostanie dla symboli w zdaniu, gdzie schemat MathML nie przewiduje.

## Odrzucone alternatywy

- **Zostawić równania pominięte do czasu MathML.** Każdy dzień pracy potoku na tekście z dziurami utrwala
  błędy pochodne: fałszywe tirety z wierszy legendy, zdania bez podmiotu, jednostki bez treści.
- **Sam ciąg znaków bez znaczników** („WYDn-1", „PIc"). Nie da się go odróżnić od innego wzoru: „E_i" i „Ei",
  „x^2" i „x2", „(a+b)/c" i „a+bc" to różne wyrażenia.
- **Notacja indeksu górnego potoku** (`x[2]` → `<sup>`) dla potęg i brak znacznika dla indeksu dolnego. Dwa
  zapisy w jednym wzorze, a `[1)]` jest już postacią znacznika odnośnika — wykładnik „1)" stałby się odnośnikiem.
- **MathML od razu, bez etapu tekstowego.** Wymaga modelu wzoru i legendy, konwersji OMML → MathML oraz
  rozpoznania granic wzoru w treści cytowanej; symbol w zdaniu i tak musi mieć postać tekstową.

## Konsekwencje

- Tekst 56 dokumentów zyskuje treść równań; w eksporcie pojawiają się znaki zapisu liniowego (`_`, `^`, `/`,
  nawiasy grupujące), których w szczotce nie ma jako znaków. Konsument tekstu musi je czytać jako zapis wzoru.
- Wiersz legendy zaczyna się od symbolu, więc nie jest już tiretem: 319 fałszywych `nie-ztp-tiret` znika,
  a wiersze legendy są dalszą treścią jednostki (`fragment`).
- Akapit z samym wzorem złożony stylem artykułu w treści cytowanej stawał się artykułem bez numeru, odkąd
  ma tekst. `AmendmentBuilder` wymaga dla artykułu oznaczenia w treści — sam styl go nie czyni, tak jak poza
  cytatem.
- Zgodnych ze schematem w całości: 1677 → 1685 z 2321; pokrycie tekstu: teksty jednolite 1402 → 1215 zgubionych
  akapitów, rozporządzenia 154 → 127.
- Wzór i legenda w treści cytowanej trafiają dziś do tekstu cytatu przed jego jednostkami (tak budowniczy
  traktuje każdy akapit bez oznaczenia) — miejsce w cytacie jest w backlogu.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0037_Equation_IsPartOfTheParagraphText_InLinearForm`
- `OfficeMathTextTests` — indeksy, ułamek, nawiasy, operator z granicami, pierwiastek, równanie w zdaniu,
  symbol na początku wiersza legendy, akapit bez równań.
- `AmendmentBuilderTests.ArticleStyleWithoutADesignation_IsNotAnArticle`
