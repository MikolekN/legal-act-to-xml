using Saga.Core.Services.Classify;
using Saga.Core.Services.EntryIntoForce;
using Saga.Core.Services.Parsing;
using Saga.Model.EntryIntoForce;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Liczebniki słowne przechodzące przez CAŁE ścieżki produkcyjne — jedyny dowód, że wzorce
    /// wyszukujące i słownik parsujący widzą ten sam zbiór.
    ///
    /// Każdy test tutaj odpowiada jednemu wstrzyknięciu wspólnej alternatywy. Testy powstały jako
    /// charakteryzacja zmierzonych cichych porażek i po naprawie zostają jako
    /// strażnicy: czerwony test w tym pliku znaczy, że któreś miejsce znów ma własne pojęcie
    /// o polskich liczebnikach.
    /// </summary>
    public class PolishNumeralsIntegrationTests
    {
        // ============================================================
        // Powołania jednostek (UnitEnumerationParser + UnitReferenceTokenizer)
        // ============================================================

        [Fact]
        public void TwoWordOrdinal_InTiretReference_IsComposed()
        {
            // „dwudzieste pierwsze" = 21. Wymaga i dwuwyrazowej gałęzi w alternatywie,
            // i tego, żeby normalizacja oznaczenia nie skleiła obu wyrazów w jeden niebyt.
            var result = UnitEnumerationParser.Parse("art. 1 pkt 2 lit. a tiret dwudzieste pierwsze");

            var target = Assert.Single(result.Targets);
            Assert.Equal("art_1__pkt_2__lit_a__tir_21", target.Structure.ToString());
            Assert.True(result.IsComplete);
        }

        [Fact]
        public void InflectedOrdinal_InTiretReference_IsResolved()
        {
            // „tiret czwartego" — dopełniacz. Tokenizer dopuszcza odmienioną końcówkę, więc słownik
            // musi ją znać; inaczej token powstaje i zaraz przepada w Unresolved.
            var result = UnitEnumerationParser.Parse("art. 1 pkt 3 lit. b tiret czwartego");

            var target = Assert.Single(result.Targets);
            Assert.Equal("art_1__pkt_3__lit_b__tir_4", target.Structure.ToString());
            Assert.Empty(result.Unresolved);
        }

        [Fact]
        public void OrdinalAboveTwenty_IsReachableFromTokenizer()
        {
            // Wartości 30–90 były w słowniku, ale rdzenie tokenizera kończyły się na „dwudziest".
            var result = UnitEnumerationParser.Parse("art. 5 pkt 1 lit. c tiret trzydzieste drugie");

            var target = Assert.Single(result.Targets);
            Assert.Equal("art_5__pkt_1__lit_c__tir_32", target.Structure.ToString());
        }

        // ============================================================
        // Komendy nowelizacyjne (AmendmentCommandParser)
        // ============================================================

        [Fact]
        public void TwoWordOrdinal_InAmendmentCommand_YieldsOrdinalValue()
        {
            var command = AmendmentCommandParser.Parse("tiret czterdzieste drugie otrzymuje brzmienie:");

            Assert.NotNull(command);
            Assert.Equal(AmendmentCommandKind.Change, command!.Kind);
            Assert.Equal(42, command.TargetOrdinalValue);
        }

        // ============================================================
        // Formuła wejścia w życie (EntryIntoForcePatterns + FormulaParser)
        // ============================================================

        [Fact]
        public void TwoWordCardinal_InEntryIntoForceFormula_YieldsOperand()
        {
            // „sześćdziesięciu jeden" = 61. Drugi człon jest NIEODMIENNY („jeden", nie „jednego") —
            // dlatego słownik liczebników głównych musi mieć oba warianty jedynki.
            var formula = EntryIntoForceFormulaParser.Parse(
                "po upływie sześćdziesięciu jeden dni od dnia ogłoszenia.");

            Assert.Equal(EntryIntoForceFormulaKind.AfterDaysFromAnnouncement, formula.Kind);
            Assert.Equal(61, formula.Operand);
        }

        [Fact]
        public void TwoWordCardinal_Genitive_YieldsOperand()
        {
            var formula = EntryIntoForceFormulaParser.Parse(
                "po upływie czterdziestu dwóch dni od dnia ogłoszenia.");

            Assert.Equal(EntryIntoForceFormulaKind.AfterDaysFromAnnouncement, formula.Kind);
            Assert.Equal(42, formula.Operand);
        }

        [Fact]
        public void SpelledOperand_InFirstDayAfterLapseOfMonths_IsRead()
        {
            // Wzorzec bliźniaczy (AfterMonths) ma gałąź słowną operandu, ten jej nie miał.
            var formula = EntryIntoForceFormulaParser.Parse(
                "pierwszego dnia miesiąca następującego po upływie trzech miesięcy od dnia ogłoszenia.");

            Assert.Equal(EntryIntoForceFormulaKind.FirstDayOfMonthAfterLapseOfMonths, formula.Kind);
            Assert.Equal(3, formula.Operand);
        }

        // ============================================================
        // Jednostki systematyzacyjne (ParagraphClassifier + RomanNumeralConverter)
        // ============================================================

        [Theory]
        // Rzeczowniki ŻEŃSKIE — działały, bo słownik miał tylko formy żeńskie.
        [InlineData("CZĘŚĆ PIERWSZA", ParagraphKind.PartUnit, "1")]
        [InlineData("KSIĘGA DRUGA", ParagraphKind.BookUnit, "2")]
        // Rzeczowniki MĘSKIE — cicha porażka: nagłówek lądował jako Unknown.
        [InlineData("TYTUŁ PIERWSZY", ParagraphKind.TitleUnit, "1")]
        [InlineData("DZIAŁ PIERWSZY", ParagraphKind.DivisionUnit, "1")]
        [InlineData("DZIAŁ TRZECI", ParagraphKind.DivisionUnit, "3")]
        public void WordOrdinalHeading_IsRecognized_RegardlessOfGrammaticalGender(
            string text, ParagraphKind expectedKind, string expectedValue)
        {
            var classification = new ParagraphClassifier().Classify(new ClassificationInput(text, null));
            Assert.Equal(expectedKind, classification.Kind);

            // eId musi być identyczny jak dla zapisu cyfrą rzymską: oba dają liczbę arabską (ADR-0018),
            // a oznaczenie z aktu zostaje w RawValue.
            var number = ParagraphClassifier.ParseSystematizingNumber(expectedKind, text);
            Assert.NotNull(number);
            Assert.Equal(expectedValue, number!.Value);
            Assert.Equal(text[(text.IndexOf(' ') + 1)..], number.RawValue);
        }

        [Fact]
        public void WordOrdinalAboveTwelve_IsRecognized()
        {
            // Słownik jednostek wyższych kończył się na „dwunasta".
            var classification = new ParagraphClassifier().Classify(
                new ClassificationInput("DZIAŁ PIĘTNASTY", null));

            Assert.Equal(ParagraphKind.DivisionUnit, classification.Kind);
            Assert.Equal("15", ParagraphClassifier.ParseSystematizingNumber(ParagraphKind.DivisionUnit, "DZIAŁ PIĘTNASTY")!.Value);
        }

        [Fact]
        public void NonNumeralHeadingWord_IsStillRejected()
        {
            // Poszerzenie wzorca jest bezpieczne wyłącznie dlatego, że walidacja po dopasowaniu
            // jest twarda: „CZĘŚĆ BUDŻETOWA" przechodzi regex, ale nie jest ani numerem, ani nazwą
            // z zamkniętej listy jednostek nazwanych („CZĘŚĆ OGÓLNA").
            var classification = new ParagraphClassifier().Classify(new ClassificationInput("CZĘŚĆ BUDŻETOWA", null));
            Assert.NotEqual(ParagraphKind.PartUnit, classification.Kind);
        }
    }
}
