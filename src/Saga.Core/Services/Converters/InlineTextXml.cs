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
            XmlConverterConstants.ActHeading.Kind,
            XmlConverterConstants.ActHeading.Subject,

            // Organ wydający i podstawa prawna aktu wykonawczego: tu stoją jego pierwsze odnośniki
            // („MINISTRA FINANSÓW[1)]" — zakres działania ministra; „… z późn. zm.[2)]" — zmiany ustawy).
            XmlConverterConstants.Announcements.Author,
            XmlConverterConstants.SectionBasedActs.LegalBasis,
            XmlConverterConstants.Contents.Citation.Text.Content,
            XmlConverterConstants.Contents.ContinuationFragment,
            XmlConverterConstants.Contents.SpecialContent,

            // Treść załącznika i komórek tabel — tekst bez podziału na zdania.
            XmlConverterConstants.Attachments.AttachedTo,
            XmlConverterConstants.Attachments.TitleElement,
            XmlConverterConstants.Table.ColumnTitle,
            XmlConverterConstants.Table.Number,
            XmlConverterConstants.Table.PlainText,
            XmlConverterConstants.Table.Enumeration1,
            XmlConverterConstants.Table.Enumeration2,
            XmlConverterConstants.Table.Enumeration3,
            XmlConverterConstants.Table.Enumeration4,
            XmlConverterConstants.Table.Description,
        ];

        // Indeks górny w kanale tekstowym: nawias tuż za literą albo cyfrą — „477[9]", „2[1a]", „m[2]". Znacznik
        // odnośnika ma w nawiasie „)" i tu nie pasuje. Indeksem górnym jest też znacznik objaśnienia, za którym nie
        // stoi odnośnik tego aktu (ADR-0047): gwiazdka („[*)]", „[(**)]", „[*]"), liczba rzymska odnośnika do
        // odnośnika („[I)]") i sam nawias przy gwiazdce zwykłej („S*[)]"). Wykładnik we wzorze zapisanym
        // tekstem stoi też za nawiasem i bywa ułamkiem dziesiętnym („(0,001 × średnia cena miedzi)[2,5]"). Nawias z innym
        // tekstem („[zł/GJ]", „[0,033 × …]") zostaje.
        private static readonly Regex Superscript = new(
            @"(?<=[\p{L}\d])\[(?<index>[0-9a-z]{1,4})\]|(?<=[\p{L}\d)])\[(?<index>\d+,\d+)\]|(?<=\))\[(?<index>\d{1,2})\]"
            + @"|\[(?<index>\(?\*{1,3}\)?|\(?[IVX]{1,4}\)|\))\]"
            // Znacznik, któremu nie odpowiada osadzony odnośnik — w treści odnośnika („z późn. zm.[1)]", „[a)]") albo
            // z gwiazdkami („[3(**)]") — wychodzi indeksem górnym; kanał tekstowy z nawiasem do eksportu nie trafia. Tylko kształt
            // znacznika: wzór w nawiasie („[D_2022 x (A + B)/n)]") zostaje tekstem.
            + @"|\[(?<index>\d+[a-z]?\)(?:\s*[,;]?\s*\d+[a-z]?\))*[.,;]?|\d*\(\*{1,4}\)|[a-z]\))\]",
            RegexOptions.Compiled);

        private const string UnitHeadingPrefix = "naglowek-";

        /// <summary>
        /// Węzły tekstowe treści aktu (bez metadanych), w kolejności dokumentu — także wewnątrz formatowania znakowego
        /// (<c>b</c>, <c>i</c>, <c>sub</c> — ADR-0117), które stoi w treści hosta.
        /// </summary>
        internal static List<XText> Texts(XElement actElement) =>
            actElement.DescendantNodes().OfType<XText>().Where(IsHostText).ToList();

        /// <summary>Węzeł tekstowy treści aktu, który przyjmuje elementy wewnątrz tekstu (zob. <see cref="Texts"/>).</summary>
        internal static bool IsHostText(XText text) =>
            Host(text) is var parent
            && (Hosts.Contains(parent.Name.LocalName) || IsUnitHeading(parent) || HoldsDesignation(parent))
            && !parent.Ancestors().Any(a => a.Name.LocalName == XmlConverterConstants.Metadata.Element);

        /// <summary>Element, w którego treści stoi węzeł — z pominięciem formatowania znakowego wokół niego.</summary>
        internal static XElement Host(XText text)
        {
            var parent = text.Parent!;
            while (InlineFormattingXml.ElementNames.Contains(parent.Name.LocalName) && parent.Parent is { } owner)
                parent = owner;
            return parent;
        }

        /// <summary>
        /// Nagłówek jednostki systematyzacyjnej i numer załącznika: tekstu własnego nie mają, ale za numerem
        /// mieszczą odnośniki oznaczenia (ADR-0094) — zostawione tam jako znaczniki tekstem.
        /// </summary>
        internal static bool HoldsDesignation(XElement parent) =>
            parent.Name.LocalName.StartsWith(UnitHeadingPrefix, StringComparison.Ordinal)
            || parent.Name.LocalName == XmlConverterConstants.Attachments.NumberElement;

        // Tytuł jednostki systematyzacyjnej stoi w „p" bez zdań („naglowek-dzialu" → „p") — w tekstach jednolitych
        // niesie odnośnik o brzmieniu tytułu. Zwykłe „p" tekstu nie ma: mieści zdania.
        private static bool IsUnitHeading(XElement parent) =>
            parent.Name.LocalName == XmlConverterConstants.Contents.TextSection
            && parent.Parent is { } owner
            && owner.Name.LocalName.StartsWith(UnitHeadingPrefix, StringComparison.Ordinal);

        /// <summary>
        /// Indeks górny w tekście zdania staje się elementem <c>sup</c> („art. 477[9]" → 477, sup 9).
        /// W <c>nr</c> i w eId zostaje notacja z nawiasem — tak wymaga schemat.
        /// </summary>
        internal static void EmbedSuperscripts(XElement actElement)
        {
            foreach (var text in Texts(actElement))
            {
                // Wzorzec patrzy na znak przed nawiasem — także w poprzednim węźle za formatowaniem („x<sub>1</sub>[2]").
                var context = PrecedingCharacter(text) is { } preceding ? preceding + text.Value : text.Value;
                var offset = context.Length - text.Value.Length;
                var matches = Superscript.Matches(context).Where(match => match.Index >= offset).ToList();
                if (matches.Count == 0)
                    continue;

                var nodes = new List<XNode>();
                var cursor = 0;
                foreach (var match in matches)
                {
                    var index = match.Index - offset;
                    nodes.Add(new XText(text.Value[cursor..index]));
                    nodes.Add(Element(XmlConverterConstants.Contents.Superscript, match.Groups["index"].Value));
                    cursor = index + match.Length;
                }

                if (cursor < text.Value.Length)
                    nodes.Add(new XText(text.Value[cursor..]));
                text.ReplaceWith(nodes);
            }
        }

        /// <summary>
        /// Znak tuż przed węzłem w treści hosta — w poprzednim węźle tekstowym albo na końcu poprzedzającego formatowania
        /// znakowego; null, gdy przed węzłem stoi inny element (odnośnik, indeks górny) albo nic.
        /// </summary>
        private static char? PrecedingCharacter(XText text)
        {
            XNode node = text;
            while (true)
            {
                switch (node.PreviousNode)
                {
                    case XText { Value.Length: > 0 } previous:
                        return previous.Value[^1];
                    case XElement element when InlineFormattingXml.ElementNames.Contains(element.Name.LocalName):
                        return element.DescendantNodes().OfType<XText>().LastOrDefault() is { Value.Length: > 0 } last ? last.Value[^1] : null;
                    case null when node.Parent is { } parent && InlineFormattingXml.ElementNames.Contains(parent.Name.LocalName):
                        node = parent;
                        continue;
                    default:
                        return null;
                }
            }
        }
    }
}
