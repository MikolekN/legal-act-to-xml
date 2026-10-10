#nullable enable

namespace Saga.Core.Ingest
{
    /// <summary>
    /// Miejsce bloku w tabeli dokumentu źródłowego: która tabela, wiersz i kolumna siatki oraz scalenia
    /// komórki. Kilka akapitów jednej komórki ma tę samą wartość; kolejność niesie kolejność bloków.
    /// </summary>
    public sealed record BlockTableCell
    {
        /// <summary>Numer tabeli w dokumencie (od 0, w kolejności wystąpienia). Tabela zagnieżdżona ma własny numer.</summary>
        public required int Table { get; init; }

        /// <summary>Wiersz tabeli (od 0).</summary>
        public required int Row { get; init; }

        /// <summary>Kolumna siatki tabeli (od 0), po uwzględnieniu scaleń komórek stojących wcześniej w wierszu.</summary>
        public required int Column { get; init; }

        /// <summary>Liczba kolumn siatki, które komórka zajmuje.</summary>
        public int ColumnSpan { get; init; } = 1;

        /// <summary>Liczba wierszy, które komórka zajmuje; 1, gdy nie jest scalona w pionie.</summary>
        public int RowSpan { get; init; } = 1;

        /// <summary>
        /// Komórka jest dalszym ciągiem komórki scalonej w pionie, zaczętej wyżej. Word zapisuje ją w każdym
        /// wierszu scalenia, choć w tabeli jej nie widać.
        /// </summary>
        public bool ContinuesCellAbove { get; init; }

        /// <summary>Wiersz jest oznaczony w dokumencie jako wiersz nagłówka tabeli.</summary>
        public bool IsHeaderRow { get; init; }
    }
}
