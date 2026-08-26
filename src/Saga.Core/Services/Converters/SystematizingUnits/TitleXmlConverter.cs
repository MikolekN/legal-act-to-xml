using System.Xml.Linq;
using Saga.Model.SystematizingUnits;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Konwertuje model Title na element XML.
    /// </summary>
    internal static class TitleXmlConverter
    {
        internal static XElement ToXml(Title title)
        {
            var titleElement = Element(title.IsImplicit ? XmlConverterConstants.Contents.Title.ElementImplicit : XmlConverterConstants.Contents.Title.Element);

            titleElement.Add(new XAttribute(XmlConverterConstants.Contents.eId, title.Id));
            titleElement.Add(new XAttribute(XmlConverterConstants.Contents.GUID, title.Guid.ToString()));

            UnitLifecycleXml.Apply(titleElement, title);

            titleElement.Add(Element(XmlConverterConstants.Contents.Title.Header, title.Heading));
            titleElement.Add(Element(XmlConverterConstants.Contents.Num, title.Number!.Value));

            foreach (var division in title.Divisions)
                titleElement.Add(DivisionXmlConverter.ToXml(division));

            return titleElement;
        }
    }
}
