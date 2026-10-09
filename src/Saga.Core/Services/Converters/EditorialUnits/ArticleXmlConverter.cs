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
        internal static XElement ToXml(Article article)
        {
            var articleElement = Element(XmlConverterConstants.Contents.Article);

            articleElement.Add(new XAttribute(XmlConverterConstants.Contents.eId, article.Id));
            articleElement.Add(new XAttribute(XmlConverterConstants.Contents.GUID, article.Guid.ToString()));

            UnitLifecycleXml.Apply(articleElement, article);

            articleElement.Add(Element(XmlConverterConstants.Contents.Num, article.Number == null ? "UNKNOWN" : article.Number.Designation));

            foreach (var paragraph in article.Paragraphs)
                articleElement.Add(ParagraphXmlConverter.ToXml(paragraph));

            return articleElement;
        }
    }
}
