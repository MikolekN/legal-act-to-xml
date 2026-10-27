using DocumentFormat.OpenXml.Packaging;
using Saga.Model;
using Word = DocumentFormat.OpenXml.Wordprocessing;

namespace Saga.Core.Ingest
{
    /// <summary>
    /// Formatowanie znakowe przebiegu DOCX z dziedziczeniem stylów (ADR-0116, ADR-0117): formatowanie wprost wygrywa,
    /// inaczej decydują styl znaku i styl akapitu — w każdym najbliższa definicja w łańcuchu <c>w:basedOn</c>.
    /// Kursywa i pogrubienie są właściwościami przełączanymi (ECMA-376 § 17.7.3): ustawione w obu stylach znoszą się;
    /// położenie indeksu (<c>w:vertAlign</c>) przechodzi zwykłym dziedziczeniem. Nazwisko w formule podpisu stoi w stylu
    /// znaku „Kkursywa" (1692 z 2333 podpisów korpusu) albo ma kursywę wprost (641).
    /// </summary>
    internal sealed class DocxRunFormat
    {
        private sealed record StyleFormat(bool? Italic, bool? Bold, bool? Subscript, string? BasedOn);

        private readonly Dictionary<string, StyleFormat> _styles;
        private readonly string? _defaultParagraphStyle;

        private DocxRunFormat(Dictionary<string, StyleFormat> styles, string? defaultParagraphStyle)
        {
            _styles = styles;
            _defaultParagraphStyle = defaultParagraphStyle;
        }

        internal static DocxRunFormat Read(MainDocumentPart mainPart)
        {
            var styles = new Dictionary<string, StyleFormat>(StringComparer.Ordinal);
            string? defaultParagraphStyle = null;
            foreach (var style in mainPart.StyleDefinitionsPart?.Styles?.Elements<Word.Style>() ?? [])
            {
                if (style.StyleId?.Value is not { } id)
                    continue;

                var properties = style.StyleRunProperties;
                styles[id] = new StyleFormat(Flag(properties?.Italic), Flag(properties?.Bold),
                    Subscript(properties?.VerticalTextAlignment), style.BasedOn?.Val?.Value);
                if (style.Default?.Value == true && style.Type?.Value == Word.StyleValues.Paragraph)
                    defaultParagraphStyle = id;
            }

            return new DocxRunFormat(styles, defaultParagraphStyle);
        }

        /// <summary>Czy przebieg jest złożony kursywą — wprost albo ze stylu znaku i stylu akapitu.</summary>
        internal bool IsItalic(Word.Run run, string? paragraphStyleId) =>
            Of(run, paragraphStyleId).Format.HasFlag(InlineFormat.Italic);

        /// <summary>Kursywa wprost, bez stylów — gdy dokument nie jest znany (akapit czytany poza pakietem).</summary>
        internal static bool IsDirectlyItalic(Word.Run run) => Flag(run.RunProperties?.Italic) == true;

        /// <summary>
        /// Format przebiegu, jak wydrukowano, i ta jego część, którą ma cały akapit ze swojego stylu
        /// (<see cref="InlineSpan.FromParagraphStyle"/>). Indeks górny czyta kanał „[x]" (<c>GetFullText</c>), nie ta metoda.
        /// </summary>
        internal (InlineFormat Format, InlineFormat FromParagraphStyle) Of(Word.Run run, string? paragraphStyleId)
        {
            var paragraphStyle = paragraphStyleId ?? _defaultParagraphStyle;
            var properties = run.RunProperties;
            var characterStyle = properties?.RunStyle?.Val?.Value;
            var format = InlineFormat.None;
            var baseline = InlineFormat.None;

            Toggle(InlineFormat.Italic, Flag(properties?.Italic), style => style.Italic);
            Toggle(InlineFormat.Bold, Flag(properties?.Bold), style => style.Bold);

            var paragraphSubscript = Resolve(paragraphStyle, style => style.Subscript) ?? false;
            var subscript = Subscript(properties?.VerticalTextAlignment)
                ?? Resolve(characterStyle, style => style.Subscript)
                ?? paragraphSubscript;
            if (subscript)
                format |= InlineFormat.Subscript;
            if (paragraphSubscript)
                baseline |= InlineFormat.Subscript;

            return (format, format & baseline);

            void Toggle(InlineFormat flag, bool? direct, Func<StyleFormat, bool?> property)
            {
                var paragraph = Resolve(paragraphStyle, property) ?? false;
                if (direct ?? ((Resolve(characterStyle, property) ?? false) ^ paragraph))
                    format |= flag;
                if (paragraph)
                    baseline |= flag;
            }
        }

        private bool? Resolve(string? styleId, Func<StyleFormat, bool?> property)
        {
            for (var depth = 0; styleId is not null && depth < MaxStyleDepth; depth++)
            {
                if (!_styles.TryGetValue(styleId, out var style))
                    return null;
                if (property(style) is { } value)
                    return value;
                styleId = style.BasedOn;
            }

            return null;
        }

        /// <summary>Flaga OnOff: element bez <c>val</c> to włączona; nierozpoznana wartość też (jak w odczycie układu).</summary>
        private static bool? Flag(Word.OnOffType? onOff) =>
            onOff is null ? null : onOff.Val is not { } val || !val.HasValue || val.Value;

        /// <summary>Położenie indeksu: dolny — true, inne podane — false, niepodane — null (dziedziczy).</summary>
        private static bool? Subscript(Word.VerticalTextAlignment? alignment) =>
            alignment?.Val?.Value is { } position ? position == Word.VerticalPositionValues.Subscript : null;

        // Łańcuch basedOn bywa zapętlony w uszkodzonych plikach.
        private const int MaxStyleDepth = 20;
    }
}
