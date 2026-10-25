using System.Text.RegularExpressions;
using Saga.Core.Helpers;

namespace Saga.Core.Services.EntryIntoForce
{
    /// <summary>Wzorce klauzuli wejścia w życie.</summary>
    internal static class EntryIntoForcePatterns
    {
        private const RegexOptions Opts =
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        private static readonly string Operand = PolishNumerals.CardinalAlternation;

        private static readonly string OperandGroup = @"(?<n>\d+|" + Operand + @"|\p{L}+)";

        private static readonly string OptionalOperandGroup = @"(?:" + OperandGroup + @"\s+)?";

        /// <summary>Data w formie długiej.</summary>
        private const string LongDate = @"\d{1,2}\s+(?:" + PolishDates.MonthAlternation + @")\s+\d{4}";

        /// <summary>Podmiot samozwrotny klauzuli — akt mówiący o samym sobie.</summary>
        private const string SelfSubject =
            @"(?:Ustawa|Rozporządzenie|Uchwała|Zarządzenie|Niniejsz[aeąy]\s+(?:ustawa|rozporządzenie|uchwała|zarządzenie))";

        /// <summary>Klauzula główna: „Ustawa wchodzi w życie".</summary>
        internal static readonly Regex MainClause = new(
            @"(?<subject>" + SelfSubject + @")\s+wchodz(?:i|ą)\s+w\s+życie\s+(?<tail>.+)",
            Opts | RegexOptions.Singleline);

        /// <summary>Klauzula wyjątku: „…, który wchodzi w życie" / „…, które wchodzą w życie".</summary>
        internal static readonly Regex RelativeClause = new(
            @"któr\p{L}+\s+wchodz(?:i|ą)\s+w\s+życie\s+(?<tail>.+)",
            Opts | RegexOptions.Singleline);

        internal static readonly Regex AnyClauseVerb = new(
            @"wchodz(?:i|ą)\s+w\s+życie",
            Opts);

        /// <summary>
        /// Wystąpienie rzeczownikowe („przed dniem wejścia w życie", „od dnia wejścia w życie").
        /// </summary>
        internal static readonly Regex NounPhraseMention = new(
            @"(?:przed|od|po|do|z|w)\s+dni(?:a|em|u)\s+wejścia\s+w\s+życie",
            Opts);

        /// <summary>
        /// Powołanie zewnętrzne aktu przed czasownikiem: „ustawa z dnia 15 maja 2024 r. o zmianie … wchodzi w życie".
        /// Podmiotem jest inny akt.
        /// </summary>
        internal static readonly Regex ForeignActSubject = new(
            @"ustaw\p{L}*\s+z\s+dnia\s+" + LongDate,
            Opts);

        /// <summary>Znacznik wyjątków.</summary>
        internal static readonly Regex ExceptionMarker = new(
            @",?\s*z\s+(?:wyjątkiem|zastrzeżeniem)\s*(?<colon>:)?",
            Opts);

        /// <summary>
        /// Znacznik członu nadającego moc wsteczną wskazanym jednostkom: „…, z tym że art. … z mocą od dnia …".
        /// </summary>
        internal static readonly Regex ProvisoMarker = new(
            @",?\s*z\s+tym,?\s+że\s+",
            Opts);

        /// <summary>Spójnik otwierający kolejną pozycję wyliczenia w członie („…, a art. 2 z mocą…").</summary>
        internal static readonly Regex LeadingConjunction = new(
            @"^[\s,;]*(?:a|i|oraz|zaś|natomiast)\s+",
            Opts);

        /// <summary>Moc wsteczna: „z mocą od dnia" / „z mocą obowiązującą od dnia".</summary>
        internal static readonly Regex Retroactivity = new(
            @",?\s*z\s+mocą\s+(?:obowiązującą\s+)?od\s+dnia\s+(?<date>" + LongDate + @")\s*r?\.?",
            Opts);

        /// <summary>Data odniesienia: data ogłoszenia albo jawna data podana zamiast niej.</summary>
        private const string RefDate = @"(?:ogłoszenia|(?<ref>" + LongDate + @")\s*r\.?)";

        /// <summary>„po upływie"</summary>
        private const string AfterLapse = @"po\s+upływie\s+";

