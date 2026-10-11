using System.Text.RegularExpressions;
using Saga.Model;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Legenda wzoru: wprowadzenie („gdzie poszczególne symbole oznaczają:") i opisy symboli
    /// („I_e – ilość energii elektrycznej …") stojące tuż za wzorem. Należą do wzoru, nie do dalszej treści
    /// jednostki — schemat ma dla nich <c>wprowadzenie-do-opisu</c> i <c>opis</c> wewnątrz <c>wzor-mat</c>.
    ///
    /// Legenda jest otwarta, dopóki ostatnim elementem dalszej treści jednostki jest wzór: wiersz, który
    /// nie jest ani wprowadzeniem, ani opisem symbolu, zostaje zwykłym akapitem i ją zamyka.
    /// </summary>
    internal static class FormulaLegend
    {
        /// <summary>Wprowadzenie do opisów: zdanie zakończone dwukropkiem, zapowiadające znaczenie symboli.</summary>
        private static readonly Regex Introduction = new(
            @"^(?:gdzie|w\s+który[mch]+|przy\s+czym)\b.*:$|^.*\boznacza(?:ją)?\s*:$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>Opis symbolu: krótki symbol, myślnik, objaśnienie.</summary>
        private static readonly Regex Entry = new(
            @"^(?<symbol>[^\s–—\-][^–—]{0,40}?)\s*[–—\-]\s*(?<explanation>\S.*)$",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex LeadingDash = new(@"^\s*[–—\-]\s*", RegexOptions.Compiled);

        /// <summary>
        /// Dołącza wiersz do legendy wzoru, za którym stoi; false, gdy wiersz legendą nie jest.
        /// </summary>
        /// <param name="unit">Jednostka, do której trafiłby wiersz jako dalsza treść.</param>
        /// <param name="leadingSymbol">Równanie otwierające wiersz (symbol), gdy źródło je niesie.</param>
        internal static bool TryAbsorb(BaseEntity unit, string text, FormulaSymbol? leadingSymbol)
        {
            if (unit.Continuation.LastOrDefault()?.Formula is not { } formula)
                return false;

            text = text.Trim();

            if (formula.LegendIntroduction is null && formula.Legend.Count == 0 && Introduction.IsMatch(text))
            {
                formula.LegendIntroduction = text;
                return true;
            }

            if (Describe(text, leadingSymbol) is not { } description)
                return false;

            formula.Legend.Add(description);
            return true;
        }

        private static FormulaSymbol? Describe(string text, FormulaSymbol? leadingSymbol)
        {
            // Symbol jako równanie na początku wiersza: reszta wiersza, bez myślnika, to objaśnienie.
            if (leadingSymbol is { Symbol.Length: > 0 } symbol && text.StartsWith(symbol.Symbol, StringComparison.Ordinal))
            {
                var rest = text[symbol.Symbol.Length..];
                if (!LeadingDash.IsMatch(rest))
                    return null;

                var explanation = LeadingDash.Replace(rest, string.Empty, 1).Trim();
                return explanation.Length == 0
                    ? null
                    : new FormulaSymbol { Symbol = symbol.Symbol, SymbolMathMl = symbol.SymbolMathMl, Explanation = explanation };
            }

            return Entry.Match(text) is { Success: true } entry
                ? new FormulaSymbol
                {
                    Symbol = entry.Groups["symbol"].Value.Trim(),
                    Explanation = entry.Groups["explanation"].Value.Trim(),
                }
                : null;
        }
    }
}
