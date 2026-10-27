using System.Text.RegularExpressions;
using System.Xml.Linq;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Adres internetowy zapisany w treści („https://www.gov.pl/…", „www.bip.gov.pl") stoi w elemencie <c>h</c>
    /// (ADR-0119). Kropka, przecinek, nawias i cudzysłów, którymi kończy się zdanie po adresie, do adresu nie należą.
    /// Hiperłącza z pliku Worda nie są źródłem — to zwykle pozostałości kopiowania z systemów informacji prawnej.
    /// </summary>
    internal static class HyperlinkXml
    {
        private static readonly Regex Url = new($@"(?:https?://|www\.)[^\s<>{QuotationMarks.Any}'’]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly char[] TrailingPunctuation = ['.', ',', ';', ':', '!', '?', ')', ']'];

        internal static void Embed(XElement actElement)
        {
            foreach (var text in InlineTextXml.Texts(actElement))
            {
                if (InlineTextXml.HoldsDesignation(InlineTextXml.Host(text)))
                    continue;

                var matches = Url.Matches(text.Value);
                if (matches.Count == 0)
                    continue;

                var nodes = new List<XNode>();
                var cursor = 0;
                foreach (Match match in matches)
                {
                    var url = match.Value.TrimEnd(TrailingPunctuation);
                    if (url.Length <= "www.".Length)
                        continue;

                    nodes.Add(new XText(text.Value[cursor..match.Index]));
                    nodes.Add(Element(XmlConverterConstants.Contents.Hyperlink, url));
                    cursor = match.Index + url.Length;
                }

                if (nodes.Count == 0)
                    continue;
                nodes.Add(new XText(text.Value[cursor..]));
                text.ReplaceWith(nodes);
            }
        }
    }
}
