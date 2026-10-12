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
            // Wersja czasowa jednostki tekstu jednolitego: „okres" dopisuje przebieg grup czasowych, tu stają
            // projekcje okresu na daty i stan jednostki (ADR-0060). Jednostka uchylana nie „wchodzi w życie" —
            // jej wersję przyszłą opisują okres i stan.
            if (((entity as BaseEntity)?.Version ?? (entity as TextSegment)?.Version) is { } version)
            {
                element.AddAnnotation(version);
                var future = version.Kind == WordingVersionKind.Future;
                if (version.ChangeDate is { } day && !(future && version.ChangeRepeals))
                {
                    element.Add(new XAttribute(
                        future ? XmlConverterConstants.Contents.EntryIntoForce : XmlConverterConstants.Contents.InForceUntil,
                        day.ToString("yyyy-MM-dd")));
                }

                if (future && version.ChangeRepeals && version.ChangeFootnote.Length > 0)
                    element.Add(new XAttribute(XmlConverterConstants.Contents.UnitState, XmlConverterConstants.Contents.UnitStateRepealed));
                return;
            }

            if (GoverningDate.Text(entity) is not { } entryDate)
                return;

            element.Add(new XAttribute(XmlConverterConstants.Contents.EntryIntoForce, entryDate));
            element.AddAnnotation(new UnitDates(entity!.EffectiveDate, entity.RetroactiveFrom));
        }
    }
}
