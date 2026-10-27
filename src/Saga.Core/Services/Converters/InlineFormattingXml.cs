using System.Text;
using System.Xml.Linq;
using Saga.Model;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Formatowanie znakowe w treści (ADR-0117). Tekst jednostek jest w modelu płaskim napisem, a formatowanie zostaje
    /// przy tekście bloków źródła (<see cref="LegalDocument.FormattedSources"/>). Przebieg po zbudowanej treści aktu — przed
    /// odnośnikami i indeksem górnym — odnajduje tekst każdego węzła w tekście źródła (bez odstępów i wielkości liter,
    /// w kolejności dokumentu) i obejmuje sformatowane zakresy elementami <c>b</c>, <c>i</c>, <c>sub</c>. Wartość tekstowa
    /// węzła się nie zmienia; tekst, którego nie da się odnaleźć jednoznacznie, zostaje bez formatowania. Formatowanie,
    /// które cały akapit ma ze swojego stylu (tytuł, nagłówek), jest typografią rodzaju akapitu i nie wchodzi do treści.
    /// </summary>
    internal static class InlineFormattingXml
    {
        /// <summary>Formatowanie przenoszone do eksportu — elementy włączane po kolei, każdy z własnym pomiarem korpusu.</summary>
        internal const InlineFormat Exported = InlineFormat.Subscript | InlineFormat.Italic | InlineFormat.Bold;

        /// <summary>Elementy formatowania w treści, od zewnętrznego — kolejność zagnieżdżenia.</summary>
        private static readonly (InlineFormat Format, string Element)[] Elements =
        [
            (InlineFormat.Bold, XmlConverterConstants.Contents.Bold),
            (InlineFormat.Italic, XmlConverterConstants.Contents.Italic),
            (InlineFormat.Subscript, XmlConverterConstants.Contents.Subscript),
        ];

        internal static readonly HashSet<string> ElementNames = [.. Elements.Select(element => element.Element)];

        /// <summary>Formatowanie, które na całym akapicie jest typografią wiersza; indeks dolny jest treścią zawsze.</summary>
        private static readonly InlineFormat[] ParagraphTypography = [InlineFormat.Italic, InlineFormat.Bold];

        // Krótki tekst („i", „Cs") szuka się tylko tuż za poprzednim — dalej powtarza się przypadkiem.
        private const int ShortHost = 8;
        private const int ShortHostWindow = 2000;

        // Zdanie z komendą nowelizacji mieści się w tylu znakach; kopia jego wyrazów stoi w nim.
        private const int CopyLookback = 4000;

        /// <summary>Wynik przebiegu: znaki źródła ze sformatowaniem do przeniesienia i te, które trafiły do eksportu.</summary>
        internal sealed record Report(int SourceCharacters, int PlacedCharacters, int UnplacedHosts);

        internal static Report Embed(XElement actElement, LegalDocument document, InlineFormat exported = Exported)
        {
            var source = Source.Of(document.FormattedSources, exported);
            if (source.FormattedCharacters == 0)
                return new Report(0, 0, 0);

            // Jedno przejście w kolejności dokumentu: węzły treści bez oznaczeń jednostek.
            var hosts = actElement.DescendantNodes().OfType<XText>()
                .Where(text => InlineTextXml.IsHostText(text) && !InlineTextXml.HoldsDesignation(InlineTextXml.Host(text)))
                .ToList();

            var cursor = 0;
            var placed = 0;
            var unplaced = 0;
            foreach (var text in hosts)
            {
                var (normalized, positions) = Normalize(text.Value);
                if (normalized.Length == 0)
                    continue;

                // Cytat w tekście powtarza wyrazy zdania, w którym stoi (instrukcja ręczna, wyrazy z komendy) — szuka się ich
                // najpierw w tekście, który zdanie właśnie zużyło, bez przesuwania kursora.
                var start = IsCopy(text) ? source.FindBefore(normalized, cursor) : -1;
                if (start < 0)
                    start = source.Find(normalized, ref cursor);
                if (start < 0)
                {
                    unplaced++;
                    continue;
                }

                var formats = new InlineFormat[text.Value.Length];
                for (var k = 0; k < positions.Length; k++)
                {
                    formats[positions[k]] = source.Formats[start + k];
                    if (formats[positions[k]] != InlineFormat.None)
                        placed++;
                }
                FillWhitespace(text.Value, formats);
                if (formats.Any(format => format != InlineFormat.None))
                    text.ReplaceWith(Wrapped(text.Value, formats));
            }

            return new Report(source.FormattedCharacters, placed, unplaced);
        }

        /// <summary>Tekst bez odstępów, małymi literami, i pozycja każdego znaku w tekście pierwotnym.</summary>
        private static (string Normalized, int[] Positions) Normalize(string text)
        {
            var sb = new StringBuilder(text.Length);
            var positions = new List<int>(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                if (char.IsWhiteSpace(text[i]))
                    continue;
                sb.Append(char.ToLowerInvariant(text[i]));
                positions.Add(i);
            }

            return (sb.ToString(), [.. positions]);
        }

        /// <summary>Odstęp wewnątrz sformatowanego zakresu („de minimis") należy do niego; na brzegu — nie.</summary>
        private static void FillWhitespace(string text, InlineFormat[] formats)
        {
            for (var i = 0; i < text.Length; i++)
            {
                if (!char.IsWhiteSpace(text[i]))
                    continue;

                var before = i - 1;
                while (before >= 0 && char.IsWhiteSpace(text[before]))
                    before--;
                var after = i + 1;
                while (after < text.Length && char.IsWhiteSpace(text[after]))
                    after++;
                formats[i] = before >= 0 && after < text.Length ? formats[before] & formats[after] : InlineFormat.None;
            }
        }

        private static bool IsCopy(XText text) =>
            text.Ancestors().Any(ancestor => ancestor.Name.LocalName == XmlConverterConstants.Contents.Citation.Text.Element);

        /// <summary>Tekst pocięty na kawałki jednego formatu; sformatowany kawałek w elementach od zewnętrznego.</summary>
        private static List<XNode> Wrapped(string text, InlineFormat[] formats)
        {
            var nodes = new List<XNode>();
            for (var start = 0; start < text.Length;)
            {
                var end = start;
                while (end < text.Length && formats[end] == formats[start])
                    end++;

                XNode node = new XText(text[start..end]);
                for (var i = Elements.Length - 1; i >= 0; i--)
                    if (formats[start].HasFlag(Elements[i].Format))
                        node = Element(Elements[i].Element, node);
                nodes.Add(node);
                start = end;
            }

            return nodes;
        }

        /// <summary>Tekst bloków źródła bez odstępów, małymi literami, z formatem każdego znaku.</summary>
        private sealed class Source
        {
            private readonly string _text;

            internal InlineFormat[] Formats { get; }

            internal int FormattedCharacters { get; }

            private Source(string text, InlineFormat[] formats)
            {
                _text = text;
                Formats = formats;
                FormattedCharacters = formats.Count(format => format != InlineFormat.None);
            }

            internal static Source Of(IReadOnlyList<FormattedSource> sources, InlineFormat exported)
            {
                var sb = new StringBuilder();
                var formats = new List<InlineFormat>();
                foreach (var source in sources)
                {
                    var perChar = new InlineFormat[source.Text.Length];
                    var channel = new bool[source.Text.Length];
                    foreach (var span in source.Spans)
                    {
                        var start = Math.Max(0, span.Start);
                        var end = Math.Min(span.End, perChar.Length);
                        if (span.Format.HasFlag(InlineFormat.Superscript))
                            Array.Fill(channel, true, start, Math.Max(0, end - start));

                        var format = span.Format & ~span.FromParagraphStyle & exported;
                        if (format == InlineFormat.None)
                            continue;
                        for (var i = start; i < end; i++)
                            perChar[i] |= format;
                    }

                    RemoveWholeParagraphFormatting(source.Text, perChar, channel);

                    for (var i = 0; i < source.Text.Length; i++)
                    {
                        if (char.IsWhiteSpace(source.Text[i]))
                            continue;
                        sb.Append(char.ToLowerInvariant(source.Text[i]));
                        formats.Add(perChar[i]);
                    }
                }

                return new Source(sb.ToString(), [.. formats]);
            }

            /// <summary>
            /// Kursywa albo pogrubienie całego akapitu — nadane stylem czy wprost — to typografia wiersza (rodzaj aktu,
            /// tytuł, nagłówek tabeli, numer w kolumnie „lp.”, przepis w nowym brzmieniu tekstu jednolitego), nie
            /// wyróżnienie w tekście (ADR-0118). Odstępy i grupy kanału „[…]” (znaczniki odnośników) się nie liczą.
            /// </summary>
            private static void RemoveWholeParagraphFormatting(string text, InlineFormat[] formats, bool[] channel)
            {
                foreach (var format in ParagraphTypography)
                {
                    var any = false;
                    var whole = true;
                    for (var i = 0; i < text.Length && whole; i++)
                    {
                        if (char.IsWhiteSpace(text[i]) || channel[i])
                            continue;
                        any = true;
                        whole = formats[i].HasFlag(format);
                    }

                    if (any && whole)
                        for (var i = 0; i < formats.Length; i++)
                            formats[i] &= ~format;
                }
            }

            /// <summary>
            /// Miejsce tekstu w źródle: najbliższe za kursorem (krótki tekst — tylko tuż za nim); inaczej jedyne w całym
            /// źródle. Kursor przesuwa się za odnaleziony tekst; -1 — tekstu nie ma albo jest niejednoznaczny.
            /// </summary>
            internal int Find(string text, ref int cursor)
            {
                var window = text.Length < ShortHost ? Math.Min(ShortHostWindow, _text.Length - cursor) : _text.Length - cursor;
                var ahead = window >= text.Length ? _text.IndexOf(text, cursor, window, StringComparison.Ordinal) : -1;
                if (ahead >= 0)
                {
                    cursor = ahead + text.Length;
                    return ahead;
                }

                var first = _text.IndexOf(text, StringComparison.Ordinal);
                return first >= 0 && _text.IndexOf(text, first + 1, StringComparison.Ordinal) < 0 ? first : -1;
            }

            /// <summary>Ostatnie wystąpienie tekstu tuż przed kursorem (w tekście już zużytym); -1, gdy go tam nie ma.</summary>
            internal int FindBefore(string text, int cursor)
            {
                var from = Math.Max(0, cursor - CopyLookback);
                if (cursor - from < text.Length)
                    return -1;
                var found = _text.LastIndexOf(text, cursor - 1, cursor - from, StringComparison.Ordinal);
                return found >= 0 && found + text.Length <= cursor ? found : -1;
            }
        }
    }
}
