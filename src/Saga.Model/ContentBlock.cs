#nullable enable

namespace Saga.Model
{
    /// <summary>
    /// Element treści bez własnego oznaczenia: akapit tekstu albo tabela — zawsze jedno z dwojga.
    /// Stoi w dalszej treści jednostki (<see cref="BaseEntity.Continuation"/>) i w treści załącznika.
    /// </summary>
    public class ContentBlock
    {
        public string? Text { get; set; }

        public ContentTable? Table { get; set; }
    }
}
