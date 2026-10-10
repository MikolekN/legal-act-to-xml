using System.Text.RegularExpressions;

namespace Saga.Core
{
    public static class StringExtensions
    {
        private static readonly Regex WhitespaceCollapse = new(@"\s+", RegexOptions.Compiled);

        /// <summary>
        /// Tekst do ROZPOZNAWANIA (klasyfikacja dokumentu, nagłówek publikatora): odstępy zwinięte,
        /// półpauza sprowadzona do dywizu, żeby wzorce nie musiały znać obu znaków.
        /// </summary>
        public static string Sanitize(this string input)
        {
            return input.CollapseWhitespace().Replace("\u2013", "-");
        }

        /// <summary>
        /// Tekst do TREŚCI jednostek: odstępy zwinięte, znaki jak w akcie. Półpauza zostaje półpauzą —
        /// plik ma być wiernym zapisem tego, co ogłoszono.
        /// </summary>
        public static string CollapseWhitespace(this string input) => WhitespaceCollapse.Replace(input, " ");
    }
}
