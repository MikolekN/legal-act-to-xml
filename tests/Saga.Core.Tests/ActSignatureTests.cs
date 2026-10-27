using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Core.Services.Parsing;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Formuła podpisu za ostatnim przepisem aktu („Prezydent Rzeczypospolitej Polskiej: A. Duda"):
    /// trafia do modelu i eksportu zamiast ginąć jako akapit nierozpoznany.
    /// </summary>
    public class ActSignatureTests
    {
        private const string SignatureStyle = "NAZORGWYDnazwaorganuwydajcegoprojektowanyakt";

        [Theory]
        [InlineData("Prezydent Rzeczypospolitej Polskiej: A. Duda", "Prezydent Rzeczypospolitej Polskiej", "A.", "Duda")]
        [InlineData("Marszałek Sejmu: Sz. Hołownia", "Marszałek Sejmu", "Sz.", "Hołownia")]
        [InlineData("Prezes Rady Ministrów: M. Kidawa-Błońska", "Prezes Rady Ministrów", "M.", "Kidawa-Błońska")]
        [InlineData("Minister Finansów:  J.  K. Nowak ", "Minister Finansów", "J. K.", "Nowak")]
        public void Formula_IsReadAsAuthorityAndPerson(string text, string authority, string givenName, string surname)
        {
            var signature = ActSignatureParser.TryParse(text);

            Assert.NotNull(signature);
            Assert.Equal(authority, signature.Authority);
            Assert.Equal(givenName, signature.GivenName);
            Assert.Equal(surname, signature.Surname);
        }

        [Theory]
        // W zastępstwie i z upoważnienia: skrót stoi między organem a osobą i zostaje w formule.
        [InlineData("Minister Finansów i Gospodarki: wz. J. Neneman", "Minister Finansów i Gospodarki", "wz.", "J.", "Neneman")]
        [InlineData("Minister Obrony Narodowej: z up. P. Bejda", "Minister Obrony Narodowej", "z up.", "P.", "Bejda")]
        [InlineData("Marszałek Sejmu: wz. P. Zgorzelski", "Marszałek Sejmu", "wz.", "P.", "Zgorzelski")]
        // Przecinek w nazwie organu.
        [InlineData("Minister Rodziny, Pracy i Polityki Społecznej: A. Dziemianowicz-Bąk", "Minister Rodziny, Pracy i Polityki Społecznej", null, "A.", "Dziemianowicz-Bąk")]
        public void Formula_SignedOnBehalf_KeepsTheCapacity(
            string text, string authority, string? capacity, string givenName, string surname)
        {
            var signature = ActSignatureParser.TryParse(text)!;

            Assert.Equal(authority, signature.Authority);
            Assert.Equal(capacity, signature.Capacity);
            Assert.Equal(givenName, signature.GivenName);
            Assert.Equal(surname, signature.Surname);
        }

        [Fact]
        public void SignatureOnBehalf_IsExportedAsWritten()
        {
            var document = Parse(
                ("ROZPORZĄDZENIE", null), ("MINISTRA FINANSÓW", null), ("z dnia 14 czerwca 2024 r.", null),
                ("w sprawie wzoru zgłoszenia", null),
                ("Na podstawie art. 5 ustawy z dnia 1 lutego 2021 r. o zgłoszeniach (Dz. U. poz. 200) zarządza się, co następuje:", null),
                ("§ 1. Rozporządzenie określa wzór zgłoszenia.", null),
                ("§ 2. Rozporządzenie wchodzi w życie po upływie 14 dni od dnia ogłoszenia.", null),
                ("Minister Finansów: wz. J. Neneman", SignatureStyle));

            var act = LegalDocumentConverter.Convert(document).Elements().Single();
            var signature = act.Elements().Single(e => e.Name.LocalName == "organ-podpisujacy");

            Assert.Equal("Minister Finansów: wz. J. Neneman", signature.Value);
            Assert.Equal("Neneman", signature.Descendants().Single(e => e.Name.LocalName == "nazwisko").Value);

            // Formuła nie zostaje dalszą treścią ostatniego paragrafu.
            Assert.DoesNotContain(act.Descendants(), e => e.Name.LocalName == "fragment");
        }

        [SchemaPlFact]
        public void ActIssuedJointly_KeepsEverySignature()
        {
            // Korpus: 329092_3181272 — dwie formuły pod rozporządzeniem wydanym wspólnie; żadna nie ginie.
            var document = Parse(
                ("ROZPORZĄDZENIE", null), ("MINISTRÓW INFRASTRUKTURY ORAZ SPRAW WEWNĘTRZNYCH I ADMINISTRACJI", null),
                ("z dnia 14 czerwca 2024 r.", null), ("w sprawie znaków drogowych", null),
                ("Na podstawie art. 7 ustawy z dnia 20 czerwca 1997 r. – Prawo o ruchu drogowym (Dz. U. z 2024 r. poz. 1251) zarządza się, co następuje:", null),
                ("§ 1. Rozporządzenie określa znaki drogowe.", null),
                ("§ 2. Rozporządzenie wchodzi w życie po upływie 14 dni od dnia ogłoszenia.", null),
                ("Minister Infrastruktury: wz. M. Bukowiec", SignatureStyle),
                ("Minister Spraw Wewnętrznych i Administracji: wz. C. Mroczek", SignatureStyle));

            Assert.Equal("Bukowiec", document.Signature!.Surname);
            Assert.Equal("Mroczek", Assert.Single(document.CoSignatures).Surname);

            var act = LegalDocumentConverter.Convert(document).Elements().Single();
            Assert.Equal(
                new[] { "Minister Infrastruktury: wz. M. Bukowiec", "Minister Spraw Wewnętrznych i Administracji: wz. C. Mroczek" },
                act.Elements().Where(e => e.Name.LocalName == "organ-podpisujacy").Select(e => e.Value));
            Assert.Empty(SchemaPl.Validator.Validate(act.Parent!));
        }

        [Theory]
        [InlineData("Organem właściwym jest minister: A. Kowalski i inni.")]
        // Przecinek dopuszcza nazwę organu, nie zdanie z wyliczeniem przed dwukropkiem i dalszym tekstem.
        [InlineData("Do zadań ministra, wojewody i starosty należy: Nadzór nad jednostkami.")]
        [InlineData("Prezydent Rzeczypospolitej Polskiej")]
        [InlineData("Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.")]
        [InlineData("")]
        public void OtherText_IsNotASignature(string text)
        {
            Assert.Null(ActSignatureParser.TryParse(text));
        }

        private static LegalDocument Parse(params (string Text, string? Style)[] lines) =>
            Parse(lines.Select(line => new DocumentBlock { Text = line.Text, StyleId = line.Style }));

        private static LegalDocument Parse(IEnumerable<DocumentBlock> body)
        {
            var blocks = new List<DocumentBlock>
            {
                new() { Text = "DZIENNIK USTAW RZECZYPOSPOLITEJ POLSKIEJ", Role = BlockRole.JournalHeader },
                new() { Text = "Warszawa, dnia 24 czerwca 2024 r.", Role = BlockRole.JournalHeader },
                new() { Text = "Poz. 928", Role = BlockRole.JournalHeader },
            };
            blocks.AddRange(body);

            return LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
        }

        private static LegalDocument SignedAct() => Parse(
            ("USTAWA", null), ("z dnia 14 czerwca 2024 r.", null), ("o ochronie sygnalistów", null),
            ("Art. 1. Ustawa określa zasady ochrony sygnalistów.", null),
            ("Art. 2. Ustawa wchodzi w życie po upływie 3 miesięcy od dnia ogłoszenia.", null),
            ("Prezydent Rzeczypospolitej Polskiej: A. Duda", SignatureStyle));

        [Fact]
        public void SignatureAfterTheLastProvision_IsKeptInTheModel()
        {
            var document = SignedAct();

            Assert.NotNull(document.Signature);
            Assert.Equal("Prezydent Rzeczypospolitej Polskiej", document.Signature.Authority);
            Assert.Equal("Duda", document.Signature.Surname);

            // Podpis nie jest już „pominiętym akapitem nierozpoznanym" ostatniego artykułu.
            var last = document.Articles.Last().Paragraphs.Single();
            Assert.DoesNotContain(last.ValidationMessages, message => message.Message.Contains("Pominięty akapit"));
        }

        [Fact]
        public void Export_ClosesTheActWithTheSigningAuthority()
        {
            // Jak w przykładach autorów schematu: organ, dwukropek i osoba z imieniem i nazwiskiem.
            var act = LegalDocumentConverter.Convert(SignedAct()).Elements().First();

            var signature = act.Elements().Last();
            Assert.Equal("organ-podpisujacy", signature.Name.LocalName);
            Assert.Equal("Prezydent Rzeczypospolitej Polskiej: A. Duda", signature.Value);

            var person = Assert.Single(signature.Elements());
            Assert.Equal("osoba", person.Name.LocalName);
            Assert.Equal("true", person.Attribute("podpisujacy")?.Value);
            Assert.Equal(new[] { "imie", "nazwisko" }, person.Elements().Select(e => e.Name.LocalName));
        }

        [Fact]
        public void Export_ListsTheSigningAuthorityInMetadata()
        {
            var xml = LegalDocumentConverter.Convert(SignedAct());

            var signing = xml.Descendants().Single(e => e.Name.LocalName == "Organ" && e.Attribute("rola")?.Value == "podpisujacy");
            string Child(string name) => signing.Elements().Single(e => e.Name.LocalName == name).Value;

            Assert.Equal("Prezydent Rzeczypospolitej Polskiej", Child("Nazwa"));
            Assert.Equal("Prezydent Rzeczypospolitej Polskiej", Child("OrganPodpisujacy"));
            Assert.Equal("A. Duda", Child("OsobaPodpisujaca"));
        }

        [SchemaPlFact]
        public void SignedAct_IsSchemaValid_AsAWhole()
        {
            Assert.Empty(SchemaPl.Validator.Validate(LegalDocumentConverter.Convert(SignedAct())));
        }

        // Kursywa osoby w formule podpisu (ADR-0116): styl znaku szablonu RCL albo kursywa wprost.
        private const string ItalicCharacterStyle =
            @"<w:style w:type=""character"" w:styleId=""Kkursywa""><w:name w:val=""_K_ - kursywa""/><w:rPr><w:i/></w:rPr></w:style>";

        private static string Line(params string[] runs) => $"<w:p>{string.Concat(runs)}</w:p>";

        private static string Run(string text, string properties = "") =>
            $@"<w:r>{(properties.Length > 0 ? $"<w:rPr>{properties}</w:rPr>" : string.Empty)}<w:t xml:space=""preserve"">{text}</w:t></w:r>";

        private static LegalDocument ActSignedInDocx(string personRunProperties)
        {
            var body = string.Concat(
                Line(Run("USTAWA")), Line(Run("z dnia 14 czerwca 2024 r.")), Line(Run("o ochronie sygnalistów")),
                Line(Run("Art. 1. Ustawa określa zasady ochrony sygnalistów.")),
                Line(Run("Art. 2. Ustawa wchodzi w życie po upływie 3 miesięcy od dnia ogłoszenia.")),
                Line(Run("Prezydent Rzeczypospolitej Polskiej: "), Run("A. Duda", personRunProperties)));
            using var stream = new MemoryStream(DocxFixtures.WithBodyXmlAndStyles(body, ItalicCharacterStyle));
            return LegalDocumentParser.Parse(stream, "ustawa.docx", new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
        }

        [Theory]
        // Korpus DOCX: 1692 z 2333 podpisów ma nazwisko w stylu znaku „Kkursywa", 641 — kursywę wprost.
        [InlineData(@"<w:rStyle w:val=""Kkursywa""/>")]
        [InlineData("<w:i/>")]
        public void PersonSetInItalics_StandsInsideI_AsInTheExamplesOfTheSchemaAuthors(string personRunProperties)
        {
            var signature = LegalDocumentConverter.Convert(ActSignedInDocx(personRunProperties))
                .Descendants().Single(e => e.Name.LocalName == "organ-podpisujacy");

            Assert.Equal("Prezydent Rzeczypospolitej Polskiej: A. Duda", signature.Value);
            var italic = Assert.Single(signature.Elements());
            Assert.Equal("i", italic.Name.LocalName);
            Assert.Equal("osoba", Assert.Single(italic.Elements()).Name.LocalName);
        }

        [Fact]
        public void DoubleBarrelledSurnameWithANonBreakingHyphen_StaysInItalics()
        {
            // 40 podpisów korpusu: łącznik w „Dziemianowicz-Bąk" to element w:noBreakHyphen, nie znak w w:t.
            var body = string.Concat(
                Line(Run("ROZPORZĄDZENIE")), Line(Run("MINISTRA RODZINY, PRACY I POLITYKI SPOŁECZNEJ")),
                Line(Run("z dnia 14 czerwca 2024 r.")), Line(Run("w sprawie wzoru wniosku")),
                Line(Run("§ 1. Rozporządzenie wchodzi w życie po upływie 14 dni od dnia ogłoszenia.")),
                Line(Run("Minister Rodziny, Pracy i Polityki Społecznej: "),
                    @"<w:r><w:rPr><w:i/></w:rPr><w:t>A. Dziemianowicz</w:t><w:noBreakHyphen/><w:t>Bąk</w:t></w:r>"));
            using var stream = new MemoryStream(DocxFixtures.WithBodyXmlAndStyles(body, ItalicCharacterStyle));
            var document = LegalDocumentParser.Parse(stream, "rozporzadzenie.docx", new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

            Assert.True(document.Signature!.IsPersonItalic);
            Assert.Equal("Dziemianowicz-Bąk", document.Signature.Surname);
        }

        [Fact]
        public void PersonInUprightType_HasNoI()
        {
            var signature = LegalDocumentConverter.Convert(ActSignedInDocx(personRunProperties: string.Empty))
                .Descendants().Single(e => e.Name.LocalName == "organ-podpisujacy");

            Assert.Equal("osoba", Assert.Single(signature.Elements()).Name.LocalName);
        }

        [SchemaPlFact]
        public void SignatureWithAPersonInItalics_IsSchemaValid()
        {
            string[] lines =
            [
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o ochronie sygnalistów",
                "Art. 1. Ustawa określa zasady ochrony sygnalistów.",
                "Art. 2. Ustawa wchodzi w życie po upływie 3 miesięcy od dnia ogłoszenia.",
            ];
            var act = Parse(lines.Select(line => new DocumentBlock { Text = line }).Append(new DocumentBlock
            {
                Text = "Prezydent Rzeczypospolitej Polskiej: A. Duda",
                StyleId = SignatureStyle,
                Layout = new BlockLayoutInfo { ItalicText = ["A. Duda"] },
            }));

            var xml = LegalDocumentConverter.Convert(act);

            Assert.Single(xml.Descendants(), e => e.Name.LocalName == "i" && e.Parent?.Name.LocalName == "organ-podpisujacy");
            Assert.Empty(SchemaPl.Validator.Validate(xml));
        }

        [Theory]
        [InlineData("A. Duda", true)]
        [InlineData("A.Duda", true)]
        [InlineData("wz. A. Duda", true)]
        [InlineData("Duda", false)]
        [InlineData("Prezydent Rzeczypospolitej Polskiej", false)]
        public void PersonWithinAnItalicFragment_IsItalic(string fragment, bool expected)
        {
            Assert.Equal(expected, ActSignatureParser.TryParse("Prezydent Rzeczypospolitej Polskiej: A. Duda", [fragment])!.IsPersonItalic);
        }

        [Fact]
        public void FormulaFollowedByAnotherArticle_IsNotTheSignatureOfTheAct()
        {
            // Podpis pod obwieszczeniem stoi PRZED tekstem jednolitym — nie zamyka aktu.
            var document = Parse(
                ("USTAWA", null), ("z dnia 14 czerwca 2024 r.", null), ("o ochronie sygnalistów", null),
                ("Art. 1. Ustawa określa zasady ochrony sygnalistów.", null),
                ("Marszałek Sejmu: Sz. Hołownia", null),
                ("Art. 2. Ustawa wchodzi w życie po upływie 3 miesięcy od dnia ogłoszenia.", null));

            Assert.Null(document.Signature);
            Assert.DoesNotContain(LegalDocumentConverter.Convert(document).Descendants(),
                e => e.Name.LocalName == "organ-podpisujacy");
        }
    }
}
