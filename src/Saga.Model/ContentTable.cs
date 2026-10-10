using System.Collections.Generic;

#nullable enable

namespace Saga.Model
{
    /// <summary>Tabela w treści aktu: wiersze i komórki z akapitami tekstu.</summary>
    public class ContentTable
    {
        public List<ContentTableRow> Rows { get; set; } = new();
    }

    public class ContentTableRow
    {
        /// <summary>Wiersz nagłówka tabeli (tytuły kolumn).</summary>
        public bool IsHeader { get; set; }

        /// <summary>
        /// Komórki zaczynające się w tym wierszu. Komórka scalona w pionie stoi tylko w wierszu, w którym
        /// się zaczyna, więc wiersz pod nią ma mniej komórek, niż tabela kolumn.
        /// </summary>
        public List<ContentTableCell> Cells { get; set; } = new();
    }

    public class ContentTableCell
    {
        /// <summary>Liczba kolumn, które komórka zajmuje.</summary>
        public int ColumnSpan { get; set; } = 1;

        /// <summary>Liczba wierszy, które komórka zajmuje.</summary>
        public int RowSpan { get; set; } = 1;

        /// <summary>Akapity komórki w kolejności dokumentu; pusta komórka nie ma żadnego.</summary>
        public List<string> Paragraphs { get; set; } = new();
    }
}
