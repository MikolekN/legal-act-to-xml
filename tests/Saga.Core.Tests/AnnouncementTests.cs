using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Core.Services.Parsing;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Obwieszczenie w sprawie ogłoszenia tekstu jednolitego: treść sprzed wiersza „Załącznik do obwieszczenia …"
    /// należy do obwieszczenia, a nie do aktu, który ono ogłasza.
    /// </summary>
    public class AnnouncementTests
    {
        private static DocumentBlock Block(string text, string? style = null) => new() { Text = text, StyleId = style };

        // Układ szczotki RCL (336586_2987956): nagłówek, punkty obwieszczenia z podpunktami, przytoczony przepis,
        // podpis, akt.
        private static readonly string[] ConsolidatedText =
        [
            "Obwieszczenie",
            "Marszałka Sejmu Rzeczypospolitej Polskiej",
            "z dnia 6 sierpnia 2025 r.",
            "w sprawie ogłoszenia jednolitego tekstu ustawy o odbudowie obiektów budowlanych",
            "1. Na podstawie art. 16 ust. 1 ustawy z dnia 20 lipca 2000 r. o ogłaszaniu aktów normatywnych ogłasza się w załączniku do niniejszego obwieszczenia jednolity tekst ustawy z dnia 11 sierpnia 2001 r. o odbudowie obiektów budowlanych, z uwzględnieniem zmian wprowadzonych:",
            "1) ustawą z dnia 1 października 2024 r. o zmianie ustawy o odbudowie (Dz. U. poz. 1473),",
            "2) ustawą z dnia 9 maja 2025 r. o zmianie ustawy o odbudowie (Dz. U. poz. 680)",
            "oraz zmian wynikających z przepisów ogłoszonych przed dniem 6 sierpnia 2025 r.",
            "2. Podany w załączniku do niniejszego obwieszczenia tekst jednolity ustawy nie obejmuje art. 57 ustawy z dnia 1 października 2024 r. o zmianie ustawy o odbudowie (Dz. U. poz. 1473), który stanowi:",
            "„Art. 57. Przepisy ustawy zmienianej w art. 10 stosuje się również do odbudowy.”",
            "Marszałek Sejmu: S. Hołownia",
            "Załącznik do obwieszczenia Marszałka Sejmu Rzeczypospolitej Polskiej z dnia 6 sierpnia 2025 r. (Dz. U. poz. 1094)",
            "USTAWA",
            "z dnia 11 sierpnia 2001 r.",
            "o odbudowie obiektów budowlanych",
            "Art. 1. Ustawa reguluje zasady odbudowy.",
            "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
        ];

        private const int ActStart = 11;
        private const int ActSubjectLine = 14;

        private static LegalDocument Parse(IEnumerable<string> lines) =>
            LegalDocumentParser.Parse(
                lines.Select(line => Block(line)).ToList(),
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

        // ============================================================
        // Granica obwieszczenia i ogłaszanego aktu
        // ============================================================

        [Fact]
        public void ContentBeforeTheAnnexedAct_BelongsToTheAnnouncement()
        {
            var document = Parse(ConsolidatedText);

            // Przytoczony „Art. 57." nie jest artykułem ogłaszanego aktu.
            Assert.Equal(new[] { "1", "2" }, document.Articles.Select(a => a.Number?.Value));
            Assert.NotNull(document.Announcement);
        }

        [Fact]
        public void AnnouncementHeading_StaysTheHeadingOfTheDocument()
        {
            var document = Parse(ConsolidatedText);

            Assert.Equal("Obwieszczenie", document.KindLine);
            Assert.Equal("Marszałka Sejmu Rzeczypospolitej Polskiej", document.IssuerLine);
            Assert.Equal("z dnia 6 sierpnia 2025 r.", document.DateLine);
            Assert.Equal(new DateTime(2025, 8, 6), document.ActDate);
            Assert.StartsWith("w sprawie ogłoszenia jednolitego tekstu", document.Subject);
        }

        [Fact]
        public void ActAnnouncedDirectly_HasNoAnnouncement()
        {
            var document = Parse(ConsolidatedText.Skip(ActStart + 1));

            Assert.Null(document.Announcement);
            Assert.Equal(2, document.Articles.Count());
        }

        [Fact]
        public void AnnouncementWithoutAnAnnexedAct_IsLeftToTheActPipeline()
        {
            // Obwieszczenie innego rodzaju (bez „Załącznik do obwieszczenia") nie ma granicy — nic się nie zmienia.
            Assert.Equal(-1, AnnouncementBoundary.Find(ConsolidatedText.Take(ActStart).Select(line => Block(line)).ToList()));
            Assert.Equal(ActStart, AnnouncementBoundary.Find(ConsolidatedText.Select(line => Block(line)).ToList()));
        }

        // ============================================================
        // Jednostki obwieszczenia
        // ============================================================

        [Fact]
        public void AnnouncementPoints_HoldSubPointsAndTheirCommonPart()
        {
            var announcement = Parse(ConsolidatedText).Announcement!;

            Assert.Empty(announcement.Content);
            Assert.Equal(new[] { "pkt_1", "pkt_2" }, announcement.Points.Select(p => p.Id));

            var first = announcement.Points[0];
            Assert.StartsWith("Na podstawie art. 16", first.ContentText);
            Assert.Equal(new[] { "pkt_1__ppkt_1", "pkt_1__ppkt_2" }, first.Points.Select(p => p.Id));
            Assert.StartsWith("ustawą z dnia 1 października 2024 r.", first.Points[0].ContentText);

            var commonPart = Assert.Single(first.CommonParts, part => part.Type == CommonPartType.WrapUp);
            Assert.StartsWith("oraz zmian wynikających", commonPart.ContentText);
        }

        [Fact]
        public void QuotedProvision_ContinuesThePointItStandsAfter()
        {
            var second = Parse(ConsolidatedText).Announcement!.Points[1];

            Assert.Empty(second.Points);
            Assert.StartsWith("„Art. 57.", Assert.Single(second.Continuation).Text);
        }

        [Fact]
        public void QuotedProvisionOfSeveralParagraphs_StaysOneQuotation()
        {
            // Przytoczenie otwarte cudzysłowem ciągnie się do jego domknięcia — „1)" w środku nie jest podpunktem.
            var lines = ConsolidatedText.ToList();
            lines[9] = "„Art. 57. Przepisy stosuje się do:";
            lines.InsertRange(10, ["1) odbudowy,", "2) remontu.”"]);

            var second = Parse(lines).Announcement!.Points[1];

            Assert.Empty(second.Points);
            Assert.Equal(new[] { "„Art. 57. Przepisy stosuje się do:", "1) odbudowy,", "2) remontu.”" }, second.Continuation.Select(c => c.Text));
        }

        [Fact]
        public void SignatureUnderTheAnnouncement_IsTheSignatureOfTheDocument()
        {
            var signature = Parse(ConsolidatedText).Signature!;

            Assert.Equal("Marszałek Sejmu", signature.Authority);
            Assert.Equal("Hołownia", signature.Surname);
        }

        // ============================================================
        // Korzeń „obwieszczenie" z aktem osadzonym (ADR-0031)
        // ============================================================

        [Fact]
        public void AnnouncedAct_KeepsItsOwnHeading()
        {
            var announcement = Parse(ConsolidatedText).Announcement!;

            Assert.StartsWith("Załącznik do obwieszczenia Marszałka Sejmu", announcement.AttachedTo);
            Assert.Equal("USTAWA", announcement.ActKindLine);
            Assert.Equal("z dnia 11 sierpnia 2001 r.", announcement.ActDateLine);
            Assert.Equal("o odbudowie obiektów budowlanych", announcement.ActSubject);
        }

        private static string[] ChildNames(XElement element) =>
            element.Elements().Select(child => child.Name.LocalName).ToArray();

        private static XElement Child(XElement element, string name) =>
            element.Elements().First(child => child.Name.LocalName == name);

        [Fact]
        public void ConsolidatedStatute_IsExportedUnderTheAnnouncement()
        {
            var xml = LegalDocumentConverter.Convert(Parse(ConsolidatedText));

            var announcement = Assert.Single(xml.Elements());
            Assert.Equal("obwieszczenie", announcement.Name.LocalName);
            Assert.Equal(
                new[] { "metadane", "rodzaj-aktu", "autor-aktu", "data", "tytul", "punkt-OTJ", "punkt-OTJ", "organ-podpisujacy", "ustawa" },
                ChildNames(announcement));
            Assert.Equal("Obwieszczenie", Child(announcement, "rodzaj-aktu").Value);
            Assert.Equal("Marszałka Sejmu Rzeczypospolitej Polskiej", Child(announcement, "autor-aktu").Value);

            // Akt ogłaszany w załączniku ma własną strefę tytułową i tylko własne artykuły.
            var act = Child(announcement, "ustawa");
            Assert.Equal(new[] { "zalacznik-do", "rodzaj-aktu", "data", "tytul", "artykul", "artykul" }, ChildNames(act));
            Assert.Equal("USTAWA", Child(act, "rodzaj-aktu").Value);
            Assert.Equal("o odbudowie obiektów budowlanych", Child(act, "tytul").Value);
        }

        [Fact]
        public void AnnouncementPoints_AreExportedWithAddressesOfTheAnnouncement()
        {
            var announcement = LegalDocumentConverter.Convert(Parse(ConsolidatedText)).Elements().Single();

            var first = Child(announcement, "punkt-OTJ");
            Assert.Equal(new[] { "nr", "p", "podpunkt-OTJ", "podpunkt-OTJ", "czesc-wspolna-podpunkt-OTJ" }, ChildNames(first));
            Assert.Equal("pkt_1", first.Attribute("eId")!.Value);
            Assert.Equal(
                new[] { "pkt_1__ppkt_1", "pkt_1__ppkt_2", "pkt_1__czw" },
                first.Elements().Skip(2).Select(e => e.Attribute("eId")!.Value));

            // Przytoczony przepis stoi za zdaniami punktu jako jego dalsza treść.
            var second = announcement.Elements().Where(e => e.Name.LocalName == "punkt-OTJ").ElementAt(1);
            Assert.Equal(new[] { "nr", "p", "fragment" }, ChildNames(second));

            // Jednostki obwieszczenia nie są przepisami aktu — nie niosą dat.
            Assert.DoesNotContain(
                announcement.Elements().Where(e => e.Name.LocalName == "punkt-OTJ").DescendantsAndSelf(),
                e => e.Attribute("wejscie-w-zycie") != null || e.Attribute("okres") != null);
        }

        [Fact]
        public void ActAnnouncedDirectly_KeepsTheActAsTheRoot()
        {
            var xml = LegalDocumentConverter.Convert(Parse(ConsolidatedText.Skip(ActStart + 1)));

            Assert.Equal("ustawa", Assert.Single(xml.Elements()).Name.LocalName);
        }

        [Fact]
        public void AnnouncementWithAnAnnexThatIsNotAnAct_KeepsTheLegacyRoot()
        {
            // Załącznik do obwieszczenia bywa wykazem, nie aktem: nie ma rodzaju, którym dałoby się nazwać
            // osadzony element, więc eksport nie udaje, że go zbudował.
            var lines = ConsolidatedText.Take(ActStart + 1).Concat(
            [
                "WYKAZ ZAREJESTROWANYCH KANCELARII",
                "(według stanu na dzień 31 grudnia 2024 r.)",
            ]);

            var xml = LegalDocumentConverter.Convert(Parse(lines));

            // Treść obwieszczenia i jego podpis są w modelu; eksport ich nie niesie, dopóki korzeń nie jest obwieszczeniem.
            var root = Assert.Single(xml.Elements());
            Assert.Equal("ustawa", root.Name.LocalName);
            Assert.Equal(new[] { "metadane", "rodzaj-aktu", "data", "tytul" }, ChildNames(root));
        }

        [SchemaPlFact]
        public void ConsolidatedStatute_IsSchemaValid_InItsBody()
        {
            var xml = LegalDocumentConverter.Convert(Parse(ConsolidatedText));

            Assert.Empty(SchemaPl.Validator.ValidateActBody(xml.Elements().First()));
        }

        // ============================================================
        // Dane czasowe tekstu jednolitego (ADR-0033)
        // ============================================================

        private static XElement ExportWithJournalHeader()
        {
            var blocks = new List<DocumentBlock>
            {
                new() { Text = "DZIENNIK USTAW RZECZYPOSPOLITEJ POLSKIEJ", Role = BlockRole.JournalHeader },
                new() { Text = "Warszawa, dnia 11 sierpnia 2025 r.", Role = BlockRole.JournalHeader },
                new() { Text = "Poz. 1094", Role = BlockRole.JournalHeader },
            };
            blocks.AddRange(ConsolidatedText.Select(line => Block(line)));
            var document = LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
            return LegalDocumentConverter.Convert(document);
        }

        [Fact]
        public void ConsolidatedText_HasOneConsolidationEvent_AndNoUnitDates()
        {
            var xml = ExportWithJournalHeader();
            const string eventId = "zdarz_ujednolicenie_2025-08-11_DU-2025-1094_domyslne";

            // „Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia" w art. 2 mówi o ogłoszeniu ustawy,
            // nie obwieszczenia — z daty dokumentu nie wolno wyliczać wejścia w życie.
            var lifecycleEvent = Assert.Single(xml.Descendants(), e => e.Name.LocalName == "Zdarzenie");
            Assert.Equal(eventId, lifecycleEvent.Attribute("id")!.Value);
            Assert.Equal("ujednolicenie", lifecycleEvent.Attribute("rodzaj")!.Value);
            Assert.Equal("2025-08-11", lifecycleEvent.Attribute("data")!.Value);

            var group = Assert.Single(xml.Descendants(), e => e.Name.LocalName == "GrupaCzasowa");
            Assert.Equal($"okr_ujednolicenie.{eventId}", group.Attribute("id")!.Value);
            Assert.Equal("true", group.Attribute("glowna")!.Value);
            var interval = Assert.Single(group.Elements());
            Assert.Equal("ujednolicenie", interval.Attribute("rodzaj")!.Value);
            Assert.Equal(eventId, interval.Attribute("od")!.Value);

            Assert.DoesNotContain(xml.Descendants(), e => e.Attribute("wejscie-w-zycie") != null || e.Attribute("okres") != null);
        }

        [SchemaPlFact]
        public void ConsolidatedStatute_IsSchemaValid_AsAWhole()
        {
            Assert.Empty(SchemaPl.Validator.Validate(ExportWithJournalHeader()));
        }

        [Theory]
        [InlineData("o Policji")]
        [InlineData("o Państwowej Straży Pożarnej[1)]")]
        [InlineData("Kodeks cywilny[1)]")]
        [InlineData("Prawo restrukturyzacyjne")]
        public void AnnouncedActSubject_MayStartWithAProperName(string subject)
        {
            var lines = ConsolidatedText.ToArray();
            lines[ActSubjectLine] = subject;

            Assert.Equal(subject, Parse(lines).Announcement!.ActSubject);
        }
    }
}
