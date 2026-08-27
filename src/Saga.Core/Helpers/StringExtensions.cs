using System.Text.RegularExpressions;

namespace Saga.Core
{
    public static class StringExtensions
    {
        private static readonly Regex WhitespaceCollapse = new(@"\s+", RegexOptions.Compiled);

        public static string Sanitize(this string input)
        {
            input = WhitespaceCollapse.Replace(input, " ");
            return input.Replace("\u2013", "-");
        }
    }
}
