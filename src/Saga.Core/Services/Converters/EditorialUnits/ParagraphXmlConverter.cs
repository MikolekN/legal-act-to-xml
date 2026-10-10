using System.Xml.Linq;
using Saga.Model.EditorialUnits;
using static Saga.Core.Services.Converters.CommonPartXmlConverter;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Konwertuje model Paragraph na element XML.
    /// </summary>
    internal static class ParagraphXmlConverter
    {
        internal static XElement? ToXml(Paragraph paragraph, SentenceContext? previousSentenceContext = null)
        {
            var isImplicit = IsParagraphImplicit(paragraph);
            var isPathological = IsPathological(paragraph);

            // Parsowane dokumenty potrafią zawierać puste ustępy
            if (isImplicit
                && paragraph.ContentText == string.Empty
                && !paragraph.Points.Any()
                && (paragraph.Amendment == null || paragraph.Amendment.Content == null))
                return null;

            var paragraphElement = GetParagraphElement(paragraph, isImplicit, isPathological);

            if (!isImplicit)
                paragraphElement.Add(new XAttribute(XmlConverterConstants.Contents.eId, paragraph.Id));

            paragraphElement.Add(new XAttribute(XmlConverterConstants.Contents.GUID, paragraph.Guid.ToString()));

            UnitLifecycleXml.Apply(paragraphElement, paragraph);

            if (!isImplicit)
                paragraphElement.Add(Element(XmlConverterConstants.Contents.Num, paragraph.Number?.Designation ?? "UNKNOWN"));

            var textSectionElement = Element(XmlConverterConstants.Contents.TextSection);
            SentenceContext? contextToPass = SentenceProcessor.Process(paragraph.TextSegments, previousSentenceContext, textSectionElement);
            AmendmentXmlConverter.ToXml(paragraph.Amendment, textSectionElement.LastNode);
            if (!string.IsNullOrEmpty(paragraph.ContentText))
            {
                paragraphElement.Add(textSectionElement);
            }

            EmbeddedContentXmlConverter.AppendEmbedded(paragraphElement, paragraph);

            foreach (var point in paragraph.Points)
                PointXmlConverter.AppendTo(paragraphElement, point, isPathological, contextToPass);

            HandleCommonPart(paragraph, paragraphElement, contextToPass);

            return paragraphElement;
        }

        private static XElement GetParagraphElement(Paragraph paragraph, bool isImplicit, bool isPathological)
        {
            var isCodeParagraph = paragraph.Role == Paragraph.CodeParagraphRole;

            return (isImplicit, isPathological) switch
            {
                (true, true) => Element(isCodeParagraph
                    ? XmlConverterConstants.Contents.NonZtpCodeParagraphImplicit
                    : XmlConverterConstants.Contents.NonZtpParagraphImplicit),
                (true, false) => Element(isCodeParagraph
                    ? XmlConverterConstants.Contents.CodeParagraphImplicit
                    : XmlConverterConstants.Contents.ParagraphImplicit),
                (false, true) => Element(isCodeParagraph
                    ? XmlConverterConstants.Contents.NonZtpCodeParagraph
                    : XmlConverterConstants.Contents.NonZtpParagraph),
                (false, false) => Element(isCodeParagraph
                    ? XmlConverterConstants.Contents.CodeParagraph
                    : XmlConverterConstants.Contents.Paragraph)
            };
        }

        // Use parent article to check for number of child paragraphs and assigned IsImplicit value as fallback.
        private static bool IsParagraphImplicit(Paragraph paragraph) =>
            paragraph.IsImplicitComputed == null ? paragraph.IsImplicit : paragraph.IsImplicitComputed.Value;

        // Wyliczenie ma co najmniej dwa punkty; jeden punkt i poziom pominięty (litery wprost w ustępie) są spoza ZTP.
        private static bool IsPathological(Paragraph paragraph) =>
            paragraph.Points.Count == 1 || paragraph.Points.Any(point => point.IsImplicit);
    }
}
