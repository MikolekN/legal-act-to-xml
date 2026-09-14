using System.Text;
using System.Xml.Linq;
using Saga.Core.Services;
using Saga.Core.Services.Converters;
using Saga.Core.Services.EntryIntoForce;
using Saga.Model;
using Saga.Model.EditorialUnits;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Przysłanianie daty wejścia w życie mocą wsteczną NA WYJŚCIU.
    ///
    /// Podział ról: model trzyma obie daty osobno (pilnuje tego <c>EffectiveDatePropagatorTests</c>),
    /// a ten plik sprawdza, którą z nich widać w eksporcie. Drzewo jest budowane ręcznie, bo tylko
    /// tak da się ustawić wszystkie cztery stany dat jednostki, łącznie z „sama moc wsteczna".
    /// </summary>
    public class EntryIntoForceXmlOutputTests
    {
        private static readonly DateTime Entry = new(2026, 1, 1);
        private static readonly DateTime Retroactive = new(2025, 7, 1);

        // ============================================================
        // Sama reguła precedencji
        // ============================================================

        public static TheoryData<DateTime, DateTime, string?> DateStates => new()
        {
			// wejście w życie, moc wsteczna, oczekiwana wartość atrybutu
			{ new DateTime(2026, 1, 1), default, "2026-01-01" },
            { new DateTime(2026, 1, 1), new DateTime(2025, 7, 1), "2025-07-01" },
            { default, new DateTime(2025, 7, 1), "2025-07-01" },
            { default, default, null },
        };

        [Theory]
        [MemberData(nameof(DateStates))]
        public void GoverningDate_PrefersRetroactivity_OverEntryDate(
            DateTime entry, DateTime retroactive, string? expected)
        {
            var article = new Article { EffectiveDate = entry, RetroactiveFrom = retroactive };

            Assert.Equal(expected, GoverningDate.Text(article));
        }

        [Fact]
        public void GoverningDate_ComparesPresence_NotChronology()
        {
            // Moc wsteczna PÓŹNIEJSZA niż data wejścia w życie to sprzeczność redakcyjna.
            // Reguła i tak ją bierze — cicha korekta wyniku byłaby zgadywaniem.
            var article = new Article
            {
                EffectiveDate = new DateTime(2026, 1, 1),
                RetroactiveFrom = new DateTime(2027, 1, 1),
            };

            Assert.Equal("2027-01-01", GoverningDate.Text(article));
        }

        // ============================================================
        // Eksport schemaPL
        // ============================================================

        /// <summary>Akt z jednym artykułem: ustęp ze zdaniem, punkt i część wspólna.</summary>
        private static LegalDocument BuildTree()
        {
            var document = new LegalDocument();
            var subchapter = document.RootPart.Books[0].Titles[0].Divisions[0].Chapters[0].Subchapters[0];

            var article = new Article { Parent = subchapter, Number = new EntityNumber { Value = "1" } };
            subchapter.Articles.Add(article);

            var paragraph = new Paragraph
            {
                Parent = article, Article = article, Number = new EntityNumber { Value = "1" },
                IsImplicit = true,
            };
            paragraph.TextSegments.Add(new TextSegment { Order = 1, Text = "Zdanie pierwsze." });
            article.Paragraphs.Add(paragraph);

            var point = new Point { Parent = paragraph, Number = new EntityNumber { Value = "1" } };
            point.TextSegments.Add(new TextSegment { Order = 1, Text = "Treść punktu." });
            paragraph.Points.Add(point);

            paragraph.CommonParts.Add(new CommonPart
            {
                Parent = paragraph, Type = CommonPartType.WrapUp, ContentText = "– przy czym…",
            });

            return document;
        }

        private static IEnumerable<IEntersIntoForce> Everything(LegalDocument document)
        {
            var article = document.Articles.Single();
            var paragraph = article.Paragraphs.Single();
            var point = paragraph.Points.Single();

            yield return article;
            yield return paragraph;
            yield return point;
            yield return paragraph.CommonParts.Single();

            foreach (var segment in paragraph.TextSegments) yield return segment;
            foreach (var segment in point.TextSegments) yield return segment;
        }

        private static List<string> EntryAttributes(XElement root) => root
            .Descendants()
            .Select(e => e.Attribute(XmlConverterConstants.Contents.EntryIntoForce)?.Value)
            .Where(value => value is not null)
            .ToList()!;

        private static string? Meta(XElement root, string name) =>
            root.Descendants().FirstOrDefault(e => e.Name.LocalName == name)?.Value;

        [Fact]
        public void Retroactivity_MasksTheEntryDate_OnEveryDatedElement()
        {
            var document = BuildTree();
            foreach (var unit in Everything(document))
            {
                unit.EffectiveDate = Entry;
                unit.RetroactiveFrom = Retroactive;
            }

            var dates = EntryAttributes(LegalDocumentConverter.Convert(document));

            // Jednostki, zdania i część wspólna — miejsca, które liczyły datę osobno, zanim
            // trafiły pod wspólny selektor.
            Assert.NotEmpty(dates);
            Assert.All(dates, d => Assert.Equal("2025-07-01", d));
            Assert.DoesNotContain("2026-01-01", dates);

            // Model zostaje nietknięty — przysłonięcie jest wyłącznie zapisem.
            Assert.All(Everything(document), u => Assert.Equal(Entry, u.EffectiveDate));
        }

        [Fact]
        public void WithoutRetroactivity_TheEntryDateIsWritten()
        {
            var document = BuildTree();
            foreach (var unit in Everything(document))
                unit.EffectiveDate = Entry;

            var dates = EntryAttributes(LegalDocumentConverter.Convert(document));

            Assert.NotEmpty(dates);
            Assert.All(dates, d => Assert.Equal("2026-01-01", d));
        }

        [Fact]
        public void RetroactivityAlone_FillsAnOtherwiseEmptyAttribute()
        {
            // Kształt z korpusu: „wchodzi w życie z dniem ogłoszenia, z mocą od dnia …" bez znanej
            // daty ogłoszenia. Bez przysłaniania jednostka nie miałaby w ogóle atrybutu.
            var document = BuildTree();
            foreach (var unit in Everything(document))
                unit.RetroactiveFrom = Retroactive;

            var dates = EntryAttributes(LegalDocumentConverter.Convert(document));

            Assert.NotEmpty(dates);
            Assert.All(dates, d => Assert.Equal("2025-07-01", d));
        }

        [Fact]
        public void WithoutAnyDate_NoAttributeIsWritten()
        {
            Assert.Empty(EntryAttributes(LegalDocumentConverter.Convert(BuildTree())));
        }

        // ============================================================
        // Metadane aktu i zrzut kanoniczny — bez przysłaniania
        // ============================================================

        [Fact]
        public void ActMetadata_KeepsTheFormalEntryDate_AndRecordsRetroactivitySeparately()
        {
            var document = BuildTree();
            document.EffectiveDate = Entry;
            document.RetroactiveFrom = Retroactive;

            var xml = LegalDocumentConverter.Convert(document);

            Assert.Equal("2026-01-01", Meta(xml, XmlConverterConstants.Metadata.EntryIntoForceElement));

            var retroactive = xml.Descendants()
                .Single(e => e.Name.LocalName == XmlConverterConstants.Metadata.RetroactiveDateElement);

            Assert.Equal("2025-07-01", Meta(retroactive, XmlConverterConstants.Metadata.PartialDate.Date));
            Assert.DoesNotContain(retroactive.Descendants(), e => e.Name.LocalName == XmlConverterConstants.Metadata.PartialDate.Unit);
        }

        [Fact]
        public void CanonicalDump_ShowsBothDates_SoTheModelStaysInspectable()
        {
            var document = BuildTree();
            foreach (var unit in Everything(document))
            {
                unit.EffectiveDate = Entry;
                unit.RetroactiveFrom = Retroactive;
            }

            var dump = XElement.Parse(CanonicalDtoXmlSerializer.Serialize(document));

            var article = dump.Descendants("article").Single();
            Assert.Equal("2026-01-01", article.Attribute("effectiveDate")?.Value);
            Assert.Equal("2025-07-01", article.Attribute("retroactiveFrom")?.Value);

            var segment = dump.Descendants("segment").First();
            Assert.Equal("2026-01-01", segment.Attribute("effectiveDate")?.Value);
            Assert.Equal("2025-07-01", segment.Attribute("retroactiveFrom")?.Value);
        }

        // ============================================================
        // Pełny potok
        // ============================================================

        [Fact]
        public void ParsedAct_WritesRetroactivity_ExceptOnTheUnitTakenOutByAnException()
        {
            var lines = new[]
            {
                "USTAWA",
                "z dnia 5 marca 2025 r.",
                "o zmianie ustawy o rzeczach różnych",
                "Art. 1. Wprowadza się zmiany porządkowe.",
                "Art. 2. Przepis przejściowy.",
                "Art. 3. Ustawa wchodzi w życie z dniem 1 stycznia 2026 r., z mocą od dnia 1 lipca 2025 r., " +
                "z wyjątkiem art. 2, który wchodzi w życie z dniem 1 marca 2026 r.",
            };

            using var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(string.Join("\n", lines)));
            var document = LegalDocumentParser.Parse(stream, "ustawa.txt",
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document;

            Assert.NotNull(document);
            var xml = LegalDocumentConverter.Convert(document!);

            var byEId = xml.Descendants()
                .Where(e => e.Attribute(XmlConverterConstants.Contents.eId) is not null)
                .ToDictionary(
                    e => e.Attribute(XmlConverterConstants.Contents.eId)!.Value,
                    e => e.Attribute(XmlConverterConstants.Contents.EntryIntoForce)?.Value);

            Assert.Equal("2025-07-01", byEId["art_1"]);
            Assert.Equal("2026-03-01", byEId["art_2"]);
            Assert.Equal("2025-07-01", byEId["art_3"]);

            // Metadana aktu nadal niesie datę formalną, nie przysłoniętą.
            Assert.Equal("2026-01-01", Meta(xml, XmlConverterConstants.Metadata.EntryIntoForceElement));
        }
    }
}
