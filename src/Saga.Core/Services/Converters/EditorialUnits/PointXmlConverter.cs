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
        /// <summary>
        /// Dopisuje punkt do jednostki nadrzędnej. Punkt niejawny (poziom pominięty w akcie) nie ma elementu:
        /// jego litery i część wspólna stają wprost w ustępie, jako jednostki spoza ZTP, i ciągną zdanie ustępu.
        /// </summary>
        internal static void AppendTo(XElement parentElement, Point point, bool isParentPathological, SentenceContext? previousSentenceContext)
        {
            if (!point.IsImplicit)
            {
                parentElement.Add(ToXml(point, isParentPathological, previousSentenceContext));
                return;
            }

            foreach (var letter in point.Letters)
                LetterXmlConverter.AppendTo(parentElement, letter, isParentPathological: true, previousSentenceContext);

            HandleCommonPart(point, parentElement, previousSentenceContext);
        }

        internal static XElement ToXml(Point point, bool isParentPathological = false, SentenceContext? previousSentenceContext = null)
        {
            var isPathological = IsPathological(point, isParentPathological);

            var pointElement = GetPointElement(isPathological);

            pointElement.Add(new XAttribute(XmlConverterConstants.Contents.eId, point.Id));
            pointElement.Add(new XAttribute(XmlConverterConstants.Contents.GUID, point.Guid.ToString()));

            UnitLifecycleXml.Apply(pointElement, point);

            pointElement.Add(Element(XmlConverterConstants.Contents.Num, point.Number?.Designation ?? "UNKNOWN"));

            var textSectionElement = Element(XmlConverterConstants.Contents.TextSection);
            SentenceContext? contextToPass = SentenceProcessor.Process(point.TextSegments, previousSentenceContext, textSectionElement);
            AmendmentXmlConverter.ToXml(point.Amendment, textSectionElement.LastNode);
            if (!string.IsNullOrEmpty(point.ContentText))
            {
                pointElement.Add(textSectionElement);
            }

            EmbeddedContentXmlConverter.AppendEmbedded(pointElement, point);

            foreach (var letter in point.Letters)
                LetterXmlConverter.AppendTo(pointElement, letter, isPathological, contextToPass);

            HandleCommonPart(point, pointElement, contextToPass);

            return pointElement;
        }

        private static XElement GetPointElement(bool isPathological) =>
            Element(isPathological
                ? XmlConverterConstants.Contents.NonZtpPoint
                : XmlConverterConstants.Contents.Point);

        // Wyliczenie ma co najmniej dwie litery; jedna litera i poziom pominięty (tirety wprost w punkcie) są spoza ZTP.
        private static bool IsPathological(Point point, bool isParentPathological) =>
            isParentPathological || point.Letters.Count == 1 || point.Letters.Any(letter => letter.IsImplicit);
    }
}
