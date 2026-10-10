using System.Xml.Linq;
using Saga.Model;
using Saga.Model.EditorialUnits;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Konwertuje fragment CommonPart encji na XML.
    /// </summary>
    internal static class CommonPartXmlConverter
    {
        internal static void HandleCommonPart(IHasCommonParts entity, XElement element, SentenceContext? context, int? nesting = null)
        {
            if (entity.CommonParts.Any())
            {
                var wrapUp = entity.CommonParts.FirstOrDefault(cp => cp.Type == CommonPartType.WrapUp);
                if (wrapUp != null)
                {
                    var textElement = Element(XmlConverterConstants.Contents.TextSection);
                    if (context != null)
                    {
                        context.CurrentFragment += 1;
                        textElement.Add(SentenceContext.ToXml(context, wrapUp.ContentText, dated: wrapUp));
                    }
                    else
                    {
                        textElement.Add(SentenceContext.ToXml(text: wrapUp.ContentText, dated: wrapUp));
                    }
                    var commonPartElement = Element(GetCommonPartName(entity, nesting));

                    commonPartElement.Add(new XAttribute(XmlConverterConstants.Contents.eId, wrapUp.Id));
                    commonPartElement.Add(new XAttribute(XmlConverterConstants.Contents.GUID, wrapUp.Guid.ToString()));

                    UnitLifecycleXml.Apply(commonPartElement, wrapUp);

                    commonPartElement.Add(textElement);
                    element.Add(commonPartElement);
                }
            }
        }

        private static string GetCommonPartName(IHasCommonParts entity, int? nesting)
        {
            return (entity, nesting) switch
            {
                (Paragraph, null) => XmlConverterConstants.Contents.CommonPart.Point,
                (Point, null) => XmlConverterConstants.Contents.CommonPart.Letter,
                (Letter, null) => XmlConverterConstants.Contents.CommonPart.Tiret,
                (Tiret, 1) => XmlConverterConstants.Contents.CommonPart.DoubleTiret,
                (Tiret, 2) => XmlConverterConstants.Contents.CommonPart.TripleTiret,
                (Tiret, 3) => XmlConverterConstants.Contents.CommonPart.QuadrupleTiret,
                (Tiret, 4) => XmlConverterConstants.Contents.CommonPart.QuintupleTiret,
                _ => throw new InvalidOperationException($"Unsupported entity type {entity.GetType()} with nesting {nesting}")
            };
        }
    }
}
