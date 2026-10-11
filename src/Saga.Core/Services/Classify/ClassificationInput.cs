namespace Saga.Core.Services.Classify
{
    /// <summary>
    /// Dane wejściowe dla <see cref="IParagraphClassifier"/>.
    /// </summary>
    public sealed record ClassificationInput(string Text, string? StyleId)
    {
        /// <summary>
        /// Opcjonalna podpowiedź numeracyjna obliczona przez orkiestrator.
        /// Gdy podana, klasyfikator stosuje karę <see cref="ConfidencePenaltyConfig.NumberingBreakPenalty"/>
        /// jeśli numer jednostki zaburza ciągłość.
        /// </summary>
        public NumberingHint? NumberingHint { get; init; }

        /// <summary>
        /// Czy samodzielny „§" jest w tym dokumencie jednostką podstawową (rozporządzenie, uchwała, zarządzenie)
        /// — wtedy „§ 5." jest tym, czym „Art. 5." w ustawie, a nie paragrafem kodeksu.
        /// </summary>
        public bool SectionIsBasicUnit { get; init; }
    }
}
