using Saga.Model.EntryIntoForce;

namespace Saga.Core.Services.EntryIntoForce
{
	/// <summary>
	/// Wylicza datę wejścia w życie z rozpoznanej formuły.
    /// Zwraca null, gdy formuła nie prowadzi do daty kalendarzowej
    /// albo gdy brakuje punktu odniesienia.
	/// </summary>
	internal static class EntryIntoForceDateResolver
	{
		internal static DateTime? Resolve(EntryIntoForceFormula? formula, DateTime? announcementDate)
		{
			if (formula is null || !formula.IsResolvable)
				return null;

			if (formula.Kind == EntryIntoForceFormulaKind.OnSpecificDate)
				return formula.ExplicitDate;

			var reference = formula.ReferenceDateOverride ?? announcementDate;
			if (reference is not { } baseDate)
				return null;

			return formula.Kind switch
			{
				EntryIntoForceFormulaKind.OnAnnouncementDay => baseDate,

				EntryIntoForceFormulaKind.DayAfterAnnouncement => baseDate.AddDays(1),

				EntryIntoForceFormulaKind.AfterDaysFromAnnouncement =>
					formula.Operand is { } days ? baseDate.AddDays(days + 1) : null,

				EntryIntoForceFormulaKind.AfterWeeksFromAnnouncement =>
					formula.Operand is { } weeks ? baseDate.AddDays(weeks * 7 + 1) : null,

				EntryIntoForceFormulaKind.AfterMonthsFromAnnouncement =>
					formula.Operand is { } months ? baseDate.AddMonths(months).AddDays(1) : null,

				EntryIntoForceFormulaKind.AfterYearsFromAnnouncement =>
					formula.Operand is { } years ? baseDate.AddYears(years).AddDays(1) : null,

				EntryIntoForceFormulaKind.FirstDayOfMonthAfterAnnouncementMonth =>
					FirstDayOfNextMonth(baseDate),

				EntryIntoForceFormulaKind.FirstDayOfMonthAfterLapseOfDays =>
					formula.Operand is { } days ? FirstDayOfNextMonth(baseDate.AddDays(days + 1)) : null,

				EntryIntoForceFormulaKind.FirstDayOfMonthAfterLapseOfMonths =>
					formula.Operand is { } months ? FirstDayOfNextMonth(baseDate.AddMonths(months).AddDays(1)) : null,

				_ => null,
			};
		}

		private static DateTime FirstDayOfNextMonth(DateTime date) =>
			new DateTime(date.Year, date.Month, 1).AddMonths(1);
	}
}
