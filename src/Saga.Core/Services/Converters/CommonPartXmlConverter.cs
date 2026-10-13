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
        /// <summary>Część wspólna zamykająca wyliczenie — po ostatniej pozycji, z eId gospodarza (ADR-0025).</summary>
        internal static void HandleCommonPart(IHasCommonParts entity, XElement element, SentenceContext? context, int? nesting = null)
        {
            // Część wspólna wydrukowana w tekście jednolitym w dwóch brzmieniach to dwie części wspólne o tym
            // samym eId (ADR-0065); drugie brzmienie jest tym samym fragmentem zdania co pierwsze.
            CommonPart? previous = null;
            foreach (var wrapUp in entity.CommonParts.Where(cp => cp is { Type: CommonPartType.WrapUp, InterruptsAfter: null }))
            {
                var commonPartElement = Element(GetCommonPartName(entity, nesting));
                commonPartElement.Add(new XAttribute(XmlConverterConstants.Contents.eId, wrapUp.Id));
                Fill(commonPartElement, wrapUp, context, sameFragment:
                    previous is { Version.Kind: WordingVersionKind.Current } && wrapUp.Version is { Kind: WordingVersionKind.Future });
                element.Add(commonPartElement);
                previous = wrapUp;
            }
        }

        /// <summary>
        /// Część wspólna będąca treścią nowelizacji, cytowana bez gospodarza. Rodzaj wyliczenia niesie sama
        /// (ze stylu akapitu), a adres — kotwica celu komendy.
        /// </summary>
        internal static XElement Quoted(CommonPart part)
        {
            var commonPartElement = Element(part.ListItemType switch
            {
                UnitType.Letter => XmlConverterConstants.Contents.CommonPart.Letter,
                UnitType.Tiret => XmlConverterConstants.Contents.CommonPart.Tiret,
                _ => XmlConverterConstants.Contents.CommonPart.Point,
            });
            commonPartElement.Add(new XAttribute(XmlConverterConstants.Contents.eId, part.Id));
            Fill(commonPartElement, part, context: null);
            return commonPartElement;
        }

        /// <summary>
        /// Części wspólne przerywające wyliczenie, które stoją po <paramref name="itemsSoFar"/> pozycjach.
        /// Wychodzą w miejscu, w którym stoją w akcie, bez eId: adres części wspólnej nie rozróżnia kilku
        /// pod jednym gospodarzem, więc schemat każe nazwać powód jego braku.
        /// </summary>
        internal static void AppendInterrupting(IHasCommonParts entity, XElement element, int itemsSoFar, SentenceContext? context)
        {
            foreach (var part in entity.CommonParts.Where(cp => cp.Type == CommonPartType.WrapUp && cp.InterruptsAfter == itemsSoFar))
            {
                var commonPartElement = Element(entity is Paragraph
                    ? XmlConverterConstants.Contents.CommonPart.InterruptingPoint
                    : XmlConverterConstants.Contents.CommonPart.InterruptingLetter);
                commonPartElement.Add(new XAttribute(
                    XmlConverterConstants.Contents.MissingEId, XmlConverterConstants.Contents.RepeatedCommonPart));
                Fill(commonPartElement, part, context);
                element.Add(commonPartElement);
            }
        }

        // Tekst części wspólnej jest kolejnym fragmentem zdania otwartego wprowadzeniem do wyliczenia.
        private static void Fill(XElement commonPartElement, CommonPart part, SentenceContext? context, bool sameFragment = false)
        {
            var textElement = Element(XmlConverterConstants.Contents.TextSection);
            if (context != null)
            {
                if (!sameFragment)
                    context.CurrentFragment += 1;
                textElement.Add(SentenceContext.ToXml(context, part.ContentText, dated: part));
            }
            else
            {
                textElement.Add(SentenceContext.ToXml(text: part.ContentText, dated: part));
            }

            commonPartElement.Add(new XAttribute(XmlConverterConstants.Contents.GUID, part.Guid.ToString()));
            UnitLifecycleXml.Apply(commonPartElement, part);
            commonPartElement.Add(textElement);

            // Dalsza treść części wspólnej: kolejny akapit w jej stylu (drugie brzmienie w tekście jednolitym).
            EmbeddedContentXmlConverter.AppendEmbedded(commonPartElement, part);
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
