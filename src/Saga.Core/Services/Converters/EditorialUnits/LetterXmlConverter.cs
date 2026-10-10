using System.Xml.Linq;
using Saga.Model.EditorialUnits;
using static Saga.Core.Services.Converters.CommonPartXmlConverter;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Konwertuje model Letter na element XML.
    /// </summary>
    internal static class LetterXmlConverter
    {
        /// <summary>
        /// Dopisuje literę do jednostki nadrzędnej. Litera niejawna (poziom pominięty w akcie) nie ma elementu:
        /// jej tirety i część wspólna stają wprost w jednostce nad nią, jako jednostki spoza ZTP.
        /// </summary>
        internal static void AppendTo(XElement parentElement, Letter letter, bool isParentPathological, SentenceContext? previousSentenceContext)
        {
            if (!letter.IsImplicit)
            {
                parentElement.Add(ToXml(letter, isParentPathological, previousSentenceContext));
                return;
            }

            foreach (var tiret in letter.Tirets)
                parentElement.Add(TiretXmlConverter.ToXml(tiret, isParentPathological: true, 1, previousSentenceContext));

            HandleCommonPart(letter, parentElement, previousSentenceContext);
        }

        internal static XElement ToXml(Letter letter, bool isParentPathological = false, SentenceContext? previousSentenceContext = null)
        {
            var isPathological = IsPathological(letter, isParentPathological);

            var letterElement = GetLetterElement(isPathological);

            letterElement.Add(new XAttribute(XmlConverterConstants.Contents.eId, letter.Id));
            letterElement.Add(new XAttribute(XmlConverterConstants.Contents.GUID, letter.Guid.ToString()));

            UnitLifecycleXml.Apply(letterElement, letter);

            letterElement.Add(Element(XmlConverterConstants.Contents.Num, letter.Number == null ? "UNKNOWN" : letter.Number.Designation));

            var textSectionElement = Element(XmlConverterConstants.Contents.TextSection);
            SentenceContext? contextToPass = SentenceProcessor.Process(letter.TextSegments, previousSentenceContext, textSectionElement);
            AmendmentXmlConverter.ToXml(letter.Amendment, textSectionElement.LastNode);
            if (!string.IsNullOrEmpty(letter.ContentText))
            {
                letterElement.Add(textSectionElement);
            }

            EmbeddedContentXmlConverter.AppendEmbedded(letterElement, letter);

            foreach (var tiret in letter.Tirets)
                letterElement.Add(TiretXmlConverter.ToXml(tiret, isPathological, 1, contextToPass));

            HandleCommonPart(letter, letterElement, contextToPass);

            return letterElement;
        }

        private static XElement GetLetterElement(bool isPathological) =>
            Element(isPathological
                ? XmlConverterConstants.Contents.NonZtpLetter
                : XmlConverterConstants.Contents.Letter);

        private static bool IsPathological(Letter letter, bool isParentPathological)
        {
            // TODO: unhandled if the structure of letter is not respected - code doesn't expect that e.g. secondary tiret can appear in a letter
            if (isParentPathological || letter.Tirets.Count == 1) // Every letter is expected to have no tirets or more than 1 tiret
                return true;
            else
                return false;
        }
    }
}
