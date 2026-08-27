using System.Xml.Linq;
using Saga.Model.SystematizingUnits;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Konwertuje model Part na element XML.
    /// </summary>
    internal static class PartXmlConverter
    {
        internal static XElement ToXml(Part part)
        {
            var partElement = Element(part.IsImplicit ? XmlConverterConstants.Contents.Part.ElementImplicit : XmlConverterConstants.Contents.Part.Element);

            partElement.Add(new XAttribute(XmlConverterConstants.Contents.eId, part.Id));
            partElement.Add(new XAttribute(XmlConverterConstants.Contents.GUID, part.Guid.ToString()));

            partElement.Add(Element(XmlConverterConstants.Contents.Part.Header, part.Heading));

            UnitLifecycleXml.Apply(partElement, part);

            foreach (var book in part.Books)
                partElement.Add(BookXmlConverter.ToXml(book));

            return partElement;
        }
    }
}
