# ADR-0126: Zdanie-przedmiot stoi w każdym miejscu komendy, zdanie-miejsce przy swojej jednostce; zdanie nazwane wyżej bez numeru bierze numer z przepisu niższego albo daje instrukcję ręczną

- Status: Accepted (uzupełnia [ADR-0102](0102-zdanie-nazwane-w-komendzie-jest-celem.md); doprecyzowuje [ADR-0016](0016-wyliczenie-celow-to-osobne-komendy.md) w punkcie przedziału i dziedziczenia celów)
- Data: 2026-10-28

## Kontekst

Odczyt adresu idzie za § 59 ust. 2 ZTP: „art. 7 i 8 zdanie pierwsze” zamyka jedno odesłanie, więc zdanie trafia tylko do
ostatniej jednostki wyliczenia. Dla zdania-miejsca („w ust. 10 i w ust. 11 w zdaniu pierwszym wyrazy …”) to trafne, ale
gdy zdanie jest przedmiotem komendy („w art. 7 i 8 zdanie pierwsze otrzymuje brzmienie:”), art. 7 zostawał celem
w całości — cytat zdania zastąpiłby cały artykuł. Zdanie za przedziałem („w art. 7–9 zdanie pierwsze”, „w ust. 2–4
w zdaniu pierwszym wyrazy …”) gubiło przedział: zostawało tylko zdanie ostatniej jednostki. A przepis, który nazwał
zdanie bez numeru, gubił je dla przepisów niższych: Dz. U. 2025 poz. 1367 art. 1 pkt 10 (pliki 322102, 343879) — „w § 12
w ust. 1 w zdaniu: a) pierwszym wyraz „Przetarg” …, b) drugim wyraz „przetarg” …” — dawał cztery automatyczne
`now:zamien-tekst` na całym ustępie, a pod „w zdaniu ostatnim:” zamiana szła automatycznie na całą jednostkę, choć
ADR-0102 każe dać wtedy instrukcję ręczną.

## Decyzja

- **Zdanie będące przedmiotem komendy stoi w każdym miejscu wymienionym przed nim** — gdy treść cytowana to same zdania
  (ADR-0124) albo czasownik uchylenia ma za przedmiot zdanie i żadnej innej jednostki (`SentenceTargets.ShareNamedSentences`).
  Powtórzone „w” niczego tu nie zmienia: miejsca są okolicznikami, a przedmiot jest jeden. Kilka zdań w kilku jednostkach
  — zakres w każdej (`SentenceTargets.MergeReplacedRun`).
- **Zdanie będące miejscem należy do jednostki, po której stoi** (§ 59 ust. 2, § 89 ust. 2 ZTP) — odczyt jak dotąd;
  wyliczenie zwarte z miejscem („w art. 7 i 8 w zdaniu pierwszym wyrazy …”) czyta się tak samo.
- **Zdanie za przedziałem stoi w każdej jednostce przedziału** (`UnitAddressReader.Read` zachowuje oba końce). Takiego celu
  `eId` nie zaadresuje — para eId na zdaniach objęłaby tekst między nimi, a rozwinięcia przedziału ADR-0016 zabrania — więc
  instrukcja jest ręczna na przedziale jednostek (`SentenceTargets.SentenceInEveryUnitOfARange`, ADR-0100).
- **Przepis, który podaje miejsce i nazywa zdanie bez numeru**: gdy jego adres kończy się samym słowem i dwukropkiem
  („w zdaniu:”, „zdanie:”, „w zdaniach:” — `UnitAddressReader.LeavesSentenceOpen`), numer zdania daje liczebnik na początku
  przepisu niższego, w każdym miejscu (`LegalReferenceService.InheritPlace`); bez liczebnika albo pod „w zdaniu
  ostatnim:” — instrukcja ręczna na jednostce (ADR-0102). Wzmianka o zdaniu w prozie zdania nie otwiera.

## Odrzucone alternatywy

- **Rozdzielanie każdego głębszego poziomu na wszystkie jednostki wyliczenia już w odczycie adresu.** Łamie odesłania,
  w których poziom głębszy należy tylko do ostatniej jednostki: korpus 347576_3084408 art. 1 pkt 30, „w art. 69 i art. 70
  ust. 1 wyrazy …” to art. 69 i art. 70 ust. 1. Rolę zdania (przedmiot czy miejsce) pokazuje dopiero rodzaj komendy i treść
  cytowana, więc rozstrzyga `SentenceTargets`.
- **Powtórzone „w” jako sygnał zakresu** — przy zdaniu-przedmiocie zakres jest ten sam.
- **Instrukcja ręczna we wcześniejszych miejscach zdania-przedmiotu** — to gramatyka, nie niejednoznaczność.
- **Dopisanie „w zdaniu ” do tekstu przepisu niższego** — tekst komendy zasila też ADR-0122 i odczyt przedmiotu.

## Konsekwencje

- Korpus DOCX: zmieniają się 4 instrukcje w 2 plikach (322102, 343879: `…__art_12__ust_1` → `…__zdn_1` / `…__zdn_2`);
  pozostałe kształty korpus zna tylko jako odesłania albo nie zna wcale. Akty ISAP — bez zmian.
- W backlogu: jednostka-przedmiot po wyliczeniu („w art. 7 i 8 uchyla się ust. 2” — uchylenie całego art. 7) i przepis
  z własnym adresem pod kilkoma miejscami (bierze pierwsze miejsce, ADR-0016).

## Weryfikacja

- `ArchitectureDecisionTests.Adr0126_ObjectSentenceInEveryPlace_PlaceSentenceWithItsUnit_NumberFromBelow`
- `SentenceTargetTests` — zdanie-przedmiot w każdym miejscu, zdanie-miejsce, przedział, numer z przepisu niższego, zdanie
  bez numeru nazwane wyżej
- `UnitReferencesTests.Reader_SentenceLeftOpen_OnlyAtTheEndOfTheAddress`, `UnitReferencesTests.Reader_AnnouncedSentence_TakesTheLeadingOrdinal`,
  `UnitReferencesTests.Reader_SentenceAfterARangeOrAnEnumeration`
