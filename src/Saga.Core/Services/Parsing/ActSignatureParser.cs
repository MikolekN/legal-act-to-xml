using System.Text.RegularExpressions;
using Saga.Model;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Rozpoznaje formułę podpisu aktu: organ, dwukropek, inicjały i nazwisko
    /// („Prezydent Rzeczypospolitej Polskiej: A. Duda", „Marszałek Sejmu: Sz. Hołownia").
    /// </summary>
    internal static class ActSignatureParser
    {
        // Organ: nazwa od wielkiej litery, bez znaków interpunkcyjnych. Osoba: co najmniej jeden inicjał
        // („A.", „Sz.") i nazwisko, także dwuczłonowe. Formuła zajmuje cały akapit — przepis kończący się
        // dwukropkiem i nazwiskiem w środku zdania nie pasuje.
        private static readonly Regex Formula = new(
            @"^(?<authority>\p{Lu}[\p{L} ]{2,80}?)\s*:\s*(?<given>(?:\p{Lu}\p{Ll}?\.\s*)+)(?<surname>\p{Lu}\p{Ll}+(?:-\p{Lu}\p{Ll}+)?)\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

        internal static ActSignature? TryParse(string? text)
        {
            var match = Formula.Match(text?.Trim() ?? string.Empty);
            if (!match.Success)
                return null;

            return new ActSignature
            {
                Authority = match.Groups["authority"].Value.Trim(),
                GivenName = Whitespace.Replace(match.Groups["given"].Value.Trim(), " "),
                Surname = match.Groups["surname"].Value,
            };
        }
    }
}
