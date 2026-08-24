# ADR-0009: Słownik polskich liczebników jest jeden i generuje także regex do ich wyszukiwania

- Status: Accepted
- Data: 2026-07-29

## Kontekst

Polskie liczebniki słowne pojawiają się w akcie w trzech niezależnych rolach:

- oznaczenie tiretu i zdania — § 57 ust. 6 ZTP nakazuje powoływać je liczebnikiem porządkowym
  („tiret pierwsze", „zdanie drugie"), więc forma słowna jest KANONICZNA, nie anomalią;
- oznaczenie jednostki systematyzacyjnej — „KSIĘGA PIERWSZA", „DZIAŁ PIERWSZY" (§ 60 ZTP);
- operand terminu wejścia w życie — „po upływie czternastu dni od dnia ogłoszenia" (§ 45 ZTP).

Każda rola dostała własny słownik, bo powstawały w różnym czasie. W efekcie w kodzie były **trzy
niezależne źródła prawdy** o tym samym zbiorze:

| źródło | forma | zakres |
|---|---|---|
| `PolishDates.NeuterOrdinalMap` | nijaka, mianownik, formy dokładne | 1–19 + okrągłe 20–90 |
| `RomanNumeralConverter.WordOrdinals` | **żeńska**, mianownik, formy dokładne | **tylko 1–12** |
| `UnitReferenceTokenizer.OrdinalWord` | rdzenie + dowolna końcówka (regex) | **rdzenie 1–20** |

Rozjazdy nie są hipotetyczne — wszystkie trzy zostały zmierzone w kodzie i **każdy zawodził cicho**:

1. **„DZIAŁ PIERWSZY" / „TYTUŁ PIERWSZY" nie było rozpoznawane.** `ParagraphClassifier` wpuszczał
   `\p{L}+` jako oznaczenie, ale rozstrzygał je słownik zawierający wyłącznie formy żeńskie.
   „CZĘŚĆ PIERWSZA"/„KSIĘGA PIERWSZA" działało (rzeczowniki żeńskie), a „dział", „tytuł", „rozdział",
   „oddział" są **męskie** → nagłówek lądował jako `Unknown`, a artykuły pod nim dostawały eId
   z poprzedniej jednostki.
2. **„tiret czwartego" gubiło cel.** Tokenizer celowo dopuszczał odmienioną końcówkę (`\p{Ll}*`),
   a słownik znał tylko formy na `-e` → token powstawał, normalizacja zwracała null, cel lądował
   w `Unresolved`.
3. **Wartości 30–90 były nieosiągalne** — rdzenie tokenizera kończyły się na `dwudziest`.

Do tego dwa mniejsze: `UnitReferenceTokenizer.MapTiretDepth` był **czwartym** miejscem zamiany słowa
na liczbę (hardkodowane `if`-y `podwójn`→2 … `pięciokrotn`→5), a
`EntryIntoForcePatterns.FirstDayAfterLapseOfMonths` nie miał gałęzi słownej operandu, którą jego
bliźniak `AfterMonths` miał.

Wspólny mianownik wszystkich pięciu defektów: **„znajdź liczebnik w tekście" i „przeczytaj liczebnik
na liczbę" miały osobne definicje tego samego zbioru.** Dopóki tak jest, każde uzupełnienie jednej
strony milcząco rozjeżdża się z drugą.

## Decyzja

Jeden komponent — `src/Saga.Core/Helpers/PolishNumerals.cs` — jest **wyłącznym** właścicielem wiedzy
o polskich liczebnikach i wystawia obie strony:

- parsowanie: `TryParseOrdinal`, `TryParseCardinal`, `TryParseMultiplicity`;
- wyszukiwanie: `OrdinalAlternation`, `CardinalAlternation`, `MultiplicityAlternation` —
  **fragmenty regexa GENEROWANE z tych samych tabel**, wstrzykiwane do wzorców w
  `UnitReferenceTokenizer`, `AmendmentCommandParser` i `EntryIntoForcePatterns`.

Konsekwencje kształtu:

- **Liczebniki porządkowe trzymamy jako rdzeń + zamknięty zbiór końcówek przymiotnikowych.** Odmiana
  jest regularna, a rodzaj wymusza rzeczownik, nie parser — dlatego `TryParseOrdinal` jest
  agnostyczne rodzajowo. Obie zmierzone porażki były porażkami **nadmiernej surowości**.
