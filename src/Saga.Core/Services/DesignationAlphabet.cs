using System.Text.RegularExpressions;

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
        /// Litery cyrylicy nie do odróżnienia od łacińskich w druku i ich łacińskie odpowiedniki. Oznaczenie zna tylko
        /// litery łacińskie i polskie, więc taka litera w oznaczeniu to łacińska wpisana w innym układzie klawiatury
        /// („а)" z U+0430 — Dz. U. 2026 poz. 1324 i poprzedni tekst jednolity tej ustawy).
        /// </summary>
        private const string CyrillicLookalikes = "аеорсхуіјѕһԁ";
        private const string LatinOfLookalikes = "aeopcxyijshd";

        private static readonly Regex LeadingLetterDesignation = new(
            @"^\s*[" + QuotationMarks.OpeningOrStraight + @"]?(?<designation>[a-z" + CyrillicLookalikes + @"]{1,2})\)", RegexOptions.Compiled);

        /// <summary>
        /// Tekst akapitu z oznaczeniem litery na początku zapisanym literami łacińskimi („а) art. 6 …" → „a) art. 6 …");
        /// ten sam napis, gdy oznaczenie jest łacińskie albo go nie ma. Reszta tekstu zostaje jak w akcie.
        /// </summary>
        internal static string WithLatinLeadingDesignation(string text)
        {
            var designation = LeadingLetterDesignation.Match(text).Groups["designation"];
            if (!designation.Success || designation.Value.All(IsLowerLetter))
                return text;

            var latin = string.Concat(designation.Value.Select(c => CyrillicLookalikes.IndexOf(c) is var i and >= 0 ? LatinOfLookalikes[i] : c));
            return text[..designation.Index] + latin + text[(designation.Index + designation.Length)..];
        }

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
