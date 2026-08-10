using System.Xml.Linq;
using Saga.Model.SystematizingUnits;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Konwertuje model Book na element XML.
    /// </summary>
    internal static class BookXmlConverter
    {
        internal static XElement ToXml(Book book)
        {
            var bookElement = Element(book.IsImplicit ? XmlConverterConstants.Contents.Book.ElementImplicit : XmlConverterConstants.Contents.Book.Element);

            bookElement.Add(new XAttribute(XmlConverterConstants.Contents.eId, book.Id));
            bookElement.Add(new XAttribute(XmlConverterConstants.Contents.GUID, book.Guid.ToString()));

            UnitLifecycleXml.Apply(bookElement, book);

            bookElement.Add(Element(XmlConverterConstants.Contents.Book.Header, book.Heading));
            bookElement.Add(Element(XmlConverterConstants.Contents.Num, book.Number!.Value));

            foreach (var title in book.Titles)
                bookElement.Add(TitleXmlConverter.ToXml(title));

            return bookElement;
        }
    }
}
