using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;

namespace Saga.Core.Tests
{
    /// <summary>Cały eksport aktu wobec schemaPL.xsd — z nagłówkiem, nie tylko treść.</summary>
    public class ActSchemaTests
    {
        private static XElement Export() => Export(
            "USTAWA", "z dnia 14 czerwca 2024 r.", "o ochronie sygnalistów",
            "Rozdział 1", "Przepisy ogólne",
            "Art. 1. Ustawa określa zasady ochrony sygnalistów.",
            "Art. 2. Ustawa wchodzi w życie po upływie 3 miesięcy od dnia ogłoszenia.");

        private static XElement Export(params string[] lines)
        {
            var blocks = new List<DocumentBlock>
            {
                new() { Text = "DZIENNIK USTAW RZECZYPOSPOLITEJ POLSKIEJ", Role = BlockRole.JournalHeader },
                new() { Text = "Warszawa, dnia 24 czerwca 2024 r.", Role = BlockRole.JournalHeader },
                new() { Text = "Poz. 928", Role = BlockRole.JournalHeader },
            };
            blocks.AddRange(lines.Select(text => new DocumentBlock { Text = text }));

            var document = LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document;
            return LegalDocumentConverter.Convert(document!);
        }

        private static IReadOnlyList<string> Problems(XElement xml) => SchemaPl.Validator.Validate(xml);

        [SchemaPlFact]
        public void ActWithJournalHeader_IsSchemaValid_AsAWhole()
        {
            Assert.Empty(Problems(Export()));
        }

