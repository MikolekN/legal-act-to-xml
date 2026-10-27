#nullable enable

using Saga.Model;

namespace Saga.Core.Ingest
{
    /// <summary>
    /// Blok tekstu dokumentu — format-agnostyczny odpowiednik akapitu Word,
    /// wspólna reprezentacja pośrednia potoku parsowania (DOCX/PDF/TXT).
    ///
    /// Text zawiera kanał indeksu górnego w nawiasach kwadratowych [x]
    /// (spójnie z ParagraphExtensions.GetFullText i notacją x[y] z § 89 ust. 6 ZTP)
    /// oraz tabulatory jako \t. Text NIE jest trimowany ani sanityzowany —
    /// Trim i zwinięcie odstępów wykonuje orkiestrator (parytet z dotychczasową ścieżką DOCX).
    /// </summary>
    public sealed record DocumentBlock
    {
        public required string Text { get; init; }

        /// <summary>
        /// Zakresy <see cref="Text"/> złożone formatowaniem znakowym (kursywa, pogrubienie, indeks dolny, grupy indeksu
        /// górnego) — z DOCX, z PDF bez indeksu dolnego; obok tekstu, nie w nim, więc wzorce rozpoznawania ich nie widzą
        /// (ADR-0117).
        /// </summary>
        public IReadOnlyList<InlineSpan> Spans { get; init; } = [];

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

        /// <summary>Komórka tabeli, w której stoi blok; null dla akapitu poza tabelą i dla formatów bez tabel.</summary>
        public BlockTableCell? TableCell { get; init; }

        /// <summary>
        /// MathML akapitu złożonego z samego równania (wzór w osobnym akapicie); null dla akapitu z tekstem
        /// i dla formatów bez równań. <see cref="Text"/> niesie wtedy zapis liniowy tego wzoru.
        /// </summary>
        public string? FormulaMathMl { get; init; }

        /// <summary>
        /// Równanie otwierające akapit, po którym stoi tekst — symbol opisywany w wierszu legendy wzoru
        /// („I_e – ilość energii …"); null, gdy akapit nie zaczyna się równaniem albo jest samym wzorem.
        /// </summary>
        public BlockSymbol? LeadingSymbol { get; init; }

        /// <summary>
        /// Czy strona, na której stoi blok, jest pozioma — z ustawień sekcji DOCX (<c>w:pgSz</c>), w PDF z rozmiaru strony
        /// albo z treści złożonej w poprzek strony. Null, gdy źródło układu strony nie podaje (TXT, sekcja bez rozmiaru strony).
        /// </summary>
        public bool? IsLandscape { get; init; }

        public bool IsEmpty => string.IsNullOrWhiteSpace(Text);
    }

    /// <summary>Symbol otwierający akapit: zapis liniowy (początek <see cref="DocumentBlock.Text"/>) i MathML.</summary>
    public sealed record BlockSymbol(string Text, string MathMl);

    /// <summary>Przypis w reprezentacji pośredniej: oznaczenie, pod którym stoi w akcie, i akapity jego treści.</summary>
    public sealed record BlockFootnote(string Symbol, IReadOnlyList<string> Paragraphs)
    {
        /// <summary>
        /// Numer, pod którym znacznik stoi w <see cref="DocumentBlock.Text"/> — jednoznaczny w dokumencie.
        /// Różni się od oznaczenia, gdy sekcja dokumentu wznawia numerację przypisów albo zmienia jej zapis.
        /// </summary>
        public string Marker { get; init; } = Symbol;

        /// <summary>Ciąg numeracji (sekcja dokumentu), w którym przypis stoi.</summary>
        public int Run { get; init; }

        /// <summary>Czy odwołanie ma własny znak zamiast numeru z licznika (ADR-0095).</summary>
        public bool IsCustomMark { get; init; }
    }
}
