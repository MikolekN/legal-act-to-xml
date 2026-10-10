using System.Xml.Linq;
using Saga.Model.EditorialUnits;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Konwertuje model Article na element XML.
    /// </summary>
    internal static class ArticleXmlConverter
    {
        /// <summary>
        /// <paramref name="inSubchapter"/> — artykuł stoi w oddziale (jawnym albo „-n"), a nie wprost w akcie
        /// albo na szczycie cytatu. Schemat rozróżnia tam artykuł ustawy i artykuł z paragrafami („§").
        /// </summary>
        internal static XElement ToXml(Article article, bool inSubchapter = false)
        {
            var articleElement = Element(inSubchapter && HasCodeParagraphs(article)
                ? XmlConverterConstants.Contents.CodeArticle
                : XmlConverterConstants.Contents.Article);

            articleElement.Add(new XAttribute(XmlConverterConstants.Contents.eId, article.Id));
            articleElement.Add(new XAttribute(XmlConverterConstants.Contents.GUID, article.Guid.ToString()));

            UnitLifecycleXml.Apply(articleElement, article);

            articleElement.Add(Element(XmlConverterConstants.Contents.Num, article.Number == null ? "UNKNOWN" : article.Number.Designation));

            foreach (var paragraph in article.Paragraphs)
                articleElement.Add(ParagraphXmlConverter.ToXml(paragraph));

            return articleElement;
        }

        private static bool HasCodeParagraphs(Article article) =>
            article.Paragraphs.Any(paragraph => paragraph.Role == Paragraph.CodeParagraphRole);
    }
}
