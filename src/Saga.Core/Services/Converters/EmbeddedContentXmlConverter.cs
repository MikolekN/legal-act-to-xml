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
            var math = Math(formula.MathMl);

            var element = Element(XmlConverterConstants.Contents.Formula.Element,
                new XAttribute(XNamespace.Xmlns + XmlConverterConstants.Contents.Formula.MathMlPrefix, math.Name.Namespace),
                new XAttribute(XmlConverterConstants.Contents.GUID, Guid.NewGuid().ToString()),
                Element(XmlConverterConstants.Contents.Formula.Expression, math));

            if (formula.LegendIntroduction is { Length: > 0 } introduction)
                element.Add(Element(XmlConverterConstants.Contents.Formula.LegendIntroduction, introduction));

            element.Add(formula.Legend.Select(Description));
            return element;
        }

        /// <summary>
        /// Opis symbolu: symbol (MathML, gdy w źródle jest równaniem; inaczej tekst) i objaśnienie podzielone
        /// na zdania, jak treść jednostki.
        /// </summary>
        private static XElement Description(FormulaSymbol symbol)
        {
            var explanation = Element(XmlConverterConstants.Contents.TextSection);
            SentenceProcessor.Process(Parsing.ParsingFactories.SplitIntoSentences(symbol.Explanation), null, explanation);

            return Element(XmlConverterConstants.Contents.Formula.Description,
                symbol.SymbolMathMl is { Length: > 0 } mathMl
                    ? Element(XmlConverterConstants.Contents.Formula.SymbolAsMath, Math(mathMl))
                    : Element(XmlConverterConstants.Contents.Formula.SymbolAsText, symbol.Symbol),
                Element(XmlConverterConstants.Contents.Formula.Explanation, explanation));
        }

        // Deklaracje przestrzeni nazw z zapisanego MathML ustępują deklaracji na elemencie wzoru.
        private static XElement Math(string mathMl)
        {
            var math = XElement.Parse(mathMl);
            foreach (var element in math.DescendantsAndSelf())
                element.Attributes().Where(attribute => attribute.IsNamespaceDeclaration).Remove();
            return math;
        }
    }
}
