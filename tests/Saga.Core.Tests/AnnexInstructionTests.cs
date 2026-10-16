using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Core.Services.Parsing;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Załącznik zmieniany odesłaniem do załącznika aktu zmieniającego (ADR-0058): przepis nie ma cytatu, a eksport
    /// niesie instrukcję <c>now:zastap</c> albo <c>now:dodaj-po</c> z adresami obu załączników.
    /// </summary>
    public class AnnexInstructionTests
    {
        private static (bool Adds, string? Target, string? Source, string? SourceTo)[] Read(string text) =>
            AnnexInstructionReader.Read(text).Select(i => (i.Adds, i.Target, i.Source, i.SourceTo)).ToArray();

        [Fact]
        public void ReplacedAnnexes_ArePairedWithTheAnnexesOfTheAmendingAct()
        {
            // Przykład autorów schematu (DU/2026/252).
            Assert.Equal(
                [(false, "1", "1", null), (false, "2", "2", null)],
                Read("załączniki nr 1 i 2 do ustawy otrzymują brzmienie określone w załącznikach nr 1 i 2 do niniejszej ustawy;"));
        }

        [Theory]
        // Korpus: 347895_3208505, 321690_2980834 — „odpowiednio" mówi tylko, że załączniki idą parami.
        [InlineData("załączniki nr 1 i 2 do rozporządzenia otrzymują brzmienie określone odpowiednio w załącznikach nr 1 i 2 do niniejszego rozporządzenia;")]
        [InlineData("załączniki nr 1 i 2 otrzymują brzmienie określone odpowiednio w załącznikach nr 1 i 2 do niniejszego rozporządzenia.")]
        public void ReplacedAnnexes_SourcedRespectively_ArePairedTheSameWay(string text)
        {
            Assert.Equal([(false, "1", "1", null), (false, "2", "2", null)], Read(text));
        }

        [Fact]
        public void AddedAnnexes_SourcedRespectively_AreOneInstruction()
        {
            Assert.Equal(
                [(true, "2", "3", "4")],
                Read("dodaje się załączniki nr 3 i 4 do ustawy w brzmieniu określonym odpowiednio w załącznikach nr 3 i 4 do niniejszej ustawy."));
        }

        [Fact]
        public void RangeOfReplacedAnnexes_IsPairedOneByOne()
        {
            // Korpus: 275887_3019095 — „załączniki nr 3–9 … w załącznikach nr 5–11".
            var instructions = Read("załączniki nr 3–9 do rozporządzenia otrzymują brzmienie określone w załącznikach nr 5–11 do niniejszego rozporządzenia.");

            Assert.Equal(new[] { "3", "4", "5", "6", "7", "8", "9" }, instructions.Select(i => i.Target));
            Assert.Equal(new[] { "5", "6", "7", "8", "9", "10", "11" }, instructions.Select(i => i.Source));
        }

        [Fact]
        public void ReplacedAnnex_WhoseSourceHasNoNumber_NamesOnlyTheTarget()
        {
            Assert.Equal(
                [(false, "1", null, null)],
                Read("załącznik nr 1 do rozporządzenia otrzymuje brzmienie określone w załączniku do niniejszego rozporządzenia."));
        }

        [Theory]
        // Miejsce wskazane wprost.
        [InlineData("po załączniku nr 2 do rozporządzenia dodaje się załączniki nr 2a i 2b w brzmieniu określonym w załącznikach nr 3 i 4 do niniejszego rozporządzenia;", "2", "3", "4")]
        // Miejsce wynika z kolejności numerów: „nr 3" staje po „nr 2", „nr 2a" po „nr 2".
        [InlineData("dodaje się załączniki nr 3 i 4 do ustawy w brzmieniu określonym w załącznikach nr 3 i 4 do niniejszej ustawy.", "2", "3", "4")]
        [InlineData("dodaje się załącznik nr 4 do ustawy w brzmieniu określonym w załączniku do niniejszej ustawy.", "3", null, null)]
        [InlineData("dodaje się załącznik nr 2a do rozporządzenia w brzmieniu określonym w załączniku nr 1 do niniejszego rozporządzenia.", "2", "1", null)]
        // Pierwszy załącznik aktu nie ma poprzednika.
        [InlineData("dodaje się załącznik nr 1 do ustawy w brzmieniu określonym w załączniku do niniejszej ustawy.", null, null, null)]
        public void AddedAnnexes_AreOneInstructionAfterTheAnnexBeforeThem(string text, string? after, string? source, string? sourceTo)
        {
            Assert.Equal([(true, after, source, sourceTo)], Read(text));
        }

        [Theory]
        // Zmienia się część załącznika, nie załącznik.
        [InlineData("w załączniku nr 4 do rozporządzenia tabela D otrzymuje brzmienie określone w załączniku do niniejszego rozporządzenia.")]
        [InlineData("w załączniku po lp. 71 dodaje się lp. 72 w brzmieniu określonym w załączniku do niniejszego rozporządzenia.")]
        // Załącznik bez numeru nie ma adresu.
        [InlineData("załącznik do rozporządzenia otrzymuje brzmienie określone w załączniku do niniejszego rozporządzenia.")]
        // Brzmienie przytoczone, nie odesłanie.
        [InlineData("załącznik nr 1 do ustawy otrzymuje brzmienie:")]
        public void OtherCommands_AreNotAnnexInstructions(string text)
        {
            Assert.Empty(AnnexInstructionReader.Read(text));
        }

        private static System.Xml.Linq.XElement Convert(params string[] body)
        {
            // Akt bez nagłówka dziennika nie ma własnego prefiksu — adres załącznika aktu zmieniającego zostaje bez niego.
            string[] heading = ["USTAWA", "z dnia 14 czerwca 2024 r.", "o zmianie ustawy o opłatach"];
            var blocks = heading.Concat(body).Append("Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.")
                .Select(line => new DocumentBlock { Text = line }).ToList();
            return LegalDocumentConverter.Convert(
                LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!);
        }

        private static string? Attr(System.Xml.Linq.XElement element, string name) =>
            element.Attributes().FirstOrDefault(a => a.Name.LocalName == name)?.Value;

        [Fact]
        public void ReplacedAnnex_IsExportedAsAnInstructionWithBothAnnexes()
        {
            var xml = Convert(
                "Art. 1. W ustawie z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) załącznik nr 1 do ustawy otrzymuje brzmienie określone w załączniku nr 2 do niniejszej ustawy.");

            var instruction = Assert.Single(xml.Descendants(), e => e.Name.LocalName == "zastap");
            Assert.Equal("https://eli.gov.pl/eli/DU/2021/200", Attr(instruction, "akt"));
            Assert.Equal("DU/2021/200__zal_1", Attr(instruction, "element-eId"));
            Assert.StartsWith("https://eli.gov.pl/eli/", Attr(instruction, "akt-nowelizujacy"));
            Assert.Equal("zal_2", Attr(instruction, "nowelizujacy-element-eId"));

            // Przepis nie ma cytatu ani ostrzeżenia o braku celu — celem jest załącznik z instrukcji.
            Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName.StartsWith("cytat", StringComparison.Ordinal));
        }

        [Fact]
        public void AddedAnnexes_AreExportedAsOneInstructionWithTheirRange()
        {
            var xml = Convert(
                "Art. 1. W ustawie z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) dodaje się załączniki nr 3 i 4 do ustawy w brzmieniu określonym w załącznikach nr 1 i 2 do niniejszej ustawy.");

            var instruction = Assert.Single(xml.Descendants(), e => e.Name.LocalName == "dodaj-po");
            Assert.Equal("DU/2021/200__zal_2", Attr(instruction, "element-eId"));
            Assert.Equal("zal_1", Attr(instruction, "nowelizujacy-element-eId"));
            Assert.Equal("zal_2", Attr(instruction, "nowelizujacy-element-eId-do"));
        }

        [Fact]
        public void AnnexInstruction_IsOnTheAmendmentOfTheProvision_WithoutATargetWarning()
        {
            var blocks = new[]
            {
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o zmianie ustawy o opłatach",
                "Art. 1. W ustawie z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) załącznik nr 1 do ustawy otrzymuje brzmienie określone w załączniku do niniejszej ustawy.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
            }.Select(line => new DocumentBlock { Text = line }).ToList();
            var document = LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

            var provision = document.Articles.First().Paragraphs.Single();
            var annex = Assert.Single(provision.Amendment!.AnnexInstructions);
            Assert.Equal((false, "1", (string?)null), (annex.Adds, annex.Target, annex.Source));
            Assert.DoesNotContain(provision.ValidationMessages, m => m.Message.Contains("bez wykrytego celu", StringComparison.Ordinal));
        }
    }
}
