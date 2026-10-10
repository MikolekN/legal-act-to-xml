namespace Saga.Core.Services
{
    /// <summary>
    /// Litery w oznaczeniach jednostek („2a", „art. 48ł", „lit. ł"). ZTP zna tylko łacińskie bez znaków
    /// diakrytycznych (§ 57 ust. 4), a akty wielokrotnie nowelizowane wstawiają też polskie — zawsze tuż
    /// za literą bazową (l → ł → m). Jedno źródło dla rozpoznania akapitu, powołań i ciągłości numeracji.
    /// </summary>
    internal static class DesignationAlphabet
    {
        private const string PolishLower = "ąćęłńóśźż";
        private const string PolishUpper = "ĄĆĘŁŃÓŚŹŻ";
        private const string BaseOfPolishLower = "acelnoszz";

        /// <summary>Klasa znaków wyrażenia regularnego: litera oznaczenia dowolnej wielkości.</summary>
        internal const string Letter = "[a-zA-Z" + PolishLower + PolishUpper + "]";

        /// <summary>Klasa znaków wyrażenia regularnego: mała litera oznaczenia.</summary>
        internal const string LowerLetter = "[a-z" + PolishLower + "]";

        /// <summary>Mała litera oznaczenia — łacińska albo polska.</summary>
        internal static bool IsLowerLetter(char c) => c is >= 'a' and <= 'z' || PolishLower.Contains(c);

        /// <summary>
        /// Oznaczenie sprowadzone do liter bazowych („ł" → „l") — do porównań kolejności: polska litera
        /// jest wariantem bazowej, tak jak „2a" jest wariantem „2".
        /// </summary>
        internal static string Fold(string designation) => string.Create(designation.Length, designation, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                var lower = char.ToLowerInvariant(source[i]);
                var index = PolishLower.IndexOf(lower);
                span[i] = index < 0 ? lower : BaseOfPolishLower[index];
            }
        });
    }
}
