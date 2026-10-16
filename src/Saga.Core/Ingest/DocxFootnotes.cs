using System.Globalization;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Saga.Core.Helpers;
using Word = DocumentFormat.OpenXml.Wordprocessing;

namespace Saga.Core.Ingest
{
    /// <summary>
    /// Przypisy dokumentu Word: numer kolejny odwołania, oznaczenie, pod którym przypis stoi w akcie, i jego treść.
    /// Treść leży w osobnej części pakietu (<c>word/footnotes.xml</c>), a w treści aktu stoi tylko odwołanie.
    /// </summary>
    internal sealed class DocxFootnotes
    {
        /// <summary>Oznaczenie przypisu w akcie, ciąg numeracji, w którym stoi, i czy to własny znak odwołania.</summary>
        private sealed record Designation(string Symbol, int Run, bool Custom = false);

        private const string Settings = "footnotePr";

        /// <summary>Indeks górny w notacji kanału tekstowego: „[…]".</summary>
        private static readonly Regex Superscript = new(@"\[[^\[\]]*\]", RegexOptions.Compiled);

        /// <summary>Liczba w indeksie górnym, za którą nie stoi nawias — nie jest znacznikiem odnośnika.</summary>
        private static readonly Regex BareNumber = new(@"(?<!\d)\d+(?!\d|\s*\))", RegexOptions.Compiled);

        /// <summary>
        /// Numery znaczników kolejnych ciągów numeracji zaczynają się od wielokrotności tej liczby — poza zakresem
        /// numerów drukowanych, więc znacznik wpisany w akcie tekstem („[2)]") nie pomyli się z odwołaniem.
        /// </summary>
        private const int RunBase = 100_000;

        private readonly Dictionary<long, int> _numbers = new();
        private readonly Dictionary<long, Designation> _designations = new();
        private readonly Dictionary<long, IReadOnlyList<string>> _texts = new();

        /// <summary>
        /// Numer, pod którym odwołanie stoi w tekście akapitu („[3)]"), dla każdego identyfikatora <c>w:id</c>.
        /// W pierwszym ciągu numeracji to numer przypisu w akcie; w kolejnych — ten sam numer przesunięty
        /// o wielokrotność <see cref="RunBase"/>, bo numery drukowane powtarzają się między ciągami.
        /// </summary>
        internal IReadOnlyDictionary<long, int> Numbers => _numbers;

        internal static DocxFootnotes Read(MainDocumentPart mainPart)
        {
            var footnotes = new DocxFootnotes();
            var documentWide = mainPart.DocumentSettingsPart?.Settings?.Elements().FirstOrDefault(e => e.LocalName == Settings);

            // Kolejne odwołania w treści dostają 1, 2, 3… Identyfikator w:id numerem nie jest — Word zaczyna
            // go zwykle od 2 (wcześniejsze zajmują separatory), więc pierwszy odnośnik aktu ma id 2.
            // Sekcja dokumentu może wznawiać numerację i zmieniać jej zapis („a)" w obwieszczeniu, „1)"
            // w ogłaszanym akcie i znów „1)" w załączniku) — wtedy zaczyna się nowy ciąg numeracji.
            var printed = 0;
            var slot = 0;
            var run = 0;
            var first = true;
            var runHasFootnotes = false;
            foreach (var (references, own) in Sections(mainPart.Document))
            {
                var settings = own ?? documentWide;
                var start = int.TryParse(Setting(settings, "numStart"), NumberStyles.None, CultureInfo.InvariantCulture, out var given) ? given : 1;
                if (first)
                {
                    printed = slot = start - 1;
                }
                else if (Setting(settings, "numRestart") == "eachSect")
                {
                    // Nowy ciąg zaczyna się dopiero wtedy, gdy poprzedni ma odnośniki: sekcja bez odnośników
                    // (obwieszczenie przed ogłaszanym aktem) niczego nie przesuwa.
                    printed = slot = start - 1;
                    if (runHasFootnotes)
                    {
                        run++;
                        runHasFootnotes = false;
                    }
                }

                first = false;
                foreach (var reference in references)
                {
                    if (reference.Id?.Value is not { } id || footnotes._numbers.ContainsKey(id))
                        continue;

                    // Odwołanie z własnym znakiem („3)", „*", „za") nie bierze numeru z licznika Worda: oznaczeniem
                    // jest ten znak. Znacznik w tekście dostaje jednak kolejny numer — znaczniki rosną w kolejności
                    // dokumentu (ADR-0061), a bez własnych znaków są to numery z licznika (ADR-0095).
                    string symbol;
                    if (reference.CustomMarkFollows?.Value == true)
                    {
                        symbol = CustomSymbol(ParagraphExtensions.CustomMark(reference));
                        if (symbol.Length == 0)
                            continue;
                    }
                    else
                    {
                        printed++;
                        symbol = Format(printed, Setting(settings, "numFmt"));
                    }

                    slot++;
                    runHasFootnotes = true;
                    footnotes._numbers[id] = run * RunBase + slot;
                    footnotes._designations[id] = new Designation(symbol, run, Custom: reference.CustomMarkFollows?.Value == true);
                }
            }

            foreach (var footnote in mainPart.FootnotesPart?.Footnotes?.Elements<Word.Footnote>() ?? [])
            {
                if (footnote.Id?.Value is not { } id || !footnotes._numbers.ContainsKey(id))
                    continue;

                var texts = footnote.Descendants<Word.Paragraph>()
                    .Select(paragraph => WithoutOwnMark(paragraph.GetFullText(footnotes._numbers)))
                    .Where(text => text.Length > 0)
                    .ToList();
                var designation = footnotes._designations[id];
                if (texts.Count > 0)
                    texts[0] = designation.Custom
                        ? WithoutTypedCustomMark(texts[0], designation.Symbol)
                        : WithoutTypedOwnNumber(texts[0], designation.Symbol);
                footnotes._texts[id] = texts;
            }

            return footnotes;
        }

