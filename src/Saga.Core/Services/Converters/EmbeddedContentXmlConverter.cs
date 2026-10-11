using System.Xml.Linq;
using Saga.Model;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Dalsza treść akapitu jednostki (schemat: <c>kontynuacja-akapitu</c>) — to, co stoi za jej zdaniami,
    /// a przed jednostkami podrzędnymi: akapit bez oznaczenia jako <c>fragment</c>, tabela jako <c>tabela</c>
    /// i wzór w osobnym akapicie jako <c>wzor-mat</c> z wyrażeniem w MathML, w kolejności z aktu.
    /// </summary>
    internal static class EmbeddedContentXmlConverter
    {
        /// <summary>Dopisuje dalszą treść jednostki do jej elementu; wywoływane tuż za elementem <c>p</c>.</summary>
        internal static void AppendEmbedded(XElement unitElement, BaseEntity entity)
        {
            foreach (var block in entity.Continuation)
            {
                if (block.Table is { } table)
                    unitElement.Add(TableXmlConverter.ToXml(table));
                else if (block.Formula is { } formula)
                    unitElement.Add(Formula(formula));
                else if (!string.IsNullOrEmpty(block.Text))
                    unitElement.Add(Element(XmlConverterConstants.Contents.ContinuationFragment,
                        new XAttribute(XmlConverterConstants.Contents.GUID, Guid.NewGuid().ToString()),
                        block.Text));
            }
        }

        /// <summary>
        /// Wzór jako <c>wzor-mat</c> → <c>wzor</c> → <c>mml:math</c>. Przedrostek przestrzeni nazw MathML deklaruje
        /// sam element wzoru — dokument bez wzorów nie niesie tej deklaracji.
        /// </summary>
        private static XElement Formula(ContentFormula formula)
        {
            var math = XElement.Parse(formula.MathMl);
            foreach (var element in math.DescendantsAndSelf())
                element.Attributes().Where(attribute => attribute.IsNamespaceDeclaration).Remove();

            return Element(XmlConverterConstants.Contents.Formula.Element,
                new XAttribute(XNamespace.Xmlns + XmlConverterConstants.Contents.Formula.MathMlPrefix, math.Name.Namespace),
                new XAttribute(XmlConverterConstants.Contents.GUID, Guid.NewGuid().ToString()),
                Element(XmlConverterConstants.Contents.Formula.Expression, math));
        }
    }
}
