using System.Text.RegularExpressions;
using Saga.Core.Services.Classify;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Treść odnośnika za jego pierwszym akapitem jako jednostki odnośnika: „Niniejsza ustawa służy stosowaniu:"
    /// i pod nim „1) rozporządzenia …;" to punkty odnośnika, nie kolejne zdania jego akapitu (ADR-0078).
    /// Oznaczenia czyta się tymi samymi wzorcami co jednostki aktu (§ 56 ZTP); o granicach rozstrzyga treść, nie styl
    /// akapitu przypisu.
    /// </summary>
    internal static class FootnoteContent
    {
        internal enum Kind { Point, Letter, Tiret, CommonPart, Paragraph }

        /// <param name="Kind">Rodzaj jednostki.</param>
        /// <param name="Number">Oznaczenie bez nawiasu („1", „2a", „b"); null dla tiretu, części wspólnej i akapitu.</param>
        /// <param name="Text">Treść bez oznaczenia; część wspólna — z myślnikiem, jak w akcie.</param>
        /// <param name="Units">Jednostki niższego poziomu: litery punktu, tirety litery i części wspólne ich wyliczeń.</param>
        internal sealed record Unit(Kind Kind, string? Number, string Text, List<Unit> Units);

        // Numer jednostki odnośnika: liczba z literą albo litera, z indeksem w nawiasie kwadratowym (§ 56 ZTP).
        private static readonly Regex SimpleNumber = new(
            @"^(?:[1-9]\d*[a-z]*|[a-z]+)(?:\[[1-9]\d*[a-z]*\])?$", RegexOptions.Compiled);

        /// <summary>
        /// Jednostki treści za pierwszym akapitem odnośnika. Null, gdy odnośnik ma jeden akapit albo jego wyliczenia
        /// nie da się zapisać jednostkami odnośnika — litera bez punktu, tiret poza literą, akapit kontynuujący zdanie
        /// bez wyliczenia przed sobą — wtedy akapity zostają zdaniami jednego akapitu.
        /// </summary>
        internal static List<Unit>? Read(IReadOnlyList<string> paragraphs)
        {
            if (paragraphs.Count < 2)
                return null;

            var top = new List<Unit>();
            Unit? point = null, letter = null;

            // Ostatnia jednostka wyliczenia — po niej myślnik otwiera tirety albo zamyka wyliczenie częścią wspólną.
            Unit? last = null;

            foreach (var paragraph in paragraphs.Skip(1).Select(text => text.Trim()).Where(text => text.Length > 0))
            {
                if (ParagraphClassifier.PointNumberCapture.Match(paragraph) is { Success: true } pointMatch)
                {
                    point = Designated(Kind.Point, pointMatch, paragraph);
                    if (point is null)
                        return null;
                    top.Add(point);
                    letter = null;
                    last = point;
                }
                else if (ParagraphClassifier.IsLetterByText(paragraph)
                    && ParagraphClassifier.LetterNumberCapture.Match(paragraph) is { Success: true } letterMatch)
                {
                    letter = Designated(Kind.Letter, letterMatch, paragraph);
                    if (point is null || letter is null)
                        return null;
                    point.Units.Add(letter);
                    last = letter;
                }
                else if (ParagraphClassifier.TiretPattern.Match(paragraph) is { Success: true } dash
                    && last is not null && !CommonPartPlacement.ClosesBeforeDash(last.Text)
                    && (last.Kind == Kind.Tiret || last.Text.TrimEnd().EndsWith(':')))
                {
                    // Myślnik pod jednostką zakończoną dwukropkiem otwiera tirety, za tiretem — kolejny tiret; za pozycją
                    // bez znaku końcowego zamyka wyliczenie, jak w treści aktu (ADR-0074). Tiret stoi tylko w literze.
                    if (letter is null)
                        return null;
                    var tiret = new Unit(Kind.Tiret, null, paragraph[dash.Length..], []);
                    letter.Units.Add(tiret);
                    last = tiret;
                }
                else if (ParagraphClassifier.IsTiretByText(paragraph) || char.IsLower(paragraph[0]))
                {
                    // Myślnik w innym miejscu i mała litera ciągną zdanie wprowadzenia: część wspólna wyliczenia,
                    // które zamykają (§ 57 ZTP).
                    if (last is null)
                        return null;
                    var commonPart = new Unit(Kind.CommonPart, null, paragraph, []);
                    switch (last.Kind)
                    {
                        case Kind.Point:
                            top.Add(commonPart);
                            point = letter = last = null;
                            break;
                        case Kind.Letter:
                            point!.Units.Add(commonPart);
                            letter = null;
                            last = point;
                            break;
                        default:
                            letter!.Units.Add(commonPart);
                            last = letter;
                            break;
                    }
                }
                else
                {
                    // Akapit od wielkiej litery bez oznaczenia to kolejne zdanie odnośnika, nie część wspólna — także
                    // w stylu części wspólnej („Dane dotyczące ogłoszenia aktów prawa Unii Europejskiej …").
                    top.Add(new Unit(Kind.Paragraph, null, paragraph, []));
                    point = letter = last = null;
                }
            }

            return top.Count > 0 ? top : null;
        }

        private static Unit? Designated(Kind kind, Match match, string paragraph)
        {
            var number = match.Groups[1].Value.Trim();
            return SimpleNumber.IsMatch(number) ? new Unit(kind, number, paragraph[match.Length..], []) : null;
        }
    }
}
