using System.Text.RegularExpressions;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Nowelizacja zagnieżdżona w treści cytowanej (ADR-0082): cytowany przepis sam jest komendą („4) po art. 28a dodaje
    /// się art. 28b i art. 28c w brzmieniu:"), a za nim stoi jego własny cytat („„Art. 28b. …”;"). Akapity tego cytatu nie
    /// są jednostkami cytatu zewnętrznego — są treścią zmiany, którą wprowadza cytowany przepis. Tak samo wiersze
    /// cudzysłowu otwartego w środku akapitu („… zastępuje się wyrazami „…:", „1) do dnia 15 lipca …;", „2) … r”,"):
    /// to dalszy tekst tego akapitu, nie jednostki cytatu.
    /// </summary>
    internal static class NestedQuotes
    {
        /// <summary>Akapit cytatu zewnętrznego i cytat zagnieżdżony, który po nim stoi (pusty, gdy go nie ma).</summary>
        internal sealed record Item(CollectedAmendmentParagraph Paragraph, List<CollectedAmendmentParagraph> Nested)
        {
            /// <summary>
            /// Dalsze wiersze cudzysłowu, który akapit otwiera w środku i który domyka któryś z następnych akapitów —
            /// dalsza treść jednostki akapitu.
            /// </summary>
            public List<CollectedAmendmentParagraph> Continued { get; } = [];
        }

        // Komenda kończąca akapit: cytat, który po niej stoi, jest jej treścią — także „w następującym brzmieniu:".
        // Dwukropek bywa pominięty albo zastąpiony średnikiem, a „w" zgubione („dodaje się pkt 1b brzmieniu:"); że to
        // komenda, potwierdza drugi sygnał — cudzysłów otwierający następny akapit.
        private static readonly Regex Command = new(
            @"(?:otrzymuj[eą]\s+brzmienie|\bbrzmieniu)\s*[:;]?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex OpeningQuote = new(
            $@"^\s*[{QuotationMarks.OpeningOrStraight}]", RegexOptions.Compiled);

        /// <summary>
        /// Dzieli akapity treści cytowanej (bez cudzysłowów granicznych cytatu zewnętrznego) na akapity cytatu zewnętrznego,
        /// każdy z cytatem zagnieżdżonym, który po nim stoi. Cytat zagnieżdżony otwiera akapit zaczynający się cudzysłowem
        /// tuż po akapicie zakończonym komendą, a kończy domknięcie tego cudzysłowu. Akapit, który zostawia cudzysłów
        /// otwarty, bierze następne akapity aż do jego domknięcia jako dalsze wiersze; cudzysłów niedomknięty do końca
        /// treści (literówka) nie bierze niczego.
        /// </summary>
        internal static List<Item> Split(IReadOnlyList<CollectedAmendmentParagraph> paragraphs)
        {
            var items = new List<Item>();
            List<CollectedAmendmentParagraph>? nested = null;
            var depth = 0;

            for (var i = 0; i < paragraphs.Count; i++)
            {
                var paragraph = paragraphs[i];
                if (nested is not null)
                {
                    nested.Add(paragraph);
                    depth = DepthAfter(depth, paragraph);
                    if (depth == 0)
                        nested = null;
                    continue;
                }

                if (items.Count > 0 && items[^1] is { Nested.Count: 0, Continued.Count: 0 } previous && previous.Paragraph.Table is null
                    && Command.IsMatch(previous.Paragraph.Text.TrimEnd())
                    && paragraph.Table is null && OpeningQuote.IsMatch(paragraph.Text))
                {
                    nested = previous.Nested;
                    nested.Add(paragraph);
                    depth = DepthAfter(0, paragraph);
                    if (depth == 0)
                        nested = null;
                    continue;
                }

                var item = new Item(paragraph, []);
                items.Add(item);
                if (DepthAfter(0, paragraph) > 0 && Closing(paragraphs, i) is { } end)
                {
                    item.Continued.AddRange(paragraphs.Skip(i + 1).Take(end - i));
                    i = end;
                }
            }

            return items;
        }

        /// <summary>Akapit, w którym domyka się cudzysłów otwarty w akapicie <paramref name="opening"/>; null, gdy żaden.</summary>
        private static int? Closing(IReadOnlyList<CollectedAmendmentParagraph> paragraphs, int opening)
        {
            var depth = DepthAfter(0, paragraphs[opening]);
            for (var i = opening + 1; i < paragraphs.Count; i++)
            {
                depth = DepthAfter(depth, paragraphs[i]);
                if (depth == 0)
                    return i;
            }

            return null;
        }

        /// <summary>Głębokość cudzysłowu po akapicie: otwarcia minus zamknięcia, nie mniej niż zero.</summary>
        private static int DepthAfter(int depth, CollectedAmendmentParagraph paragraph)
        {
            if (paragraph.Table is not null)
                return depth;

            foreach (var character in paragraph.Text)
            {
                // Cudzysłów prosty otwiera, gdy żaden nie jest otwarty, a w przeciwnym razie zamyka.
                if (character == QuotationMarks.Opening || (character == QuotationMarks.Straight && depth == 0))
                    depth++;
                else if ((QuotationMarks.IsClosing(character) || character == QuotationMarks.Straight) && depth > 0)
                    depth--;
            }

            return depth;
        }
    }
}
