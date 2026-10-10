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

            AppendLetters(parentElement, point, isPathological: true, previousSentenceContext);
            HandleCommonPart(point, parentElement, previousSentenceContext);
        }

        // Litery z częściami wspólnymi, które przerywają ich wyliczenie, w kolejności z aktu.
        private static void AppendLetters(XElement element, Point point, bool isPathological, SentenceContext? context)
        {
            for (var i = 0; i < point.Letters.Count; i++)
            {
                LetterXmlConverter.AppendTo(element, point.Letters[i], isPathological, context);
                AppendInterrupting(point, element, itemsSoFar: i + 1, context);
            }
        }

        internal static XElement ToXml(Point point, bool isParentPathological = false, SentenceContext? previousSentenceContext = null)
        {
            var isPathological = IsPathological(point, isParentPathological);

            var pointElement = GetPointElement(isPathological);

            pointElement.Add(UnitAddressXml.Address(point, UnitAddressXml.HasDesignation(point)));
            pointElement.Add(new XAttribute(XmlConverterConstants.Contents.GUID, point.Guid.ToString()));

            UnitLifecycleXml.Apply(pointElement, point);

            pointElement.Add(UnitAddressXml.Number(point));

            var textSectionElement = Element(XmlConverterConstants.Contents.TextSection);
            SentenceContext? contextToPass = SentenceProcessor.Process(point.TextSegments, previousSentenceContext, textSectionElement);
            AmendmentXmlConverter.ToXml(point.Amendment, textSectionElement.LastNode);
            if (!string.IsNullOrEmpty(point.ContentText))
            {
                pointElement.Add(textSectionElement);
            }

            EmbeddedContentXmlConverter.AppendEmbedded(pointElement, point);

            AppendLetters(pointElement, point, isPathological, contextToPass);

            HandleCommonPart(point, pointElement, contextToPass);

            return pointElement;
        }

        private static XElement GetPointElement(bool isPathological) =>
            Element(isPathological
                ? XmlConverterConstants.Contents.NonZtpPoint
                : XmlConverterConstants.Contents.Point);

        // Wyliczenie ma co najmniej dwie litery; jedna litera, poziom pominięty (tirety wprost w punkcie)
        // i punkt bez odczytanego oznaczenia są spoza ZTP.
        private static bool IsPathological(Point point, bool isParentPathological) =>
            isParentPathological || point.Letters.Count == 1 || point.Letters.Any(letter => letter.IsImplicit) ||
            !UnitAddressXml.HasDesignation(point);
    }
}
