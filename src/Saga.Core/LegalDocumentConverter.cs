using System.Xml.Linq;
using Saga.Core.Services.Converters;
using Saga.Model;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services
{
    /// <summary>
    /// Konwertuje model LegalDocument na XML.
    /// </summary>
    public static class LegalDocumentConverter
    {
        public static XElement Convert(LegalDocument document)
        {
            var rootElement = Element(XmlConverterConstants.RootElement,
                new XAttribute(XNamespace.Xmlns + "now", XmlConverterConstants.AmendmentNamespace),
                new XAttribute(XNamespace.Xmlns + "gd", XmlConverterConstants.GuidNamespace.NamespaceName));

            var actElement = Element(XmlConverterConstants.Act);
            rootElement.Add(actElement);

            PrepareMetadata(document, actElement);
            PrepareHeading(document, actElement);

            foreach (var part in document.Parts)
                actElement.Add(SystematizingUnitXmlConverter.ActLevel(part));

            if (ContainsMathML(rootElement))
                rootElement.Add(new XAttribute(XNamespace.Xmlns + "mml", XmlConverterConstants.MathMLNamespace));

            return rootElement;
        }

        /// <summary>
        /// Strefa tytułowa: rodzaj aktu, data i przedmiot jak w akcie. Wiersz nierozpoznany nie jest
        /// emitowany — schemat go wymaga, więc brak widać w walidacji, zamiast zgadywać treść.
        /// </summary>
        private static void PrepareHeading(LegalDocument document, XElement actElement)
        {
            if (document.KindLine.Length > 0)
                actElement.Add(Element(XmlConverterConstants.ActHeading.Kind, document.KindLine));
            if (document.DateLine.Length > 0)
                actElement.Add(Element(XmlConverterConstants.ActHeading.Date, document.DateLine));
            if (document.Subject.Length > 0)
                actElement.Add(Element(XmlConverterConstants.ActHeading.Subject, document.Subject));
        }

        /// <summary>Kod rodzaju aktu ze słownika schematu (RodzajAktuTyp).</summary>
        private static string KindCode(LegalActType type) => type switch
        {
            LegalActType.Statute or LegalActType.AmendingStatute or LegalActType.Code => "USTAWA",
            LegalActType.Bill => "PROJ_U",
            LegalActType.Regulation => "ROZP",
            LegalActType.Announcement => "OBW_TJ",
            LegalActType.Resolution => "UCHW",
            LegalActType.ExecutiveOrder => "ZARZĄD",
            _ => "INNY",
        };

        private static void PrepareMetadata(LegalDocument document, XElement rootElement)
        {
            var metaElement = Element(XmlConverterConstants.Metadata.Element);

            var eliElement = Element(XmlConverterConstants.Metadata.Eli.Element);
            var eliStrings = document.SourceJournal.GetELIStrings();
            if (!eliStrings.Any())
                eliElement.Add(Element(XmlConverterConstants.Metadata.Eli.ELI, "UNKNOWN/0000/000"));
            else
                foreach (var eliString in eliStrings)
                    eliElement.Add(Element(XmlConverterConstants.Metadata.Eli.ELI, eliString));
            metaElement.Add(eliElement);

            metaElement.Add(new XElement(XmlConverterConstants.Metadata.VersionGUID, Guid.NewGuid().ToString()));

            // metaElement.Add(Element(XmlConverterConstants.Metadata.ISAPAdressElement, "xs:string"));

            metaElement.Add(Element(XmlConverterConstants.Metadata.PublisherElement, document.SourceJournal.PublisherSymbol()));

            metaElement.Add(Element(XmlConverterConstants.Metadata.YearElement, document.SourceJournal.Year == null ? "" : document.SourceJournal.Year));

            // metaElement.Add(Element(XmlConverterConstants.Metadata.VolumeElement, "xs:integer"));

            if (!document.SourceJournal.Positions.Any())
                metaElement.Add(Element(XmlConverterConstants.Metadata.PositionElement, " "));
            else
                foreach (var position in document.SourceJournal.Positions)
                    metaElement.Add(Element(XmlConverterConstants.Metadata.PositionElement, position));

            if (document.SourceJournal.Year != null && document.SourceJournal.Positions.Any())
                metaElement.Add(Element(XmlConverterConstants.Metadata.DisplayAddressElement,
                    $"{document.SourceJournal.PublisherShort()} {document.SourceJournal.Year} poz. {document.SourceJournal.Positions.First()}"));

            metaElement.Add(Element(XmlConverterConstants.Metadata.TitleElement, document.Title));

            metaElement.Add(Element(XmlConverterConstants.Metadata.TypeElement, KindCode(document.Type)));

            // metaElement.Add(Element(XmlConverterConstants.Metadata.PreviousTitlesElement, "xs:string"));

            if (document.AnnouncementDate == null)
                metaElement.Add(Element(XmlConverterConstants.Metadata.AnnouncementDateElement, ""));
            else
                metaElement.Add(Element(XmlConverterConstants.Metadata.AnnouncementDateElement, document.AnnouncementDate.Value.ToString("yyyy-MM-dd")));

            var dateElement = Element(XmlConverterConstants.Metadata.Date.Element);
            if (document.ActDate is { } actDate)
                dateElement.Add(Element(XmlConverterConstants.Metadata.Date.Publication, actDate.ToString("yyyy-MM-dd")));
            metaElement.Add(dateElement);

            if (document.EffectiveDate != default)
                metaElement.Add(Element(XmlConverterConstants.Metadata.EntryIntoForceElement,
                    document.EffectiveDate.ToString("yyyy-MM-dd")));

            if (document.RetroactiveFrom != default)
                metaElement.Add(Element(XmlConverterConstants.Metadata.RetroactiveDatesElement,
                    Element(XmlConverterConstants.Metadata.RetroactiveDateElement,
                        Element(XmlConverterConstants.Metadata.PartialDate.Date,
                            document.RetroactiveFrom.ToString("yyyy-MM-dd")))));

            // metaElement.Add(Element(XmlConverterConstants.Metadata.RepealDateElement, "xs:date"));

            // metaElement.Add(Element(XmlConverterConstants.Metadata.RepealDatePartialElement, "xs:date"));

            // metaElement.Add(Element(XmlConverterConstants.Metadata.ExpirationDateElement, "xs:date"));

            // metaElement.Add(Element(XmlConverterConstants.Metadata.LegalStatusDateElement, "xs:date"));

            // metaElement.Add(Element(XmlConverterConstants.Metadata.ChangeDateElement, "xs:date"));

            // metaElement.Add(Element(XmlConverterConstants.Metadata.StatusElement, "xs:string"));

            // metaElement.Add(Element(XmlConverterConstants.Metadata.InForceElement, "xs:string"));

            var organy = Element(XmlConverterConstants.Metadata.Authorities.Element);
            var organ = Element(XmlConverterConstants.Metadata.Authorities.Authority.Element);
            organ.Add(new XAttribute(XmlConverterConstants.Metadata.Authorities.Authority.GUID, Guid.NewGuid().ToString()));
            organ.Add(new XAttribute(XmlConverterConstants.Metadata.Authorities.Authority.Role, XmlConverterConstants.Metadata.Authorities.Authority.RoleIssuing));
            organ.Add(new XAttribute(XmlConverterConstants.Metadata.Authorities.Authority.URI, "URI"));
            var nazwa = Element(XmlConverterConstants.Metadata.Authorities.Authority.Name, "UNKNOWN");
            organ.Add(nazwa);
            organy.Add(organ);

            metaElement.Add(organy);

            // metaElement.Add(Element(XmlConverterConstants.Metadata.KeywordsElement, "COMPLEX"));

            // metaElement.Add(Element(XmlConverterConstants.Metadata.ProperNamesElement, "COMPLEX"));

            // metaElement.Add(Element(XmlConverterConstants.Metadata.RelationsElement, "COMPLEX"));

            // metaElement.Add(Element(XmlConverterConstants.Metadata.DirectivesElement, "COMPLEX"));

            // metaElement.Add(Element(XmlConverterConstants.Metadata.TextsElement, "COMPLEX"));

            // metaElement.Add(Element(XmlConverterConstants.Metadata.PrintsElement, "COMPLEX"));

            // metaElement.Add(Element(XmlConverterConstants.Metadata.CommentsElement, "COMPLEX"));

            rootElement.Add(metaElement);
        }

        // private static void PrepareActType(LegalDocument document, XElement rootElement)
        // {
        //     var actTypeElement = Element(XmlConverterConstants.ActType.Element, document.TypeElement);
        //     rootElement.Add(actTypeElement);
        // }

        // private static void PrepareDate(LegalDocument document, XElement rootElement)
        // {
        //     var dateElement = Element(XmlConverterConstants.Date.Element, document.DateElement);
        //     rootElement.Add(dateElement);
        // }

        // private static void PrepareTitle(LegalDocument document, XElement rootElement)
        // {
        //     var titleElement = Element(XmlConverterConstants.Title.Element,
        //         FootnoteXmlConverter.BuildTextContent(document.TitleElement));
        //     rootElement.Add(titleElement);
        // }
    }
}
