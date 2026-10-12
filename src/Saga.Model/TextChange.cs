#nullable enable

namespace Saga.Model
{
    /// <summary>
    /// Zmiana w tekście jednostki aktu zmienianego, która nie przytacza nowego brzmienia jednostki: wskazane
    /// wyrazy zastępuje się innymi (§ 87–88 ZTP). W eksporcie instrukcja <c>now:zamien-tekst</c>.
    /// </summary>
    public class TextChange
    {
        /// <summary>Tekst zastępowany — taki, jak stoi w akcie zmienianym.</summary>
        public string OldText { get; set; } = string.Empty;

        /// <summary>Tekst, który go zastępuje.</summary>
        public string NewText { get; set; } = string.Empty;
    }
}
