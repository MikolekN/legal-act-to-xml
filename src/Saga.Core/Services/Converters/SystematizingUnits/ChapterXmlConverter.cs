using System.Xml.Linq;
using Saga.Model.SystematizingUnits;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Konwertuje model Chapter na element XML.
    /// </summary>
    internal static class ChapterXmlConverter
    {
        internal static XElement ToXml(Chapter chapter)
        {
            var chapterElement = Element(chapter.IsImplicit ? XmlConverterConstants.Contents.Chapter.ElementImplicit : XmlConverterConstants.Contents.Chapter.Element);

            chapterElement.Add(new XAttribute(XmlConverterConstants.Contents.eId, chapter.Id));
            chapterElement.Add(new XAttribute(XmlConverterConstants.Contents.GUID, chapter.Guid.ToString()));

            UnitLifecycleXml.Apply(chapterElement, chapter);

            chapterElement.Add(Element(XmlConverterConstants.Contents.Chapter.Header, chapter.Heading));
            chapterElement.Add(Element(XmlConverterConstants.Contents.Num, chapter.Number!.Value));

            foreach (var subchapter in chapter.Subchapters)
                chapterElement.Add(SubchapterXmlConverter.ToXml(subchapter));

            return chapterElement;
        }
    }
}
