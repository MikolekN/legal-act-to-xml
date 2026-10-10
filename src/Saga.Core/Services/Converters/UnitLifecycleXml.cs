using System.Xml.Linq;
using Saga.Core.Services.EntryIntoForce;
using Saga.Model;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Obie daty jednostki dołączone do jej elementu jako adnotacja (poza XML-em): atrybut
    /// <c>wejscie-w-zycie</c> niesie tylko jedną z nich (ADR-0013), a grupę czasową wyznaczają obie.
    /// </summary>
    internal sealed record UnitDates(DateTime Entry, DateTime Retroactive);

    /// <summary>Atrybut <c>wejscie-w-zycie</c> jednostki, zdania i części wspólnej.</summary>
    internal static class UnitLifecycleXml
    {
        internal static void Apply(XElement element, IEntersIntoForce? entity)
        {
            if (GoverningDate.Text(entity) is not { } entryDate)
                return;

            element.Add(new XAttribute(XmlConverterConstants.Contents.EntryIntoForce, entryDate));
            element.AddAnnotation(new UnitDates(entity!.EffectiveDate, entity.RetroactiveFrom));
        }
    }
}
