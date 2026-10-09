using System;
using System.Collections.Generic;

namespace Saga.Model.SystematizingUnits
{
    /// <summary>
    /// Jednostka systematyzująca: dział
    /// </summary>
    public class Division : BaseEntity, ISystematizingUnit
    {
        /// <summary>
        /// Tytuł działu (np. "Ochrona środowiska", "Bezpieczeństwo i higiena pracy")
        /// </summary>
        public string Heading { get; set; } = string.Empty;

        /// <summary>
        /// Data wejścia w życie nagłówka działu, niezależna od daty samego działu.
        /// </summary>
        public DateTime HeadingEffectiveDate { get; set; }

        /// <summary>
        /// Czy dział jest domyślny/ukryty (niejawny w dokumencie).
        /// </summary>
        public bool IsImplicit { get; set; } = true;

        public Division()
        {
            UnitType = UnitType.Division;
            EIdPrefix = SystematizingHierarchy.EIdPrefix(UnitType.Division);
            DisplayLabel = "dz.";
        }

        public List<Chapter> Chapters { get; set; } = new();
    }
}
