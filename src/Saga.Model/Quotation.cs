#nullable enable

namespace Saga.Model
{
    /// <summary>
    /// Przytoczenie w treści jednostki — tekst w cudzysłowie, który nie jest nowelizacją:
    /// <list type="bullet">
    /// <item>przepisy innego aktu („…, który stanowi: „Art. 19. …"") — tak obwieszczenie tekstu jednolitego podaje
    /// przepisy, których tekst jednolity nie obejmuje (§ 104 ZTP); przytoczone jednostki adresuje się w akcie,
    /// z którego pochodzą;</item>
    /// <item>tekst, którego brzmienie przepis ustala („oświadczenie o następującej treści: „…"", rota ślubowania) —
    /// nie pochodzi z żadnego aktu, więc jego jednostki nie mają adresu (ADR-0071).</item>
    /// </list>
    /// </summary>
    public class Quotation
    {
        /// <summary>Przytoczone jednostki — ten sam kształt co treść cytowana w nowelizacji.</summary>
        public AmendmentContent Content { get; set; } = new();

        /// <summary>Akt, z którego pochodzi przytoczenie; null, gdy treść jednostki go nie wskazuje.</summary>
        public JournalInfo? SourceAct { get; set; }

        /// <summary>Interpunkcja za zamykającym cudzysłowem — należy do zdania jednostki, nie do przytoczenia.</summary>
        public string? TrailingPunctuation { get; set; }

        /// <summary>Czy treść otwiera cudzysłów. Przytoczenie przepisów w obwieszczeniu ma oba.</summary>
        public bool HasOpeningQuote { get; set; } = true;

        /// <summary>Czy treść zamyka cudzysłów.</summary>
        public bool HasClosingQuote { get; set; } = true;
    }
}
