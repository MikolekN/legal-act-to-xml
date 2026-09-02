namespace Saga.Core.Ingest
{
    /// <summary>
    // Rola bloku w dokumencie. Bloki spoza głównej treści (przypisy, nagłówek strony)
	/// są pomijane przez parser strukturalny, ale mogą zasilać innych konsumentów —
	/// klasyfikator dokumentu (sygnały tekstu jednolitego — § 102-106 ZTP) albo parser
	/// metadanych publikatora.
    /// </summary>
    public enum BlockRole
    {
        Body,
        FootnoteText,
        JournalHeader,
    }
}
