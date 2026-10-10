using System.Xml.Linq;
using Saga.Model;
using Saga.Model.EditorialUnits;
using Saga.Model.SystematizingUnits;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Jednostki systematyzacyjne w eksporcie schemaPL (ADR-0020). Jednostka niejawna, której wszyscy
    /// przodkowie są niejawni, nie jest emitowana — jej treść staje na poziomie aktu, więc akt bez jawnych
    /// jednostek eksportuje się jak płaska lista artykułów. Pod jawną jednostką niejawne poziomy pośrednie
    /// wychodzą jako „-n", bo schemat ich wymaga (rozdział trzyma wyłącznie oddziały).
    /// </summary>
    internal static class SystematizingUnitXmlConverter
    {
        private sealed record Names(string Element, string Implicit, string Header);

        /// <summary>Elementy poziomu aktu z jednej części — jednostki jawne albo artykuły z niejawnego łańcucha.</summary>
        internal static IEnumerable<XElement> ActLevel(Part part) => Emit(part);

        private static IEnumerable<XElement> Emit(BaseEntity unit)
        {
            if (!((ISystematizingUnit)unit).IsImplicit)
            {
                yield return ToXml(unit, actLevel: true);
                yield break;
            }

            foreach (var child in Children(unit))
            {
                if (child is Article article)
                {
                    yield return ArticleXmlConverter.ToXml(article);
                    continue;
                }

                foreach (var element in Emit(child))
                    yield return element;
            }
        }

        /// <summary>Jednostka z całym poddrzewem; <paramref name="actLevel"/> — czy stoi wprost w akcie.</summary>
        internal static XElement ToXml(BaseEntity unit, bool actLevel)
        {
            var systematizing = (ISystematizingUnit)unit;
            var names = NamesOf(unit, actLevel);

            XElement element;
            if (systematizing.IsImplicit)
            {
                element = Element(names.Implicit,
                    UnitAddressXml.Address(unit, addressed: false),
                    new XAttribute(XmlConverterConstants.Contents.GUID, unit.Guid.ToString()));
                UnitLifecycleXml.Apply(element, unit);
            }
            else
            {
                element = Element(names.Element,
                    new XAttribute(XmlConverterConstants.Contents.eId, unit.Id),
                    new XAttribute(XmlConverterConstants.Contents.GUID, unit.Guid.ToString()));
                UnitLifecycleXml.Apply(element, unit);
                element.Add(Header(names.Header, unit.Number, systematizing.Heading));
            }

            foreach (var child in Children(unit))
                element.Add(child is Article article
                    ? ArticleXmlConverter.ToXml(article, inSubchapter: true)
                    : ToXml(child, actLevel: false));

            return element;
        }

        /// <summary>Nagłówek: oznaczenie jak w akcie (z indeksem górnym) i tytuł, jeśli jest.</summary>
        private static XElement Header(string name, EntityNumber? number, string heading)
        {
            var designation = number == null ? string.Empty : number.RawValue ?? number.Value;
            if (number != null && !string.IsNullOrEmpty(number.Superscript))
                designation += $"[{number.Superscript}]";

            var header = Element(name, Element(XmlConverterConstants.Contents.Num, designation));
            if (!string.IsNullOrWhiteSpace(heading))
                header.Add(Element(XmlConverterConstants.Contents.TextSection, heading));
            return header;
        }

        private static IEnumerable<BaseEntity> Children(BaseEntity unit) => SystematizingHierarchy.Children(unit);

        private static Names NamesOf(BaseEntity unit, bool actLevel) => unit switch
        {
            Part => new(XmlConverterConstants.Contents.Part.Element, XmlConverterConstants.Contents.Part.ElementImplicit,
                XmlConverterConstants.Contents.Part.Header),
            Book => new(XmlConverterConstants.Contents.Book.Element, XmlConverterConstants.Contents.Book.ElementImplicit,
                XmlConverterConstants.Contents.Book.Header),
            // Na poziomie aktu „tytul" jest już tytułem aktu — jednostka nazywa się tam „tytul-jednostka".
            Title when actLevel => new(XmlConverterConstants.Contents.Title.ActLevelElement,
                XmlConverterConstants.Contents.Title.ActLevelElementImplicit, XmlConverterConstants.Contents.Title.Header),
            Title => new(XmlConverterConstants.Contents.Title.Element, XmlConverterConstants.Contents.Title.ElementImplicit,
                XmlConverterConstants.Contents.Title.Header),
            Division => new(XmlConverterConstants.Contents.Division.Element, XmlConverterConstants.Contents.Division.ElementImplicit,
                XmlConverterConstants.Contents.Division.Header),
            Chapter => new(XmlConverterConstants.Contents.Chapter.Element, XmlConverterConstants.Contents.Chapter.ElementImplicit,
                XmlConverterConstants.Contents.Chapter.Header),
            Subchapter => new(XmlConverterConstants.Contents.Subchapter.Element, XmlConverterConstants.Contents.Subchapter.ElementImplicit,
                XmlConverterConstants.Contents.Subchapter.Header),
            _ => throw new ArgumentException($"Nie jest jednostką systematyzacyjną: {unit.GetType().Name}", nameof(unit)),
        };
    }
}
