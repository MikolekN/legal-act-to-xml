using System.Text.RegularExpressions;
using Saga.Model;
using Saga.Model.EditorialUnits;
using Serilog;

namespace Saga.Core.Services
{
    /// <summary>
    /// Serwis do parsowania publikatorów (Dz. U., M.P.) z treści artykułów
    /// i uzupełniania listy JournalInfo w encji Article.
    /// </summary>
    public class JournalReferenceService
    {
        // Regex dla publikatorów typu "Dz. U. z 2020 r. poz. 1234, 5678 i 9012" oraz "M.P. poz. 1029"
        // (Monitor Polski — uchwały i zarządzenia; § 158 ust. 7 ZTP stosuje się do obu dzienników). „Dz. U. 2025 r. poz. 881"
        // bez „z" to literówka źródła — rok z „r." czyta się tak samo.
        private static readonly Regex JournalRegex = new Regex(
            @"(?<source>(?:Dz\.\s*U\.|(?<monitor>M\.\s?P\.))\s*(?:(?:z\s*(?<year>\d{4})\s*r\.?)|(?:(?<year>\d{4})\s*r\.))?\s*poz\.\s*(?<positions>[\d,\sandi]+(?:,\s*[\d,\sandi]+)*))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        // Data aktu w jego tytule („ustawie z dnia 18 stycznia 2024 r.") — rok, w którym akt wydano.
        private static readonly Regex ActDateRegex = new Regex(
            @"\bz\s+dnia\s+\d{1,2}\s+\p{L}+\s+(?<year>\d{4})\s*r\.",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        /// <summary>
        /// Parsuje publikatory (Dz. U.) z treści artykułu i uzupełnia listę Journals.
        /// Grupuje pozycje po roku – jeśli ten sam rok wystąpi wielokrotnie,
        /// pozycje są scalane w jeden JournalInfo.
        /// </summary>
        public void ParseJournalReferences(Article article)
        {
            if (article == null || string.IsNullOrEmpty(article.ContentText))
                return;

            if (!StartsWithAmendingFormula(article.ContentText))
                return;

            article.Journals.AddRange(ReadJournals(article.ContentText));

            if (article.Journals.Count > 0)
            {
                Log.Debug("Wykryto {Count} publikator(ów) w artykule [{ArticleId}]: {Journals}",
                    article.Journals.Count,
                    article.Id,
                    string.Join("; ", article.Journals.Select(j => j.ToStringLong())));
            }
        }

        /// <summary>
        /// Jedyny akt wskazany w tekście przez publikator („art. 19 ustawy z dnia … (Dz. U. poz. 1792), który
        /// stanowi:"); null, gdy tekst nie wskazuje aktu albo wskazuje więcej niż jeden — wtedy nie wiadomo, który.
        /// </summary>
        public static JournalInfo? ReadSingleAct(string? text)
        {
            if (string.IsNullOrEmpty(text) || JournalRegex.Matches(text).Count != 1)
                return null;

            return ReadJournals(text).FirstOrDefault();
        }

        /// <summary>
        /// Akt nazwany w tekście jako pierwszy: dziennik, rocznik i pierwsza wypisana pozycja. Dalsze pozycje
        /// („(Dz. U. poz. 2320, z 2021 r. poz. 72 i 802)") to zmiany tego aktu, nie jego miejsce ogłoszenia.
        /// Null, gdy tekst nie podaje publikatora albo nie da się ustalić rocznika.
        /// </summary>
        internal static JournalInfo? ReadFirstAct(string text)
        {
            if (JournalRegex.Match(text) is not { Success: true } journal
                || FirstNumber.Match(journal.Groups["positions"].Value) is not { Success: true } position)
            {
                return null;
            }

            int? written = journal.Groups["year"].Success ? int.Parse(journal.Groups["year"].Value) : null;
            return (written ?? YearOfActDateBefore(text, journal.Index)) is { } year
                ? new JournalInfo
                {
                    Publisher = journal.Groups["monitor"].Success ? PublisherType.MonitorPolski : PublisherType.DziennikUstaw,
                    Year = year,
                    Positions = [int.Parse(position.Value)],
                    SourceString = journal.Groups["source"].Value,
                }
                : null;
        }

        private static readonly Regex FirstNumber = new(@"\d+", RegexOptions.Compiled);

        /// <summary>
        /// Publikatory (Dz. U., M.P.) wymienione w tekście, z pozycjami scalonymi w obrębie dziennika i rocznika.
        /// </summary>
        internal static List<JournalInfo> ReadJournals(string text)
        {
            var journals = new List<JournalInfo>();
            var journalsByYear = new Dictionary<(PublisherType Publisher, int Year), JournalInfo>();

            foreach (Match m in JournalRegex.Matches(text))
            {
                var sourceMatch = m.Groups["source"].Value;
                // § 158 ust. 7 ZTP: rocznik pomija się, gdy akt ogłoszono w roku wymienionym w jego dacie —
                // brakujący rok to rok z daty aktu, którego tytuł poprzedza publikator.
                int? written = m.Groups["year"].Success ? int.Parse(m.Groups["year"].Value) : null;
                if ((written ?? YearOfActDateBefore(text, m.Index)) is not { } year)
                {
                    Log.Warning("Publikator bez rocznika i bez daty aktu przed nim — pominięty: {Source}", sourceMatch);
                    continue;
                }

                var positionsStr = m.Groups["positions"].Success ? m.Groups["positions"].Value : "";

                // Dziennik jest częścią tożsamości aktu: bez niego ELI nie ma segmentu „DU" albo „MP".
                var publisher = m.Groups["monitor"].Success ? PublisherType.MonitorPolski : PublisherType.DziennikUstaw;

                if (!journalsByYear.TryGetValue((publisher, year), out var journalInfo))
                {
                    journalInfo = new JournalInfo
                    {
                        Publisher = publisher,
                        Year = year,
                        SourceString = sourceMatch,
                    };
                    journalsByYear[(publisher, year)] = journalInfo;
                    journals.Add(journalInfo);
                }
                else
                {
                    // Aktualizuj SourceString jeśli nowe dopasowanie jest pełniejsze
                    if (sourceMatch.Length > journalInfo.SourceString.Length)
                    {
                        journalInfo.SourceString = sourceMatch;
                    }
                }

                var positionNumbers = positionsStr
                    .Split(new[] { ',', ' ', 'i' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s.Trim(), out int num) ? num : (int?)null)
                    .Where(n => n.HasValue)
                    .Select(n => n!.Value)
                    .ToList();

                journalInfo.Positions.AddRange(positionNumbers);
                journalInfo.Positions = journalInfo.Positions.Distinct().OrderBy(p => p).ToList();
            }

            return journals;
        }

        /// <summary>Rok z ostatniej daty aktu stojącej przed wskazanym miejscem tekstu; null, gdy daty nie ma.</summary>
        private static int? YearOfActDateBefore(string text, int index) =>
            ActDateRegex.Matches(text[..index]).LastOrDefault() is { } date ? int.Parse(date.Groups["year"].Value) : null;

        private static readonly Regex ArticlePrefixStrip = new(
            @"^(Art\.|\u00a7)\s*[\w\d]+\.?\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // „W ustawie z dnia …", także z określeniem ustawy przed datą („W ustawie budżetowej na rok 2024 z dnia …"),
        // oraz „W rozporządzeniu Ministra … z dnia …" — przed datą stoi organ wydający, stąd dłuższe okno
        // („Ministrów Infrastruktury oraz Spraw Wewnętrznych i Administracji").
        // Określenie nie sięga za nawias — tam zaczyna się już publikator albo inna treść.
        private static readonly Regex AmendingFormula = new(
            @"^W\s+(?:ustawie\b[^()]{0,60}?|rozporządzeniu\b[^()]{0,120}?)\bz\s+dnia\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static bool StartsWithAmendingFormula(string contentText)
        {
            var text = contentText.Trim();
            text = ArticlePrefixStrip.Replace(text, string.Empty, 1);
            return AmendingFormula.IsMatch(text);
        }
    }
}
