using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Model;
using Saga.Model.EditorialUnits;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Poziom pominięty w akcie — litery wprost w ustępie, tirety wprost w ustępie albo punkcie.
    /// Model wstawia w to miejsce jednostkę niejawną; nie ma jej w eId ani w eksporcie, a jej dzieci
    /// stają w najbliższej jednostce jawnej jako jednostki spoza ZTP.
    /// </summary>
    public class SkippedLevelTests
    {
        private static readonly string[] Act =
        [
            "USTAWA", "z dnia 14 czerwca 2024 r.", "o ochronie sygnalistów",
            "Art. 1. 1. Ustawa określa:",
            "a) zasady ochrony,",
            "b) środki ochrony.",
            "2. Przepisy stosuje się do:",
            "– pracodawców,",
            "– organów.",
            "3. Zgłoszenie zawiera:",
            "1) dane zgłaszającego:",
            "– imię,",
            "– nazwisko;",
            "2) opis naruszenia.",
            "Art. 2. Ustawa wchodzi w życie po upływie 3 miesięcy od dnia ogłoszenia, " +
                "z wyjątkiem art. 1 ust. 1 lit. b, który wchodzi w życie z dniem 1 stycznia 2025 r.",
        ];

        private static LegalDocument Parse()
        {
            var blocks = new List<DocumentBlock>
            {
                new() { Text = "DZIENNIK USTAW RZECZYPOSPOLITEJ POLSKIEJ", Role = BlockRole.JournalHeader },
                new() { Text = "Warszawa, dnia 24 czerwca 2024 r.", Role = BlockRole.JournalHeader },
                new() { Text = "Poz. 928", Role = BlockRole.JournalHeader },
            };
            blocks.AddRange(Act.Select(text => new DocumentBlock { Text = text }));

            return LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
        }

        private static List<Paragraph> Paragraphs(LegalDocument document) =>
            document.Articles.First(a => a.Number!.Value == "1").Paragraphs;

        private static XElement Unit(XElement xml, string eId) =>
            xml.Descendants().Single(e => (string?)e.Attribute("eId") == eId);

        private static List<string> ChildNames(XElement element) =>
            element.Elements().Select(e => e.Name.LocalName).ToList();

        [Fact]
        public void LettersDirectlyInAParagraph_StandInAnImplicitPoint_LeftOutOfTheEId()
        {
            var paragraph = Paragraphs(Parse())[0];

            var point = Assert.Single(paragraph.Points);
            Assert.True(point.IsImplicit);
            Assert.Null(point.Number);
            Assert.Equal("art_1__ust_1", point.Id);
            Assert.Equal(new[] { "art_1__ust_1__lit_a", "art_1__ust_1__lit_b" }, point.Letters.Select(l => l.Id));

            // Wprowadzeniem do wyliczenia jest zdanie ustępu — punkt niejawny nie ma treści.
            Assert.Contains(paragraph.CommonParts, part => part.Type == CommonPartType.Intro);
            Assert.Empty(point.CommonParts);
        }

        [Fact]
        public void TiretsWithoutALetter_StandInAnImplicitLetter_LeftOutOfTheEId()
        {
            var paragraphs = Paragraphs(Parse());

            // Tirety wprost w ustępie: niejawny punkt i niejawna litera.
            var letter = Assert.Single(Assert.Single(paragraphs[1].Points).Letters);
            Assert.True(letter.IsImplicit);
            Assert.True(letter.Point!.IsImplicit);
            Assert.Equal(new[] { "art_1__ust_2__tir_1", "art_1__ust_2__tir_2" }, letter.Tirets.Select(t => t.Id));

            // Tirety wprost w punkcie: punkt jest jawny, litera niejawna.
            var point = paragraphs[2].Points[0];
            Assert.False(point.IsImplicit);
            Assert.True(Assert.Single(point.Letters).IsImplicit);
            Assert.Equal(new[] { "art_1__ust_3__pkt_1__tir_1", "art_1__ust_3__pkt_1__tir_2" },
                point.Letters[0].Tirets.Select(t => t.Id));
        }

        [Fact]
        public void Export_PutsTheChildrenStraightIntoTheNearestRealUnit()
        {
            var xml = LegalDocumentConverter.Convert(Parse());

            var first = Unit(xml, "art_1__ust_1");
            Assert.Equal("nie-ztp-ustep", first.Name.LocalName);
            Assert.Equal(new[] { "nr", "p", "nie-ztp-litera", "nie-ztp-litera" }, ChildNames(first));

            var second = Unit(xml, "art_1__ust_2");
            Assert.Equal("nie-ztp-ustep", second.Name.LocalName);
            Assert.Equal(new[] { "nr", "p", "nie-ztp-tiret", "nie-ztp-tiret" }, ChildNames(second));

            // Ustęp z dwoma punktami zostaje zwykłym ustępem; spoza ZTP jest tylko punkt z tiretami.
            var third = Unit(xml, "art_1__ust_3");
            Assert.Equal("ustep", third.Name.LocalName);
            Assert.Equal(new[] { "nr", "p", "nie-ztp-punkt", "punkt" }, ChildNames(third));
            Assert.Equal(new[] { "nr", "p", "nie-ztp-tiret", "nie-ztp-tiret" }, ChildNames(Unit(xml, "art_1__ust_3__pkt_1")));

            // Żadna jednostka nie wychodzi bez numeru.
            Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName == "nr" && e.Value == "UNKNOWN");
        }

        [Fact]
        public void Export_ContinuesTheSentenceOfTheParagraphInItsLetters()
        {
            var xml = LegalDocumentConverter.Convert(Parse());

            var fragments = Unit(xml, "art_1__ust_1").Descendants()
                .Where(e => e.Name.LocalName == "zdanie")
                .Select(e => (e.Attribute("nr")!.Value, e.Attribute("fragment")!.Value))
                .ToList();

            Assert.Equal(new[] { ("1", "1"), ("1", "2"), ("1", "3") }, fragments);
        }

        [Fact]
        public void ReferenceSkippingTheLevel_FindsTheLetter()
        {
            // „art. 1 ust. 1 lit. b" — powołanie pomija punkt tak samo jak akt.
            var paragraph = Paragraphs(Parse())[0];
            var letters = paragraph.Points[0].Letters;

            Assert.Equal(new DateTime(2025, 1, 1), letters[1].EffectiveDate);
            Assert.NotEqual(new DateTime(2025, 1, 1), letters[0].EffectiveDate);
            Assert.NotEqual(new DateTime(2025, 1, 1), paragraph.EffectiveDate);
        }

        [SchemaPlFact]
        public void Export_IsSchemaValid()
        {
            Assert.Empty(SchemaPl.Validator.Validate(LegalDocumentConverter.Convert(Parse())));
        }
    }
}
