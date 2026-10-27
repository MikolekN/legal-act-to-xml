# ADR-0128: Artykuł, którego jedyny ustęp ma oznaczenie, to `nie-ztp-artykul` z `nie-ztp-ustep` bez akapitu artykułu

- Status: Accepted
- Data: 2026-10-28

## Kontekst

ZTP nie numeruje jedynego ustępu artykułu. Zdarza się jednak artykuł „Art. 41a. 1. Zabieg …: 1) … 2) …” bez „2.”
(Dz. U. 2025 poz. 1709, art. 4 pkt 15), a ustawa powołuje go potem jako „art. 41a ust. 1”. `ArtykulUstawyTyp`
przyjmuje albo co najmniej dwa ustępy, albo jeden `ustep-n` bez numeru, więc eksport dawał `ustep-n` z
`brak-eid="jednostka-nieoznaczona"`: numer „1.” ginął, a punkty i tak miały w adresie `…__art_41a__ust_1__pkt_1`.
`nie-ztp-artykul` przyjmował jeden numerowany `nie-ztp-ustep`, ale wymagał przed nim własnego akapitu artykułu, którego
tu nie ma. Autor schematu, zapytany o ten przypadek, uczynił tekst jednostek nie-ZTP opcjonalnym — w schemaPL 0.3.6
(2026-10-28) `akapit-dowolny` ma `minOccurs="0"` przed jednostkami niższego poziomu, więc `nie-ztp-artykul` może
składać się z samych jednostek (wersja 0.3.5 z wyborem zamiast sekwencji odebrała zgodność jednostkom z tekstem
i wyliczeniem — zgłoszone autorowi i wycofane).

## Decyzja

- Artykuł z oznaczeniem, którego jedyny ustęp ma oznaczenie, wychodzi jako `nie-ztp-artykul` bez własnego `p`, a
  ustęp jako `nie-ztp-ustep` z `nr` i adresem `…__art_N__ust_M` (`ParagraphXmlConverter.IsLoneDesignated`,
  `ArticleXmlConverter`). Jednostki pod nim są `nie-ztp-*` — `nie-ztp-ustep` przyjmuje tylko takie.
- Tak samo jedyny paragraf kodeksu („Art. 5. § 1. …” bez „§ 2.”): `nie-ztp-paragraf-kodeksu` pod `nie-ztp-artykul`.
- Reguła nie dotyczy `§` rozporządzenia (schemat nie ma `nie-ztp-paragraf`), artykułu bez oznaczenia (pojemnik treści
  załącznika lub obwieszczenia) ani ustępu przytoczonego bez artykułu
  ([ADR-0113](0113-przytoczona-czesc-artykulu-stoi-we-fragmencie-zdania-z-oznaczeniem-artykulu.md)).

## Odrzucone alternatywy

- **`ustep-n` bez numeru** (dotychczas). Gubi „1.” z tekstu aktu, a ustęp nie ma adresu, choć jego punkty go mają.
- **Tekst ustępu jako akapit `nie-ztp-artykul`, punkty wprost pod artykułem.** Zgodne ze schematem przed zmianą, ale
  też gubi „1.”, a punkty dostają adres `…__art_41a__pkt_1`, choć ustawa powołuje „art. 41a ust. 1”.
- **Puste zdanie zastępcze jako akapit artykułu** — zgodne przed zmianą, ale to treść, której w akcie nie ma.

## Konsekwencje

- Korpus DOCX: 5 artykułów w 5 plikach (351456 — przypadek z pytania; 320912 i 358454 — art. 22a z punktami;
  348320 — art. 53d; 307511 — art. 21). W 307511 ust. 2 ma w dokumencie numer sklejony z tekstem („2.W przypadku …”,
  jedyny taki akapit stylu ustępu w korpusie) i zostaje dalszą treścią ustępu 1 — wada dokumentu, ust. 2 ginął też
  wcześniej. 171 aktów ISAP — bez zmian.
- Zapis wymaga schemaPL 0.3.6; z nią wszystkie testy przechodzą, a plik 351456 jest zgodny ze schematem.

## Weryfikacja

- `ArchitectureDecisionTests.Adr0128_ArticleWhoseOnlyParagraphIsNumbered_IsANonZtpArticleWithANumberedParagraph`
