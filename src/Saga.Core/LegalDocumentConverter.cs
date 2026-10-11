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

            // Tekst jednolity ustawy: korzeniem jest obwieszczenie, a ogłaszany akt stoi w nim jako osobny
            // element z własną strefą tytułową (ADR-0031).
            var announced = AnnouncedStatute(document);
            var actElement = Element(announced is null ? XmlConverterConstants.Act : XmlConverterConstants.Announcements.Element);
            rootElement.Add(actElement);

            PrepareMetadata(document, actElement);
            PrepareHeading(document, actElement, withIssuer: announced is not null);

            if (announced is not null)
                actElement.Add(AnnouncementXmlConverter.Points(announced));

            var body = announced is null ? actElement : AnnouncedActElement(announced);

            foreach (var part in document.Parts)
                body.Add(SystematizingUnitXmlConverter.ActLevel(part));

            // Podpis pod obwieszczeniem, którego aktu eksport nie niesie (tekst jednolity rozporządzenia), nie ma
            // gdzie stanąć: dotychczasowy korzeń nie jest obwieszczeniem.
            if (document.Signature is { } signature && (announced is not null || document.Announcement is null))
                actElement.Add(SignatureXmlConverter.ToXml(signature));

            if (body != actElement)
                actElement.Add(body);

            foreach (var annex in document.Annexes)
                body.Add(AnnexXmlConverter.ToXml(annex));

            FootnoteXmlConverter.Embed(actElement, document);
            InlineTextXml.EmbedSuperscripts(actElement);

            TemporalDataXmlConverter.AssignPeriods(actElement, document);
            ActPrefixXml.Apply(actElement, document);

            if (ContainsMathML(rootElement))
                rootElement.Add(new XAttribute(XNamespace.Xmlns + "mml", XmlConverterConstants.MathMLNamespace));

            return rootElement;
        }

        /// <summary>
        /// Strefa tytułowa: rodzaj aktu, data i przedmiot jak w akcie. Wiersz nierozpoznany nie jest
        /// emitowany — schemat go wymaga, więc brak widać w walidacji, zamiast zgadywać treść.
        /// </summary>
        private static void PrepareHeading(LegalDocument document, XElement actElement, bool withIssuer)
        {
            if (document.KindLine.Length > 0)
                actElement.Add(Element(XmlConverterConstants.ActHeading.Kind, document.KindLine));
            if (withIssuer && document.IssuerLine.Length > 0)
                actElement.Add(Element(XmlConverterConstants.Announcements.Author, document.IssuerLine));
            if (document.DateLine.Length > 0)
                actElement.Add(Element(XmlConverterConstants.ActHeading.Date, document.DateLine));
            if (document.Subject.Length > 0)
                actElement.Add(Element(XmlConverterConstants.ActHeading.Subject, document.Subject));
        }


        /// <summary>
        /// Obwieszczenie, które ogłasza w załączniku ustawę; null dla aktu ogłoszonego wprost i dla obwieszczeń
        /// ogłaszających inne akty (rozporządzenia nie są jeszcze budowane).
        /// </summary>
        private static Announcement? AnnouncedStatute(LegalDocument document) =>
            document.Announcement is { } announcement
            && Classify.Document.ZtpPatterns.StatuteHeaderPattern.IsMatch(FootnoteMarkers.Remove(announcement.ActKindLine))
                ? announcement
                : null;

        private static XElement AnnouncedActElement(Announcement announcement)
        {
            var act = Element(XmlConverterConstants.Act);
            if (announcement.AttachedTo.Length > 0)
                act.Add(Element(XmlConverterConstants.Attachments.AttachedTo, announcement.AttachedTo));
            if (announcement.ActKindLine.Length > 0)
                act.Add(Element(XmlConverterConstants.ActHeading.Kind, announcement.ActKindLine));
            if (announcement.ActDateLine.Length > 0)
                act.Add(Element(XmlConverterConstants.ActHeading.Date, announcement.ActDateLine));
            if (announcement.ActSubject.Length > 0)
                act.Add(Element(XmlConverterConstants.ActHeading.Subject, announcement.ActSubject));
            return act;
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

            metaElement.Add(Element(XmlConverterConstants.Metadata.TitleElement, FootnoteXmlConverter.WithoutMarkers(document.Title)));

            metaElement.Add(Element(XmlConverterConstants.Metadata.TypeElement, ActKindXml.Code(document)));

            // metaElement.Add(Element(XmlConverterConstants.Metadata.PreviousTitlesElement, "xs:string"));

            if (document.AnnouncementDate == null)
                metaElement.Add(Element(XmlConverterConstants.Metadata.AnnouncementDateElement, ""));
            else
                metaElement.Add(Element(XmlConverterConstants.Metadata.AnnouncementDateElement, document.AnnouncementDate.Value.ToString("yyyy-MM-dd")));

            var dateElement = Element(XmlConverterConstants.Metadata.Date.Element);
            if (document.ActDate is { } actDate)
                dateElement.Add(Element(XmlConverterConstants.Metadata.Date.Publication, actDate.ToString("yyyy-MM-dd")));
            metaElement.Add(dateElement);

            if (TemporalDataXmlConverter.ToXml(document) is { } temporal)
                metaElement.Add(temporal);

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
            // Organ wydający wynika z rodzaju aktu (ustawa — Sejm); nieodczytany zostaje nazwany wprost,
            // bez zmyślonego zasobu ELI.
            var issuing = ActKindXml.IssuingAuthority(document);
            if (issuing?.EliUri is { } issuingUri)
                organ.Add(new XAttribute(XmlConverterConstants.Metadata.Authorities.Authority.URI, issuingUri));
            organ.Add(Element(XmlConverterConstants.Metadata.Authorities.Authority.Name, issuing?.Name ?? "UNKNOWN"));
            organy.Add(organ);

            if (document.Signature is { } signature)
                organy.Add(SignatureXmlConverter.ToAuthority(signature));

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
