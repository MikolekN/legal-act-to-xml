using System.Text;
using System.Text.RegularExpressions;

namespace Saga.Core.Ingest
{
    /// <summary>
    /// Indeks górny w zwykłym tekście zapisany na kanał „[…]" czytników DOCX i PDF. Tekst bez formatowania zapisuje
    /// indeks górny na dwa sposoby:
    /// <list type="bullet">
    /// <item>znakami indeksu górnego Unicode („Art. 29²", „ustaw¹⁾") — wtedy wiadomo dokładnie, co jest w indeksie;</item>
    /// <item>zwykłymi cyframi — znacznik odnośnika przyklejony do słowa („ustaw1), 2)", „z późn. zm.2))"). Cyfra
    ///   z nawiasem tuż za wyrazem nie jest zapisem tekstu aktu, tylko znacznikiem, który stracił indeks górny.</item>
    /// </list>
    /// Oznaczenia jednostek zapisane zwykłymi cyframi („Art. 292.") odtwarza osobno <see cref="SuperscriptDesignations"/>.
    /// </summary>
    internal static class PlainTextSuperscripts
    {
        private const string Digits = "⁰¹²³⁴⁵⁶⁷⁸⁹";

        private static readonly Dictionary<char, char> Plain = BuildPlain();

        /// <summary>
        /// Znacznik odnośnika przyklejony do słowa: liczba z nawiasem (kilka — po przecinku) tuż za co najmniej trzema
        /// literami („ustaw1)", „ADMINISTRACJI1)") albo za „zm.", przed odstępem, interpunkcją albo końcem. Krótszy wyraz
        /// („dm3)", „CO2)") to jednostka miary albo wzór z indeksem, nie wyraz ze znacznikiem.
        /// </summary>
        private static readonly Regex GluedFootnoteMarker = new(
            @"(?<=\p{L}{3}|\bzm\.)(?<marker>[1-9]\d{0,2}\)(?:,\s?[1-9]\d{0,2}\))*)(?=[\s,;:.”""\)]|$)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Oznaczenie jednostki na początku wiersza z przyklejonym do niego znacznikiem odnośnika: „44b)10) …",
        /// „1d.22) …", „Art. 13mb.44) 1. …", także jednostka niższego poziomu w tym samym wierszu („Art. 5. 1.22) …").
        /// Tekst jednolity daje odnośnik niemal każdej zmienionej jednostce, a eksport tekstu stawia jego numer tuż za
        /// oznaczeniem — bez kanału „[…]" oznaczenie przestawało być rozpoznawalne.
        /// </summary>
        private static readonly Regex DesignationWithMarker = new(
            @"^(?<head>„?(?:(?:Art\.|§)\s*\d+[a-z]*(?:\[[^\]]*\])?\.\s+(?:§\s*)?)?\d+[a-z]*(?:\[[^\]]*\])?[.)]|„?(?:Art\.|§)\s*\d+[a-z]*(?:\[[^\]]*\])?\.)(?<marker>\d{1,3}\)|\))(?=\s)",
            RegexOptions.Compiled);

        internal static string Restore(string line)
        {
            var text = line.Any(Plain.ContainsKey) ? Unicode(line) : line;
            text = GluedFootnoteMarker.Replace(text, match => "[" + match.Groups["marker"].Value + "]");

            // Znacznik bez numeru („1.)", „Art. 13ja.)") to kotwica odnośnika, której numeru eksport nie zapisał —
            // a treści odnośnika eksport tekstu nie ma wcale; zostaje samo oznaczenie.
            return DesignationWithMarker.Replace(text, match => match.Groups["marker"].Value == ")"
                ? match.Groups["head"].Value
                : match.Groups["head"].Value + "[" + match.Groups["marker"].Value + "]");
        }

        /// <summary>Ciąg znaków indeksu górnego Unicode → „[…]" ze zwykłymi znakami.</summary>
        private static string Unicode(string line)
        {
            var sb = new StringBuilder(line.Length + 4);
            var open = false;
            foreach (var c in line)
            {
                if (Plain.TryGetValue(c, out var plain))
                {
                    if (!open)
                        sb.Append('[');
                    open = true;
                    sb.Append(plain);
                    continue;
                }

                if (open)
                    sb.Append(']');
                open = false;
                sb.Append(c);
            }

            if (open)
                sb.Append(']');
            return sb.ToString();
        }

        private static Dictionary<char, char> BuildPlain()
        {
            var map = new Dictionary<char, char>();
            for (var i = 0; i < Digits.Length; i++)
                map[Digits[i]] = (char)('0' + i);
            map['⁾'] = ')';
            map['⁽'] = '(';
            foreach (var (superscript, letter) in new[] { ('ᵃ', 'a'), ('ᵇ', 'b'), ('ᶜ', 'c'), ('ᵈ', 'd'), ('ᵉ', 'e'), ('ᶠ', 'f'), ('ᵍ', 'g'), ('ʰ', 'h'), ('ⁱ', 'i'), ('ʲ', 'j') })
                map[superscript] = letter;
            return map;
        }
    }
}
