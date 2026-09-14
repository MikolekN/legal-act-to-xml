using Saga.Model;

namespace Saga.Core.Services.EntryIntoForce
{
    /// <summary>
    /// Która z dwóch dat jednostki rządzi jej wejściem w życie.
    ///
    /// Moc wsteczna przysłania datę wejścia w życie.
    /// Model trzyma obie daty osobno, a przysłonięcie
    /// zachodzi dopiero przy zapisie do formatu wyjściowego.
    /// </summary>
    public static class GoverningDate
    {
        public static DateTime? Of(IEntersIntoForce? entity)
        {
            if (entity is null)
                return null;

            if (entity.RetroactiveFrom != default)
                return entity.RetroactiveFrom;

            return entity.EffectiveDate == default ? null : entity.EffectiveDate;
        }

        public static string? Text(IEntersIntoForce? entity) =>
            Of(entity) is { } date ? date.ToString("yyyy-MM-dd") : null;
    }
}
