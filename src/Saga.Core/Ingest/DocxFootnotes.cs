using DocumentFormat.OpenXml.Packaging;
using Word = DocumentFormat.OpenXml.Wordprocessing;

namespace Saga.Core.Ingest
{
    /// <summary>
    /// Przypisy dokumentu Word: numer, pod którym każdy stoi w akcie, i jego treść.
    /// Treść leży w osobnej części pakietu (<c>word/footnotes.xml</c>), a w treści aktu stoi tylko odwołanie.
    /// </summary>
    internal sealed class DocxFootnotes
    {
        private readonly Dictionary<long, int> _numbers = new();
        private readonly Dictionary<long, IReadOnlyList<string>> _texts = new();

        /// <summary>Numer przypisu w akcie dla każdego identyfikatora <c>w:id</c>.</summary>
        internal IReadOnlyDictionary<long, int> Numbers => _numbers;

        internal static DocxFootnotes Read(MainDocumentPart mainPart)
        {
            var footnotes = new DocxFootnotes();

            // Kolejne odwołania w treści dostają 1, 2, 3… Identyfikator w:id numerem nie jest — Word zaczyna
            // go zwykle od 2 (wcześniejsze zajmują separatory), więc pierwszy odnośnik aktu ma id 2.
            foreach (var reference in mainPart.Document.Descendants<Word.FootnoteReference>())
            {
                // Odwołanie z własnym znakiem (gwiazdka) nie bierze numeru z licznika.
                if (reference.CustomMarkFollows?.Value == true || reference.Id?.Value is not { } id)
                    continue;

                footnotes._numbers.TryAdd(id, footnotes._numbers.Count + 1);
            }

            foreach (var footnote in mainPart.FootnotesPart?.Footnotes?.Elements<Word.Footnote>() ?? [])
            {
                if (footnote.Id?.Value is not { } id || !footnotes._numbers.ContainsKey(id))
                    continue;

                footnotes._texts[id] = footnote.Descendants<Word.Paragraph>()
                    .Select(paragraph => WithoutOwnMark(paragraph.GetFullText(footnotes._numbers)))
                    .Where(text => text.Length > 0)
                    .ToList();
            }

            return footnotes;
        }

        /// <summary>Przypisy, do których odwołuje się akapit, w kolejności odwołań.</summary>
        internal IReadOnlyList<BlockFootnote> ReferencedBy(Word.Paragraph paragraph)
        {
            List<BlockFootnote>? referenced = null;
            foreach (var reference in paragraph.Descendants<Word.FootnoteReference>())
            {
                if (reference.Id?.Value is not { } id || !_numbers.TryGetValue(id, out var number))
                    continue;

                (referenced ??= []).Add(new BlockFootnote(
                    number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _texts.GetValueOrDefault(id) ?? []));
            }

            return referenced ?? (IReadOnlyList<BlockFootnote>)[];
        }

        // Treść przypisu zaczyna własny znacznik: pusty znak odwołania i nawias w indeksie górnym — „[)]".
        private static string WithoutOwnMark(string text)
        {
            var trimmed = text.TrimStart();
            if (trimmed.StartsWith("[)]", StringComparison.Ordinal))
                trimmed = trimmed[3..];
            else if (trimmed.StartsWith(')'))
                trimmed = trimmed[1..];

            return trimmed.Trim();
        }
    }
}
