# ADR-0103: Zmiana wyrazów w różnych formach („w różnej liczbie i różnym przypadku") to instrukcja ręczna na każdy cel

- Status: Accepted
- Data: 2026-10-20

## Kontekst

„użyte w art. 40, w art. 42 w ust. 1 w pkt 5 …, w różnej liczbie i różnym przypadku, wyrazy „sektorowy zespół
cyberbezpieczeństwa” zastępuje się użytymi w odpowiedniej liczbie i odpowiednim przypadku wyrazami „CSIRT sektorowy”"
(DU/2026/252 art. 1 pkt 42). Komenda podaje wyrazy w formie podstawowej, a w każdym miejscu aktu zmienianego stoją one
w innej formie. Eksport dawał `now:zamien-tekst` z formą podstawową z komendy ([ADR-0052](0052-zmiana-wyrazow-jest-instrukcja-zamien-tekst.md))
— instrukcję udającą automatyczną, która w akcie zmienianym trafia co najwyżej w mianownik. W korpusie DOCX 28 komend
w 24 dokumentach.

Autorzy schematu (odpowiedź z 2026-10-20 na pytanie 9): ich przykład daje osobny `now:zamien-tekst` na każdy cel,
z formą wziętą z aktu zmienianego; wyrażeniem regularnym, bez sztucznej inteligencji, nie da się tego zrobić, chyba że
konwerter ma ustrukturyzowany tekst zmieniany. Zasada ogólna 6 (2026-10-20): SAGA robi to, co wynika z samego aktu
zmieniającego; instrukcja automatyczna, która w akcie zmienianym nie trafi, jest gorsza niż ręczna.

## Decyzja

**Zmiana wyrazów, której komenda zapowiada różne formy („w różnej liczbie", „w różnym przypadku", „w odpowiedniej
liczbie", „w odpowiednim przypadku"), wychodzi na każdy cel jako `cytat-w-tekscie funkcja="zastap-recznie"` z nowymi
wyrazami z komendy — nie jako `now:zamien-tekst`.**

- Rozpoznaje `ManualTargets` (formuła poza wyrazami cytowanymi); cele i instrukcja ręczna — jak w
  [ADR-0100](0100-cel-w-zalaczniku-bez-adresu-to-instrukcja-reczna.md).
- `now:zamien-tekst` nie ma atrybutu `funkcja`, więc nie da się go oznaczyć jako ręcznego — stąd cytat.
- Jedna instrukcja na każdy cel; elementu z kilkoma celami schemat nie ma.

## Odrzucone alternatywy

- **`now:zamien-tekst` z formą podstawową** (stan sprzed decyzji). Udaje automatyczną, a w akcie zmienianym nie trafia
  w formy inne niż mianownik.
- **Odmiana wyrazów przez konwerter** (słownik form). Zgadywanie formy w miejscu, którego tekstu konwerter nie widzi —
  poza zakresem zasady 6; odszukiwanie form w akcie zmienianym należy do ewentualnej osobnej usługi instrukcji.

## Konsekwencje

- Stare wyrazy zostają tylko w treści zdania komendy; instrukcja niesie nowe w formie podstawowej.
- Tytuł aktu wymieniony wśród celów („użyty w tytule ustawy …, w art. 1 …") nie staje się osobnym celem — cel tytułu
  ([ADR-0101](0101-tytul-aktu-jako-cel-to-instrukcja-reczna-z-adresem-aktu.md)) powstaje tylko wtedy, gdy przepis
  nie wskazuje jednostek.

## Weryfikacja

`ArchitectureDecisionTests.Adr0103_InflectedWords_AreAManualInstructionPerTarget`, `InflectedWordChangeTests`.
