using System.Text.RegularExpressions;
using System.Xml.Linq;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Tekst jednostek jest w modelu płaskim napisem; elementy wewnątrz tekstu (odnośnik, indeks górny)
    /// powstają dopiero w eksporcie, przebiegiem po zbudowanej treści aktu.
    /// </summary>
    internal static class InlineTextXml
    {
        /// <summary>Elementy, których treść tekstowa przyjmuje elementy wewnątrz tekstu.</summary>
        private static readonly HashSet<string> Hosts =
        [
            XmlConverterConstants.Contents.Sentence.Element,
            XmlConverterConstants.ActHeading.Subject,
            XmlConverterConstants.Contents.Citation.Text.Content,
        ];

        // Indeks górny w kanale tekstowym: nawias tuż za literą albo cyfrą — „477[9]", „2[1a]", „m[2]".
        // Znacznik odnośnika ma w nawiasie „)" i tu nie pasuje.
        private static readonly Regex Superscript = new(
            @"(?<=[\p{L}\d])\[(?<index>[0-9a-z]{1,4})\]", RegexOptions.Compiled);

        /// <summary>Węzły tekstowe treści aktu (bez metadanych), w kolejności dokumentu.</summary>
        internal static List<XText> Texts(XElement actElement) =>
            actElement.DescendantNodes().OfType<XText>()
                .Where(text => text.Parent is { } parent
                    && Hosts.Contains(parent.Name.LocalName)
                    && !parent.Ancestors().Any(a => a.Name.LocalName == XmlConverterConstants.Metadata.Element))
                .ToList();

        /// <summary>
        /// Indeks górny w tekście zdania staje się elementem <c>sup</c> („art. 477[9]" → 477, sup 9).
        /// W <c>nr</c> i w eId zostaje notacja z nawiasem — tak wymaga schemat.
        /// </summary>
        internal static void EmbedSuperscripts(XElement actElement)
        {
            foreach (var text in Texts(actElement))
            {
                var matches = Superscript.Matches(text.Value);
                if (matches.Count == 0)
                    continue;

                var nodes = new List<XNode>();
                var cursor = 0;
                foreach (Match match in matches)
                {
                    nodes.Add(new XText(text.Value[cursor..match.Index]));
                    nodes.Add(Element(XmlConverterConstants.Contents.Superscript, match.Groups["index"].Value));
                    cursor = match.Index + match.Length;
                }

                if (cursor < text.Value.Length)
                    nodes.Add(new XText(text.Value[cursor..]));
                text.ReplaceWith(nodes);
            }
        }
    }
}
