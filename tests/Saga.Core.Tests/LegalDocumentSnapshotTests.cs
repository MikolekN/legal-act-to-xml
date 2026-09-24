using Saga.Core.Services.Converters;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Testy snapshotowe (golden) pełnego modelu aktu referencyjnego.
    /// Siatka bezpieczeństwa dla przebudowy potoku: każda zmiana zachowania parsera
    /// na ścieżce DOCX+szablon musi być widoczna jako diff snapshotu.
    /// Dokument źródłowy (DocRepo/) i golden (Artifacts/) są lokalne, niewersjonowane.
    ///
    /// Regeneracja goldenu (po świadomej, udokumentowanej zmianie zachowania):
    ///   UPDATE_SNAPSHOTS=1 dotnet test --project tests/Saga.Core.Tests/Saga.Core.Tests.csproj --filter-class "*LegalDocumentSnapshotTests"
    /// </summary>
    public class LegalDocumentSnapshotTests
    {
        private static string ParseReferenceActToCanonicalXml()
        {
            var tempPath = TestFiles.CreateTemporaryCopy(TestFiles.GetReferenceDocPath(TestFiles.ReferenceActFileName));
            try
            {
                // AlwaysParse: siatka regresyjna PARSERA ma być niezależna od progów klasyfikatora —
                // zmiana punktacji klasyfikacji nie może wyzerować snapshotu (Document = null).
                var result = LegalDocumentParser.Parse(tempPath, new ParseOptions { Policy = ParsePolicy.AlwaysParse });
                return CanonicalDtoXmlSerializer.Serialize(result.Document!);
            }
            finally
            {
                File.Delete(tempPath);
            }
        }

        [DocRepoFact(TestFiles.ReferenceActFileName, RequiredArtifact = TestFiles.ReferenceActSnapshotFileName)]
        public void ReferenceAct_CanonicalXml_MatchesSnapshot()
        {
            var actual = ParseReferenceActToCanonicalXml();
            var snapshotPath = TestFiles.GetArtifactPath(TestFiles.ReferenceActSnapshotFileName);

            if (Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
                File.WriteAllText(snapshotPath, actual);
                return;
            }

            Assert.True(File.Exists(snapshotPath),
                $"Brak pliku snapshotu: {snapshotPath}. Wygeneruj go przez UPDATE_SNAPSHOTS=1 i zrewiduj przed commitem.");

            // Normalizacja końców linii: golden mógł przejść przez konwersję EOL (git/edytor),
            // a serializer emituje wyłącznie '\n'.
            var expected = File.ReadAllText(snapshotPath).Replace("\r\n", "\n");
            Assert.Equal(expected, actual);
        }

        [DocRepoFact(TestFiles.ReferenceActFileName)]
        public void ReferenceAct_Parse_IsDeterministic()
        {
            var first = ParseReferenceActToCanonicalXml();
            var second = ParseReferenceActToCanonicalXml();

            Assert.Equal(first, second);
        }
    }
}
