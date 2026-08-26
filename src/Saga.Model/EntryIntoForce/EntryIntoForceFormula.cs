using System;

#nullable enable

namespace Saga.Model.EntryIntoForce
{
	/// <summary>
	/// Rodzaj formuły wejścia w życie.
	/// </summary>
	public enum EntryIntoForceFormulaKind
	{
		/// <summary>Formuła nierozpoznana.</summary>
		Unrecognized,

		/// <summary>„po upływie N dni od dnia ogłoszenia"</summary>
		AfterDaysFromAnnouncement,

		/// <summary>„po upływie N tygodni / tygodnia od dnia ogłoszenia"</summary>
		AfterWeeksFromAnnouncement,

		/// <summary>„po upływie N miesięcy / miesiąca od dnia ogłoszenia"</summary>
		AfterMonthsFromAnnouncement,

		/// <summary>„po upływie N lat / roku od dnia ogłoszenia"</summary>
		AfterYearsFromAnnouncement,

		/// <summary>„z dniem następującym po dniu ogłoszenia"</summary>
		DayAfterAnnouncement,

		/// <summary>„z dniem ogłoszenia"</summary>
		OnAnnouncementDay,

		/// <summary>„z dniem <dzień> <miesiąc> <rok> r."</summary>
		OnSpecificDate,

		/// <summary>„pierwszego dnia miesiąca następującego po miesiącu ogłoszenia"</summary>
		FirstDayOfMonthAfterAnnouncementMonth,

		/// <summary>„pierwszego dnia miesiąca następującego po upływie N dni od dnia ogłoszenia"</summary>
		FirstDayOfMonthAfterLapseOfDays,

		/// <summary>„pierwszego dnia miesiąca następującego po upływie N miesięcy od dnia ogłoszenia"</summary>
		FirstDayOfMonthAfterLapseOfMonths,

		/// <summary>„w terminie określonym w ustawie … (wprowadzającej)"</summary>
		DeterminedByIntroducingAct,

		/// <summary>„z dniem określonym w komunikacie, o którym mowa w art. …"</summary>
		DeterminedByCommunication,
	}

	/// <summary>
	/// Strukturalny opis formuły wejścia w życie.
	/// </summary>
	public class EntryIntoForceFormula
	{
		/// <summary>Rodzaj formuły.</summary>
		public EntryIntoForceFormulaKind Kind { get; set; } = EntryIntoForceFormulaKind.Unrecognized;

		/// <summary>Liczba dni/miesięcy w formułach „po upływie N …".</summary>
		public int? Operand { get; set; }

		/// <summary>Data jawna dla <see cref="EntryIntoForceFormulaKind.OnSpecificDate"/>.</summary>
		public DateTime? ExplicitDate { get; set; }

		/// <summary>
		/// Data odniesienia podana jawnie zamiast dnia ogłoszenia („po upływie 14 dni od dnia 1 marca 2025 r.").
		/// </summary>
		public DateTime? ReferenceDateOverride { get; set; }

		/// <summary>Dosłowny fragment tekstu, z którego rozpoznano formułę.</summary>
		public string RawText { get; set; } = string.Empty;

		/// <summary>
		/// Czy formuła w ogóle może dać datę kalendarzową.
		/// </summary>
		public bool IsResolvable =>
			Kind != EntryIntoForceFormulaKind.Unrecognized &&
			Kind != EntryIntoForceFormulaKind.DeterminedByIntroducingAct &&
			Kind != EntryIntoForceFormulaKind.DeterminedByCommunication;
	}
}
