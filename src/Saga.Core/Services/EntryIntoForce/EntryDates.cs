namespace Saga.Core.Services.EntryIntoForce
{
    /// <summary>
    /// Para dat rządzących jednostką: data wejścia w życie i moc wsteczna.
    ///
    /// Typ istnieje po to, żeby daty propagowane były parami. Propagowanie ich osobno
    /// ułatwiałoby przypadkowe zamienienie kolejności. Elementy są od siebie niezależne.
    /// </summary>
    internal readonly record struct EntryDates(DateTime? Entry, DateTime? RetroactiveFrom);
}
