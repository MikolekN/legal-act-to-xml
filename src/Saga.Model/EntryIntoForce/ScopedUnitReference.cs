using System.Collections.Generic;

#nullable enable

namespace Saga.Model.EntryIntoForce
{
	/// <summary>
	/// Rodzaj zakresu wyjątku („w zakresie", „w odniesieniu do", „w stosunku do").
	/// </summary>
	public enum ScopeKind
	{
		/// <summary>Brak zakresu — wyjątek obejmuje całą wskazaną jednostkę.</summary>
		None,

		/// <summary>Zakres wskazuje jednostkę redakcyjną („w zakresie art. 18b ust. 2") — dało się go rozłożyć.</summary>
		Structural,

		/// <summary>
		/// Zakres opisany prozą („w zakresie, w jakim dotyczy dziesięcioletniego planu rozwoju…").
		/// </summary>
		Prose,
	}

	/// <summary>
	/// Cel wyjątku wejścia w życie: ścieżka jednostki redakcyjnej aktu i opcjonalny zakres.
	/// </summary>
	public class ScopedUnitReference
	{
		/// <summary>Ścieżka jednostki redakcyjnej będącej celem wyjątku (art. → ust. → pkt → lit. → tiret → zd.).</summary>
		public StructuralReference Structure { get; set; } = new();

		/// <summary>Rodzaj zakresu zawężającego cel.</summary>
		public ScopeKind ScopeKind { get; set; } = ScopeKind.None;

		/// <summary>
		/// Jednostki wskazane w zakresie, gdy dało się je rozłożyć strukturalnie.
		/// </summary>
		public List<StructuralReference> ScopeStructures { get; set; } = new();

		/// <summary>
		/// Imiesłów kwalifikujący zakres: „dodawanego", „uchylanego", „zmienianego".
		/// </summary>
		public string? ScopeQualifier { get; set; }

		/// <summary>Dosłowny tekst zakresu („w zakresie dodawanego art. 26e").</summary>
		public string? ScopeRawText { get; set; }

		/// <summary>Dosłowny fragment powołania, z którego powstała ta referencja.</summary>
		public string RawText { get; set; } = string.Empty;

		/// <summary>
		/// Czy referencja nadaje się do przypisania daty. False dla zakresu opisanego prozą oraz dla
		/// referencji zbudowanych z fragmentu, którego nie udało się rozłożyć.
		/// </summary>
		public bool IsApplicable { get; set; } = true;
	}
}
