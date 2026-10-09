namespace Saga.Core.Services.Classify
{
    /// <summary>
    /// Wynik klasyfikacji akapitu: typ jednostki redakcyjnej, pewność (1–100)
    /// oraz opcjonalne kary diagnostyczne.
    /// IsAmendmentContent to cecha równoległa do Kind — akapit nowelizacji zachowuje swój Kind.
    /// </summary>
    public sealed class ClassificationResult
    {
        public ParagraphKind Kind { get; init; } = ParagraphKind.Unknown;

        /// <summary>Pewność klasyfikacji w skali 1–100. 100 = pełna zgodność wszystkich sygnałów.</summary>
        public int Confidence { get; init; }

        /// <summary>
        /// True gdy akapit pochodzi z treści nowelizacji (styl Z/*).
        /// Nie wyklucza Kind — artykuł nowelizacji ma Kind=Article i IsAmendmentContent=true.
        /// </summary>
        public bool IsAmendmentContent { get; init; }

        /// <summary>
        /// Znormalizowany typ stylu Word ("ART","UST","PKT","LIT","TIR","WRAPUP","AMENDMENT", style jednostek
        /// systematyzacyjnych "ROZDZ_ODDZ","TYT_DZ","CZ_KS","PRZEDM", albo null).
        /// </summary>
        public string? StyleType { get; init; }

        /// <summary>
        /// Styl ustawy matki — zamyka nowelizację. Styl jednostki systematyzacyjnej jest nim tylko przy
        /// oznaczeniu potwierdzonym treścią: tytuł jednostki i komórka tabeli w stylu oznaczenia stoją
        /// w cytowanej treści nowelizacji.
        /// </summary>
        public bool HasParentLawStyle => StyleType switch
        {
            null or "AMENDMENT" => false,
            ParagraphClassifier.UnitHeadingStyle => false,
            ParagraphClassifier.ChapterOrSubchapterStyle or ParagraphClassifier.TitleOrDivisionStyle
                or ParagraphClassifier.PartOrBookStyle => Kind is ParagraphKind.PartUnit or ParagraphKind.BookUnit
                    or ParagraphKind.TitleUnit or ParagraphKind.DivisionUnit or ParagraphKind.ChapterUnit
                    or ParagraphKind.SubchapterUnit,
            _ => true,
        };

        /// <summary>Kary zastosowane podczas obliczania Confidence — do celów diagnostycznych.</summary>
        public IReadOnlyList<ClassificationPenalty> Penalties { get; init; } = [];
    }
}
