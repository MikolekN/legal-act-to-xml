using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Treść zebrana pod przepisem, który niczego nie zmienia (ADR-0071): tekst w cudzysłowie jest przytoczeniem
    /// (<c>tresc-strukt</c>), tekst bez cudzysłowu i bez jednostek — dalszą treścią przepisu. Pod przepisem
    /// zmieniającym ta sama treść zostaje nowelizacją.
    /// </summary>
    public class QuotedTextInProvisionTests
    {
        private const string Article = "ARTartustawynprozporzdzenia";
        private const string Paragraph = "USTustnpkodeksu";
        private const string Point = "PKTpunkt";
        private const string QuoteStyle = "CYTcytatnpprzysigi";
        private const string QuotedPoint = "ZPKTzmpktartykuempunktem";
        private const string QuotedFragment = "ZFRAGzmfragmentunpzdaniaartykuempunktem";

        private static DocumentBlock Block(string text, string? style = null) => new() { Text = text, StyleId = style };

        private static (LegalDocument Document, XElement Xml) Export(bool journalHeader, params DocumentBlock[] body)
        {
            var blocks = new List<DocumentBlock>();
            if (journalHeader)
            {
                blocks.Add(new() { Text = "DZIENNIK USTAW RZECZYPOSPOLITEJ POLSKIEJ", Role = BlockRole.JournalHeader });
                blocks.Add(new() { Text = "Warszawa, dnia 24 czerwca 2024 r.", Role = BlockRole.JournalHeader });
                blocks.Add(new() { Text = "Poz. 928", Role = BlockRole.JournalHeader });
            }

            blocks.AddRange([Block("USTAWA"), Block("z dnia 14 czerwca 2024 r."), Block("o ośrodkach szkolenia")]);
            blocks.AddRange(body);
            blocks.Add(Block("Art. 9. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.", Article));

            var document = LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
            return (document, LegalDocumentConverter.Convert(document));
        }

        // Korpus: 339881_3019379 — wiersz otwierający w stylu cytatu, punkty w stylu zmiany, cudzysłów zamykający
        // na końcu ostatniego punktu.
        private static readonly DocumentBlock[] Declaration =
        [
            Block("Art. 1. 1. Do wniosku o wpis do rejestru przedsiębiorca dołącza:", Article),
            Block("1) dowód uiszczenia opłaty;", Point),
            Block("2) pisemne oświadczenie następującej treści:", Point),
            Block("„Oświadczam, że:", QuoteStyle),
            Block("1) dane zawarte we wniosku są kompletne i zgodne z prawdą;", QuotedPoint),
            Block("2) znane mi są warunki wykonywania działalności.”.", QuotedPoint),
            Block("2. Oświadczenie składa się pod rygorem odpowiedzialności karnej.", Paragraph),
        ];

        private static IEnumerable<XElement> Named(XElement xml, string name) =>
            xml.Descendants().Where(e => e.Name.LocalName == name);

        [Fact]
        public void QuotedDeclaration_IsQuotedContentOfThePoint_NotAnAmendment()
        {
            var (document, xml) = Export(false, Declaration);

            var point = document.Articles.First().Paragraphs[0].Points[1];
            Assert.Null(point.Amendment);
            var quotation = Assert.Single(point.Continuation.Select(block => block.Quotation).OfType<Quotation>());
            Assert.Equal("Oświadczam, że:", quotation.Content.PlainText);
            Assert.Equal(["1", "2"], quotation.Content.Points.Select(p => p.Number!.Value));
            Assert.True(quotation.HasOpeningQuote && quotation.HasClosingQuote);

            // W zdaniu punktu, jak rota ślubowania w przykładzie autorów schematu: wiersz otwierający, potem punkty.
            Assert.Empty(Named(xml, "cytat-strukt"));
            Assert.Empty(Named(xml, "cytat-w-tekscie"));
            var quoted = Assert.Single(Named(xml, "tresc-strukt"));
            Assert.Equal("oba", quoted.Attribute("cudzyslowy")?.Value);
            Assert.Equal(["p", "punkt", "punkt"], quoted.Elements().Select(e => e.Name.LocalName));
            Assert.Equal("Oświadczam, że:", quoted.Elements().First().Value);
            Assert.Equal("zdanie", quoted.Parent!.Name.LocalName);
            Assert.EndsWith(".", quoted.Parent.Value);

            // Przytoczony tekst nie jest jednostką żadnego aktu — bez adresów.
            Assert.All(quoted.Descendants(), e => Assert.Null(e.Attribute("eId")));
        }

        [SchemaPlFact]
        public void QuotedDeclaration_IsSchemaValid()
        {
            var (_, xml) = Export(true, Declaration);

            Assert.Empty(SchemaPl.Validator.Validate(xml));
        }

        // Korpus: 299146_2684028 — rota ślubowania w jednym akapicie.
        private static readonly DocumentBlock[] Oath =
        [
            Block("Art. 1. 1. Prezes sądu odbiera od komornika ślubowanie według następującej roty:", Article),
            Block("„Ślubuję uroczyście jako komornik powierzone mi obowiązki wypełniać zgodnie z prawem.”."),
            Block("2. Składający ślubowanie może dodać na końcu zwrot: „Tak mi dopomóż Bóg.”.", Paragraph),
        ];

        [Fact]
        public void QuotationInASingleParagraph_IsQuotedContentOfTheProvision()
        {
            var (document, xml) = Export(false, Oath);

            var paragraphs = document.Articles.First().Paragraphs;
            Assert.Equal(["1", "2"], paragraphs.Select(paragraph => paragraph.Number!.Value));
            var quotation = Assert.Single(paragraphs[0].Continuation.Select(block => block.Quotation).OfType<Quotation>());
            Assert.True(quotation.HasOpeningQuote && quotation.HasClosingQuote);

            var quoted = Assert.Single(Named(xml, "tresc-strukt"));
            Assert.Equal("Ślubuję uroczyście jako komornik powierzone mi obowiązki wypełniać zgodnie z prawem.", quoted.Value);
            Assert.EndsWith(".", quoted.Parent!.Value);
            Assert.Empty(Named(xml, "fragment"));
        }

        [Theory]
        // Wyrażenie w cudzysłowie na początku akapitu domyka się przed końcem — akapit nie jest cytatem.
        [InlineData("„Komornik” oznacza także asesora; tak samo „aplikant”.")]
        // Cytat bez cudzysłowu zamykającego na końcu.
        [InlineData("„Ślubuję uroczyście” — tak brzmi początek roty.")]
        public void ParagraphThatIsNotOneWholeQuotation_StaysText(string text)
        {
            var (document, xml) = Export(false,
                Block("Art. 1. 1. Prezes sądu odbiera od komornika ślubowanie według następującej roty:", Article),
                Block(text),
                Block("2. Ślubowanie składa się w siedzibie sądu.", Paragraph));

            Assert.Empty(Named(xml, "tresc-strukt"));
            Assert.Equal([text], document.Articles.First().Paragraphs[0].Continuation.Select(block => block.Text));
        }

        [SchemaPlFact]
        public void QuotationInASingleParagraph_IsSchemaValid()
        {
            var (_, xml) = Export(true, Oath);

            Assert.Empty(SchemaPl.Validator.Validate(xml));
        }

        [Fact]
        public void QuoteOpenedAndNotClosed_KeepsOnlyTheOpeningMark()
        {
            // Korpus: 322154_2888226 — klauzula „Świadomy odpowiedzialności karnej…" bez cudzysłowu zamykającego.
            var (_, xml) = Export(false,
                Block("Art. 1. Do wniosku dołącza się oświadczenie o następującej treści:", Article),
                Block("„Świadomy odpowiedzialności karnej za złożenie fałszywego oświadczenia oświadczam, że dane są prawdziwe.", QuotedFragment));

            var quoted = Assert.Single(Named(xml, "tresc-strukt"));
            Assert.Equal("otwierajacy", quoted.Attribute("cudzyslowy")?.Value);
            Assert.StartsWith("Świadomy odpowiedzialności karnej", quoted.Value);
        }

        [Fact]
        public void ClosingQuotePairedInTheLastLine_IsTextOfTheLine_NotTheEndOfAQuotation()
        {
            // Korpus: 321651_2968750 — legenda wzoru kończy się wyrazem w cudzysłowie („n"), nie zamknięciem cytatu.
            var (document, xml) = Export(false,
                Block("Art. 1. Rozliczenia kosztów dokonuje się zgodnie ze wzorem:", Article),
                Block("KPn = ∑KP × KOn", QuotedFragment),
                Block("gdzie poszczególne symbole oznaczają:", QuotedFragment),
                Block("KOn – klucz ogólny do działania „n”,", QuotedFragment));

            Assert.Empty(Named(xml, "tresc-strukt"));
            Assert.Equal(
                ["KPn = ∑KP × KOn", "gdzie poszczególne symbole oznaczają:", "KOn – klucz ogólny do działania „n”,"],
                document.Articles.First().Paragraphs[0].Continuation.Select(block => block.Text));
        }

        // Korpus: 329304_2949179 — oświadczenie w stylu cytatu, druga pozycja omyłkowo w stylu litery, a po nim kolejny
        // punkt aktu. Przedtem pozycje oświadczenia były punktami aktu o adresach jego punktów (ADR-0076).
        private static readonly DocumentBlock[] DeclarationInQuotationStyle =
        [
            Block("Art. 1. 1. Wytwórca dołącza do wniosku o wpis do rejestru wytwórców:", Article),
            Block("1) oświadczenie następującej treści:", Point),
            Block("„Oświadczam, że:", QuoteStyle),
            Block("1) dane zawarte we wniosku są kompletne i zgodne z prawdą;", QuoteStyle),
            Block("2) znane mi są warunki wykonywania działalności.”;", "LITlitera"),
            Block("2) oświadczenie o niekaralności wytwórcy.", Point),
            Block("2. Oświadczenia, o których mowa w ust. 1, powinny również zawierać podpis.", Paragraph),
        ];

        [Fact]
        public void QuoteOpenedUnderTheProvision_RunsToItsClosingMark_WhateverTheStyles()
        {
            var (document, xml) = Export(false, DeclarationInQuotationStyle);

            var paragraph = document.Articles.First().Paragraphs[0];
            Assert.Equal(["1", "2"], paragraph.Points.Select(point => point.Number!.Value));
            var quotation = Assert.Single(paragraph.Points[0].Continuation.Select(block => block.Quotation).OfType<Quotation>());
            Assert.Equal("Oświadczam, że:", quotation.Content.PlainText);
            Assert.Equal(["1", "2"], quotation.Content.Points.Select(point => point.Number!.Value));

            Assert.Equal("oba", Assert.Single(Named(xml, "tresc-strukt")).Attribute("cudzyslowy")?.Value);
            Assert.Equal(2, document.Articles.First().Paragraphs.Count);
        }

        [SchemaPlFact]
        public void QuoteOpenedUnderTheProvision_IsSchemaValid()
        {
            var (_, xml) = Export(true, DeclarationInQuotationStyle);

            Assert.Empty(SchemaPl.Validator.Validate(xml));
        }

        [Fact]
        public void QuoteOpenedUnderTheProvision_WithoutStyles_IsQuotedContentToo()
        {
            var (document, _) = Export(false, DeclarationInQuotationStyle.Select(block => block with { StyleId = null }).ToArray());

            var paragraph = document.Articles.First().Paragraphs[0];
            Assert.Equal(["1", "2"], paragraph.Points.Select(point => point.Number!.Value));
            Assert.Single(paragraph.Points[0].Continuation.Select(block => block.Quotation).OfType<Quotation>());
        }

        [Fact]
        public void QuoteLeftOpenUnderTheProvision_EndsAtTheNextArticle()
        {
            // Cudzysłowu zamykającego zabrakło w źródle — przytoczenie nie przechodzi przez artykuły.
            var (document, _) = Export(false,
                Block("Art. 1. Przedsiębiorca składa oświadczenie następującej treści:", Article),
                Block("„Oświadczam, że:", QuoteStyle),
                Block("1) dane są kompletne;", QuoteStyle),
                Block("2) znane mi są warunki działalności.", QuoteStyle),
                Block("Art. 2. Oświadczenie składa się pod rygorem odpowiedzialności karnej.", Article));

            Assert.Equal(["1", "2", "9"], document.Articles.Select(article => article.Number!.Value));
            var quotation = Assert.Single(document.Articles.First().Paragraphs[0].Continuation.Select(block => block.Quotation).OfType<Quotation>());
            Assert.False(quotation.HasClosingQuote);
        }

        [Fact]
        public void QuotedContentUnderAnAmendingProvision_IsStillAnAmendment()
        {
            var (_, xml) = Export(false,
                Block("Art. 1. W ustawie z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) w art. 5 ust. 2 otrzymuje brzmienie:", Article),
                Block("„2. Opłatę wnosi się w terminie 14 dni.”.", "ZUSTzmustartykuempunktem"));

            Assert.Empty(Named(xml, "tresc-strukt"));
            Assert.Equal("DU/2021/200__art_5__ust_2", Assert.Single(Named(xml, "cytat-strukt")).Attribute("element-eId")?.Value);
        }
    }
}
