namespace Saga.Core.Services
{
    /// <summary>
    /// Znaki myślnika spotykane w aktach — jedno źródło dla znacznika tiretu, części wspólnej zaczynającej się
    /// od myślnika, przedziału oznaczeń i legendy wzoru. Szczotki piszą ten sam znak redakcyjny różnie: dywizem,
    /// półpauzą, myślnikiem cyfrowym (U+2012), pauzą, minusem. Tekstu się nie normalizuje (eksport niesie znak
    /// ze źródła), więc każdy wariant trzeba rozpoznać tam, gdzie decyduje o strukturze (ADR-0040).
    /// Znaki są zapisane kodami: w edytorze myślnik cyfrowy, półpauza i minus wyglądają tak samo.
    /// </summary>
    internal static class Dashes
    {
        /// <summary>
        /// Do klasy znaków we wzorcu: dywiz (U+002D), dywiz typograficzny i nierozdzielający (U+2010, U+2011),
        /// myślnik cyfrowy (U+2012), półpauza (U+2013), pauza (U+2014), kreska pozioma (U+2015), minus (U+2212).
        /// </summary>
        internal const string Any = @"\-\u2010-\u2015\u2212";

        /// <summary>
        /// Do klasy znaków we wzorcu: myślniki, które nie bywają częścią oznaczenia ani symbolu („n-1", „a-c"
        /// pisze się dywizem) — myślnik cyfrowy, półpauza, pauza, kreska pozioma.
        /// </summary>
        internal const string Long = @"\u2012-\u2015";

        /// <summary>Znaki myślnika jako tablica — do dzielenia przedziału oznaczeń („85c-85j").</summary>
        internal static readonly char[] Characters =
            ['-', '\u2010', '\u2011', '\u2012', '\u2013', '\u2014', '\u2015', '\u2212'];

        internal static bool IsDash(char character) =>
            character == '-' || (character >= '\u2010' && character <= '\u2015') || character == '\u2212';
    }
}
