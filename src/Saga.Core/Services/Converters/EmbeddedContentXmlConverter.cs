using System.Xml.Linq;
using Saga.Model;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Konwertuje treść osadzoną jednostki redakcyjnej: tabele,
    /// grafiki i wzory matematyczne.
    /// </summary>
    internal static class EmbeddedContentXmlConverter
    {
        /// <summary>
        /// Dołącza treść osadzoną jednostki (tabele, grafiki, wzory) do elementu jednostki.
        /// </summary>
        internal static void AppendEmbedded(XElement unitElement, BaseEntity entity)
        {
            // foreach (var table in entity.Tables)
            //     unitElement.Add(TableXmlConverter.ToXml(table));

            // foreach (var image in entity.Images)
            //     unitElement.Add(ImageXmlConverter.ToXml(image));

            // foreach (var formula in entity.Formulas)
            //     unitElement.Add(FormulaXmlConverter.ToXml(formula));
        }
    }
}
