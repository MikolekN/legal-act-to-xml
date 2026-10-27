using System.Text.RegularExpressions;
using Saga.Model;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Rozpoznaje formułę podpisu aktu: organ, dwukropek, inicjały i nazwisko
    /// („Prezydent Rzeczypospolitej Polskiej: A. Duda", „Marszałek Sejmu: Sz. Hołownia"), także podpisaną
    /// w zastępstwie albo z upoważnienia („Minister Finansów: wz. J. Neneman", „… : z up. P. Bejda").
    /// </summary>
    internal static class ActSignatureParser
    {
        // Organ: nazwa od wielkiej litery; z interpunkcji tylko przecinek („Minister Rodziny, Pracy i Polityki
        // Społecznej"). Osoba: co najmniej jeden inicjał („A.", „Sz.") i nazwisko, także dwuczłonowe; przed nią
        // może stać „wz." albo „z up.". Formuła zajmuje cały akapit — przepis kończący się dwukropkiem
        // i nazwiskiem w środku zdania nie pasuje.
        private static readonly Regex Formula = new(
            @"^(?<authority>\p{Lu}[\p{L} ,]{2,80}?)\s*:\s*(?:(?<capacity>wz\.|z\s+up\.)\s*)?(?<given>(?:\p{Lu}\p{Ll}?\.\s*)+)(?<surname>\p{Lu}\p{Ll}+(?:-\p{Lu}\p{Ll}+)?)\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

        /// <param name="italicText">
        /// Fragmenty akapitu złożone kursywą (<see cref="Ingest.BlockLayoutInfo.ItalicText"/>): osoba, która w całości
        /// stoi w jednym z nich, jest kursywą (<see cref="ActSignature.IsPersonItalic"/>).
        /// </param>
        internal static ActSignature? TryParse(string? text, IReadOnlyList<string>? italicText = null)
        {
            var match = Formula.Match(text?.Trim() ?? string.Empty);
            if (!match.Success)
                return null;

            var signature = new ActSignature
            {
                Authority = match.Groups["authority"].Value.Trim(),
                Capacity = match.Groups["capacity"].Success ? Whitespace.Replace(match.Groups["capacity"].Value, " ") : null,
                GivenName = Whitespace.Replace(match.Groups["given"].Value.Trim(), " "),
                Surname = match.Groups["surname"].Value,
            };
            var person = Whitespace.Replace(signature.GivenName + signature.Surname, string.Empty);
            signature.IsPersonItalic = italicText?.Any(fragment =>
                Whitespace.Replace(fragment, string.Empty).Contains(person, StringComparison.OrdinalIgnoreCase)) == true;
            return signature;
        }
    }
}
