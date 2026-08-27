namespace Saga.Core.Tests
{
    /// <summary>
    /// Wspólne ścieżki plików testowych. Dokumenty referencyjne (DOCX) leżą
    /// w lokalnym, niewersjonowanym DocRepo/; goldeny snapshotowe w lokalnym
    /// Saga.Core.Tests/Artifacts/ (też niewersjonowane — decyzja projektu:
    /// dokumenty robocze i artefakty nie trafiają do repozytorium).
    /// </summary>
    internal static class TestFiles
    {
        /// <summary>
        /// Korzeń projektu testowego (katalog z Saga.Core.Tests.csproj),
        /// odnajdywany od katalogu wyjściowego testów w górę.
        /// </summary>
        public static string GetTestProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Saga.Core.Tests.csproj")))
            {
                dir = dir.Parent;
            }

            if (dir == null)
            {
                throw new DirectoryNotFoundException(
                    "Nie znaleziono katalogu projektu testowego (Saga.Core.Tests.csproj) powyżej katalogu wyjściowego testów.");
            }

            return dir.FullName;
        }

        public static string GetArtifactPath(string fileName)
            => Path.Combine(GetTestProjectRoot(), "Artifacts", fileName);

        /// <summary>
        /// Korzeń repozytorium (katalog z Saga.sln) — potrzebny testom weryfikującym
        /// dokumentację, która odwołuje się do plików w innych projektach.
        /// </summary>
        public static string GetRepositoryRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Saga.sln")))
            {
                dir = dir.Parent;
            }

            if (dir == null)
            {
                throw new DirectoryNotFoundException(
                    "Nie znaleziono korzenia repozytorium (Saga.sln) powyżej katalogu wyjściowego testów.");
            }

            return dir.FullName;
        }

        /// <summary>
        /// Katalog DocRepo/ odnaleziony powyżej katalogu wyjściowego testów, albo null, gdy go nie ma.
        /// Wariant nierzucający: na jego podstawie <see cref="DocRepoFactAttribute"/> decyduje
        /// o pominięciu testu, zanim ten w ogóle wystartuje.
        /// </summary>
        public static string? FindReferenceDocDirectory()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "DocRepo")))
            {
                dir = dir.Parent;
            }

            return dir == null ? null : Path.Combine(dir.FullName, "DocRepo");
        }

        /// <summary>
        /// Ścieżka dokumentu referencyjnego w lokalnym repozytorium dokumentów (DocRepo/).
        /// Rzuca, gdy katalogu brak — to zabezpieczenie na wypadek testu bez [DocRepoFact],
        /// który przy braku danych i tak nie miałby czego sprawdzić.
        /// </summary>
        public static string GetReferenceDocPath(string fileName)
        {
            var docRepo = FindReferenceDocDirectory();
            if (docRepo == null)
            {
                throw new DirectoryNotFoundException(
                    "Nie znaleziono katalogu DocRepo powyżej katalogu testów — dokumenty referencyjne są lokalne (niewersjonowane).");
            }

            return Path.Combine(docRepo, fileName);
        }

        /// <summary>
        /// Powód pominięcia testu opartego na dokumentach z DocRepo/, albo null, gdy komplet jest
        /// na miejscu. Komunikat niesie tę samą informację co dawny wyjątek — czego brakuje
        /// i skąd to wziąć — żeby pominięcie nie zamiatało problemu pod dywan.
        /// </summary>
        public static string? DescribeMissingReferenceDocs(params string[] fileNames)
        {
            var docRepo = FindReferenceDocDirectory();
            if (docRepo == null)
            {
                return "Brak katalogu DocRepo/ powyżej katalogu testów — dokumenty referencyjne są lokalne "
                    + "(niewersjonowane). Przywróć DocRepo/, żeby ten test się wykonał.";
            }

            var missing = new List<string>();
            foreach (var fileName in fileNames)
            {
                if (!File.Exists(Path.Combine(docRepo, fileName)))
                {
                    missing.Add(fileName);
                }
            }

            return missing.Count == 0
                ? null
                : $"Brak niewersjonowanych dokumentów referencyjnych w {docRepo}: {string.Join(", ", missing)}.";
        }

        /// <summary>
        /// Powód pominięcia testu opartego na goldenie z Artifacts/, albo null, gdy golden istnieje.
        /// </summary>
        public static string? DescribeMissingArtifact(string fileName)
        {
            var path = GetArtifactPath(fileName);
            return File.Exists(path)
                ? null
                : $"Brak goldenu {path} — jest lokalny (niewersjonowany). Wygeneruj go przez "
                    + "UPDATE_SNAPSHOTS=1 dotnet test i zrewiduj przed użyciem.";
        }

        /// <summary>
        /// Kopia tymczasowa artefaktu — testy nie dotykają plików w drzewie źródłowym.
        /// </summary>
        public static string CreateTemporaryCopy(string sourcePath)
        {
            var tempPath = Path.Combine(Path.GetTempPath(),
                $"SagaTests_{Guid.NewGuid():N}_{Path.GetFileName(sourcePath)}");
            File.Copy(sourcePath, tempPath, true);
            return tempPath;
        }
    }
}
