using System.Text.RegularExpressions;
using System.Xml.Linq;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Sankcja karna w treści (ADR-0119): zwrot wymierzający karę (§ 79 ZTP), także administracyjną karę pieniężną
    /// (§ 81a ZTP). Część wspólna, której zdanie jest sankcją, dostaje <c>rola="sankcja-karna"</c> — sankcja i domknięcie
    /// wyliczenia to dwie cechy jednego akapitu. Poza częścią wspólną zwrot od „podlega" do końca zdania, bez kropki,
    /// stoi w elemencie <c>sankcja-karna</c> — jak w przykładzie autorów schematu (Dz. U. 2024 poz. 928, art. 54–58).
    /// </summary>
    internal static class PenalSanctionXml
    {
        // „podlega" i najwyżej dwa wyrazy przed „karze"/„grzywnie" („podlega karze pozbawienia wolności", „podlega
        // administracyjnej karze pieniężnej"); „podlega opłacie", „podlega zwrotowi" i kara dyscyplinarna sankcją nie są.
        private static readonly Regex Clause = new(
            @"\bpodlega\s+(?:\p{L}+\s+){0,2}?(?:karze|grzywnie)\b(?!\s+dyscyplinarn)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Zwrot sankcji zaczyna się za przecinkiem po opisie czynu albo otwiera tekst („– podlega …").
        private static readonly Regex Opening = new($@"(?:^|,)\s*(?:[{Dashes.Any}]\s*)?$", RegexOptions.Compiled);

        private const string CommonPartPrefix = "czesc-wspolna";
        private const string NonZtpCommonPartPrefix = "nie-ztp-czesc-wspolna";

        internal static void Embed(XElement actElement)
        {
            foreach (var part in actElement.Descendants().Where(IsCommonPart).ToList())
                if (Sanction(part.Value) is not null)
                    part.SetAttributeValue(XmlConverterConstants.Contents.Role, XmlConverterConstants.Contents.PenalSanction);

            foreach (var text in InlineTextXml.Texts(actElement))
            {
                var host = InlineTextXml.Host(text);
                if (host.Name.LocalName != XmlConverterConstants.Contents.Sentence.Element || text.Parent != host
                    || text.NextNode is not null || host.Ancestors().Any(IsCommonPart))
                    continue;

                if (Sanction(text.Value) is not { } clause)
                    continue;

                text.ReplaceWith(
                    new XText(text.Value[..clause.Start]),
                    Element(XmlConverterConstants.Contents.PenalSanction, text.Value[clause.Start..clause.End]),
                    new XText(text.Value[clause.End..]));
            }
        }

        /// <summary>Zwrot sankcji domykający tekst: od „podlega" do końca zdania bez kończącej interpunkcji; null — brak.</summary>
        private static (int Start, int End)? Sanction(string text)
        {
            var match = Clause.Match(text);
            if (!match.Success || !Opening.IsMatch(text[..match.Index]))
                return null;

            var end = text.TrimEnd().Length;
            while (end > match.Index && text[end - 1] is '.' or ';' or ',')
                end--;
            return end > match.Index + match.Length - 1 ? (match.Index, end) : null;
        }

        private static bool IsCommonPart(XElement element) =>
            element.Name.LocalName.StartsWith(CommonPartPrefix, StringComparison.Ordinal)
            || element.Name.LocalName.StartsWith(NonZtpCommonPartPrefix, StringComparison.Ordinal);
    }
}
