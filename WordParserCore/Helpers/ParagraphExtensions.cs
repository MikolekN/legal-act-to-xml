using System.Text;
using DocumentFormat.OpenXml.Wordprocessing;

namespace WordParserCore
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
            var superscriptBuffer = new StringBuilder();
            bool inSuperscript = false;

            foreach (var run in paragraph.Descendants<Run>())
            {
                bool isSuperscript = IsSuperscriptRun(run);

                if (isSuperscript != inSuperscript)
                {
                    if (inSuperscript)
                        FlushSuperscript(sb, superscriptBuffer);
                    inSuperscript = isSuperscript;
                }

                StringBuilder target = isSuperscript ? superscriptBuffer : sb;
                foreach (var child in run.ChildElements)
                {
                    switch (child)
                    {
                        case Text text:
                            target.Append(text.Text);
                            break;

                        case SymbolChar sym
                            when string.Equals(sym.Font?.Value, "Symbol", StringComparison.OrdinalIgnoreCase)
                            && string.Equals(sym.Char?.Value, "F02D", StringComparison.OrdinalIgnoreCase):
                            target.Append('\u2013');
                            break;

                        case FootnoteReference fnRef:
                            target.Append(fnRef.Id?.Value);
                            break;

                        case TabChar:
                            target.Append('\t');
                            break;
                    }
                }
            }

            if (inSuperscript)
                FlushSuperscript(sb, superscriptBuffer);

            return sb.ToString();
        }

        /// <summary> Ujmuje w nawiasy kwadratowe treść indeksu górnego przenosząc białe znaki poza nawiasy. </summary>
        private static void FlushSuperscript(StringBuilder sb, StringBuilder buffer)
        {
            var content = buffer.ToString();
            buffer.Clear();

            if (content.Trim().Length == 0)
            {
                sb.Append(content);
                return;
            }

            int start = 0;
            while (start < content.Length && char.IsWhiteSpace(content[start]))
                start++;
            int end = content.Length;
            while (end > start && char.IsWhiteSpace(content[end - 1]))
                end--;

            sb.Append(content, 0, start);
            sb.Append('[').Append(content, start, end - start).Append(']');
            sb.Append(content, end, content.Length - end);
        }

        private static bool IsSuperscriptRun(Run run)
        {
            var rp = run.RunProperties;
            if (rp == null)
                return false;

            var styleVal = rp.RunStyle?.Val?.Value;
            if (styleVal != null &&
                (styleVal.StartsWith("IG", StringComparison.Ordinal) || styleVal == "Odwoanieprzypisudolnego"))
                return true;

            if (rp.VerticalTextAlignment?.Val?.Value == VerticalPositionValues.Superscript)
                return true;

            return false;
        }
    }
}
