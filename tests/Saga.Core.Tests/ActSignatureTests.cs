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
        [InlineData("Organem właściwym jest minister: A. Kowalski i inni.")]
        [InlineData("Prezydent Rzeczypospolitej Polskiej")]
        [InlineData("Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.")]
        [InlineData("")]
        public void OtherText_IsNotASignature(string text)
        {
            Assert.Null(ActSignatureParser.TryParse(text));
        }

        private static LegalDocument Parse(params (string Text, string? Style)[] lines)
        {
            var blocks = new List<DocumentBlock>
            {
                new() { Text = "DZIENNIK USTAW RZECZYPOSPOLITEJ POLSKIEJ", Role = BlockRole.JournalHeader },
                new() { Text = "Warszawa, dnia 24 czerwca 2024 r.", Role = BlockRole.JournalHeader },
                new() { Text = "Poz. 928", Role = BlockRole.JournalHeader },
            };
            blocks.AddRange(lines.Select(line => new DocumentBlock { Text = line.Text, StyleId = line.Style }));

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
