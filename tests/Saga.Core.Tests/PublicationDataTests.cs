using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Dane ogłoszenia podane przez wywołującego (ADR-0081): uzupełniają dziennik, rocznik, pozycję i dzień ogłoszenia
    /// dokumentu, który ich nie niesie (szczotka przed publikacją), a dane z dokumentu wygrywają.
    /// </summary>
    public class PublicationDataTests
    {
        [Fact]
        public void ValuesFromAFormOrACommandLine_AreRead()
        {
            Assert.True(PublicationData.TryCreate("du", " 2026 ", "132", "2026-02-04", out var data, out var error));

            Assert.Null(error);
            Assert.Equal(new PublicationData
            {
                Publisher = PublisherType.DziennikUstaw,
                Year = 2026,
                Position = 132,
                AnnouncementDate = new DateTime(2026, 2, 4),
            }, data);
        }

        [Fact]
        public void NoValues_AreNoPublicationData()
        {
            Assert.True(PublicationData.TryCreate(null, "", "  ", null, out var data, out _));
            Assert.Null(data);
        }

        [Theory]
        [InlineData("DZ", null, null, null, "Nieznany dziennik")]
        [InlineData(null, "1917", null, null, "Rocznik")]
        [InlineData(null, "dwa tysiące", null, null, "Rocznik")]
        [InlineData(null, null, "0", null, "Pozycja")]
        [InlineData(null, null, "-5", null, "Pozycja")]
        [InlineData(null, null, null, "04.02.2026", "Dzień ogłoszenia")]
        public void WrongValue_IsReported(string? publisher, string? year, string? position, string? date, string expected)
        {
            Assert.False(PublicationData.TryCreate(publisher, year, position, date, out var data, out var error));

            Assert.Null(data);
            Assert.StartsWith(expected, error);
        }

        // ============================================================
        // Parsowanie
        // ============================================================

        // Szczotka bez nagłówka publikatora — korpus: 260120_2800427 i 40 innych.
        private static readonly string[] Proof =
        [
            "ROZPORZĄDZENIE",
            "MINISTRA INFRASTRUKTURY",
            "z dnia 14 stycznia 2026 r.",
            "w sprawie znaków i sygnałów drogowych",
            "Na podstawie art. 7 ust. 3 ustawy z dnia 20 czerwca 1997 r. – Prawo o ruchu drogowym (Dz. U. z 2024 r. poz. 1251) zarządza się, co następuje:",
            "§ 1. Rozporządzenie określa warunki techniczne znaków drogowych.",
            "§ 2. Rozporządzenie wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
        ];

        private static readonly PublicationData Given = new()
        {
            Publisher = PublisherType.DziennikUstaw,
            Position = 132,
            AnnouncementDate = new DateTime(2026, 2, 4),
        };

        private static LegalDocument Parse(PublicationData? publication) =>
            LegalDocumentParser.Parse(
                Proof.Select(line => new DocumentBlock { Text = line }).ToList(),
                new ParseOptions { Policy = ParsePolicy.AlwaysParse, Publication = publication }).Document!;

        [Fact]
        public void ProofWithoutAJournalHeader_TakesTheGivenPublication()
        {
            var document = Parse(Given);

            Assert.Equal(PublisherType.DziennikUstaw, document.SourceJournal.Publisher);
            Assert.Equal(2026, document.SourceJournal.Year);
            Assert.Equal([132], document.SourceJournal.Positions);
            Assert.Equal(new DateTime(2026, 2, 4), document.AnnouncementDate);
        }

        [Fact]
        public void EntryIntoForce_CountsFromTheGivenAnnouncementDate()
        {
            // „po upływie 14 dni od dnia ogłoszenia" — ogłoszone 4 lutego, wchodzi w życie 19 lutego.
            Assert.Equal(new DateTime(2026, 2, 19), Parse(Given).Articles.First().EffectiveDate);
            Assert.Equal(default, Parse(null).Articles.First().EffectiveDate);
        }

        [Fact]
        public void PublicationStatedInTheDocument_WinsOverTheGivenOne()
        {
            using var stream = new MemoryStream(DocxFixtures.WithRclJournalHeader(
                "USTAWA", "z dnia 9 stycznia 2026 r.", "o rzemiośle",
                "Art. 1. Ustawa określa zasady wykonywania rzemiosła.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia."));
            var document = LegalDocumentParser.Parse(stream, "ustawa.docx", new ParseOptions
            {
                Policy = ParsePolicy.AlwaysParse,
                Publication = new PublicationData { Year = 2025, Position = 999, AnnouncementDate = new DateTime(2025, 12, 1) },
            }).Document!;

            Assert.Equal([26], document.SourceJournal.Positions);
            Assert.Equal(2026, document.SourceJournal.Year);
            Assert.Equal(new DateTime(2026, 1, 12), document.AnnouncementDate);
        }

        [Fact]
        public void ActAddress_UsesTheGivenPublication()
        {
            var xml = LegalDocumentConverter.Convert(Parse(Given));

            Assert.All(xml.Descendants().Select(e => e.Attribute("eId")?.Value).OfType<string>(),
                eId => Assert.StartsWith("DU/2026/132__", eId));
        }

        [SchemaPlFact]
        public void ProofWithTheGivenPublication_IsSchemaValid_AndWithoutItIsNot()
        {
            Assert.Empty(SchemaPl.Validator.Validate(LegalDocumentConverter.Convert(Parse(Given))));
            Assert.NotEmpty(SchemaPl.Validator.Validate(LegalDocumentConverter.Convert(Parse(null))));
        }
    }
}