        /// <summary>
        /// Odwołania do przypisów w kolejnych sekcjach dokumentu, z ustawieniami przypisów sekcji (null, gdy sekcja
        /// ich nie podaje). Sekcję zamyka akapit niosący <c>w:sectPr</c>, a ostatnią — <c>w:sectPr</c> treści.
        /// </summary>
        private static IEnumerable<(List<Word.FootnoteReference> References, OpenXmlElement? Settings)> Sections(Word.Document document)
        {
            var current = new List<Word.FootnoteReference>();
            var seen = new HashSet<Word.FootnoteReference>();
            foreach (var paragraph in document.Descendants<Word.Paragraph>())
            {
                // Akapit pola tekstowego leży wewnątrz innego akapitu — jego odwołania są już policzone.
                foreach (var reference in paragraph.Descendants<Word.FootnoteReference>())
                    if (seen.Add(reference))
                        current.Add(reference);

                if (paragraph.ParagraphProperties?.SectionProperties is { } ending)
                {
                    yield return (current, SettingsOf(ending));
                    current = [];
                }
            }

            yield return (current, SettingsOf(document.Body?.Elements<Word.SectionProperties>().LastOrDefault()));
        }

        private static OpenXmlElement? SettingsOf(OpenXmlElement? section) =>
            section?.Elements().FirstOrDefault(e => e.LocalName == Settings);

        /// <summary>Wartość ustawienia przypisów („numRestart", „numFmt", „numStart"); null, gdy go nie podano.</summary>
        private static string? Setting(OpenXmlElement? settings, string name) =>
            settings?.Elements().FirstOrDefault(e => e.LocalName == name)?
                .GetAttributes().FirstOrDefault(a => a.LocalName == "val").Value;

        /// <summary>Numer przypisu w zapisie sekcji; zapisy, których nie znamy, zostają liczbą.</summary>
        private static string Format(int number, string? format) => format switch
        {
            // Word po „z" powtarza literę: „aa", „bb", …
            "lowerLetter" => new string((char)('a' + (number - 1) % 26), (number - 1) / 26 + 1),
            "upperLetter" => new string((char)('A' + (number - 1) % 26), (number - 1) / 26 + 1),
            "lowerRoman" => RomanNumeralConverter.ToRoman(number).ToLowerInvariant(),
            "upperRoman" => RomanNumeralConverter.ToRoman(number),
            _ => number.ToString(CultureInfo.InvariantCulture),
        };

