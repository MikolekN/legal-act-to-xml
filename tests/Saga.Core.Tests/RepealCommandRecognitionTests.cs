using System.Xml.Linq;
using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Core.Services.Parsing;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Czasownik uchylenia („uchyla się", „skreśla się") jest komendą w artykule zmieniającym inny akt albo wtedy,
    /// gdy zaraz za nim stoi uchylana jednostka — w przepisie merytorycznym komendą nie jest (ADR-0069).
    /// </summary>
    public class RepealCommandRecognitionTests
    {
        private static XElement Export(params string[] provisions)
        {
            string[] lines =
            [
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o szkolnictwie wyższym",
                .. provisions,
                "Art. 9. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
            ];
            var document = LegalDocumentParser.Parse(
                lines.Select(line => new DocumentBlock { Text = line }).ToList(),
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

            return LegalDocumentConverter.Convert(document);
        }

        private static List<XElement> Repeals(XElement xml) =>
            xml.Descendants().Where(e => e.Name.LocalName == "uchyl").ToList();

        private static string? Unit(XElement repeal) => repeal.Attribute("element-eId")?.Value;

        [Theory]
        // Korpus (teksty jednolite): 299147_2705748, 275043_2474596, 300758_2692741, 302302_2702754, 307517_2786682, 305230_2740431.
        [InlineData("Art. 1. Studenta skreśla się z listy studentów w przypadku niepodjęcia studiów.")]
        [InlineData("Art. 1. Kto uchyla się od wykonania obowiązku, o którym mowa w art. 5 ust. 2, podlega karze grzywny.")]
        [InlineData("Art. 1. Decyzję, o której mowa w ust. 10, uchyla się, jeżeli prowadzący instalację wprowadzi zmiany.")]
        [InlineData("Art. 1. Tymczasowe zawieszenie uchyla się niezwłocznie, jeżeli ustaną przyczyny, wskutek których zostało ono zastosowane.")]
        [InlineData("Art. 1. Orzeczenia nie uchyla się z tego tylko powodu, że sąd orzekł w postępowaniu karnym.")]
        [InlineData("Art. 1. Osobę wpisaną na listę, o której mowa w art. 113 ust. 1 i 2, skreśla się z danej listy.")]
        // Zdanie stojące obok w tej samej jednostce też nie dostaje instrukcji.
        [InlineData("Art. 1. Strona może odmówić przyjęcia protokołu. Kto uchyla się od jego odbioru, ponosi koszty doręczenia.")]
        public void RepealVerbInAnOrdinaryProvision_IsNotACommand(string provision)
        {
            Assert.Empty(Repeals(Export(provision)));
        }

        [Fact]
        public void RepealVerbInAnOrdinaryProvision_DoesNotRepealTheUnitItRefersTo()
        {
            // Korpus: 315420_2831167 — „z zastrzeżeniem ust. 2" dawało uchylenie ust. 2 tego samego aktu.
            var xml = Export(
                "Art. 1. 1. Zezwolenie, o którym mowa w ust. 2, uchyla się w przypadku ustania przesłanek.",
                "2. Zezwolenie wydaje minister.");

            Assert.Empty(Repeals(xml));
        }

        [Fact]
        public void RepealOfANamedUnit_IsACommand_AlsoOutsideTheAmendingFormula()
        {
            // Przepis nie zaczyna się od „W ustawie z dnia …" — komendę poznaje się po jednostce za czasownikiem.
            var xml = Export("Art. 1. W art. 5 ustawy z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) uchyla się ust. 3.");

            Assert.Equal("art_5__ust_3", Unit(Assert.Single(Repeals(xml))));
        }

        [Fact]
        public void AnythingRepealedInAnAmendingArticle_IsACommand()
        {
            // W artykule zmieniającym przedmiotem bywa część załącznika, której adresu nie znamy — komenda zostaje,
            // jako instrukcja ręczna z adresem załącznika (ADR-0100).
            // Korpus: 327744_2958127 („uchyla się wiersz oznaczony lp. 16"), 321550_2998819 („skreśla się lp. 7").
            var xml = Export(
                "Art. 1. W ustawie z dnia 1 lutego 2021 r. o opłatach (Dz. U. poz. 200) wprowadza się następujące zmiany:",
                "1) w art. 3 uchyla się ust. 2;",
                "2) w załączniku nr 1 w tabeli skreśla się wiersz oznaczony lp. 16.");

            Assert.Equal(["DU/2021/200__art_3__ust_2"], Repeals(xml).Select(Unit));
            var manual = Assert.Single(xml.Descendants(), e => e.Attribute("funkcja")?.Value == "uchyl-recznie");
            Assert.Equal("DU/2021/200__zal_1", Unit(manual));
            Assert.Equal("wiersz oznaczony lp. 16", manual.Value);
        }

        [Theory]
        [InlineData("osoba usamodzielniana bez uzasadnionej przyczyny uchyla się od podjęcia proponowanego jej zatrudnienia;")]
        [InlineData("jeżeli uchyla się on od wykonania obowiązków określonych w decyzji")]
        [InlineData("Policjanta skreśla się ze szkolenia w przypadku jego śmierci.")]
        [InlineData("W postępowaniu przed organem wyższego stopnia nie uchyla się decyzji w całości.")]
        public void RepealVerbInItsOrdinaryMeaning_IsNotACommandVerb(string text)
        {
            Assert.False(AmendmentCommandParser.HasRepealVerb(text));
        }

        [Theory]
        [InlineData("w art. 15 uchyla się pkt 3;")]
        [InlineData("skreśla się lp. 7;")]
        // Sam czasownik zostaje kandydatem — o komendzie rozstrzyga dopiero miejsce przepisu albo jego przedmiot.
        [InlineData("Kuratelę uchyla się na żądanie osoby niepełnosprawnej.")]
        public void RepealVerbWithoutAPrepositionOrNegation_IsACandidate(string text)
        {
            Assert.True(AmendmentCommandParser.HasRepealVerb(text));
        }

        [Theory]
        [InlineData("uchyla się ust. 3;", true)]
        [InlineData("w art. 10 w ust. 5 uchyla się zdanie drugie;", true)]
        [InlineData("w ust. 6 uchyla się drugie zdanie,", true)]
        [InlineData("uchyla się rozdział 2a;", true)]
        [InlineData("uchyla się podwójne tiret trzecie i podwójne tiret czwarte,", true)]
        [InlineData("w art. 44 uchyla się pkt 2–7;", true)]
        [InlineData("uchyla się § 21.", true)]
        // Jednostka powołana gdzie indziej w zdaniu nie jest przedmiotem czasownika.
        [InlineData("Decyzję, o której mowa w ust. 10, uchyla się, jeżeli prowadzący instalację wprowadzi zmiany, o których mowa w ust. 4.", false)]
        [InlineData("uchyla się decyzję o udzieleniu pomocy.", false)]
        [InlineData("Kuratelę uchyla się na żądanie osoby niepełnosprawnej.", false)]
        // Nazwa jednostki bez oznaczenia to zwykły rzeczownik („tytuł wykonawczy", „część decyzji").
        [InlineData("uchyla się tytuł wykonawczy w całości", false)]
        [InlineData("uchyla się część decyzji dotkniętą wadą", false)]
        // Część załącznika nie jest jednostką redakcyjną — tu rozstrzyga artykuł zmieniający.
        [InlineData("skreśla się lp. 7;", false)]
        public void UnitRightAfterTheVerb_IsWhatIsRepealed(string text, bool expected)
        {
            Assert.Equal(expected, AmendmentCommandParser.RepealsNamedUnit(text));
        }
    }
}
