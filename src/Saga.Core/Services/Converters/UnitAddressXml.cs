using System.Xml.Linq;
using Saga.Model;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Adres jednostki redakcyjnej w eksporcie: <c>eId</c> albo nazwany powód jego braku (<c>brak-eid</c>).
    /// Schemat traktuje brak adresu jako decyzję, więc jednostka bez eId zawsze mówi dlaczego (ADR-0024).
    /// </summary>
    internal static class UnitAddressXml
    {
        /// <summary>Jednostka z odczytanym oznaczeniem („2a", „b") — tylko taka wnosi segment do adresu.</summary>
        internal static bool HasDesignation(BaseEntity unit) => !string.IsNullOrEmpty(unit.Number?.Designation);

        /// <summary>
        /// <c>eId</c> jednostki adresowanej albo <c>brak-eid="jednostka-nieoznaczona"</c>: jednostka niejawna
        /// i jednostka bez odczytanego oznaczenia nie mają adresu, a ich adresem jest najbliższy oznaczony przodek.
        /// </summary>
        internal static XAttribute Address(BaseEntity unit, bool addressed) => addressed
            ? new XAttribute(XmlConverterConstants.Contents.eId, unit.Id)
            : new XAttribute(XmlConverterConstants.Contents.MissingEId, XmlConverterConstants.Contents.UnmarkedUnit);

        /// <summary>Oznaczenie jednostki; przy braku odczytu element zostaje pusty — nie ma czego wpisać.</summary>
        internal static XElement Number(BaseEntity unit) =>
            Element(XmlConverterConstants.Contents.Num, unit.Number?.Designation ?? string.Empty);
    }
}
