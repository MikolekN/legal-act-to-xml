using Saga.Model;

namespace Saga.Core.Services.UnitReferences
{
    /// <summary>
    /// Co znaczy samodzielny „§" (bez artykułu w ścieżce) w powołaniach danego dokumentu.
    /// </summary>
    internal static class SectionSignRole
    {
        /// <summary>Rodzaje aktów, których jednostką podstawową jest paragraf.</summary>
        private static readonly HashSet<LegalActType> SectionBasedActs = new()
        {
            LegalActType.Regulation,
            LegalActType.Resolution,
            LegalActType.ExecutiveOrder,
            LegalActType.LocalLegalAct,
        };

        /// <summary>Czy samodzielny „§" jest w tym dokumencie jednostką główną.</summary>
        internal static bool IsBasicUnit(DocumentClassificationResult? classification)
        {
            if (classification is null)
                return false;

            if (classification.ActType is { } actType && SectionBasedActs.Contains(actType))
                return true;

            return classification.ActType == LegalActType.Announcement &&
                classification.Signals.Any(signal =>
                    signal.Kind == DocumentSignalKind.BaseUnitDominance &&
                    signal.SupportsType is { } announced && SectionBasedActs.Contains(announced));
        }
    }
}
