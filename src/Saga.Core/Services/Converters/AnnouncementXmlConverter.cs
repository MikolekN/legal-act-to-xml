using System.Xml.Linq;
using Saga.Model;
using Saga.Model.EditorialUnits;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Punkty obwieszczenia tekstu jednolitego: <c>punkt-OTJ</c> z podpunktami i częścią wspólną podpunktów.
    /// Adres liczy się od obwieszczenia (<c>pkt_1__ppkt_2</c>), nie od ogłaszanego aktu; jednostki obwieszczenia
    /// nie niosą dat — nie są przepisami aktu. Przytoczone przepisy (<see cref="Quotation"/>) stoją w zdaniu
    /// jednostki jako <c>tresc-strukt</c>, z adresami w akcie, z którego pochodzą.
    /// </summary>
    internal static class AnnouncementXmlConverter
    {
        internal static IEnumerable<XElement> Points(Announcement announcement) => announcement.Points.Select(Point);

        private static XElement Point(Paragraph point)
        {
            var element = Unit(XmlConverterConstants.Announcements.Point, point);

            var text = Element(XmlConverterConstants.Contents.TextSection);
            var context = SentenceProcessor.Process(point.TextSegments, null, text);
            element.Add(text);
            AppendQuotations(text, point);
            EmbeddedContentXmlConverter.AppendEmbedded(element, point);

            foreach (var subPoint in point.Points)
                element.Add(SubPoint(subPoint, context));

            // Część wspólna wychodzi przez konwerter jednostek aktu; w obwieszczeniu ma własną nazwę elementu.
            CommonPartXmlConverter.HandleCommonPart(point, element, context);
            foreach (var commonPart in element.Elements().Where(e => e.Name.LocalName == XmlConverterConstants.Contents.CommonPart.Point).ToList())
                commonPart.Name = commonPart.Name.Namespace + XmlConverterConstants.Announcements.SubPointCommonPart;

            return element;
        }

        private static XElement SubPoint(Point subPoint, SentenceContext? context)
        {
            var element = Unit(XmlConverterConstants.Announcements.SubPoint, subPoint);

            var text = Element(XmlConverterConstants.Contents.TextSection);
            SentenceProcessor.Process(subPoint.TextSegments, context, text);
            element.Add(text);
            AppendQuotations(text, subPoint);
            EmbeddedContentXmlConverter.AppendEmbedded(element, subPoint);

            return element;
        }

        /// <summary>
        /// Przytoczenia jednostki trafiają do jej ostatniego zdania („…, który stanowi: „Art. 19. …";"), jak
        /// w przykładzie autorów schematu; interpunkcja za cudzysłowem zostaje w zdaniu. Akt, z którego pochodzą
        /// jednostki, niesie adnotacja elementu — schemat nie daje <c>tresc-strukt</c> atrybutu aktu.
        /// </summary>
        private static void AppendQuotations(XElement text, BaseEntity unit)
        {
            foreach (var quotation in unit.Continuation.Select(block => block.Quotation).OfType<Quotation>())
            {
                var quoted = Element(XmlConverterConstants.Contents.Citation.QuotedStructElement,
                    new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.QuotationAttribute,
                        XmlConverterConstants.Contents.Citation.Quotation.QuotationBoth),
                    new XAttribute(XmlConverterConstants.Contents.GUID, Guid.NewGuid().ToString()));
                quoted.Add(quotation.Content.Articles.Select(article => ArticleXmlConverter.ToXml(article)));
                quoted.AddAnnotation(new ActPrefixXml.QuotedAct(quotation.SourceAct?.GetELIStrings().FirstOrDefault()));

                var sentence = text.Elements().LastOrDefault(e => e.Name.LocalName == XmlConverterConstants.Contents.Sentence.Element);
                if (sentence is null)
                {
                    text.Add(quoted);
                    continue;
                }

                sentence.Add(quoted);
                if (quotation.TrailingPunctuation is { Length: > 0 } trailing)
                    sentence.Add(trailing);
            }
        }

        private static XElement Unit(string name, BaseEntity unit) =>
            Element(name,
                UnitAddressXml.Address(unit, UnitAddressXml.HasDesignation(unit)),
                new XAttribute(XmlConverterConstants.Contents.GUID, unit.Guid.ToString()),
                UnitAddressXml.Number(unit));
    }
}
