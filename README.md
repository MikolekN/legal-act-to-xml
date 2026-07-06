# WordParser

Zestaw narzędzi .NET 10 do parsowania polskich dokumentów prawnych z formatu DOCX do hierarchicznego modelu obiektowego oraz eksportu do XML/XLSX.

## Projekty

### ModelDto
Czyste DTO bez logiki biznesowej. Definiuje hierarchię encji dokumentu prawnego: jednostki redakcyjne (`Article`, `Paragraph`, `Point`, `Letter`, `Tiret`) oraz jednostki systematyzacyjne (`Part`, `Book`, `Title`, `Division`, `Chapter`, `Subchapter`). Zawiera też modele nowelizacji (`Amendment`) i komunikatów walidacji.

### WordParserCore
Silnik parsowania. Przetwarza pliki DOCX (via OpenXml) na model obiektowy z `ModelDto`. Kluczowe komponenty: wielowarstwowy klasyfikator akapitów (`ParagraphClassifier`), buildery encji (wzorzec kaskadowy), obsługa nowelizacji (`AmendmentCollector`, `AmendmentFinalizer`) oraz konwertery do XML/XLSX.

### WordParserCore.Tests
Testy jednostkowe i integracyjne (xUnit). Pokrywa klasyfikację akapitów, generowanie eId, parsowanie nowelizacji, numerowanie encji i konwersję do XML.

### WordParser
Cienka nakładka CLI. Przyjmuje ścieżkę do pliku DOCX i wywołuje `WordParserCore`, zapisując wynik do XML/XLSX.

### WordParserWeb
Interfejs webowy. Renderuje sparsowany dokument jako stronę HTML z nawigacją po strukturze aktu prawnego.

## Szybki start

```bash
# Budowanie
dotnet build WordParserCore/WordParserCore.csproj
dotnet build WordParser/WordParser.csproj

# Testy
dotnet test WordParserCore.Tests/WordParserCore.Tests.csproj

# Uruchomienie CLI
dotnet run --project WordParser --docx <ścieżka-do-pliku.docx> --xml

# Uruchomienie WordParserWeb
dotnet run --project WordParserWeb
```
