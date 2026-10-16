using System;
using System.Collections.Generic;
using Saga.Model.EditorialUnits;

namespace Saga.Model.SystematizingUnits
{
    /// <summary>
    /// Jednostka systematyzująca: oddział (najniższa, zawiera artykuły)
    /// </summary>
    public class Subchapter : BaseEntity, ISystematizingUnit
    {
        /// <summary>
        /// Tytuł oddziału (np. "Zakres stosowania", "Definicje")
        /// </summary>
        public string Heading { get; set; } = string.Empty;

        /// <inheritdoc/>
        public string DesignationMarkers { get; set; } = string.Empty;

        /// <summary>
        /// Data wejścia w życie nagłówka oddziału, niezależna od daty samego oddziału.
        /// </summary>
        public DateTime HeadingEffectiveDate { get; set; }

        public Subchapter()
        {
            UnitType = UnitType.Subchapter;
            IsImplicit = true;
            EIdPrefix = SystematizingHierarchy.EIdPrefix(UnitType.Subchapter);
            DisplayLabel = "oddz.";
        }

        // oddziały zawierają artykuły
        public List<Article> Articles { get; set; } = new();
    }
}
