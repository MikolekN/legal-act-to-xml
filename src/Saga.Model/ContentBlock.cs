#nullable enable

namespace Saga.Model
{
    /// <summary>
    /// Element treści bez własnego oznaczenia: akapit tekstu, tabela albo przytoczenie przepisów innego
    /// aktu — zawsze jedno z trojga. Stoi w dalszej treści jednostki (<see cref="BaseEntity.Continuation"/>)
    /// i w treści załącznika.
    /// </summary>
    public class ContentBlock
    {
        public string? Text { get; set; }

        public ContentTable? Table { get; set; }

        /// <summary>Przytoczenie rozpoznane jako jednostki („…, który stanowi: „Art. 19. …"").</summary>
        public Quotation? Quotation { get; set; }
    }
}
