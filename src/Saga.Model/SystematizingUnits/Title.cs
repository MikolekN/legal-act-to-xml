using System;
using System.Collections.Generic;

namespace Saga.Model.SystematizingUnits
{
    /// <summary>
    /// Jednostka systematyzująca: tytuł
    /// </summary>
    public class Title : BaseEntity, ISystematizingUnit
    {
        /// <summary>
        /// Tytuł sekcji (np. "Podmioty prawa cywilnego", "Spadki")
        /// </summary>
        public string Heading { get; set; } = string.Empty;

        /// <summary>
        /// Data wejścia w życie nagłówka tytułu, niezależna od daty samego tytułu.
        /// </summary>
        public DateTime HeadingEffectiveDate { get; set; }

        /// <summary>
        /// Czy tytuł jest domyślny/ukryty (niejawny w dokumencie).
        /// </summary>
        public bool IsImplicit { get; set; } = true;

        public Title()
        {
            UnitType = UnitType.Title;
            EIdPrefix = SystematizingHierarchy.EIdPrefix(UnitType.Title);
            DisplayLabel = "tyt.";
        }

        public List<Division> Divisions { get; set; } = new();
    }
}
