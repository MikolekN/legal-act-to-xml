using System.Xml.Linq;
using Saga.Model.SystematizingUnits;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Konwertuje model Subchapter na element XML.
    /// </summary>
    internal static class SubchapterXmlConverter
    {
        internal static XElement ToXml(Subchapter subchapter)
        {
            var subchapterElement = Element(subchapter.IsImplicit ? XmlConverterConstants.Contents.Subchapter.ElementImplicit : XmlConverterConstants.Contents.Subchapter.Element);

            subchapterElement.Add(new XAttribute(XmlConverterConstants.Contents.eId, subchapter.Id));
            subchapterElement.Add(new XAttribute(XmlConverterConstants.Contents.GUID, subchapter.Guid.ToString()));

            UnitLifecycleXml.Apply(subchapterElement, subchapter);

            subchapterElement.Add(Element(XmlConverterConstants.Contents.Subchapter.Header, subchapter.Heading));
            subchapterElement.Add(Element(XmlConverterConstants.Contents.Num, subchapter.Number!.Value));

            foreach (var article in subchapter.Articles)
                subchapterElement.Add(ArticleXmlConverter.ToXml(article));

            return subchapterElement;
        }
    }
}
