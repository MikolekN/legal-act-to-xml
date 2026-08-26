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
    }
}
