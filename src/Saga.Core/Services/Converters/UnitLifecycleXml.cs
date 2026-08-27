using System.Xml.Linq;
using Saga.Model;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Atrybuty cyklu życia jednostki (status-jednostki, wejscie-w-zycie)
    /// na samej jednostce oraz na jej zdaniach.
    /// </summary>
    internal static class UnitLifecycleXml
    {
        internal static void Apply(XElement element, IEntersIntoForce entity)
        {
            if (EntryDate(entity) is { } entryDate)
                element.Add(new XAttribute(XmlConverterConstants.Contents.EntryIntoForce, entryDate));
        }

        internal static string? EntryDate(IEntersIntoForce entity) =>
            entity.EffectiveDate == default ? null : entity.EffectiveDate.ToString("yyyy-MM-dd");
    }
}
