using System.Xml.Linq;
using Saga.Model.SystematizingUnits;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Konwertuje model Division na element XML.
    /// </summary>
    internal static class DivisionXmlConverter
    {
        internal static XElement ToXml(Division division)
        {
            var divisionElement = Element(division.IsImplicit ? XmlConverterConstants.Contents.Division.ElementImplicit : XmlConverterConstants.Contents.Division.Element);

            divisionElement.Add(new XAttribute(XmlConverterConstants.Contents.eId, division.Id));
            divisionElement.Add(new XAttribute(XmlConverterConstants.Contents.GUID, division.Guid.ToString()));

            UnitLifecycleXml.Apply(divisionElement, division);

            divisionElement.Add(Element(XmlConverterConstants.Contents.Division.Header, division.Heading));
            divisionElement.Add(Element(XmlConverterConstants.Contents.Num, division.Number!.Value));

            foreach (var chapter in division.Chapters)
                divisionElement.Add(ChapterXmlConverter.ToXml(chapter));

            return divisionElement;
        }
    }
}
