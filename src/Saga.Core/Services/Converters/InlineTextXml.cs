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
            + @"|\[(?<index>\(?\*{1,3}\)?|\(?[IVX]{1,4}\)|\))\]",
            RegexOptions.Compiled);

        private const string UnitHeadingPrefix = "naglowek-";

        /// <summary>Węzły tekstowe treści aktu (bez metadanych), w kolejności dokumentu.</summary>
        internal static List<XText> Texts(XElement actElement) =>
            actElement.DescendantNodes().OfType<XText>()
                .Where(text => text.Parent is { } parent
                    && (Hosts.Contains(parent.Name.LocalName) || IsUnitHeading(parent) || HoldsDesignation(parent))
                    && !parent.Ancestors().Any(a => a.Name.LocalName == XmlConverterConstants.Metadata.Element))
                .ToList();

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
