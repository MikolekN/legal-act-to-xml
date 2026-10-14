using System.Xml.Linq;
using Saga.Model;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Kontekst parsowania zdania przechowujacy numer i GUID aktualnie przetwarzanego
    /// zdania oraz numer ostatnio napotkanego fragmentu zdania.
    /// </summary>
    internal sealed class SentenceContext
    {
        /// <summary>
        /// Numer ostatnio przetworzonego fragmentu zdania
        /// </summary>
        internal int CurrentFragment { get; set; } = 1;

        /// <summary>
        /// Numer przetwarzanego zdania
        /// </summary>
        internal int ReferenceSentenceNumber { get; init; }

        /// <summary>
        /// GUID przetwarzanego zdania
        /// </summary>
        internal string? ReferenceSentenceGUID { get; init; }

        internal SentenceContext(int currentFragment,
                                    int referenceSentenceNumber = 1,
                                    string? referenceSentenceGUID = null)
        {
            CurrentFragment = currentFragment;
            ReferenceSentenceNumber = referenceSentenceNumber;
            if (referenceSentenceGUID is null)
                referenceSentenceGUID = Guid.NewGuid().ToString();
            ReferenceSentenceGUID = referenceSentenceGUID;
        }

        internal static XElement ToXml(SentenceContext context, string text, IEntersIntoForce? dated = null)
        {
            var sentence = Element(XmlConverterConstants.Contents.Sentence.Element,
                new XAttribute(XmlConverterConstants.Contents.Sentence.Number, context.ReferenceSentenceNumber),
                new XAttribute(XmlConverterConstants.Contents.Sentence.GUID, context.ReferenceSentenceGUID is null ? Guid.NewGuid().ToString() : context.ReferenceSentenceGUID),
                new XAttribute(XmlConverterConstants.Contents.Sentence.Fragment, context.CurrentFragment),
                new XAttribute(XmlConverterConstants.Contents.Sentence.FragmentGUID, Guid.NewGuid().ToString()),
                text);
            UnitLifecycleXml.Apply(sentence, dated);
            return sentence;
        }

        /// <summary>
        /// Akapit jednostki redakcyjnej — schemat wymaga go zawsze, także gdy akapit źródła niesie samo oznaczenie
        /// („1)", a za nim wzór w osobnym akapicie): wtedy jedno puste zdanie.
        /// </summary>
        internal static XElement UnitParagraph(XElement textSection)
        {
            if (!textSection.HasElements)
                textSection.Add(ToXml(text: string.Empty));
            return textSection;
        }

        internal static XElement ToXml(string text, int number = 1, string? guid = null, int fragment = 1, IEntersIntoForce? dated = null)
        {
            var sentence = Element(XmlConverterConstants.Contents.Sentence.Element,
                new XAttribute(XmlConverterConstants.Contents.Sentence.Number, number),
                new XAttribute(XmlConverterConstants.Contents.Sentence.GUID, guid is null ? Guid.NewGuid().ToString() : guid),
                new XAttribute(XmlConverterConstants.Contents.Sentence.Fragment, fragment),
                new XAttribute(XmlConverterConstants.Contents.Sentence.FragmentGUID, Guid.NewGuid().ToString()),
                text);
            UnitLifecycleXml.Apply(sentence, dated);
            return sentence;
        }
    };
}
