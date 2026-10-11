using System.Text.RegularExpressions;

namespace Saga.Core.Services.Classify
{
    /// <summary>
    /// Czy akapit zaczyna się czymś, co wygląda na oznaczenie jednostki — także spoza wzorców klasyfikatora
    /// („l)" zamiast „1)", „A)", „3-5.", „3.8.", „2.1 wykorzystuje …", tiret pisany myślnikiem cyfrowym (U+2012)
    /// albo ze znacznikiem odnośnika tuż za myślnikiem). Rozstrzyga o akapicie złożonym stylem jednostki,
    /// którego oznaczenia wzorce nie odczytały: z oznaczeniem spoza wzorca pozostaje jednostką bez odczytanego
    /// oznaczenia (ADR-0024), bez żadnego oznaczenia jest dalszą treścią jednostki, za którą stoi (ADR-0039).
    /// </summary>
    internal static class UnitDesignation
    {
        // Liczba (z literą, indeksem, przedziałem albo kolejnym poziomem) z kropką lub nawiasem; oznaczenie
        // wielopoziomowe („2.1", „3.8.2") także bez znaku końcowego; do trzech liter z nawiasem; liczba rzymska
        // z kropką lub nawiasem — za oznaczeniem odstęp, koniec akapitu albo znacznik odnośnika („1)[2)] …").
        // Myślnik na początku akapitu jest oznaczeniem tiretu bez względu na to, co po nim stoi.
        // Opcjonalnie „§" i cudzysłów otwierający przed oznaczeniem.
        private static readonly Regex Shape = new(
            $@"^\s*[{QuotationMarks.OpeningOrStraight}]?\s*(?:(?:(?:§\s*)?\d[\w\[\]{Dashes.Any}.]{{0,8}}?[.)]|\d+(?:\.\d+[a-z]?)+|\p{{L}}{{1,3}}\)|[IVXLCDM]{{1,6}}[.)])(?:\s|$|\[)|[{Dashes.Any}])",
            RegexOptions.Compiled);

        internal static bool LooksDesignated(string text) => Shape.IsMatch(text);
    }
}
