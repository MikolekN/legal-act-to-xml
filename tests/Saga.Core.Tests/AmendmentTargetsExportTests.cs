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
               .Where(e => e.Name.LocalName is "cytat-strukt" or "cytat-w-tekscie" or "uchyl" or "zamien-tekst"
                   or "dodaj-ustep" or "dodaj-paragraf")
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
            Assert.All(commands, c => Assert.Equal("zamien-tekst", c.Name.LocalName));
            Assert.All(commands, c => Assert.Equal("rozwoju regionalnego", Attr(c, "stary-tekst")));
            Assert.All(commands, c => Assert.Equal("szkolnictwa wyższego", Attr(c, "nowy-tekst")));
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
                .GroupBy(c => Attr(c, "nowy-tekst"))
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
        public void ReplacedWords_AreAnInstructionBeforeTheSentenceText_NotACitation()
        {
            // Jak w przykładach autorów schematu (DU/2026/26): zmiana wyrazów nie przytacza brzmienia jednostki,
            // więc jest instrukcją „now:zamien-tekst" z wyrazami zastępowanymi i zastępującymi, przed tekstem zdania.
            var (xml, _) = Convert("1) w art. 5 wyrazy „organ właściwy” zastępuje się wyrazami „organ nadzoru”;");

            var instruction = Assert.Single(Commands(xml));
            Assert.Equal("zamien-tekst", instruction.Name.LocalName);
            Assert.Equal(XmlConverterConstants.AmendmentNamespace, instruction.Name.NamespaceName);
            Assert.Equal("organ właściwy", Attr(instruction, "stary-tekst"));
            Assert.Equal("organ nadzoru", Attr(instruction, "nowy-tekst"));
            Assert.Equal("art_5", Attr(instruction, "element-eId"));
            Assert.NotNull(Attr(instruction, "akt"));
            Assert.True(instruction.IsEmpty);

            // Zdanie zachowuje swój tekst w całości, z cudzysłowami; instrukcja stoi przed nim.
            var sentence = instruction.Parent!;
            Assert.Same(instruction, sentence.Nodes().First(node => node is not XText { Value: "" }));
            Assert.Equal("w art. 5 wyrazy „organ właściwy” zastępuje się wyrazami „organ nadzoru”;", sentence.Value);
        }

        [Fact]
        public void EveryInstructionAndCitation_HasItsOwnGuid_AndNoTargetGuid()
        {
            // GUID elementu aktu zmienianego jest nieznany — akt zmieniający go nie niesie — więc eksport nie pisze
            // GUIDcelu; cel bez eId to instrukcja ręczna (odpowiedź autorów schematu, 2026-10-18). Każda instrukcja
            // i każdy cytat ma za to własny nowGUID, różny od pozostałych, także przy kopii na kolejne cele.
            string[] lines =
            [
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o zmianie ustawy o opłatach",
                "Art. 1. W ustawie z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) wprowadza się następujące zmiany:",
                "1) w art. 2 w ust. 1 uchyla się pkt 31, 32 i 34;",
                "2) w art. 5 i 6 wyrazy „organ właściwy” zastępuje się wyrazami „organ nadzoru”;",
                "3) w art. 7 i 8 zdanie pierwsze otrzymuje brzmienie:",
                "„Opłatę wnosi się w terminie 14 dni.”;",
                "4) po art. 9 dodaje się art. 9a w brzmieniu:",
                "„Art. 9a. Opłaty nie pobiera się.”;",
                "5) w załączniku nr 2 do ustawy uchyla się lp. 7.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
            ];
            var xml = LegalDocumentConverter.Convert(LegalDocumentParser.Parse(
                lines.Select(line => new DocumentBlock { Text = line }).ToList(),
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!);

            XNamespace gd = "https://projects.task.gda.pl/caise/NLP/kisi/rcl/GUIDs/1.0";
            var instructions = xml.Descendants()
                .Where(e => e.Name.NamespaceName == XmlConverterConstants.AmendmentNamespace || e.Name.LocalName.StartsWith("cytat-"))
                .ToList();
            Assert.Equal(9, instructions.Count);

            var guids = instructions.Select(e => e.Attribute(gd + "nowGUID")?.Value).ToList();
            Assert.All(guids, guid => Assert.True(Guid.TryParse(guid, out _)));
            Assert.Equal(guids.Count, guids.Distinct().Count());
            Assert.DoesNotContain(xml.Descendants().Attributes(), attribute => attribute.Name.LocalName.Contains("GUIDcelu"));
        }

        [Fact]
        public void RepealedUnit_IsAnInstructionBeforeTheSentenceText()
        {
            // Odpowiedź autora schematu (2026-10-27): instrukcja przed tekstem, jak w DU/2026/26 (ADR-0123).
            var (xml, _) = Convert("1) w art. 77 uchyla się ust. 5;");

            var instruction = Assert.Single(Commands(xml));
            Assert.Equal("uchyl", instruction.Name.LocalName);
            var sentence = instruction.Parent!;
            Assert.Same(instruction, sentence.Nodes().First(node => node is not XText { Value: "" }));
            Assert.Equal("w art. 77 uchyla się ust. 5;", sentence.Value);
        }

        [Fact]
        public void ReplacedWords_DoNotCarryAFootnoteOfThisAct_NorASoftHyphen()
        {
            // 306779_2736407: odnośnik aktu zmieniającego („z późn. zm.⁸⁾") stoi w wyrazach skreślanych; 331615_3027942:
            // miękki dywiz Worda w „jesienno-zimowym". Akt zmieniany nie ma ani jednego, ani drugiego.
            var document = new LegalDocument { Type = LegalActType.Statute };
            var context = new ParsingContext(document, document.RootPart.Books[0].Titles[0].Divisions[0].Chapters[0].Subchapters[0]);
            var orchestrator = new ParserOrchestrator();
            orchestrator.ProcessBlock(new DocumentBlock { Text = "Art. 1. W ustawie wprowadza się następujące zmiany:", Source = new BlockSourceLocation() }, context);
            orchestrator.ProcessBlock(new DocumentBlock
            {
                Text = "1) w art. 37 w ust. 4 skreśla się wyrazy „(Dz. U. z 2023 r. poz. 1270, z późn. zm.[8)])”;",
                Source = new BlockSourceLocation(),
                Footnotes = [new BlockFootnote("8", ["Zmiany tekstu jednolitego wymienionej ustawy zostały ogłoszone."]) { Marker = "8" }],
            }, context);
            orchestrator.ProcessBlock(new DocumentBlock
            {
                Text = "2) w art. 38 wyrazy „w okresie jesienno" + (char)0x00AD + "-zimowym” zastępuje się wyrazami „w okresie zimowym”.",
                Source = new BlockSourceLocation(),
            }, context);
            orchestrator.Finalize(context);
            var xml = new XDocument(LegalDocumentConverter.Convert(document));

            var instructions = Commands(xml);
            Assert.Equal(new[] { "(Dz. U. z 2023 r. poz. 1270, z późn. zm.)", "w okresie jesienno-zimowym" },
                instructions.Select(instruction => Attr(instruction, "stary-tekst")));

            // Treść odnośnika nie ginie — stoi w zdaniu komendy.
            Assert.Single(instructions[0].Parent!.Elements(), e => e.Name.LocalName == "odnosnik");
        }

        [Fact]
        public void NewWords_CarryTheFootnoteOfThisAct_AsAnXmlFragment()
        {
            // 296120_2785579 (Dz. U. 2025 poz. 43): odnośnik wymagany przy „z późn. zm." (§ 158 ust. 7 ZTP) stoi w wyrazach
            // nowych i przechodzi do aktu zmienianego razem z nimi — fragment XML odnośnika w nowy-tekst (ADR-0105).
            var document = new LegalDocument { Type = LegalActType.Statute };
            var context = new ParsingContext(document, document.RootPart.Books[0].Titles[0].Divisions[0].Chapters[0].Subchapters[0]);
            var orchestrator = new ParserOrchestrator();
            orchestrator.ProcessBlock(new DocumentBlock { Text = "Art. 1. W ustawie wprowadza się następujące zmiany:", Source = new BlockSourceLocation() }, context);
            orchestrator.ProcessBlock(new DocumentBlock
            {
                Text = "1) w art. 37 wyrazy „(Dz. Urz. UE L 20 z 31.01.2022, str. 95)” zastępuje się wyrazami „(Dz. Urz. UE L 20 z 31.01.2022, str. 95, z późn. zm.[1)])”.",
                Source = new BlockSourceLocation(),
                Footnotes = [new BlockFootnote("1", ["Zmiany wymienionego rozporządzenia zostały ogłoszone w Dz. Urz. UE L 31 & L 45."]) { Marker = "1" }],
            }, context);
            orchestrator.Finalize(context);
            var xml = new XDocument(LegalDocumentConverter.Convert(document));

            var change = Assert.Single(Commands(xml));
            var newWords = Attr(change, "nowy-tekst")!;
            Assert.StartsWith("(Dz. Urz. UE L 20 z 31.01.2022, str. 95, z późn. zm.<odnosnik id-odnosnika=\"1\" symbol=\"1\"", newWords);
            Assert.Contains("Zmiany wymienionego rozporządzenia zostały ogłoszone w Dz. Urz. UE L 31 &amp; L 45.", newWords);
            Assert.EndsWith("</odnosnik>)", newWords);
            Assert.DoesNotContain("xmlns", newWords);

            // Odnośnik zostaje też odnośnikiem aktu zmieniającego — w zdaniu komendy.
            Assert.Single(change.Parent!.Elements(), e => e.Name.LocalName == "odnosnik");
        }

        [Fact]
        public void AddedWords_AreAnInstructionReplacingTheIndicatedWordsWithThemAndTheAddedOnes()
        {
            // Przykład autorów schematu (DU/2026/26): „po wyrazach „art. 61b ust. 6" dodaje się wyrazy „i art. 68g"".
            var (xml, context) = Convert("1) w art. 69 po wyrazach „art. 61b ust. 6” dodaje się wyrazy „i art. 68g”;");

            var instruction = Assert.Single(Commands(xml));
            Assert.Equal("zamien-tekst", instruction.Name.LocalName);
            Assert.Equal("art. 61b ust. 6", Attr(instruction, "stary-tekst"));
            Assert.Equal("art. 61b ust. 6 i art. 68g", Attr(instruction, "nowy-tekst"));
            Assert.Equal("art_69", Attr(instruction, "element-eId"));

            // Komenda ma już odwzorowanie w modelu — nie zostawia ostrzeżenia o komendzie nieodwzorowanej.
            var owner = context.Document.Articles.First().Paragraphs[0].Points.Single();
            Assert.Equal(AmendmentOperationType.Modification, owner.Amendment?.OperationType);
            Assert.DoesNotContain(owner.ValidationMessages, m => m.Message.Contains("nieodwzorowana", StringComparison.Ordinal));
        }

        [Fact]
        public void DeletedWords_AreAnInstructionWithAnEmptyNewText()
        {
            // Przykład autorów schematu (DU/2026/252): „skreśla się wyrazy „X"" → stary-tekst="X", nowy-tekst="".
            var (xml, context) = Convert("1) w art. 5 w ust. 3 skreśla się wyrazy „lub ministra właściwego do spraw informatyzacji”;");

            var instruction = Assert.Single(Commands(xml));
            Assert.Equal("zamien-tekst", instruction.Name.LocalName);
            Assert.Equal("lub ministra właściwego do spraw informatyzacji", Attr(instruction, "stary-tekst"));
            Assert.Equal(string.Empty, Attr(instruction, "nowy-tekst"));
            Assert.Equal("art_5__ust_3", Attr(instruction, "element-eId"));

            var owner = context.Document.Articles.First().Paragraphs[0].Points.Single();
            Assert.DoesNotContain(owner.ValidationMessages, m => m.Message.Contains("nieodwzorowana", StringComparison.Ordinal));
        }

        [Fact]
        public void EveryTextChangeOfTheSentence_GetsItsInstruction_InTheOrderWritten()
        {
            // Przykład autorów schematu (DU/2026/26): zamiana i dodanie wyrazów w jednym zdaniu.
            var (xml, _) = Convert(
                "1) w art. 69 wyrazy „art. 63–68” zastępuje się wyrazami „art. 63–68f” oraz po wyrazach „art. 61b ust. 6” dodaje się wyrazy „i art. 68g”.");

            var instructions = Commands(xml);

            Assert.Equal(new[] { "art. 63–68", "art. 61b ust. 6" }, instructions.Select(i => Attr(i, "stary-tekst")));
            Assert.Equal(new[] { "art. 63–68f", "art. 61b ust. 6 i art. 68g" }, instructions.Select(i => Attr(i, "nowy-tekst")));
            Assert.All(instructions, i => Assert.Equal("art_69", Attr(i, "element-eId")));
            Assert.Equal(2, instructions.Select(i => Attr(i, "nowGUID")).Distinct().Count());
        }

        [Fact]
        public void DeletedWords_UsedInSeveralUnits_AreDeletedInEach_NotRepealedUnits()
        {
            // Dz. U. 2026 poz. 846 (§ 88 ZTP): miejsce użycia stoi między „skreśla się" a wyrazami — komendę brało
            // uchylenie i wychodziły uchylenia całych jednostek.
            var (xml, _) = Convert("1) skreśla się użyte w art. 299 w § 1 i 2 oraz w art. 299d wyrazy „spraw podatkowych”;");

            var instructions = Commands(xml);
            Assert.Equal(["zamien-tekst", "zamien-tekst", "zamien-tekst"], instructions.Select(i => i.Name.LocalName));
            Assert.Equal(["art_299__par_1", "art_299__par_2", "art_299d"], instructions.Select(i => Attr(i, "element-eId")));
            Assert.All(instructions, i => Assert.Equal("spraw podatkowych", Attr(i, "stary-tekst")));
            Assert.All(instructions, i => Assert.Equal(string.Empty, Attr(i, "nowy-tekst")));
            Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName == "uchyl");
        }

        [Fact]
        public void InflectedDeletedWord_UsedInSeveralUnits_IsAManualInstructionOnEach()
        {
            // Dz. U. 2026 poz. 846: „… w różnej liczbie i różnym przypadku, wyraz „sprawa”" — formy stoją tylko w akcie
            // zmienianym (ADR-0103).
            var (xml, _) = Convert(
                "1) skreśla się użyty w art. 296 w § 1 i w § 3 oraz w art. 297 w § 3, w różnej liczbie i różnym przypadku, wyraz „sprawa”;");

            Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName == "uchyl");
            var manual = xml.Descendants().Where(e => e.Name.LocalName == "cytat-w-tekscie").ToList();
            Assert.Equal(["art_296__par_1", "art_296__par_3", "art_297__par_3"], manual.Select(c => Attr(c, "element-eId")));
            Assert.All(manual, c => Assert.EndsWith("-recznie", Attr(c, "funkcja"), StringComparison.Ordinal));
        }

        [Fact]
        public void SeveralDeletedPhrases_AreSeparateInstructions()
        {
            // Korpus: „w § 6 w ust. 1 w pkt 5 skreśla się wyrazy „jarząbków," oraz „i łysek"".
            var (xml, _) = Convert("1) w art. 6 w ust. 1 w pkt 5 skreśla się wyrazy „jarząbków,” oraz „i łysek”.");

            var instructions = Commands(xml);

            Assert.Equal(new[] { "jarząbków,", "i łysek" }, instructions.Select(i => Attr(i, "stary-tekst")));
            Assert.All(instructions, i => Assert.Equal(string.Empty, Attr(i, "nowy-tekst")));
        }

        [Fact]
        public void TextChangeThatCannotBeWritten_LeavesAWarningBesideTheRecognisedOnes()
        {
            // Skreślenie jest rozpoznane; dodanie „w to miejsce" nie ma wskazanych wyrazów, przy których stoi.
            var (xml, context) = Convert(
                "1) w art. 7 po wyrazach „Dysponent Funduszu umarza” skreśla się wyraz „pożyczkę” i dodaje się wyrazy „należność z tytułu pożyczki”,");

            var instruction = Assert.Single(Commands(xml));
            Assert.Equal("pożyczkę", Attr(instruction, "stary-tekst"));

            var owner = context.Document.Articles.First().Paragraphs[0].Points.Single();
            Assert.Contains(owner.ValidationMessages, m => m.Message.Contains("nieodwzorowana", StringComparison.Ordinal));
        }

        // ============================================================
        // Dodanie w jednostce i po jednostce (ADR-0055)
        // ============================================================

        [Fact]
        public void UnitAddedWithoutAnAnchor_IsAddedInsideTheUnitNamedBeforeTheCommand()
        {
            // Przykład autorów schematu (DU/2026/26): „w art. 13 dodaje się ust. 6 w brzmieniu:" → dodaj-w, art. 13.
            var (xml, _) = Convert(
                "1) w art. 13 dodaje się ust. 6 w brzmieniu:",
                "„6. Opłatę wnosi się w terminie 14 dni.”;");

            var citation = Assert.Single(Commands(xml));
            Assert.Equal("dodaj-w", Attr(citation, "funkcja"));
            Assert.Equal("art_13", Attr(citation, "element-eId"));
            Assert.Null(Attr(citation, "element-do-eId"));

            // Jednostka cytowana ma adres w akcie zmienianym — pod jednostką, w której ją dodano.
            Assert.Equal(new[] { "art_13__ust_6" }, UnitIds(citation));
        }

        [Fact]
        public void SeveralUnitsAddedWithoutAnAnchor_AreOneCommandOnTheirContainer()
        {
            // Przykład autorów schematu (DU/2026/252): „w art. 26 dodaje się ust. 12–16" → jeden cytat, dodaj-w art. 26.
            var (xml, _) = Convert(
                "1) w art. 26 dodaje się ust. 12 i 13 w brzmieniu:",
                "„12. Pierwszy dodawany ustęp.",
                "13. Drugi dodawany ustęp.”;");

            var citation = Assert.Single(Commands(xml));
            Assert.Equal(("dodaj-w", "art_26"), (Attr(citation, "funkcja"), Attr(citation, "element-eId")));
            Assert.Equal(new[] { "art_26__ust_12", "art_26__ust_13" }, UnitIds(citation));
        }

        [Fact]
        public void UnitAddedAfterAnotherOne_KeepsThatUnitAsItsAnchor()
        {
            var (xml, _) = Convert(
                "1) w art. 13 po ust. 5 dodaje się ust. 5a w brzmieniu:",
                "„5a. Opłaty nie zwraca się.”;");

            var citation = Assert.Single(Commands(xml));
            Assert.Equal(("dodaj-po", "art_13__ust_5"), (Attr(citation, "funkcja"), Attr(citation, "element-eId")));
            Assert.Equal(new[] { "art_13__ust_5a" }, UnitIds(citation));
        }

        [Fact]
        public void UnitAddedUnderAParentCommand_IsAddedInsideTheParentsUnit()
        {
            var (xml, _) = Convert(
                "1) w art. 13:",
                "a) dodaje się ust. 6 w brzmieniu:",
                "„6. Opłatę wnosi się w terminie 14 dni.”;");

            var citation = Assert.Single(Commands(xml));
            Assert.Equal(("dodaj-w", "art_13"), (Attr(citation, "funkcja"), Attr(citation, "element-eId")));
            Assert.Equal(new[] { "art_13__ust_6" }, UnitIds(citation));
        }

        [Fact]
        public void PointAddedInAParagraph_IsAddedInsideIt()
        {
            var (xml, _) = Convert(
                "1) w art. 45 w ust. 1 dodaje się pkt 7 w brzmieniu:",
                "„7) inne podmioty.”;");

            var citation = Assert.Single(Commands(xml));
            Assert.Equal(("dodaj-w", "art_45__ust_1"), (Attr(citation, "funkcja"), Attr(citation, "element-eId")));
            Assert.Equal(new[] { "art_45__ust_1__pkt_7" }, UnitIds(citation));
        }

        // ============================================================
        // Obudowanie dotychczasowej treści ustępem albo paragrafem (ADR-0056)
        // ============================================================

        [Fact]
        public void ExistingContentMarkedAsParagraphOne_IsAnInstructionOnTheArticle()
        {
            // Przykład autorów schematu (DU/2026/26): instrukcja na art. 24, a dodawany ust. 2 — po ust. 1.
            var (xml, context) = Convert(
                "1) w art. 24 dotychczasową treść oznacza się jako ust. 1 i dodaje się ust. 2 w brzmieniu:",
                "„2. Orzeczenie lekarza orzecznika stanowi podstawę decyzji.”;");

            var commands = Commands(xml);
            Assert.Equal(new[] { "dodaj-ustep", "cytat-strukt" }, commands.Select(c => c.Name.LocalName));

            var wrap = commands[0];
            Assert.Equal(XmlConverterConstants.AmendmentNamespace, wrap.Name.NamespaceName);
            Assert.Equal("art_24", Attr(wrap, "element-eId"));
            Assert.NotNull(Attr(wrap, "akt"));
            Assert.Same(wrap, wrap.Parent!.Nodes().First(node => node is not XText { Value: "" }));

            Assert.Equal(("dodaj-po", "art_24__ust_1"), (Attr(commands[1], "funkcja"), Attr(commands[1], "element-eId")));
            Assert.Equal(new[] { "art_24__ust_2" }, UnitIds(commands[1]));

            var amendment = context.Document.Articles.First().Paragraphs[0].Points.Single().Amendment!;
            Assert.Equal(ContentWrap.Paragraph, amendment.WrapsContentAs);
            Assert.Equal("art_24", amendment.WrappedUnit?.Structure.ToString());
        }

        [Fact]
        public void ExistingContentOfACodeArticleMarkedAsSectionOne_IsTheSectionInstruction()
        {
            var (xml, _) = Convert(
                "1) w art. 5 dotychczasową treść oznacza się jako § 1 i dodaje się § 2 w brzmieniu:",
                "„§ 2. Przepisu § 1 nie stosuje się do spółek.”;");

            var wrap = Assert.Single(Commands(xml), c => c.Name.LocalName == "dodaj-paragraf");
            Assert.Equal("art_5", Attr(wrap, "element-eId"));
            Assert.DoesNotContain(Commands(xml), c => c.Name.LocalName == "dodaj-ustep");
        }

        [Fact]
        public void ExistingContentMarkedAsParagraphOne_WithoutAnotherChange_IsAnAmendmentOfItsOwn()
        {
            // Przykład autorów schematu (DU/2026/252): „w art. 50 dotychczasową treść oznacza się jako ust. 1 i:"
            // przed wyliczeniem dalszych zmian.
            var (xml, context) = Convert(
                "1) w art. 50 dotychczasową treść oznacza się jako ust. 1 i:",
                "a) wyrazy „organ właściwy” zastępuje się wyrazami „organ nadzoru”,",
                "b) dodaje się ust. 2 w brzmieniu:",
                "„2. Organ nadzoru prowadzi rejestr.”;");

            var wrap = Assert.Single(Commands(xml), c => c.Name.LocalName == "dodaj-ustep");
            Assert.Equal("art_50", Attr(wrap, "element-eId"));

            var point = context.Document.Articles.First().Paragraphs[0].Points.Single();
            Assert.Equal(ContentWrap.Paragraph, point.Amendment?.WrapsContentAs);
            Assert.DoesNotContain(point.ValidationMessages, m => m.Message.Contains("nie zawiera jednostek", StringComparison.Ordinal));

            // Zmiany wyliczone pod spodem dotyczą już ustępu 1.
            var replaced = Assert.Single(Commands(xml), c => c.Name.LocalName == "zamien-tekst");
            Assert.Equal("art_50__ust_1", Attr(replaced, "element-eId"));
        }

        [Fact]
        public void ProvisionThatWrapsNothing_HasNoWrapInstruction()
        {
            var (xml, context) = Convert(
                "1) w art. 24 dodaje się ust. 2 w brzmieniu:",
                "„2. Orzeczenie lekarza orzecznika stanowi podstawę decyzji.”;");

            Assert.DoesNotContain(Commands(xml), c => c.Name.LocalName is "dodaj-ustep" or "dodaj-paragraf");
            Assert.Equal(ContentWrap.None, context.Document.Articles.First().Paragraphs[0].Points.Single().Amendment!.WrapsContentAs);
        }

        // ============================================================
        // Zdanie przytoczone w całości (ADR-0057, ADR-0124)
        // ============================================================

        [Theory]
        // Przykład autorów schematu (DU/2026/26): „w art. 126 zdanie pierwsze otrzymuje brzmienie:".
        [InlineData("1) w art. 126 zdanie pierwsze otrzymuje brzmienie:")]
        [InlineData("1) w art. 126 w ust. 2 dodaje się zdanie drugie w brzmieniu:")]
        [InlineData("1) w art. 126 po zdaniu pierwszym dodaje się zdanie w brzmieniu:")]
        public void QuotedSentence_IsExportedAsASentence(string command)
        {
            var (xml, context) = Convert(command, "„Osoba, która złożyła wniosek, jest obowiązana poddać się badaniom.”;");

            var citation = Assert.Single(Commands(xml));
            Assert.Equal("cytat-w-tekscie", citation.Name.LocalName);

            var sentence = Assert.Single(citation.Elements());
            Assert.Equal("zdanie", sentence.Name.LocalName);
            Assert.Equal("1", Attr(sentence, "nr"));
            Assert.Equal("Osoba, która złożyła wniosek, jest obowiązana poddać się badaniom.", sentence.Value);

            Assert.True(context.Document.Articles.First().Paragraphs[0].Points.Single().Amendment!.Content!.PlainTextIsSentences);
        }

        [Fact]
        public void SeveralQuotedSentences_AreNumberedInOrder()
        {
            var (xml, _) = Convert(
                "1) w art. 5 w ust. 3 zdania pierwsze i drugie otrzymują brzmienie:",
                "„Opłatę wnosi się w terminie 14 dni. Opłaty nie zwraca się.”;");

            var sentences = Assert.Single(Commands(xml)).Elements().ToList();
            Assert.Equal(new[] { "zdanie", "zdanie" }, sentences.Select(s => s.Name.LocalName));
            Assert.Equal(new[] { "1", "2" }, sentences.Select(s => Attr(s, "nr")));
            Assert.Equal(new[] { "Opłatę wnosi się w terminie 14 dni.", "Opłaty nie zwraca się." }, sentences.Select(s => s.Value.Trim()));
        }

        [Theory]
        // Wprowadzenie do wyliczenia to cały fragment zdania: zdanie nr 1, fragment 1 (DU/2026/26 art. 11).
        [InlineData("1) w art. 5 wprowadzenie do wyliczenia otrzymuje brzmienie:", "art_5")]
        // „W zdaniu pierwszym" wskazuje miejsce (zdanie, ADR-0102); przedmiotem jest wprowadzenie.
        [InlineData("1) w art. 5 w zdaniu pierwszym wprowadzenie do wyliczenia otrzymuje brzmienie:", "art_5__zdn_1")]
        // Korpus: 326224_2994150 — komenda bez dwukropka.
        [InlineData("1) w art. 5 wprowadzenie do wyliczenia otrzymuje brzmienie", "art_5")]
        public void QuotedIntroduction_IsExportedAsAFragmentOfASentence(string command, string target)
        {
            var (xml, context) = Convert(command, "„Organ właściwy wydaje decyzję w sprawie:”;");

            var citation = Assert.Single(Commands(xml), c => c.Name.LocalName == "cytat-w-tekscie");
            var sentence = Assert.Single(citation.Elements());
            Assert.Equal("zdanie", sentence.Name.LocalName);
            Assert.Equal(("1", "1"), (Attr(sentence, "nr"), Attr(sentence, "fragment")));
            Assert.Equal("Organ właściwy wydaje decyzję w sprawie:", sentence.Value);
            Assert.Equal(target, Attr(citation, "element-eId"));

            var content = context.Document.Articles.First().Paragraphs[0].Points.Single().Amendment!.Content!;
            Assert.True(content.PlainTextIsSentences);
            Assert.True(content.PlainTextIsIntroduction);
        }

        [Fact]
        public void IntroductionChangedWithItsPoints_IsAddressedToTheUnitWhoseEnumerationItOpens()
        {
            // Korpus: 308795_2759226 — cel komendy wskazuje punkt, a wprowadzenie należy do ustępu nad nim (ADR-0124).
            var (xml, _) = Convert(
                "1) w art. 103 w ust. 1 wprowadzenie do wyliczenia i pkt 1 otrzymują brzmienie:",
                "„Przepisy ustawy stosuje się do:",
                "1) pracodawców;”;");

            var commands = Commands(xml);
            var introduction = Assert.Single(commands, c => c.Name.LocalName == "cytat-w-tekscie");
            Assert.Equal("art_103__ust_1", Attr(introduction, "element-eId"));
            Assert.Equal("zdanie", Assert.Single(introduction.Elements()).Name.LocalName);
            var point = Assert.Single(commands, c => c.Name.LocalName == "cytat-strukt");
            Assert.Equal("art_103__ust_1__pkt_1", Attr(point, "element-eId"));
        }

        [Theory]
        // Tytuł zdaniem nie jest.
        [InlineData("1) tytuł rozdziału 3 otrzymuje brzmienie:", "„Obowiązki podmiotów kluczowych”;")]
        public void QuotedTextThatIsNotASentence_StaysAFragment(string command, string quoted)
        {
            var (xml, _) = Convert(command, quoted);

            var citation = Assert.Single(Commands(xml), c => c.Name.LocalName == "cytat-w-tekscie");
            Assert.Equal("tekst", Assert.Single(citation.Elements()).Name.LocalName);
        }

        // ============================================================
        // Zamiana znaku kończącego jednostkę (ADR-0054)
        // ============================================================

        [Fact]
        public void SignReplacedBeforeAnAddedUnit_IsAnInstructionOnTheUnitBeforeTheSign()
        {
            // Przykład autorów schematu (DU/2026/26): „w art. 4 w pkt 20 kropkę zastępuje się średnikiem i dodaje
            // się pkt 21 w brzmieniu:" — zamiana ostatniej kropki pkt 20 i dodanie pkt 21 po pkt 20.
            var (xml, context) = Convert(
                "1) w art. 4 w pkt 20 kropkę zastępuje się średnikiem i dodaje się pkt 21 w brzmieniu:",
                "„21) osoba wykonująca zawód medyczny.”;");

            var commands = Commands(xml);
            Assert.Equal(new[] { "zamien-tekst", "cytat-strukt" }, commands.Select(c => c.Name.LocalName));

            var sign = commands[0];
            Assert.Equal(".", Attr(sign, "stary-tekst"));
            Assert.Equal(";", Attr(sign, "nowy-tekst"));
            Assert.Equal("art_4__pkt_20", Attr(sign, "element-eId"));
            Assert.Equal("ostatnie", Attr(sign, "wystapienie"));
            Assert.Equal(XmlConverterConstants.AmendmentNamespace, sign.Attributes().Single(a => a.Name.LocalName == "wystapienie").Name.NamespaceName);

            Assert.Equal("art_4__pkt_20", Attr(commands[1], "element-eId"));
            Assert.Equal("dodaj-po", Attr(commands[1], "funkcja"));
            Assert.NotEqual(Attr(sign, "nowGUID"), Attr(commands[1], "nowGUID"));

            // Zmiana w tekście siedzi w nowelizacji tego przepisu, z własnym celem.
            var amendment = context.Document.Articles.First().Paragraphs[0].Points.Single().Amendment!;
            var change = Assert.Single(amendment.TextChanges);
            Assert.Equal(TextOccurrence.Last, change.Occurrence);
            Assert.Equal("art_4__pkt_20", Assert.Single(change.Targets).Structure.ToString());
        }

        [Theory]
        // Korpus: 308795_2759226, 310708_2773514, 320206_2951141. Znak zmienia się w jednostce sprzed komendy,
        // a uchylana jest jednostka nazwana po niej — przedtem uchylenie wskazywało jednostkę, w której zmienia
        // się znak.
        [InlineData("1) w art. 44 w ust. 4 w pkt 4 średnik zastępuje się kropką i uchyla się pkt 5;", "art_44__ust_4__pkt_4", "art_44__ust_4__pkt_5", null)]
        [InlineData("1) w art. 131 w ust. 2 w pkt 1 średnik zastępuje się kropką i uchyla się pkt 2–7;", "art_131__ust_2__pkt_1", "art_131__ust_2__pkt_2", "art_131__ust_2__pkt_7")]
        [InlineData("1) w art. 4 w pkt 1 w lit. g przecinek zastępuje się średnikiem i uchyla się lit. h;", "art_4__pkt_1__lit_g", "art_4__pkt_1__lit_h", null)]
        public void RepealAfterAReplacedSign_NamesTheUnitAfterTheCommand(string command, string signUnit, string repealed, string? repealedTo)
        {
            var (xml, _) = Convert(command);

            var sign = Assert.Single(Commands(xml), c => c.Name.LocalName == "zamien-tekst");
            Assert.Equal(signUnit, Attr(sign, "element-eId"));
            Assert.Equal("ostatnie", Attr(sign, "wystapienie"));

            var repeal = Assert.Single(Commands(xml), c => c.Name.LocalName == "uchyl");
            Assert.Equal(repealed, Attr(repeal, "element-eId"));
            Assert.Equal(repealedTo, Attr(repeal, "element-do-eId"));
        }

        [Fact]
        public void RepealedSentence_IsAddressedWithItsSentence_NotAsItsWholeUnit()
        {
            // Korpus: 308795_2759226 — przedtem `now:uchyl` wskazywał cały ust. 2.
            var (xml, _) = Convert("1) w art. 76 w ust. 2 skreśla się zdanie drugie;");

            var repeal = Assert.Single(Commands(xml));
            Assert.Equal(("uchyl", "art_76__ust_2__zdn_2"), (repeal.Name.LocalName, Attr(repeal, "element-eId")));
        }

        [Fact]
        public void RangeOfRepealedSentences_HasBothEnds()
        {
            var (xml, _) = Convert("1) w art. 76 w ust. 2 uchyla się zdania drugie–czwarte;");

            var repeal = Assert.Single(Commands(xml));
            Assert.Equal(("art_76__ust_2__zdn_2", "art_76__ust_2__zdn_4"), (Attr(repeal, "element-eId"), Attr(repeal, "element-do-eId")));
        }

        [Fact]
        public void DeletedSignWithWords_IsATextChange_NotARepealOfTheUnit()
        {
            // Korpus: 314558_2800589 — przedtem całe zdanie wychodziło jako uchylenie art. 16.
            var (xml, _) = Convert("1) w art. 16 skreśla się przecinek i wyrazy „o której mowa w art. 11 ust. 2 pkt 8”;");

            var change = Assert.Single(Commands(xml));
            Assert.Equal("zamien-tekst", change.Name.LocalName);
            Assert.Equal((", o której mowa w art. 11 ust. 2 pkt 8", "", "art_16"),
                (Attr(change, "stary-tekst"), Attr(change, "nowy-tekst"), Attr(change, "element-eId")));
        }

        [Fact]
        public void RepealAfterADeletedSign_NamesTheUnitAfterTheCommand()
        {
            // Korpus: 310239_2768075 — znak znika w pkt 8, uchylane są pkt 9–11.
            var (xml, _) = Convert("1) w art. 14f w ust. 1 w pkt 8 skreśla się przecinek i uchyla się pkt 9–11;");

            var sign = Assert.Single(Commands(xml), c => c.Name.LocalName == "zamien-tekst");
            Assert.Equal((",", "", "art_14f__ust_1__pkt_8", "ostatnie"),
                (Attr(sign, "stary-tekst"), Attr(sign, "nowy-tekst"), Attr(sign, "element-eId"), Attr(sign, "wystapienie")));

            var repeal = Assert.Single(Commands(xml), c => c.Name.LocalName == "uchyl");
            Assert.Equal(("art_14f__ust_1__pkt_9", "art_14f__ust_1__pkt_11"), (Attr(repeal, "element-eId"), Attr(repeal, "element-do-eId")));
        }

        [Fact]
        public void ContentAddedAfterADeletedSign_IsNotLost()
        {
            // Korpus: 358922_3205674 — przedtem zdanie było uchyleniem lit. c, a cytowana część wspólna ginęła.
            var (xml, _) = Convert(
                "1) w art. 7 w ust. 2 w pkt 2 w lit. c skreśla się średnik i dodaje się część wspólną w brzmieniu:",
                "„– w przypadku gdy wyposażanie pojazdu miało miejsce na terytorium Rzeczypospolitej Polskiej;”;");

            Assert.DoesNotContain(Commands(xml), c => c.Name.LocalName == "uchyl");
            var sign = Assert.Single(Commands(xml), c => c.Name.LocalName == "zamien-tekst");
            Assert.Equal((";", "", "ostatnie"), (Attr(sign, "stary-tekst"), Attr(sign, "nowy-tekst"), Attr(sign, "wystapienie")));
            Assert.Contains("w przypadku gdy wyposażanie pojazdu miało miejsce na terytorium Rzeczypospolitej Polskiej", xml.Root!.Value);
        }

        [Fact]
        public void CommonPartOfPoints_NamedAfterAPoint_StandsUnderTheUnitHoldingThePoints()
        {
            // Korpus: 360425_3190283 — „w ust. 1 pkt 2 i część wspólna otrzymują brzmienie:"; część wspólna punktów
            // należy do ustępu, nie do punktu 2.
            var amendment = new Amendment
            {
                Targets = { new StructuralAmendmentReference { Structure = new StructuralReference { Article = "12", Paragraph = "1", Point = "2" } } },
                Content = new AmendmentContent
                {
                    Points = { new Saga.Model.EditorialUnits.Point { Number = new EntityNumber { Value = "2" } } },
                    CommonParts = { new Saga.Model.EditorialUnits.CommonPart { Type = CommonPartType.WrapUp, ListItemType = UnitType.Point } },
                },
            };

            AmendmentTargetPairing.AnchorContentUnits(amendment);

            Assert.Equal("art_12__ust_1", amendment.Content.CommonParts[0].AddressAnchor);
            Assert.Equal("art_12__ust_1", amendment.Content.Points[0].AddressAnchor);
        }

        [Fact]
        public void SignReplacedOnItsOwn_IsATextChangeOfTheUnit()
        {
            var (xml, _) = Convert("1) w art. 12 w pkt 2 kropkę zastępuje się średnikiem;");

            var sign = Assert.Single(Commands(xml));
            Assert.Equal((".", ";", "art_12__pkt_2", "ostatnie"),
                (Attr(sign, "stary-tekst"), Attr(sign, "nowy-tekst"), Attr(sign, "element-eId"), Attr(sign, "wystapienie")));
        }

        [Fact]
        public void WordsAddedAfterTheNewSign_AreOneChangeWithIt()
        {
            // Korpus: 318167_2965687 — „kropkę na końcu zastępuje się przecinkiem i dodaje się wyrazy „z tym że …"".
            var (xml, _) = Convert("1) w art. 9 w ust. 1 w pkt 3 kropkę na końcu zastępuje się przecinkiem i dodaje się wyrazy „z tym że opłaty nie pobiera się”;");

            var sign = Assert.Single(Commands(xml));
            Assert.Equal(".", Attr(sign, "stary-tekst"));
            Assert.Equal(", z tym że opłaty nie pobiera się", Attr(sign, "nowy-tekst"));
        }

        [Fact]
        public void ReplacedWords_CarryNoSpaceFromInsideTheQuotationMarks()
        {
            // Korpus: 361913_3209300 — odstęp za cudzysłowem otwierającym oddziela go od przecinka w składzie.
            var (xml, _) = Convert("1) w art. 5 wyrazy „ , o którym mowa w ust. 4” zastępuje się wyrazami „ kluczowym”;");

            var instruction = Assert.Single(Commands(xml));
            Assert.Equal(", o którym mowa w ust. 4", Attr(instruction, "stary-tekst"));
            Assert.Equal("kluczowym", Attr(instruction, "nowy-tekst"));
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
