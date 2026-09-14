using System;

namespace Saga.Model
{
    /// <summary>
    /// Kontrakt dla jednostek, które wchodzą w życie.
    /// </summary>
    public interface IEntersIntoForce
    {
        /// <summary>
        /// Data wejścia w życie danej jednostki redakcyjnej.
        /// </summary>
        DateTime EffectiveDate { get; set; }

        /// <summary>
        /// Data mocy wstecznej danej jednostki redakcyjnej.
        /// Wartość domyślna znaczy brak mocy wstecznej.
        /// </summary>
        DateTime RetroactiveFrom { get; set; }
    }
}
