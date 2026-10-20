using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Core.Services.Parsing;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Odnośnik jako przedmiot komendy nowelizacyjnej (ADR-0059): treść cytowana jest odnośnikiem — nie jednostkami,
    /// choć zaczyna się jak punkt — a eksport niesie cytat strukturalny z <c>odnosnik-cyt</c> pod adresem <c>odn_N</c>.
    /// </summary>
    public class QuotedFootnoteTests
    {
        [Theory]
        // Przykład autorów schematu (DU/2026/252).
        [InlineData("odnośnik nr 1 do tytułu ustawy otrzymuje brzmienie:", "1", false)]
        [InlineData("do tytułu ustawy dodaje się odnośnik nr 2 w brzmieniu:", "2", true)]
        [InlineData("odnośnik nr 3 otrzymuje brzmienie:", "3", false)]
        [InlineData("do tytułu rozporządzenia dodaje się odnośnik nr 1a w brzmieniu:", "1a", true)]
        // Korpus: 353666_3130982.
        [InlineData("w tytule ustawy po odnośniku nr 1 dodaje się przecinek i odnośnik nr 2 w brzmieniu:", "2", true)]
        public void CommandAboutAFootnote_IsRead(string text, string number, bool adds)
        {
            Assert.Equal(new QuotedFootnoteReader.Command(number, adds), QuotedFootnoteReader.Read(text));
        }

        [Theory]
        // Odnośnik w załączniku ma adres pod załącznikiem.
        [InlineData("w załączniku nr 2 do rozporządzenia odnośnik nr 3 otrzymuje brzmienie:")]
        // Zmienia się punkt odnośnika, nie odnośnik.
        [InlineData("w odnośniku nr 1 do tytułu rozporządzenia pkt 2 otrzymuje brzmienie:")]
        // Przedmiotem jest jednostka.
        [InlineData("w art. 5 ust. 2 otrzymuje brzmienie:")]
        // Odnośnik bez numeru nie ma adresu.
        [InlineData("po nazwie ministra wydającego rozporządzenie dodaje się odnośnik w brzmieniu:")]
        public void OtherCommands_AreNotAboutAFootnote(string text)
        {
            Assert.Null(QuotedFootnoteReader.Read(text));
        }

        [Theory]
        [InlineData("1) Niniejsza ustawa wdraża dyrektywę.", false)]
        // Znacznik złożony indeksem górnym, także razem z cudzysłowem otwierającym cytat.
        [InlineData("[1)] Niniejsza ustawa wdraża dyrektywę.", false)]
        [InlineData("[„1)] Niniejsza ustawa wdraża dyrektywę.", true)]
        // Korpus: 353666_3130982 — numer wstawiony polem nie dochodzi do tekstu, zostaje sam nawias w indeksie.
        [InlineData("[)] Niniejsza ustawa wdraża dyrektywę.", false)]
        public void FootnoteContent_DropsItsOwnMarker(string firstParagraph, bool quoteAtTheMarker)
        {
            var paragraphs = new[] { new CollectedAmendmentParagraph(firstParagraph, null, null) };

            var footnote = QuotedFootnoteReader.Build(new QuotedFootnoteReader.Command("1", false), paragraphs, out var opensWithQuote);

            Assert.Equal("1", footnote.Symbol);
            Assert.Equal(new[] { "Niniejsza ustawa wdraża dyrektywę." }, footnote.Paragraphs);
            Assert.Equal(quoteAtTheMarker, opensWithQuote);
        }

        private static LegalDocument Parse(params string[] body)
        {
            string[] heading =
            [
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o zmianie ustawy o opłatach",
                "Art. 1. W ustawie z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) wprowadza się następujące zmiany:",
            ];
            var blocks = heading.Concat(body).Append("Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.")
                .Select(line => new DocumentBlock { Text = line }).ToList();
            return LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
        }

        private static LegalDocument ParseBothCommands() => Parse(
            "1) odnośnik nr 1 do tytułu ustawy otrzymuje brzmienie:",
            "„1) Niniejsza ustawa wdraża dyrektywę 2022/2555.”;",
            "2) do tytułu ustawy dodaje się odnośnik nr 2 w brzmieniu:",
            "„2) Niniejsza ustawa służy stosowaniu rozporządzenia 2024/1366.”;");

        [Fact]
        public void FirstFootnoteAddedInAnAnnex_IsAManualInstructionOnTheAnnex()
        {
            // Korpus: 303357_3003029 — odnośnik załącznika nie ma adresu, a dodawany nr 1 nie ma poprzednika.
            var xml = LegalDocumentConverter.Convert(Parse(
                "1) w załączniku nr 2 do ustawy:",
                "a) w objaśnieniach odnośniki nr 1–7 oznacza się odpowiednio jako odnośniki nr 2–8 i dodaje się odnośnik nr 1 w brzmieniu:",
                "„1) Zgodnie z unijnym wykazem nowej żywności.”;"));

            var citation = Assert.Single(xml.Descendants(), e => e.Name.LocalName == "cytat-strukt");
            Assert.Equal("DU/2021/200__zal_2", citation.Attribute("element-eId")?.Value);
            Assert.Equal("dodaj-recznie", citation.Attribute("funkcja")?.Value);
            Assert.Equal("odnosnik-cyt", citation.Elements().Single().Name.LocalName);
        }

        [Fact]
        public void QuotedFootnote_IsNotBuiltAsUnits()
        {
            // Przedtem „1) Niniejsza ustawa …" bywało punktem 1 cytowanej treści albo tekstem bez adresu.
            var points = ParseBothCommands().Articles.First().Paragraphs.Single().Points;

            var replaced = points[0].Amendment!;
            Assert.Equal("1", replaced.Content!.Footnote?.Symbol);
            Assert.Equal(new[] { "Niniejsza ustawa wdraża dyrektywę 2022/2555." }, replaced.Content.Footnote!.Paragraphs);
            Assert.Empty(replaced.Content.Points);
            Assert.Null(replaced.Content.PlainText);
            Assert.Equal("odn_1", replaced.Targets.Single().Structure.ToString());

            // Odnośnik nr 2 staje po odnośniku nr 1.
            var added = points[1].Amendment!;
            Assert.Equal(AmendmentOperationType.Insertion, added.OperationType);
            Assert.Equal("odn_1", added.Targets.Single().Structure.ToString());
            Assert.Equal("2", added.Content!.Footnote?.Symbol);
        }

        [Fact]
        public void QuotedFootnote_IsExportedAsAStructuralCitationWithTheFootnote()
        {
            var xml = LegalDocumentConverter.Convert(ParseBothCommands());

            var citations = xml.Descendants().Where(e => e.Name.LocalName == "cytat-strukt").ToList();
            Assert.Equal(new[] { "zastap", "dodaj-po" }, citations.Select(c => c.Attribute("funkcja")?.Value));
            Assert.Equal(new[] { "DU/2021/200__odn_1", "DU/2021/200__odn_1" }, citations.Select(c => c.Attribute("element-eId")?.Value));
            Assert.All(citations, c => Assert.Equal("oba", c.Attribute("cudzyslowy")?.Value));

            var footnotes = citations.Select(c => c.Elements().Single()).ToList();
            Assert.All(footnotes, f => Assert.Equal("odnosnik-cyt", f.Name.LocalName));
            Assert.Equal(new[] { "DU/2021/200__odn_1", "DU/2021/200__odn_2" }, footnotes.Select(f => f.Attribute("eId")?.Value));

            // Znacznik stoi w treści indeksem górnym — numer cytowanego odnośnika nie jest wydzielany.
            var sentence = footnotes[0].Descendants().First(e => e.Name.LocalName == "zdanie");
            Assert.Equal("1)", sentence.Elements().First(e => e.Name.LocalName == "sup").Value);
            Assert.Equal("1) Niniejsza ustawa wdraża dyrektywę 2022/2555.", sentence.Value);
            Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName == "cytat-w-tekscie");
        }

        [Fact]
        public void EnumerationInsideAQuotedFootnote_StaysItsText()
        {
            // Korpus: 303357_3003029 — odnośnik z wyliczeniem; jego pozycje nie są punktami aktu.
            var document = Parse(
                "1) odnośnik nr 2 do tytułu ustawy otrzymuje brzmienie:",
                "„2) Niniejsza ustawa:",
                "1) wdraża dyrektywę 2022/2555;",
                "2) służy stosowaniu rozporządzenia 2024/1366.”;");

            var points = document.Articles.First().Paragraphs.Single().Points;
            Assert.Single(points);

            var footnote = points[0].Amendment!.Content!.Footnote!;
            Assert.Equal(new[] { "Niniejsza ustawa:", "1) wdraża dyrektywę 2022/2555;", "2) służy stosowaniu rozporządzenia 2024/1366." }, footnote.Paragraphs);
            Assert.Empty(points[0].Amendment!.Content!.Points);
        }

        private static LegalDocument ParseEnumeratingFootnote() => Parse(
            "1) odnośnik nr 2 do tytułu ustawy otrzymuje brzmienie:",
            "„2) Niniejsza ustawa:",
            "1) wdraża dyrektywę 2022/2555;",
            "2) służy stosowaniu rozporządzenia 2024/1366.”;");

        [Fact]
        public void EnumerationInsideAQuotedFootnote_IsExportedAsPointsOfThatFootnote()
        {
            // Treść cytowanego odnośnika ma tę samą budowę co odnośnika aktu (ADR-0078).
            var quoted = LegalDocumentConverter.Convert(ParseEnumeratingFootnote()).Descendants()
                .Single(e => e.Name.LocalName == "odnosnik-cyt");

            Assert.Equal(new[] { "p", "punkt-odnosnik", "punkt-odnosnik" }, quoted.Elements().Select(e => e.Name.LocalName));
            Assert.Equal("2) Niniejsza ustawa:", quoted.Elements().First().Value);
            Assert.Equal("1", quoted.Elements().ElementAt(1).Elements().First().Value);
        }

        [SchemaPlFact]
        public void EnumerationInsideAQuotedFootnote_IsSchemaValid_InTheActBody()
        {
            Assert.Empty(SchemaPl.Validator.ValidateActBody(LegalDocumentConverter.Convert(ParseEnumeratingFootnote()).Elements().First()));
        }

        [SchemaPlFact]
        public void QuotedFootnote_IsSchemaValid_InTheActBody()
        {
            Assert.Empty(SchemaPl.Validator.ValidateActBody(LegalDocumentConverter.Convert(ParseBothCommands()).Elements().First()));
        }
    }
}
