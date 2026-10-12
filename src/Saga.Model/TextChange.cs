using System.Collections.Generic;

#nullable enable

namespace Saga.Model
{
    /// <summary>Które wystąpienie tekstu w jednostce zmienia <see cref="TextChange"/>.</summary>
    public enum TextOccurrence
    {
        /// <summary>Każde wystąpienie (domyślnie).</summary>
        All,

        /// <summary>Ostatnie — znak kończący jednostkę („kropkę zastępuje się średnikiem").</summary>
        Last,
    }

    /// <summary>
    /// Zmiana w tekście jednostki aktu zmienianego, która nie przytacza nowego brzmienia jednostki: wskazane
    /// wyrazy albo znak zastępuje się innymi (§ 87–88 ZTP). W eksporcie instrukcja <c>now:zamien-tekst</c>.
    /// </summary>
    public class TextChange
    {
        /// <summary>Tekst zastępowany — taki, jak stoi w akcie zmienianym.</summary>
        public string OldText { get; set; } = string.Empty;

        /// <summary>Tekst, który go zastępuje; pusty przy skreśleniu.</summary>
        public string NewText { get; set; } = string.Empty;

        /// <summary>Które wystąpienie tekstu w jednostce się zmienia.</summary>
        public TextOccurrence Occurrence { get; set; }

        /// <summary>
        /// Jednostki, w których zmienia się tekst, gdy są inne niż cele nowelizacji: zamiana znaku kończącego
        /// jednostkę stoi w jednym zdaniu ze zmianą jednostki po niej („w pkt 4 średnik zastępuje się kropką
        /// i uchyla się pkt 5"). Pusta lista — zmiana dotyczy celów nowelizacji.
        /// </summary>
        public List<StructuralAmendmentReference> Targets { get; set; } = new();
    }
}
