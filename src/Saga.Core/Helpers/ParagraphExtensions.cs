using System.Text;
using DocumentFormat.OpenXml.Wordprocessing;
using Saga.Core.Helpers;

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
        /// - FootnoteReference (w:footnoteReference) → numer przypisu jak w akcie (<paramref name="footnoteNumbers"/>);
        ///   bez tej mapy zostaje identyfikator w:id, który bywa przesunięty względem numeru
        /// - TabChar (w:tab) → tabulator
        /// - NoBreakHyphen (w:noBreakHyphen) → łącznik; Break / CarriageReturn (w:br, w:cr) → odstęp
        /// - Tekst w indeksie gornym (styl IGindeksgrny, Odwoanieprzypisudolnego
        ///   lub VerticalAlignment=Superscript) jest ujmowany w nawiasy [ ].
        /// - Równanie Worda (m:oMath) wchodzi do tekstu w zapisie liniowym (<see cref="OfficeMathText"/>),
        ///   w miejscu, w którym stoi w akapicie — symbol otwierający wiersz legendy nie znika.
        /// </summary>
        /// <param name="footnoteNumbers">Numer przypisu, pod którym stoi w akcie, dla każdego w:id.</param>
        public static string GetFullText(this Paragraph paragraph, IReadOnlyDictionary<long, int>? footnoteNumbers = null)
        {
            var sb = new StringBuilder();
            var superscript = new StringBuilder();

            foreach (var element in paragraph.Descendants())
            {
                // Wnętrze równania czyta OfficeMathText — jego części nie są osobnymi źródłami tekstu.
                if (element.Ancestors().TakeWhile(ancestor => ancestor != paragraph).Any(OfficeMathText.IsMath))
                    continue;

                if (OfficeMathText.IsMath(element))
                {
                    FlushSuperscript(sb, superscript);
                    sb.Append(OfficeMathText.Linear(element));
                    continue;
                }

                if (element is not Run run)
                    continue;

                var text = RunText(run, footnoteNumbers);

                // Przebieg bez tekstu (znaczniki pola: odnośnik wstawiony polem NOTEREF) nie rozcina indeksu
                // górnego — inaczej „8" i „)" jednego odnośnika stają w dwóch nawiasach „[8][)]".
                if (text.Length == 0)
                    continue;

                // Wersaliki nadane wprost przebiegowi to w akcie wielkie litery — tekst czytamy tak, jak go wydrukowano.
                if (IsCapsRun(run))
                    text = text.ToUpperInvariant();

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

        private static string RunText(Run run, IReadOnlyDictionary<long, int>? footnoteNumbers)
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
                        if (fnRef.Id?.Value is { } id && footnoteNumbers != null && footnoteNumbers.TryGetValue(id, out var number))
                            sb.Append(number);
                        else
                            sb.Append(fnRef.Id?.Value);
                        break;

                    case TabChar:
                        sb.Append('\t');
                        break;

                    // Łącznik nierozdzielający jest w pliku osobnym elementem, nie znakiem — bez niego
                    // „Celno-Skarbowej" zlewa się w „CelnoSkarbowej", a „pkt 1-3" w „pkt 13".
                    case NoBreakHyphen:
                        sb.Append('-');
                        break;

                    // Wymuszone przejście do nowego wiersza rozdziela wyrazy tak jak odstęp.
                    case Break or CarriageReturn:
                        sb.Append(' ');
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

        /// <summary>
        /// Wersaliki nadane przebiegowi wprost (<c>w:caps</c>). Wersaliki ze stylu zostają tekstem, jak go
        /// zapisano: styl jest prezentacją rodzaju tekstu („Ustawa" w stylu rodzaju aktu), a formatowanie
        /// wprost — decyzją o tym jednym tekście (tytuł załącznika). Kapitaliki liter nie zmieniają.
        /// </summary>
        private static bool IsCapsRun(Run run) =>
            run.RunProperties?.Caps is { } caps && (caps.Val?.Value ?? true);

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
