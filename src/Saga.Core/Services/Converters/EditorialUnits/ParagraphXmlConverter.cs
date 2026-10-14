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
            var designated = UnitAddressXml.HasDesignation(paragraph);
            var isPathological = IsPathological(paragraph) || (!isImplicit && !designated);

            // Parsowane dokumenty potrafią zawierać puste ustępy
            if (isImplicit
                && paragraph.ContentText == string.Empty
                && !paragraph.Points.Any()
                && (paragraph.Amendment == null || paragraph.Amendment.Content == null))
                return null;

            // Oznaczenie liczbą rzymską („I.") jest spoza ZTP, ale wyliczenie pod takim ustępem zostaje zwykłe.
            var paragraphElement = GetParagraphElement(paragraph, isImplicit, isPathological || HasRomanDesignation(paragraph));

            paragraphElement.Add(UnitAddressXml.Address(paragraph, addressed: !isImplicit && designated));
            paragraphElement.Add(new XAttribute(XmlConverterConstants.Contents.GUID, paragraph.Guid.ToString()));

            UnitLifecycleXml.Apply(paragraphElement, paragraph);

            if (!isImplicit)
                paragraphElement.Add(UnitAddressXml.Number(paragraph));

            var textSectionElement = Element(XmlConverterConstants.Contents.TextSection);
            SentenceContext? contextToPass = SentenceProcessor.Process(paragraph.TextSegments, previousSentenceContext, textSectionElement);
            AmendmentXmlConverter.ToXml(paragraph.Amendment, textSectionElement.LastNode);
            QuotationXmlConverter.Append(paragraph, textSectionElement.LastNode);
            // Ustęp niejawny jest adresowo i treściowo przezroczysty: bez własnego tekstu nie ma czego zapisać.
            if (!isImplicit || !string.IsNullOrEmpty(paragraph.ContentText))
                paragraphElement.Add(SentenceContext.UnitParagraph(textSectionElement));

            EmbeddedContentXmlConverter.AppendEmbedded(paragraphElement, paragraph);

            for (var i = 0; i < paragraph.Points.Count; i++)
            {
                PointXmlConverter.AppendTo(paragraphElement, paragraph.Points[i], isPathological, contextToPass);
                AppendInterrupting(paragraph, paragraphElement, itemsSoFar: i + 1, contextToPass);
            }

            HandleCommonPart(paragraph, paragraphElement, contextToPass);

            return paragraphElement;
        }

        private static XElement GetParagraphElement(Paragraph paragraph, bool isImplicit, bool isPathological)
        {
            var isCodeParagraph = IsCodeParagraph(paragraph);

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

        // Akapit bez oznaczenia między paragrafami („§") należy do tej samej rodziny elementów co one —
        // artykuł nie może mieszać ustępów z paragrafami kodeksu.
        private static bool IsCodeParagraph(Paragraph paragraph) =>
            paragraph.Role == Paragraph.CodeParagraphRole ||
            (!UnitAddressXml.HasDesignation(paragraph) &&
             paragraph.Article is { } article &&
             article.Paragraphs.Any(sibling => sibling.Role == Paragraph.CodeParagraphRole));

        // Use parent article to check for number of child paragraphs and assigned IsImplicit value as fallback.
        private static bool IsParagraphImplicit(Paragraph paragraph) =>
            paragraph.IsImplicitComputed == null ? paragraph.IsImplicit : paragraph.IsImplicitComputed.Value;

        private static bool HasRomanDesignation(Paragraph paragraph) =>
            paragraph.Number?.Value is { Length: > 0 } value && value.All(c => "IVXLCDM".Contains(c));

        // Wyliczenie ma co najmniej dwa punkty; jeden punkt i poziom pominięty (litery wprost w ustępie) są spoza ZTP.
        private static bool IsPathological(Paragraph paragraph) =>
            paragraph.Points.Count == 1 || paragraph.Points.Any(point => point.IsImplicit);
    }
}
