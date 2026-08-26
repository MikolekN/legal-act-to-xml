using System;
using Xunit;

namespace Saga.Core.Tests
{
	/// <summary>
	/// [Fact] dla testu opartego na lokalnych, NIEWERSJONOWANYCH danych — dokumentach z DocRepo/
	/// i opcjonalnie goldenie z Artifacts/. Gdy danych nie ma na maszynie, test jest POMIJANY
	/// z powodem, zamiast czerwienić przebieg: brak plików nie jest regresją parsera, a czysty
	/// klon repozytorium nie zawiera żadnego z tych katalogów.
	///
	/// Powód wyliczany jest w konstruktorze atrybutu, czyli przy DYSKRYWACJI testów — dlatego
	/// xUnit raportuje go jako „[SKIP] &lt;powód&gt;”, a nie jako wyjątek z ciała testu. Wymaga to
	/// xUnit 2.x, gdzie Skip jest zwykłą właściwością atrybutu (Assert.Skip pojawia się dopiero w 3.x).
	/// </summary>
	public sealed class DocRepoFactAttribute : FactAttribute
	{
		private string _requiredArtifact = string.Empty;

		public DocRepoFactAttribute(params string[] fileNames)
		{
			ApplyReason(TestFiles.DescribeMissingReferenceDocs(fileNames));
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
