using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace Saga.Model.EntryIntoForce
{
	/// <summary>
	/// Rozłożona klauzula wejścia w życie aktu.
	/// </summary>
	public class EntryIntoForceInfo
	{
		/// <summary>Dosłowny tekst całej klauzuli, wraz z wyjątkami.</summary>
		public string RawText { get; set; } = string.Empty;

		/// <summary>Formuła reguły ogólnej (część klauzuli przed pierwszym wyjątkiem).</summary>
		public EntryIntoForceFormula Formula { get; set; } = new();

		/// <summary>Wyliczona data wejścia w życie aktu.</summary>
		public DateTime? ResolvedDate { get; set; }

		/// <summary>
		/// Moc wsteczna reguły ogólnej: „z mocą od dnia 1 lipca 2025 r.".
		/// </summary>
		public DateTime? RetroactiveFrom { get; set; }

		/// <summary>Dosłowny fragment, z którego odczytano moc wsteczną.</summary>
		public string? RetroactiveFromRawText { get; set; }

		/// <summary>Wyjątki — częściowe daty wejścia w życie dla wskazanych jednostek.</summary>
		public List<PartialEntryIntoForce> Partials { get; set; } = new();

		/// <summary>
		/// Fragmenty, których nie udało się rozłożyć (nieznane formuły, powołania opisane prozą,
		/// dodatkowe klauzule samozwrotne poza klauzulą główną).
		/// </summary>
		public List<string> UnresolvedFragments { get; set; } = new();

		/// <summary>eId jednostki, w której znaleziono klauzulę główną (np. „art_25__ust_1").</summary>
		public string? SourceUnitEId { get; set; }

		/// <summary>
		/// Czy klauzula została rozłożona w całości: data reguły ogólnej znana, brak fragmentów
		/// nierozłożonych i każdy wyjątek przypisywalny z rozstrzygniętą datą.
		/// </summary>
		public bool IsComplete =>
			ResolvedDate.HasValue &&
			UnresolvedFragments.Count == 0 &&
			Partials.All(p => p.IsApplicable && p.ResolvedDate.HasValue);
	}
}
