#nullable enable

namespace Saga.Core.Ingest.Pdf
{
    /// <summary>
    /// Wiersz tekstu odtworzony z liter strony PDF (współrzędne w punktach; oś Y rośnie DO GÓRY).
    /// Tekst niesie już kanał indeksu górnego w notacji [x] (parytet z GetFullText DOCX).
    /// </summary>
    internal sealed record PdfTextLine
    {
        public required string Text { get; init; }

        /// <summary>Numer strony (1-based).</summary>
        public required int PageNumber { get; init; }

        /// <summary>X początku wiersza (punkt zerowy pierwszej litery).</summary>
        public required double StartX { get; init; }

        /// <summary>Linia bazowa wiersza (dominanta Y liter — superscripty nie przesuwają bazy).</summary>
        public required double BaselineY { get; init; }

        /// <summary>Dominujący rozmiar fontu wiersza (punkty).</summary>
        public required double DominantSize { get; init; }

        /// <summary>Wysokość strony (do klasyfikacji stref marginesów/przypisów).</summary>
        public required double PageHeight { get; init; }

        /// <summary>
        /// Największy odstęp między sąsiednimi znakami wiersza (punkty), mierzony od końca ostatniego znaku widocznego —
        /// wiersz tabeli ma między kolumnami odstęp wielokrotnie większy niż między wyrazami.
        /// </summary>
        public double MaxGap { get; init; }

        /// <summary>
        /// Wiersz leży na stronie poziomej — szerszej niż wysoka albo w treści złożonej w poprzek strony pionowej
        /// (odpowiednik orientacji sekcji DOCX; załącznik dzieli się na tej granicy na segmenty).
        /// </summary>
        public bool IsLandscape { get; init; }

        /// <summary>Fragmenty wiersza złożone pismem kursywnym (<see cref="BlockLayoutInfo.ItalicText"/>).</summary>
        public IReadOnlyList<string> ItalicText { get; init; } = [];

        /// <summary>Zakresy <see cref="Text"/> złożone kursywą albo pogrubieniem i grupy indeksu górnego (ADR-0117).</summary>
        public IReadOnlyList<Saga.Model.InlineSpan> Spans { get; init; } = [];

        /// <summary>
        /// Wiersz zastępczy tabeli z obramowaniem: stoi w miejscu tabeli na stronie (góra siatki), a komórki niosą
        /// własne wiersze. Null dla wiersza tekstu.
        /// </summary>
        public PdfTable? Table { get; init; }
    }
}