        /// <summary>Przypisy, do których odwołuje się akapit, w kolejności odwołań.</summary>
        internal IReadOnlyList<BlockFootnote> ReferencedBy(Word.Paragraph paragraph)
        {
            List<BlockFootnote>? referenced = null;
            foreach (var reference in paragraph.Descendants<Word.FootnoteReference>())
            {
                if (reference.Id?.Value is not { } id || !_numbers.TryGetValue(id, out var number))
                    continue;

                var designation = _designations[id];
                (referenced ??= []).Add(new BlockFootnote(designation.Symbol, _texts.GetValueOrDefault(id) ?? [])
                {
                    Marker = number.ToString(CultureInfo.InvariantCulture),
                    Run = designation.Run,
                    IsCustomMark = designation.Custom,
                });
            }

            return referenced ?? (IReadOnlyList<BlockFootnote>)[];
        }

        /// <summary>
        /// Odwołanie bez nawiasu („[7]") nie jest znacznikiem odnośnika i nikt go nie rozstrzygnie, więc zostaje
        /// w tekście tak, jak stoi w akcie — z oznaczeniem przypisu zamiast numeru znacznika dalszego ciągu
        /// numeracji. W pierwszym ciągu oba numery są te same.
        /// </summary>
        internal static string WithBareReferencesAsPrinted(string text, IReadOnlyList<BlockFootnote> referenced)
        {
            var symbols = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var footnote in referenced.Where(footnote => footnote.Run > 0))
                symbols.TryAdd(footnote.Marker, footnote.Symbol);

            return symbols.Count == 0
                ? text
                : Superscript.Replace(text, superscript => BareNumber.Replace(
                    superscript.Value, number => symbols.GetValueOrDefault(number.Value, number.Value)));
        }

        // Treść przypisu zaczyna własny znacznik: pusty znak odwołania i nawias w indeksie górnym — „[)]".
        // Indeks górny znacznika bywa rozciągnięty na dalszy tekst („[) Zmiany … 1911 i] 1914"); nawias
        // zamykający stoi wtedy w treści i też do niej nie należy.
        internal static string WithoutOwnMark(string text)
        {
            var trimmed = text.TrimStart();
            if (trimmed.StartsWith("[)", StringComparison.Ordinal))
            {
                var close = trimmed.IndexOf(']');
                trimmed = close < 0 ? trimmed[2..] : trimmed[2..close] + trimmed[(close + 1)..];
            }
            else if (trimmed.StartsWith(')'))
            {
                trimmed = trimmed[1..];
            }

            return trimmed.Trim();
        }

        // Treść przypisu bywa otwarta jego numerem wpisanym tekstem w indeksie górnym („[1)]") zamiast znaku
        // odwołania Worda — to też oznaczenie przypisu, nie jego treść. Tylko własne oznaczenie: inny numer
        // w indeksie górnym na początku treści byłby odwołaniem do innego przypisu.
        internal static string WithoutTypedOwnNumber(string text, string symbol)
        {
            var typed = "[" + symbol + ")]";
            return text.StartsWith(typed, StringComparison.Ordinal) ? text[typed.Length..].TrimStart() : text;
        }

        /// <summary>
        /// Oznaczenie przypisu z własnym znakiem: znak bez nawiasu zamykającego („3)" → „3", „*)" → „*", „za" → „za").
        /// </summary>
        private static string CustomSymbol(string mark) => mark.Trim().TrimEnd(')').Trim();

        /// <summary>
        /// Treść przypisu z własnym znakiem zaczyna ten znak wpisany tekstem — w indeksie górnym albo zwykłym
        /// („[3)]W brzmieniu …", „za)Zmiana …", „*)WSSE …", „1 Niniejszy …"). To oznaczenie, nie treść.
        /// </summary>
        internal static string WithoutTypedCustomMark(string text, string symbol)
        {
            foreach (var typed in new[] { "[" + symbol + ")]", "[" + symbol + "]", symbol + ")" })
            {
                if (text.StartsWith(typed, StringComparison.Ordinal))
                    return text[typed.Length..].TrimStart();
            }

            return text.StartsWith(symbol + " ", StringComparison.Ordinal) ? text[(symbol.Length + 1)..].TrimStart() : text;
        }
    }
}
