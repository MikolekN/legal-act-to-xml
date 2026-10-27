using DocumentFormat.OpenXml.Packaging;
using Word = DocumentFormat.OpenXml.Wordprocessing;

namespace Saga.Core.Ingest
{
    /// <summary>
    /// Kursywa przebiegu DOCX z dziedziczeniem stylów: formatowanie wprost (<c>w:i</c>) wygrywa, inaczej decydują styl
    /// znaku i styl akapitu — w każdym najbliższa definicja w łańcuchu <c>w:basedOn</c>. Kursywa jest właściwością
    /// przełączaną (ECMA-376 § 17.7.3): ustawiona w obu stylach znosi się. Nazwisko w formule podpisu stoi w stylu znaku
    /// „Kkursywa" (1692 z 2333 podpisów korpusu) albo ma kursywę wprost (641).
    /// </summary>
    internal sealed class DocxItalics
    {
        private readonly Dictionary<string, (bool? Italic, string? BasedOn)> _styles;
        private readonly string? _defaultParagraphStyle;

        private DocxItalics(Dictionary<string, (bool? Italic, string? BasedOn)> styles, string? defaultParagraphStyle)
        {
            _styles = styles;
            _defaultParagraphStyle = defaultParagraphStyle;
        }

        internal static DocxItalics Read(MainDocumentPart mainPart)
        {
            var styles = new Dictionary<string, (bool? Italic, string? BasedOn)>(StringComparer.Ordinal);
            string? defaultParagraphStyle = null;
            foreach (var style in mainPart.StyleDefinitionsPart?.Styles?.Elements<Word.Style>() ?? [])
            {
                if (style.StyleId?.Value is not { } id)
                    continue;

                styles[id] = (Flag(style.StyleRunProperties?.Italic), style.BasedOn?.Val?.Value);
                if (style.Default?.Value == true && style.Type?.Value == Word.StyleValues.Paragraph)
                    defaultParagraphStyle = id;
            }

            return new DocxItalics(styles, defaultParagraphStyle);
        }

        /// <summary>Czy przebieg jest złożony kursywą — wprost albo ze stylu znaku i stylu akapitu.</summary>
        internal bool IsItalic(Word.Run run, string? paragraphStyleId)
        {
            if (Flag(run.RunProperties?.Italic) is { } direct)
                return direct;

            var character = Resolve(run.RunProperties?.RunStyle?.Val?.Value) ?? false;
            var paragraph = Resolve(paragraphStyleId ?? _defaultParagraphStyle) ?? false;
            return character ^ paragraph;
        }

        /// <summary>Kursywa wprost, bez stylów — gdy dokument nie jest znany (akapit czytany poza pakietem).</summary>
        internal static bool IsDirectlyItalic(Word.Run run) => Flag(run.RunProperties?.Italic) == true;

        private bool? Resolve(string? styleId)
        {
            for (var depth = 0; styleId is not null && depth < MaxStyleDepth; depth++)
            {
                if (!_styles.TryGetValue(styleId, out var style))
                    return null;
                if (style.Italic is { } italic)
                    return italic;
                styleId = style.BasedOn;
            }

            return null;
        }

        /// <summary>Flaga OnOff: element bez <c>val</c> to włączona; nierozpoznana wartość też (jak w odczycie układu).</summary>
        private static bool? Flag(Word.OnOffType? onOff) =>
            onOff is null ? null : onOff.Val is not { } val || !val.HasValue || val.Value;

        // Łańcuch basedOn bywa zapętlony w uszkodzonych plikach.
        private const int MaxStyleDepth = 20;
    }
}
