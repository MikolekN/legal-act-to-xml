using System.Xml.Linq;
using Saga.Model;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Dalsza treść akapitu jednostki (schemat: <c>kontynuacja-akapitu</c>) — to, co stoi za jej zdaniami,
    /// a przed jednostkami podrzędnymi: akapit bez oznaczenia jako <c>fragment</c> i tabela jako <c>tabela</c>,
    /// w kolejności z aktu.
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
                else if (!string.IsNullOrEmpty(block.Text))
                    unitElement.Add(Element(XmlConverterConstants.Contents.ContinuationFragment,
                        new XAttribute(XmlConverterConstants.Contents.GUID, Guid.NewGuid().ToString()),
                        block.Text));
            }
        }
    }
}
