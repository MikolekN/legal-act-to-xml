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

        // Układ szczotki RCL (336586_2987956): nagłówek, punkty obwieszczenia, przytoczony przepis, podpis, akt.
        private static readonly string[] ConsolidatedText =
        [
            "Obwieszczenie",
            "Marszałka Sejmu Rzeczypospolitej Polskiej",
            "z dnia 6 sierpnia 2025 r.",
            "w sprawie ogłoszenia jednolitego tekstu ustawy o odbudowie obiektów budowlanych",
            "1. Na podstawie art. 16 ust. 1 ustawy z dnia 20 lipca 2000 r. o ogłaszaniu aktów normatywnych ogłasza się w załączniku do niniejszego obwieszczenia jednolity tekst ustawy z dnia 11 sierpnia 2001 r. o odbudowie obiektów budowlanych.",
            "2. Podany w załączniku do niniejszego obwieszczenia tekst jednolity ustawy nie obejmuje art. 57 ustawy z dnia 1 października 2024 r. o zmianie ustawy (Dz. U. poz. 1473), który stanowi:",
            "„Art. 57. Przepisy ustawy zmienianej w art. 10 stosuje się również do odbudowy.”",
            "Marszałek Sejmu: S. Hołownia",
            "Załącznik do obwieszczenia Marszałka Sejmu Rzeczypospolitej Polskiej z dnia 6 sierpnia 2025 r. (Dz. U. poz. 1094)",
            "USTAWA",
            "z dnia 11 sierpnia 2001 r.",
            "o odbudowie obiektów budowlanych",
            "Art. 1. Ustawa reguluje zasady odbudowy.",
            "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
        ];

        private static LegalDocument Parse(IEnumerable<string> lines) =>
            LegalDocumentParser.Parse(
                lines.Select(line => Block(line)).ToList(),
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

        [Fact]
        public void ContentBeforeTheAnnexedAct_BelongsToTheAnnouncement()
        {
            var document = Parse(ConsolidatedText);

            // Przytoczony „Art. 57." nie jest artykułem ogłaszanego aktu.
            Assert.Equal(new[] { "1", "2" }, document.Articles.Select(a => a.Number?.Value));

            var announcement = Assert.IsType<Announcement>(document.Announcement);
            Assert.Equal(4, announcement.Content.Count);
            Assert.StartsWith("1. Na podstawie art. 16", announcement.Content[0].Text);
            Assert.StartsWith("„Art. 57.", announcement.Content[2].Text);
            Assert.Equal("Marszałek Sejmu: S. Hołownia", announcement.Content[3].Text);
        }

        [Fact]
        public void AnnouncementHeading_StaysTheHeadingOfTheDocument()
        {
            var document = Parse(ConsolidatedText);

            Assert.Equal("Obwieszczenie", document.KindLine);
            Assert.Equal("z dnia 6 sierpnia 2025 r.", document.DateLine);
            Assert.Equal(new DateTime(2025, 8, 6), document.ActDate);
            Assert.StartsWith("w sprawie ogłoszenia jednolitego tekstu", document.Subject);
        }

        [Fact]
        public void ActAnnouncedDirectly_HasNoAnnouncement()
        {
            var document = Parse(ConsolidatedText.Skip(9));

            Assert.Null(document.Announcement);
            Assert.Equal(2, document.Articles.Count());
        }

        [Fact]
        public void AnnouncementWithoutAnAnnexedAct_IsLeftToTheActPipeline()
        {
            // Obwieszczenie innego rodzaju (bez „Załącznik do obwieszczenia") nie ma granicy — nic się nie zmienia.
            Assert.Equal(-1, AnnouncementBoundary.Find(ConsolidatedText.Take(8).Select(line => Block(line)).ToList()));
            Assert.Equal(8, AnnouncementBoundary.Find(ConsolidatedText.Select(line => Block(line)).ToList()));
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

        [Fact]
        public void ConsolidatedStatute_IsExportedUnderTheAnnouncement()
        {
            var xml = LegalDocumentConverter.Convert(Parse(ConsolidatedText));

            var announcement = Assert.Single(xml.Elements());
            Assert.Equal("obwieszczenie", announcement.Name.LocalName);
            Assert.Equal(new[] { "metadane", "rodzaj-aktu", "autor-aktu", "data", "tytul", "ustawa" }, ChildNames(announcement));
            Assert.Equal("Obwieszczenie", announcement.Elements().Single(e => e.Name.LocalName == "rodzaj-aktu").Value);
            Assert.Equal("Marszałka Sejmu Rzeczypospolitej Polskiej",
                announcement.Elements().Single(e => e.Name.LocalName == "autor-aktu").Value);

            // Akt ogłaszany w załączniku ma własną strefę tytułową i tylko własne artykuły.
            var act = announcement.Elements().Single(e => e.Name.LocalName == "ustawa");
            Assert.Equal(new[] { "zalacznik-do", "rodzaj-aktu", "data", "tytul", "artykul", "artykul" }, ChildNames(act));
            Assert.Equal("USTAWA", act.Elements().Single(e => e.Name.LocalName == "rodzaj-aktu").Value);
            Assert.Equal("o odbudowie obiektów budowlanych", act.Elements().Single(e => e.Name.LocalName == "tytul").Value);
        }

        [Fact]
        public void ActAnnouncedDirectly_KeepsTheActAsTheRoot()
        {
            var xml = LegalDocumentConverter.Convert(Parse(ConsolidatedText.Skip(9)));

            Assert.Equal("ustawa", Assert.Single(xml.Elements()).Name.LocalName);
        }

        [Fact]
        public void AnnouncementOfAnotherKindOfAct_KeepsTheLegacyRoot()
        {
            // Tekst jednolity rozporządzenia: jednostek „§" potok jeszcze nie buduje, więc korzeń zostaje bez zmian.
            var lines = ConsolidatedText.Take(9).Concat(
            [
                "ROZPORZĄDZENIE", "MINISTRA FINANSÓW", "z dnia 11 maja 2017 r.", "w sprawie osób prawnych",
                "§ 1. Rozporządzenie określa wykaz osób prawnych.",
            ]);

            var xml = LegalDocumentConverter.Convert(Parse(lines));

            Assert.Equal("ustawa", Assert.Single(xml.Elements()).Name.LocalName);
        }

        [SchemaPlFact]
        public void ConsolidatedStatute_IsSchemaValid_InItsBody()
        {
            var xml = LegalDocumentConverter.Convert(Parse(ConsolidatedText));

            Assert.Empty(SchemaPl.Validator.ValidateActBody(xml.Elements().First()));
        }

        [Theory]
        [InlineData("o Policji")]
        [InlineData("o Państwowej Straży Pożarnej[1)]")]
        [InlineData("Kodeks cywilny[1)]")]
        [InlineData("Prawo restrukturyzacyjne")]
        public void AnnouncedActSubject_MayStartWithAProperName(string subject)
        {
            var lines = ConsolidatedText.ToArray();
            lines[11] = subject;

            Assert.Equal(subject, Parse(lines).Announcement!.ActSubject);
        }
    }
}
