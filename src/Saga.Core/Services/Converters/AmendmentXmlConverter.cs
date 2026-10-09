using System.Xml.Linq;
using Saga.Core.Services.Parsing;
using Saga.Model;
using Saga.Model.EditorialUnits;
using Saga.Model.SystematizingUnits;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Konwertuje model Amendment na XML.
    /// </summary>
    internal static class AmendmentXmlConverter
    {
        private sealed record Command(StructuralAmendmentReference? Target, Guid Guid);

        internal static void ToXml(Amendment? amendment, XNode? sentence = null)
        {
            if (amendment is null)
                return;

            var commands = Commands(amendment);

            if (amendment.OperationType == AmendmentOperationType.Repeal)
            {
                foreach (var command in commands)
                    AddRepealInstruction(amendment, command, sentence);
                return;
            }

            if (amendment.Content is null)
                return;

            var units = AmendmentTargetPairing.ContentUnits(amendment.Content);

            if (units.Count == 0 && commands.Count > 1)
            {
                foreach (var command in commands)
                {
                    var copy = new List<XElement>();
                    AddPlainTextContent(amendment, command, sentence, copy);
                    AddCommonParts(amendment, command, sentence, copy);
                    ApplyQuotationMarks(amendment, copy);
                }
            }
            else
            {
                var paired = commands.Count > 1 ? AmendmentTargetPairing.Pair(amendment.Targets, units) : null;
                if (paired is null)
                {
                    commands = commands.Take(1).ToList();
                    paired = [units];
                }

                var blocks = new List<XElement>();
                for (var i = 0; i < commands.Count; i++)
                {
                    if (i == 0)
                        AddPlainTextContent(amendment, commands[i], sentence, blocks);
                    AddStructuredContentItems(amendment, commands[i], paired[i], sentence, blocks);
                    if (i == commands.Count - 1)
                        AddCommonParts(amendment, commands[i], sentence, blocks);
                }

                ApplyQuotationMarks(amendment, blocks);
            }

            // Interpunkcja po cytacie należy do zdania nowelizującego, nie do cytowanej treści.
            if (amendment.TrailingPunctuation is { Length: > 0 } trailing && sentence is XElement sentenceElement)
            {
                sentenceElement.Add(trailing);
            }
        }

        private static List<Command> Commands(Amendment amendment)
        {
            var first = amendment.Guid != Guid.Empty ? amendment.Guid : Guid.NewGuid();
            var targets = amendment.Targets ?? [];
            if (targets.Count == 0)
                return [new Command(null, first)];

            return targets
                .Select((target, index) => new Command(target, index == 0 ? first : Guid.NewGuid()))
                .ToList();
        }

        /// <summary>
        /// Rozdziela cudzysłowy cytatu pomiędzy jego bloki: otwierający trafia do pierwszego, zamykający
        /// do ostatniego, środkowe nie mają żadnego. Cudzysłowy niesie wyłącznie atrybut — treść cytatu,
        /// także tekstowego, jest bez nich (jak w przykładach autorów schematu).
        /// </summary>
        private static void ApplyQuotationMarks(Amendment amendment, List<XElement> blocks)
        {
            for (var i = 0; i < blocks.Count; i++)
            {
                var opens = i == 0 && amendment.HasOpeningQuote;
                var closes = i == blocks.Count - 1 && amendment.HasClosingQuote;
                blocks[i].SetAttributeValue(XmlConverterConstants.Contents.Citation.Quotation.QuotationAttribute, QuotationMarks(opens, closes));
            }
        }

        private static string QuotationMarks(bool opens, bool closes) => (opens, closes) switch
        {
            (true, true) => XmlConverterConstants.Contents.Citation.Quotation.QuotationBoth,
            (true, false) => XmlConverterConstants.Contents.Citation.Quotation.QuotationOpening,
            (false, true) => XmlConverterConstants.Contents.Citation.Quotation.QuotationClosing,
            _ => XmlConverterConstants.Contents.Citation.Quotation.QuotationNone,
        };

        private static string BuildActUri(Amendment amendment) =>
            XmlConverterConstants.ELIUrl + "eli/" + (amendment.TargetLegalAct.GetELIStrings().FirstOrDefault() ?? string.Empty);

        private static void ApplyAttributes(XElement element, Amendment amendment, Command command, bool isStructural)
        {
            XNamespace now = XmlConverterConstants.AmendmentNamespace;

            element.Add(new XAttribute(now + XmlConverterConstants.Contents.Citation.Quotation.ActAttribute, BuildActUri(amendment)));

            element.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.QuotationAttribute,
                XmlConverterConstants.Contents.Citation.Quotation.QuotationBoth));

            element.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.GUIDAttribute, command.Guid.ToString()));

            // if (amendment.RefNowGuid != Guid.Empty)
            // element.Add(new XAttribute(XmlConverterConstants.QuotationRefGUIDAttribute, "UNKNOWN"));

            AddTargetAttributes(element, command.Target);

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

        private static void AddTargetAttributes(XElement element, StructuralAmendmentReference? target)
        {
            var start = target?.Structure.ToString();
            element.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.ElementAttribute,
                string.IsNullOrWhiteSpace(start) ? "UNKNOWN" : start));

            var end = target?.RangeEnd?.ToString();
            if (!string.IsNullOrWhiteSpace(end) && end != start)
                element.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.ElementDoAttribute, end));
        }

        private static void AddRepealInstruction(Amendment amendment, Command command, XNode? sentence)
        {
            if (sentence is not XElement zdanie)
                return;

            XNamespace now = XmlConverterConstants.AmendmentNamespace;

            var uchyl = new XElement(now + XmlConverterConstants.Contents.Instruction.Repeal);
            uchyl.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.ActAttribute, BuildActUri(amendment)));
            AddTargetAttributes(uchyl, command.Target);
            uchyl.Add(new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.GUIDAttribute, command.Guid.ToString()));

            zdanie.Add(uchyl);
        }

        private static void AddPlainTextContent(Amendment amendment, Command command, XNode? sentence, List<XElement> blocks)
        {
            var plainText = amendment.Content?.PlainText;
            if (plainText == null) return;
            AddTextCitation(amendment, command, sentence, plainText, blocks);
        }

        private static void AddCommonParts(Amendment amendment, Command command, XNode? sentence, List<XElement> blocks)
        {
            var commonParts = amendment.Content?.CommonParts;
            if (commonParts == null) return;

            foreach (var commonPart in commonParts)
                AddTextCitation(amendment, command, sentence, commonPart.ContentText, blocks);
        }

        private static void AddTextCitation(Amendment amendment, Command command, XNode? sentence, string text, List<XElement> blocks)
        {
            var citation = Element(XmlConverterConstants.Contents.Citation.Text.Element,
                Element(XmlConverterConstants.Contents.Citation.Text.Content, text));
            ApplyAttributes(citation, amendment, command, isStructural: false);
            if (sentence is XElement zdanie)
                zdanie.Add(citation);
            blocks.Add(citation);
        }

        private static void AddStructuredContentItems(
            Amendment amendment, Command command, IReadOnlyList<BaseEntity> units, XNode? sentence, List<XElement> blocks)
        {
            foreach (var group in units.GroupBy(e => e.GetType()))
            {
                var elements = group
                    .Select(BuildContentElement)
                    .Where(element => element is not null)
                    .Select(element => element!)
                    .ToList();

                var citation = Element(XmlConverterConstants.Contents.Citation.StructElement);
                ApplyAttributes(citation, amendment, command, isStructural: true);
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
                // Cytat strukturalny przyjmuje te same jednostki co akt, więc i ich nazwy z poziomu aktu.
                ISystematizingUnit => SystematizingUnitXmlConverter.ToXml(entity, actLevel: true),
                _ => throw new InvalidOperationException($"Unsupported entity type: {entity.GetType().Name}")
            };
    }
}
