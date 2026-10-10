using System.Xml.Linq;
using Saga.Model;
using Saga.Model.EditorialUnits;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Załącznik w eksporcie: <c>zalacznik</c> ze wskazaniem aktu (<c>zalacznik-do</c>), numerem
    /// (<c>zalacznik-nr</c>), tytułem i treścią w <c>segment-zalacznika</c> — tabele i akapity w kolejności
    /// dokumentu. Załącznik bez numeru nie ma adresu i mówi dlaczego (ADR-0024).
    /// </summary>
    internal static class AnnexXmlConverter
    {
        internal static XElement ToXml(Annex annex)
        {
            var element = Element(XmlConverterConstants.Attachments.Element,
                annex.Id.Length > 0
                    ? new XAttribute(XmlConverterConstants.Contents.eId, annex.Id)
                    : new XAttribute(XmlConverterConstants.Contents.MissingEId, XmlConverterConstants.Contents.UnmarkedUnit),
                Guid());

            foreach (var line in annex.AttachedTo)
                element.Add(Element(XmlConverterConstants.Attachments.AttachedTo, Guid(), line));

            if (annex.Number is { Length: > 0 } number)
                element.Add(Element(XmlConverterConstants.Attachments.NumberElement, Guid(),
                    Element(XmlConverterConstants.Contents.Num, number)));

            if (annex.Title is { Length: > 0 } title)
                element.Add(Element(XmlConverterConstants.Attachments.TitleElement, Guid(), title));

            var content = annex.Content.SelectMany(ToContent).ToList();
            if (content.Count > 0)
                element.Add(Element(XmlConverterConstants.Attachments.Segment, Guid(), content));

            return element;
        }

        private static IEnumerable<XElement> ToContent(AnnexBlock block)
        {
            if (block.Table is { } table)
                return TableXmlConverter.ToXml(table) is { } element ? [element] : [];

            if (block.Unit is { } unit)
                return ToUnit(unit);

            return [Element(XmlConverterConstants.Table.PlainText, Guid(), block.Text ?? string.Empty)];
        }

        // Wyliczenie zaczęte od punktów nie ma ustępu — punkty stoją wtedy wprost w treści załącznika.
        private static IEnumerable<XElement> ToUnit(Paragraph unit)
        {
            if (ParagraphXmlConverter.ToXml(unit) is not { } element)
                return [];

            return unit.IsImplicit ? element.Elements().ToList() : [element];
        }

        private static XAttribute Guid() => new(XmlConverterConstants.Contents.GUID, System.Guid.NewGuid().ToString());
    }
}
