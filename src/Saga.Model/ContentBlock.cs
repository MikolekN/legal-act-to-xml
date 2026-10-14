#nullable enable

namespace Saga.Model
{
    /// <summary>
    /// Element treści bez własnego oznaczenia: akapit tekstu, tabela albo przytoczenie — zawsze jedno z trojga. Stoi w dalszej treści jednostki (<see cref="BaseEntity.Continuation"/>)
    /// i w treści załącznika. Akapit będący wzorem niesie obok tekstu (zapis liniowy) wyrażenie w MathML.
    /// </summary>
    public class ContentBlock
    {
        public string? Text { get; set; }

        /// <summary>Wzór w osobnym akapicie; <see cref="Text"/> niesie wtedy jego zapis liniowy.</summary>
        public ContentFormula? Formula { get; set; }

        public ContentTable? Table { get; set; }

        /// <summary>
        /// Przytoczenie: przepisy innego aktu („…, który stanowi: „Art. 19. …"") albo tekst, którego brzmienie
        /// przepis ustala („oświadczenie o następującej treści: „…"").
        /// </summary>
        public Quotation? Quotation { get; set; }
    }
}
