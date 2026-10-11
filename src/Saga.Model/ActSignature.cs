using System;

#nullable enable

namespace Saga.Model
{
    /// <summary>
    /// Formuła podpisu zamykająca akt: „Prezydent Rzeczypospolitej Polskiej: A. Duda".
    /// Organ i osoba są zapisane tak, jak stoją w akcie — imię zwykle samym inicjałem.
    /// </summary>
    public sealed class ActSignature
    {
        /// <summary>Identyfikator osoby podpisującej w eksporcie.</summary>
        public Guid PersonGuid { get; set; } = Guid.NewGuid();

        /// <summary>Organ podpisujący („Prezydent Rzeczypospolitej Polskiej").</summary>
        public string Authority { get; set; } = string.Empty;

        /// <summary>
        /// W czyim imieniu podpisano, gdy nie podpisuje sam organ: „wz." (w zastępstwie) albo „z up."
        /// (z upoważnienia) — tak jak stoi w akcie; null, gdy podpisuje organ.
        /// </summary>
        public string? Capacity { get; set; }

        /// <summary>Imię albo inicjały („A.", „J. K.").</summary>
        public string GivenName { get; set; } = string.Empty;

        /// <summary>Nazwisko („Duda", „Kidawa-Błońska").</summary>
        public string Surname { get; set; } = string.Empty;
    }
}
