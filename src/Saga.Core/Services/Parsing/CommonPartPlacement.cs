using System.Text.RegularExpressions;
using Saga.Model;
using Saga.Model.EditorialUnits;
using Serilog;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Rozstrzyga część wspólną, po której wyliczenie biegnie dalej. Część wspólna dołączona jako zamknięcie
    /// wyliczenia okazuje się czymś innym dopiero wtedy, gdy przychodzi kolejna pozycja tego samego wyliczenia —
    /// dlatego decyzja zapada przy tej pozycji, tak samo w treści aktu i w treści nowelizacji.
    /// </summary>
    internal static class CommonPartPlacement
    {
        /// <summary>
        /// Wywoływane tuż przed dodaniem kolejnej pozycji do wyliczenia.
        /// </summary>
        /// <param name="host">Jednostka z wyliczeniem: ustęp dla punktów, punkt dla liter.</param>
        /// <param name="items">Dotychczasowe pozycje wyliczenia.</param>
        internal static void BeforeNextItem(BaseEntity host, IReadOnlyList<BaseEntity> items)
        {
            if (host is not IHasCommonParts owner || items.Count == 0)
                return;

            var closing = owner.CommonParts.LastOrDefault(IsClosing);
            if (closing is null)
                return;

            // Ostatnia pozycja ma własne wyliczenie bez zamknięcia: część wspólna stała tuż za nim, więc zamyka
            // je, a nie wyliczenie gospodarza. Styl akapitu wskazał o poziom za wysoko — układ wygrywa ze stylem.
            var last = items[^1];
            if (last is IHasCommonParts lower && HasList(last) && !lower.CommonParts.Any(IsClosing))
            {
                owner.CommonParts.Remove(closing);
                closing.Parent = last;
                closing.ParentEId = last.Id;
                lower.CommonParts.Add(closing);

                Log.Warning("Część wspólna przeniesiona do niższego wyliczenia ({Unit}): po niej stoi kolejna pozycja wyliczenia nadrzędnego.", last.Id);
                ValidationReporter.AddValidationMessage(last, ValidationLevel.Info,
                    "Część wspólna zamyka wyliczenie tej jednostki, choć styl wskazywał wyliczenie nadrzędne — po niej stoi jego kolejna pozycja.");
                return;
            }

            // Inaczej przerywa wyliczenie gospodarza: zostaje w miejscu, w którym stoi w akcie.
            closing.InterruptsAfter = items.Count;
            ValidationReporter.AddValidationMessage(host, ValidationLevel.Warning,
                "Część wspólna przerywa wyliczenie — po niej stoją kolejne pozycje (§ 57 ZTP).");
        }

        /// <summary>
        /// Czy wyliczenie punktów kończy się częścią wspólną: każdy punkt przed ostatnim kończy się wtedy przecinkiem,
        /// a nie średnikiem (§ 56 ust. 3 ZTP). Punkt z literami kończy się tam, gdzie jego ostatnia litera albo jej
        /// ostatnie tiret. Myślnik za ostatnią pozycją ostatniego punktu zamyka wtedy punkty, nie litery (ADR-0074).
        /// </summary>
        internal static bool PointsCloseWithCommonPart(IReadOnlyList<Point> points)
        {
            if (points.Count < 2)
                return false;

            for (var i = 0; i < points.Count - 1; i++)
            {
                var ending = EndingText(points[i]).TrimEnd();
                if (ending.Length == 0 || ending[^1] != ',')
                    return false;
            }

            return true;
        }

        /// <summary>Tekst, na którym kończy się punkt: ostatniej litery, jej ostatniego tiret albo samego punktu.</summary>
        private static string EndingText(Point point)
        {
            if (point.Letters.Count == 0)
                return point.ContentText;

            BaseEntity last = point.Letters[^1];
            for (var tirets = point.Letters[^1].Tirets; tirets.Count > 0; tirets = tirets[^1].Tirets)
                last = tirets[^1];
            return last.ContentText;
        }

        /// <summary>Skrót roku na końcu pozycji („… 2026 r.") — kropka skrótu, nie znak końca pozycji.</summary>
        private static readonly Regex YearAbbreviationAtEnd = new(@"\b\d{4}\s+r\.$", RegexOptions.Compiled);

        /// <summary>
        /// Czy pozycja wyliczenia nie ma znaku końcowego — wtedy myślnik tuż za nią jest częścią wspólną
        /// (§ 57 ust. 3 ZTP), a nie tiretem (wyliczenie tiretów otwiera dwukropek, § 56). Jedna reguła dla
        /// treści aktu i treści cytowanej bez stylów.
        /// </summary>
        internal static bool LeavesEnumerationOpen(string itemText)
        {
            var text = itemText.TrimEnd();
            if (text.Length == 0)
                return false;

            return text[^1] switch
            {
                ':' or ';' or ',' => false,
                '.' => YearAbbreviationAtEnd.IsMatch(text),
                _ => true,
            };
        }

        /// <summary>Część wspólna zamykająca wyliczenie — nie wprowadzenie i nie część przerywająca.</summary>
        internal static bool IsClosing(CommonPart part) =>
            part is { Type: CommonPartType.WrapUp, InterruptsAfter: null };

        private static bool HasList(BaseEntity unit) => unit switch
        {
            Point point => point.Letters.Count > 0,
            Letter letter => letter.Tirets.Count > 0,
            _ => false,
        };
    }
}
