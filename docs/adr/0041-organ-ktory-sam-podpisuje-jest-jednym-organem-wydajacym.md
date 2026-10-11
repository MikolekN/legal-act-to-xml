# ADR-0041: Organ, który wydaje akt i sam go podpisuje, jest w metadanych jednym organem wydającym

- Status: Accepted
- Data: 2026-10-11

## Kontekst

Metadane niosą `Organy`: każdy `Organ` ma rolę (`wydajacy`, `podpisujacy`, …), `Nazwa` i — opcjonalnie —
`OrganPodpisujacy` z `OsobaPodpisujaca`. Dla ustawy układ jest znany z przykładów autorów schematu: Sejm
wydaje, Prezydent podpisuje — dwa organy.

Dla pozostałych aktów organ wydający wychodził jako `UNKNOWN` (2079 z 2321 dokumentów korpusu), choć akt
nazywa go dwa razy:

- w strefie tytułowej, w dopełniaczu i zwykle wersalikami („ROZPORZĄDZENIE MINISTRA FINANSÓW") —
  `LegalDocument.IssuerLine`,
- w formule podpisu, w mianowniku („Minister Finansów: A. Domański") — `LegalDocument.Signature`.

Te dwie nazwy nie zawsze wskazują ten sam organ. W korpusie:

- 1751 aktów podpisuje organ ze strefy tytułowej (941 rozporządzeń, 724 teksty jednolite, 86 obwieszczeń),
- 116 rozporządzeń wydaje organ kolegialny, a podpisuje jego przewodniczący („Rady Ministrów" — „Prezes Rady
  Ministrów", „Krajowej Rady Radiofonii i Telewizji" — „Przewodniczący Krajowej Rady …"),
- 8 rozporządzeń Prezydenta ma drugą formułę — kontrasygnatę Prezesa Rady Ministrów,
- 2 rozporządzenia wydają wspólnie dwaj ministrowie („MINISTRÓW INFRASTRUKTURY ORAZ SPRAW WEWNĘTRZNYCH
  I ADMINISTRACJI"), każdy z własną formułą,
- 202 dokumenty nie mają odczytanego podpisu.

Jedyny przykład autorów schematu dla aktu innego niż ustawa (tekst jednolity, `DU20261261_obw_tj.xml`) ma
**jeden** organ: `rola="wydajacy"`, `Nazwa` „Marszałek Sejmu", z `OrganPodpisujacy` i `OsobaPodpisujaca`.

## Decyzja

**Organem wydającym aktu innego niż ustawa jest organ ze strefy tytułowej. Gdy nazywa go formuła podpisu,
jest to jeden `Organ` o roli `wydajacy` z osobą podpisującą; podpisujący, którego strefa tytułowa nie
wymienia, jest osobnym organem o roli `podpisujacy`.** Rozstrzyga `AuthoritiesXmlConverter`.

- **Ten sam organ**: kolejne wyrazy nazwy ze strefy tytułowej i z formuły podpisu są formami tych samych
  wyrazów, do końca krótszej nazwy („Marszałka Sejmu Rzeczypospolitej Polskiej" i „Marszałek Sejmu"). Formy
  jednego wyrazu mają wspólny początek i różnią się najwyżej końcówką („Ministra" i „Minister"; nie „Spraw"
  i „Sprawiedliwości").
- **Nazwa** takiego organu pochodzi z formuły podpisu — jest tam w mianowniku i w pisowni nazwy.
- **Współwydający**: podpisujący, którego nazwa (bez wyrazu głównego) stoi w strefie tytułowej dalej niż na
  początku, też jest organem wydającym.
- **Bez formuły nazywającej organ**: organ wydający dostaje nazwę ze strefy tytułowej, w pisowni nazwy
  i w mianowniku. Odmienia się tylko wyraz główny, ze słownika (`Heads`: „Ministra" → „Minister", „Rady" →
  „Rada", …); nazwa o wyrazie głównym spoza słownika zostaje w dopełniaczu.
- **Bez organu w strefie tytułowej** organ wydający zostaje `UNKNOWN` — podpis mówi, kto podpisał, nie kto wydał.
- **Ustawa** bez zmian: Sejm i podpisujący.
- **Zasób ELI** (`eliURI`) dostają wyłącznie organy, które mają go w przykładach autorów schematu: Sejm,
  Prezydent i Marszałek Sejmu.

## Odrzucone alternatywy

- **Zawsze dwa organy: wydający ze strefy tytułowej i podpisujący z formuły** — jak w ustawie. Jeden układ
  dla wszystkich aktów, ale sprzeczny z przykładem autorów schematu, a ten sam organ stałby w metadanych dwa
  razy pod dwiema nazwami (odmienioną ze strefy tytułowej i zapisaną w podpisie).
- **Organ wydający wprost z formuły podpisu**, bez strefy tytułowej. Najprościej, ale dla 116 rozporządzeń
  Rady Ministrów wydającym zostałby Prezes Rady Ministrów, a kontrasygnata stałaby się wydaniem aktu.
- **Nazwa zawsze ze strefy tytułowej, odmieniona w całości.** Wymaga analizy morfologicznej całej nazwy;
  formuła podpisu niesie mianownik gotowy, a bez niej wystarcza odmiana wyrazu głównego — reszta nazwy jest
  przydawką i się nie odmienia.
- **Zamknięty słownik organów** (nazwa → organ z zasobem ELI). Nie ma wiarygodnej listy zasobów, a nazwy
  resortów zmieniają się z każdym rządem (w korpusie 70 nazw) — słownik byłby zgadywaniem i stale nieaktualny.

## Konsekwencje

- W korpusie organ wydający `UNKNOWN` ma 0 dokumentów (było 2079). Układy: jeden organ wydający z osobą — 1751;
  wydający i podpisujący osobno — 116 (organ kolegialny) i 8 (kontrasygnata); dwóch wydających — 2; wydający
  ze strefy tytułowej bez podpisu — 202; ustawy bez zmian — 242. Zmiany dotyczą wyłącznie `Organy`; zgodność ze
  schematem bez zmian.
- Porównanie nazw jest porównaniem form, nie analizą morfologiczną: rozjechane w źródle „Ministra Finansów"
  i „Minister Finansów i Gospodarki" to dla niego ten sam organ (do końca krótszej nazwy).
- Słownik wyrazów głównych ma 8 wpisów — tyle, ile wyrazów głównych w korpusie. Nowy („Głównego Inspektora …")
  wyjdzie w dopełniaczu, dopóki nie zostanie dopisany.
- Akt wydany wspólnie bez odczytanych podpisów ma jeden organ o nazwie w liczbie mnogiej, jak w strefie
  tytułowej.
- Metadane nie znają tożsamości organu ponad nazwą: „Minister Finansów" i „Minister Finansów i Gospodarki"
  to dwie nazwy.
- Zasób Marszałka Sejmu stoi w przykładzie autorów pod `http://eli.gov.pl/…/MSEJMU`, a zasoby Sejmu
  i Prezydenta pod `https://`; eksport używa jednej podstawy (`https://`) dla wszystkich.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0041_AuthorityThatIssuesAndSigns_IsOneIssuingAuthority`
- `IssuingAuthorityTests` — organ podpisujący sam, organ kolegialny, akt wydany wspólnie, kontrasygnata,
  tekst jednolity, brak podpisu, brak organu w strefie tytułowej, porównanie nazw i odmiana wyrazu głównego.
