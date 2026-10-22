using System.Text.RegularExpressions;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Budowa tytułu — jednostki systematyzacyjnej (§ 60 i § 62 ust. 2 ZTP) i załącznika: zaczyna się wielką
    /// literą i nie kończy jak zdanie ani jak wprowadzenie do wyliczenia. Tytuł nie ma kropki, a dwukropek,
    /// średnik i przecinek na końcu znaczą treść przepisu. Rozstrzyga o akapicie bez stylu tytułu, który stoi
    /// w miejscu tytułu; długość nie ma znaczenia — tytuł rozdziału aktu wykonawczego bywa dłuższy niż niejeden
    /// przepis. Kropka skrótu roku zdania nie kończy („Emerytura funkcjonariusza przyjętego do służby po raz pierwszy
    /// po dniu 31 grudnia 2012 r." — Dz. U. 2026 poz. 505).
    /// </summary>
    internal static class TitleForm
    {
        private static readonly Regex YearAbbreviationEnd = new(@"\b\d{4}\s*r\.$", RegexOptions.Compiled);

        internal static bool Matches(string text)
        {
            var title = text.Trim();
            if (title.Length == 0 || !char.IsLetter(title[0]) || !char.IsUpper(title[0]))
                return false;

            return title[^1] is not ('.' or ':' or ';' or ',') || YearAbbreviationEnd.IsMatch(title);
        }
    }
}
