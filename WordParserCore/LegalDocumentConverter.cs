using System.Xml.Linq;
using ModelDto;
using WordParserCore.Services.Converters;
using static WordParserCore.XLinqWrappers;

namespace WordParserCore
{
    /// <summary>
    /// Konwertuje model LegalDocument na XML.
    /// </summary>
    public static class LegalDocumentConverter
    {
        public static XElement Convert(LegalDocument document)
        {
            var rootElement = Element(XmlConverterConstants.RootElement, new XAttribute(XNamespace.Xmlns + "now", XmlConverterConstants.AmendmentNamespace));

            var actElement = Element(XmlConverterConstants.Act);
            rootElement.Add(actElement);

            PrepareMetadata(document, actElement);
            PrepareActType(document, actElement);
            PrepareDate(document, actElement);
            PrepareTitle(document, actElement);

            foreach (var article in document.Articles)
                actElement.Add(ArticleXmlConverter.ToXml(article));

            return rootElement;
        }

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

            metaElement.Add(Element(XmlConverterConstants.Metadata.VersionGUID, Guid.NewGuid().ToString()));

            // metaElement.Add(Element(XmlConverterConstants.Metadata.ISAPAdressElement, "xs:string"));

            metaElement.Add(Element(XmlConverterConstants.Metadata.PublisherElement, document.SourceJournal.PublisherSymbol()));

            metaElement.Add(Element(XmlConverterConstants.Metadata.YearElement, document.SourceJournal.Year == null ? "" : document.SourceJournal.Year));

            // metaElement.Add(Element(XmlConverterConstants.Metadata.VolumeElement, "xs:integer"));

            if (!document.SourceJournal.Positions.Any())
                metaElement.Add(Element(XmlConverterConstants.Metadata.PositionElement, " "));
            else
                foreach (var position in document.SourceJournal.Positions)
                    metaElement.Add(Element(XmlConverterConstants.Metadata.PositionElement, position));

            // if (document.SourceJournal.SourceString == String.Empty)
            //     metaElement.Add(Element(XmlConverterConstants.Metadata.DisplayAddressElement, ""));
            // else
            //     metaElement.Add(Element(XmlConverterConstants.Metadata.DisplayAddressElement, document.SourceJournal.SourceString));

            metaElement.Add(Element(XmlConverterConstants.Metadata.TitleElement, document.Title));

            metaElement.Add(Element(XmlConverterConstants.Metadata.TypeElement, document.Type.ToFriendlyString().ToUpper()));

            // metaElement.Add(Element(XmlConverterConstants.Metadata.PreviousTitlesElement, "xs:string"));

            if (document.AnnouncementDate == null)
                metaElement.Add(Element(XmlConverterConstants.Metadata.AnnouncementDateElement, ""));
            else
                metaElement.Add(Element(XmlConverterConstants.Metadata.AnnouncementDateElement, document.AnnouncementDate.Value.ToString("yyyy-MM-dd")));

            var dateElement = Element(XmlConverterConstants.Metadata.Date.Element);

            // dateElement.Add(Element(XmlConverterConstants.Metadata.Date.Project, ""));
            if (document.PublicationDate == null)
                dateElement.Add(Element(XmlConverterConstants.Metadata.Date.Publication, ""));
            else
                dateElement.Add(Element(XmlConverterConstants.Metadata.Date.Publication, document.PublicationDate.Value.ToString("yyyy-MM-dd")));
            metaElement.Add(dateElement);

            // metaElement.Add(Element(XmlConverterConstants.Metadata.EntryIntoForceElement, "xs:date"));

            // metaElement.Add(Element(XmlConverterConstants.Metadata.EntryIntoForcePartialElement, "xs:date"));

            // metaElement.Add(Element(XmlConverterConstants.Metadata.ValidFromElement, "xs:date"));

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
            organ.Add(new XAttribute(XmlConverterConstants.Metadata.Authorities.Authority.Role, "wydający"));
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

        private static void PrepareActType(LegalDocument document, XElement rootElement)
        {
            var actTypeElement = Element(XmlConverterConstants.ActType.Element, document.TypeElement);
            rootElement.Add(actTypeElement);
        }

        private static void PrepareDate(LegalDocument document, XElement rootElement)
        {
            var dateElement = Element(XmlConverterConstants.Date.Element, document.DateElement);
            rootElement.Add(dateElement);
        }

        private static void PrepareTitle(LegalDocument document, XElement rootElement)
        {
            var titleElement = Element(XmlConverterConstants.Title.Element, document.TitleElement);
            rootElement.Add(titleElement);
        }
    }
}
