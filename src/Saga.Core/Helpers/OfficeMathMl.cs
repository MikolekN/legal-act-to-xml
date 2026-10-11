using System.Globalization;
using System.Xml.Linq;
using DocumentFormat.OpenXml;

namespace Saga.Core.Helpers
{
    /// <summary>
    /// Równanie Worda (<c>m:oMath</c>) jako MathML prezentacyjny — postać, w której schemat przyjmuje wzór
    /// (<c>wzor</c> → <c>mml:math</c>). Odwzorowuje budowę równania: indeksy (<c>msub</c>, <c>msup</c>),
    /// ułamek (<c>mfrac</c>), nawiasy, operator z granicami, pierwiastek, macierz; tekst przebiegu dzieli na
    /// identyfikatory (<c>mi</c>), liczby (<c>mn</c>) i znaki działań (<c>mo</c>).
    /// Zapis liniowy tego samego równania daje <see cref="OfficeMathText"/>.
    /// </summary>
    internal static class OfficeMathMl
    {
        internal static readonly XNamespace Mml = "http://www.w3.org/1998/Math/MathML";

        private const string MathNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/math";

        // Znak wyrównania w układzie równań („&") nie jest treścią wzoru.
        private const char AlignmentMark = '&';

        /// <summary>Element <c>math</c> równania; <paramref name="display"/> — wzór w osobnym akapicie.</summary>
        internal static XElement ToMathMl(OpenXmlElement math, bool display = false)
        {
            var root = new XElement(Mml + "math");
            if (display)
                root.Add(new XAttribute("display", "block"));
            root.Add(Children(math));
            return root;
        }

        private static List<XElement> Children(OpenXmlElement element) => element.ChildElements.SelectMany(Part).ToList();

        private static IEnumerable<XElement> Part(OpenXmlElement element)
        {
            if (element.NamespaceUri != MathNamespace)
                return [];

            switch (element.LocalName)
            {
                case "r":
                    return Tokens(element);

                case "sSub":
                    return [El("msub", Argument(element, "e"), Argument(element, "sub"))];

                case "sSup":
                    return [El("msup", Argument(element, "e"), Argument(element, "sup"))];

                case "sSubSup":
                    return [El("msubsup", Argument(element, "e"), Argument(element, "sub"), Argument(element, "sup"))];

                case "sPre":
                    return [El("mmultiscripts", Argument(element, "e"), El("mprescripts"), Argument(element, "sub"), Argument(element, "sup"))];

                case "f":
                    return [Fraction(element)];

                case "d":
                    return [Delimited(element)];

                case "nary":
                    return [Nary(element)];

                case "rad":
                    return [Flag(element, "radPr", "degHide") || Child(element, "deg").Count == 0
                        ? El("msqrt", Child(element, "e"))
                        : El("mroot", Argument(element, "e"), Argument(element, "deg"))];

                case "func":
                    return [El("mrow", Child(element, "fName"), El("mo", "⁡"), Child(element, "e"))];

                case "limLow":
                    return [El("munder", Argument(element, "e"), Argument(element, "lim"))];

                case "limUpp":
                    return [El("mover", Argument(element, "e"), Argument(element, "lim"))];

                case "acc":
                    return [El("mover", new XAttribute("accent", "true"),
                        Argument(element, "e"), El("mo", Property(element, "accPr", "chr", "̂")))];

                case "bar":
                    return [El(Property(element, "barPr", "pos", "bot") == "top" ? "mover" : "munder",
                        Argument(element, "e"), El("mo", "¯"))];

                case "groupChr":
                    return [El(Property(element, "groupChrPr", "pos", "bot") == "top" ? "mover" : "munder",
                        Argument(element, "e"), El("mo", Property(element, "groupChrPr", "chr", "⏟")))];

                case "m":
                    return [El("mtable", element.ChildElements.Where(row => Is(row, "mr")).Select(row =>
                        El("mtr", row.ChildElements.Where(cell => Is(cell, "e")).Select(cell => El("mtd", Children(cell))))))];

                case "eqArr":
                    return [El("mtable", element.ChildElements.Where(row => Is(row, "e")).Select(row =>
                        El("mtr", El("mtd", Children(row)))))];

                case "borderBox":
                    return [El("menclose", new XAttribute("notation", "box"), Child(element, "e"))];

                case "phant":
                    return [El("mphantom", Child(element, "e"))];

                // Kontenery bez własnej budowy w MathML.
                case "oMath" or "e" or "num" or "den" or "sub" or "sup" or "deg" or "lim" or "fName" or "box":
                    return Children(element);

                // Właściwości (…Pr, ctrlPr) nie niosą treści.
                default:
                    return [];
            }
        }

