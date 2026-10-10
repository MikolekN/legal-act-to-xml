using System.Text;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Saga.Core
{
    public static class ParagraphExtensions
    {
        public static string? StyleId(this Paragraph paragraph)
        {
            return paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.ToString();
        }

        public static bool? StyleId(this Paragraph paragraph, string styleId)
        {
            return paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.ToString()?.StartsWith(styleId);
        }

        /// <summary>
        /// Ekstrahuje pelny tekst z akapitu, uwzgledniajac elementy pomijane
        /// przez standardowe InnerText:
        /// - SymbolChar (w:sym font="Symbol" char="F02D") → en-dash (\u2013)
        /// - FootnoteReference (w:footnoteReference) → numer przypisu
        /// - TabChar (w:tab) → tabulator
        /// - Tekst w indeksie gornym (styl IGindeksgrny, Odwoanieprzypisudolnego
        ///   lub VerticalAlignment=Superscript) jest ujmowany w nawiasy [ ].
        /// </summary>
        public static string GetFullText(this Paragraph paragraph)
        {
            var sb = new StringBuilder();
            var superscript = new StringBuilder();

            foreach (var run in paragraph.Descendants<Run>())
            {
                var text = RunText(run);

                // Przebieg bez tekstu (znaczniki pola: odnośnik wstawiony polem NOTEREF) nie rozcina indeksu
                // górnego — inaczej „8" i „)" jednego odnośnika stają w dwóch nawiasach „[8][)]".
                if (text.Length == 0)
                    continue;

                if (IsSuperscriptRun(run))
                {
                    superscript.Append(text);
                    continue;
                }

                FlushSuperscript(sb, superscript);
                sb.Append(text);
            }

            FlushSuperscript(sb, superscript);
            return sb.ToString();
        }

        private static string RunText(Run run)
        {
            var sb = new StringBuilder();
            foreach (var child in run.ChildElements)
            {
                switch (child)
                {
                    case Text text:
                        sb.Append(text.Text);
                        break;

                    case SymbolChar sym
                        when string.Equals(sym.Font?.Value, "Symbol", StringComparison.OrdinalIgnoreCase)
                          && string.Equals(sym.Char?.Value, "F02D", StringComparison.OrdinalIgnoreCase):
                        sb.Append('\u2013');
                        break;

                    case FootnoteReference fnRef:
                        sb.Append(fnRef.Id?.Value);
                        break;

                    case TabChar:
                        sb.Append('\t');
                        break;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Zamyka grupę indeksu górnego: nawias obejmuje samą treść, a odstępy sformatowane jako indeks
        /// zostają zwykłym tekstem („4.[ ]W sprawach" → „4. W sprawach", „[7) ]w sprawach" → „[7)] w sprawach").
        /// </summary>
        private static void FlushSuperscript(StringBuilder sb, StringBuilder superscript)
        {
            if (superscript.Length == 0)
                return;

            var value = superscript.ToString();
            superscript.Clear();

            var core = value.Trim();
            if (core.Length == 0)
            {
                sb.Append(value);
                return;
            }

            var leading = value.Length - value.TrimStart().Length;
            var trailing = value.Length - value.TrimEnd().Length;
            sb.Append(value, 0, leading).Append('[').Append(core).Append(']').Append(value, value.Length - trailing, trailing);
        }

        private static bool IsSuperscriptRun(Run run)
        {
            var rp = run.RunProperties;
            if (rp == null)
                return false;

            // Szablon ma cztery style indeksu górnego (IG, IGP — pogrubiony, IGK — kursywa, IGPK);
            // oznaczenia jednostek są pogrubione, więc ich indeks idzie stylem IGP.
            var styleVal = rp.RunStyle?.Val?.Value;
            if (styleVal is "Odwoanieprzypisudolnego"
                || (styleVal != null && styleVal.StartsWith("IG", StringComparison.Ordinal)
                    && styleVal.Contains("indeksgrny", StringComparison.Ordinal)))
                return true;

            if (rp.VerticalTextAlignment?.Val?.Value == VerticalPositionValues.Superscript)
                return true;

            return false;
        }
    }
}
