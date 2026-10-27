using System.Text.RegularExpressions;
using System.Xml.Linq;
using Saga.Model;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Osadza odnośniki w treści aktu: znacznik „[N)]" w zdaniu albo tytule staje się elementem
    /// <c>odnosnik</c> z treścią przypisu, w miejscu, w którym stoi w akcie. Kolejne odwołanie do tego samego
    /// odnośnika to <c>odsylacz-do-odnosnika</c>. Znacznik, za którym nie stoi odnośnik tego aktu (odnośnik
    /// aktu cytowanego, objaśnienie wpisane akapitem pod tabelą), wychodzi tak, jak stoi w akcie — indeksem
    /// górnym; zapis kanału tekstowego („[1)]") nie trafia do eksportu (ADR-0047).
    /// </summary>
    internal static class FootnoteXmlConverter
    {
        // Znacznik z jednym albo kilkoma numerami: „[3)]", „[1), 2)]", „[13).]".
        private static readonly Regex Marker = FootnoteMarkers.Reference;
        private static readonly Regex Number = new(@"(?<number>\d+[a-z]?)\)", RegexOptions.Compiled);
        private static readonly Regex MarkerWithLeadingSpace = new(@"(?:\s*\[[^\[\]]*\d+[a-z]?\)[^\[\]]*\])+", RegexOptions.Compiled);

        private static readonly HashSet<string> Citations =
        [
            XmlConverterConstants.Contents.Citation.StructElement,
            XmlConverterConstants.Contents.Citation.Text.Element,
        ];

        /// <summary>
        /// Przechodzi treść aktu w kolejności dokumentu i podstawia odnośniki pod ich znaczniki. Każdy odnośnik
        /// trafia pod pierwszy znacznik ze swoim numerem; odnośnik, którego znacznik stoi poza zdaniem i tytułem
        /// (nagłówek jednostki, treść załącznika), zostaje nieosadzony i nie wstrzymuje następnych.
        /// </summary>
        internal static void Embed(XElement actElement, LegalDocument document)
        {
            var registry = new FootnoteResolver(document.Footnotes);
            foreach (var text in InlineTextXml.Texts(actElement))
            {
                if (!Marker.IsMatch(text.Value))
                    continue;

                // W cytacie znacznik z numerem już osadzonym należy do aktu zmienianego, nie do tego aktu.
                var quoted = text.Ancestors().Any(ancestor => Citations.Contains(ancestor.Name.LocalName));
                var rendered = Render(text.Value, registry, allowRepeats: !quoted);

                // Za numerem nagłówka schemat przyjmuje same odnośniki — znacznik bez odnośnika tego aktu znika.
                text.ReplaceWith(InlineTextXml.HoldsDesignation(InlineTextXml.Host(text))
                    ? rendered.OfType<XElement>().Where(IsFootnote)
                    : rendered);
            }
        }

        /// <summary>
        /// Odnośnik w wyrazach nowych zmiany w tekście („wyrazy „…” zastępuje się wyrazami „… z późn. zm.¹⁾”") — wymagany przy
        /// „z późn. zm." (§ 158 ust. 7 ZTP) — przechodzi do aktu zmienianego razem z wyrazami: w <c>nowy-tekst</c> pod
        /// znacznik trafia fragment XML odnośnika (ADR-0105, ustalenie z 2026-10-20). Fragment nie deklaruje przestrzeni
        /// nazw — wklejony należy do aktu, w który go wklejono. Znak „&lt;" i „&amp;" koduje sam zapis atrybutu.
        /// </summary>
        internal static void EmbedInNewWords(XElement actElement, LegalDocument document)
        {
            if (document.Footnotes.Count == 0)
                return;

            var byMarker = document.Footnotes.GroupBy(footnote => footnote.Marker).ToDictionary(group => group.Key, group => group.First());
            foreach (var attribute in actElement.Descendants().Attributes()
                .Where(attribute => attribute.Name.LocalName == XmlConverterConstants.Contents.Instruction.NewTextAttribute && Marker.IsMatch(attribute.Value)))
            {
                attribute.Value = Marker.Replace(attribute.Value, marker =>
                {
                    var numbers = Number.Matches(marker.Value).Select(number => number.Groups["number"].Value).ToList();
                    return numbers.Count > 0 && numbers.All(byMarker.ContainsKey)
                        ? string.Concat(numbers.Select(number => Fragment(ToXml(byMarker[number]))))
                        : marker.Value;
                });
            }
        }

        private static readonly Regex NamespaceDeclaration = new(@"\s+xmlns(?::\w+)?=""[^""]*""", RegexOptions.Compiled);

        // Przedrostek „gd:" jak w akcie docelowym — bez deklaracji zapis wymyśliłby własny („p1:").
        private static string Fragment(XElement footnote)
        {
            footnote.SetAttributeValue(XNamespace.Xmlns + "gd", XmlConverterConstants.GuidNamespace.NamespaceName);
            return NamespaceDeclaration.Replace(footnote.ToString(SaveOptions.DisableFormatting), string.Empty);
        }

        private static bool IsFootnote(XElement element) =>
            element.Name.LocalName is XmlConverterConstants.Contents.Odnosnik.Element
                or XmlConverterConstants.Contents.Odnosnik.ReferenceElement;

        /// <summary>Tekst bez znaczników odnośników — do pól, które niosą sam tytuł.</summary>
        internal static string WithoutMarkers(string text) => MarkerWithLeadingSpace.Replace(text, string.Empty).TrimEnd();

        private static IEnumerable<XNode> Render(string value, FootnoteResolver registry, bool allowRepeats)
        {
            var nodes = new List<XNode>();
            var cursor = 0;
            foreach (Match marker in Marker.Matches(value))
            {
                // Żaden numer nie jest odnośnikiem tego aktu: sam znacznik wychodzi indeksem górnym, jak w akcie;
                // nawias kwadratowy z innym tekstem zostaje tekstem.
                var inside = RenderMarker(marker.Value, registry, allowRepeats)
                    ?? (FootnoteMarkers.Bare.IsMatch(marker.Value[1..^1])
                        ? [Element(XmlConverterConstants.Contents.Superscript, registry.AsPrinted(marker.Value[1..^1].Trim()))]
                        : null);
                if (inside is null)
                    continue;

                if (marker.Index > cursor)
                    nodes.Add(new XText(value[cursor..marker.Index]));
                nodes.AddRange(inside);
                cursor = marker.Index + marker.Length;
            }

            if (cursor < value.Length)
                nodes.Add(new XText(value[cursor..]));
            return nodes;
        }

        /// <summary>
        /// Zawartość jednego znacznika: odnośnik albo odsyłacz za każdy rozpoznany numer, a to, co stoi między
        /// numerami („, "), w indeksie górnym jak w akcie. Null, gdy żaden numer nie jest odnośnikiem tego aktu.
        /// </summary>
        private static List<XNode>? RenderMarker(string marker, FootnoteResolver registry, bool allowRepeats)
        {
            var inner = marker[1..^1];
            var nodes = new List<XNode>();
            var cursor = 0;

            foreach (Match number in Number.Matches(inner))
            {
                var symbol = number.Groups["number"].Value;
                XElement? element =
                    registry.First(symbol) is { } footnote ? ToXml(footnote)
                    : allowRepeats && registry.Repeated(symbol) is { } repeated ? ToReference(repeated)
                    : null;
                if (element is null)
                    continue;

                AddSuperscript(nodes, registry.AsPrinted(inner[cursor..number.Index]));
                nodes.Add(element);
                cursor = number.Index + number.Length;
            }

            if (cursor == 0)
                return null;

            AddSuperscript(nodes, registry.AsPrinted(inner[cursor..]));
            return nodes;
        }

        // Przecinek między dwoma odnośnikami stoi w akcie w indeksie górnym; odstęp zostaje zwykłym tekstem.
        private static void AddSuperscript(List<XNode> nodes, string between)
        {
            var separator = between.Trim();
            if (separator.Length > 0)
                nodes.Add(Element(XmlConverterConstants.Contents.Superscript, separator));
            if (between.Length > 0 && char.IsWhiteSpace(between[^1]))
                nodes.Add(new XText(" "));
        }

        private static XElement ToXml(Footnote footnote)
        {
            var element = Element(XmlConverterConstants.Contents.Odnosnik.Element,
                new XAttribute(XmlConverterConstants.Contents.Odnosnik.IdAttribute, footnote.Marker),
                new XAttribute(XmlConverterConstants.Contents.Odnosnik.SymbolAttribute, footnote.Symbol),
                new XAttribute(XmlConverterConstants.Contents.GUID, footnote.Guid.ToString()));

            FootnoteContentXml.Fill(element, footnote.Paragraphs);
            return element;
        }

        /// <summary>Kolejne odwołanie do odnośnika osadzonego wcześniej (w tekstach jednolitych częste).</summary>
        private static XElement ToReference(Footnote footnote) =>
            Element(XmlConverterConstants.Contents.Odnosnik.ReferenceElement,
                new XAttribute(XmlConverterConstants.Contents.Odnosnik.IdAttribute, footnote.Marker),
                new XAttribute(XmlConverterConstants.Contents.Odnosnik.SymbolAttribute, footnote.Symbol));
    }
}
