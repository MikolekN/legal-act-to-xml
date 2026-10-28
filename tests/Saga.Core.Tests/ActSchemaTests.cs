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

        [SchemaPlFact]
        public void CommandsInSeparateSentences_AreSchemaValid()
        {
            // Każda komenda w zdaniu, które ją zapisuje (ADR-0122): uchylenie, zmiana wyrazów i cytat zdania.
            var xml = Export(
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o zmianie ustawy o opłatach",
                "Art. 1. W ustawie z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) wprowadza się następujące zmiany:",
                "1) art. 127 uchyla się. W art. 128 zdanie drugie otrzymuje brzmienie:",
                "„Opłaty nie pobiera się od osób zwolnionych.”;",
                "2) w art. 129 wyrazy „14 dni” zastępuje się wyrazami „30 dni”. Art. 130 uchyla się.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

            Assert.Equal(2, xml.Descendants().Count(e => e.Name.LocalName == "uchyl"));
            Assert.Empty(Problems(xml));
        }

        [SchemaPlFact]
        public void DesignationsWithPolishLetters_AreSchemaValid()
        {
            // Schemat od wersji 0.3.4 przyjmuje polskie litery w `nr` i w eId („art. 48ł", „lit. ł") — w Prawie
            // bankowym art. 48ł stoi między art. 48l a art. 48m. Starsza kopia schematu ten test obleje.
            // Literę „ł)" niesie styl litery — z samego tekstu literą nie jest (§ 56 ZTP).
            var blocks = new List<DocumentBlock>
            {
                new() { Text = "DZIENNIK USTAW RZECZYPOSPOLITEJ POLSKIEJ", Role = BlockRole.JournalHeader },
                new() { Text = "Warszawa, dnia 24 czerwca 2024 r.", Role = BlockRole.JournalHeader },
                new() { Text = "Poz. 928", Role = BlockRole.JournalHeader },
                new() { Text = "USTAWA" }, new() { Text = "z dnia 14 czerwca 2024 r." }, new() { Text = "o działalności transgranicznej" },
                new() { Text = "Art. 48l. Instytucja kredytowa prowadzi działalność przez oddział." },
                new() { Text = "Art. 48ł. 1. Instytucja kredytowa może rozpocząć działalność transgraniczną:" },
                new() { Text = "1) po otrzymaniu zawiadomienia zawierającego:" },
                new() { Text = "l) nazwę instytucji," },
                new() { Text = "ł) adres siedziby,", StyleId = "LIT" },
                new() { Text = "m) rodzaj czynności;" },
                new() { Text = "2) po upływie miesiąca od dnia zawiadomienia." },
                new() { Text = "1ł. Przepis ust. 1 pkt 1 lit. ł stosuje się odpowiednio do oddziału." },
                new() { Text = "Art. 48m. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia." },
            };
            var document = LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document;
            var xml = LegalDocumentConverter.Convert(document!);

            var addresses = xml.Descendants().Select(e => e.Attribute("eId")?.Value).OfType<string>().ToList();
            Assert.Contains("DU/2024/928__art_48ł__ust_1__pkt_1__lit_ł", addresses);
            Assert.Contains("DU/2024/928__art_48ł__ust_1ł", addresses);
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


        [Fact]
        public void Root_DeclaresTheSchemaNamespaces_AlsoTheUnusedOnes_AndMathMlOnlyWithFormulas()
        {
            // Jak w przykładach autorów schematu: każdy plik ma ten sam nagłówek, także bez typografii i komentarzy.
            var declared = Export().Attributes().Where(a => a.IsNamespaceDeclaration)
                .Select(a => (a.Name.LocalName == "xmlns" ? "" : a.Name.LocalName, a.Value)).ToList();

            Assert.Equal(
            [
                ("", "https://projects.task.gda.pl/caise/NLP/kisi/rcl/schemaPL/1.0"),
                ("now", "https://projects.task.gda.pl/caise/NLP/kisi/rcl/schemaPL/now"),
                ("gd", "https://projects.task.gda.pl/caise/NLP/kisi/rcl/GUIDs/1.0"),
                ("tpg", "https://projects.task.gda.pl/caise/NLP/kisi/rcl/typografia/1.0"),
                ("kmn", "https://projects.task.gda.pl/caise/NLP/kisi/rcl/komentarze/1.0"),
            ], declared);
        }
        [Fact]
        public void QuotedSentence_TakesTheDateAndGroupOfTheSentenceItStandsIn()
        {
            // Jak w przykładzie autorów schematu (DU/2026/26, art. 12 pkt 7): zdanie w „cytat-w-tekscie"
            // niesie tę samą datę i grupę co zdanie, w którym stoi — także datę wyjątku.
            var xml = Export(
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o zmianie ustawy o opłatach",
                "Art. 1. W ustawie z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) wprowadza się następujące zmiany:",
                "1) w art. 126 zdanie pierwsze otrzymuje brzmienie:",
                "„Opłatę wnosi się w terminie 14 dni.”;",
                "2) w art. 127 zdanie drugie otrzymuje brzmienie:",
                "„Opłaty nie pobiera się od osób zwolnionych.”.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia, z wyjątkiem art. 1 pkt 2, " +
                "który wchodzi w życie z dniem 1 stycznia 2027 r.");

            var quoted = xml.Descendants()
                .Where(e => e.Name.LocalName == "zdanie" && e.Parent?.Name.LocalName == "cytat-w-tekscie")
                .ToList();
            Assert.Equal(2, quoted.Count);

            foreach (var sentence in quoted)
            {
                var host = sentence.Ancestors().First(e => e.Name.LocalName == "zdanie");
                Assert.NotNull(Attr(host, "wejscie-w-zycie"));
                Assert.Equal(Attr(host, "wejscie-w-zycie"), Attr(sentence, "wejscie-w-zycie"));
                Assert.Equal(Attr(host, "okres"), Attr(sentence, "okres"));
            }

            Assert.Equal("2027-01-01", Attr(quoted[1], "wejscie-w-zycie"));
            Assert.Equal("okr_obowiazywanie.zdarz_wejscie_2027-01-01_DU-2024-928_odczytane.x", Attr(quoted[1], "okres"));
        }

        [Fact]
        public void QuotedSentence_TakesTheRetroactivityOfTheSentenceItStandsIn()
        {
            // Moc wsteczna wskazana zdaniem, nie jednostką, obejmuje też zdanie przytoczone w tym zdaniu (ADR-0121).
            var xml = Export(
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o zmianie ustawy o opłatach",
                "Art. 1. W ustawie z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) wprowadza się następujące zmiany:",
                "1) w art. 126 zdanie pierwsze otrzymuje brzmienie:",
                "„Opłatę wnosi się w terminie 14 dni.”;",
                "2) w art. 127 zdanie drugie otrzymuje brzmienie:",
                "„Opłaty nie pobiera się od osób zwolnionych.”.",
                "Art. 2. Ustawa wchodzi w życie z dniem następującym po dniu ogłoszenia, z tym że art. 1 pkt 2 zdanie pierwsze " +
                "z mocą od dnia 1 stycznia 2024 r.");

            var quoted = xml.Descendants()
                .Where(e => e.Name.LocalName == "zdanie" && e.Parent?.Name.LocalName == "cytat-w-tekscie")
                .ToList();
            Assert.Equal(2, quoted.Count);
            Assert.Equal("2024-06-25", Attr(quoted[0], "wejscie-w-zycie"));
            Assert.Equal("2024-01-01", Attr(quoted[1], "wejscie-w-zycie"));

            var host = quoted[1].Ancestors().First(e => e.Name.LocalName == "zdanie");
            Assert.Equal(Attr(host, "okres"), Attr(quoted[1], "okres"));
            Assert.StartsWith("okr_moc-wsteczna.", Attr(quoted[1], "okres"));
        }

        // ============================================================
        // Podstawa zdarzenia: jednostka klauzuli, która je zapisuje
        // ============================================================

        private static (string? Basis, string? Evidence) BasisOf(XElement xml, string date)
        {
            var entry = xml.Descendants().Single(e =>
                e.Name.LocalName == "Zdarzenie" && Attr(e, "rodzaj") == "wejscie-w-zycie" && Attr(e, "data") == date);
            return (Attr(entry, "podstawa"), Attr(entry, "dowod"));
        }

        [Fact]
        public void ExceptionEvent_IsBasedOnTheClauseItemThatStatesIt()
        {
            // Przykład autorów schematu (DU/2026/26, art. 43): zdarzenie wyjątku wskazuje punkt klauzuli,
            // zdarzenie reguły ogólnej — artykuł.
            var xml = ExportWithExceptionsAndRetroactivity();

            Assert.Equal(("art. 3 pkt 1", "DU/2024/928__art_3__pkt_1"), BasisOf(xml, "2027-01-01"));
            Assert.Equal(("art. 3 pkt 2", "DU/2024/928__art_3__pkt_2"), BasisOf(xml, "2024-06-25"));
            Assert.Equal(("art. 3", "DU/2024/928__art_3"), BasisOf(xml, "2024-07-09"));
        }

        [Fact]
        public void ExceptionEvent_StatedInSeveralItems_IsBasedOnTheWholeClause()
        {
            var xml = Export(
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o ochronie sygnalistów",
                "Art. 1. Przepis pierwszy.",
                "Art. 2. Przepis drugi.",
                "Art. 3. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia, z wyjątkiem:",
                "1) art. 1, który wchodzi w życie z dniem 1 stycznia 2027 r.;",
                "2) art. 2, który wchodzi w życie z dniem 1 stycznia 2027 r.");

            Assert.Equal(("art. 3", "DU/2024/928__art_3"), BasisOf(xml, "2027-01-01"));
        }

        [Fact]
        public void EventOfASectionBasedAct_CitesTheSection()
        {
            // Dz. U. 2026 poz. 661: „§ 3. Rozporządzenie wchodzi w życie …" — adres ma segment art (ADR-0034), powołanie „§ 3".
            var xml = Export(
                "ROZPORZĄDZENIE", "MINISTRA SPRAWIEDLIWOŚCI", "z dnia 17 maja 2026 r.", "w sprawie doręczeń pism",
                "Na podstawie art. 131 § 5 ustawy z dnia 6 czerwca 1997 r. – Kodeks postępowania karnego zarządza się, co następuje:",
                "§ 1. Przepis pierwszy.",
                "§ 2. Przepis drugi.",
                "§ 3. Rozporządzenie wchodzi w życie z dniem 1 czerwca 2026 r.");

            Assert.Equal(("§ 3", "DU/2024/928__art_3"), BasisOf(xml, "2026-06-01"));
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
        public void ActKindCode_ComesFromTheClassifier_WhenTheHeadingIsNotRecognised()
        {
            // Nagłówek z literówką w wersalikach nie pasuje do wzorca rodzaju — bez drugiego sygnału
            // rozporządzenie wychodziło jako USTAWA, z Sejmem jako organem wydającym.
            var metadata = Metadata(
                "ROZPORZĄDZENIE MINISTRA zDROWIA",
                "z dnia 18 sierpnia 2025 r.",
                "w sprawie statutu Krajowej Rady Kardiologicznej",
                "Na podstawie art. 33 ustawy z dnia 4 czerwca 2025 r. o Krajowej Sieci Kardiologicznej (Dz. U. poz. 779) zarządza się, co następuje:",
                "§ 1. Określa się statut Krajowej Rady Kardiologicznej, stanowiący załącznik do rozporządzenia.",
                "§ 2. Rozporządzenie wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

            Assert.Equal("ROZP", Child(metadata, "Rodzaj"));
            Assert.DoesNotContain(metadata.Descendants(), e => e.Name.LocalName == "Nazwa" && e.Value == "SEJM");
        }

        [Fact]
        public void ActKindHeading_WithAFootnoteMarker_IsStillTheHeading()
        {
            // Odnośnik przy organie wyjaśnia jego właściwość — nagłówek z takim znacznikiem nie był rozpoznawany,
            // więc obwieszczenie nie miało strefy tytułowej i wychodziło jako USTAWA.
            var xml = Export(
                "Obwieszczenie Ministra Rodziny, Pracy i Polityki Społecznej[1)]",
                "z dnia 22 stycznia 2026 r.",
                "w sprawie wysokości kwoty kryterium dochodowego",
                "Na podstawie art. 9 ust. 2f ustawy z dnia 7 września 2007 r. o pomocy osobom uprawnionym do alimentów " +
                "(Dz. U. z 2026 r. poz. 55) ogłasza się, że kwota kryterium dochodowego wynosi 1620 zł.");

            var metadata = xml.Descendants().First(e => e.Name.LocalName == "metadane");
            Assert.Equal("OBW", Child(metadata, "Rodzaj"));

            // Korzeniem jest obwieszczenie: rodzaj i organ stoją w nim osobno.
            Assert.Equal("Obwieszczenie", xml.Descendants().Single(e => e.Name.LocalName == "rodzaj-aktu").Value);
            Assert.StartsWith("Ministra Rodziny, Pracy i Polityki Społecznej",
                xml.Descendants().Single(e => e.Name.LocalName == "autor-aktu").Value);
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
        public void IssuingAuthorityOutsideTheDictionary_HasNoInventedResource()
        {
            var metadata = Metadata(
                "ROZPORZĄDZENIE MINISTRA FINANSÓW", "z dnia 14 czerwca 2024 r.", "w sprawie wzoru deklaracji",
                "Art. 1. Przepis pierwszy.");

            // Organ jest odczytany ze strefy tytułowej, ale jego zasobu ELI nie ma w przykładach autorów schematu.
            var issuing = metadata.Descendants().Single(e => e.Name.LocalName == "Organ");
            Assert.Equal("Minister Finansów", issuing.Elements().Single(e => e.Name.LocalName == "Nazwa").Value);
            Assert.Null(Attr(issuing, "eliURI"));
        }
    }
}
