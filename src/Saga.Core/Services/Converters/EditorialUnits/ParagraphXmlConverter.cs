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
                paragraphElement.Add(Element(XmlConverterConstants.Contents.Num, paragraph.Number?.Value ?? "UNKNOWN"));

            var textSectionElement = Element(XmlConverterConstants.Contents.TextSection);
            SentenceContext? contextToPass = SentenceProcessor.Process(paragraph.TextSegments, previousSentenceContext, textSectionElement);
            var amendmentElements = AmendmentXmlConverter.ToXml(paragraph.Amendment, textSectionElement.LastNode);
            if (!string.IsNullOrEmpty(paragraph.ContentText) || (amendmentElements != null && amendmentElements.Any()))
            {
                textSectionElement.Add(amendmentElements);
                paragraphElement.Add(textSectionElement);
            }

            EmbeddedContentXmlConverter.AppendEmbedded(paragraphElement, paragraph);

            foreach (var point in paragraph.Points)
                paragraphElement.Add(PointXmlConverter.ToXml(point, isPathological, contextToPass));

            HandleCommonPart(paragraph, paragraphElement, contextToPass);

            return paragraphElement;
        }

        private static XElement GetParagraphElement(Paragraph paragraph, bool isImplicit, bool isPathological)
        {
            var isCodeParagraph = false;
            // var isCodeParagraph = paragraph.Role == Paragraph.CodeParagraphRole;
            // TODO: repair

            return (isImplicit, isPathological) switch
            {
                (true, true) => Element(isCodeParagraph
                    ? XmlConverterConstants.Contents.PatoCodeParagraphImplicit
                    : XmlConverterConstants.Contents.PatoParagraphImplicit),
                (true, false) => Element(isCodeParagraph
                    ? XmlConverterConstants.Contents.CodeParagraphImplicit
                    : XmlConverterConstants.Contents.ParagraphImplicit),
                (false, true) => Element(isCodeParagraph
                    ? XmlConverterConstants.Contents.PatoCodeParagraph
                    : XmlConverterConstants.Contents.PatoParagraph),
                (false, false) => Element(isCodeParagraph
                    ? XmlConverterConstants.Contents.CodeParagraph
                    : XmlConverterConstants.Contents.Paragraph)
            };
        }

        // Use parent article to check for number of child paragraphs and assigned IsImplicit value as fallback.
        private static bool IsParagraphImplicit(Paragraph paragraph) =>
            paragraph.IsImplicitComputed == null ? paragraph.IsImplicit : paragraph.IsImplicitComputed.Value;

        private static bool IsPathological(Paragraph paragraph)
        {
            // TODO: unhandled if the structure of paragraph is not respected - code doesn't expect that e.g. letter can appear in a paragraph
            if (paragraph.Points.Count == 1) // Every paragraph is expected to have no points or more than 1 point
                return true;
            else
                return false;
        }
    }
}
