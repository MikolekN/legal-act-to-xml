#nullable enable
using System.Text;
using System.Text.RegularExpressions;

namespace Saga.Core.Ingest.Pdf
{
    /// <summary>
    /// Usuwa artefakty stron (nagłówki/stopki/paginację) z wierszy PDF — inaczej nagłówek strony
    /// wpadałby między jednostki redakcyjne i psuł ciągłość numeracji. Dwa sygnały (suma):
    /// 1) POZYCYJNO-POWTÓRZENIOWY: wiersz w strefie marginesu (górne/dolne 8% wysokości strony),
    ///    którego znormalizowana treść (cyfry → #) powtarza się na ≥ 60% stron dokumentu, stojący na skraju strony:
    ///    między nim a krawędzią są tylko inne artefakty. Nagłówek, stopka i metryczka składu („DDUTJ.60.214.2025
    ///    łamanie …") są skrajnymi wierszami strony; wiersz odnośnika powtórzony słowo w słowo na dole dwóch stron
    ///    („str. 35, Dz. Urz. UE L 227 …" w odnośnikach 1 i 2 — Dz. U. 2026 poz. 305) ma pod sobą dalszy tekst;
    /// 2) WHITELIST: znane artefakty publikacyjne (Dziennik Ustaw, paginacja „– N –", „Poz. N")
    ///    w strefie marginesu — działa też dla dokumentów jednostronicowych.
    /// </summary>
    internal static class PageArtifactFilter
    {
        private const double MarginZoneRatio = 0.08;
        private const double RepetitionThreshold = 0.6;

        // Whitelist obejmuje wyłącznie formy JEDNOZNACZNIE nagłówkowe — zdanie treści zaczynające się
        // od „Dziennik Ustaw…" ani goła liczba (komórka tabeli załącznika na dole strony!) nie mogą
        // zostać skasowane pojedynczo; niejednoznaczne przypadki wyłapuje sygnał powtórzeniowy.
        private static readonly Regex[] KnownArtifacts =
        {
            new(@"^Dziennik\s+Ustaw$", RegexOptions.Compiled),
            new(@"^Monitor\s+Polski$", RegexOptions.Compiled),
            new(@"^Dziennik\s+Ustaw\s*[–\-—]\s*\d+\s*[–\-—]\s*Poz\.\s*\d+\.?$", RegexOptions.Compiled),
            new(@"^Monitor\s+Polski\s*[–\-—]\s*\d+\s*[–\-—]\s*Poz\.\s*\d+\.?$", RegexOptions.Compiled),
            new(@"^[–\-—]\s*\d+\s*[–\-—]$", RegexOptions.Compiled),
            new(@"^Poz\.\s*\d+\.?$", RegexOptions.Compiled),
            new(@"^Strona\s+\d+(\s+z\s+\d+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        };

        public static List<PdfTextLine> Filter(List<PdfTextLine> lines, int pageCount)
        {
            if (lines.Count == 0)
                return lines;

            // Sygnał powtórzeniowy liczony wyłącznie w strefach marginesu. Klucz czysto cyfrowo-spacjowy
            // jest WYŁĄCZONY: normalizacja cyfr→# scalałaby RÓŻNE wiersze liczbowych tabel załączników
            // („1 15 200" i „2 16 300" → „# ## ###") i kasowała dane prawne; prawdziwe artefakty
            // (nagłówki, paginacja „– # –") zawierają litery lub myślniki.
            var marginRepeats = new Dictionary<string, HashSet<int>>();
            foreach (var line in lines.Where(IsInMarginZone))
            {
                var key = Normalize(line.Text);
                if (!IsRepetitionEligible(key))
                    continue;
                if (!marginRepeats.TryGetValue(key, out var pages))
                    marginRepeats[key] = pages = new HashSet<int>();
                pages.Add(line.PageNumber);
            }

            bool IsRepeated(PdfTextLine line) =>
                pageCount >= 2
                && marginRepeats.TryGetValue(Normalize(line.Text), out var pages)
                && pages.Count >= RepetitionThreshold * pageCount;

            bool IsKnownArtifact(PdfTextLine line) =>
                KnownArtifacts.Any(p => p.IsMatch(line.Text.Trim()));

            // Artefakty od krawędzi strony do pierwszego wiersza, który nim nie jest — osobno od góry i od dołu.
            var artifacts = new HashSet<PdfTextLine>(ReferenceEqualityComparer.Instance);
            foreach (var page in lines.Where(IsInMarginZone).GroupBy(l => l.PageNumber))
            {
                var top = page.Where(l => l.BaselineY > l.PageHeight / 2).OrderByDescending(l => l.BaselineY);
                var bottom = page.Where(l => l.BaselineY <= l.PageHeight / 2).OrderBy(l => l.BaselineY);
                foreach (var fromEdge in new[] { top, bottom })
                {
                    foreach (var line in fromEdge)
                    {
                        if (IsKnownArtifact(line))
                            artifacts.Add(line);
                        else if (IsRepeated(line) && !HasTextBetweenItAndTheEdge(line, lines, artifacts))
                            artifacts.Add(line);
                    }
                }
            }

            return lines.Where(l => !artifacts.Contains(l)).ToList();
        }

        /// <summary>Na stronie wiersza stoi bliżej krawędzi tekst, który nie jest artefaktem.</summary>
        private static bool HasTextBetweenItAndTheEdge(PdfTextLine line, List<PdfTextLine> lines, HashSet<PdfTextLine> artifacts)
        {
            var atTop = line.BaselineY > line.PageHeight / 2;
            return lines.Any(other => other.PageNumber == line.PageNumber && !ReferenceEquals(other, line)
                && !artifacts.Contains(other)
                && (atTop ? other.BaselineY > line.BaselineY : other.BaselineY < line.BaselineY));
        }

        private static bool IsInMarginZone(PdfTextLine line) =>
            line.BaselineY > (1 - MarginZoneRatio) * line.PageHeight ||
            line.BaselineY < MarginZoneRatio * line.PageHeight;

        /// <summary>Klucz kwalifikuje się do sygnału powtórzeniowego, gdy zawiera coś poza cyframi (#) i spacjami.</summary>
        private static bool IsRepetitionEligible(string normalizedKey) =>
            normalizedKey.Any(c => c != '#' && c != ' ');

        /// <summary>Normalizacja do wykrywania powtórzeń: cyfry → #, białe znaki zwinięte.</summary>
        private static string Normalize(string text)
        {
            var sb = new StringBuilder(text.Length);
            bool lastWasSpace = false;
            foreach (var c in text.Trim())
            {
                if (char.IsDigit(c))
                {
                    sb.Append('#');
                    lastWasSpace = false;
                }
                else if (char.IsWhiteSpace(c))
                {
                    if (!lastWasSpace) sb.Append(' ');
                    lastWasSpace = true;
                }
                else
                {
                    sb.Append(c);
                    lastWasSpace = false;
                }
            }
            return sb.ToString();
        }
    }
}
