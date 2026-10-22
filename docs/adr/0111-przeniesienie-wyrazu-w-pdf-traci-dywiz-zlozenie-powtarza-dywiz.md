# ADR-0111: Dywiz na końcu wiersza PDF przed małą literą to przeniesienie wyrazu i znika; złożenie poznaje się po dywizie powtórzonym na początku następnego wiersza

- Status: Accepted (zmienia decyzję użytkownika z 2026-07-19 zapisaną w komentarzu `PdfBlockReader`: „dywiz zostaje")
- Data: 2026-10-22

## Kontekst

Adapter PDF sklejał wiersz zakończony dywizem z następnym bezpośrednio i dywiz zostawiał. Decyzja z 2026-07-19 brzmiała:
bez słownika nie da się odróżnić dywizu złożenia („społeczno-gospodarczy”) od przeniesienia wyrazu („posta-nowienia”),
a usunięcie dywizu złożenia jest nieodwracalne — artefakt widoczny jest mniejszą szkodą. Weryfikacja na aktach
urzędowych (2026-10-22, docs/internal/weryfikacja-isap.md) pokazała skalę artefaktu: każdy z 17 sprawdzonych aktów
z Dz. U. i M.P. ma przeniesienia wyrazów, do 14–19 % wierszy („prak-tyczne”, „zanieczysz-czenia” — 288 w jednym
44-stronicowym akcie), a PDF-y korpusu złożone z DOCX — do 5 %. Żadne z tych miejsc nie było złożeniem. Skład Dziennika
Ustaw i Monitora Polskiego dzieli złożenie na jego dywizie inaczej niż wyraz: powtarza dywiz na początku następnego
wiersza („mieszczan-” / „-rolników”); to samo robi LibreOffice przy łączniku nierozdzielającym („metylo-” /
„‑tert‑butylowym”, korpus 318581). Użytkownik zdecydował 2026-10-22 o naprawie.

## Decyzja

W sklejaniu wierszy akapitu (`PdfHyphenation.Join`), gdy wiersz kończy się dywizem (albo miękkim dywizem U+00AD)
po literze:

- następny wiersz zaczyna się dywizem przed literą → złożenie: zostaje jeden dywiz („metylo-tert-butylowy”);
- następny wiersz zaczyna się spójnikiem („i”, „lub”, „albo”, „oraz”, „czy”, „bądź”, „ani”, „do”) → dywiz zawieszony:
  dywiz zostaje z odstępem („Tele- i Radiokomunikacji”);
- następny wiersz zaczyna się małą literą → przeniesienie wyrazu: dywiz znika („praktyczne”), chyba że dokument
  świadczy o złożeniu: człon przed dywizem stoi gdzie indziej w środku wiersza przed dywizem złożenia
  („kolejowo-drogowe”), a wyrazu sklejonego („kolejowodrogowy”) dokument nie zna — wtedy dywiz zostaje;
- w pozostałych przypadkach (wielka litera, cyfra) dywiz zostaje bez odstępu („Warszawa-Praga”, „COVID-19”).

Świadectwo dokumentu obsługuje PDF spoza konwencji. PDF-y korpusu złożone przez LibreOffice dzielą złożenie
bez powtórzenia dywizu; sama konwencja skleiła w nich 131 złożeń w 73 dokumentach („kolejowodrogowego”,
„oszczędnościowokredytowych”) przy 16 164 poprawnie sklejonych przeniesieniach (ocena tekstem DOCX).

## Odrzucone alternatywy

- **Zostawić dywiz (decyzja z 2026-07-19).** Artefakt psuje kilkanaście procent wierszy aktu urzędowego: wyrazy
  w zdaniach, w instrukcjach zmiany wyrazów (`stary-tekst` nie trafi w akt zmieniany) i w tytułach.
- **Słownik wyrazów (np. zbudowany z `documents-txt`).** Rozstrzyga też PDF-y bez tej konwencji, ale wnosi do parsera
  zasób, który trzeba utrzymywać, i myli się na wyrazach spoza słownika; konwencja składu rozstrzyga bez niego,
  a słownikiem dla reszty jest sam dokument.
- **Sama konwencja, bez świadectwa dokumentu.** Prostsza, ale w PDF-ie z edytora gubi dywiz każdego złożenia
  przeciętego na końcu wiersza.
- **Oddać rozstrzygnięcie `pdftotext`.** Inny silnik ekstrakcji, bez geometrii, której adapter potrzebuje do bloków,
  tabel i przypisów.

## Konsekwencje

- PDF spoza tej konwencji dostanie złożenie dzielone na końcu wiersza sklejone bez dywizu („społecznogospodarczy”),
  gdy jego pierwszy człon nie stoi nigdzie indziej w środku wiersza przed dywizem. To koszt przyjęty świadomie.
- Wyraz, którego przeniesienie wypada dokładnie za członem złożenia znanym z dokumentu („samo-” / „chodu” przy
  „samo-pomocy”), zostaje z dywizem, jeśli dokument nie zna wyrazu sklejonego.
- Przeniesienie przed wielką literą („Rzecz-” / „Pospolitej”) zostaje z dywizem — skład go tak nie dzieli, a nazwa
  złożona z wielką literą po dywizie jest częsta.
- Komórki tabel i strefa przypisów składają się tą samą regułą.

## Weryfikacja

- `PdfBlockReaderTests.TrailingHyphen_IsAWordBreak_UnlessRepeatedOnTheNextLine` — przeniesienie, złożenie z dywizem
  powtórzonym i cyfra po dywizie; spadnie, gdy dywiz przeniesienia wróci albo złożenie straci dywiz.
- `PdfBlockReaderTests.TrailingHyphen_BeforeLowercase_KeepsACompoundTheDocumentWritesWhole` i
  `TrailingHyphen_BeforeLowercase_JoinsAWordTheDocumentWritesWhole_EvenAfterACompoundHead` — świadectwo dokumentu.
- `PdfBlockReaderTests.SuspendedHyphen_AtTheLineEnd_KeepsItsSpace`, `TrailingHyphen_BeforeUppercase_JoinsCompoundName`
  i `TrailingHyphen_FollowedByASpaceGlyph_IsJoinedDirectly` (teraz „motorowerów”).
