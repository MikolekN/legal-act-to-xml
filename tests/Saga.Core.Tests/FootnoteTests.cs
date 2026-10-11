using System.Xml.Linq;
using Saga.Core.Services;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Odnośniki (przypisy) aktu: treść czytana z pliku Word trafia do modelu, a w eksporcie staje jako
    /// <c>odnosnik</c> w miejscu znacznika — jak w przykładach autorów schematu.
    /// </summary>
    public class FootnoteTests
    {
        private static readonly string[] Footnotes =
        [
            "Niniejsza ustawa wdraża dyrektywę 2019/1937.",
            "Niniejszą ustawą zmienia się ustawę o Rzeczniku Praw Obywatelskich.",
            "Zmiany wymienionego rozporządzenia zostały ogłoszone w Dz. Urz. UE L 127 z 23.05.2018, str. 2. Sprostowanie ogłoszono osobno.",
        ];

        private static LegalDocument Parse(params string[] body)
        {
            using var stream = new MemoryStream(DocxFixtures.WithFootnotes(Footnotes, body));
            return LegalDocumentParser.Parse(stream, "ustawa.docx", new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
        }

        private static LegalDocument Act() => Parse(
            "USTAWA",
            "z dnia 14 czerwca 2024 r.",
            "o ochronie sygnalistów{1}{, }{2}",
            "Art. 1. Przepisu rozporządzenia 2016/679 (Dz. Urz. UE L 119 z 04.05.2016, str. 1, z późn. zm.{3}) nie stosuje się.",
            "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

        private static List<XElement> Named(XElement xml, string name) =>
            xml.Descendants().Where(e => e.Name.LocalName == name).ToList();

        [Fact]
        public void Footnotes_AreKeptInTheModel_InTheOrderOfReferences()
        {
            var document = Act();

            Assert.Equal(new[] { "1", "2", "3" }, document.Footnotes.Select(f => f.Symbol));
            Assert.Equal(Footnotes[0], Assert.Single(document.Footnotes[0].Paragraphs));

            // Miejsce odwołania niesie tekst jednostki.
            Assert.Contains("z późn. zm.[3)])", document.Articles.First().Paragraphs.Single().ContentText);
        }

        [Fact]
        public void Export_PutsTheFootnoteWhereItsMarkerStands()
        {
            var xml = LegalDocumentConverter.Convert(Act());

            var sentence = Named(xml, "artykul").First().Descendants().First(e => e.Name.LocalName == "zdanie");
            var footnote = Assert.Single(sentence.Elements());

            Assert.Equal("odnosnik", footnote.Name.LocalName);
            Assert.Equal("3", footnote.Attribute("id-odnosnika")?.Value);
            Assert.Equal("3", footnote.Attribute("symbol")?.Value);

            // Tekst zdania wokół odnośnika zostaje nietknięty, a znacznik znika.
            Assert.Equal("Przepisu rozporządzenia 2016/679 (Dz. Urz. UE L 119 z 04.05.2016, str. 1, z późn. zm.",
                ((XText)sentence.FirstNode!).Value);
            Assert.Equal(") nie stosuje się.", ((XText)sentence.LastNode!).Value);

            // Treść odnośnika: jeden akapit, zdania numerowane od 1.
            var content = Assert.Single(footnote.Elements());
            Assert.Equal("p", content.Name.LocalName);
            Assert.Equal(new[] { "1", "2" }, content.Elements().Select(e => e.Attribute("nr")?.Value));
        }

        [Fact]
        public void Export_SeparatesAdjacentFootnotesWithASuperscriptComma()
        {
            // <tytul>o ochronie sygnalistów<odnosnik …/><sup>,</sup> <odnosnik …/></tytul>
            var title = Named(LegalDocumentConverter.Convert(Act()), "tytul").First();

            Assert.Equal(new[] { "odnosnik", "sup", "odnosnik" }, title.Elements().Select(e => e.Name.LocalName));
            Assert.Equal(new[] { "1", "2" },
                title.Elements().Where(e => e.Name.LocalName == "odnosnik").Select(e => e.Attribute("symbol")?.Value));
            Assert.Equal(",", title.Elements().Single(e => e.Name.LocalName == "sup").Value);
            Assert.StartsWith("o ochronie sygnalistów", title.Value);
        }

        [Fact]
        public void Export_KeepsMarkersOutOfTheMetadataTitle()
        {
            var xml = LegalDocumentConverter.Convert(Act());

            Assert.Equal("USTAWA z dnia 14 czerwca 2024 r. o ochronie sygnalistów", Named(xml, "Tytul").Single().Value);
            Assert.DoesNotContain("[", xml.Value);
        }

        [Fact]
        public void SuperscriptThatIsNotAFootnote_StaysText()
        {
            // „[2)]" wpisane w treść (cytowany odnośnik innego aktu) nie ma przypisu w dokumencie.
            var document = Parse(
                "USTAWA",
                "z dnia 14 czerwca 2024 r.",
                "o ochronie sygnalistów{1}",
                "Art. 1. W tytule ustawy odnośnik {2)} otrzymuje nowe brzmienie.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

            var xml = LegalDocumentConverter.Convert(document);

            Assert.Single(Named(xml, "odnosnik"));
            Assert.Contains("odnośnik [2)] otrzymuje", Named(xml, "artykul").First().Value);
        }

        [Fact]
        public void SecondReferenceToTheSameFootnote_IsACrossReference()
        {
            // W tekstach jednolitych jeden odnośnik („Dodany przez art. …") obsługuje kilka jednostek;
            // kolejne odwołania są w pliku zwykłym indeksem górnym z tym samym numerem.
            var document = Parse(
                "USTAWA",
                "z dnia 14 czerwca 2024 r.",
                "o ochronie sygnalistów",
                "Art. 1. 1. {1} Ustawa określa zasady ochrony.",
                "2. {1)} Przepisy stosuje się odpowiednio.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

            var xml = LegalDocumentConverter.Convert(document);

            Assert.Equal("1", Assert.Single(Named(xml, "odnosnik")).Attribute("id-odnosnika")?.Value);
            var reference = Assert.Single(Named(xml, "odsylacz-do-odnosnika"));
            Assert.Equal("1", reference.Attribute("id-odnosnika")?.Value);
            Assert.Equal("art_1__ust_2", reference.Ancestors().First(e => e.Attribute("eId") != null).Attribute("eId")?.Value);
        }

        [Fact]
        public void MarkerWithPunctuationInsideTheSuperscript_IsStillAFootnote()
        {
            // 310705_2774450: przecinek wciągnięty do indeksu górnego — „[1),] [2)]".
            var document = Parse(
                "USTAWA",
                "z dnia 27 listopada 2024 r.",
                "o zmianie ustawy – Prawo ochrony środowiska{1}{,} {2}",
                "Art. 1. Ustawa (Dz. U. poz. 1, z późn. zm.{3}) określa zasady.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

            var xml = LegalDocumentConverter.Convert(document);

            Assert.Equal(new[] { "1", "2", "3" }, Named(xml, "odnosnik").Select(e => e.Attribute("symbol")?.Value));
            Assert.DoesNotContain("[", xml.Value);
        }

        [Fact]
        public void FootnoteAtAUnitHeading_IsEmbeddedInTheHeading()
        {
            // W tekstach jednolitych tytuł jednostki systematyzacyjnej niesie odnośnik o swoim brzmieniu.
            var document = Parse(
                "USTAWA",
                "z dnia 14 czerwca 2024 r.",
                "o ochronie sygnalistów",
                "Rozdział 1",
                "Przepisy ogólne{1}",
                "Art. 1. Ustawa (Dz. U. poz. 1, z późn. zm.{2}) określa zasady.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

            var xml = LegalDocumentConverter.Convert(document);

            Assert.Equal(new[] { "1", "2" }, Named(xml, "odnosnik").Select(footnote => footnote.Attribute("symbol")?.Value));
            var heading = Named(xml, "naglowek-rozdzialu").Single();
            Assert.Equal("1", Assert.Single(heading.Descendants(), e => e.Name.LocalName == "odnosnik").Attribute("symbol")?.Value);
            Assert.DoesNotContain("[", heading.Value);
        }

        private static LegalDocument Regulation() => Parse(
            "ROZPORZĄDZENIE",
            "MINISTRA FINANSÓW{1}",
            "z dnia 14 czerwca 2024 r.",
            "w sprawie wzoru zgłoszenia",
            "Na podstawie art. 5 ustawy z dnia 1 lutego 2021 r. o zgłoszeniach (Dz. U. poz. 200, z późn. zm.{2}) zarządza się, co następuje:",
            "§ 1. Rozporządzenie określa wzór zgłoszenia.",
            "§ 2. Rozporządzenie wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

        [Fact]
        public void FootnoteAtTheIssuingAuthority_IsEmbeddedInTheTitleZone()
        {
            // Korpus: 1093 akty wykonawcze — pierwszy odnośnik aktu stoi przy organie („MINISTRA FINANSÓW¹⁾")
            // i mówi, z jakiego przepisu minister kieruje działem; drugi zwykle w podstawie prawnej.
            var xml = LegalDocumentConverter.Convert(Regulation());

            var issuer = Named(xml, "autor-aktu").Single();
            Assert.Equal("1", Assert.Single(issuer.Elements(), e => e.Name.LocalName == "odnosnik").Attribute("symbol")?.Value);
            Assert.StartsWith("MINISTRA FINANSÓW", issuer.Value);
            Assert.DoesNotContain("[", issuer.Value);

            var basis = Named(xml, "podstawa-prawna").Single();
            Assert.Equal("2", Assert.Single(basis.Elements(), e => e.Name.LocalName == "odnosnik").Attribute("symbol")?.Value);
            Assert.DoesNotContain("[", basis.Value);
            Assert.EndsWith(") zarządza się, co następuje:", basis.Value);
        }

        [Fact]
        public void FootnoteAtTheIssuingAuthority_StaysOutOfTheNameOfTheAuthority()
        {
            var xml = LegalDocumentConverter.Convert(Regulation());

            var authority = Named(xml, "Organ").Single();
            Assert.Equal("Minister Finansów", authority.Elements().Single(e => e.Name.LocalName == "Nazwa").Value);
        }

        [SchemaPlFact]
        public void ActWithFootnotesInTheTitleZoneAndAHeading_IsSchemaValid()
        {
            Assert.Empty(SchemaPl.Validator.ValidateActBody(LegalDocumentConverter.Convert(Regulation()).Elements().First()));
            Assert.Empty(SchemaPl.Validator.ValidateActBody(LegalDocumentConverter.Convert(Parse(
                "USTAWA",
                "z dnia 14 czerwca 2024 r.",
                "o ochronie sygnalistów",
                "Rozdział 1",
                "Przepisy ogólne{1}",
                "Art. 1. Ustawa określa zasady.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.")).Elements().First()));
        }

        [SchemaPlFact]
        public void ActWithFootnotes_IsSchemaValid()
        {
            var act = LegalDocumentConverter.Convert(Act()).Elements().First();

            Assert.Empty(SchemaPl.Validator.ValidateActBody(act));
        }
    }
}
