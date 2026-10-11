using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Core.Services.Converters;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Organ wydający w metadanych (ADR-0041): organ ze strefy tytułowej, który sam podpisuje akt, jest jednym
    /// organem wydającym z osobą podpisującą; organ kolegialny i podpisujący go przewodniczący to dwa organy.
    /// </summary>
    public class IssuingAuthorityTests
    {
        private const string SignatureStyle = "NAZORGWYDnazwaorganuwydajcegoprojektowanyakt";

        private static XElement Regulation(string? issuer, params string[] signatures)
        {
            var lines = new List<(string Text, string? Style)> { ("ROZPORZĄDZENIE", null) };
            if (issuer is not null)
                lines.Add((issuer, null));
            lines.AddRange(
            [
                ("z dnia 14 czerwca 2024 r.", null),
                ("w sprawie wzoru zgłoszenia", null),
                ("Na podstawie art. 5 ustawy z dnia 1 lutego 2021 r. o zgłoszeniach (Dz. U. poz. 200) zarządza się, co następuje:", null),
                ("§ 1. Rozporządzenie określa wzór zgłoszenia.", null),
                ("§ 2. Rozporządzenie wchodzi w życie po upływie 14 dni od dnia ogłoszenia.", null),
            ]);
            lines.AddRange(signatures.Select(signature => (signature, (string?)SignatureStyle)));

            return Export(lines);
        }

        private static XElement Export(IEnumerable<(string Text, string? Style)> lines)
        {
            var blocks = new List<DocumentBlock>
            {
                new() { Text = "DZIENNIK USTAW RZECZYPOSPOLITEJ POLSKIEJ", Role = BlockRole.JournalHeader },
                new() { Text = "Warszawa, dnia 24 czerwca 2024 r.", Role = BlockRole.JournalHeader },
                new() { Text = "Poz. 928", Role = BlockRole.JournalHeader },
            };
            blocks.AddRange(lines.Select(line => new DocumentBlock { Text = line.Text, StyleId = line.Style }));

            var document = LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
            return LegalDocumentConverter.Convert(document);
        }

        private static List<XElement> Authorities(XElement xml) =>
            xml.Descendants().Where(e => e.Name.LocalName == "Organ").ToList();

        private static string? Child(XElement authority, string name) =>
            authority.Elements().SingleOrDefault(e => e.Name.LocalName == name)?.Value;

        private static string? Role(XElement authority) => authority.Attribute("rola")?.Value;

        [Fact]
        public void AuthorityThatIssuesAndSigns_IsOneIssuingAuthorityWithThePerson()
        {
            var authority = Assert.Single(Authorities(Regulation("MINISTRA FINANSÓW", "Minister Finansów: A. Domański")));

            // Nazwa w mianowniku pochodzi z formuły podpisu — jak „Marszałek Sejmu" w przykładzie autorów schematu.
            Assert.Equal("wydajacy", Role(authority));
            Assert.Equal("Minister Finansów", Child(authority, "Nazwa"));
            Assert.Equal("Minister Finansów", Child(authority, "OrganPodpisujacy"));
            Assert.Equal("A. Domański", Child(authority, "OsobaPodpisujaca"));
            Assert.Null(authority.Attribute("eliURI"));
        }

        [Fact]
        public void CollegialBody_AndTheChairWhoSigns_AreTwoAuthorities()
        {
            // Korpus: 112 rozporządzeń Rady Ministrów podpisuje Prezes Rady Ministrów — to nie ten sam organ.
            var authorities = Authorities(Regulation("RADY MINISTRÓW", "Prezes Rady Ministrów: D. Tusk"));

            Assert.Equal(new[] { "wydajacy", "podpisujacy" }, authorities.Select(Role));
            Assert.Equal("Rada Ministrów", Child(authorities[0], "Nazwa"));
            Assert.Null(Child(authorities[0], "OsobaPodpisujaca"));
            Assert.Equal("Prezes Rady Ministrów", Child(authorities[1], "Nazwa"));
            Assert.Equal("D. Tusk", Child(authorities[1], "OsobaPodpisujaca"));
        }

        [Fact]
        public void ActIssuedJointly_HasAnIssuingAuthorityForEverySigner()
        {
            // Korpus: 329092_3181272 — wyraz główny stoi w strefie tytułowej raz, w liczbie mnogiej.
            var authorities = Authorities(Regulation(
                "MINISTRÓW INFRASTRUKTURY ORAZ SPRAW WEWNĘTRZNYCH I ADMINISTRACJI",
                "Minister Infrastruktury: wz. M. Bukowiec",
                "Minister Spraw Wewnętrznych i Administracji: wz. C. Mroczek"));

            Assert.Equal(new[] { "wydajacy", "wydajacy" }, authorities.Select(Role));
            Assert.Equal(
                new[] { "Minister Infrastruktury", "Minister Spraw Wewnętrznych i Administracji" },
                authorities.Select(authority => Child(authority, "Nazwa")));
        }

        [Fact]
        public void Countersignature_DoesNotIssueTheAct()
        {
            // Korpus: 8 rozporządzeń Prezydenta z kontrasygnatą Prezesa Rady Ministrów.
            var authorities = Authorities(Regulation(
                "PREZYDENTA RZECZYPOSPOLITEJ POLSKIEJ",
                "Prezydent Rzeczypospolitej Polskiej: K. Nawrocki",
                "Prezes Rady Ministrów: D. Tusk"));

            Assert.Equal(new[] { "wydajacy", "podpisujacy" }, authorities.Select(Role));
            Assert.Equal("Prezydent Rzeczypospolitej Polskiej", Child(authorities[0], "Nazwa"));
            Assert.EndsWith("/legal-institution/Prezydent", authorities[0].Attribute("eliURI")?.Value);
            Assert.Equal("Prezes Rady Ministrów", Child(authorities[1], "Nazwa"));
        }

        [Fact]
        public void IssuerWithoutASignature_IsNamedFromTheTitleZone()
        {
            var authority = Assert.Single(Authorities(Regulation("MINISTRA SPRAW WEWNĘTRZNYCH I ADMINISTRACJI")));

            Assert.Equal("wydajacy", Role(authority));
            Assert.Equal("Minister Spraw Wewnętrznych i Administracji", Child(authority, "Nazwa"));
            Assert.Null(Child(authority, "OsobaPodpisujaca"));
        }

        [Fact]
        public void IssuerWithAFootnoteMarker_IsNamedWithoutIt()
        {
            var authority = Assert.Single(Authorities(Regulation("MINISTRA ZDROWIA[1)]", "Minister Zdrowia: J. Sobierańska-Grenda")));

            Assert.Equal("wydajacy", Role(authority));
            Assert.Equal("Minister Zdrowia", Child(authority, "Nazwa"));
        }

        [Fact]
        public void ActWithoutAnIssuerInTheTitleZone_NamesTheUnreadAuthorityPlainly()
        {
            // Podpis mówi, kto podpisał, nie kto wydał: bez organu w strefie tytułowej wydający zostaje nieodczytany.
            var authorities = Authorities(Regulation(null, "Minister Finansów: A. Domański"));

            Assert.Equal(new[] { "wydajacy", "podpisujacy" }, authorities.Select(Role));
            Assert.Equal(AuthoritiesXmlConverter.Unknown, Child(authorities[0], "Nazwa"));
        }

        [Fact]
        public void Statute_IsIssuedBySejm_AndSignedByThePresident()
        {
            var authorities = Authorities(Export(
            [
                ("USTAWA", null), ("z dnia 14 czerwca 2024 r.", null), ("o ochronie sygnalistów", null),
                ("Art. 1. Ustawa określa zasady ochrony sygnalistów.", null),
                ("Art. 2. Ustawa wchodzi w życie po upływie 3 miesięcy od dnia ogłoszenia.", null),
                ("Prezydent Rzeczypospolitej Polskiej: A. Duda", SignatureStyle),
            ]));

            Assert.Equal(new[] { "wydajacy", "podpisujacy" }, authorities.Select(Role));
            Assert.Equal("SEJM", Child(authorities[0], "Nazwa"));
            Assert.Equal("Prezydent Rzeczypospolitej Polskiej", Child(authorities[1], "Nazwa"));
        }

        [Fact]
        public void ConsolidatedText_IsIssuedByTheAuthorityOfTheAnnouncement()
        {
            // Jak w przykładzie autorów schematu (DU20261261_obw_tj.xml): jeden organ wydający z zasobem ELI
            // i osobą podpisującą; nazwa krótsza niż w strefie tytułowej („… Rzeczypospolitej Polskiej").
            var authority = Assert.Single(Authorities(Export(
            [
                ("Obwieszczenie", null),
                ("Marszałka Sejmu Rzeczypospolitej Polskiej", null),
                ("z dnia 6 sierpnia 2025 r.", null),
                ("w sprawie ogłoszenia jednolitego tekstu ustawy o odbudowie obiektów budowlanych", null),
                ("1. Na podstawie art. 16 ust. 1 ustawy z dnia 20 lipca 2000 r. o ogłaszaniu aktów normatywnych ogłasza się w załączniku do niniejszego obwieszczenia jednolity tekst ustawy z dnia 11 sierpnia 2001 r. o odbudowie obiektów budowlanych.", null),
                ("Marszałek Sejmu: S. Hołownia", null),
                ("Załącznik do obwieszczenia Marszałka Sejmu Rzeczypospolitej Polskiej z dnia 6 sierpnia 2025 r. (Dz. U. poz. 1094)", null),
                ("USTAWA", null),
                ("z dnia 11 sierpnia 2001 r.", null),
                ("o odbudowie obiektów budowlanych", null),
                ("Art. 1. Ustawa reguluje zasady odbudowy.", null),
                ("Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.", null),
            ])));

            Assert.Equal("wydajacy", Role(authority));
            Assert.Equal("Marszałek Sejmu", Child(authority, "Nazwa"));
            Assert.Equal("S. Hołownia", Child(authority, "OsobaPodpisujaca"));
            Assert.EndsWith("/legal-institution/MSEJMU", authority.Attribute("eliURI")?.Value);
        }

        [Theory]
        [InlineData("MINISTRA FINANSÓW", "Minister Finansów")]
        [InlineData("Marszałka Sejmu Rzeczypospolitej Polskiej", "Marszałek Sejmu")]
        [InlineData("PREZESA RADY MINISTRÓW", "Prezes Rady Ministrów")]
        [InlineData("Prokuratora Generalnego", "Prokurator Generalny")]
        [InlineData("Przewodniczącego Komitetu do spraw Pożytku Publicznego", "Przewodniczący Komitetu do spraw Pożytku Publicznego")]
        [InlineData("MINISTRÓW INFRASTRUKTURY ORAZ SPRAW WEWNĘTRZNYCH I ADMINISTRACJI", "Minister Infrastruktury")]
        // Strefa tytułowa i podpis rozjechane w źródle: organ rozpoznaje się po wspólnym początku nazwy.
        [InlineData("MINISTRA FINANSÓW", "Minister Finansów i Gospodarki")]
        public void TitledAndSigningName_OfTheSameAuthority_Correspond(string issuer, string signer)
        {
            Assert.True(AuthoritiesXmlConverter.IsTheSameAuthority(issuer, signer));
        }

        [Theory]
        [InlineData("RADY MINISTRÓW", "Prezes Rady Ministrów")]
        [InlineData("KRAJOWEJ RADY RADIOFONII I TELEWIZJI", "Przewodniczący Krajowej Rady Radiofonii i Telewizji")]
        [InlineData("PREZYDENTA RZECZYPOSPOLITEJ POLSKIEJ", "Prezes Rady Ministrów")]
        // Wspólny początek to za mało: formy jednego wyrazu różnią się tylko końcówką.
        [InlineData("MINISTRA SPRAWIEDLIWOŚCI", "Minister Spraw Wewnętrznych i Administracji")]
        [InlineData("MINISTRA ZDROWIA", "Minister Finansów")]
        [InlineData("", "Minister Finansów")]
        public void TitledAndSigningName_OfDifferentAuthorities_DoNotCorrespond(string issuer, string signer)
        {
            Assert.False(AuthoritiesXmlConverter.IsTheSameAuthority(issuer, signer));
        }

        [Theory]
        [InlineData("RADY MINISTRÓW", "Rada Ministrów")]
        [InlineData("KRAJOWEJ RADY RADIOFONII I TELEWIZJI", "Krajowa Rada Radiofonii i Telewizji")]
        [InlineData("PREZESA NACZELNEGO SĄDU ADMINISTRACYJNEGO", "Prezes Naczelnego Sądu Administracyjnego")]
        [InlineData("Prokuratora Generalnego", "Prokurator Generalny")]
        [InlineData("marszałka Sejmu Rzeczypospolitej Polskiej", "Marszałek Sejmu Rzeczypospolitej Polskiej")]
        [InlineData("PRZEWODNICZĄCEGO KOMITETU DO SPRAW POŻYTKU PUBLICZNEGO", "Przewodniczący Komitetu do spraw Pożytku Publicznego")]
        // Korpus: wersaliki ze stylu, a w tekście litery, jak je wpisano.
        [InlineData("MINISTRA sPRAW WEWNĘTRZNYCH I aDMINISTRACJI", "Minister Spraw Wewnętrznych i Administracji")]
        [InlineData("MINISTRA RODZINY, PRACY I POLITYKI SPOŁECZNEJ", "Minister Rodziny, Pracy i Polityki Społecznej")]
        // Wyraz główny spoza słownika: nazwa zostaje w dopełniaczu — odmiana zgadywana byłaby nazwą zmyśloną.
        [InlineData("GŁÓWNEGO INSPEKTORA SANITARNEGO", "Głównego Inspektora Sanitarnego")]
        // „Rady" jako całe słowo, nie początek innego.
        [InlineData("RADYKALNEJ KOMISJI", "Radykalnej Komisji")]
        public void NameFromTheTitleZone_IsPutInTheNominative(string titled, string expected)
        {
            Assert.Equal(expected, AuthoritiesXmlConverter.Nominative(titled));
        }

        [SchemaPlFact]
        public void ActsWithEveryLayoutOfAuthorities_AreSchemaValid_AsAWhole()
        {
            Assert.Empty(SchemaPl.Validator.Validate(Regulation("MINISTRA FINANSÓW", "Minister Finansów: A. Domański")));
            Assert.Empty(SchemaPl.Validator.Validate(Regulation("RADY MINISTRÓW", "Prezes Rady Ministrów: D. Tusk")));
            Assert.Empty(SchemaPl.Validator.Validate(Regulation(
                "MINISTRÓW INFRASTRUKTURY ORAZ SPRAW WEWNĘTRZNYCH I ADMINISTRACJI",
                "Minister Infrastruktury: wz. M. Bukowiec",
                "Minister Spraw Wewnętrznych i Administracji: wz. C. Mroczek")));
            Assert.Empty(SchemaPl.Validator.Validate(Regulation("MINISTRA FINANSÓW")));
        }
    }
}
