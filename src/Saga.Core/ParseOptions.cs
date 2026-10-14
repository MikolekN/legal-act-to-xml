using Saga.Core.Ingest;

namespace Saga.Core
{
    /// <summary>
    /// Polityka decydująca, czy po klasyfikacji dokumentu budować model strukturalny.
    /// Decyzja „czy parsować nie-akt" należy do wywołującego (rozstrzygnięcie planu) —
    /// klasyfikator wyłącznie raportuje.
    /// </summary>
    public enum ParsePolicy
    {
        /// <summary>Buduj model tylko, gdy klasyfikator rozpoznał akt prawny (domyślna).</summary>
        ParseWhenLegalAct,

        /// <summary>Buduj model zawsze — „parsuj mimo wszystko".</summary>
        AlwaysParse,

        /// <summary>Tylko klasyfikacja — bez budowy modelu (Document = null).</summary>
        ClassifyOnly,
    }

    /// <summary>Opcje parsowania uniwersalnego wejścia.</summary>
    public sealed record ParseOptions
    {
        public static ParseOptions Default { get; } = new();

        public ParsePolicy Policy { get; init; } = ParsePolicy.ParseWhenLegalAct;

        /// <summary>Wymuszony format źródłowy.</summary>
        public SourceFormat? ForcedFormat { get; init; }

        /// <summary>
        /// Dane ogłoszenia, których dokument nie niesie (szczotka przed publikacją, akt bez nagłówka publikatora);
        /// uzupełniają tylko braki — dane z dokumentu wygrywają (ADR-0081).
        /// </summary>
        public PublicationData? Publication { get; init; }
    }
}
