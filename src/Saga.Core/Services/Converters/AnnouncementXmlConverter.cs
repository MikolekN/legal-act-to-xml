using System.Xml.Linq;
using Saga.Model;
using Saga.Model.EditorialUnits;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Punkty obwieszczenia tekstu jednolitego: <c>punkt-OTJ</c> z podpunktami i częścią wspólną podpunktów.
    /// Adres liczy się od obwieszczenia (<c>pkt_1__ppkt_2</c>), nie od ogłaszanego aktu; jednostki obwieszczenia
    /// nie niosą dat — nie są przepisami aktu.
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
            EmbeddedContentXmlConverter.AppendEmbedded(element, subPoint);

            return element;
        }

        private static XElement Unit(string name, BaseEntity unit) =>
            Element(name,
                UnitAddressXml.Address(unit, UnitAddressXml.HasDesignation(unit)),
                new XAttribute(XmlConverterConstants.Contents.GUID, unit.Guid.ToString()),
                UnitAddressXml.Number(unit));
    }
}
