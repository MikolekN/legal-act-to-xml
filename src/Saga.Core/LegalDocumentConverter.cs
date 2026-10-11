using System.Xml.Linq;
using Saga.Core.Services.Classify.Document;
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
            var announcedName = ActKindXml.AnnouncedActElement(document);
            var announced = announcedName is null ? null : document.Announcement;
            // Obwieszczenie, które aktu nie ogłasza (stawki, wskaźniki, wolne stanowiska): korzeniem jest ono samo.
            var plain = document.Announcement is { AnnouncesAct: false } ? document.Announcement : null;
            var announcement = announced ?? plain;
            // Akt ogłoszony wprost ma korzeń według rodzaju; obwieszczenie, którego aktu nie da się nazwać,
            // zostaje przy dotychczasowym korzeniu.
            var direct = document.Announcement is null;
            var actName = direct ? ActKindXml.RootElement(document) : XmlConverterConstants.Act;
            var sectionBased = ActKindXml.IsSectionBased(actName);
            var actElement = Element(announcement is not null ? XmlConverterConstants.Announcements.Element : actName);
            rootElement.Add(actElement);

            PrepareMetadata(document, actElement);
            PrepareHeading(document, actElement, withIssuer: announcement is not null || sectionBased);

            // Podstawa prawna aktu wykonawczego stoi między tytułem a pierwszym paragrafem (§ 121 ZTP);
            // w obwieszczeniu, które aktu nie ogłasza — między tytułem a jego treścią.
            if ((sectionBased || plain is not null) && document.LegalBasis.Length > 0)
                actElement.Add(Element(XmlConverterConstants.SectionBasedActs.LegalBasis, document.LegalBasis));

            if (announcement is not null)
                actElement.Add(AnnouncementXmlConverter.Points(announcement));

            var body = announced is null ? actElement : AnnouncedActElement(announced, announcedName!, document);

            foreach (var part in document.Parts)
                body.Add(SystematizingUnitXmlConverter.ActLevel(part));

            // Podpis pod obwieszczeniem, którego aktu eksport nie niesie (tekst jednolity rozporządzenia), nie ma
            // gdzie stanąć: dotychczasowy korzeń nie jest obwieszczeniem.
            if (document.Signature is { } signature && (announcement is not null || document.Announcement is null))
            {
                actElement.Add(SignatureXmlConverter.ToXml(signature));
                actElement.Add(document.CoSignatures.Select(SignatureXmlConverter.ToXml));
            }

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
                actElement.Add(Element(XmlConverterConstants.ActHeading.Kind,
                    withIssuer ? KindWithoutIssuer(document.KindLine, document.IssuerLine) : document.KindLine));
            if (withIssuer && document.IssuerLine.Length > 0)
                actElement.Add(Element(XmlConverterConstants.Announcements.Author, document.IssuerLine));
            if (document.DateLine.Length > 0)
                actElement.Add(Element(XmlConverterConstants.ActHeading.Date, document.DateLine));
            if (document.Subject.Length > 0)
                actElement.Add(Element(XmlConverterConstants.ActHeading.Subject, document.Subject));
        }


        /// <summary>
        /// Akt ogłaszany w załączniku do obwieszczenia: element według jego rodzaju z własną strefą tytułową.
        /// Akt oparty na paragrafach niesie też organ wydający i podstawę prawną.
        /// </summary>
        private static XElement AnnouncedActElement(Announcement announcement, string name, LegalDocument document)
        {
            var act = Element(name);
            var sectionBased = ActKindXml.IsSectionBased(name);
            if (announcement.AttachedTo.Length > 0)
                act.Add(Element(XmlConverterConstants.Attachments.AttachedTo, announcement.AttachedTo));
            if (announcement.ActKindLine.Length > 0)
                act.Add(Element(XmlConverterConstants.ActHeading.Kind, sectionBased
                    ? KindWithoutIssuer(announcement.ActKindLine, announcement.ActIssuerLine)
                    : announcement.ActKindLine));
            if (sectionBased && announcement.ActIssuerLine.Length > 0)
                act.Add(Element(XmlConverterConstants.Announcements.Author, announcement.ActIssuerLine));
            if (announcement.ActDateLine.Length > 0)
                act.Add(Element(XmlConverterConstants.ActHeading.Date, announcement.ActDateLine));
            if (announcement.ActSubject.Length > 0)
                act.Add(Element(XmlConverterConstants.ActHeading.Subject, announcement.ActSubject));
            if (sectionBased && document.LegalBasis.Length > 0)
                act.Add(Element(XmlConverterConstants.SectionBasedActs.LegalBasis, document.LegalBasis));
            return act;
        }

        /// <summary>
        /// Rodzaj aktu bez organu stojącego w tym samym wierszu („ROZPORZĄDZENIE MINISTRA FINANSÓW" →
        /// „ROZPORZĄDZENIE") — organ wychodzi wtedy osobno, w <c>autor-aktu</c>.
        /// </summary>
        private static string KindWithoutIssuer(string kindLine, string issuerLine) =>
            issuerLine.Length > 0 && ZtpPatterns.KindWithIssuerPattern.Match(kindLine) is { Success: true } sameLine
                ? sameLine.Groups["kind"].Value
                : kindLine;

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

            // Kto akt wydał i kto go podpisał — z rodzaju aktu, strefy tytułowej i formuł podpisu.
            metaElement.Add(AuthoritiesXmlConverter.ToXml(document));

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
