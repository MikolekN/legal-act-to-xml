using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Core.Services.Converters;
using Saga.Model;
using Saga.Model.SystematizingUnits;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Jednostki systematyzacyjne w eksporcie schemaPL — kształt jak w przykładach autorów schematu
    /// (akn4pl/przyklady): rozdzial[eId] → naglowek-rozdzialu (nr, p) → oddzial-n[brak-eid] → artykul.
    /// </summary>
    public class SystematizingUnitXmlConverterTests
    {
        private static readonly DateTime Effective = new(2026, 1, 1);

        private static T Dated<T>(T unit) where T : BaseEntity, ISystematizingUnit
        {
            unit.Number = new EntityNumber { Value = "1", RawValue = "I", NumericPart = 1 };
            unit.Heading = "Przepisy ogólne";
            unit.IsImplicit = false;
            unit.EffectiveDate = Effective;
            return unit;
        }

        [Fact]
        public void DatedUnit_EmitsExactlyOneEntryIntoForceAttribute()
        {
            BaseEntity[] units = [Dated(new Part()), Dated(new Book()), Dated(new Title()),
                Dated(new Division()), Dated(new Chapter()), Dated(new Subchapter())];

            foreach (var unit in units)
            {
                var element = SystematizingUnitXmlConverter.ToXml(unit, actLevel: false);
                var dates = element.Attributes().Where(a => a.Name.LocalName == XmlConverterConstants.Contents.EntryIntoForce).ToList();

                Assert.True(dates.Count == 1, $"{element.Name.LocalName}: oczekiwano jednego atrybutu wejscie-w-zycie, jest {dates.Count}");
                Assert.Equal("2026-01-01", dates[0].Value);
            }
        }

        [Fact]
        public void ExplicitUnit_HasEIdAndHeaderWithDesignationAsWrittenAndTitle()
        {
            var chapter = Dated(new Chapter());
            chapter.Number = new EntityNumber { Value = "4", RawValue = "IV", NumericPart = 4, Superscript = "1" };

            var element = SystematizingUnitXmlConverter.ToXml(chapter, actLevel: true);
            var ns = element.Name.Namespace;
            var header = element.Element(ns + "naglowek-rozdzialu")!;

            Assert.Equal("rozdzial", element.Name.LocalName);
            Assert.Equal("rozdz_4[1]", element.Attribute("eId")?.Value);
            Assert.Equal(new[] { "nr", "p" }, header.Elements().Select(e => e.Name.LocalName));
            Assert.Equal("IV[1]", header.Element(ns + "nr")?.Value);
            Assert.Equal("Przepisy ogólne", header.Element(ns + "p")?.Value);
        }

        [Fact]
        public void UnitWithoutTitle_HasHeaderWithNumberOnly()
        {
            var chapter = Dated(new Chapter());
            chapter.Heading = string.Empty;

            var header = SystematizingUnitXmlConverter.ToXml(chapter, actLevel: true).Elements().First();

            Assert.Equal(new[] { "nr" }, header.Elements().Select(e => e.Name.LocalName));
        }

        [Fact]
        public void ImplicitUnit_IsMinusN_WithMissingEIdReason_AndNoHeader()
        {
            var subchapter = new Subchapter { IsImplicit = true };

            var element = SystematizingUnitXmlConverter.ToXml(subchapter, actLevel: false);

            Assert.Equal("oddzial-n", element.Name.LocalName);
            Assert.Null(element.Attribute("eId"));
            Assert.Equal("jednostka-nieoznaczona", element.Attribute("brak-eid")?.Value);
            Assert.Empty(element.Elements());
        }

        [Fact]
        public void Title_IsTytulJednostkaAtActLevel_AndTytulUnderABook()
        {
            Assert.Equal("tytul-jednostka", SystematizingUnitXmlConverter.ToXml(Dated(new Title()), actLevel: true).Name.LocalName);
            Assert.Equal("tytul", SystematizingUnitXmlConverter.ToXml(Dated(new Title()), actLevel: false).Name.LocalName);
        }

        // ============================================================
        // Cały eksport
        // ============================================================

        private static XElement Export(params string[] lines) =>
            LegalDocumentConverter.Convert(LegalDocumentParser.ParseBlocks(
                lines.Select(line => new DocumentBlock { Text = line }).ToList()));

        private static List<string> Structure(XElement xml)
        {
            var act = xml.Descendants().First(e => e.Name.LocalName == "ustawa");
            var units = new HashSet<string> { "czesc", "ksiega", "tytul-jednostka", "tytul", "dzial", "rozdzial", "oddzial",
                "czesc-n", "ksiega-n", "tytul-n", "dzial-n", "rozdzial-n", "oddzial-n", "artykul" };
            return act.Descendants()
                .Where(e => units.Contains(e.Name.LocalName) && (e.Name.LocalName != "tytul" || e.Attribute("eId") != null))
                .Select(e => string.Join('/', e.AncestorsAndSelf().TakeWhile(a => a != act)
                    .Select(a => a.Name.LocalName).Reverse()) + (e.Attribute("eId") is { } id ? $"[{id.Value}]" : ""))
                .ToList();
        }

        [Fact]
        public void ActWithoutExplicitUnits_ExportsArticlesDirectlyInTheAct()
        {
            var structure = Structure(Export("Art. 1. Treść.", "Art. 2. Treść."));

            Assert.Equal(new[] { "artykul[art_1]", "artykul[art_2]" }, structure);
        }

        [Fact]
        public void Chapters_NestArticlesInImplicitSubchapters_ArticleBeforeFirstChapterStaysInTheAct()
        {
            var structure = Structure(Export(
                "Art. 1. Przed rozdziałem.",
                "Rozdział 1", "Przepisy ogólne", "Art. 2. Treść.",
                "Rozdział 2", "Przepisy końcowe", "Oddział 1", "Zasady", "Art. 3. Treść."));

            Assert.Equal(new[]
            {
                "artykul[art_1]",
                "rozdzial[rozdz_1]", "rozdzial/oddzial-n", "rozdzial/oddzial-n/artykul[art_2]",
                "rozdzial[rozdz_2]", "rozdzial/oddzial[rozdz_2__oddz_1]", "rozdzial/oddzial/artykul[art_3]",
            }, structure);
        }

        [Fact]
        public void DivisionOverChapters_EmitsImplicitLevelsBelowItAsMinusN()
        {
            var structure = Structure(Export("DZIAŁ II", "Przepisy szczególne", "Art. 5. Treść."));

            Assert.Equal(new[]
            {
                "dzial[dzial_2]", "dzial/rozdzial-n", "dzial/rozdzial-n/oddzial-n", "dzial/rozdzial-n/oddzial-n/artykul[art_5]",
            }, structure);
        }

        [Fact]
        public void BookWithTitles_NestsTytulUnderKsiega()
        {
            var structure = Structure(Export("KSIĘGA PIERWSZA", "Część ogólna", "TYTUŁ I", "Przepisy wstępne", "Art. 1. Treść."));

            Assert.Equal(new[]
            {
                "ksiega[ksi_1]", "ksiega/tytul[ksi_1__tyt_1]", "ksiega/tytul/dzial-n", "ksiega/tytul/dzial-n/rozdzial-n",
                "ksiega/tytul/dzial-n/rozdzial-n/oddzial-n", "ksiega/tytul/dzial-n/rozdzial-n/oddzial-n/artykul[art_1]",
            }, structure);
        }

        [Fact]
        public void SeveralParts_AreSiblingsInTheAct()
        {
            var structure = Structure(Export("CZĘŚĆ OGÓLNA", "Art. 1. Treść.", "CZĘŚĆ SZCZEGÓLNA", "Art. 117. Treść."));

            Assert.Equal(new[] { "czesc[czesc_OGÓLNA]", "czesc[czesc_SZCZEGÓLNA]" },
                structure.Where(s => !s.Contains('/')));
        }

        [Fact]
        public void NamedUnit_HasAnEmptyNumber_AndItsNameBeforeTheTitleInOneParagraph()
        {
            // Kodeks postępowania cywilnego (TJ 302049_2704309): „TYTUŁ WSTĘPNY" i pod nim „Przepisy ogólne".
            // Nagłówek ma jeden p i nie ma elementu tytułu, więc nazwa i tytuł stoją razem (ADR-0098).
            var xml = Export("CZĘŚĆ OGÓLNA", "Art. 1. Treść.", "TYTUŁ WSTĘPNY", "Przepisy ogólne", "Art. 2. Treść.");

            var headers = xml.Descendants().Where(e => e.Name.LocalName is "naglowek-czesci" or "naglowek-tytulu").ToList();
            Assert.Equal(new[] { ("", "OGÓLNA"), ("", "WSTĘPNY Przepisy ogólne") },
                headers.Select(h => (h.Elements().Single(e => e.Name.LocalName == "nr").Value,
                    h.Elements().Single(e => e.Name.LocalName == "p").Value)));
        }
    }
}