- **Liczebniki główne trzymamy jako formy dokładne w dopełniaczu.** Ich odmiana jest nieregularna
  („dwóch/dwu/dwoma"), a po „po upływie" potrzebny jest wyłącznie dopełniacz — rdzeniowanie nic
  nie dałoby. Ta asymetria jest zamierzona i opisana w komentarzu klasy.
- **Kompozycja 1–99 z dwóch wyrazów** zamiast wyliczania form: dwie rozłączne tabele (jedności 1–19,
  dziesiątki 20–90), złożenie = dziesiątka + jedność 1–9. Walidacja jest strukturalna, wynika
  z rozłączności — „dziesięciu dwóch" pada, bo „dziesięciu" nie jest dziesiątką.
- Wpis `["jeden"] = 1` obok `["jednego"] = 1` **jest niezbędny**: w złożeniu jedynka pozostaje
  nieodmienna („dwudziestu jeden dni"), więc bez niego 21, 31, …, 91 są nieosiągalne.
- **`ParagraphClassifier` świadomie NIE dostaje wstrzykniętej alternatywy.** Jego wzorce są celowo
  bez `IgnoreCase` (wersaliki to sygnał ZTP), a generowana alternatywa jest małymi literami —
  wstrzyknięcie zabiłoby „KSIĘGA PIERWSZA". Tam obowiązuje wzorzec „znajdź luźno (`\p{L}+`),
  zwaliduj twardo": kotwica `^…$` nie pozwala fałszywce uciec, a `ParseHigherUnitToken` odrzuca
  „CZĘŚĆ OGÓLNA".
- W rodzinie `EntryIntoForcePatterns.After*` catch-all `\p{L}+` **zostaje** obok alternatywy: po
  operandzie stoi rzeczownik-kotwica („dni"/„miesięcy"/„lat"), więc nieznany liczebnik ma dać
  „formuła rozpoznana, operand nieznany", a nie zdegradować klauzulę do `Unrecognized`.

Zakres: **1–99**. Terminy i oznaczenia ≥ 100 prawodawca zapisuje cyframi.

## Odrzucone alternatywy

**Zostawić trzy słowniki i tylko uzupełnić każdy.** Najtańsze i najbardziej kuszące. Odrzucone,
bo trzy zmierzone rozjazdy są dowodem empirycznym, że to zawodzi — i zawodzi **cicho**: każdy z nich
przez jakiś czas istniał w kodzie, żaden nie wywalił builda ani testu. Uzupełnianie trzech list
w trzech plikach zakłada, że następna osoba znajdzie wszystkie trzy.

**Wyliczyć wszystkie formy.** 99 wartości × 3 rodzaje × ~7 przypadków to kilka tysięcy wpisów,
nieutrzymywalnych ręcznie; alternatywa regex byłaby gigantyczna. Rdzeń + 15 końcówek daje to samo
pokrycie w 27 wpisach.

**`[GeneratedRegex]` z ręcznie pisaną `const` alternatywą.** Byłoby szybsze w runtime, ale source
generator wymaga wzorca będącego **stałą kompilacji**, a stała nie może pochodzić ze słownika. To jest
dokładnie ten mechanizm — „regex żyje osobno od danych" — który wyprodukował wszystkie trzy rozjazdy.
Dlatego wzorce muszą być `static readonly`, a nie `const`, i to nie jest ustępstwo, tylko warunek
poprawności.

**Analizator morfologiczny (Morfologik/spaCy).** Kilkanaście MB słownika, licencja i koszt startu dla
99-elementowego, zamkniętego leksykonu.

## Konsekwencje

- Wzorce korzystające z alternatyw są `static readonly`, nie `const`; kolejność deklaracji ma
  znaczenie (inicjalizatory statyczne biegną w kolejności tekstowej). Generator rzuca
  `InvalidOperationException` przy pustych tabelach — bez tego cykl inicjalizacji dałby regex `(?:)`
  pasujący do pustki, czyli awarię absolutnie cichą.
- `[GeneratedRegex]` jest dla tych trzech rodzin niedostępny.
- `RomanNumeralConverter` jest wyłącznie rzymski; fallback „rzymska ?? słowna" należy do
  `ParagraphClassifier.ParseHigherUnitToken`, który zna domenę.
- `PolishDates` trzyma wyłącznie miesiące i daty. Miesiące zostają z ręcznie pisaną `const`
  alternatywą — zamknięty 12-elementowy leksykon nie potrzebuje generatora.
- **Naprawa „DZIAŁ PIERWSZY" zmienia klasyfikację nagłówka z `Unknown` na `DivisionUnit`**, więc eId
  artykułów pod takim nagłówkiem się przesuwa. To poprawa, ale rusza golden snapshoty
  w niewersjonowanym `tests/Saga.Core.Tests/Artifacts/` — przy pierwszym przebiegu diff należy
  obejrzeć, nie przebazowywać hurtowo.
- Poszerzenie zbioru końcówek zwiększa ryzyko, że przymiotnik w prozie zostanie wzięty za oznaczenie.
  Łagodzą to: wąski zbiór końcówek (bez form liczby mnogiej), guard `(?!\p{L})` wbudowany
  w generowany fragment (to on odróżnia „pierwsze" od „pierwszeństwo") oraz przebieg korpusowy.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0009_PolishNumerals_AreDefinedInExactlyOnePlace` — skanuje `src/`
  i zawodzi, gdy jakikolwiek plik poza `Helpers/PolishNumerals.cs` zawiera w KODZIE (nie
  w komentarzu) co najmniej 4 różne rdzenie liczebnikowe. Próg 4, a nie 1, bo pojedyncze wzmianki
  są legalne (`pierwszego\s+dnia` w formule wejścia w życie, „drugie" w nazwie stylu Worda);
  słownik albo alternatywa zawsze go przekracza.
- `PolishNumeralsTests.FindAndParse_AgreeOnEveryValue` — dla każdej wartości 1–99, każdej rodziny
  i każdej kanonicznej pisowni: alternatywa MUSI dopasować, a parser MUSI odczytać tę samą liczbę.
  To strażnik niezmiennika **find ≡ parse**; jego usunięcie jest jedynym sposobem, żeby rozjazd wrócił.
- `PolishNumeralsTests.NumeralTables_AreWellFormed` — niezmiennik tabel (jedności 1–19, dziesiątki
  jako wielokrotności 10 z [20..90], rozłączność kluczy i wartości).
- `PolishNumeralsIntegrationTests` — po jednym teście na każde wstrzyknięcie alternatywy:
  „tiret dwudzieste pierwsze" → `tiret_21`, „tiret czterdzieste drugie" → 42,
  „po upływie sześćdziesięciu jeden dni" → 61, „DZIAŁ PIERWSZY" → `DivisionUnit`/`I`.
  Wszystkie były czerwone przed tą zmianą.

Odwrócenie decyzji wymaga usunięcia którejś z tych asercji — zmiana widoczna w diffie.
