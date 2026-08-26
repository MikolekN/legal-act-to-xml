using System.Xml.Linq;
using Saga.Core.Services.Converters;

namespace Saga.Core
{
    /// <summary>
    /// Nakładki na API XLinq dodające przestrzeń nazw do tworzonych elementów.
    /// </summary>
    internal static class XLinqWrappers
    {
        private static readonly XNamespace Ns = XmlConverterConstants.Namespace;
        private static readonly XNamespace Mml = XmlConverterConstants.MathMLNamespace;

        internal static XElement Element(string name, params object[] content) =>
            new XElement(Ns + name, content);

        internal static IEnumerable<XElement> GetChildrenByName(XElement parent, string name)
        {
            return parent.Elements(Ns + name);
        }

        internal static XElement MathMLElement(string name, params object[] content) =>
            new XElement(Mml + name, content);

        internal static bool ContainsMathML(XElement element)
        {
            return element.DescendantsAndSelf().Any(e =>
                e.Name.Namespace == Mml ||
                e.Attributes().Any(a => a.Name.Namespace == Mml));
        }
    }
}
