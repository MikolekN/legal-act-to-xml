using System.Text.RegularExpressions;
using Saga.Core.Services;

namespace Saga.Core.Ingest
{
    /// <summary>
    /// Indeks górny oznaczenia jednostki w TXT (ADR-0089). Eksport do zwykłego tekstu zapisuje indeks górny zwykłymi
    /// cyframi: „Art. 29²." staje się „Art. 292.", „§ 2¹." — „§ 21.", ustęp „2¹." — „21.", punkt „5¹)" — „51)".
    /// Oznaczenie samo nie mówi, czy to indeks górny; mówi to ciąg numeracji: po „Art. 29." następnym zwykłym jest
    /// „Art. 30.", a „Art. 292." to dalszy ciąg oznaczenia 29 — jednostka dodana między 29 a 30.
    ///
    /// Reguła: oznaczenie, które zaczyna się oznaczeniem poprzedniej jednostki tego samego poziomu i nie jest jej
    /// zwykłym następnikiem, ma resztę w indeksie górnym — gdy reszta jest liczbą (z literą) większą od indeksu
    /// poprzedniej jednostki. Wynik ma postać kanału DOCX („Art. 29[2].", „§ 2[1].", „5[1])").
    ///
    /// Poziomy i ich ciągi: artykuły w całym dokumencie; paragrafy i ustępy w artykule (paragrafy rozporządzenia —
    /// w całym dokumencie); punkty w jednostce, którą wyliczają. Treść cytowana (od „ do ”) jest pomijana i ciągów
    /// nie zmienia: przytacza jednostki innego aktu, a ich numeracja nie musi być ciągła.
    /// </summary>
    internal static class SuperscriptDesignations
    {
        private const string Designation = @"\d+[a-z]{0,3}(?:\d{1,3}[a-z]?)?";

        /// <summary>Znacznik odnośnika za oznaczeniem („1.[22)] …", „44b)[10)] …").</summary>
        private const string Marker = @"(?:\[[^\[\]]*\)\])?";

        /// <summary>Indeks górny zapisany wprost (znakami indeksu Unicode — <see cref="PlainTextSuperscripts"/>).</summary>
        private const string Explicit = @"(?:\[(?<explicit>[^\[\]]*)\])?";

        private static readonly Regex Article = new($@"^(?<lead>Art\.\s*)(?<number>{Designation}){Explicit}(?=\.)", RegexOptions.Compiled);

        private static readonly Regex Paragraph = new($@"^(?<lead>§\s*)(?<number>{Designation}){Explicit}(?=\.)", RegexOptions.Compiled);

        private static readonly Regex Passage = new($@"^(?<lead>)(?<number>{Designation}){Explicit}(?=\.{Marker}\s)", RegexOptions.Compiled);

        private static readonly Regex Point = new($@"^(?<lead>)(?<number>{Designation}){Explicit}(?=\){Marker}\s)", RegexOptions.Compiled);

        /// <summary>Pierwsza jednostka niższego poziomu w wierszu jednostki wyższego („Art. 5. 1.", „Art. 5. § 1.", „§ 3. 1.").</summary>
        private static readonly Regex InlineParagraph = new($@"\G\.\s*§\s*(?<number>{Designation})\.", RegexOptions.Compiled);

        private static readonly Regex InlinePassage = new($@"\G\.\s*(?<number>{Designation})\.{Marker}\s", RegexOptions.Compiled);

        /// <summary>Reszta oznaczenia w indeksie górnym: liczba bez zera na początku, najwyżej z literą („2", „14a").</summary>
        private static readonly Regex SuperscriptPart = new(@"^(?<digits>[1-9]\d{0,2})(?<letter>[a-z]?)$", RegexOptions.Compiled);

        private static readonly Regex Number = new(@"^\d+$", RegexOptions.Compiled);

        /// <summary>Ostatnie oznaczenie poziomu: część zwykła i indeks górny.</summary>
        private sealed class Level
        {
            private string? _base;
            private string? _superscript;

            internal void Reset() => (_base, _superscript) = (null, null);

            internal void Set(string number) => (_base, _superscript) = (number, null);

            /// <summary>Oznaczenie z indeksem górnym zapisanym wprost: tylko zapamiętane jako ostatnie w ciągu.</summary>
            internal void SetExplicit(string number, string superscript) =>
                (_base, _superscript) = (number, SuperscriptPart.IsMatch(superscript) ? superscript : null);

