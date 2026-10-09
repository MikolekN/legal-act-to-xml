using System.Collections.Generic;

#nullable enable

namespace Saga.Model
{
    /// <summary>
    /// Segmenty eId — miejsce, w którym oznaczenie jednostki staje się częścią adresu.
    /// </summary>
    public static class EIdSegment
    {
        /// <summary>Separator segmentów eId („art_5__ust_2").</summary>
        public const string Separator = "__";

        /// <summary>
        /// Segment „levelType_oznaczenie".
        /// Indeks górny zostaje w notacji „5[1]" (ADR-0019).
        /// Przy braku oznaczenia sam levelType (jednostka zostaje w adresie).
        /// </summary>
        public static string Of(string levelType, string? designation) =>
            string.IsNullOrEmpty(designation) ? levelType : $"{levelType}_{designation}";

        /// <summary>Składa segmenty w eId.</summary>
        public static string Join(IEnumerable<string> segments) => string.Join(Separator, segments);
    }
}
