using System.Xml.Linq;
using Saga.Model;
using Saga.Model.EditorialUnits;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Konwertuje model Amendment na XML.
    /// </summary>
    internal static class AmendmentXmlConverter
    {
        internal static void ToXml(Amendment? amendment, XNode? sentence = null)
        {
            if (amendment is null)
                return;

            if (amendment.OperationType == AmendmentOperationType.Repeal)
            {
                AddRepealInstruction(amendment, sentence);
                return;
            }

            if (amendment.Content is null)
                return;

            var blocks = new List<XElement>();

            AddPlainTextContent(amendment, sentence, blocks);
            AddStructuredContentItems(amendment, sentence, blocks);
            AddCommonParts(amendment, sentence, blocks);

            ApplyQuotationMarks(amendment, blocks, sentence);
        }

        /// <summary>
        /// Rozdziela cudzysłowy cytatu pomiędzy jego bloki: otwierający trafia do pierwszego,
        /// zamykający do ostatniego. Atrybut 'cudzyslowy' jest maską bitową
        /// (1 = otwiera, 2 = zamyka, 3 = oba, 0 = blok środkowy).
        /// </summary>
        private static void ApplyQuotationMarks(Amendment amendment, List<XElement> blocks, XNode? sentence)
        {
            var punctuation = amendment.TrailingPunctuation;
            var punctuationConsumed = false;

            for (var i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                var opens = i == 0 && amendment.HasOpeningQuote;
                var closes = i == blocks.Count - 1 && amendment.HasClosingQuote;

                if (IsTextCitation(block))
                {
                    // Cudzysłowy dosłownie w treści — atrybut ich nie powiela.
                    var closingSuffix = closes ? ClosingQuote + punctuation : null;
                    if (closes && punctuation is { Length: > 0 })
                        punctuationConsumed = true;

                    ApplyLiteralQuotes(block, opens ? OpeningQuote : null, closingSuffix);
                    SetQuotationAttribute(block, 0);
                    continue;
                }

                SetQuotationAttribute(block, (opens ? 1 : 0) + (closes ? 2 : 0));
            }

            if (!punctuationConsumed && punctuation is { Length: > 0 } trailing &&
                sentence is XElement sentenceElement)
            {
                sentenceElement.Add(trailing);
            }
        }

        private const string OpeningQuote = "„";
        private const string ClosingQuote = "”";

        private static bool IsTextCitation(XElement element) =>
            element.Name.LocalName == XmlConverterConstants.Contents.Citation.Text.Element;

        private static void SetQuotationAttribute(XElement element, int value) =>
            element.SetAttributeValue(XmlConverterConstants.Contents.Citation.Quotation.QuotationAttribute, value);

        private static void ApplyLiteralQuotes(XElement citation, string? prefix, string? suffix)
        {
            XNamespace ns = XmlConverterConstants.Namespace;
            var textElements = citation.Elements(ns + XmlConverterConstants.Contents.Citation.Text.Content).ToList();
            if (textElements.Count == 0)
                return;

            if (prefix is { Length: > 0 })
                textElements[0].Value = prefix + textElements[0].Value;

            if (suffix is { Length: > 0 })
                textElements[^1].Value += suffix;
        }

        private static string BuildActUri(Amendment amendment) =>
            XmlConverterConstants.ELIUrl + "eli/" + (amendment.TargetLegalAct.GetELIStrings().FirstOrDefault() ?? string.Empty);

        private static void ApplyAttributes(XElement element, Amendment amendment, bool isStructural)
        {
            XNamespace now = XmlConverterConstants.AmendmentNamespace;

            element.Add(new XAttribute(now + XmlConverterConstants.Contents.Citation.Quotation.ActAttribute, BuildActUri(amendment)));

            element.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.QuotationAttribute, 3));

            if (amendment.Guid != Guid.Empty)
                element.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.GUIDAttribute, amendment.Guid.ToString()));
            else
                element.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.GUIDAttribute, Guid.NewGuid().ToString()));

            // if (amendment.RefNowGuid != Guid.Empty)
            // element.Add(new XAttribute(XmlConverterConstants.QuotationRefGUIDAttribute, "UNKNOWN"));

            var targets = amendment.Targets?.ToList() ?? new List<StructuralAmendmentReference>();
            var start_target = targets.FirstOrDefault()?.ToString() ?? string.Empty;
            var end_target = targets.LastOrDefault()?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(start_target))
                element.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.ElementAttribute, start_target));
            else
                element.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.ElementAttribute, "UNKNOWN"));

            if (!string.IsNullOrWhiteSpace(end_target) && string.Compare(start_target, end_target) != 0)
                element.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.ElementDoAttribute, end_target));

            element.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.FunctionAttribute, GetFunctionType(amendment.OperationType, isStructural)));

            // element.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.SentenceAttribute, sentence.ReferenceSentenceNumber));

            // string appearance = string.IsNullOrWhiteSpace(amendment.Appearance) ? "wszystkie" : amendment.Appearance;
            // element.Add(new XAttribute(XmlConverterConstants.QuotationAppearanceAttribute, "UNKNOWN"));
        }

        private static string GetFunctionType(AmendmentOperationType operation, bool isStructural) =>
            operation switch
            {
                AmendmentOperationType.Repeal => XmlConverterConstants.Contents.Citation.Quotation.Function.RepealText,
                AmendmentOperationType.Insertion => XmlConverterConstants.Contents.Citation.Quotation.Function.AddAfter,
                AmendmentOperationType.Modification => isStructural
                    ? XmlConverterConstants.Contents.Citation.Quotation.Function.Edit
                    : XmlConverterConstants.Contents.Citation.Quotation.Function.EditText,
                AmendmentOperationType.Error => XmlConverterConstants.Contents.Citation.Quotation.Function.NewText,
                _ => XmlConverterConstants.Contents.Citation.Quotation.Function.NewText
            };

        private static void AddRepealInstruction(Amendment amendment, XNode? sentence)
        {
            if (sentence is not XElement zdanie)
                return;

            XNamespace now = XmlConverterConstants.AmendmentNamespace;

            var uchyl = new XElement(now + XmlConverterConstants.Contents.Instruction.Repeal);
            uchyl.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.ActAttribute, BuildActUri(amendment)));

            var targets = amendment.Targets ?? new List<StructuralAmendmentReference>();
            var startTarget = targets.FirstOrDefault()?.ToString();
            var endTarget = targets.LastOrDefault()?.ToString();
            uchyl.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.ElementAttribute,
                string.IsNullOrWhiteSpace(startTarget) ? "UNKNOWN" : startTarget));
            if (!string.IsNullOrWhiteSpace(endTarget) && endTarget != startTarget)
                uchyl.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.ElementDoAttribute, endTarget));

            uchyl.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.GUIDAttribute,
                amendment.Guid != Guid.Empty ? amendment.Guid.ToString() : Guid.NewGuid().ToString()));

            zdanie.Add(uchyl);
        }

        private static void AddPlainTextContent(Amendment? amendment, XNode? sentence, List<XElement> blocks)
        {
            var plainText = amendment?.Content?.PlainText;
            if (plainText == null) return;
            AddTextCitation(amendment!, sentence, plainText, blocks);
        }

        private static void AddCommonParts(Amendment? amendment, XNode? sentence, List<XElement> blocks)
        {
            var commonParts = amendment?.Content?.CommonParts;
            if (commonParts == null) return;

            foreach (var commonPart in commonParts)
                AddTextCitation(amendment!, sentence, commonPart.ContentText, blocks);
        }

        private static void AddTextCitation(Amendment amendment, XNode? sentence, string text, List<XElement> blocks)
        {
            var citation = Element(XmlConverterConstants.Contents.Citation.Text.Element,
                Element(XmlConverterConstants.Contents.Citation.Text.Content, text));
            ApplyAttributes(citation, amendment, isStructural: false);
            if (sentence is XElement zdanie)
                zdanie.Add(citation);
            blocks.Add(citation);
        }

        private static void AddStructuredContentItems(Amendment? amendment, XNode? sentence, List<XElement> blocks)
        {
            var content = amendment?.Content;
            if (content == null) return;

            foreach (var group in EnumerateAmendmentContentItems(content).GroupBy(e => e.GetType()))
            {
                var elements = group
                    .Select(BuildContentElement)
                    .Where(element => element is not null)
                    .Select(element => element!)
                    .ToList();

                var citation = Element(XmlConverterConstants.Contents.Citation.StructElement);
                ApplyAttributes(citation, amendment!, isStructural: true);
                citation.Add(elements);
                if (sentence is XElement zdanie)
                    zdanie.Add(citation);
                blocks.Add(citation);
            }
        }

        private static XElement? BuildContentElement(BaseEntity entity) =>
            entity switch
            {
                Article article => ArticleXmlConverter.ToXml(article),
                Paragraph paragraph => ParagraphXmlConverter.ToXml(paragraph),
                Point point => PointXmlConverter.ToXml(point),
                Letter letter => LetterXmlConverter.ToXml(letter),
                Tiret tiret => TiretXmlConverter.ToXml(tiret),
                _ => throw new InvalidOperationException($"Unsupported entity type: {entity.GetType().Name}")
            };

        private static IEnumerable<BaseEntity> EnumerateAmendmentContentItems(AmendmentContent? content)
        {
            if (content is null) return [];

            return Enumerable.Empty<BaseEntity>()
                .Concat(content.Articles ?? [])
                .Concat(content.Paragraphs ?? [])
                .Concat(content.Points ?? [])
                .Concat(content.Letters ?? [])
                .Concat(content.Tirets ?? []);
        }
    }
}
