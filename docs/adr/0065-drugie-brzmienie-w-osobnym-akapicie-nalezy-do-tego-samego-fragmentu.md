# ADR-0065: Drugie brzmienie wydrukowane osobnym akapitem należy do tego samego fragmentu, nie do „dalszej treści"

- Status: Accepted
- Data: 2026-10-13

## Kontekst

Wprowadzenie do wyliczenia, część wspólną i niektóre zdania tekst jednolity drukuje w dwóch brzmieniach tak, że
drugie stoi w **osobnym akapicie** bez oznaczenia, zaraz pod pierwszym:

```
1. SKW może uzyskiwać … dane … przekazu telekomunikacyjnego …, określone w:[7)]
SKW może uzyskiwać … dane … komunikatu elektronicznego …, określone w:[8)]
1) art. 180c …
```

Akapit bez oznaczenia jest u nas dalszą treścią jednostki ([ADR-0029](0029-akapit-bez-oznaczenia-jest-dalsza-trescia-jednostki.md))
i wychodził jako `fragment` — także drugie brzmienie części wspólnej. Eksport nie mówił więc, że to dwie wersje
tego samego tekstu, a punkty wyliczenia stały pod „fragmentem", nie pod wprowadzeniem.

## Decyzja

**Akapit stojący zaraz za tekstem jednostki, który jest drugim brzmieniem jej ostatniego zdania albo jej samej,
wraca tam, gdzie jego pierwsze brzmienie.** Rozpoznaje to `WordingVersions` po zbudowaniu dokumentu:

- **Zdanie i wprowadzenie do wyliczenia** — oba akapity kończą się odnośnikiem o podmiocie „Zdanie …" albo
  „Wprowadzenie do wyliczenia", a pierwszy mówi „obowiązuje do …". Zdania drugiego akapitu dołączają do zdań
  jednostki i dalej czyta je reguła zdania w dwóch brzmieniach ([ADR-0064](0064-zdanie-w-dwoch-brzmieniach-to-dwa-zdania-o-jednym-numerze.md)): jeden numer, różny `okres`.
  Oba brzmienia wprowadzenia otwierają **to samo** wyliczenie — są pierwszym fragmentem tego samego zdania,
  a punkty jego dalszymi fragmentami (`SentenceProcessor`).
- **Część wspólna** — ma własny adres, więc jej drugie brzmienie jest drugą częścią wspólną tego samego wyliczenia:
  to samo eId, różny `okres`, jak jednostka wydrukowana dwa razy ([ADR-0060](0060-jednostka-drukowana-dwa-razy-to-dwie-wersje-rozrozniane-okresem.md)). Obie są tym samym
  fragmentem zdania. `CommonPartXmlConverter.HandleCommonPart` eksportuje każdą część wspólną zamykającą
  wyliczenie, nie tylko pierwszą.
- Znaczniki dalszej treści jednostki czyta się w kolejności dokumentu — za jej tekstem, przed jednostkami
  podrzędnymi (`FootnoteResolver`).

## Odrzucone alternatywy

- **Rozstrzygać przy budowaniu jednostek.** Budowniczy nie zna treści odnośników; a to ona — nie styl ani układ
  — mówi, że akapit jest drugim brzmieniem.
- **Zostawić `fragment` i nadać mu okres.** Schemat nie daje fragmentowi czasu, a wyliczenie zostałoby pod
  niewłaściwym elementem.
- **Wprowadzenie w dwóch brzmieniach jako dwa zdania, każde z własnym wyliczeniem.** Punkty są wydrukowane raz
  i należą do obu brzmień.
- **Druga część wspólna jako drugie zdanie pierwszej.** Część wspólna jest adresowalną jednostką (`…__czw`);
  odnośnik mówi o niej całej, tak jak o jednostce.

## Konsekwencje

- W korpusie zmienia się 17 dokumentów. W tekstach jednolitych 24 akapity wychodzące dotąd jako `fragment` stają się drugim brzmieniem: 16 — zdania jednostki (wprowadzenie do wyliczenia albo zdanie), 8 — części wspólnej. Eksport wszystkich części wspólnych zamykających wyliczenie przywraca też 8 akapitów cytowanych części wspólnych jednego rozporządzenia, które dotąd ginęły (pokrycie tekstu rozporządzeń: 2 zgubione akapity zamiast 10). Zgodnych ze schematem jest nadal 1937 z 2321 (oba walidatory).
- Wychodzą jak dotąd: objaśnienie symbolu we wzorze w dwóch brzmieniach (4 odnośniki), zdanie uchylane bez drugiej
  kopii („Zdanie drugie uchylone przez …; obowiązuje do …") oraz tytuły jednostek systematyzacyjnych.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0065_SecondWordingInASeparateParagraph_ReturnsToItsFragment`
- `WordingVersionTests` — wprowadzenie w dwóch brzmieniach (model i eksport jednego wyliczenia), zdanie z drugim
  brzmieniem w osobnym akapicie, część wspólna (model i eksport pod jednym adresem), akapit bez takich odnośników,
  zgodność ze schematem.
