using System;
using Saga.Core.Helpers;
using Xunit;

namespace Saga.Core.Tests
{
	/// <summary>
	/// Wspólny słownik dat i liczebników. Kluczowy kontrakt: metody Try* NIGDY nie rzucają —
	/// literówka prawodawcy („31 lutego") nie może wywrócić parsowania aktu.
	/// </summary>
	public class PolishDatesTests
	{
		[Theory]
		[InlineData("1 stycznia 2025 r.", 2025, 1, 1)]
		[InlineData("dnia 29 maja 2020 r.", 2020, 5, 29)]
		[InlineData("z dniem 31 grudnia 2025 r.", 2025, 12, 31)]
		[InlineData("9 września 2024", 2024, 9, 9)]
		[InlineData("15 października 2026 r.", 2026, 10, 15)]
		public void TryParseLongDate_ReadsDateEmbeddedInSentence(string input, int year, int month, int day)
		{
			Assert.True(PolishDates.TryParseLongDate(input, out var date));
			Assert.Equal(new DateTime(year, month, day), date);
		}

		[Theory]
		[InlineData("31 lutego 2025 r.")]     // data nieistniejąca w kalendarzu
		[InlineData("1 vasariou 2025 r.")]    // nazwa miesiąca spoza słownika
		[InlineData("bez daty")]
		[InlineData("")]
		[InlineData(null)]
		public void TryParseLongDate_ReturnsFalse_InsteadOfThrowing(string? input)
		{
			Assert.False(PolishDates.TryParseLongDate(input, out var date));
			Assert.Equal(default, date);
		}

		[Fact]
		public void TryMonth_IsCaseInsensitive()
		{
			Assert.True(PolishDates.TryMonth("Września", out var month));
			Assert.Equal(9, month);
		}

		[Fact]
		public void TryBuild_RejectsImpossibleDate()
		{
			Assert.False(PolishDates.TryBuild(2025, 2, 31, out _));
			Assert.True(PolishDates.TryBuild(2024, 2, 29, out var leap));
			Assert.Equal(new DateTime(2024, 2, 29), leap);
		}

		// Liczebniki mieszkają w PolishNumeralsTests — PolishDates trzyma już tylko miesiące i daty.
	}
}
