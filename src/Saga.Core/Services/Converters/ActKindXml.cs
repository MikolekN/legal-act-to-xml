using Saga.Core.Services.Classify.Document;
using Saga.Model;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Rodzaj aktu w metadanych eksportu: kod ze słownika schematu (<c>RodzajAktuTyp</c>) i organ wydający,
    /// który z rodzaju wynika.
    /// </summary>
    internal static class ActKindXml
    {
        private const string AuthorityResource = XmlConverterConstants.ELIUrl + "resource/authority/legal-institution/";

        /// <summary>Organ z nazwą i — gdy znany — zasobem ELI.</summary>
        internal sealed record Authority(string Name, string? EliUri);

        /// <summary>
        /// Kod rodzaju aktu. <see cref="LegalDocument.Type"/> jest deklaracją wywołującego i wygrywa, gdy ją
        /// ustawił; przy wartości domyślnej rozstrzyga nagłówek samego aktu („USTAWA", „ROZPORZĄDZENIE …") —
        /// inaczej każde rozporządzenie i obwieszczenie wychodziłoby jako ustawa.
        /// </summary>
        internal static string Code(LegalDocument document) =>
            document.Type != LegalActType.Statute
                ? CodeOf(document.Type)
                : FromHeading(document) ?? CodeOf(document.Type);

        /// <summary>
        /// Organ wydający wynikający z rodzaju aktu: ustawę uchwala Sejm. Dla pozostałych rodzajów organ
        /// stoi w tytule w dopełniaczu („Ministra Finansów") i nie jest jeszcze odczytywany.
        /// </summary>
        internal static Authority? IssuingAuthority(LegalDocument document) =>
            Code(document) == "USTAWA" ? new Authority("SEJM", AuthorityResource + "Sejm") : null;

        /// <summary>Zasób ELI organu o podanej nazwie; null, gdy nie ma go w słowniku.</summary>
        internal static string? EliUriOf(string authorityName) =>
            authorityName == "Prezydent Rzeczypospolitej Polskiej" ? AuthorityResource + "Prezydent" : null;

        private static string? FromHeading(LegalDocument document)
        {
            var heading = document.KindLine;
            if (heading.Length == 0)
                return null;

            if (ZtpPatterns.StatuteHeaderPattern.IsMatch(heading)) return "USTAWA";
            if (ZtpPatterns.RegulationHeaderPattern.IsMatch(heading)) return "ROZP";
            if (ZtpPatterns.ResolutionHeaderPattern.IsMatch(heading)) return "UCHW";
            if (ZtpPatterns.OrderHeaderPattern.IsMatch(heading)) return "ZARZĄD";
            // Obwieszczenie o tekście jednolitym: mówi to jego tytuł („w sprawie ogłoszenia jednolitego tekstu…"),
            // a gdy strefa tytułowa nie dała przedmiotu — klasyfikacja dokumentu (formuła obwieszczenia).
            if (ZtpPatterns.AnnouncementHeaderPattern.IsMatch(heading))
                return ZtpPatterns.ConsolidatedTextTitlePattern.IsMatch(document.Subject)
                       || document.Classification is { IsConsolidatedText: true }
                    ? "OBW_TJ"
                    : "OBW";

            return null;
        }

        private static string CodeOf(LegalActType type) => type switch
        {
            LegalActType.Statute or LegalActType.AmendingStatute or LegalActType.Code => "USTAWA",
            LegalActType.Bill => "PROJ_U",
            LegalActType.Regulation => "ROZP",
            LegalActType.Announcement => "OBW_TJ",
            LegalActType.Resolution => "UCHW",
            LegalActType.ExecutiveOrder => "ZARZĄD",
            _ => "INNY",
        };
    }
}
