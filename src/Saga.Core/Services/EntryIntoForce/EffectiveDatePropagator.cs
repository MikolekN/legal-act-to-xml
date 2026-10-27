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
            var visited = new HashSet<BaseEntity>(ReferenceEqualityComparer.Instance);
            foreach (var part in document.Parts)
                Paint(part, general, exceptions, visited);

            // Jednostki w załącznikach wchodzą w życie z aktem; wyjątki klauzuli wskazują dziś tylko jednostki aktu.
            foreach (var unit in document.Annexes.SelectMany(annex => annex.Content).Select(block => block.Unit).OfType<BaseEntity>())
                Paint(unit, general, exceptions, visited);
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

            // Treść cytowana nowelizacji stoi w ostatnim zdaniu jednostki i wchodzi w życie razem z nim — także
            // wtedy, gdy wyjątek klauzuli wskazuje samo to zdanie, a nie jednostkę (ADR-0121).
            var quoting = governing;
            var bySentence = new Dictionary<int, EntryDates>();
            if (unit is IHasTextSegments holder)
            {
                foreach (var segment in holder.TextSegments)
                {
                    quoting = new EntryDates(
                        exceptions.TryGetSegment(segment, out var segmentEntry) ? segmentEntry : governing.Entry,
                        exceptions.TryGetSegmentRetroactivity(segment, out var segmentRetro)
                            ? segmentRetro
                            : governing.RetroactiveFrom);
                    Write(segment, quoting);
                    bySentence[segment.Order] = quoting;
                }
            }

            if (unit is IHasAmendments owner)
            {
                if (owner.Amendment is { } amendment)
                    Write(amendment, quoting);

                // Komenda wcześniejszego zdania wchodzi w życie ze swoim zdaniem (ADR-0122).
                foreach (var earlier in owner.EarlierSentenceAmendments)
                    Write(earlier, earlier.CommandSentence is { } sentence && bySentence.TryGetValue(sentence, out var dates) ? dates : governing);
            }

            foreach (var child in EditorialUnitWalker.Children(unit, includeAmendmentContent: false))
                Paint(child, governing, exceptions, visited);

            foreach (var quoted in EditorialUnitWalker.AmendmentContent(unit))
                Paint(quoted, quoting, exceptions, visited);
        }

        private static void Write(IEntersIntoForce target, EntryDates dates)
        {
            target.EffectiveDate = dates.Entry ?? default;
            target.RetroactiveFrom = dates.RetroactiveFrom ?? default;
        }
    }
}
