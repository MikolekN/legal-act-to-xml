#nullable enable

namespace Saga.Core.Ingest
{
    /// <summary>
    /// Blok tekstu dokumentu — format-agnostyczny odpowiednik akapitu Word,
    /// wspólna reprezentacja pośrednia potoku parsowania (DOCX/PDF/TXT).
    ///
    /// Text zawiera kanał indeksu górnego w nawiasach kwadratowych [x]
    /// (spójnie z ParagraphExtensions.GetFullText i notacją x[y] z § 89 ust. 6 ZTP)
    /// oraz tabulatory jako \t. Text NIE jest trimowany ani sanityzowany —
    /// Trim i Sanitize wykonuje orkiestrator (parytet z dotychczasową ścieżką DOCX).
    /// </summary>
    public sealed record DocumentBlock
    {
        public required string Text { get; init; }

        /// <summary>Identyfikator stylu akapitu — tylko DOCX; null dla PDF/TXT.</summary>
        public string? StyleId { get; init; }

        /// <summary>Opcjonalne metadane układu (wcięcia, pogrubienie, kursywa).</summary>
        public BlockLayoutInfo? Layout { get; init; }

        /// <summary>Położenie bloku w źródle (diagnostyka).</summary>
        public BlockSourceLocation Source { get; init; }

        public BlockRole Role { get; init; } = BlockRole.Body;

        /// <summary>
        /// Przypisy, do których odwołuje się blok (znaczniki „[N)]" w <see cref="Text"/>), w kolejności odwołań.
        /// Tylko DOCX — tam treść przypisu leży poza treścią aktu; PDF oddaje przypisy osobnymi blokami
        /// <see cref="BlockRole.FootnoteText"/>.
        /// </summary>
        public IReadOnlyList<BlockFootnote> Footnotes { get; init; } = [];

        public bool IsEmpty => string.IsNullOrWhiteSpace(Text);
    }

    /// <summary>Przypis w reprezentacji pośredniej: numer, pod którym stoi w akcie, i akapity jego treści.</summary>
    public sealed record BlockFootnote(string Symbol, IReadOnlyList<string> Paragraphs);
}
