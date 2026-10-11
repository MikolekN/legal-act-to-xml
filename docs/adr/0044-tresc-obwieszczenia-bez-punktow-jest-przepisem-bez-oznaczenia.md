# ADR-0044: Treść obwieszczenia bez numerowanych punktów jest jednym przepisem bez oznaczenia, zbudowanym potokiem aktu

- Status: Accepted
- Data: 2026-10-11

## Kontekst

Obwieszczenie, które nie ogłasza aktu ([ADR-0043](0043-obwieszczenie-ktore-nie-oglasza-aktu-jest-korzeniem.md)),
w 108 z 284 dokumentów korpusu ma treść poza samą podstawą prawną: wyliczenie po wprowadzeniu („Na podstawie
art. … ogłasza się następujące stawki:" i „1) …", z literami i tiretami — 500 punktów, 158 liter, 42 tirety),
dalsze akapity, tabelę przed podpisem. Numerowanych punktów („1. …"), jakie ma obwieszczenie tekstu
jednolitego, takie obwieszczenie nie ma (jeden dokument korpusu).

`AnnouncementUnits` zna tylko punkt („N.") i podpunkt („N)") obwieszczenia tekstu jednolitego; treść bez
punktu trafiała do `Announcement.Content` i nie wychodziła w eksporcie.

Schemat pod `obwieszczenie` przewiduje: `podstawa-prawna` (sam tekst), `punkt-OTJ` — z treścią tylko razem
z numerem — oraz `artykul`, `paragraf` i ich warianty spoza ZTP. `nie-ztp-artykul` przyjmuje pusty `nr`,
akapit, dalszą treść i jednostki od punktu w dół, wyłącznie w wariancie `nie-ztp-*`.

## Decyzja

**Treść obwieszczenia bez numerowanych punktów jest jednym przepisem bez oznaczenia: `Announcement.Body`
(ustęp modelu z punktami, literami, tiretami, częścią wspólną i dalszą treścią), w eksporcie
`nie-ztp-artykul` z `brak-eid="jednostka-nieoznaczona"`. Buduje go potok aktu pod artykułem zastępczym
(`AnnouncementBody`), tak jak jednostki załącznika.**

- **Tekst przepisu** to pierwszy akapit treści. Podstawa prawna zostaje osobno (`podstawa-prawna`), gdy za nią
  stoi zwykły akapit albo nic; gdy stoi za nią pozycja wyliczenia albo tabela, jest wprowadzeniem do nich
  i sama jest tekstem przepisu — `podstawa-prawna` wtedy nie wychodzi.
- **Jednostki** pod przepisem są zwykłymi punktami, literami i tiretami modelu; adresują się od obwieszczenia
  (`MP/2025/702__pkt_1__lit_a`), bo przepis bez oznaczenia jest adresowo przezroczysty (ADR-0024).
- **Numerowane punkty** („1. …") zostają jednostkami obwieszczenia (`punkt-OTJ`, ADR-0032).
- Gdy potok nie złoży z treści jednego przepisu (pominięty akapit, wejście w nowelizację, treść zaczynająca się
  od pozycji wyliczenia), zostaje dotychczasowa droga — treść w `Announcement.Content`, bez eksportu.

## Odrzucone alternatywy

- **`punkt-OTJ` dla pozycji „1)"** (podpunkty obwieszczenia jako jego punkty). Zgodne ze schematem, ale „a)"
  pod takim punktem musiałoby być `podpunkt-OTJ`, a tiret nie miałby miejsca; nazwy jednostek przestałyby
  odpowiadać ich oznaczeniom.
- **`podstawa-prawna` zawsze, a wyliczenie w przepisie bez tekstu.** `nie-ztp-artykul` wymaga akapitu ze
  zdaniem; pusty akapit byłby atrapą, a wprowadzenie oderwane od swoich punktów.
- **Rozbudowa `AnnouncementUnits`** o litery, tirety, części wspólne i tabele. Druga implementacja tego, co
  potok aktu już robi (i co nadal jest tam poprawiane) — zaległość „litery pod podpunktem" pokazuje koszt.
- **Każdy akapit treści osobnym przepisem.** Wyliczenie po akapicie „Informuję, że … stosuje się:" należy do
  tego samego wywodu co akapity wokół; schemat i tak nie daje takim przepisom adresu, który by je rozróżniał.

## Konsekwencje

- W korpusie: 108 obwieszczeń ma przepis bez oznaczenia (44 także `podstawa-prawna`), z 500 punktami, 158
  literami i 42 tiretami. Pokrycie tekstu obwieszczeń 83 % → 99 % (zgubionych akapitów 1542 → 90 w 5
  dokumentach). Zgodność ze schematem bez zmian (1915 z 2321) — wszystkie nowe przepisy są zgodne.
- Liczba jednostek `nie-ztp-*` rośnie z 600 do 1408: schemat nie przyjmuje pod przepisem bez oznaczenia
  jednostek zwykłych. Przyrost wynika z budowy dokumentu, nie z usterki odczytu; miary jakości trzeba czytać
  z tym zastrzeżeniem.
- Wiersz „Załączniki do obwieszczenia …" (liczba mnoga, także z numerem) otwiera załączniki — przedtem tabele
  takiego obwieszczenia trafiały do jego treści i ginęły z całym przepisem (6 dokumentów).
- Poza zakresem: obwieszczenie o sprostowaniu błędów z cytatami wieloakapitowymi (1 dokument — potok
  wchodzi w nowelizację), treść bez punktów w obwieszczeniu tekstu jednolitego (`Announcement.Content` nadal
  bez eksportu) i obwieszczenia zbudowane z paragrafów (4 dokumenty).

## Weryfikacja

- `ArchitectureDecisionTests.Adr0044_AnnouncementContentWithoutPoints_IsOneProvisionWithoutADesignation`
- `PlainAnnouncementTests` — wprowadzenie z wyliczeniem, akapity po podstawie prawnej, numerowane punkty,
  załączniki w liczbie mnogiej, eksport i zgodność ze schematem.
