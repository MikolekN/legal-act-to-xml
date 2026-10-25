using Saga.Core.Services.EntryIntoForce;
using Saga.Model.EntryIntoForce;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Rozpoznawanie formuł wejścia w życie (§ 45 ZTP). Ogony wejściowe są dosłownymi fragmentami
    /// z katalogu formuł i z korpusu.
    /// </summary>
    public class EntryIntoForceFormulaParserTests
    {
        [Theory]
        [InlineData("po upływie 14 dni od dnia ogłoszenia.", EntryIntoForceFormulaKind.AfterDaysFromAnnouncement, 14)]
        [InlineData("po upływie 30 dni od dnia ogłoszenia.", EntryIntoForceFormulaKind.AfterDaysFromAnnouncement, 30)]
        [InlineData("po upływie 90 dni od dnia ogłoszenia.", EntryIntoForceFormulaKind.AfterDaysFromAnnouncement, 90)]
        [InlineData("po upływie czternastu dni od dnia ogłoszenia.", EntryIntoForceFormulaKind.AfterDaysFromAnnouncement, 14)]
        [InlineData("po upływie 3 miesięcy od dnia ogłoszenia.", EntryIntoForceFormulaKind.AfterMonthsFromAnnouncement, 3)]
        [InlineData("po upływie 18 miesięcy od dnia ogłoszenia.", EntryIntoForceFormulaKind.AfterMonthsFromAnnouncement, 18)]
        [InlineData("po upływie miesiąca od dnia ogłoszenia.", EntryIntoForceFormulaKind.AfterMonthsFromAnnouncement, 1)]
        // § 45 ust. 1 pkt 2 wymienia dni, tygodnie, miesiące i lata — tygodnie i lata nie wystąpiły
        // w dotychczasowym korpusie, ale należą do zamkniętego katalogu formuł.
        [InlineData("po upływie 2 tygodni od dnia ogłoszenia.", EntryIntoForceFormulaKind.AfterWeeksFromAnnouncement, 2)]
        [InlineData("po upływie tygodnia od dnia ogłoszenia.", EntryIntoForceFormulaKind.AfterWeeksFromAnnouncement, 1)]
        [InlineData("po upływie 2 lat od dnia ogłoszenia.", EntryIntoForceFormulaKind.AfterYearsFromAnnouncement, 2)]
        [InlineData("po upływie roku od dnia ogłoszenia.", EntryIntoForceFormulaKind.AfterYearsFromAnnouncement, 1)]
        public void RelativeFormulas_AreRecognizedWithOperand(
            string tail, EntryIntoForceFormulaKind kind, int operand)
        {
            var formula = EntryIntoForceFormulaParser.Parse(tail);
            Assert.Equal(kind, formula.Kind);
            Assert.Equal(operand, formula.Operand);
        }

        [Theory]
        [InlineData("z dniem ogłoszenia.", EntryIntoForceFormulaKind.OnAnnouncementDay)]
        [InlineData("z dniem następującym po dniu ogłoszenia.", EntryIntoForceFormulaKind.DayAfterAnnouncement)]
        [InlineData("z dniem 1 stycznia 2025 r.", EntryIntoForceFormulaKind.OnSpecificDate)]
        [InlineData("pierwszego dnia miesiąca następującego po miesiącu ogłoszenia.", EntryIntoForceFormulaKind.FirstDayOfMonthAfterAnnouncementMonth)]
        [InlineData("z dniem określonym w komunikacie, o którym mowa w art. 18 ust. 1", EntryIntoForceFormulaKind.DeterminedByCommunication)]
        [InlineData("w terminie określonym w ustawie z dnia 15 maja 2024 r.", EntryIntoForceFormulaKind.DeterminedByIntroducingAct)]
        // M.P. 2026 poz. 1014: „Uchwała wchodzi w życie z dniem podjęcia." — data uchwały, nie ogłoszenia.
        [InlineData("z dniem podjęcia.", EntryIntoForceFormulaKind.OnAdoptionDay)]
        [InlineData("z dniem podpisania.", EntryIntoForceFormulaKind.OnAdoptionDay)]
        // M.P. 2026 poz. 349: „Uchwała wchodzi w życie z dniem następującym po dniu podjęcia."
        [InlineData("z dniem następującym po dniu podjęcia.", EntryIntoForceFormulaKind.DayAfterAdoption)]
        [InlineData("z dniem następującym po dniu podpisania.", EntryIntoForceFormulaKind.DayAfterAdoption)]
        public void PointFormulas_AreRecognized(string tail, EntryIntoForceFormulaKind kind)
        {
            Assert.Equal(kind, EntryIntoForceFormulaParser.Parse(tail).Kind);
        }

        // ============================================================
        // Kolejność kaskady — formuły są swoimi prefiksami
        // ============================================================

        [Fact]
        public void FirstDayAfterLapseOfDays_IsNotMistakenForPlainAfterDays()
        {
            // Ogon zawiera dosłownie „po upływie 14 dni", więc bez właściwej kolejności kaskady
            // wpadłby w AfterDaysFromAnnouncement.
            var formula = EntryIntoForceFormulaParser.Parse(
                "pierwszego dnia miesiąca następującego po upływie 14 dni od dnia ogłoszenia.");

            Assert.Equal(EntryIntoForceFormulaKind.FirstDayOfMonthAfterLapseOfDays, formula.Kind);
            Assert.Equal(14, formula.Operand);
        }

        [Fact]
        public void FirstDayAfterLapseOfMonths_IsNotMistakenForPlainAfterMonths()
        {
            var formula = EntryIntoForceFormulaParser.Parse(
                "pierwszego dnia miesiąca następującego po upływie 3 miesięcy od dnia ogłoszenia.");

            Assert.Equal(EntryIntoForceFormulaKind.FirstDayOfMonthAfterLapseOfMonths, formula.Kind);
            Assert.Equal(3, formula.Operand);
        }

        [Fact]
        public void DayAfterAnnouncement_IsNotMistakenForOnAnnouncementDay()
        {
            Assert.Equal(
                EntryIntoForceFormulaKind.DayAfterAnnouncement,
                EntryIntoForceFormulaParser.Parse("z dniem następującym po dniu ogłoszenia.").Kind);
        }

        [Fact]
        public void CommunicationFormula_IsNotMistakenForSpecificDate()
        {
            // Ogon zawiera „z dniem" oraz datę w powołaniu ustawy — bez właściwej kolejności
            // zostałby odczytany jako data jawna.
            var formula = EntryIntoForceFormulaParser.Parse(
                "z dniem określonym w komunikacie, o którym mowa w art. 33 ustawy z dnia 15 maja 2024 r. " +
                "o zmianie ustawy o pomocy obywatelom Ukrainy");

            Assert.Equal(EntryIntoForceFormulaKind.DeterminedByCommunication, formula.Kind);
            Assert.Null(formula.ExplicitDate);
        }

        [Fact]
        public void MandatoryOperand_IsNotDefaultedToOne()
        {
            // Rodziny „po upływie N dni" i „…N tygodni/miesięcy/lat" różnią się kształtem grupy
            // operandu: pierwsza wymaga liczebnika (OperandGroup), druga pozwala go pominąć
            // (OptionalOperandGroup, DefaultOperand = 1). Ta odpowiedniość jest utrzymywana ręcznie
            // w tablicy Cascade i nic poza tym testem jej nie pilnuje.
            var withoutOperand = EntryIntoForceFormulaParser.Parse("po upływie dni od dnia ogłoszenia.");

            Assert.NotEqual(1, withoutOperand.Operand);
            Assert.False(withoutOperand.IsResolvable && withoutOperand.Operand is null,
                "formuła bez obowiązkowego operandu nie może uchodzić za rozstrzygalną");

            // Kontrola dodatnia: tam, gdzie pominięcie JEST dozwolone, domyślna jedynka ma działać.
            Assert.Equal(1, EntryIntoForceFormulaParser.Parse("po upływie miesiąca od dnia ogłoszenia.").Operand);
        }

        // ============================================================
        // Data odniesienia i moc wsteczna
        // ============================================================

        [Fact]
        public void ExplicitReferenceDate_ReplacesAnnouncementDate()
        {
            var formula = EntryIntoForceFormulaParser.Parse("po upływie 14 dni od dnia 1 marca 2025 r.");

            Assert.Equal(EntryIntoForceFormulaKind.AfterDaysFromAnnouncement, formula.Kind);
            Assert.Equal(new DateTime(2025, 3, 1), formula.ReferenceDateOverride);

            // Formuła względna rozstrzyga się BEZ daty ogłoszenia, bo punkt odniesienia jest w tekście.
            Assert.Equal(new DateTime(2025, 3, 16), EntryIntoForceDateResolver.Resolve(formula, null));
        }

        [Fact]
        public void SplitRetroactivity_RemovesRetroClause_SoItIsNotReadAsEntryDate()
        {
            var (tail, retro, raw) = EntryIntoForceFormulaParser.SplitRetroactivity(
                "z dniem następującym po dniu ogłoszenia, z mocą od dnia 1 lipca 2025 r.");

            Assert.Equal(new DateTime(2025, 7, 1), retro);
            Assert.NotNull(raw);
            Assert.DoesNotContain("mocą", tail);
            Assert.Equal(EntryIntoForceFormulaKind.DayAfterAnnouncement, EntryIntoForceFormulaParser.Parse(tail).Kind);
        }

        [Fact]
        public void SplitScopedRetroactivity_ReadsUnitsAndDate()
        {
            var item = Assert.Single(
                EntryIntoForceFormulaParser.SplitScopedRetroactivity("art. 2 z mocą od dnia 1 stycznia 2025 r."));

            Assert.Equal("art. 2", item.Units);
            Assert.Equal(new DateTime(2025, 1, 1), item.RetroactiveFrom);
            Assert.Contains("z mocą", item.RawText);
        }

        [Fact]
        public void SplitScopedRetroactivity_SplitsSeveralPairsInOneClause()
        {
            // Powołanie drugiej pary zaczyna się dopiero za mocą wsteczną pierwszej, a otwierający
            // je spójnik („, a …") nie należy do powołania.
            var items = EntryIntoForceFormulaParser.SplitScopedRetroactivity(
                "art. 1 pkt 5 z mocą od dnia 1 stycznia 2025 r., a art. 2 z mocą od dnia 1 marca 2025 r.").ToList();

            Assert.Equal(2, items.Count);
            Assert.Equal("art. 1 pkt 5", items[0].Units);
            Assert.Equal(new DateTime(2025, 1, 1), items[0].RetroactiveFrom);
            Assert.Equal("art. 2", items[1].Units);
            Assert.Equal(new DateTime(2025, 3, 1), items[1].RetroactiveFrom);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("do spraw wszczętych stosuje się przepisy dotychczasowe")]
        [InlineData("z mocą od dnia 1 stycznia 2025 r.")]
        public void SplitScopedRetroactivity_WithoutBothParts_YieldsNothing(string? chunk)
        {
            // Ostatni przypadek jest istotny: moc wsteczna BEZ powołania jednostki nie jest
            // wyjątkiem — to moc wsteczna reguły ogólnej i należy do innego przebiegu.
            Assert.Empty(EntryIntoForceFormulaParser.SplitScopedRetroactivity(chunk));
        }

        [Fact]
        public void UnrecognizedTail_KeepsRawTextAndIsNotResolvable()
        {
            var formula = EntryIntoForceFormulaParser.Parse("kiedyś tam, gdy gwiazdy się ułożą");

            Assert.Equal(EntryIntoForceFormulaKind.Unrecognized, formula.Kind);
            Assert.False(formula.IsResolvable);
            Assert.Equal("kiedyś tam, gdy gwiazdy się ułożą", formula.RawText);
        }
    }

    /// <summary>
    /// Arytmetyka terminów. Konwencja: dzień odniesienia nie wlicza się do terminu, a wejście
    /// w życie następuje dnia następnego po jego upływie.
    /// </summary>
    public class EntryIntoForceDateResolverTests
    {
        private static readonly DateTime Announced = new(2025, 1, 15);

        private static EntryIntoForceFormula Formula(EntryIntoForceFormulaKind kind, int? operand = null,
            DateTime? explicitDate = null) =>
            new() { Kind = kind, Operand = operand, ExplicitDate = explicitDate };

        [Fact]
        public void OnAnnouncementDay_IsTheAnnouncementDate()
        {
            Assert.Equal(Announced,
                EntryIntoForceDateResolver.Resolve(Formula(EntryIntoForceFormulaKind.OnAnnouncementDay), Announced));
        }

        [Fact]
        public void OnAdoptionDay_IsTheActDate_NotTheAnnouncementDate()
        {
            var adopted = new DateTime(2025, 1, 10);
            Assert.Equal(adopted,
                EntryIntoForceDateResolver.Resolve(Formula(EntryIntoForceFormulaKind.OnAdoptionDay), Announced, adopted));
            Assert.Null(EntryIntoForceDateResolver.Resolve(Formula(EntryIntoForceFormulaKind.OnAdoptionDay), Announced));
        }

        [Fact]
        public void DayAfterAdoption_IsTheDayAfterTheActDate_NotAfterTheAnnouncement()
        {
            var adopted = new DateTime(2026, 3, 27);
            Assert.Equal(new DateTime(2026, 3, 28),
                EntryIntoForceDateResolver.Resolve(Formula(EntryIntoForceFormulaKind.DayAfterAdoption), Announced, adopted));
            Assert.Null(EntryIntoForceDateResolver.Resolve(Formula(EntryIntoForceFormulaKind.DayAfterAdoption), Announced));
        }

        [Fact]
        public void DayAfterAnnouncement_IsNextDay()
        {
            Assert.Equal(new DateTime(2025, 1, 16),
                EntryIntoForceDateResolver.Resolve(Formula(EntryIntoForceFormulaKind.DayAfterAnnouncement), Announced));
        }

        [Fact]
        public void AfterDays_SkipsTheReferenceDay()
        {
            // 15 I + 14 dni: termin biegnie 16–29 I, wejście w życie 30 I.
            Assert.Equal(new DateTime(2025, 1, 30),
                EntryIntoForceDateResolver.Resolve(
                    Formula(EntryIntoForceFormulaKind.AfterDaysFromAnnouncement, 14), Announced));
        }

        [Fact]
        public void AfterMonths_EndsDayAfterTheMatchingDate()
        {
            Assert.Equal(new DateTime(2025, 4, 16),
                EntryIntoForceDateResolver.Resolve(
                    Formula(EntryIntoForceFormulaKind.AfterMonthsFromAnnouncement, 3), Announced));
        }

        [Fact]
        public void AfterWeeks_CountsSevenDayPeriods()
        {
            // 15 I + 2 tygodnie: termin biegnie 16–29 I, wejście w życie 30 I.
            Assert.Equal(new DateTime(2025, 1, 30),
                EntryIntoForceDateResolver.Resolve(
                    Formula(EntryIntoForceFormulaKind.AfterWeeksFromAnnouncement, 2), Announced));
        }

        [Fact]
        public void AfterYears_EndsDayAfterTheMatchingDate()
        {
            Assert.Equal(new DateTime(2027, 1, 16),
                EntryIntoForceDateResolver.Resolve(
                    Formula(EntryIntoForceFormulaKind.AfterYearsFromAnnouncement, 2), Announced));
        }

        [Fact]
        public void FirstDayOfMonthAfterAnnouncementMonth()
        {
            Assert.Equal(new DateTime(2025, 2, 1),
                EntryIntoForceDateResolver.Resolve(
                    Formula(EntryIntoForceFormulaKind.FirstDayOfMonthAfterAnnouncementMonth), Announced));
        }

        [Fact]
        public void FirstDayOfMonthAfterLapseOfDays()
        {
            // Upływ 14 dni → 30 I; pierwszy dzień następnego miesiąca → 1 II.
            Assert.Equal(new DateTime(2025, 2, 1),
                EntryIntoForceDateResolver.Resolve(
                    Formula(EntryIntoForceFormulaKind.FirstDayOfMonthAfterLapseOfDays, 14), Announced));
        }

        [Fact]
        public void FirstDayOfMonthAfterLapseOfMonths()
        {
            // Upływ 3 miesięcy → 16 IV; pierwszy dzień następnego miesiąca → 1 V.
            Assert.Equal(new DateTime(2025, 5, 1),
                EntryIntoForceDateResolver.Resolve(
                    Formula(EntryIntoForceFormulaKind.FirstDayOfMonthAfterLapseOfMonths, 3), Announced));
        }

        [Fact]
        public void OnSpecificDate_ResolvesWithoutAnnouncementDate()
        {
            Assert.Equal(new DateTime(2026, 1, 1),
                EntryIntoForceDateResolver.Resolve(
                    Formula(EntryIntoForceFormulaKind.OnSpecificDate, explicitDate: new DateTime(2026, 1, 1)), null));
        }

        [Theory]
        [InlineData(EntryIntoForceFormulaKind.OnAnnouncementDay)]
        [InlineData(EntryIntoForceFormulaKind.DayAfterAnnouncement)]
        [InlineData(EntryIntoForceFormulaKind.AfterDaysFromAnnouncement)]
        [InlineData(EntryIntoForceFormulaKind.AfterMonthsFromAnnouncement)]
        [InlineData(EntryIntoForceFormulaKind.FirstDayOfMonthAfterAnnouncementMonth)]
        public void RelativeFormulas_YieldNull_WithoutAnnouncementDate(EntryIntoForceFormulaKind kind)
        {
            Assert.Null(EntryIntoForceDateResolver.Resolve(Formula(kind, 14), null));
        }

        [Theory]
        [InlineData(EntryIntoForceFormulaKind.DeterminedByCommunication)]
        [InlineData(EntryIntoForceFormulaKind.DeterminedByIntroducingAct)]
        [InlineData(EntryIntoForceFormulaKind.Unrecognized)]
        public void NonCalendarFormulas_NeverResolve_EvenWithAnnouncementDate(EntryIntoForceFormulaKind kind)
        {
            Assert.Null(EntryIntoForceDateResolver.Resolve(Formula(kind), Announced));
        }

        [Fact]
        public void NullFormula_IsHandled()
        {
            Assert.Null(EntryIntoForceDateResolver.Resolve(null, Announced));
        }
    }
}