        /// <summary>„od dnia"</summary>
        private const string FromReferenceDay = @"\s+od\s+dnia\s+" + RefDate;

        /// <summary>„pierwszego dnia miesiąca (następującego) po upływie"</summary>
        private const string FirstDayOfMonthAfterLapse =
            @"pierwszego\s+dnia\s+miesiąca\s+(?:następującego\s+)?" + AfterLapse;

        /// <summary>„pierwszego dnia miesiąca (następującego) po upływie N miesięcy od dnia"</summary>
        internal static readonly Regex FirstDayAfterLapseOfMonths = new(
            FirstDayOfMonthAfterLapse + OptionalOperandGroup + @"miesi(?:ąca|ęcy|ące)" + FromReferenceDay,
            Opts);

        /// <summary>„pierwszego dnia miesiąca (następującego) po upływie N dni od dnia"</summary>
        internal static readonly Regex FirstDayAfterLapseOfDays = new(
            FirstDayOfMonthAfterLapse + OperandGroup + @"\s+dni" + FromReferenceDay,
            Opts);

        /// <summary>„pierwszego dnia miesiąca następującego po miesiącu ogłoszenia"</summary>
        internal static readonly Regex FirstDayOfMonthAfterAnnouncementMonth = new(
            @"pierwszego\s+dnia\s+miesiąca\s+następującego\s+po\s+miesiącu\s+ogłoszenia",
            Opts);

        /// <summary>„z dniem następującym po dniu ogłoszenia"</summary>
        internal static readonly Regex DayAfterAnnouncement = new(
            @"z\s+dniem\s+następującym\s+po\s+dniu\s+" + RefDate,
            Opts);

        /// <summary>„z dniem określonym w komunikacie, o którym mowa w"</summary>
        internal static readonly Regex DeterminedByCommunication = new(
            @"z\s+dniem\s+określonym\s+w\s+komunikacie",
            Opts);

        /// <summary>„w terminie określonym w"</summary>
        internal static readonly Regex DeterminedByIntroducingAct = new(
            @"w\s+terminie\s+określonym\s+w\s+(?:ustawie|odrębn\p{L}+|przepis\p{L}+)",
            Opts);

        /// <summary>„z dniem <dzień> <miesiąc> <rok>"</summary>
        internal static readonly Regex OnSpecificDate = new(
            @"z\s+dniem\s+(?<date>" + LongDate + @")\s*r?\.?",
            Opts);

        /// <summary>„po upływie N miesięcy / miesiąca od dnia"</summary>
        internal static readonly Regex AfterMonths = new(
            AfterLapse + OptionalOperandGroup + @"miesi(?:ąca|ęcy|ące)" + FromReferenceDay,
            Opts);

        /// <summary>„po upływie N tygodni / tygodnia od dnia"</summary>
        internal static readonly Regex AfterWeeks = new(
            AfterLapse + OptionalOperandGroup + @"tygodni(?:a|e)?" + FromReferenceDay,
            Opts);

        /// <summary>„po upływie N lat / roku od dnia"</summary>
        internal static readonly Regex AfterYears = new(
            AfterLapse + OptionalOperandGroup + @"(?:lat|lata|roku)" + FromReferenceDay,
            Opts);

        /// <summary>„po upływie N dni od dnia"</summary>
        internal static readonly Regex AfterDays = new(
            AfterLapse + OperandGroup + @"\s+dni" + FromReferenceDay,
            Opts);

        /// <summary>„z dniem ogłoszenia"</summary>
        internal static readonly Regex OnAnnouncementDay = new(
            @"z\s+dniem\s+ogłoszenia",
            Opts);

        /// <summary>„z dniem podjęcia" (uchwała), „z dniem podpisania" (zarządzenie), „z dniem wydania"</summary>
        internal static readonly Regex OnAdoptionDay = new(
            @"z\s+dniem\s+(?:podjęcia|podpisania|wydania)",
            Opts);

        /// <summary>„z dniem następującym po dniu podjęcia" (uchwała Sejmu — M.P. 2026 poz. 349), „… po dniu podpisania"</summary>
        internal static readonly Regex DayAfterAdoption = new(
            @"z\s+dniem\s+następującym\s+po\s+dniu\s+(?:podjęcia|podpisania|wydania)",
            Opts);
    }
}
