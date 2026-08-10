using Saga.Model;

namespace Saga.Core.Services.EntryIntoForce
{
	/// <summary>
	/// Rozsyła datę wejścia w życie po drzewie jednostek.
    /// Każda jednostka dziedziczy od rodzica, a wyjątki nadpisują wartość.
    ///
	/// Brak niesie informację o tym, że jednostka jest objęta wyjątkiem, którego nie
	/// udało się rozstrzygnąć lub cały akt ma nierozstrzygniętą regułę ogólną.
	/// </summary>
	internal static class EffectiveDatePropagator
	{
		internal static void Propagate(
			LegalDocument document, DateTime? generalDate, EntryIntoForceExceptionMap exceptions)
		{
			Paint(document.RootPart, generalDate, exceptions,
				new HashSet<BaseEntity>(ReferenceEqualityComparer.Instance));
		}

		private static void Paint(
			BaseEntity unit, DateTime? inherited, EntryIntoForceExceptionMap exceptions, HashSet<BaseEntity> visited)
		{
			if (!visited.Add(unit))
				return;

			var governing = exceptions.TryGetUnit(unit, out var own) ? own : inherited;

			unit.EffectiveDate = governing ?? default;

			if (unit is IHasTextSegments holder)
			{
				foreach (var segment in holder.TextSegments)
					segment.EffectiveDate =
						(exceptions.TryGetSegment(segment, out var ownSegment) ? ownSegment : governing) ?? default;
			}

			if (unit is IHasAmendments { Amendment: { } amendment })
				amendment.EffectiveDate = governing ?? default;

			foreach (var child in EditorialUnitWalker.Children(unit, includeAmendmentContent: true))
				Paint(child, governing, exceptions, visited);
		}
	}
}
