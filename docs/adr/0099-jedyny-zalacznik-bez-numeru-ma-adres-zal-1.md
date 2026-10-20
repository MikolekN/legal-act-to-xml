# ADR-0099: Jedyny załącznik bez numeru ma adres `zal_1` — własny, w celach komend i w instrukcjach odesłania

- Status: Accepted (Supersedes w części ADR-0024, ADR-0028, ADR-0058 i ADR-0080: załącznik bez numeru)
- Data: 2026-10-20

## Kontekst

Akt z jednym załącznikiem drukuje go bez numeru: „Załącznik do rozporządzenia Ministra … z dnia …". Komendy aktu
zmieniającego mówią o nim tak samo: „w załączniku do rozporządzenia po pkt 4 dodaje się pkt 4a", „załącznik do
rozporządzenia otrzymuje brzmienie określone w załączniku do niniejszego rozporządzenia".

`EIdTyp` odrzuca samo `zal`, więc dotąd taki załącznik nie miał adresu
([ADR-0024](0024-jednostka-bez-oznaczenia-nie-ma-eid.md) — `brak-eid`), jego treść zostawała akapitami
([ADR-0028](0028-wyliczenie-w-zalaczniku-buduje-potok-aktu.md)), odesłanie nie dawało instrukcji
([ADR-0058](0058-zalacznik-zmieniany-odeslaniem-jest-instrukcja-z-adresami-obu-zalacznikow.md) wymaga numeru), a cel
komendy w nim — ani on, ani jednostki pod nim — był nieznany
([ADR-0080](0080-komenda-w-zalaczniku-wskazuje-jednostki-zalacznika.md)). W korpusie DOCX: 435 dokumentów z jedynym
załącznikiem bez numeru, 28 komend odesłania i 224 instrukcje wewnątrz takich załączników w 25 dokumentach.

Autorzy schematu (odpowiedź z 2026-10-18 na pytanie 8, `docs/internal/odpowiedzi-autorow-schematu.md`): `zal_1`. Skoro
komenda zakłada, że załącznik jest jedyny, można też założyć, że jest pierwszy; załącznik dodany później dostanie nr 2,
więc kolizji nie będzie. Jeśli załącznik nie jest jedyny, błąd leży w dokumencie.

## Decyzja

**Jedyny załącznik aktu wydrukowany bez numeru ma adres `zal_1`. Ten sam adres daje załącznik bez numeru w celu komendy
(„w załączniku do rozporządzenia") i w odesłaniu po obu stronach („załącznik do rozporządzenia … w załączniku do
niniejszego rozporządzenia").**

- Własny załącznik: `Annex.IsOnly` (ustawia `AnnexBuilder`, gdy akt ma dokładnie jeden załącznik, a ten nie ma numeru);
  `Number` zostaje pusty, bo numeru w akcie nie ma — eksport nie pisze `zalacznik-nr`. Wyliczenie w jego treści buduje
  potok aktu z adresami `zal_1__pkt_1` (ADR-0028).
- Cel komendy: załącznik bez numeru to w ścieżce adresu numer „1" (`UnitAddressReader`); jednostki pod nim adresuje się
  pod `zal_1`, jak pod załącznikiem z numerem.
- Odesłanie: „załącznik" i „w załączniku" w liczbie pojedynczej bez numeru to „1" po swojej stronie
  (`AnnexInstructionReader`).
- Załącznik bez numeru obok innych załączników nadal nie ma adresu (`brak-eid`) — akt go nie daje, a numeru nie
  zgadujemy.

## Odrzucone alternatywy

- **Bez adresu** (stan sprzed decyzji). Komenda zmieniająca jedyny załącznik nie dawała instrukcji ani celu, a jego
  treść nie miała jednostek.
- **Samo `zal`.** `EIdTyp` wymaga oznaczenia w segmencie.
- **Numer z kolejności także obok innych załączników.** Akt z kilkoma załącznikami numeruje je; brak numeru przy jednym
  z nich to błąd dokumentu albo odczytu — nadany numer mógłby wskazać inny załącznik.

## Konsekwencje

- Jedyny załącznik bez numeru ma `eId` (`DU/2024/20__zal_1`) zamiast `brak-eid`, a jego wyliczenie — jednostki pod nim.
- Część załącznika bez adresu („w tabeli", „lp. 7") nadal zostaje celem bez adresu (ADR-0080) — to osobna decyzja
  autorów schematu (instrukcje `*-recznie`).

## Weryfikacja

`ArchitectureDecisionTests.Adr0099_OnlyAnnexWithoutANumber_IsZal1_ForItselfTargetsAndInstructions`;
`AnnexTests` — jedyny załącznik bez numeru i załącznik bez numeru obok innych, wyliczenie w obu;
`AnnexCommandTargetTests.AnnexWithoutNumber_IsTheOnlyAnnex_AddressedAsTheFirst`;
`AnnexInstructionTests.ReplacedAnnexWithoutANumber_IsTheOnlyAnnex_OnBothSides`.