        /// <summary>Ułamek: zwykły, bez kreski (<c>noBar</c>), ukośny (<c>skw</c>) albo zapisany w wierszu (<c>lin</c>).</summary>
        private static XElement Fraction(OpenXmlElement element)
        {
            var type = Property(element, "fPr", "type", "bar");
            if (type == "lin")
                return El("mrow", Argument(element, "num"), El("mo", "/"), Argument(element, "den"));

            var fraction = El("mfrac", Argument(element, "num"), Argument(element, "den"));
            if (type == "noBar")
                fraction.Add(new XAttribute("linethickness", "0"));
            else if (type == "skw")
                fraction.Add(new XAttribute("bevelled", "true"));
            return fraction;
        }

        /// <summary>Nawias ze znakami z właściwości równania; pusty znak (układ z klamrą z jednej strony) nie wychodzi.</summary>
        private static XElement Delimited(OpenXmlElement element)
        {
            var row = El("mrow");
            AddFence(row, Property(element, "dPr", "begChr", "("));

            var separator = Property(element, "dPr", "sepChr", "|");
            var first = true;
            foreach (var part in element.ChildElements.Where(child => Is(child, "e")))
            {
                if (!first && separator.Length > 0)
                    row.Add(El("mo", new XAttribute("separator", "true"), separator));
                row.Add(Children(part));
                first = false;
            }

            AddFence(row, Property(element, "dPr", "endChr", ")"));
            return row;
        }

        private static void AddFence(XElement row, string character)
        {
            if (character.Length > 0)
                row.Add(El("mo", new XAttribute("fence", "true"), character));
        }

        /// <summary>
        /// Operator z granicami („∑" od i=u do j): granice pod i nad znakiem (<c>undOvr</c>) albo jako indeksy
        /// (<c>subSup</c>); ukryta granica nie wychodzi. Za operatorem stoi jego argument.
        /// </summary>
        private static XElement Nary(OpenXmlElement element)
        {
            var sign = El("mo", Property(element, "naryPr", "chr", "∫"));
            var lower = Flag(element, "naryPr", "subHide") ? null : Argument(element, "sub");
            var upper = Flag(element, "naryPr", "supHide") ? null : Argument(element, "sup");
            var stacked = Property(element, "naryPr", "limLoc", "undOvr") != "subSup";

            var op = (lower, upper) switch
            {
                (not null, not null) => El(stacked ? "munderover" : "msubsup", sign, lower, upper),
                (not null, null) => El(stacked ? "munder" : "msub", sign, lower),
                (null, not null) => El(stacked ? "mover" : "msup", sign, upper),
                _ => sign,
            };

            return El("mrow", op, Child(element, "e"));
        }

        // ============================================================
        // Tekst przebiegu
        // ============================================================