            /// <summary>Indeks górny ukryty w oznaczeniu albo null; zapamiętuje oznaczenie jako ostatnie w ciągu.</summary>
            internal string? Read(string number)
            {
                if (_base is { } previous
                    && number.Length > previous.Length
                    && number.StartsWith(previous, StringComparison.Ordinal)
                    && !IsSuccessor(previous, number)
                    && SuperscriptPart.Match(number[previous.Length..]) is { Success: true } rest
                    && IsAfter(rest, _superscript))
                {
                    _superscript = rest.Value;
                    return rest.Value;
                }

                Set(number);
                return null;
            }

            private static bool IsSuccessor(string previous, string number) =>
                Number.IsMatch(previous) && Number.IsMatch(number)
                && long.TryParse(previous, out var p) && long.TryParse(number, out var n) && n == p + 1;

            private static bool IsAfter(Match rest, string? previous)
            {
                if (previous is null)
                    return true;
                var last = SuperscriptPart.Match(previous);
                var (digits, lastDigits) = (int.Parse(rest.Groups["digits"].Value), int.Parse(last.Groups["digits"].Value));
                return digits > lastDigits
                    || (digits == lastDigits && string.CompareOrdinal(rest.Groups["letter"].Value, last.Groups["letter"].Value) > 0);
            }
        }

        internal static IReadOnlyList<DocumentBlock> Restore(IReadOnlyList<DocumentBlock> blocks)
        {
            var articles = new Level();
            var paragraphs = new Level();
            var passages = new Level();
            var points = new Level();
            var quoteDepth = 0;

            var result = new List<DocumentBlock>(blocks.Count);
            foreach (var block in blocks)
            {
                var text = block.Text;
                var quoted = quoteDepth > 0 || text.StartsWith(QuotationMarks.Opening);
                quoteDepth = Math.Max(0, quoteDepth + Count(text, QuotationMarks.Opening) - text.Count(QuotationMarks.IsClosing));
                if (quoted)
                {
                    result.Add(block);
                    continue;
                }

                string? restored = null;
                if (Article.Match(text) is { Success: true } article)
                {
                    restored = Rewrite(text, article, Read(articles, article));
                    passages.Reset();
                    paragraphs.Reset();
                    points.Reset();
                    ReadInline(restored ?? text, article.Index + article.Length + (restored is null ? 0 : restored.Length - text.Length),
                        paragraphs, passages);
                }
                else if (Paragraph.Match(text) is { Success: true } paragraph)
                {
                    restored = Rewrite(text, paragraph, Read(paragraphs, paragraph));
                    passages.Reset();
                    points.Reset();
                    ReadInline(restored ?? text, paragraph.Index + paragraph.Length + (restored is null ? 0 : restored.Length - text.Length),
                        paragraphs: null, passages);
                }
                else if (Passage.Match(text) is { Success: true } passage)
                {
                    restored = Rewrite(text, passage, Read(passages, passage));
                    points.Reset();
                }
                else if (Point.Match(text) is { Success: true } point)
                {
                    restored = Rewrite(text, point, Read(points, point));
                }

                result.Add(restored is null ? block : block with { Text = restored });
            }

            return result;
        }

        /// <summary>Pierwsza jednostka niższego poziomu stojąca w wierszu jednostki wyższego otwiera swój ciąg.</summary>
        private static void ReadInline(string text, int at, Level? paragraphs, Level passages)
        {
            if (paragraphs is not null && InlineParagraph.Match(text, at) is { Success: true } paragraph)
                paragraphs.Set(paragraph.Groups["number"].Value);
            else if (InlinePassage.Match(text, at) is { Success: true } passage)
                passages.Set(passage.Groups["number"].Value);
        }

        /// <summary>Indeks górny ukryty w oznaczeniu — chyba że stoi już wprost; wtedy oznaczenie tylko przedłuża ciąg.</summary>
        private static string? Read(Level level, Match designation)
        {
            if (designation.Groups["explicit"].Success)
            {
                level.SetExplicit(designation.Groups["number"].Value, designation.Groups["explicit"].Value);
                return null;
            }

            return level.Read(designation.Groups["number"].Value);
        }

        private static string? Rewrite(string text, Match designation, string? superscript)
        {
            if (superscript is null)
                return null;

            var number = designation.Groups["number"];
            var plain = number.Value[..^superscript.Length];
            return text[..number.Index] + plain + "[" + superscript + "]" + text[(number.Index + number.Length)..];
        }

        private static int Count(string text, char character) => text.Count(c => c == character);
    }
}
