using Saga.Core.Ingest;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Tekst jednolity składa jednostki dodane nowelizacją stylami zmian (Z…). Bez komendy nowelizacyjnej przed
    /// takim akapitem i bez cudzysłowu to treść samego aktu — tekst jednolity nie zmienia sam siebie. Przedtem
    /// styl Z otwierał nowelizację aktu ogłaszanego: „§ 12." znikał w cytacie pod § 11, a legenda wzoru wychodziła
    /// komendą „zastąp".
    /// </summary>
    public class AmendmentStyledActContentTests
    {
        private static DocumentBlock Block(string text, string? style = null) => new() { Text = text, StyleId = style };

        private static LegalDocument Parse(bool consolidated, params DocumentBlock[] body)
        {
            var blocks = new List<DocumentBlock>
            {
                new() { Text = "DZIENNIK USTAW RZECZYPOSPOLITEJ POLSKIEJ", Role = BlockRole.JournalHeader },
                new() { Text = "Warszawa, dnia 11 lutego 2026 r.", Role = BlockRole.JournalHeader },
                new() { Text = "Poz. 152", Role = BlockRole.JournalHeader },
            };

            if (consolidated)
            {
                blocks.AddRange(new[]
                {
                    "Obwieszczenie",
                    "Ministra Finansów",
                    "z dnia 2 lutego 2026 r.",
                    "w sprawie ogłoszenia jednolitego tekstu rozporządzenia Ministra Finansów w sprawie właściwości organów podatkowych",
                    "1. Na podstawie art. 16 ust. 1 ustawy z dnia 20 lipca 2000 r. o ogłaszaniu aktów normatywnych ogłasza się w załączniku do niniejszego obwieszczenia jednolity tekst rozporządzenia Ministra Finansów z dnia 10 grudnia 2021 r. w sprawie właściwości organów podatkowych (Dz. U. poz. 2299).",
                    "Minister Finansów: A. Domański",
                    "Załącznik do obwieszczenia Ministra Finansów z dnia 2 lutego 2026 r. (Dz. U. poz. 152)",
                }.Select(line => Block(line)));
            }

            blocks.AddRange(new[]
            {
                "ROZPORZĄDZENIE",
                "MINISTRA FINANSÓW",
                "z dnia 10 grudnia 2021 r.",
                "w sprawie właściwości organów podatkowych",
                "Na podstawie art. 18 ustawy z dnia 29 sierpnia 1997 r. – Ordynacja podatkowa zarządza się, co następuje:",
            }.Select(line => Block(line)));
            blocks.AddRange(body);
            blocks.Add(Block("§ 20. Rozporządzenie wchodzi w życie z dniem 1 stycznia 2022 r."));
            return LegalDocumentParser.Parse(blocks, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;
        }

        private static IEnumerable<object> Amendments(LegalDocument document) =>
            document.Articles.SelectMany(article => article.Paragraphs)
                .SelectMany(paragraph => new object?[] { paragraph.Amendment }.Concat(paragraph.Points.Select(point => (object?)point.Amendment)))
                .OfType<object>();

        [Fact]
        public void BasicUnitInAmendmentStyle_InAConsolidatedText_IsAUnitOfTheAct()
        {
            // Korpus: 352501_3188638 — „§ 12." w stylu artykułu nowelizacji (Z/ART) za § 11 tekstu jednolitego.
            var document = Parse(consolidated: true,
                Block("§ 11. 1. Organem podatkowym właściwym w sprawach podatku dochodowego jest naczelnik urzędu skarbowego.", "ARTartustawynprozporzdzenia"),
                Block("2. Przepis ust. 1 stosuje się odpowiednio do płatników.", "USTustnpkodeksu"),
                Block("§ 12. 1. Organem podatkowym właściwym w sprawach zaliczek na podatek dochodowy:", "ZARTzmartartykuempunktem"),
                Block("1) od osób fizycznych – naczelnik urzędu skarbowego według miejsca zamieszkania;", "PKTpunkt"),
                Block("2) od osób prawnych – naczelnik urzędu skarbowego według adresu siedziby.", "PKTpunkt"),
                Block("2. Przepis ust. 1 stosuje się odpowiednio do płatników.", "USTustnpkodeksu"));

            Assert.Equal(new[] { "11", "12", "20" }, document.Articles.Select(article => article.Number?.Value));
            Assert.Equal(2, document.Articles.ElementAt(1).Paragraphs[0].Points.Count);
            Assert.Empty(Amendments(document));
        }

        [Fact]
        public void FormulaLegendInAmendmentStyles_InAConsolidatedText_StaysInTheProvision()
        {
            // Korpus: 352078_3158919 — legenda wzoru i wyliczenia miesięcy w stylach zmian (wstawione nowelizacją).
            var document = Parse(consolidated: true,
                Block("§ 1. 1. Rekompensata przysługuje za każdy miesiąc.", "ARTartustawynprozporzdzenia"),
                Block("2. Wysokość rekompensaty ustala się zgodnie ze wzorem:", "USTustnpkodeksu"),
                Block("R = KF – KT", "ZWMATFIZCHEMzmwzorumatfizlubchemartykuempunktem"),
                Block("gdzie:", "USTustnpkodeksu"),
                Block("KF – oznacza faktyczny koszt zakupu,", "ZLEGWMATFIZCHEMzmlegendywzorumatfizlubchemartykuempunktem"),
                Block("KT – oznacza koszt uzasadniony, stanowiący dla miesiąca:", "ZLEGWMATFIZCHEMzmlegendywzorumatfizlubchemartykuempunktem"),
                Block("1) stycznia – 15 % tej ilości;", "ZCZWSPLITwPKTzmczciwsplitwpktartykuempunktem"),
                Block("2) lutego – 15 % tej ilości.", "ZCZWSPLITwPKTzmczciwsplitwpktartykuempunktem"),
                Block("3. Rekompensata jest wypłacana na wniosek.", "USTustnpkodeksu"));

            Assert.Equal(new[] { "1", "2", "3" }, document.Articles.First().Paragraphs.Select(paragraph => paragraph.Number?.Value));
            Assert.Empty(Amendments(document));
        }

        [Fact]
        public void AmendmentStyleAfterACommand_InAConsolidatedText_IsStillTheAmendment()
        {
            // Przepis zmieniający zachowany w tekście jednolitym: za komendą styl Z nadal niesie treść cytowaną.
            var document = Parse(consolidated: true,
                Block("§ 1. W rozporządzeniu Ministra Finansów z dnia 5 maja 2020 r. w sprawie ewidencji (Dz. U. poz. 800) § 2 otrzymuje brzmienie:", "ARTartustawynprozporzdzenia"),
                Block("„§ 2. Ewidencję prowadzi się w postaci elektronicznej.”.", "ZARTzmartartykuempunktem"));

            Assert.Single(Amendments(document));
        }

        [Fact]
        public void BasicUnitInAmendmentStyle_OutsideAConsolidatedText_StaysTheQuotedContent()
        {
            // Poza tekstem jednolitym styl Z bez rozpoznanej komendy pozostaje sygnałem treści cytowanej.
            var document = Parse(consolidated: false,
                Block("§ 1. W rozporządzeniu Ministra Finansów z dnia 5 maja 2020 r. w sprawie ewidencji (Dz. U. poz. 800) wprowadza się zmianę polegającą na nadaniu § 2 nowej treści, jak niżej", "ARTartustawynprozporzdzenia"),
                Block("§ 2. Ewidencję prowadzi się w postaci elektronicznej.", "ZARTzmartartykuempunktem"));

            Assert.DoesNotContain(document.Articles, article => article.Number?.Value == "2");
        }
    }
}
