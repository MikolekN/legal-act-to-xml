#nullable enable

namespace ModelDto
{
    /// <summary>
    /// Odnośnik (przypis dolny) aktu prawnego.
    /// </summary>
    public class Footnote
    {
        /// <summary>
        /// Wewnętrzny identyfikator odnośnika z dokumentu Word (w:id) —
        /// używany do dopasowania markerów "[id)]" w tekście.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Kolejny numer odnośnika widoczny w akcie (1..N)
        /// </summary>
        public int Number { get; set; }

        /// <summary>
        /// Treść odnośnika (bez oznaczenia numeru).
        /// </summary>
        public string Content { get; set; } = string.Empty;
    }
}
