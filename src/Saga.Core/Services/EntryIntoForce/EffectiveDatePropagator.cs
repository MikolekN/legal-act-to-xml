using Saga.Model;

namespace Saga.Core.Services.EntryIntoForce
{
    /// <summary>
    /// Rozsyła po drzewie jednostek parę dat: datę wejścia w życie i moc wsteczną.
    /// Każda jednostka dziedziczy od rodzica, a wyjątki nadpisują wartość.
    ///
    /// Brak wpisanej daty wejścia w życie niesie informację o tym, że jednostka jest objęta
    /// wyjątkiem, którego nie udało się rozstrzygnąć lub cały akt ma nierozstrzygniętą regułę
    /// ogólną. Brak mocy wstecznej znaczy po prostu, że jej nie ma.
    /// </summary>
    internal static class EffectiveDatePropagator
    {
        internal static void Propagate(
            LegalDocument document, EntryDates general, EntryIntoForceExceptionMap exceptions)
        {
            Paint(document.RootPart, general, exceptions,
                new HashSet<BaseEntity>(ReferenceEqualityComparer.Instance));
        }

        private static void Paint(
            BaseEntity unit, EntryDates inherited, EntryIntoForceExceptionMap exceptions, HashSet<BaseEntity> visited)
        {
            if (!visited.Add(unit))
                return;

            var governing = new EntryDates(
                exceptions.TryGetUnit(unit, out var ownEntry) ? ownEntry : inherited.Entry,
                exceptions.TryGetUnitRetroactivity(unit, out var ownRetro) ? ownRetro : inherited.RetroactiveFrom);

            Write(unit, governing);

            if (unit is IHasTextSegments holder)
            {
                foreach (var segment in holder.TextSegments)
                    Write(segment, new EntryDates(
                        exceptions.TryGetSegment(segment, out var segmentEntry) ? segmentEntry : governing.Entry,
                        exceptions.TryGetSegmentRetroactivity(segment, out var segmentRetro)
                            ? segmentRetro
                            : governing.RetroactiveFrom));
            }

            if (unit is IHasAmendments { Amendment: { } amendment })
                Write(amendment, governing);

            foreach (var child in EditorialUnitWalker.Children(unit, includeAmendmentContent: true))
                Paint(child, governing, exceptions, visited);
        }

        private static void Write(IEntersIntoForce target, EntryDates dates)
        {
            target.EffectiveDate = dates.Entry ?? default;
            target.RetroactiveFrom = dates.RetroactiveFrom ?? default;
        }
    }
}
