namespace Saga.Core.Services
{
    /// <summary>
    /// Znaki cudzysłowu spotykane w aktach — jedno źródło dla granic cytatu, bilansu cudzysłowów i wzorców.
    /// Tekstu się nie normalizuje (eksport niesie znak ze źródła), więc każdy wariant trzeba rozpoznać
    /// tam, gdzie decyduje o strukturze (ADR-0030).
    /// </summary>
    internal static class QuotationMarks
    {
        /// <summary>„ (U+201E) — cudzysłów otwierający.</summary>
        internal const char Opening = '„';

        /// <summary>" (U+0022) — cudzysłów prosty: otwiera i zamyka (kanał TXT/PDF).</summary>
        internal const char Straight = '"';

        /// <summary>
        /// Cudzysłowy zamykające: ” (U+201D), “ (U+201C — niedomyślny wariant edytorów) i ˮ (U+02EE —
        /// litera modyfikująca wstawiana z klawiatury zamiast ”).
        /// </summary>
        internal const string Closing = "”“ˮ";

        /// <summary>Do klasy znaków we wzorcu: cudzysłów otwierający albo prosty.</summary>
        internal const string OpeningOrStraight = "„\"";

        /// <summary>Do klasy znaków we wzorcu: cudzysłów zamykający albo prosty.</summary>
        internal const string ClosingOrStraight = Closing + "\"";

        /// <summary>Do klasy znaków we wzorcu: dowolny cudzysłów.</summary>
        internal const string Any = "„" + Closing + "\"";

        internal static bool IsClosing(char character) => Closing.Contains(character);
    }
}
