namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Budowa tytułu — jednostki systematyzacyjnej (§ 60 i § 62 ust. 2 ZTP) i załącznika: zaczyna się wielką
    /// literą i nie kończy jak zdanie ani jak wprowadzenie do wyliczenia. Tytuł nie ma kropki, a dwukropek,
    /// średnik i przecinek na końcu znaczą treść przepisu. Rozstrzyga o akapicie bez stylu tytułu, który stoi
    /// w miejscu tytułu; długość nie ma znaczenia — tytuł rozdziału aktu wykonawczego bywa dłuższy niż niejeden
    /// przepis.
    /// </summary>
    internal static class TitleForm
    {
        internal static bool Matches(string text)
        {
            var title = text.Trim();
            if (title.Length == 0 || !char.IsLetter(title[0]) || !char.IsUpper(title[0]))
                return false;

            return title[^1] is not ('.' or ':' or ';' or ',');
        }
    }
}
