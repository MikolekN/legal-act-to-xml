using System.Text.RegularExpressions;

namespace Saga.Core.Helpers
{
    /// <summary>
    /// Rozpoznaje skróty, po których kropka nie kończy zdania.
    ///
    /// Na liście umieszczono tylko skróty, które wymagają dopełnienia i z tego powodu nie mogą
    /// kończyć („do spraw" - „ds.", „imienia" - „im."). Skróty mogące stać na końcu zdania
    /// („itp.", „itd.", „i in.", „z późn. zm.") są świadomie pominięte.
    /// </summary>
    internal static class AbbreviationRecognizer
    {
        /// <summary>Skrót bezpośrednio przed kropką.</summary>
        internal static readonly Regex Pattern = new(
            @"(?<!\p{L})(?:m\.st|ds|im|ul|al|pl|art|ust|pkt|lit|poz|rozdz|oddz|cz|zd|str|prof|dr|ks|gen|mgr|dz|np)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        internal static bool EndsAbbreviation(string text, int dotIndex)
            => dotIndex > 0 && Pattern.IsMatch(text.AsSpan(0, dotIndex));
    }
}
