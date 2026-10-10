using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Core.Services.Converters;
using Saga.Core.Services.Parsing;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Cele komendy nowelizacyjnej w eksporcie (ADR-0016): jeden cel → <c>element-eId</c>; przedział
    /// („ust. 8-10") → jeden cel z <c>element-eId</c> i <c>element-do-eId</c>; wyliczenie („ust. 2 i 3")
    /// → osobna komenda na każdy cel, każda z własnym <c>element-eId</c> i <c>nowGUID</c>.
    /// </summary>
    public class AmendmentTargetsExportTests
    {
        private static (XDocument Xml, ParsingContext Context) Convert(params string[] commandLines)
        {
            var document = new LegalDocument { Type = LegalActType.Statute };
            var subchapter = document.RootPart.Books[0].Titles[0].Divisions[0].Chapters[0].Subchapters[0];
            var orchestrator = new ParserOrchestrator();
            var context = new ParsingContext(document, subchapter);

            var lines = new[] { "Art. 1. W ustawie wprowadza się następujące zmiany:" }.Concat(commandLines);
            foreach (var line in lines)
                orchestrator.ProcessBlock(new DocumentBlock { Text = line, Source = new BlockSourceLocation() }, context);
            orchestrator.Finalize(context);

            return (new XDocument(LegalDocumentConverter.Convert(document)), context);
        }

        private static List<XElement> Commands(XDocument xml) =>
            xml.Descendants()
               .Where(e => e.Name.LocalName is "cytat-strukt" or "cytat-w-tekscie" or "uchyl")
               .ToList();

        // Po nazwie lokalnej — atrybuty GUID stoją w przestrzeni „gd".
        private static string? Attr(XElement element, string name) =>
            element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == name)?.Value;

        private static string[] UnitIds(XElement citation) =>
            citation.Elements().Select(unit => Attr(unit, "eId") ?? string.Empty).ToArray();

        [Fact]
        public void EnumeratedUnits_WithNewWording_GetACitationEach()
        {
            // Korpus: 305911_2729460 — „w art. 99 ust. 2 i 3 otrzymują brzmienie:".
            var (xml, _) = Convert(
                "1) w art. 99 ust. 2 i 3 otrzymują brzmienie:",
                "„2. Pierwsze nowe brzmienie.",
                "3. Drugie nowe brzmienie.”;");

            var commands = Commands(xml);

            Assert.Equal(new[] { "art_99__ust_2", "art_99__ust_3" }, commands.Select(c => Attr(c, "element-eId")));
            Assert.All(commands, c => Assert.Null(Attr(c, "element-do-eId")));
            // eId jednostki cytatu zaczyna się od adresu celu w akcie zmienianym.
            Assert.Equal(new[] { "art_99__ust_2" }, UnitIds(commands[0]));
            Assert.Equal(new[] { "art_99__ust_3" }, UnitIds(commands[1]));
            Assert.NotEqual(Attr(commands[0], "nowGUID"), Attr(commands[1], "nowGUID"));

            // Cytat nadal jeden: pierwsza komenda go otwiera, ostatnia zamyka, a średnik stoi raz — po nim.
            Assert.Equal(new[] { "otwierajacy", "zamykajacy" }, commands.Select(c => Attr(c, "cudzyslowy")));
            Assert.Null(commands[0].NextNode as XText);
            Assert.Equal(";", (commands[1].NextNode as XText)?.Value);
        }

        [Fact]
        public void Range_IsOneCitation_FromItsFirstToItsLastUnit()
        {
            // Korpus: 347969_3084519 — „ust. 8-10 otrzymują brzmienie:".
            var (xml, _) = Convert(
                "1) w art. 23 ust. 8-10 otrzymują brzmienie:",
                "„8. Brzmienie ósmego.",
                "9. Brzmienie dziewiątego.",
                "10. Brzmienie dziesiątego.”;");

            var citation = Assert.Single(Commands(xml));

            Assert.Equal("art_23__ust_8", Attr(citation, "element-eId"));
            Assert.Equal("art_23__ust_10", Attr(citation, "element-do-eId"));
            Assert.Equal(new[] { "art_23__ust_8", "art_23__ust_9", "art_23__ust_10" }, UnitIds(citation));
            Assert.Equal("oba", Attr(citation, "cudzyslowy"));
        }

        [Fact]
        public void Range_InTheCanonicalSnapshot_KeepsItsEnd()
        {
            var (_, context) = Convert(
                "1) w art. 23 ust. 8-10 otrzymują brzmienie:",
                "„8. Brzmienie ósmego.",
                "9. Brzmienie dziewiątego.",
                "10. Brzmienie dziesiątego.”;");

            var target = Assert.Single(XDocument.Parse(CanonicalDtoXmlSerializer.Serialize(context.Document))
                .Descendants("target"));

            Assert.Equal("art_23__ust_8", target.Value);
            Assert.Equal("art_23__ust_10", Attr(target, "rangeEnd"));
        }

        [Fact]
        public void CommandWithoutATarget_IsExportedAsUnknown()
        {
            var sentence = new XElement("zdanie");

            AmendmentXmlConverter.ToXml(
                new Amendment { OperationType = AmendmentOperationType.Repeal }, sentence);

            var instruction = Assert.Single(sentence.Elements());
            Assert.Equal("UNKNOWN", Attr(instruction, "element-eId"));
            Assert.Null(Attr(instruction, "element-do-eId"));
        }

        [Fact]
        public void RepealOfAnEnumerationWithARange_GivesAnInstructionPerTarget()
        {
            // Korpus: 318581_2836177 — „uchyla się pkt 31, 32 i 34-36;".
            var (xml, _) = Convert("1) w art. 2 w ust. 1 uchyla się pkt 31, 32 i 34-36;");

            var instructions = Commands(xml);

            Assert.All(instructions, i => Assert.Equal("uchyl", i.Name.LocalName));
            Assert.Equal(
                new[] { "art_2__ust_1__pkt_31", "art_2__ust_1__pkt_32", "art_2__ust_1__pkt_34" },
                instructions.Select(i => Attr(i, "element-eId")));
            Assert.Equal(new string?[] { null, null, "art_2__ust_1__pkt_36" }, instructions.Select(i => Attr(i, "element-do-eId")));
            Assert.Equal(3, instructions.Select(i => Attr(i, "nowGUID")).Distinct().Count());
        }

        [Fact]
        public void ReplacedWords_InSeveralUnits_GetACommandPerUnit()
        {
            // Korpus: 278228_2482477 (skrócone do dwóch ustępów).
            var (xml, _) = Convert(
                "1) w art. 4 w ust. 1 i 8 wyrazy „rozwoju regionalnego” zastępuje się wyrazami „szkolnictwa wyższego”;");

            var commands = Commands(xml);

            Assert.Equal(new[] { "art_4__ust_1", "art_4__ust_8" }, commands.Select(c => Attr(c, "element-eId")));
            Assert.All(commands, c => Assert.Equal("szkolnictwa wyższego", c.Value));
            Assert.NotEqual(Attr(commands[0], "nowGUID"), Attr(commands[1], "nowGUID"));
        }

        [Fact]
        public void ContentThatDoesNotMatchTheTargets_StaysOneCommand_AndIsReported()
        {
            // Dwa cele, jedna jednostka treści — nie wiadomo, któremu celowi ją przypisać. Eksport zostaje
            // przy jednej komendzie na pierwszy cel, a właściciel dostaje ostrzeżenie.
            var (xml, context) = Convert(
                "1) art. 24 i art. 25 otrzymują brzmienie:",
                "„Art. 24. Jedyne nowe brzmienie.”;");

            var citation = Assert.Single(Commands(xml));
            Assert.Equal("art_24", Attr(citation, "element-eId"));

            var owner = context.Document.Articles.First().Paragraphs[0].Points.Single();
            Assert.Equal(2, owner.Amendment?.Targets.Count);
            Assert.Contains(owner.ValidationMessages, m =>
                m.Level == ValidationLevel.Warning && m.Message.Contains("nie da się rozdzielić", StringComparison.Ordinal));
        }

        [Theory]
        [InlineData("1) w art. 27:", new[] { "art_27" })]                   // korpus: 301399_2685961
        [InlineData("1) w art. 27 i 28:", new[] { "art_27", "art_28" })]    // wyliczenie w komendzie nadrzędnej
        public void CommandWithoutItsOwnAddress_InheritsEveryTargetOfItsParent(string parent, string[] expected)
        {
            var (xml, _) = Convert(
                parent,
                "a) wyrazy „Przepisu art. 3d” zastępuje się wyrazami „Przepisów art. 3d i art. 3e”,",
                "b) wyrazy „pkt 11 i 12” zastępuje się wyrazami „pkt 11-12”;");

            var perLetter = Commands(xml)
                .GroupBy(c => c.Value)
                .Select(group => group.Select(c => Attr(c, "element-eId")).ToArray())
                .ToList();

            Assert.Equal(2, perLetter.Count);
            Assert.All(perLetter, targets => Assert.Equal(expected, targets));
        }

        [Fact]
        public void OneCommandInSeveralCitations_GivesEachItsOwnGuid()
        {
            // Korpus: 351212_3113348 — tytuł rozdziału (cytat tekstowy) i artykuły (cytat strukturalny)
            // z jednej komendy; schemat wymaga unikalnego nowGUID każdego cytatu.
            var (xml, _) = Convert(
                "1) art. 5 otrzymuje brzmienie:",
                "„Przepis wprowadzający",
                "Art. 5. Nowe brzmienie artykułu.”;");

            var citations = Commands(xml);

            Assert.Equal(new[] { "cytat-w-tekscie", "cytat-strukt" }, citations.Select(c => c.Name.LocalName));
            Assert.Equal(2, citations.Select(c => Attr(c, "nowGUID")).Distinct().Count());
        }

        [Fact]
        public void ReplacedWords_AreQuotedOnBothEnds_ByTheAttribute()
        {
            // Jak w przykładach autorów schematu: cudzysłowy w atrybucie („oba"), nie w treści cytatu.
            var (xml, _) = Convert("1) w art. 5 wyrazy „organ właściwy” zastępuje się wyrazami „organ nadzoru”;");

            var citation = Assert.Single(Commands(xml));
            Assert.Equal("cytat-w-tekscie", citation.Name.LocalName);
            Assert.Equal("oba", Attr(citation, "cudzyslowy"));
            Assert.Equal("organ nadzoru", citation.Value);
        }

        [Fact]
        public void TextCommandsForSeveralTargets_LeaveNoStrayPunctuationInTheSentence()
        {
            // Treść bez jednostek powtarza się w każdej komendzie; cudzysłowy niesie atrybut, a interpunkcja
            // po cytacie trafia do zdania raz — nie raz na komendę.
            var amendment = new Amendment
            {
                OperationType = AmendmentOperationType.Modification,
                Content = new AmendmentContent { PlainText = "Organ właściwy:" },
                HasOpeningQuote = true,
                HasClosingQuote = true,
                TrailingPunctuation = ";",
                Targets = [Target("1"), Target("2")],
            };
            var sentence = new XElement("zdanie", "w ust. 1 i 2 wprowadzenie do wyliczenia otrzymuje brzmienie:");

            AmendmentXmlConverter.ToXml(amendment, sentence);

            Assert.Equal(new[] { "Organ właściwy:", "Organ właściwy:" }, sentence.Elements().Select(c => c.Value));
            Assert.All(sentence.Elements(), c => Assert.Equal("oba", Attr(c, "cudzyslowy")));
            Assert.Equal(";", (sentence.LastNode as XText)?.Value);
            Assert.Equal(2, sentence.Nodes().OfType<XText>().Count());
        }

        // ============================================================
        // Przypisanie jednostek treści do celów
        // ============================================================

        private static StructuralAmendmentReference Target(string paragraph, string? rangeEnd = null) => new()
        {
            Structure = new StructuralReference { Article = "5", Paragraph = paragraph },
            RangeEnd = rangeEnd is null ? null : new StructuralReference { Article = "5", Paragraph = rangeEnd },
        };

        private static BaseEntity Unit(string number) =>
            new Saga.Model.EditorialUnits.Paragraph { Number = new EntityNumber { Value = number } };

        [Fact]
        public void Pairing_ByDesignation_GivesARangeItsWholeSpan()
        {
            var units = new[] { Unit("2"), Unit("3"), Unit("4"), Unit("6") };

            var paired = AmendmentTargetPairing.Pair([Target("2", rangeEnd: "4"), Target("6")], units);

            Assert.NotNull(paired);
            Assert.Equal(new[] { 3, 1 }, paired!.Select(group => group.Count));
        }

        [Fact]
        public void Pairing_FallsBackToOrder_WhenNumbersDoNotCarryTheDesignation()
        {
            // „§ 1 i 1¹" — indeks górny nie trafia do numeru jednostki treści, więc oznaczenia się rozjeżdżają;
            // celów bez przedziału jest tyle co jednostek, więc decyduje kolejność.
            var units = new[] { Unit("1"), Unit("1") };

            var paired = AmendmentTargetPairing.Pair([Target("1"), Target("1[1]")], units);

            Assert.NotNull(paired);
            Assert.Same(units[1], paired![1].Single());
        }

        [Fact]
        public void Pairing_Fails_WhenUnitsAndTargetsDisagree()
        {
            Assert.Null(AmendmentTargetPairing.Pair([Target("2")], []));
            Assert.Null(AmendmentTargetPairing.Pair([Target("2"), Target("3")], [Unit("2")]));
            Assert.Null(AmendmentTargetPairing.Pair([Target("2", rangeEnd: "4"), Target("6")], [Unit("2"), Unit("6")]));
        }

        [Fact]
        public void AmendedAct_IsIdentifiedByItsFullEli()
        {
            // Przykłady autorów schematu: now:akt="https://eli.gov.pl/eli/DU/2025/350" — z publikatorem.
            var document = LegalDocumentParser.ParseBlocks(new[]
            {
                "Art. 1. W ustawie z dnia 13 października 1998 r. o systemie ubezpieczeń społecznych (Dz. U. z 2025 r. poz. 350) art. 5 otrzymuje brzmienie:",
                "„Art. 5. Zakład wykonuje zadania określone w ustawie.”.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
            }.Select(text => new DocumentBlock { Text = text }).ToList());

            var citation = LegalDocumentConverter.Convert(document).Descendants().Single(e => e.Name.LocalName == "cytat-strukt");

            Assert.Equal("https://eli.gov.pl/eli/DU/2025/350",
                citation.Attributes().Single(a => a.Name.LocalName == "akt").Value);
        }

        [Fact]
        public void AmendedAct_PublishedInTheYearOfItsDate_GetsThatYearInItsEli()
        {
            // „(Dz. U. poz. 1688)" bez rocznika: akt ogłoszono w roku z jego daty (§ 158 ust. 7 ZTP).
            var document = LegalDocumentParser.ParseBlocks(new[]
            {
                "Art. 1. W ustawie z dnia 7 lipca 2023 r. o zmianie ustawy o planowaniu (Dz. U. poz. 1688) art. 5 otrzymuje brzmienie:",
                "„Art. 5. Plan ogólny uchwala rada gminy.”.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
            }.Select(text => new DocumentBlock { Text = text }).ToList());

            var citation = LegalDocumentConverter.Convert(document).Descendants().Single(e => e.Name.LocalName == "cytat-strukt");

            Assert.Equal("https://eli.gov.pl/eli/DU/2023/1688",
                citation.Attributes().Single(a => a.Name.LocalName == "akt").Value);
            Assert.Equal("DU/2023/1688__art_5", citation.Attribute("element-eId")?.Value);
        }
    }
}
