#nullable enable

namespace Saga.Model
{
    /// <summary>
    /// Przytoczenie przepisów innego aktu w treści jednostki („…, który stanowi: „Art. 19. …"") — tak
    /// obwieszczenie tekstu jednolitego podaje przepisy, których tekst jednolity nie obejmuje (§ 104 ZTP).
    /// Przytoczone jednostki nie należą do aktu, w którym stoją: adresuje się je w akcie, z którego pochodzą.
    /// </summary>
    public class Quotation
    {
        /// <summary>Przytoczone jednostki — ten sam kształt co treść cytowana w nowelizacji.</summary>
        public AmendmentContent Content { get; set; } = new();

        /// <summary>Akt, z którego pochodzi przytoczenie; null, gdy treść jednostki go nie wskazuje.</summary>
        public JournalInfo? SourceAct { get; set; }

        /// <summary>Interpunkcja za zamykającym cudzysłowem — należy do zdania jednostki, nie do przytoczenia.</summary>
        public string? TrailingPunctuation { get; set; }
    }
}
