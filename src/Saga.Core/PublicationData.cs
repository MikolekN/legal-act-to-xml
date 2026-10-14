using System.Globalization;
using Saga.Model;

namespace Saga.Core
{
    /// <summary>
    /// Dane ogłoszenia aktu podane przez wywołującego: dziennik, rok, pozycja i dzień ogłoszenia — każde opcjonalne.
    /// Dla dokumentu, który ich nie niesie: szczotki przed publikacją („Poz. 000", „&lt;data ogłoszenia&gt;") i aktu bez
    /// nagłówka publikatora. Uzupełniają wyłącznie to, czego dokument nie podaje — nagłówek publikatora i wiersz
    /// przyłączenia wygrywają (ADR-0081).
    /// </summary>
    public sealed record PublicationData
    {
        /// <summary>Dziennik urzędowy.</summary>
        public PublisherType? Publisher { get; init; }

        /// <summary>Rocznik dziennika; bez niego — rok dnia ogłoszenia.</summary>
        public int? Year { get; init; }

        /// <summary>Pozycja aktu w roczniku dziennika.</summary>
        public int? Position { get; init; }

        /// <summary>Dzień ogłoszenia — od niego liczy się wejście w życie (§ 45 ZTP).</summary>
        public DateTime? AnnouncementDate { get; init; }

        /// <summary>Najmniejszy przyjmowany rocznik — pierwszy rocznik Dziennika Ustaw.</summary>
        public const int FirstYear = 1918;

        /// <summary>
        /// Dane ogłoszenia z wartości tekstowych (formularz usługi, wiersz poleceń). Null w <paramref name="data"/>, gdy
        /// nie podano żadnej wartości; <paramref name="error"/> — opis pierwszej błędnej wartości.
        /// </summary>
        /// <param name="publisher">„DU" (Dziennik Ustaw) albo „MP" (Monitor Polski), bez względu na wielkość liter.</param>
        /// <param name="year">Rocznik: liczba od 1918 do roku następnego.</param>
        /// <param name="position">Pozycja: liczba dodatnia.</param>
        /// <param name="announcementDate">Dzień ogłoszenia w zapisie RRRR-MM-DD.</param>
        public static bool TryCreate(
            string? publisher, string? year, string? position, string? announcementDate,
            out PublicationData? data, out string? error)
        {
            data = null;
            error = null;

            PublisherType? journal = null;
            if (!string.IsNullOrWhiteSpace(publisher))
            {
                journal = publisher.Trim().ToUpperInvariant() switch
                {
                    "DU" => PublisherType.DziennikUstaw,
                    "MP" => PublisherType.MonitorPolski,
                    _ => null,
                };
                if (journal is null)
                {
                    error = $"Nieznany dziennik „{publisher.Trim()}” — podaj DU (Dziennik Ustaw) albo MP (Monitor Polski).";
                    return false;
                }
            }

            int? volume = null;
            if (!string.IsNullOrWhiteSpace(year))
            {
                var lastYear = DateTime.Today.Year + 1;
                if (!int.TryParse(year.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsedYear)
                    || parsedYear < FirstYear || parsedYear > lastYear)
                {
                    error = $"Rocznik „{year.Trim()}” nie jest rokiem od {FirstYear} do {lastYear}.";
                    return false;
                }
                volume = parsedYear;
            }

            int? number = null;
            if (!string.IsNullOrWhiteSpace(position))
            {
                if (!int.TryParse(position.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsedPosition)
                    || parsedPosition < 1)
                {
                    error = $"Pozycja „{position.Trim()}” nie jest liczbą dodatnią.";
                    return false;
                }
                number = parsedPosition;
            }

            DateTime? announced = null;
            if (!string.IsNullOrWhiteSpace(announcementDate))
            {
                if (!DateTime.TryParseExact(announcementDate.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var parsedDate))
                {
                    error = $"Dzień ogłoszenia „{announcementDate.Trim()}” nie jest datą w zapisie RRRR-MM-DD.";
                    return false;
                }
                announced = parsedDate;
            }

            if (journal is null && volume is null && number is null && announced is null)
                return true;

            data = new PublicationData { Publisher = journal, Year = volume, Position = number, AnnouncementDate = announced };
            return true;
        }

        /// <summary>
        /// Dzień ogłoszenia, którego dokument nie podał — przed budową modelu, bo od niego liczy się wejście w życie.
        /// </summary>
        internal void FillAnnouncementDate(LegalDocument document) =>
            document.AnnouncementDate ??= AnnouncementDate;

        /// <summary>
        /// Dziennik, rocznik i pozycja, których dokument nie podał — po budowie modelu, gdy nagłówek publikatora i wiersz
        /// przyłączenia miały już szansę je podać.
        /// </summary>
        internal void FillJournal(LegalDocument document)
        {
            var journal = document.SourceJournal;
            journal.Publisher ??= Publisher;
            journal.Year ??= Year ?? AnnouncementDate?.Year;
            if (journal.Positions.Count == 0 && Position is { } number)
                journal.Positions.Add(number);
        }
    }
}
