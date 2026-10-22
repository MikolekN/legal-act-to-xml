#nullable enable
using System.Text;
using System.Text.RegularExpressions;

namespace Saga.Core.Ingest.Pdf
{
    /// <summary>
    /// Dywiz na końcu wiersza PDF: przeniesienie wyrazu (dywiz znika) albo złożenie (dywiz zostaje). Rozstrzyga konwencja
    /// składu Dziennika Ustaw i Monitora Polskiego (ADR-0111): złożenie dzielone na dywizie ma dywiz powtórzony na początku
    /// następnego wiersza, przeniesienie — nie. Skład bez tej konwencji (PDF z edytora) dzieli złożenie tak samo jak wyraz;
    /// wtedy świadkiem jest sam dokument: pierwszy człon, który stoi gdzie indziej w środku wiersza przed dywizem
    /// („kolejowo-drogowego”), zostaje członem złożenia, chyba że dokument zna też wyraz sklejony.
    /// </summary>
    internal sealed class PdfHyphenation
    {
        /// <summary>Rozstrzygnięcie bez świadectwa dokumentu — sama konwencja składu.</summary>
        internal static PdfHyphenation ConventionOnly { get; } = new([], []);

        /// <summary>Wyraz: litery, także połączone dywizem w złożenie — dywiz na końcu wiersza nie wchodzi do wyrazu.</summary>
        private static readonly Regex WordPattern = new(@"\p{L}+(?:-\p{L}+)*", RegexOptions.Compiled);

        /// <summary>
        /// Spójnik za dywizem zawieszonym („Tele- i Radiokomunikacji", „krótko- lub długoterminowe", „od pięcio- do
        /// dziesięcioletnich") — początek wiersza po dywizie, który nie dzieli wyrazu.
        /// </summary>
        private static readonly Regex SuspendedHyphenFollowerPattern = new(
            @"^(?:i|lub|albo|oraz|czy|bądź|ani|do)\s", RegexOptions.Compiled);

        /// <summary>Miękki dywiz — tak niektóre składy zapisują przeniesienie wyrazu.</summary>
        private const char SoftHyphen = (char)0xAD;

        /// <summary>Wyrazy bez dywizu, małymi literami.</summary>
        private readonly HashSet<string> words;

        /// <summary>Człony przed dywizem złożeń zapisanych w środku wiersza, małymi literami („kolejowo”).</summary>
        private readonly HashSet<string> compoundHeads;

        private PdfHyphenation(HashSet<string> words, HashSet<string> compoundHeads)
        {
            this.words = words;
            this.compoundHeads = compoundHeads;
        }

        /// <summary>Świadectwo dokumentu z jego wierszy (treść, komórki tabel, przypisy).</summary>
        internal static PdfHyphenation From(IEnumerable<string> lines)
        {
            var words = new HashSet<string>(StringComparer.Ordinal);
            var heads = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in lines)
            {
                foreach (Match match in WordPattern.Matches(line))
                {
                    var parts = match.Value.ToLowerInvariant().Split('-');
                    if (parts.Length == 1)
                        words.Add(parts[0]);
                    else
                        heads.UnionWith(parts[..^1]);
                }
            }

            return new PdfHyphenation(words, heads);
        }

        /// <summary>
        /// Dokleja wiersz do bloku. Dywiz na końcu wiersza po literze: powtórzony na początku następnego wiersza
        /// („metylo-" / „-tert-butylowym") — złożenie, zostaje jeden; przed spójnikiem („Tele- i Radiokomunikacji") — dywiz
        /// zawieszony, zostaje z odstępem; przed małą literą — przeniesienie („prak-" / „tyczne"), dywiz znika, chyba że
        /// dokument świadczy o złożeniu; przed wielką literą albo cyfrą („Warszawa-Praga", „COVID-19") dywiz zostaje.
        /// </summary>
        internal void Join(StringBuilder buffer, string next)
        {
            var hyphenated = buffer.Length > 1 && (buffer[^1] == '-' || buffer[^1] == SoftHyphen) && char.IsLetter(buffer[^2]);
            if (!hyphenated)
            {
                if (buffer.Length > 0 && buffer[^1] != '-')
                    buffer.Append(' ');
                buffer.Append(next);
                return;
            }

            if (next.Length > 1 && next[0] == '-' && char.IsLetter(next[1]))
            {
                buffer[^1] = '-';
                buffer.Append(next, 1, next.Length - 1);
            }
            else if (SuspendedHyphenFollowerPattern.IsMatch(next))
            {
                buffer[^1] = '-';
                buffer.Append(' ').Append(next);
            }
            else if (next.Length > 0 && char.IsLower(next[0]) && !IsAttestedCompound(buffer, next))
            {
                buffer.Length--;
                buffer.Append(next);
            }
            else
            {
                buffer[^1] = '-';
                buffer.Append(next);
            }
        }

        /// <summary>Człon przed dywizem stoi w dokumencie przed dywizem złożenia, a wyrazu sklejonego dokument nie zna.</summary>
        private bool IsAttestedCompound(StringBuilder buffer, string next)
        {
            var start = buffer.Length - 1;
            while (start > 0 && char.IsLetter(buffer[start - 1]))
                start--;
            var head = buffer.ToString(start, buffer.Length - 1 - start).ToLowerInvariant();
            if (!compoundHeads.Contains(head))
                return false;

            var end = 0;
            while (end < next.Length && char.IsLetter(next[end]))
                end++;
            return !words.Contains(head + next[..end].ToLowerInvariant());
        }
    }
}
