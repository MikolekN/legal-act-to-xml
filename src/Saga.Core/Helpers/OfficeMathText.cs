using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;

namespace Saga.Core.Helpers
{
    /// <summary>
    /// Równanie Worda (<c>m:oMath</c>) jako tekst w zapisie liniowym: indeks dolny „E_i", górny „x^2",
    /// ułamek „a/b", nawiasy i znaki działań tak, jak stoją w równaniu („I_oze=(I_e∙U_oze)+I_b").
    /// Argument złożony z więcej niż jednego wyrazu stoi w nawiasie („x^(n+1)", „(a+b)/c").
    /// Zapis liniowy nie zastępuje wzoru w MathML — pilnuje, żeby treść równania nie znikała z tekstu.
    ///
    /// Nawias kwadratowy równania wychodzi znakami „［" i „］": zwykły „[x]" jest w tekście bloku kanałem
    /// indeksu górnego i znacznika odnośnika, więc „[(CM/12)]" stawałoby się odwołaniem do odnośnika 12.
    /// </summary>
    internal static class OfficeMathText
    {
        private const string MathNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/math";

        /// <summary>Argument, który nie wymaga nawiasu: jeden wyraz z liter i cyfr.</summary>
        private static readonly Regex SingleToken = new(@"^[\p{L}\p{N}]*$", RegexOptions.Compiled);

        internal static bool IsMath(OpenXmlElement element) => Is(element, "oMath");

        /// <summary>Treść równania w zapisie liniowym.</summary>
        internal static string Linear(OpenXmlElement math) =>
            OutsideTheSuperscriptChannel(string.Concat(math.ChildElements.Select(Part)));

        private static string OutsideTheSuperscriptChannel(string text) =>
            text.Replace('[', '\uFF3B').Replace(']', '\uFF3D');

        private static string Part(OpenXmlElement element)
        {
            if (element.NamespaceUri != MathNamespace)
                return string.Empty;

            switch (element.LocalName)
            {
                case "r":
                    return string.Concat(element.ChildElements.Where(child => Is(child, "t")).Select(child => child.InnerText));

                case "sSub":
                    return Child(element, "e") + "_" + Argument(element, "sub");

                case "sSup":
                    return Child(element, "e") + "^" + Argument(element, "sup");

                case "sSubSup":
                    return Child(element, "e") + "_" + Argument(element, "sub") + "^" + Argument(element, "sup");

                case "sPre":
                    return "_" + Argument(element, "sub") + "^" + Argument(element, "sup") + Child(element, "e");

                case "f":
                    return Argument(element, "num") + "/" + Argument(element, "den");

                case "d":
                    return Property(element, "dPr", "begChr", "(")
                        + string.Join(Property(element, "dPr", "sepChr", "|"), element.ChildElements.Where(child => Is(child, "e")).Select(Linear))
                        + Property(element, "dPr", "endChr", ")");

                case "nary":
                    return Property(element, "naryPr", "chr", "∫")
                        + (Flag(element, "naryPr", "subHide") ? string.Empty : Marked("_", Argument(element, "sub")))
                        + (Flag(element, "naryPr", "supHide") ? string.Empty : Marked("^", Argument(element, "sup")))
                        + Child(element, "e");

                case "rad":
                    var degree = Flag(element, "radPr", "degHide") ? string.Empty : Child(element, "deg");
                    return degree.Length == 0
                        ? "√" + Argument(element, "e")
                        : "√(" + degree + "&" + Child(element, "e") + ")";

                case "func":
                    return Child(element, "fName") + " " + Child(element, "e");

                case "limLow":
                    return Child(element, "e") + Marked("_", Argument(element, "lim"));

                case "limUpp":
                    return Child(element, "e") + Marked("^", Argument(element, "lim"));

                case "m":
                    return "(" + string.Join("; ", element.ChildElements.Where(child => Is(child, "mr"))
                        .Select(row => string.Join(", ", row.ChildElements.Where(cell => Is(cell, "e")).Select(Linear)))) + ")";

                case "eqArr":
                    return string.Join("; ", element.ChildElements.Where(child => Is(child, "e")).Select(Linear));

                // Kontenery i ozdobniki bez własnego znaku w zapisie liniowym (akcent, kreska, ramka, grupa).
                case "oMath" or "e" or "num" or "den" or "sub" or "sup" or "deg" or "lim" or "fName"
                    or "acc" or "bar" or "box" or "borderBox" or "groupChr" or "phant":
                    return Linear(element);

                // Właściwości (…Pr, ctrlPr) nie niosą treści.
                default:
                    return string.Empty;
            }
        }

        private static bool Is(OpenXmlElement element, string localName) =>
            element.NamespaceUri == MathNamespace && element.LocalName == localName;

        private static string Child(OpenXmlElement element, string localName) =>
            element.ChildElements.FirstOrDefault(child => Is(child, localName)) is { } child ? Linear(child) : string.Empty;

        private static string Argument(OpenXmlElement element, string localName)
        {
            var text = Child(element, localName);
            return SingleToken.IsMatch(text) || IsParenthesised(text) ? text : "(" + text + ")";
        }

        /// <summary>Czy cały tekst obejmuje jedna para nawiasów okrągłych („(t)", ale nie „(a)+(b)").</summary>
        private static bool IsParenthesised(string text)
        {
            if (text.Length < 2 || text[0] != '(' || text[^1] != ')')
                return false;

            var depth = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '(')
                    depth++;
                else if (text[i] == ')' && --depth == 0 && i < text.Length - 1)
                    return false;
            }

            return depth == 0;
        }

        // Pusty argument (granica bez treści) nie zostawia samego znaku „_" albo „^".
        private static string Marked(string mark, string argument) => argument.Length == 0 ? string.Empty : mark + argument;

        /// <summary>Znak z właściwości (<c>m:begChr m:val="["</c>); brak elementu to znak domyślny, pusty <c>val</c> — brak znaku.</summary>
        private static string Property(OpenXmlElement element, string properties, string name, string fallback)
        {
            var property = element.ChildElements.FirstOrDefault(child => Is(child, properties))?
                .ChildElements.FirstOrDefault(child => Is(child, name));
            return property is null ? fallback : Value(property) ?? fallback;
        }

        /// <summary>Przełącznik (<c>m:subHide</c>): obecny bez wartości znaczy „tak".</summary>
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
