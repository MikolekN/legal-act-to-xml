# ADR-0062: Termin zmiany zapisany przy akcie zmieniającym obejmuje zmiany, które nazywają ten akt odesłaniem

- Status: Accepted (akt zmieniający i przepis sprawczy są już eksportowane — [ADR-0063](0063-zdarzenie-zmiany-nalezy-do-aktu-ktory-ja-wprowadza.md))
- Data: 2026-10-12

## Kontekst

Odnośnik tekstu jednolitego nazywa akt zmieniający w pełni tylko raz; dalsze odsyłają do tego miejsca (§ 106 ZTP):
„Dodany przez art. 5 pkt 2 ustawy, o której mowa w odnośniku 3". Dzień, w którym akt wejdzie w życie, stoi wtedy
przy jego pełnej nazwie — „…ustawy z dnia 14 kwietnia 2023 r. … (Dz. U. poz. 818), która wejdzie w życie z dniem
15 lutego 2024 r." — a odnośniki odsyłające go nie powtarzają.

Wersje czasowe ([ADR-0060](0060-jednostka-drukowana-dwa-razy-to-dwie-wersje-rozrozniane-okresem.md)) czytały termin tylko z odnośnika samej zmiany. Brzmienie przyszłe, którego
odnośnik odsyłał po akt, dostawało zdarzenie bez dnia (123 zdarzenia w korpusie), a jednostka dodana takim aktem —
bez drugiej kopii obok — nie była rozpoznawana jako przyszła wcale: wychodziła jak przepis obowiązujący.

Żeby pójść za odesłaniem, trzeba wiedzieć, który odnośnik stoi pod numerem drukowanym — to daje
[ADR-0061](0061-odnosnik-ma-oznaczenie-jak-w-akcie-a-znacznik-numer-jednoznaczny.md).

## Decyzja

**Zmiana, której odnośnik nazywa akt zmieniający odesłaniem, bierze termin wejścia w życie tego aktu z odnośnika,
w którym akt nazwano w pełni.**

- Odesłanie (`WordingVersions`): „ustawy / rozporządzenia, o której (którym) mowa w odnośniku N" w odnośniku
  o zmianie; odnośnik N szuka się po oznaczeniu drukowanym w tym samym ciągu numeracji.
- Terminem aktu jest tylko zdanie podrzędne przy jego nazwie: „…, która wejdzie w życie z dniem …" albo z terminem
  zapisanym prozą („z dniem określonym w decyzji Komisji Europejskiej"). Zdanie po średniku („…; wejdzie w życie
  z dniem …") mówi o przepisie, przy którym stoi tamten odnośnik, i na inne zmiany nie przechodzi.
- Własny termin odnośnika ma pierwszeństwo. Akt, o którym odnośnik mówi „która weszła w życie", terminu nie daje.
- Jednostka z takim odnośnikiem jest brzmieniem przyszłym także bez drugiej kopii obok (jednostka dodawana).
- `WordingVersion.TermFootnote` mówi, w którym odnośniku termin zapisano. Jest dowodem zdarzenia (`dowod`)
  i rozróżnikiem zdarzenia bez dnia (`odn-N`), więc zmiany czekające na ten sam termin aktu są jednym zdarzeniem.

## Odrzucone alternatywy

- **Przenosić każdy termin z odnośnika wskazanego** — także „…; wejdzie w życie z dniem …". Ten termin należy do
  jednego przepisu aktu zmieniającego; inne jego przepisy mogą wchodzić w życie innego dnia.
- **Zostawić zdarzenie bez dnia** (stan z ADR-0060). Schemat to dopuszcza, ale dzień jest w akcie zapisany —
  pomijanie go zostawiało też jednostki dodawane bez żadnej informacji, że jeszcze nie obowiązują.
- **Rozróżnik zdarzenia bez dnia z odnośnika samej zmiany.** Każda zmiana aktu miałaby własne zdarzenie, choć
  wszystkie czekają na to samo.

## Konsekwencje

- W korpusie zmienia się 35 tekstów jednolitych. Zdarzeń zmiany bez dnia i bez terminu zostaje 7 (było 123), z dniem jest 149 (było 140), a wszystkich 255 (było 362) — zmiany jednego aktu czekają na jedno zdarzenie. Okres wersji niesie 3218 jednostek (było 2904): doszły jednostki dodane albo zmienione aktem, którego termin stoi w innym odnośniku; dzień wejścia w życie ma 1867 jednostek przyszłych (było 1366), `obowiazuje-do` — 738 (było 514). Zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory); treść bez zmian.
- Odesłania innych postaci zostają nieczytane: „ustawy, o której mowa w zdaniu pierwszym odnośnika 33", „ustawy
  wymienionej jako druga w odnośniku 11".
- Tożsamość aktu zmieniającego (`zrodlo`, pole aktu w identyfikatorze zdarzenia) i przepis sprawczy (`podstawa`)
  nadal nie są eksportowane — to następny krok po tym samym odesłaniu.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0062_ChangeNamingItsActElsewhere_TakesTheTermOfThatAct`
- `WordingVersionTests` — dzień i termin prozą przez odesłanie, druga kopia, akt już obowiązujący, termin przepisu
  po średniku, odesłanie w ciągu numeracji, wspólne zdarzenie bez dnia.
