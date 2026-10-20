using System.Text.RegularExpressions;
using Saga.Model;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Cel komendy w załączniku, którego <c>eId</c> nie zaadresuje: wiersz tabeli („lp. 7"), kolumna, część, tabela,
    /// tytuł załącznika, numeracja wielopoziomowa (ADR-0100). Według autorów schematu taki cel wskazuje <c>GUIDcelu</c>
    /// przy <c>eId</c> najgłębszej adresowalnej jednostki; w akcie istniejącym GUID jest nieznany, więc instrukcja jest
    /// ręczna (<c>*-recznie</c>) z adresem tej jednostki. Zmiana w tekście, której przepis nie zawęża do miejsca w
    /// załączniku („w załączniku nr 2 wyrazy „X” zastępuje się …"), zostaje automatyczna — obejmuje cały załącznik.
    /// </summary>
    internal static class ManualTargets
    {
        /// <summary>Formuła wprowadzająca akt zmieniany: „W rozporządzeniu Ministra … (Dz. U. poz. 200)".</summary>
        private static readonly Regex ActFormula = new(
            @"^W\s+(?:ustawie|rozporządzeniu|obwieszczeniu|uchwale|zarządzeniu)\b[^()]*\([^)]*\)\s*(?:wprowadza\s+się\s+następujące\s+zmiany:?\s*)?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Zmiana w tekście całego załącznika: za miejscem „w załączniku (nr N) (do …)" — albo bez niego, gdy podał je
        /// przepis nadrzędny — stoi od razu przedmiot zmiany w tekście (wyrazy, liczba, znak), a nie miejsce w załączniku.
        /// </summary>
        private static readonly Regex WholeAnnexTextChange = new(
            @"^(?:użyt\p{L}*\s+)?(?:w\s+załączniku(?:\s+nr\s+\S+)?(?:\s+do\s+\p{L}+)?[\s:]*)?(?:użyt\p{L}*\s+)?(?:(?:po|przed)\s+)?(?:skreśla\s+się\s+)?"
            + @"(?:wyraz|liczb|cyfr|kwot|znak|kropk|przecin|średnik|dwukrop|nawias)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>Załącznik jest przedmiotem uchylenia („uchyla się załącznik nr 4") — cel ma adres.</summary>
        private static readonly Regex RepealedAnnex = new(
            @"\b(?:uchyla|skreśla)\s+się\s+załącznik", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Oznacza cele bez adresu jako ręczne: adresem zostaje najgłębsza adresowalna jednostka — załącznik albo jego
        /// jednostka, a instrukcja dostaje funkcję <c>*-recznie</c>.
        /// </summary>
        /// <param name="ownText">Tekst przepisu, który jest komendą — z niego widać, czy zmiana w tekście obejmuje cały załącznik.</param>
        /// <summary>Tekst przepisu bez formuły wprowadzającej akt zmieniany („W ustawie z dnia … (Dz. U. poz. 200)").</summary>
        internal static string WithoutActFormula(string text) => ActFormula.Replace(text.Trim(), string.Empty, 1);

        internal static void Mark(Amendment amendment, string? ownText)
        {
            var text = WithoutActFormula(ownText ?? string.Empty);
            var wholeAnnexText = ChangesTextOnly(amendment) && WholeAnnexTextChange.IsMatch(text);
            var annexRepealed = amendment.OperationType == AmendmentOperationType.Repeal && RepealedAnnex.IsMatch(text);

            foreach (var target in amendment.Targets.Concat(amendment.TextChanges.SelectMany(change => change.Targets)))
            {
                var structure = target.Structure;
                var manual = structure.ActTitle
                    || structure is { Annex.Length: > 0, InAnnexContent: true }
                    || (IsAnnexItself(structure) && !wholeAnnexText && !annexRepealed);
                if (!manual)
                    continue;

                target.IsManual = true;
                target.RangeEnd = null;
            }
        }

        private static bool IsAnnexItself(StructuralReference reference) =>
            reference is { Annex.Length: > 0, Article: null, Paragraph: null, Point: null, Letter: null, Tiret: null, InAnnexContent: false };

        /// <summary>
        /// Zmiana w samym tekście: wyrazy (§ 87 ZTP) albo treść cytowana będąca tekstem — bez jednostek, tabel
        /// i odnośnika, i nie uchylenie.
        /// </summary>
        internal static bool ChangesTextOnly(Amendment amendment)
        {
            var content = amendment.Content;
            var noUnits = content is null
                || (content.SystematizingUnits.Count == 0 && content.Articles.Count == 0 && content.Paragraphs.Count == 0
                    && content.Points.Count == 0 && content.Letters.Count == 0 && content.Tirets.Count == 0
                    && content.CommonParts.Count == 0 && content.Tables.Count == 0 && content.Footnote is null);
            return noUnits && (amendment.TextChanges.Count > 0
                || (amendment.OperationType != AmendmentOperationType.Repeal && !string.IsNullOrEmpty(content?.PlainText)));
        }
    }
}
