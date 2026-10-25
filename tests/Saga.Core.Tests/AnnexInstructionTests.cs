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
        public void ReplacedAnnex_WhoseSourceHasNoNumber_IsReplacedByTheOnlyAnnexOfTheAmendingAct()
        {
            Assert.Equal(
                [(false, "2", "1", null)],
                Read("załącznik nr 2 do rozporządzenia otrzymuje brzmienie określone w załączniku do niniejszego rozporządzenia."));
        }

        [Fact]
        public void ReplacedAnnexWithoutANumber_IsTheOnlyAnnex_OnBothSides()
        {
            // Korpus: 28 komend „załącznik do rozporządzenia otrzymuje brzmienie określone w załączniku do niniejszego
            // rozporządzenia" — według autorów schematu (2026-10-18) jedyny załącznik to zal_1 (ADR-0099).
            Assert.Equal(
                [(false, "1", "1", null)],
                Read("załącznik do rozporządzenia otrzymuje brzmienie określone w załączniku do niniejszego rozporządzenia."));
        }

        [Theory]
        // Miejsce wskazane wprost.
        [InlineData("po załączniku nr 2 do rozporządzenia dodaje się załączniki nr 2a i 2b w brzmieniu określonym w załącznikach nr 3 i 4 do niniejszego rozporządzenia;", "2", "3", "4")]
        // Miejsce wynika z kolejności numerów: „nr 3" staje po „nr 2", „nr 2a" po „nr 2".
        [InlineData("dodaje się załączniki nr 3 i 4 do ustawy w brzmieniu określonym w załącznikach nr 3 i 4 do niniejszej ustawy.", "2", "3", "4")]
        // Załącznik aktu zmieniającego bez numeru jest jego jedynym załącznikiem — „1" (ADR-0099).
        [InlineData("dodaje się załącznik nr 4 do ustawy w brzmieniu określonym w załączniku do niniejszej ustawy.", "3", "1", null)]
        [InlineData("dodaje się załącznik nr 2a do rozporządzenia w brzmieniu określonym w załączniku nr 1 do niniejszego rozporządzenia.", "2", "1", null)]
        // Pierwszy załącznik aktu nie ma poprzednika.
        [InlineData("dodaje się załącznik nr 1 do ustawy w brzmieniu określonym w załączniku do niniejszej ustawy.", null, "1", null)]
        public void AddedAnnexes_AreOneInstructionAfterTheAnnexBeforeThem(string text, string? after, string? source, string? sourceTo)
        {
            Assert.Equal([(true, after, source, sourceTo)], Read(text));
        }

        [Theory]
        // Brzmienie przytoczone, nie odesłanie.
        [InlineData("załącznik nr 1 do ustawy otrzymuje brzmienie:")]
        [InlineData("w załączniku nr 4 do rozporządzenia tabela D otrzymuje brzmienie:")]
        public void OtherCommands_AreNotAnnexInstructions(string text)
        {
            Assert.Empty(AnnexInstructionReader.Read(text));
        }

        [Theory]
        // Korpus: 310486_2768334, 351540_3121851 — wyraz przeniesiony w źródle ręcznie.
        [InlineData("załącznik do rozporządzenia otrzymuje brzmienie okreś- lone w załączniku do niniejszego rozporządzenia.")]
        [InlineData("załącznik do rozporządzenia otrzymuje brzmienie okreś-lone w załączniku do niniejszego rozporządzenia.")]
        public void ReplacedAnnex_WithTheWordSplitByHand_IsStillAnInstruction(string text)
        {
            Assert.Equal([(false, "1", "1", null)], Read(text));
        }

        // ============================================================
        // Część załącznika zmieniana odesłaniem (ADR-0108)
        // ============================================================

        private static (bool Adds, string? Target, string? Part, string? Source)[] ReadParts(string text) =>
            AnnexInstructionReader.Read(text).Select(i => (i.Adds, i.Target, i.Part, i.Source)).ToArray();

        [Theory]
        // Korpus: 295470_2750296, 286890_2707650.
        [InlineData("w załączniku nr 4 do rozporządzenia tabela D otrzymuje brzmienie określone w załączniku do niniejszego rozporządzenia.", "4", "tabela D", "1")]
        [InlineData("w załączniku nr 3 do rozporządzenia w tabeli VI część I. Urzędy morskie otrzymuje brzmienie określone w załączniku nr 2 do niniejszego rozporządzenia;", "3", "w tabeli VI część I. Urzędy morskie", "2")]
        // Korpus: 307572_2743131 — przecinek zamykający wtrącenie nie należy do części.
        [InlineData("w załączniku nr 14 do ustawy „Plany finansowe państwowych osób prawnych” plan finansowy Polskiej Organizacji Turystycznej, ustalony w tabeli 14, otrzymuje brzmienie określone w załączniku nr 9 do niniejszej ustawy.", "14", "plan finansowy Polskiej Organizacji Turystycznej, ustalony w tabeli 14", "9")]
        // Korpus: 335318_2975891 — wyraz przeniesiony w źródle ręcznie.
        [InlineData("w załączniku nr 1 do rozporządzenia tabela nr 1 otrzymuje brzmienie okreś-lone w załączniku do niniejszego rozporządzenia.", "1", "tabela nr 1", "1")]
        // M.P. 2026 poz. 912: załącznik Programu, który sam jest załącznikiem uchwały zmienianej — część jej jedynego załącznika.
        [InlineData("załącznik nr 1 do Programu „Wydatki z budżetu państwa na lata 2024–2030” otrzymuje brzmienie określone w załączniku do niniejszej uchwały.", "1", "załącznik nr 1 do Programu „Wydatki z budżetu państwa na lata 2024–2030”", "1")]
        public void PartOfAnAnnexReplacedByReference_IsAnInstructionOnThatAnnex(string text, string target, string part, string source)
        {
            Assert.Equal([(false, target, part, source)], ReadParts(text));
        }

        [Theory]
        // Korpus: 346994_3160600 — tytuł załącznika w cudzysłowie między numerem a komendą.
        [InlineData("w załączniku nr 4 „Wykaz świadczeń gwarantowanych z zakresu leczenia szpitalnego” dodaje się lp. 73 w brzmieniu określonym w załączniku do niniejszego rozporządzenia.", "4", "lp. 73")]
        // Załącznik bez numeru to jedyny — zal_1 (ADR-0099).
        [InlineData("w załączniku po lp. 71 dodaje się lp. 72 w brzmieniu określonym w załączniku do niniejszego rozporządzenia.", "1", "lp. 72")]
        public void PartOfAnAnnexAddedByReference_IsAnInstructionOnThatAnnex(string text, string target, string part)
        {
            Assert.Equal([(true, target, part, "1")], ReadParts(text));
        }

        [Fact]
        public void PartNamedUnderAPlaceInAnAnnex_IsAnInstructionOnTheEnclosingAnnex()
        {
            // M.P. 2026 poz. 343: „2) w załączniku do uchwały:" / „a) rozdział I „Informacje ogólne” otrzymuje brzmienie
            // określone w załączniku nr 1 do niniejszej uchwały" — załącznik nazywa przepis nadrzędny.
            const string replaced = "rozdział I „Informacje ogólne” otrzymuje brzmienie określone w załączniku nr 1 do niniejszej uchwały,";
            const string added = "po podrozdziale III.5. „Geriatria” dodaje się podrozdział III.5a. „Centrum Zdrowia 75+” w brzmieniu określonym w załączniku nr 3 do niniejszej uchwały,";

            Assert.Equal([(false, "1", "rozdział I „Informacje ogólne”", "1")],
                AnnexInstructionReader.ReadPartUnderAnAnnex(replaced, enclosingAnnex: "1").Select(i => (i.Adds, i.Target, i.Part, i.Source)));
            Assert.Equal([(true, "13", "podrozdział III.5a. „Centrum Zdrowia 75+”", "3")],
                AnnexInstructionReader.ReadPartUnderAnAnnex(added, enclosingAnnex: "13").Select(i => (i.Adds, i.Target, i.Part, i.Source)));

            // Bez miejsca w załączniku część nie wskazuje żadnego załącznika aktu zmienianego.
            Assert.Empty(AnnexInstructionReader.Read(replaced));
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
        public void PartOfAnAnnexChangedByReference_IsExportedAsAManualInstructionOnThatAnnex()
        {
            var xml = Convert(
                "Art. 1. W ustawie z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) w załączniku nr 4 do ustawy tabela D otrzymuje brzmienie określone w załączniku do niniejszej ustawy.");

            // Części nie zaadresuje eId — instrukcja ręczna na załączniku, z częścią nazwaną tak jak w przepisie.
            var citation = Assert.Single(xml.Descendants(), e => e.Name.LocalName == "cytat-w-tekscie");
            Assert.Equal("DU/2021/200__zal_4", Attr(citation, "element-eId"));
            Assert.Equal("zastap-recznie", Attr(citation, "funkcja"));
            Assert.Equal("brak", Attr(citation, "cudzyslowy"));
            Assert.Equal("tabela D", citation.Value);
            Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName == "zastap");
        }

        [Fact]
        public void PartChangedByReference_UnderAPlaceInAnAnnex_IsAManualInstructionOnThatAnnex()
        {
            // Korpus 307572_2743131: „10) w załączniku nr 13 do ustawy „Plany finansowe państwowych funduszy celowych”:" /
            // „a) plan finansowy …, ustalony w tabeli 9, otrzymuje brzmienie określone w załączniku nr 7 do niniejszej ustawy,".
            var xml = Convert(
                "Art. 1. W ustawie z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) wprowadza się następujące zmiany:",
                "1) w załączniku nr 13 do ustawy „Plany finansowe państwowych funduszy celowych”:",
                "a) plan finansowy Funduszu Cyberbezpieczeństwa, ustalony w tabeli 9, otrzymuje brzmienie określone w załączniku nr 7 do niniejszej ustawy,",
                "b) po planie finansowym Funduszu Pracy dodaje się plan finansowy Funduszu Solidarnościowego w brzmieniu określonym w załączniku nr 8 do niniejszej ustawy;",
                "2) w załączniku nr 14 do ustawy uchyla się tabelę 3.");

            var citations = xml.Descendants().Where(e => e.Name.LocalName == "cytat-w-tekscie").ToList();
            Assert.Equal(["zastap-recznie", "dodaj-recznie"], citations.Take(2).Select(c => Attr(c, "funkcja")));
            Assert.All(citations.Take(2), c => Assert.Equal("DU/2021/200__zal_13", Attr(c, "element-eId")));
            Assert.Equal("plan finansowy Funduszu Cyberbezpieczeństwa, ustalony w tabeli 9", citations[0].Value);
            Assert.Equal("plan finansowy Funduszu Solidarnościowego", citations[1].Value);
        }

        [Fact]
        public void PartChangedByReference_UnderPlacesInAnAnnex_StartsWithThosePlaces()
        {
            // Korpus 326724_2995341: „3) w załączniku nr 3 do rozporządzenia:" / „a) w tabeli XII:" / „– część I. …" i znów
            // „b) w tabeli XIII:" / „– część I. …" — bez tabeli obie części brzmiałyby tak samo.
            var xml = Convert(
                "Art. 1. W ustawie z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) wprowadza się następujące zmiany:",
                "1) w załączniku nr 3 do ustawy:",
                "a) w tabeli XII:",
                "– część I. Wszystkie urzędy otrzymuje brzmienie określone w załączniku nr 2 do niniejszej ustawy,",
                "b) w tabeli XIII:",
                "– część I. Wszystkie urzędy otrzymuje brzmienie określone w załączniku nr 5 do niniejszej ustawy,",
                "c) tabela XV otrzymuje brzmienie określone w załączniku nr 7 do niniejszej ustawy;",
                "2) w załączniku nr 4 do ustawy w tabeli B:",
                "a) część A otrzymuje brzmienie określone w załączniku nr 8 do niniejszej ustawy.");

            var citations = xml.Descendants().Where(e => e.Name.LocalName == "cytat-w-tekscie").ToList();
            Assert.Equal(
                ["w tabeli XII część I. Wszystkie urzędy", "w tabeli XIII część I. Wszystkie urzędy", "tabela XV", "w tabeli B część A"],
                citations.Select(c => c.Value));
            Assert.Equal(["DU/2021/200__zal_3", "DU/2021/200__zal_3", "DU/2021/200__zal_3", "DU/2021/200__zal_4"],
                citations.Select(c => Attr(c, "element-eId")));
        }

        [Theory]
        [InlineData("w tabeli XII:", "w tabeli XII", false)]
        [InlineData("w rozdziale III „Wsparcie rozwoju infrastruktury”:", "w rozdziale III „Wsparcie rozwoju infrastruktury”", false)]
        [InlineData("w załączniku nr 3 do rozporządzenia:", null, true)]
        [InlineData("w załączniku nr 4 do rozporządzenia w tabeli B:", "w tabeli B", true)]
        [InlineData("w załączniku do uchwały wprowadza się następujące zmiany:", null, true)]
        [InlineData("w ustawie z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) wprowadza się następujące zmiany:", null, false)]
        [InlineData("w art. 5 ust. 2 otrzymuje brzmienie:", null, false)]
        public void PlaceOfAnIntroduction_IsWhatTheParentProvisionNames(string text, string? place, bool namesTheAnnex)
        {
            Assert.Equal(place, AnnexInstructionReader.PlaceOfAnIntroduction(text, out var names));
            Assert.Equal(namesTheAnnex, names);
        }

        [SchemaPlFact]
        public void PartOfAnAnnexChangedByReference_IsSchemaValid_InItsBody()
        {
            var xml = Convert(
                "Art. 1. W ustawie z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) wprowadza się następujące zmiany:",
                "1) w załączniku nr 4 do ustawy tabela D otrzymuje brzmienie określone w załączniku nr 1 do niniejszej ustawy;",
                "2) w załączniku nr 5 do ustawy po lp. 3 dodaje się lp. 3a w brzmieniu określonym w załączniku nr 2 do niniejszej ustawy.");

            Assert.Equal(["zastap-recznie", "dodaj-recznie"],
                xml.Descendants().Where(e => e.Name.LocalName == "cytat-w-tekscie").Select(e => Attr(e, "funkcja")));
            Assert.Empty(SchemaPl.Validator.ValidateActBody(xml.Elements().First()));
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
            Assert.Equal((false, "1", (string?)"1"), (annex.Adds, annex.Target, annex.Source));
            Assert.DoesNotContain(provision.ValidationMessages, m => m.Message.Contains("bez wykrytego celu", StringComparison.Ordinal));
        }
    }
}
