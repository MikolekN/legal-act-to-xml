using System;
using System.Collections.Generic;

namespace Saga.Model.SystematizingUnits
{
    /// <summary>
    /// Jednostka systematyzująca: księga
    /// </summary>
    public class Book : BaseEntity, ISystematizingUnit
    {
        /// <summary>
        /// Tytuł księgi (np. "Własność", "Zobowiązania")
        /// </summary>
        public string Heading { get; set; } = string.Empty;

        /// <inheritdoc/>
        public string DesignationMarkers { get; set; } = string.Empty;

        /// <summary>
        /// Data wejścia w życie nagłówka księgi, niezależna od daty samej księgi.
        /// </summary>
        public DateTime HeadingEffectiveDate { get; set; }

        public Book()
        {
            UnitType = UnitType.Book;
            IsImplicit = true;
            EIdPrefix = SystematizingHierarchy.EIdPrefix(UnitType.Book);
            DisplayLabel = "ks.";
        }

        public List<Title> Titles { get; set; } = new();
    }
}
