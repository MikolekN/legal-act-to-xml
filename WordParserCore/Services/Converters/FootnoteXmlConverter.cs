using System.Text.RegularExpressions;
using System.Xml.Linq;
using ModelDto;
using static WordParserCore.XLinqWrappers;

namespace WordParserCore.Services.Converters
{
    /// <summary>
    /// Konwertuje odnośniki (przypisy dolne) na XML.
    /// </summary>
    internal static class FootnoteXmlConverter
    {
        /// <summary>Marker odwołania do odnośnika w tekście, np. "[3)]".</summary>
        private static readonly Regex MarkerPattern = new(@"\[(\d+)\)\]", RegexOptions.Compiled);

        // Stan per wątek: konwersja dokumentu przebiega synchronicznie na jednym wątku,
        // a ThreadStatic zapobiega interferencji przy równoległym uruchamianiu testów.
        // Klucz: wewnętrzny identyfikator Word (w:id) — taki niesie marker "[id)]" w tekście.
        [ThreadStatic] private static Dictionary<int, Footnote>? _footnotes;
        [ThreadStatic] private static HashSet<int>? _emitted;

        /// <summary>Inicjuje rejestr odnośników dla konwertowanego dokumentu.</summary>
        internal static void Initialize(IEnumerable<Footnote> footnotes)
        {
            _footnotes = footnotes.ToDictionary(f => f.Id);
            _emitted = new HashSet<int>();
        }

        /// <summary>
        /// Buduje zawartość mieszaną (tekst + odnośniki) z tekstu zawierającego markery "[N)]".
        /// </summary>
        internal static IEnumerable<object> BuildTextContent(string text)
        {
            if (string.IsNullOrEmpty(text) || _footnotes is not { Count: > 0 } || !text.Contains('['))
                return new object[] { text };

            var nodes = new List<object>();
            var position = 0;

            foreach (Match marker in MarkerPattern.Matches(text))
            {
                var id = int.Parse(marker.Groups[1].Value);
                if (!_footnotes.TryGetValue(id, out var footnote))
                    continue;

                if (marker.Index > position)
                    nodes.Add(text[position..marker.Index]);
                nodes.Add(BuildFootnoteNode(footnote));
                position = marker.Index + marker.Length;
            }

            if (position < text.Length)
                nodes.Add(text[position..]);

            return nodes;
        }

        /// <summary>
        /// Buduje element odnośnika: pełną treść przy pierwszym wystąpieniu,
        /// odsyłacz przy kolejnych wystąpieniach tego samego odnośnika.
        /// </summary>
        private static XElement BuildFootnoteNode(Footnote footnote)
        {
            if (!_emitted!.Add(footnote.Id))
                return Element(XmlConverterConstants.Contents.Odnosnik.ReferenceElement,
                    new XAttribute(XmlConverterConstants.Contents.Odnosnik.IdAttribute, footnote.Number),
                    new XAttribute(XmlConverterConstants.Contents.Odnosnik.SymbolAttribute, footnote.Number));

            var sentence = Element(XmlConverterConstants.Contents.Sentence.Element,
                new XAttribute(XmlConverterConstants.Contents.Sentence.Number, 1),
                new XAttribute(XmlConverterConstants.Contents.Sentence.GUID, Guid.NewGuid().ToString()),
                new XAttribute(XmlConverterConstants.Contents.Sentence.Fragment, 1),
                new XAttribute(XmlConverterConstants.Contents.Sentence.FragmentGUID, Guid.NewGuid().ToString()),
                footnote.Content);

            return Element(XmlConverterConstants.Contents.Odnosnik.Element,
                new XAttribute(XmlConverterConstants.Contents.Odnosnik.IdAttribute, footnote.Number),
                new XAttribute(XmlConverterConstants.Contents.Odnosnik.SymbolAttribute, footnote.Number),
                new XAttribute(XmlConverterConstants.Contents.GUID, Guid.NewGuid().ToString()),
                Element(XmlConverterConstants.Contents.TextSection, sentence));
        }
    }
}
