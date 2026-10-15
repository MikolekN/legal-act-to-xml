#nullable enable
using System.Globalization;
using System.Text.RegularExpressions;

namespace Saga.Core.Ingest.Pdf
{
    /// <summary>
    /// Wiąże odnośniki PDF ze znacznikami w treści — tak, jak DOCX podaje je czytnikowi gotowe: znacznik „[N)]"
    /// w tekście bloku z numerem jednoznacznym w dokumencie i <see cref="BlockFootnote"/> z oznaczeniem drukowanym
    /// i treścią (ADR-0061). PDF ma tylko dwie rzeczy osobno: znacznik w indeksie górnym w treści („[1)]", „[a)]")
    /// i blok strefy przypisów zaczynający się tym samym oznaczeniem.
    ///
    /// Odnośnik stoi na stronie, na której jest odwołanie, więc znacznik bloku łączy się z pierwszym niezużytym
    /// odnośnikiem o tym samym oznaczeniu ze stron, które blok zajmuje. Odnośnik, którego oznaczenia warstwa tekstowa
    /// nie zapisała („[)]"), łączy się ze znacznikiem, który swojego odnośnika na tych stronach nie ma — w kolejności,
    /// bo odnośniki strony stoją w kolejności odwołań. Znacznik bez odnośnika (powtórzone odwołanie, odnośnik aktu
    /// cytowanego, indeks górny, który odwołaniem nie jest) zostaje bez zmian.
    ///
    /// Numer znacznika: oznaczenie liczbowe albo pozycja litery w alfabecie, a gdy oznaczenia zaczynają się od nowa
    /// albo zmieniają zapis (litery obwieszczenia, liczby ogłaszanego aktu, znów liczby od 1 w załączniku), kolejny
    /// ciąg numeracji z numerami przesuniętymi o <see cref="RunBase"/> — jak w DOCX, bo numer pierwszego wystąpienia
    /// musi w dokumencie rosnąć.
    /// </summary>
    internal static class PdfFootnotes
    {
        private const int RunBase = 100_000;

        /// <summary>Indeks górny z co najmniej jednym oznaczeniem odnośnika („[1)]", „[1), 2)]", „[a)]").</summary>
        private static readonly Regex Superscript = new(@"\[[^\[\]]*\)[^\[\]]*\]", RegexOptions.Compiled);

        /// <summary>Znacznik bez oznaczenia — warstwa tekstowa zapisała z indeksu górnego tylko nawias („[)]").</summary>
        private static readonly Regex UnnumberedMarker = new(@"^\[\s*\)\s*\]$", RegexOptions.Compiled);

        private static readonly Regex Item = new(@"(?<![\p{L}\d])(?<symbol>\d{1,3}|[a-z]{1,2})\)", RegexOptions.Compiled);

        /// <summary>Oznaczenie na początku bloku odnośnika („[1)] …", „1) …", „[a)] …", bez oznaczenia — „[)] …").</summary>
        private static readonly Regex Start = new(
            @"^(?:\[(?<symbol>\d{1,3}|[a-z]{1,2})?\)\]|(?<symbol>\d{1,3})\))\s*", RegexOptions.Compiled);

        private sealed class Note(string symbol, int page, string text)
        {
            public string Symbol { get; } = symbol;
            public int Page { get; } = page;
            public string Text { get; } = text;
            public bool Used { get; set; }
        }

