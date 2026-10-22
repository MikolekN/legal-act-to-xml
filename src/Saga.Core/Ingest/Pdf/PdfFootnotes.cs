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

        /// <summary>
        /// Znacznik bez oznaczenia — warstwa tekstowa zapisała z indeksu górnego tylko nawias („[)]"), także z przecinkiem
        /// oddzielającym go od następnego odwołania („[),]").
        /// </summary>
        private static readonly Regex UnnumberedMarker = new(@"^\[\s*\)\s*(?<comma>,)?\s*\]$", RegexOptions.Compiled);

        private static readonly Regex Item = new(@"(?<![\p{L}\d])(?<symbol>\d{1,3}|[a-z]{1,2})\)", RegexOptions.Compiled);

        /// <summary>
        /// Oznaczenie na początku bloku odnośnika („[1)] …", „1) …", „[a)] …", bez oznaczenia — „[)] …", sama liczba
        /// w indeksie górnym — „[1] …").
        /// </summary>
        private static readonly Regex Start = new(
            @"^(?:\[(?<symbol>\d{1,3}|[a-z]{1,2})?\)\]|(?<plain>(?<symbol>\d{1,3})\))|\[(?<bare>(?<symbol>\d{1,3}))\](?=\s))\s*",
            RegexOptions.Compiled);

        /// <summary>Odnośnik oznaczony samą liczbą w indeksie górnym („[1] W przypadku …") — tak oznacza odnośniki część wzorów w załącznikach.</summary>
        private static readonly Regex BareStart = new(@"^\[\d{1,3}\]\s", RegexOptions.Compiled);

        /// <summary>
        /// Odwołanie do odnośnika oznaczonego samą liczbą („przemysłowe[3]", „R.[1]"). Liczba w indeksie górnym bywa też
        /// wykładnikiem jednostki („mln m[3]") i indeksem oznaczenia jednostki („art. 9[1]", „21a[1]") — za liczbą albo
        /// samą jednostką miary odwołaniem nie jest.
        /// </summary>
        private static readonly Regex BareReference = new(
            @"(?<!\d\p{Ll}{0,3}|(?<!\p{L})[kcdm]?m)\[(?<symbol>\d{1,3})\]", RegexOptions.Compiled);

        /// <summary>Akapit, który otwiera wyliczenie albo jest jego pozycją — następny „N)" jest jego dalszą pozycją.</summary>
        private static readonly Regex OpensItem = new(@"[:;]\s*$", RegexOptions.Compiled);

        /// <summary>Treść pozycji wyliczenia: mała litera albo nawias („(uchylony)"), nie wielka litera nowego odnośnika.</summary>
        private static readonly Regex ItemText = new(@"^[\p{Ll}(]", RegexOptions.Compiled);

        private sealed class Note(string symbol, int page, string text, bool bare = false, string mark = "")
        {
            public string Symbol { get; } = symbol;
            public int Page { get; } = page;
            public List<string> Paragraphs { get; } = [text];
            public bool Used { get; set; }

            /// <summary>Oznaczony samą liczbą w indeksie górnym — odwołuje się do niego <see cref="BareReference"/>.</summary>
            public bool Bare { get; } = bare;

            /// <summary>Oznaczenie na początku bloku, jak je wydrukowano („[1]").</summary>
            public string Mark { get; } = mark;
        }

        /// <summary>
        /// Odnośniki strefy przypisów. Odnośnik bywa wieloakapitowy — wyliczenie („…, o których mowa:" / „1) w art. 28 …;"
        /// / „2) w art. 39g …"), dalszy akapit — a jego akapity są w PDF osobnymi blokami. Blok bez oznaczenia jest dalszym
        /// akapitem poprzedniego odnośnika; blok „N)" zapisany zwykłym tekstem — jego pozycją wyliczenia, gdy poprzedni
        /// akapit otwiera wyliczenie albo kończy pozycję („:", „;") lub treść zaczyna się małą literą. Odnośnik oznaczony
        /// zwykłym tekstem zaczyna się wielką literą po odnośniku zakończonym kropką („2) W brzmieniu …").
        /// </summary>
        private static List<Note> ReadNotes(IReadOnlyList<DocumentBlock> footnotes)
        {
            var notes = new List<Note>();
            foreach (var block in footnotes)
            {
                var start = Start.Match(block.Text);
                var text = block.Text[(start.Success ? start.Length : 0)..].Trim();
                var previous = notes.Count > 0 ? notes[^1] : null;
                var continues = previous is not null
                    && (!start.Success
                        || (start.Groups["plain"].Success
                            && (OpensItem.IsMatch(previous.Paragraphs[^1]) || ItemText.IsMatch(text))));

                if (continues)
                    previous!.Paragraphs.Add(block.Text.Trim());
                else if (start.Success)
                    notes.Add(new Note(start.Groups["symbol"].Value, block.Source.PageNumber ?? 0, text, start.Groups["bare"].Success,
                        start.Value.Trim()));
            }

            return notes;
        }

        /// <param name="body">Bloki treści w kolejności czytania.</param>
        /// <param name="lastPages">Strona ostatniego wiersza każdego bloku treści.</param>
        /// <param name="footnotes">Bloki strefy przypisów.</param>
        /// <returns>Bloki treści ze znacznikami przenumerowanymi i odnośnikami dołączonymi do bloku odwołania.</returns>
        internal static List<DocumentBlock> Link(
            IReadOnlyList<DocumentBlock> body, IReadOnlyList<int> lastPages, IReadOnlyList<DocumentBlock> footnotes)
        {
            var notes = ReadNotes(footnotes);
            FoldUnreferencedBareNotes(notes, body, lastPages);

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
                    if (UnnumberedMarker.Match(superscript.Value) is { Success: true } unnumberedMarker)
                    {
                        var unnumbered = notes.FirstOrDefault(n => OnThesePages(n) && n.Symbol.Length == 0);
                        if (unnumbered is null)
                            return string.Empty;

                        unnumbered.Used = true;
                        var (symbol, marker, run) = numbering.NextUnknown();
                        (attached ??= []).Add(new BlockFootnote(symbol, unnumbered.Paragraphs) { Marker = marker, Run = run });
                        return "[" + marker + ")" + unnumberedMarker.Groups["comma"].Value + "]";
                    }

                    return Item.Replace(superscript.Value, item =>
                    {
                        var symbol = item.Groups["symbol"].Value;
                        // Numer złożony dwa razy („[11)]" przy odwołaniu „1)"): automatyczny numer Worda i ten sam numer
                        // wpisany ręcznie w indeksie górnym — DOCX wpisany numer pomija, PDF drukuje oba.
                        var note = notes.FirstOrDefault(n => OnThesePages(n) && n.Symbol == symbol)
                            ?? (char.IsDigit(symbol[0]) ? notes.FirstOrDefault(n => OnThesePages(n) && n.Symbol == symbol + symbol) : null)
                            ?? notes.FirstOrDefault(n => OnThesePages(n) && n.Symbol.Length == 0);
                        if (note is null)
                            return item.Value;

                        note.Used = true;
                        var (marker, run) = numbering.Next(symbol);
                        (attached ??= []).Add(new BlockFootnote(symbol, note.Paragraphs) { Marker = marker, Run = run });
                        return marker + ")";
                    });
                });

                // Odwołanie samą liczbą łączy się tylko z odnośnikiem tak samo oznaczonym — wykładnik bez odnośnika zostaje.
                text = BareReference.Replace(text, reference =>
                {
                    var symbol = reference.Groups["symbol"].Value;
                    var note = notes.FirstOrDefault(n => OnThesePages(n) && n.Bare && n.Symbol == symbol);
                    if (note is null)
                        return reference.Value;

                    note.Used = true;
                    var (marker, run) = numbering.Next(symbol);
                    (attached ??= []).Add(new BlockFootnote(symbol, note.Paragraphs) { Marker = marker, Run = run });
                    return "[" + marker + ")]";
                });

                result.Add(attached is null && text == block.Text ? block : block with { Text = text, Footnotes = attached ?? block.Footnotes });
            }

            return result;
        }

        /// <summary>
        /// Odnośnik oznaczony samą liczbą, do którego żaden tekst na jego stronie się nie odwołuje („[11] Niniejszy dokument …"
        /// w strefie przypisów albo odwołanie w komórce tabeli zaczętej na wcześniejszej stronie), jest dalszym tekstem
        /// poprzedniego odnośnika — jak czytało się go przed rozpoznaniem takich oznaczeń. Bez odwołania nie miałby gdzie
        /// stanąć i jego tekst by zginął.
        /// </summary>
        private static void FoldUnreferencedBareNotes(List<Note> notes, IReadOnlyList<DocumentBlock> body, IReadOnlyList<int> lastPages)
        {
            for (var n = notes.Count - 1; n > 0; n--)
            {
                var note = notes[n];
                if (!note.Bare)
                    continue;

                var referenced = body.Where((block, i) =>
                        (block.Source.PageNumber ?? 0) <= note.Page && note.Page <= (i < lastPages.Count ? lastPages[i] : block.Source.PageNumber ?? 0))
                    .Any(block => BareReferencedSymbols(block.Text).Contains(note.Symbol));
                if (referenced)
                    continue;

                // Odnośnik bywa już poszerzony o następny, złożony do niego wcześniej (pętla idzie od końca).
                var previous = notes[n - 1];
                previous.Paragraphs[^1] += " " + note.Mark + " " + note.Paragraphs[0];
                previous.Paragraphs.AddRange(note.Paragraphs.Skip(1));
                notes.RemoveAt(n);
            }
        }

        /// <summary>
        /// Bloki strefy przypisów, które nie są odnośnikiem ani jego dalszym akapitem: oznaczenia nie da się odczytać
        /// („[*] Wizerunek nie przedstawia monety …" pod tabelą wzoru — M.P. 2026 poz. 951), a odnośnika przed nimi nie ma.
        /// Nie wiążą się z niczym, więc wracają do treści strony — inaczej ich tekst by zginął.
        /// </summary>
        internal static List<DocumentBlock> Unreadable(IReadOnlyList<DocumentBlock> footnotes)
        {
            var unreadable = new List<DocumentBlock>();
            var anyNote = false;
            foreach (var block in footnotes)
            {
                if (Start.IsMatch(block.Text))
                    anyNote = true;
                else if (!anyNote)
                    unreadable.Add(block);
            }

            return unreadable;
        }

        /// <summary>Oznaczenia odnośników, do których tekst odwołuje się znacznikiem w indeksie górnym („[1), 2)]" → 1, 2).</summary>
        internal static IEnumerable<string> ReferencedSymbols(string text) =>
            Superscript.Matches(text).SelectMany(superscript => Item.Matches(superscript.Value).Select(item => item.Groups["symbol"].Value));

        /// <summary>Oznaczenia odnośników, do których tekst odwołuje się samą liczbą w indeksie górnym („przemysłowe[3]" → 3).</summary>
        internal static IEnumerable<string> BareReferencedSymbols(string text) =>
            BareReference.Matches(text).Select(reference => reference.Groups["symbol"].Value);

        /// <summary>Blok strefy przypisów zaczyna odnośnik oznaczony samą liczbą w indeksie górnym.</summary>
        internal static bool OpensBareNote(string text) => BareStart.IsMatch(text);

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
