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
        /// ustawił; przy wartości domyślnej rozstrzyga nagłówek samego aktu („USTAWA", „ROZPORZĄDZENIE …"),
        /// a gdy nagłówek nie ma rozpoznawalnej postaci („ROZPORZĄDZENIE MINISTRA zDROWIA") — rodzaj
        /// rozpoznany przez klasyfikator dokumentu. Inaczej każde rozporządzenie i obwieszczenie wychodziłoby
        /// jako ustawa.
        /// </summary>
        internal static string Code(LegalDocument document) =>
            document.Type != LegalActType.Statute
                ? CodeOf(document.Type)
                : FromHeading(document) ?? FromClassification(document) ?? CodeOf(document.Type);

        /// <summary>
        /// Element aktu w eksporcie: akt oparty na paragrafach ma korzeń według rodzaju (wszystkie typu
        /// rozporządzenia w schemacie), pozostałe — <c>ustawa</c>. Rozstrzyga treść: korzeń aktu opartego na
        /// paragrafach wymaga jednostek „§", więc bez nich zostaje dotychczasowy.
        /// </summary>
        internal static string RootElement(LegalDocument document)
        {
            if (!document.Articles.Any(article => article.IsSection))
                return StatuteElement(document.Subject, document);

            return Code(document) switch
            {
                "UCHW" => XmlConverterConstants.SectionBasedActs.Resolution,
                "ZARZĄD" => XmlConverterConstants.SectionBasedActs.Order,
                _ => XmlConverterConstants.SectionBasedActs.Regulation,
            };
        }

        /// <summary>
        /// Ustawa o tytule „Kodeks …" jest kodeksem; ustawa o zmianie kodeksu — zwykłą ustawą. Element kodeksu
        /// dostaje akt, którego artykuły stoją w jednostkach systematyzacyjnych (każdy kodeks je ma): artykuł
        /// wprost w kodeksie ma w schemacie inną budowę niż ta, którą eksport pisze dla ustawy.
        /// </summary>
        private static string StatuteElement(string subject, LegalDocument document) =>
            subject.StartsWith("Kodeks ", StringComparison.Ordinal) && document.Articles.All(InSystematizingUnit)
                ? XmlConverterConstants.Code
                : XmlConverterConstants.Act;

        private static bool InSystematizingUnit(BaseEntity unit)
        {
            for (var parent = unit.Parent; parent is not null; parent = parent.Parent)
            {
                if (parent is Saga.Model.SystematizingUnits.ISystematizingUnit { IsImplicit: false })
                    return true;
            }

            return false;
        }

        /// <summary>Czy element aktu jest korzeniem aktu opartego na paragrafach (z organem i podstawą prawną).</summary>
        internal static bool IsSectionBased(string actElement) =>
            actElement is XmlConverterConstants.SectionBasedActs.Regulation
                or XmlConverterConstants.SectionBasedActs.Resolution
                or XmlConverterConstants.SectionBasedActs.Order;

        /// <summary>
        /// Element aktu ogłaszanego w załączniku do obwieszczenia — z jego własnego nagłówka i jednostek;
        /// null, gdy rodzaju nie da się nazwać albo akt oparty na paragrafach nie ma żadnej jednostki „§".
        /// </summary>
        internal static string? AnnouncedActElement(LegalDocument document)
        {
            var heading = FootnoteMarkers.Remove(document.Announcement?.ActKindLine ?? string.Empty);
            if (ZtpPatterns.StatuteHeaderPattern.IsMatch(heading))
                return StatuteElement(document.Announcement!.ActSubject, document);

            if (!document.Articles.Any(article => article.IsSection))
                return null;

            if (ZtpPatterns.RegulationHeaderPattern.IsMatch(heading)) return XmlConverterConstants.SectionBasedActs.Regulation;
            if (ZtpPatterns.ResolutionHeaderPattern.IsMatch(heading)) return XmlConverterConstants.SectionBasedActs.Resolution;
            if (ZtpPatterns.OrderHeaderPattern.IsMatch(heading)) return XmlConverterConstants.SectionBasedActs.Order;
            return null;
        }

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
            var heading = FootnoteMarkers.Remove(document.KindLine);
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

        // Klasyfikator czyta cały dokument (formuły, jednostki, styl), więc zna rodzaj także bez nagłówka.
        private static string? FromClassification(LegalDocument document) => document.Classification switch
        {
            { ActType: LegalActType.Announcement, IsConsolidatedText: false } => "OBW",
            { ActType: { } type } => CodeOf(type),
            _ => null,
        };

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
