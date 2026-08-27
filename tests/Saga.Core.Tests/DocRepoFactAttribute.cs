using System.Runtime.CompilerServices;

namespace Saga.Core.Tests
{
    /// <summary>
    /// [Fact] dla testu opartego na lokalnych, NIEWERSJONOWANYCH danych — dokumentach z DocRepo/
    /// i opcjonalnie goldenie z Artifacts/. Gdy danych nie ma na maszynie, test jest POMIJANY
    /// z powodem, zamiast czerwienić przebieg: brak plików nie jest regresją parsera, a czysty
    /// klon repozytorium nie zawiera żadnego z tych katalogów.
    ///
    /// Powód wyliczany jest w konstruktorze atrybutu, czyli przy DYSKRYWACJI testów — dlatego
    /// xUnit raportuje go jako „[SKIP] &lt;powód&gt;”, a nie jako wyjątek z ciała testu.
    /// Działa tak samo w xUnit v3: Skip pozostaje zwykłą właściwością atrybutu (dodane w v3
    /// Assert.Skip pomija dopiero w trakcie wykonania, więc nie zastąpiłoby tego mechanizmu).
    /// </summary>
    public sealed class DocRepoFactAttribute : FactAttribute
    {
        private string _requiredArtifact = string.Empty;

        /// <param name="fileName">Dokument wymagany w DocRepo/.</param>
        /// <param name="sourceFilePath">Uzupełniane przez kompilator — xUnit v3 pokazuje dzięki
        /// temu miejsce testu w raporcie (reguła xUnit3003).</param>
        /// <param name="sourceLineNumber">Jak wyżej.</param>
        public DocRepoFactAttribute(
            string fileName,
            [CallerFilePath] string? sourceFilePath = null,
            [CallerLineNumber] int sourceLineNumber = -1)
            : base(sourceFilePath, sourceLineNumber)
        {
            ApplyReason(TestFiles.DescribeMissingReferenceDocs(fileName));
        }

        /// <summary>
        /// Dodatkowy golden wymagany w Artifacts/. Sprawdzany wyłącznie poza trybem regeneracji:
        /// UPDATE_SNAPSHOTS=1 uruchamia taki test właśnie po to, żeby golden powstał, więc jego brak
        /// nie może wtedy pomijać testu.
        /// </summary>
        public string RequiredArtifact
        {
            // Getter jest wymagany przez język: nazwany argument atrybutu musi wskazywać właściwość
            // do odczytu i zapisu (CS0617). Sama wartość nie jest nigdzie czytana.
            get => _requiredArtifact;
            set
            {
                _requiredArtifact = value;

                if (Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1")
                {
                    return;
                }

                ApplyReason(TestFiles.DescribeMissingArtifact(value));
            }
        }

        // Pierwszy powód wygrywa: brakujący dokument źródłowy jest bliższy przyczynie niż brakujący
        // golden, który przy braku źródła i tak nie miałby z czego powstać.
        private void ApplyReason(string? reason)
        {
            if (reason != null && Skip == null)
            {
                Skip = reason;
            }
        }
    }
}
