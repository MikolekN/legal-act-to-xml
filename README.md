# SAGA — System Automatycznego Generowania Aktów

Zestaw narzędzi .NET 10 do parsowania polskich dokumentów prawnych z formatu DOCX do hierarchicznego modelu obiektowego oraz eksportu do XML.

## Projekty

### Saga.Model
Czyste DTO bez logiki biznesowej. Definiuje hierarchię encji dokumentu prawnego: jednostki redakcyjne (`Article`, `Paragraph`, `Point`, `Letter`, `Tiret`) oraz jednostki systematyzacyjne (`Part`, `Book`, `Title`, `Division`, `Chapter`, `Subchapter`). Zawiera też modele nowelizacji (`Amendment`) i komunikatów walidacji.

### Saga.Core
Silnik parsowania. Przetwarza pliki DOCX (via OpenXml) na model obiektowy z `Saga.Model`. Kluczowe komponenty: klasyfikator dokumentu (`DocumentClassifier`, rozpoznaje, czy plik jest aktem prawnym i jakiego rodzaju), wielowarstwowy klasyfikator akapitów (`ParagraphClassifier`), buildery encji (wzorzec kaskadowy), obsługa nowelizacji (`AmendmentCollector`, `AmendmentFinalizer`) oraz konwertery do XML.

### Saga.Core.Tests
Testy jednostkowe i integracyjne (xUnit). Pokrywa klasyfikację akapitów, klasyfikację dokumentu, wykrywanie formatu (DOCX/PDF/TXT), generowanie eId, parsowanie nowelizacji, numerowanie encji i konwersję do XML.

### Saga.Cli
Konwerter wsadowy; plik wykonywalny nazywa się `saga`. Przyjmuje pojedynczy plik albo katalog
i zapisuje XML aktu prawnego dla każdego rozpoznanego dokumentu. Dokumenty, których klasyfikator
nie uzna za akt prawny, są pomijane i wykazane w podsumowaniu przebiegu.

### Saga.Web
Interfejs webowy. Przyjmuje upload wieloformatowy DOCX/PDF/TXT (limit 64MB). Gdy dokument nie jest aktem prawnym — wyświetla widok raportu klasyfikacji z opcją „parsuj mimo wszystko"; gdy jest aktem — renderuje stronę HTML z nawigacją po strukturze aktu prawnego.

## Szybki start

```bash
# Budowanie
dotnet build src/Saga.Core/Saga.Core.csproj
dotnet build src/Saga.Cli/Saga.Cli.csproj

# Testy
dotnet test tests/Saga.Core.Tests/Saga.Core.Tests.csproj

# Konwersja pojedynczego pliku do XML
dotnet run --project src/Saga.Cli -- plik.docx --output wynik

# Konwersja wsadowa całego katalogu (domyślnie *.docx, bez rekursji)
dotnet run --project src/Saga.Cli -- katalog-z-ustawami --output katalog-xml

# Inny wzorzec plików (np. PDF albo TXT)
dotnet run --project src/Saga.Cli -- katalog --pattern "*.pdf" --output katalog-xml
```
