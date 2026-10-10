using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace Saga.Model.EntryIntoForce
{
    /// <summary>Rodzaj wyjątku od reguły ogólnej.</summary>
    public enum PartialEntryIntoForceKind
    {
        /// <summary>
        /// Jednostka dostaje własną datę wejścia w życie i jest wyłączona z reguły ogólnej.
        /// </summary>
        EntryDate,

        /// <summary>
        /// Jednostka dostaje tylko datę mocy wstecznej. Data wejścia w życie zostaje z
        /// reguły ogólnej, a jednostka nie jest z niej wyłączona.
        /// </summary>
        RetroactivityOnly,
    }

    /// <summary>
    /// Częściowa data wejścia w życie — wyjątek od reguły ogólnej:
    /// „z wyjątkiem art. 2, który wchodzi w życie z dniem 31 maja 2026 r.".
    /// </summary>
    public class PartialEntryIntoForce
    {
        /// <summary>Który składnik dat wyjątek nadpisuje.</summary>
        public PartialEntryIntoForceKind Kind { get; set; } = PartialEntryIntoForceKind.EntryDate;

        /// <summary>Jednostki objęte wyjątkiem.</summary>
        public List<ScopedUnitReference> Targets { get; set; } = new();

        /// <summary>Formuła wejścia w życie dla tego wyjątku.</summary>
        public EntryIntoForceFormula Formula { get; set; } = new();

        /// <summary>Data wyliczona z formuły.</summary>
        public DateTime? ResolvedDate { get; set; }

        /// <summary>Moc wsteczna wyjątku („…, z mocą od dnia 1 września 2024 r.").</summary>
        public DateTime? RetroactiveFrom { get; set; }

        /// <summary>Dosłowny tekst wyjątku.</summary>
        public string RawText { get; set; } = string.Empty;

        /// <summary>
        /// eId jednostki, w której wyjątek zapisano: pozycja wyliczenia wyjątków („art_43__pkt_2")
        /// albo jednostka klauzuli głównej, gdy wyjątek stoi w jej zdaniu.
        /// </summary>
        public string? SourceUnitEId { get; set; }

        /// <summary>Czy wyjątek można zastosować.</summary>
        public bool IsApplicable { get; set; } = true;

        /// <summary>Powód, dla którego wyjątek nie został zastosowany.</summary>
        public string? UnresolvedReason { get; set; }

        /// <summary>
        /// Czy wyjątek doprowadził do wartości, którą da się zapisać na jednostce.
        /// </summary>
        public bool IsResolved =>
            Kind == PartialEntryIntoForceKind.RetroactivityOnly
                ? RetroactiveFrom.HasValue
                : ResolvedDate.HasValue;

        /// <summary>Cele, które faktycznie nadają się do przypisania daty.</summary>
        public IEnumerable<ScopedUnitReference> ApplicableTargets =>
            IsApplicable ? Targets.Where(t => t.IsApplicable) : Enumerable.Empty<ScopedUnitReference>();
    }
}
