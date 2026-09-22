using System.Xml.Linq;
using Saga.Model.EditorialUnits;
using static Saga.Core.Services.Converters.CommonPartXmlConverter;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Konwertuje model Point na element XML.
    /// </summary>
    internal static class PointXmlConverter
    {
        internal static XElement ToXml(Point point, bool isParentPathological = false, SentenceContext? previousSentenceContext = null)
        {
            var isPathological = IsPathological(point, isParentPathological);

            var pointElement = GetPointElement(isPathological);

            pointElement.Add(new XAttribute(XmlConverterConstants.Contents.eId, point.Id));
            pointElement.Add(new XAttribute(XmlConverterConstants.Contents.GUID, point.Guid.ToString()));

            UnitLifecycleXml.Apply(pointElement, point);

            pointElement.Add(Element(XmlConverterConstants.Contents.Num, point.Number?.Value ?? "UNKNOWN"));

            var textSectionElement = Element(XmlConverterConstants.Contents.TextSection);
            SentenceContext? contextToPass = SentenceProcessor.Process(point.TextSegments, previousSentenceContext, textSectionElement);
            AmendmentXmlConverter.ToXml(point.Amendment, textSectionElement.LastNode);
            if (!string.IsNullOrEmpty(point.ContentText))
            {
                pointElement.Add(textSectionElement);
            }

            EmbeddedContentXmlConverter.AppendEmbedded(pointElement, point);

            foreach (var letter in point.Letters)
                pointElement.Add(LetterXmlConverter.ToXml(letter, isPathological, contextToPass));

            HandleCommonPart(point, pointElement, contextToPass);

            return pointElement;
        }

        private static XElement GetPointElement(bool isPathological) =>
            Element(isPathological
                ? XmlConverterConstants.Contents.PatoPoint
                : XmlConverterConstants.Contents.Point);

        private static bool IsPathological(Point point, bool isParentPathological)
        {
            // TODO: unhandled if the structure of point is not respected - code doesn't expect that e.g. tiret can appear in a point
            if (isParentPathological || point.Letters.Count == 1) // Every point is expected to have no letters or more than 1 letter
                return true;
            else
                return false;
        }
    }
}
