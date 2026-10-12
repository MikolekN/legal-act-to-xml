#nullable enable

namespace Saga.Model
{
    /// <summary>
    /// Poziom struktury, którym nowelizacja obudowuje dotychczasową treść jednostki („w art. 24 dotychczasową treść
    /// oznacza się jako ust. 1 i dodaje się ust. 2"). W eksporcie instrukcja <c>now:dodaj-ustep</c> albo
    /// <c>now:dodaj-paragraf</c>.
    /// </summary>
    public enum ContentWrap
    {
        /// <summary>Nowelizacja niczego nie obudowuje.</summary>
        None,

        /// <summary>Dotychczasowa treść staje się ustępem 1.</summary>
        Paragraph,

        /// <summary>Dotychczasowa treść artykułu kodeksu staje się paragrafem 1.</summary>
        CodeParagraph,
    }
}
