# ADR-0086: Część wspólna bez wyliczenia i bez komendy, która ją nazywa, jest tekstem cytowanym

- Status: Accepted — Superseded in part by [ADR-0124](0124-zdanie-i-wprowadzenie-do-wyliczenia-przytoczone-w-calosci-sa-zdaniem.md) (wprowadzenie do wyliczenia nie nazywa części wspólnej)
- Data: 2026-10-14

## Kontekst

Szablon nie ma stylu dla każdej treści cytowanej. Podpisy rysunków („Rys. 6.3.1.8. Tablica E-1a …"), tytuły tabel
(„Tabela 1a. Dla funkcjonariusza …"), pozycje efektów kształcenia („B.W15. wybrane zagadnienia …") i objaśnienia
wzorów redaktorzy składają stylem zmiany części wspólnej (`Z/CZ_WSP_PKT` i pochodne). Budowa treści cytowanej
(`AmendmentBuilder`) czyniła z nich części wspólne, a bez wyliczenia przed nimi — części wspólne samej treści cytowanej:
eksport dawał osobny `cytat-strukt` na każdy akapit (cudzysłów otwierający w pierwszym, zamykający w ostatnim) z adresem
`…__czw`, który nie wskazuje niczego. W korpusie 185 takich cytatów w 16 dokumentach; bez stylów te same akapity są
tekstem.

## Decyzja

**Treść cytowana złożona z samych części wspólnych — bez jednostek, bez tekstu i bez dalszej treści — pod komendą, która
części wspólnej nie nazywa („część wspólna", „wprowadzenie do wyliczenia"), jest tekstem cytowanym
(`AmendmentBuilder.DemoteLoneCommonParts`, wołane przy nowelizacji i nowelizacji zagnieżdżonej).** Część wspólna
nazwana komendą („w ust. 2 część wspólna otrzymuje brzmienie:") zostaje częścią wspólną.

## Odrzucone alternatywy

- **Styl rozstrzyga** (stan dotychczasowy). Część wspólna bez wyliczenia nie ma czego wieńczyć; sam styl bez
  potwierdzenia w treści i w komendzie przegrywa (zasada klasyfikacji warstwowej, jak w
  [ADR-0085](0085-otwarty-cudzyslow-cytatu-przewaza-nad-stylem-ustawy-matki.md)).
- **Rodzaj treści z rzeczownika komendy** („rysunek", „tytuł tabeli" → tekst), jak zdanie i akapit w
  [ADR-0079](0079-zdanie-albo-akapit-jako-przedmiot-komendy-czyni-tresc-cytowana-tekstem.md). Rzeczowników jest wiele i są otwarte („pozycja", „wiersz", „efekt"), a sygnał
  „nie ma wyliczenia i komenda nie mówi o części wspólnej" jest jeden i nie zależy od słownika.

## Konsekwencje

- W korpusie zmienia się 7 dokumentów: 121 części wspólnych bez wyliczenia (podpisy rysunków, tytuły tabel, pozycje efektów kształcenia „B.W15.") przestaje wychodzić osobnymi cytatami `cytat-strukt` — 54 cytaty tekstu zamiast 121 cytatów strukturalnych; w jednym tekście jednolitym znika fałszywa nowelizacja aktu przez samego siebie (część wspólna w stylu zmiany zostaje dalszą treścią punktu). Zgodnych ze schematem nadal 2279 z 2321 (oba walidatory, ten sam zbiór); pokrycie tekstu bez zmian. Zgodność bez stylów rozporządzeń 98,64 % → 99,04 % jednostek (brakujących 396 → 276; w pełni zgodnych 1013 → 1015 dokumentów).
- Tekst cytowany wychodzi jednym cytatem z oboma cudzysłowami; akapit z samym cudzysłowem (miejsce rysunku) nie
  zostawia pustej części wspólnej.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0086_CommonPartStyleWithoutAnEnumeration_IsQuotedText_UnlessTheCommandNamesACommonPart`
- `AmendmentBuilderTests.LoneCommonParts_UnderACommandNotNamingThem_AreQuotedText`,
  `AmendmentBuilderTests.CommonParts_NamedByTheCommand_StayCommonParts`.
