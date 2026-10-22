# ADR-0075: Cudzysłów otwarty za oznaczeniem artykułu otwiera przytoczenie ustępu tego artykułu

- Status: Accepted — Superseded in part by [ADR-0113](0113-przytoczona-czesc-artykulu-stoi-we-fragmencie-zdania-z-oznaczeniem-artykulu.md) (budowa przytoczenia: oznaczenie artykułu poza cudzysłowem, same ustępy)
- Data: 2026-10-14

## Kontekst

Obwieszczenie tekstu jednolitego przytacza przepisy, których tekst jednolity nie obejmuje
([ADR-0036](0036-przytoczone-artykuly-sa-jednostkami-aktu-przytaczanego.md)). Przytoczenie rozpoznaje się po akapicie zaczynającym się cudzysłowem
(„„Art. 57. …"). Gdy przytoczony jest tylko ustęp, oznaczenie artykułu stoi przed cudzysłowem:
„1) art. 78 ust. 1 ustawy …, który stanowi:" i dalej „Art. 78. „1. Uznaje się, że …:".

Taki akapit nie zaczynał przytoczenia: zostawał tekstem, a jednostki przytoczonego ustępu wchodziły do obwieszczenia —
punkty ustępu stawały się podpunktami obwieszczenia, kolejny ustęp („4. Przepis art. 48 …") jego punktem, z adresami
powtarzającymi adresy obwieszczenia.

## Decyzja

**Akapit, w którym cudzysłów otwiera się tuż za oznaczeniem artykułu albo paragrafu („Art. 78. „1. …",
„§ 5. „2. …"), zaczyna przytoczenie i czyta się tak, jakby cudzysłów stał przed oznaczeniem** — przytoczony ustęp
należy do swojego artykułu (`AnnouncementUnits.Build`). Przytoczenie biegnie do domknięcia cudzysłowu i buduje się
jak każde inne: jednostki adresowane w akcie przytaczanym (`DU/2024/858__art_78__ust_1__pkt_1`).

## Odrzucone alternatywy

- **Przytoczony ustęp bez artykułu.** Adres ustępu bez artykułu nie wskazuje niczego w akcie przytaczanym; oznaczenie
  artykułu stoi w akapicie i rozstrzyga.
- **Zostawić tekstem.** Tekst zostawał, ale punkty ustępu i tak wchodziły do obwieszczenia jako jego podpunkty —
  fałszywa struktura obwieszczenia.

## Konsekwencje

- W korpusie zmienia się 45 tekstów jednolitych: przybywa 62 przytoczeń zbudowanych z jednostek (`tresc-strukt`), znika 75 akapitów zostawionych tekstem (`fragment`), 73 fałszywe podpunkty i 16 fałszywych punktów obwieszczenia (punkty i ustępy przytoczonego przepisu brane za jednostki obwieszczenia). Powtórzonych eId w tekstach jednolitych jest 559 (było 594). Zgodnych ze schematem jest nadal 2278 z 2321 (oba walidatory), pokrycie tekstu bez zmian.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0075_QuoteOpenedAfterTheArticleDesignation_QuotesAParagraphOfThatArticle`
- `AnnouncementTests.QuoteOpenedAfterTheArticleDesignation_IsAQuotationOfThatArticle`