        /// <param name="body">Bloki treści w kolejności czytania.</param>
        /// <param name="lastPages">Strona ostatniego wiersza każdego bloku treści.</param>
        /// <param name="footnotes">Bloki strefy przypisów.</param>
        /// <returns>Bloki treści ze znacznikami przenumerowanymi i odnośnikami dołączonymi do bloku odwołania.</returns>
        internal static List<DocumentBlock> Link(
            IReadOnlyList<DocumentBlock> body, IReadOnlyList<int> lastPages, IReadOnlyList<DocumentBlock> footnotes)
        {
            var notes = footnotes
                .Select(block => (Block: block, Start: Start.Match(block.Text)))
                .Where(note => note.Start.Success)
                .Select(note => new Note(note.Start.Groups["symbol"].Value, note.Block.Source.PageNumber ?? 0,
                    note.Block.Text[note.Start.Length..].Trim()))
                .ToList();

            var result = new List<DocumentBlock>(body.Count);
            var numbering = new Numbering();
            for (var i = 0; i < body.Count; i++)
            {
                var block = body[i];
                var first = block.Source.PageNumber ?? 0;
                var last = i < lastPages.Count ? lastPages[i] : first;
                List<BlockFootnote>? attached = null;

                bool OnThesePages(Note n) => !n.Used && n.Page >= first && n.Page <= last;

                var text = Superscript.Replace(block.Text, superscript =>
                {
                    // Odwołanie bez oznaczenia łączy się z odnośnikiem bez oznaczenia z tej samej strony, w kolejności,
                    // i dostaje kolejny numer bieżącego ciągu; bez odnośnika zostaje sam ślad — ginie.
                    if (UnnumberedMarker.IsMatch(superscript.Value))
                    {
                        var unnumbered = notes.FirstOrDefault(n => OnThesePages(n) && n.Symbol.Length == 0);
                        if (unnumbered is null)
                            return string.Empty;

                        unnumbered.Used = true;
                        var (symbol, marker, run) = numbering.NextUnknown();
                        (attached ??= []).Add(new BlockFootnote(symbol, [unnumbered.Text]) { Marker = marker, Run = run });
                        return "[" + marker + ")]";
                    }

                    return Item.Replace(superscript.Value, item =>
                    {
                        var symbol = item.Groups["symbol"].Value;
                        var note = notes.FirstOrDefault(n => OnThesePages(n) && n.Symbol == symbol)
                            ?? notes.FirstOrDefault(n => OnThesePages(n) && n.Symbol.Length == 0);
                        if (note is null)
                            return item.Value;

                        note.Used = true;
                        var (marker, run) = numbering.Next(symbol);
                        (attached ??= []).Add(new BlockFootnote(symbol, [note.Text]) { Marker = marker, Run = run });
                        return marker + ")";
                    });
                });

                result.Add(attached is null && text == block.Text ? block : block with { Text = text, Footnotes = attached ?? block.Footnotes });
            }

            return result;
        }

        /// <summary>Oznaczenia odnośników, do których tekst odwołuje się znacznikiem w indeksie górnym („[1), 2)]" → 1, 2).</summary>
        internal static IEnumerable<string> ReferencedSymbols(string text) =>
            Superscript.Matches(text).SelectMany(superscript => Item.Matches(superscript.Value).Select(item => item.Groups["symbol"].Value));

        /// <summary>Numery znaczników kolejnych odnośników: ciąg numeracji zmienia się, gdy oznaczenia zaczynają się od nowa.</summary>
        private sealed class Numbering
        {
            private int _run = -1;
            private int _last;
            private bool _letters;

            internal (string Marker, int Run) Next(string symbol)
            {
                var letters = !char.IsDigit(symbol[0]);
                var value = letters
                    ? (symbol[0] - 'a' + 1) + 26 * (symbol.Length - 1)
                    : int.Parse(symbol, NumberStyles.None, CultureInfo.InvariantCulture);

                if (_run < 0 || letters != _letters || value <= _last)
                    _run++;

                _last = value;
                _letters = letters;
                return ((_run * RunBase + value).ToString(CultureInfo.InvariantCulture), _run);
            }

            /// <summary>Odnośnik bez oznaczenia: następny w bieżącym ciągu numeracji (oznaczenie odtworzone z kolejności).</summary>
            internal (string Symbol, string Marker, int Run) NextUnknown()
            {
                if (_run < 0)
                    _run = 0;

                _last++;
                var symbol = _letters
                    ? new string((char)('a' + (_last - 1) % 26), (_last - 1) / 26 + 1)
                    : _last.ToString(CultureInfo.InvariantCulture);
                return (symbol, (_run * RunBase + _last).ToString(CultureInfo.InvariantCulture), _run);
            }
        }
    }
}
