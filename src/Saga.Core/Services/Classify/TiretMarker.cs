using System.Text.RegularExpressions;

namespace Saga.Core.Services.Classify
{
    /// <summary>
    /// Znacznik tiretu: myślnik, a przy zagnieżdżeniu tyle myślników, ile wynosi poziom („– –" to podwójne
    /// tiret, „– – –" potrójne). Jedno źródło głębokości tiretu zapisanej w treści i zdejmowania znacznika.
    /// </summary>
    internal static class TiretMarker
    {
        /// <summary>
        /// Myślniki znacznika rozdzielone odstępem. Kolejny myślnik należy do znacznika tylko wtedy, gdy stoi
        /// po nim odstęp — „– -5 °C" to tiret pierwszego poziomu z treścią zaczynającą się od minusa.
        /// </summary>
        private static readonly Regex Marker = new(
            $@"^{ParagraphClassifier.OptionalQuotePrefix}(?<dash>[{Dashes.Any}]+)(?:\s+(?<dash>[{Dashes.Any}]+)(?=\s))*\s*",
            RegexOptions.Compiled);

        /// <summary>Poziom zapisany znacznikiem; 0, gdy tekst nie zaczyna się myślnikiem.</summary>
        internal static int Depth(string text)
        {
            var match = Marker.Match(text.Trim());
            return match.Success ? match.Groups["dash"].Captures.Count : 0;
        }

        /// <summary>Treść tiretu bez znacznika (i bez cudzysłowu otwierającego przed nim).</summary>
        internal static string Strip(string text) => Marker.Replace(text.Trim(), "", 1);
    }
}
