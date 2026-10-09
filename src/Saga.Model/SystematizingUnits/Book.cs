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

        /// <summary>
        /// Data wejścia w życie nagłówka księgi, niezależna od daty samej księgi.
        /// </summary>
        public DateTime HeadingEffectiveDate { get; set; }

        /// <summary>
        /// Czy księga jest domyślna/ukryta (niejawna w dokumencie).
        /// </summary>
        public bool IsImplicit { get; set; } = true;

        public Book()
        {
            UnitType = UnitType.Book;
            EIdPrefix = SystematizingHierarchy.EIdPrefix(UnitType.Book);
            DisplayLabel = "ks.";
        }

        public List<Title> Titles { get; set; } = new();
    }
}