        /// <summary>
        /// Tekst przebiegu jako żetony: ciąg liter to identyfikator, ciąg cyfr (także z odstępem albo przecinkiem
        /// między cyframi: „1 724 188", „0,20") to liczba, każdy inny znak to znak działania. Przebieg tekstu
        /// zwykłego (<c>m:nor</c>) wychodzi w całości jako <c>mtext</c>.
        /// </summary>
        private static IEnumerable<XElement> Tokens(OpenXmlElement run)
        {
            var text = string.Concat(run.ChildElements.Where(child => Is(child, "t")).Select(child => child.InnerText));
            if (Flag(run, "rPr", "nor"))
            {
                if (text.Length > 0)
                    yield return El("mtext", text);
                yield break;
            }

            var style = Property(run, "rPr", "sty", "i");
            var i = 0;
            while (i < text.Length)
            {
                var character = text[i];
                if (char.IsWhiteSpace(character) || character == AlignmentMark)
                {
                    i++;
                }
                else if (char.IsDigit(character))
                {
                    var end = i + 1;
                    while (end < text.Length && (char.IsDigit(text[end]) || IsDigitSeparator(text, end)))
                        end++;
                    yield return El("mn", text[i..end]);
                    i = end;
                }
                else if (char.IsLetter(character))
                {
                    var end = i + 1;
                    while (end < text.Length && (char.IsLetter(text[end]) || IsCombining(text[end])))
                        end++;
                    yield return Identifier(text[i..end], style);
                    i = end;
                }
                else
                {
                    var end = i + 1;
                    while (end < text.Length && IsCombining(text[end]))
                        end++;
                    yield return El("mo", text[i..end]);
                    i = end;
                }
            }
        }

        // Odstęp, przecinek albo kropka między dwiema cyframi należy do liczby.
        private static bool IsDigitSeparator(string text, int index) =>
            text[index] is ' ' or ' ' or ',' or '.'
            && index + 1 < text.Length && char.IsDigit(text[index + 1]);

        private static bool IsCombining(char character) =>
            char.GetUnicodeCategory(character) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark;

        /// <summary>
        /// Identyfikator z krojem przebiegu. MathML składa jedną literę kursywą, a kilka — pismem prostym,
        /// więc krój wychodzi tylko wtedy, gdy różni się od domyślnego.
        /// </summary>
        private static XElement Identifier(string name, string style)
        {
            var variant = style switch
            {
                "p" => "normal",
                "b" => "bold",
                "bi" => "bold-italic",
                _ => "italic",
            };
            var letters = new StringInfo(name).LengthInTextElements;
            var defaultVariant = letters == 1 ? "italic" : "normal";

            var identifier = El("mi", name);
            if (variant != defaultVariant)
                identifier.Add(new XAttribute("mathvariant", variant));
            return identifier;
        }

        // ============================================================
        // Pomocnicze
        // ============================================================

        private static XElement El(string name, params object?[] content) => new(Mml + name, content);

        private static bool Is(OpenXmlElement element, string localName) =>
            element.NamespaceUri == MathNamespace && element.LocalName == localName;

        private static List<XElement> Child(OpenXmlElement element, string localName) =>
            element.ChildElements.FirstOrDefault(child => Is(child, localName)) is { } child ? Children(child) : [];

        /// <summary>Argument konstrukcji o stałej liczbie miejsc: jeden element albo <c>mrow</c> obejmujący kilka.</summary>
        private static XElement Argument(OpenXmlElement element, string localName)
        {
            var parts = Child(element, localName);
            return parts.Count == 1 ? parts[0] : El("mrow", parts);
        }

        private static string Property(OpenXmlElement element, string properties, string name, string fallback)
        {
            var property = element.ChildElements.FirstOrDefault(child => Is(child, properties))?
                .ChildElements.FirstOrDefault(child => Is(child, name));
            return property is null ? fallback : Value(property) ?? fallback;
        }

        private static bool Flag(OpenXmlElement element, string properties, string name)
        {
            var property = element.ChildElements.FirstOrDefault(child => Is(child, properties))?
                .ChildElements.FirstOrDefault(child => Is(child, name));
            return property is not null && Value(property) is not ("0" or "off" or "false");
        }

        private static string? Value(OpenXmlElement property) =>
            property.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == "val") is { Value: { } value } ? value : null;
    }
}
