using System.Text.RegularExpressions;
using System.Xml.Linq;
using Saga.Model;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Prefiks aktu w adresach eksportu (ADR-0027): adres w akcie („art_5__ust_2") staje się adresem
    /// w zbiorze aktów („DU/2024/928__art_5__ust_2"). Jednostki aktu niosą jego własny prefiks, jednostki
    /// cytowane w nowelizacji i cele komend — prefiks aktu zmienianego, a jednostki przytoczone
    /// w obwieszczeniu (<c>tresc-strukt</c>) — prefiks aktu, z którego pochodzą.
    /// </summary>
    internal static class ActPrefixXml
    {
        /// <summary>
        /// Akt, z którego pochodzi przytoczenie (ELI bez adresu zasobu, np. „DU/2025/1792"); null, gdy nieodczytany.
        /// Adnotacja elementu <c>tresc-strukt</c> — schemat nie przewiduje dla niego atrybutu aktu.
        /// </summary>
        internal sealed record QuotedAct(string? Eli);

        private const string Separator = EIdSegment.Separator;

        // Identyfikator ELI aktu na końcu adresu zasobu: „https://eli.gov.pl/eli/DU/2025/350" → „DU/2025/350".
        private static readonly Regex EliOfAct = new(@"/eli/(?<eli>[A-Z]{2}/\d{4}/\d+)$", RegexOptions.Compiled);

        private static readonly string[] TargetAttributes =
        [
            XmlConverterConstants.Contents.Citation.Quotation.ElementAttribute,
            XmlConverterConstants.Contents.Citation.Quotation.ElementDoAttribute,
        ];

        /// <summary>
        /// Przebieg po gotowym dokumencie. Akt bez znanego publikatora (projekt, tekst bez nagłówka) zostaje
        /// przy adresach bez prefiksu; tak samo cytat, którego aktu zmienianego nie odczytano.
        /// </summary>
        internal static void Apply(XElement actElement, LegalDocument document)
        {
            var own = document.SourceJournal.GetELIStrings().FirstOrDefault();
            var metadata = actElement.Elements().FirstOrDefault(e => e.Name.LocalName == XmlConverterConstants.Metadata.Element);

            // Adres w akcie zmienianym (jednostki cytowanej albo celu komendy) → prefiks tego aktu; potrzebny
            // metadanym, które wskazują taką jednostkę bez cytatu.
            var amendedUnits = new Dictionary<string, string?>();

            foreach (var element in actElement.Descendants().Where(e => e.AncestorsAndSelf().All(a => a != metadata)).ToList())
            {
                var amended = AmendedActOf(element);

                foreach (var name in TargetAttributes)
                    if (element.Attribute(name) is { } target && amended is not null && IsAddress(target.Value))
                    {
                        Remember(amendedUnits, target.Value, amended);
                        target.Value = amended + Separator + target.Value;
                    }

                if (element.Attribute(XmlConverterConstants.Contents.eId) is not { } eId)
                    continue;

                var citation = element.Ancestors().FirstOrDefault(IsStructuralCitation);
                if (citation is null)
                {
                    if (own is not null)
                        eId.Value = own + Separator + eId.Value;
                    continue;
                }

                var prefix = AmendedActOf(citation);
                Remember(amendedUnits, eId.Value, prefix);
                if (prefix is not null)
                    eId.Value = prefix + Separator + eId.Value;
            }

            if (metadata is not null)
                PrefixMetadata(metadata, own, amendedUnits);
        }

        // Ten sam adres w dwóch różnych aktach zmienianych nie rozstrzyga, o który chodzi.
        private static void Remember(Dictionary<string, string?> amendedUnits, string address, string? prefix) =>
            amendedUnits[address] = amendedUnits.TryGetValue(address, out var known) && known != prefix ? null : prefix;

        // Zdarzenia cyklu życia wskazują jednostki aktu (własny prefiks) i jednostki cytowane w zakresie wyjątku.
        private static void PrefixMetadata(XElement metadata, string? own, Dictionary<string, string?> amendedUnits)
        {
            foreach (var element in metadata.Descendants())
            {
                switch (element.Name.LocalName)
                {
                    case XmlConverterConstants.Metadata.Temporal.Unit when own is not null:
                        Prefix(element.Attribute(XmlConverterConstants.Contents.eId), own);
                        break;

                    case XmlConverterConstants.Metadata.Temporal.UnitInScope:
                        if (element.Attribute(XmlConverterConstants.Contents.eId) is { } scoped
                            && amendedUnits.GetValueOrDefault(scoped.Value) is { } amended)
                            Prefix(scoped, amended);
                        break;
                }

                if (own is not null)
                    Prefix(element.Attribute(XmlConverterConstants.Metadata.Temporal.Evidence), own);
            }
        }

        private static void Prefix(XAttribute? attribute, string prefix)
        {
            if (attribute is not null && IsAddress(attribute.Value))
                attribute.Value = prefix + Separator + attribute.Value;
        }

        /// <summary>
        /// Akt zmieniany wskazany przez cytat albo instrukcję (atrybut <c>akt</c>), a dla przytoczenia — akt,
        /// z którego pochodzi (<see cref="QuotedAct"/>); null, gdy nieodczytany.
        /// </summary>
        private static string? AmendedActOf(XElement element)
        {
            if (element.Annotation<QuotedAct>() is { } quoted)
                return quoted.Eli;

            var act = element.Attributes().FirstOrDefault(a =>
                a.Name.LocalName == XmlConverterConstants.Contents.Citation.Quotation.ActAttribute && !a.IsNamespaceDeclaration);
            return act is null ? null : EliOfAct.Match(act.Value) is { Success: true } match ? match.Groups["eli"].Value : null;
        }

        private static bool IsStructuralCitation(XElement element) =>
            element.Name.LocalName is XmlConverterConstants.Contents.Citation.StructElement
                or XmlConverterConstants.Contents.Citation.QuotedStructElement;

        // „UNKNOWN" i pusta wartość nie są adresami — nie ma czego poprzedzać.
        private static bool IsAddress(string value) => value.Length > 0 && value.Contains('_');
    }
}
