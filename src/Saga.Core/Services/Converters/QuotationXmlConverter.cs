using System.Xml.Linq;
using Saga.Core.Services.Parsing;
using Saga.Model;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Przytoczenie stojące w treści jednostki aktu („oświadczenie o następującej treści: „…"") → <c>tresc-strukt</c>
    /// w jej ostatnim zdaniu, jak rota ślubowania w przykładzie autorów schematu. Treść składa ten sam konwerter
    /// co treść cytowaną nowelizacji; z cytatu zostaje sama treść, bez instrukcji i bez adresów — przytoczony tekst
    /// nie jest jednostką żadnego aktu (ADR-0071).
    /// </summary>
    internal static class QuotationXmlConverter
    {
        private static readonly string[] UnitAttributes =
        [
            XmlConverterConstants.Contents.eId,
            XmlConverterConstants.Contents.EntryIntoForce,
            XmlConverterConstants.Contents.InForceUntil,
            XmlConverterConstants.Contents.UnitState,
        ];

        /// <summary>Dopisuje przytoczenia jednostki do zdania; wywoływane tuż po treści cytowanej nowelizacji.</summary>
        internal static void Append(BaseEntity unit, XNode? sentence)
        {
            if (sentence is not XElement host)
                return;

            foreach (var quotation in unit.Continuation.Select(block => block.Quotation).OfType<Quotation>())
            {
                var before = host.Nodes().ToList();
                AmendmentXmlConverter.ToXml(
                    new Amendment { OperationType = AmendmentOperationType.Modification, Content = quotation.Content }, host);

                var citations = host.Nodes().Except(before).OfType<XElement>().ToList();
                if (citations.Count == 0)
                    continue;

                var quoted = Element(XmlConverterConstants.Contents.Citation.QuotedStructElement,
                    new XAttribute(XmlConverterConstants.Contents.Citation.Quotation.QuotationAttribute,
                        AmendmentXmlConverter.QuotationMarks(quotation.HasOpeningQuote, quotation.HasClosingQuote)),
                    new XAttribute(XmlConverterConstants.Contents.GUID, Guid.NewGuid().ToString()));

                foreach (var citation in citations)
                {
                    quoted.Add(citation.Name.LocalName == XmlConverterConstants.Contents.Citation.Text.Element
                        ? Paragraphs(citation)
                        : Detached(citation));
                    citation.Remove();
                }

                foreach (var element in quoted.Descendants())
                {
                    foreach (var name in UnitAttributes)
                        element.Attribute(name)?.Remove();
                    element.RemoveAnnotations<UnitDates>();
                }

                host.Add(quoted);
                if (quotation.TrailingPunctuation is { Length: > 0 } trailing)
                    host.Add(trailing);
            }
        }

        /// <summary>Tekst przytoczony bez jednostek to akapity zdań — akapit na każdy wiersz tekstu.</summary>
        private static List<XElement> Paragraphs(XElement textCitation)
        {
            var paragraphs = new List<XElement>();

            foreach (var part in Detached(textCitation))
            {
                if (part.Name.LocalName == XmlConverterConstants.Contents.Sentence.Element)
                {
                    if (paragraphs.Count == 0)
                        paragraphs.Add(Element(XmlConverterConstants.Contents.TextSection));
                    paragraphs[^1].Add(part);
                    continue;
                }

                foreach (var line in part.Value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var paragraph = Element(XmlConverterConstants.Contents.TextSection);
                    var number = 0;
                    foreach (var sentence in ParsingFactories.SplitIntoSentences(line))
                        paragraph.Add(SentenceContext.ToXml(text: sentence.Text, number: ++number));
                    paragraphs.Add(paragraph);
                }
            }

            return paragraphs;
        }

        private static List<XElement> Detached(XElement citation)
        {
            var content = citation.Elements().ToList();
            citation.RemoveNodes();
            return content;
        }
    }
}
