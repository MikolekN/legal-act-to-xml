using System.Xml.Linq;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Treść mieszana (tekst z elementami: zdanie, komórka tabeli, tytuł) zapisuje się bez wcięć. Zapis z wcięciami uznaje
    /// treść elementu za mieszaną dopiero po pierwszym tekście, więc element na początku — indeks górny, odnośnik,
    /// formatowanie znakowe — dostawał przed sobą nowy wiersz i wcięcie, które stawały się częścią tekstu
    /// („<c>&lt;zdanie&gt;⏎    &lt;sup&gt;1)&lt;/sup&gt; Niniejsza</c>"). Pusty węzeł tekstu na początku przełącza zapis w tryb
    /// treści mieszanej od pierwszego węzła — jak w przykładach autorów schematu (<c>&lt;zdanie&gt;&lt;sup&gt;</c>).
    /// </summary>
    internal static class MixedContentXml
    {
        /// <summary>Elementy wewnątrz tekstu, od których treść tekstowa może się zaczynać.</summary>
        private static readonly HashSet<string> Inline =
        [
            XmlConverterConstants.Contents.Superscript,
            XmlConverterConstants.Contents.Subscript,
            XmlConverterConstants.Contents.Italic,
            XmlConverterConstants.Contents.Bold,
            XmlConverterConstants.Contents.Odnosnik.Element,
            XmlConverterConstants.Contents.Odnosnik.ReferenceElement,
            XmlConverterConstants.Contents.PenalSanction,
            XmlConverterConstants.Contents.Hyperlink,
        ];

        internal static void KeepUnindented(XElement root)
        {
            foreach (var element in root.DescendantsAndSelf().ToList())
            {
                if (element.FirstNode is not XElement first)
                    continue;

                if (Inline.Contains(first.Name.LocalName)
                    || element.Nodes().OfType<XText>().Any(text => !string.IsNullOrWhiteSpace(text.Value)))
                    element.AddFirst(new XText(string.Empty));
            }
        }
    }
}