        [SchemaPlFact]
        public void AmendingAct_IsSchemaValid_AsAWhole()
        {
            var xml = Export(
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o zmianie ustawy o systemie ubezpieczeń społecznych",
                "Art. 1. W ustawie z dnia 13 października 1998 r. o systemie ubezpieczeń społecznych (Dz. U. z 2025 r. poz. 350) wprowadza się następujące zmiany:",
                "1) w art. 68 ust. 1 pkt 5 otrzymuje brzmienie:",
                "„5) orzekanie o niezdolności do pracy;”;",
                "2) w art. 69 wyrazy „organ rentowy” zastępuje się wyrazami „Zakład”;",
                "3) uchyla się art. 70;",
                "4) po rozdziale 9 dodaje się rozdział 9a w brzmieniu:",
                "„Rozdział 9a",
                "Orzekanie dla celów świadczeń",
                "Art. 85a. Zadania realizuje Zakład.”.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

            Assert.Empty(Problems(xml));
        }

        private static XElement ExportWithExceptionsAndRetroactivity() => Export(
            "USTAWA", "z dnia 14 czerwca 2024 r.", "o zmianie ustawy o systemie ubezpieczeń społecznych",
            "Art. 1. W ustawie z dnia 13 października 1998 r. o systemie ubezpieczeń społecznych (Dz. U. z 2025 r. poz. 350) wprowadza się następujące zmiany:",
            "1) w art. 68 ust. 1 pkt 5 otrzymuje brzmienie:",
            "„5) orzekanie o niezdolności do pracy;”;",
            "2) po art. 85 dodaje się art. 85a w brzmieniu:",
            "„Art. 85a. Zadania realizuje Zakład.”.",
            "Art. 2. Przepis przejściowy.",
            "Art. 3. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia, z mocą od dnia 1 stycznia 2024 r., z wyjątkiem:",
            "1) art. 1 pkt 2 w zakresie art. 85a, który wchodzi w życie z dniem 1 stycznia 2027 r.;",
            "2) art. 2, który wchodzi w życie z dniem następującym po dniu ogłoszenia.");

        [SchemaPlFact]
        public void ActWithExceptionsAndRetroactivity_IsSchemaValid_AsAWhole()
        {
            var xml = ExportWithExceptionsAndRetroactivity();

            Assert.Empty(Problems(xml));
            Assert.Equal(4, xml.Descendants().Count(e => e.Name.LocalName == "Zdarzenie"));
        }

        // ============================================================
        // Jednostki spoza ZTP
        // ============================================================

        [SchemaPlFact]
        public void SingleItemEnumerations_AreNonZtpUnits_AndSchemaValid()
        {
            // Wyliczenie z jedną pozycją odbiega od ZTP — schemat ma na to elementy „nie-ztp-*".
            var xml = Export(
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o ochronie sygnalistów",
                "Art. 1. 1. Ustawa określa:",
                "1) zasady ochrony.",
                "2. Przepisy stosuje się do:",
                "1) pracodawców:",
                "a) publicznych;",
                "2) organów:",
                "a) centralnych:",
                "– naczelnych,",
                "b) terenowych.",
                "Art. 2. Ustawa wchodzi w życie po upływie 3 miesięcy od dnia ogłoszenia.");

            var names = xml.Descendants().Select(e => e.Name.LocalName).ToList();
            Assert.Contains("nie-ztp-ustep", names);
            Assert.Contains("nie-ztp-punkt", names);
            Assert.Contains("nie-ztp-litera", names);
            Assert.Contains("nie-ztp-tiret", names);
            Assert.DoesNotContain(names, name => name.StartsWith("pato-"));

            Assert.Empty(Problems(xml));
        }

        // ============================================================
        // Grupy czasowe i atrybut „okres" (ADR-0022)
        // ============================================================

        private static string? Attr(XElement element, string name) => element.Attribute(name)?.Value;

        private static List<XElement> Groups(XElement xml) =>
            xml.Descendants().Where(e => e.Name.LocalName == "GrupaCzasowa").ToList();

        [Fact]
        public void EveryDatedElement_PointsAtTheGroupOfTheGeneralRule_NamedLikeInTheAuthorsExample()
        {
            // Ten sam akt co przykład autorów schematu (DU/2024/928): ogłoszony 24 czerwca 2024 r.,
            // wchodzi w życie po upływie 3 miesięcy — identyfikator grupy co do znaku jak w przykładzie.
            const string group = "okr_obowiazywanie.zdarz_wejscie_2024-09-25_DU-2024-928_domyslne.x";
            var xml = Export();

            var dated = xml.Descendants().Where(e => Attr(e, "wejscie-w-zycie") is not null).ToList();
            Assert.NotEmpty(dated);
            Assert.All(dated, e => Assert.Equal(group, Attr(e, "okres")));

            var only = Assert.Single(Groups(xml));
            Assert.Equal(group, Attr(only, "id"));
            Assert.Equal("true", Attr(only, "glowna"));
            Assert.Equal(new[] { "obowiazywanie" }, only.Elements().Select(i => Attr(i, "rodzaj")));
            Assert.Equal("zdarz_wejscie_2024-09-25_DU-2024-928_domyslne", Attr(only.Elements().Single(), "od"));
        }

        [Fact]
        public void Exceptions_GetTheirOwnGroups_AndRetroactivityPrecedesBeingInForce()
        {
            var xml = ExportWithExceptionsAndRetroactivity();
            var groups = Groups(xml);

            // Każdy „okres" wskazuje istniejącą grupę, a każda grupa jest używana.
            var used = xml.Descendants().Select(e => Attr(e, "okres")).Where(id => id is not null).Distinct().ToList();
            Assert.Equal(groups.Select(g => Attr(g, "id")).Order(), used.Order());

            // Reguła ogólna: stosowanie z mocą wsteczną do dnia wejścia w życie, potem obowiązywanie.
            var main = Assert.Single(groups, g => Attr(g, "glowna") == "true");
            Assert.Equal(new[] { "stosowanie-z-moca-wsteczna", "obowiazywanie" }, main.Elements().Select(i => Attr(i, "rodzaj")));
            Assert.Equal(Attr(main.Elements().First(), "do"), Attr(main.Elements().Last(), "od"));

            // Wyjątki mają własne daty wejścia w życie i nie dziedziczą mocy wstecznej reguły ogólnej.
            // Artykuł cytowany niesie prefiks aktu zmienianego, artykuły samej ustawy — jej własny (ADR-0027).
            XElement Unit(string eId) => xml.Descendants().Single(e => e.Name.LocalName == "artykul" && Attr(e, "eId") == eId);
            Assert.Equal("okr_obowiazywanie.zdarz_wejscie_2027-01-01_DU-2024-928_odczytane.x", Attr(Unit("DU/2025/350__art_85a"), "okres"));
            Assert.Equal("okr_obowiazywanie.zdarz_wejscie_2024-06-25_DU-2024-928_domyslne.x", Attr(Unit("DU/2024/928__art_2"), "okres"));
            Assert.Equal(Attr(main, "id"), Attr(Unit("DU/2024/928__art_1"), "okres"));
        }

        // ============================================================
        // Zakres wyjątku: adres jednostki w akcie zmienianym
        // ============================================================

        private static string?[] ScopeOf(XElement xml, string unit) => xml.Descendants()
            .Single(e => e.Name.LocalName == "JednostkaAktu" && Attr(e, "eId") == unit)
            .Elements().Select(e => Attr(e, "eId")).ToArray();

        [Fact]
        public void ExceptionScope_IsTheAddressOfTheQuotedUnit()
        {
            // Przykład autorów schematu (DU/2026/26, art. 43 pkt 3): zakres zapisany względem przepisu
            // zmieniającego („w zakresie ust. 1d") wskazuje jednostkę aktu zmienianego.
            var xml = Export(
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o zmianie ustawy o świadczeniach",
                "Art. 1. W ustawie z dnia 27 sierpnia 2004 r. o świadczeniach (Dz. U. z 2025 r. poz. 501) w art. 17 po ust. 1c dodaje się ust. 1d i 1e w brzmieniu:",
                "„1d. Pierwszy dodawany ustęp.",
                "1e. Drugi dodawany ustęp.”.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia, z wyjątkiem art. 1 w zakresie ust. 1d, " +
                "który wchodzi w życie z dniem 1 stycznia 2027 r.");

            Assert.Equal(new[] { "DU/2025/501__art_17__ust_1d" }, ScopeOf(xml, "DU/2024/928__art_1"));

            // Artykuł bez ustępów niesie nowelizację w ustępie niejawnym — wyjątek datuje wskazany ustęp cytowany.
            string? Entry(string eId) =>
                Attr(xml.Descendants().Single(e => e.Name.LocalName == "ustep" && Attr(e, "eId") == eId), "wejscie-w-zycie");
            Assert.Equal("2027-01-01", Entry("DU/2025/501__art_17__ust_1d"));
            Assert.Equal("2024-07-09", Entry("DU/2025/501__art_17__ust_1e"));
        }

        [Fact]
        public void ExceptionScope_OfARepeal_IsTheTargetOfItsCommand()
        {
            // Uchylenie nie cytuje jednostki — jej adres niesie cel komendy.
            var xml = Export(
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o zmianie ustawy o świadczeniach",
                "Art. 1. W ustawie z dnia 27 sierpnia 2004 r. o świadczeniach (Dz. U. z 2025 r. poz. 501) w art. 22 uchyla się ust. 12.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia, z wyjątkiem art. 1 w zakresie ust. 12, " +
                "który wchodzi w życie z dniem 1 stycznia 2027 r.");

            Assert.Equal(new[] { "DU/2025/501__art_22__ust_12" }, ScopeOf(xml, "DU/2024/928__art_1"));
        }

        [Fact]
        public void ExceptionScope_NotPointedAtByTheProvision_StaysAsWrittenInTheClause()
        {
            var xml = Export(
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o zmianie ustawy o świadczeniach",
                "Art. 1. W ustawie z dnia 27 sierpnia 2004 r. o świadczeniach (Dz. U. z 2025 r. poz. 501) w art. 22 uchyla się ust. 12.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia, z wyjątkiem art. 1 w zakresie ust. 7, " +
                "który wchodzi w życie z dniem 1 stycznia 2027 r.");

            Assert.Equal(new[] { "ust_7" }, ScopeOf(xml, "DU/2024/928__art_1"));
        }

        [Fact]
        public void ActTitleBlock_IsExportedAsKindDateAndSubject()
        {
            var act = Export().Elements().First();
            var ns = act.Name.Namespace;

            Assert.Equal("USTAWA", act.Element(ns + "rodzaj-aktu")?.Value);
            Assert.Equal("z dnia 14 czerwca 2024 r.", act.Element(ns + "data")?.Value);
            Assert.Equal("o ochronie sygnalistów", act.Element(ns + "tytul")?.Value);
            Assert.Equal(new[] { "metadane", "rodzaj-aktu", "data", "tytul", "rozdzial" },
                act.Elements().Select(e => e.Name.LocalName).Take(5));
        }

        // ============================================================
        // Rodzaj aktu i organy w metadanych
        // ============================================================

        private static XElement Metadata(params string[] lines) =>
            Export(lines).Descendants().First(e => e.Name.LocalName == "metadane");

        private static string? Child(XElement parent, string name) =>
            parent.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value;

        [Theory]
        // Rodzaj rozstrzyga nagłówek samego aktu — inaczej każdy dokument wychodził jako USTAWA.
        [InlineData("USTAWA", "o ochronie sygnalistów", "USTAWA")]
        [InlineData("ROZPORZĄDZENIE MINISTRA FINANSÓW", "w sprawie wzoru deklaracji", "ROZP")]
        [InlineData("OBWIESZCZENIE MARSZAŁKA SEJMU RZECZYPOSPOLITEJ POLSKIEJ", "w sprawie ogłoszenia jednolitego tekstu ustawy – Prawo bankowe", "OBW_TJ")]
        [InlineData("OBWIESZCZENIE MINISTRA ZDROWIA", "w sprawie wykazu leków refundowanych", "OBW")]
        public void ActKindCode_FollowsTheHeadingOfTheAct(string heading, string subject, string code)
        {
            var metadata = Metadata(heading, "z dnia 14 czerwca 2024 r.", subject,
                "Art. 1. Przepis pierwszy.", "Art. 2. Akt wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

            Assert.Equal(code, Child(metadata, "Rodzaj"));
        }

        [Fact]
        public void Statute_IsIssuedBySejm_AndSignedByThePresident()
        {
            // Jak w przykładach autorów schematu: organ wydający SEJM, podpisujący Prezydent, oba z zasobem ELI.
            var metadata = Metadata(
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o ochronie sygnalistów",
                "Art. 1. Ustawa określa zasady ochrony sygnalistów.",
                "Art. 2. Ustawa wchodzi w życie po upływie 3 miesięcy od dnia ogłoszenia.",
                "Prezydent Rzeczypospolitej Polskiej: A. Duda");

            var authorities = metadata.Descendants().Where(e => e.Name.LocalName == "Organ").ToList();

            Assert.Equal(new[] { "wydajacy", "podpisujacy" }, authorities.Select(a => Attr(a, "rola")));
            Assert.Equal("SEJM", Child(authorities[0], "Nazwa"));
            Assert.Equal("https://eli.gov.pl/resource/authority/legal-institution/Sejm", Attr(authorities[0], "eliURI"));
            Assert.Equal("https://eli.gov.pl/resource/authority/legal-institution/Prezydent", Attr(authorities[1], "eliURI"));
        }

        [Fact]
        public void UnreadIssuingAuthority_HasNoInventedResource()
        {
            var metadata = Metadata(
                "ROZPORZĄDZENIE MINISTRA FINANSÓW", "z dnia 14 czerwca 2024 r.", "w sprawie wzoru deklaracji",
                "Art. 1. Przepis pierwszy.");

            var issuing = metadata.Descendants().Single(e => e.Name.LocalName == "Organ");
            Assert.Null(Attr(issuing, "eliURI"));
        }
    }
}
