using System;
using System.Collections.Generic;

#nullable enable

namespace Saga.Model
{
    /// <summary>
    /// Odnośnik (przypis) aktu: numer, pod którym stoi w akcie, i treść. Miejsce odwołania niesie tekst
    /// jednostki — znacznik „[N)]" z tym samym numerem.
    /// </summary>
    public sealed class Footnote
    {
        public Guid Guid { get; set; } = Guid.NewGuid();

        /// <summary>Numer odnośnika jak w akcie („1", „12").</summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>Akapity treści odnośnika, bez własnego znacznika na początku.</summary>
        public List<string> Paragraphs { get; set; } = new();
    }
}
