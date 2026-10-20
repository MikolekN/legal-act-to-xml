# ADR-0024: Jednostka bez odczytanego oznaczenia nie ma eId, tylko nazwany powód jego braku

- Status: Accepted — Superseded in part by [ADR-0099](0099-jedyny-zalacznik-bez-numeru-ma-adres-zal-1.md) (załącznik bez numeru)
- Data: 2026-10-10

## Kontekst

Parser buduje czasem jednostkę redakcyjną, której oznaczenia nie odczytał: akapit w stylu ustępu bez
numeru (legenda wzoru, sankcja w osobnym wierszu, treść oświadczenia), pierwszy akapit artykułu, po
którym stoi od razu „§ 2." albo „2.", literę z oznaczeniem spoza wzorca. Eksport zapisywał ją jak
każdą inną: `nr` o wartości `UNKNOWN` i eId z samym skrótem poziomu (`art_16f__ust`), który
dziedziczyły jednostki pod nią (`art_16f__ust__pkt_1`). Wzorce schemaPL.xsd odrzucają jedno i drugie
(`nr`, `EIdTyp`) — po poprzednich poprawkach było to 119 jednostek w 56 dokumentach korpusu.

Schemat ma na ten przypadek osobne pojęcie. Atrybut `brak-eid` (`PowodBrakuEIdTyp`) opisuje brak
adresu jako decyzję, którą trzeba nazwać, a wartość `jednostka-nieoznaczona` definiuje wprost:
jednostka bez odczytanego oznaczenia nie wnosi segmentu do ścieżki, a jej adresem jest adres
najbliższego oznaczonego przodka. Przykłady autorów schematu noszą ten atrybut na każdym `ustep-n`.

## Decyzja

**Jednostka redakcyjna bez odczytanego oznaczenia nie ma eId. Niesie `brak-eid="jednostka-nieoznaczona"`
i nie wnosi segmentu do eId jednostek pod sobą.**

- **eId w modelu** (`BaseEntity.Id`): jednostka bez `Number` jest w ścieżce przezroczysta, tak jak
  jednostka niejawna. Punkt pod ustępem bez numeru ma `art_16f__pkt_1`.
- **Eksport**: element bez `eId`, z `brak-eid` i pustym `nr`. Ponieważ zwykłe typy wymagają `nr`
  zgodnego ze wzorcem, jednostka wychodzi jako `nie-ztp-*`, a z nią jednostki pod spodem.
- Akapit bez oznaczenia obok paragrafów („§") należy do rodziny `paragraf-kodeksu`
  (`nie-ztp-paragraf-kodeksu`) — artykuł nie miesza ustępów z paragrafami.
- Jedyny ustęp artykułu (`ustep-n`, `paragraf-kodeksu-n`) dostaje ten sam atrybut. Jedno miejsce pisze
  adres albo powód jego braku: `UnitAddressXml`.

## Odrzucone alternatywy

- **Skrót poziomu bez oznaczenia w eId i `UNKNOWN` w `nr` (stan sprzed decyzji).** Niezgodne ze
  schematem i mylące: `UNKNOWN` wygląda jak oznaczenie, a `art_16f__ust` jak adres, choć w akcie nie
  ma jednostki, na którą wskazują.
- **Oznaczenie nadane przez parser** (kolejny wolny numer albo licznik `ust_x1`). Adres, którego nie
  da się wyprowadzić z aktu, nie zgodzi się z żadnym powołaniem i zmieni się przy każdej poprawce
  parsera.
- **Wchłonięcie akapitu bez numeru do poprzedniej jednostki jako dalszego akapitu.** Bywa właściwe
  (legenda wzoru należy do ustępu ze wzorem), ale wymaga modelu treści z kilkoma akapitami, tabelą
  i wzorem, którego jeszcze nie ma; nie rozstrzyga też pierwszego akapitu artykułu. Zostaje w backlogu
  jako poprawa rozpoznania — ta decyzja mówi tylko, jak zapisać jednostkę, której oznaczenia nie ma.

## Konsekwencje

- Zmienia wyjście: znikają `nr` = `UNKNOWN` i eId z pustym segmentem; jednostki pod jednostką bez
  oznaczenia mają krótszy eId; `ustep-n` niesie `brak-eid`.
- Dwie jednostki bez oznaczenia w jednym artykule dają swoim punktom ten sam adres (`art_16f__pkt_1`
  dwa razy). Schemat przyjmuje to świadomie — adres jest ścieżką po jednostkach oznaczonych — a liczba
  powtórzonych eId w korpusie nie wzrosła (3433 → 3367).
- `brak-eid` jest miarą jakości rozpoznania: `nie-ztp-*` z tym atrybutem to jednostki, których
  oznaczenia nie odczytano (119 w korpusie), policzalne w gotowym pliku.
- Artykuł bez oznaczenia zostaje elementem `artykul` z pustym `nr` (niezgodny ze schematem) —
  `nie-ztp-artykul` nie jest jeszcze emitowany. W korpusie taki artykuł nie występuje.
- Jedyny ustęp artykułu zapisany z numerem („Art. 5. 1. …") wychodzi jako `ustep-n` bez numeru, choć
  w modelu ma eId `art_5__ust_1`; schemat nie dopuszcza pojedynczego `ustep`.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0024_UnitWithoutADesignation_HasNoEId_OnlyAReason`.
- `MissingEIdReasonTests` — `ustep-n`, jednostka bez oznaczenia, adresy jednostek pod nią, akapit
  obok paragrafów kodeksu, zgodność ze schematem.
