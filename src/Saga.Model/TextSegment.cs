#nullable enable

using System;

namespace Saga.Model
{
    /// <summary>
    /// Segment tekstu w jednostce redakcyjnej, np. pojedyncze zdanie.
    /// Umożliwia precyzyjne kotwiczenie nowelizacji na poziomie zdań.
    /// </summary>
    public class TextSegment : IEntersIntoForce
    {
        /// <summary>
        /// Typ segmentu (np. Sentence).
        /// </summary>
        public TextSegmentType Type { get; set; } = TextSegmentType.Sentence;

        /// <summary>
        /// Tekst segmentu.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Kolejność segmentu w jednostce (1-based).
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Opcjonalna rola segmentu (np. "ListIntro").
        /// </summary>
        public string? Role { get; set; }

        /// <summary>
        /// Data wejścia w życie danej jednostki redakcyjnej.
        /// </summary>
        public DateTime EffectiveDate { get; set; }
    }
}
