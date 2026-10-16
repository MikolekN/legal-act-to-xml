using System;
using System.Collections.Generic;

namespace Saga.Model.SystematizingUnits
{
    /// <summary>
    /// Jednostka systematyzująca: część
    /// </summary>
    public class Part : BaseEntity, ISystematizingUnit
    {
        /// <summary>
        /// Tytuł części (np. "Przepisy ogólne", "Własność")
        /// </summary>
        public string Heading { get; set; } = string.Empty;

        /// <inheritdoc/>
        public string DesignationMarkers { get; set; } = string.Empty;

        /// <summary>
        /// Data wejścia w życie nagłówka części, niezależna od daty samej części.
        /// </summary>
        public DateTime HeadingEffectiveDate { get; set; }

        public Part()
        {
            UnitType = UnitType.Part;
            IsImplicit = true;
            EIdPrefix = SystematizingHierarchy.EIdPrefix(UnitType.Part);
            DisplayLabel = "cz.";
        }

        public List<Book> Books { get; set; } = new();
    }
}
