using Saga.Core.Helpers;
using Saga.Core.Services.Parsing;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Testy parsera komend nowelizacyjnych (Etap 8b, § 82-97 ZTP): rozpoznanie rodzaju komendy
    /// i jednostki-przedmiotu z tokenu przy czasowniku; zamiana wyrazów z ekstrakcją brzmień.
    /// </summary>
    public class AmendmentCommandParserTests
    {
        [Theory]
        [InlineData("1) art. 5 otrzymuje brzmienie:", AmendmentTargetKind.Article, "5")]
        [InlineData("2) w art. 15 ust. 2 otrzymuje brzmienie:", AmendmentTargetKind.Paragraph, "2")]
        [InlineData("3) w art. 5 ust. 2 pkt 3 otrzymuje brzmienie:", AmendmentTargetKind.Point, "3")]
        [InlineData("4) w art. 5 pkt 3 lit. b otrzymuje brzmienie:", AmendmentTargetKind.Letter, "b")]
        [InlineData("§ 2 otrzymuje brzmienie:", AmendmentTargetKind.Article, "2")]
        public void ChangeCommand_TargetsUnitAdjacentToVerb(string text, AmendmentTargetKind kind, string number)
        {
            var cmd = AmendmentCommandParser.Parse(text);

            Assert.NotNull(cmd);
            Assert.Equal(AmendmentCommandKind.Change, cmd!.Kind);
            Assert.Equal(kind, cmd.TargetKind);
            Assert.Equal(number, cmd.TargetNumber);
        }

        [Fact]
        public void AddCommand_TargetsAddedUnit_NotAnchor()
        {
            var cmd = AmendmentCommandParser.Parse("3) po art. 5 dodaje się art. 5a w brzmieniu:");

            Assert.NotNull(cmd);
            Assert.Equal(AmendmentCommandKind.Add, cmd!.Kind);
            Assert.Equal(AmendmentTargetKind.Article, cmd.TargetKind);
            Assert.Equal("5a", cmd.TargetNumber);
        }

        [Fact]
        public void AddCommand_RangeDesignation_IsCaptured()
        {
            var cmd = AmendmentCommandParser.Parse("3) po art. 5 dodaje się art. 5a-5g w brzmieniu:");

            Assert.Equal(AmendmentCommandKind.Add, cmd!.Kind);
            Assert.Equal("5a-5g", cmd.TargetNumber);
        }

        [Fact]
        public void RepealCommand_TargetsRepealedUnit()
        {
            var cmd = AmendmentCommandParser.Parse("2) w art. 7 uchyla się ust. 2;");

            Assert.NotNull(cmd);
            Assert.Equal(AmendmentCommandKind.Repeal, cmd!.Kind);
            Assert.Equal(AmendmentTargetKind.Paragraph, cmd.TargetKind);
            Assert.Equal("2", cmd.TargetNumber);
        }

        [Fact]
        public void ReplaceWordsCommand_ExtractsOldAndNewWording()
        {
            var cmd = AmendmentCommandParser.Parse(
                "3) w art. 9 ust. 1 wyrazy „trzech dni” zastępuje się wyrazami „siedmiu dni”.");

            Assert.NotNull(cmd);
            Assert.Equal(AmendmentCommandKind.ReplaceWords, cmd!.Kind);
            Assert.Equal("trzech dni", cmd.OldWording);
            Assert.Equal("siedmiu dni", cmd.NewWording);
            // Jednostka wskazana przed komendą (ostatni token przed „wyrazy").
            Assert.Equal(AmendmentTargetKind.Paragraph, cmd.TargetKind);
            Assert.Equal("1", cmd.TargetNumber);
        }

        // ============================================================
        // Dodanie wyrazów przy wskazanych — zmiana w tekście jak zamiana wyrazów
        // ============================================================

        [Theory]
        // Wyrazy wskazane zastępuje się nimi samymi razem z dodanymi (jak w przykładach autorów schematu).
        [InlineData("w art. 69 po wyrazach „art. 61b ust. 6” dodaje się wyrazy „i art. 68g”.", "art. 61b ust. 6", "art. 61b ust. 6 i art. 68g")]
        [InlineData("w § 2 w ust. 1 po wyrazie „przedsiębiorcy” dodaje się wyraz „krajowemu”;", "przedsiębiorcy", "przedsiębiorcy krajowemu")]
        // Dodane wyrazy zaczynają się przecinkiem — bez odstępu przed nim, także gdy w cudzysłowie go wstawiono.
        [InlineData("w ust. 3 po wyrazie „wniosek” dodaje się wyrazy „ , o którym mowa w art. 3 ust. 1,”;", "wniosek", "wniosek, o którym mowa w art. 3 ust. 1,")]
        // „Dodaje się przecinek i wyrazy" — przecinek stoi przed dodanymi wyrazami.
        [InlineData("w pkt 2 po wyrazach „ministra właściwego do spraw informatyzacji” dodaje się przecinek i wyrazy „CSIRT sektorowymi”;", "ministra właściwego do spraw informatyzacji", "ministra właściwego do spraw informatyzacji, CSIRT sektorowymi")]
        // „Przed wyrazami" — dodane stoją pierwsze.
        [InlineData("w ust. 1 przed wyrazem „artystyczne” dodaje się wyraz „kształcenie”,", "artystyczne", "kształcenie artystyczne")]
        public void AddWordsCommand_IsATextChangeFromTheIndicatedWordsToThemWithTheAddedOnes(string text, string old, string @new)
        {
            var cmd = AmendmentCommandParser.Parse(text);

            Assert.Equal(AmendmentCommandKind.AddWords, cmd!.Kind);
            Assert.True(cmd.ChangesText);
            Assert.Equal(old, cmd.OldWording);
            Assert.Equal(@new, cmd.NewWording);
        }

        [Fact]
        public void AddWordsCommand_KeepsTheUnitIndicatedBeforeIt()
        {
            var cmd = AmendmentCommandParser.Parse("w art. 9 w ust. 4 po wyrazach „w terminie” dodaje się wyrazy „14 dni”;");

            Assert.Equal(AmendmentTargetKind.Paragraph, cmd!.TargetKind);
            Assert.Equal("4", cmd.TargetNumber);
        }

        [Theory]
        // Nowego wiersza zamianą tekstu zapisać się nie da.
        [InlineData("w ust. 2 po wyrazach „w terminie” dodaje się od nowego wiersza wyrazy „14 dni”;")]
        // Bez wskazanych wyrazów nie ma czego zastąpić („na końcu dodaje się wyrazy").
        [InlineData("w pkt 4 na końcu dodaje się wyrazy „ , lub”;")]
        public void AddedWordsWithoutAPlaceInTheText_AreNotATextChange(string text)
        {
            Assert.NotEqual(AmendmentCommandKind.AddWords, AmendmentCommandParser.Parse(text)?.Kind);
        }

        // ============================================================
        // Skreślenie wyrazów i wszystkie zmiany w tekście jednego akapitu
        // ============================================================

        [Fact]
        public void DeleteWordsCommand_IsATextChangeToNothing()
        {
            var cmd = AmendmentCommandParser.Parse("w art. 15 w ust. 2 skreśla się wyrazy „oraz informacje o zdarzeniach”;");

            Assert.Equal(AmendmentCommandKind.DeleteWords, cmd!.Kind);
            Assert.True(cmd.ChangesText);
            Assert.Equal("oraz informacje o zdarzeniach", cmd.OldWording);
            Assert.Equal(string.Empty, cmd.NewWording);
            Assert.Equal(AmendmentTargetKind.Paragraph, cmd.TargetKind);
        }

        [Fact]
        public void TextChangeCommands_ReturnsEveryChangeInTheOrderWritten()
        {
            var commands = AmendmentCommandParser.TextChangeCommands(
                "w § 2 skreśla się wyrazy „na piśmie” i po wyrazie „pracodawcy” dodaje się wyrazy „w postaci papierowej”, " +
                "a wyraz „niezwłocznie” zastępuje się wyrazami „w terminie 7 dni”;");

            Assert.Equal(
                new[] { AmendmentCommandKind.DeleteWords, AmendmentCommandKind.AddWords, AmendmentCommandKind.ReplaceWords },
                commands.Select(c => c.Kind));
            Assert.Equal(new[] { "na piśmie", "pracodawcy", "niezwłocznie" }, commands.Select(c => c.OldWording));
            Assert.Equal(new[] { "", "pracodawcy w postaci papierowej", "w terminie 7 dni" }, commands.Select(c => c.NewWording));
        }

        [Theory]
        // Komendy o jednostkach nie są zmianami w tekście.
        [InlineData("w art. 5 uchyla się ust. 2;")]
        [InlineData("po art. 5 dodaje się art. 5a w brzmieniu:")]
        [InlineData("skreśla się zdanie drugie;")]
        public void TextChangeCommands_AreEmptyForCommandsAboutUnits(string text)
        {
            Assert.Empty(AmendmentCommandParser.TextChangeCommands(text));
        }

        [Theory]
        [InlineData("Funduszu", "na wypłatę", "Funduszu na wypłatę")]
        [InlineData("Finansowego", ",", "Finansowego,")]
        [InlineData("ust. 1", "; ust. 2", "ust. 1; ust. 2")]
        [InlineData("(", "w tym", "(w tym")]
        [InlineData("art. 5", ")", "art. 5)")]
        [InlineData("", "tekst", "tekst")]
        public void JoinWords_PutsASpaceUnlessPunctuationMeetsTheWord(string left, string right, string expected)
        {
            Assert.Equal(expected, AmendmentCommandParser.JoinWords(left, right));
        }

        [Fact]
        public void ReplaceWordsCommand_SingularForm_IsRecognized()
        {
            var cmd = AmendmentCommandParser.Parse("w art. 3 wyraz „gmina” zastępuje się wyrazem „powiat”.");

            Assert.Equal(AmendmentCommandKind.ReplaceWords, cmd!.Kind);
            Assert.Equal("gmina", cmd.OldWording);
            Assert.Equal("powiat", cmd.NewWording);
        }

        [Fact]
        public void TiretTarget_IsRecognized()
        {
            var cmd = AmendmentCommandParser.Parse("w art. 5 ust. 2 pkt 3 lit. b tiret 2 otrzymuje brzmienie:");

            Assert.Equal(AmendmentCommandKind.Change, cmd!.Kind);
            Assert.Equal(AmendmentTargetKind.Tiret, cmd.TargetKind);
            Assert.Equal("2", cmd.TargetNumber);
        }

        [Fact]
        public void SentenceTarget_MapsToFragment()
        {
            var cmd = AmendmentCommandParser.Parse("w art. 11 ust. 3 zdanie drugie otrzymuje brzmienie:");

            Assert.Equal(AmendmentCommandKind.Change, cmd!.Kind);
            Assert.Equal(AmendmentTargetKind.Fragment, cmd.TargetKind);
        }

        [Theory]
        [InlineData("Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.")]
        [InlineData("1. Organ prowadzi rejestr zgłoszeń.")]
        [InlineData("Prawo do informacji podlega ograniczeniu.")]
        public void NonCommandText_ReturnsNull(string text)
            => Assert.Null(AmendmentCommandParser.Parse(text));

        [Fact]
        public void ReplaceWords_Paragraph88Form_WithInterleavingWords_IsRecognized()
        {
            // § 88 ZTP: „użyte … wyrazy „X" zastępuje się użytymi w odpowiednim przypadku wyrazami „Y"".
            var cmd = AmendmentCommandParser.Parse(
                "użyte w art. 5 w różnym przypadku wyrazy „Minister Zdrowia” zastępuje się " +
                "użytymi w odpowiednim przypadku wyrazami „minister właściwy do spraw zdrowia”;");

            Assert.NotNull(cmd);
            Assert.Equal(AmendmentCommandKind.ReplaceWords, cmd!.Kind);
            Assert.Equal("Minister Zdrowia", cmd.OldWording);
            Assert.Equal("minister właściwy do spraw zdrowia", cmd.NewWording);
        }

        [Fact]
        public void ReplaceWords_StraightQuotes_AreRecognized()
        {
            // Kanał TXT/PDF: proste cudzysłowy jako ogranicznik brzmień (Sanitize ich nie normalizuje).
            var cmd = AmendmentCommandParser.Parse("w art. 3 wyrazy \"30 dni\" zastępuje się wyrazami \"14 dni\";");

            Assert.Equal(AmendmentCommandKind.ReplaceWords, cmd!.Kind);
            Assert.Equal("30 dni", cmd.OldWording);
            Assert.Equal("14 dni", cmd.NewWording);
        }

        [Fact]
        public void TiretWordOrdinal_YieldsOrdinalValue()
        {
            var cmd = AmendmentCommandParser.Parse("w art. 5 lit. b tiret dwunaste otrzymuje brzmienie:");

            Assert.Equal(AmendmentTargetKind.Tiret, cmd!.TargetKind);
            Assert.Equal(12, cmd.TargetOrdinalValue);
        }

        [Fact]
        public void PluralTirety_DoesNotSplitDesignation()
        {
            // „tirety trzecie i czwarte" — sufiks mnogi nie może wpaść do oznaczenia (bug „y").
            var cmd = AmendmentCommandParser.Parse("dodaje się tirety trzecie i czwarte w brzmieniu:");

            Assert.Equal(AmendmentCommandKind.Add, cmd!.Kind);
            Assert.Equal(AmendmentTargetKind.Tiret, cmd.TargetKind);
            Assert.Equal("trzecie", cmd.TargetNumber);
            Assert.Equal(3, cmd.TargetOrdinalValue);
        }

        [Fact]
        public void SkreslaSie_IsRepealInFinalizerPattern()
        {
            // Historyczny czasownik uchylenia — RepealPattern dorównany do własnej dokumentacji.
            Assert.Matches(Saga.Core.Services.Parsing.AmendmentFinalizer.RepealPattern,
                "w art. 5 skreśla się ust. 2;");
        }

        [Fact]
        public void AddWithWording_TakesPrecedenceOverChange()
        {
            // Komenda „dodaje się … w brzmieniu:" zawiera też frazę zmiany — musi wygrać Add.
            var cmd = AmendmentCommandParser.Parse("po ust. 3 dodaje się ust. 4 w brzmieniu:");

            Assert.Equal(AmendmentCommandKind.Add, cmd!.Kind);
            Assert.Equal(AmendmentTargetKind.Paragraph, cmd.TargetKind);
            Assert.Equal("4", cmd.TargetNumber);
        }
    }
}
