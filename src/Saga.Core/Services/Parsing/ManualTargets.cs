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

        /// <summary>
        /// Zmiana w tekście całego odnośnika: za miejscem „w odnośniku nr N (do tytułu ustawy)" — albo bez niego, gdy podał je
        /// przepis nadrzędny — stoi od razu przedmiot zmiany w tekście, a nie akapit czy punkt odnośnika.
        /// </summary>
        private static readonly Regex WholeFootnoteTextChange = new(
            @"^(?:użyt\p{L}*\s+)?(?:w\s+odnośniku\s+nr\s+\S+(?:\s+do\s+tytułu\s+\p{L}+)?[\s:]*)?(?:użyt\p{L}*\s+)?(?:(?:po|przed)\s+)?(?:skreśla\s+się\s+)?"
            + @"(?:wyraz|liczb|cyfr|kwot|znak|kropk|przecin|średnik|dwukrop|nawias)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>Załącznik jest przedmiotem uchylenia („uchyla się załącznik nr 4") — cel ma adres.</summary>
        private static readonly Regex RepealedAnnex = new(
            @"\b(?:uchyla|skreśla)\s+się\s+załącznik", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Wyrazy w różnych formach: „w różnej liczbie i różnym przypadku, wyrazy „X” zastępuje się użytymi w odpowiedniej
        /// liczbie i odpowiednim przypadku wyrazami „Y”" — formy stoją tylko w akcie zmienianym (ADR-0103).
        /// </summary>
        private static readonly Regex InflectedWords = new(
            @"\bw\s+(?:różn|odpowiedni)\p{L}*\s+(?:liczb|przypad)\p{L}*", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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

            // Odmiana wyrazów w akcie zmienianym: instrukcja z formą podstawową z komendy trafiłaby co najwyżej w mianownik —
            // każdy cel dostaje instrukcję ręczną z nowymi wyrazami (zasada ogólna 6, ADR-0103).
            var inflected = amendment.TextChanges.Count > 0 && InflectedWords.IsMatch(UnitReferences.UnitAddressReader.WithoutQuotedText(text));

            // Odnośnik jako miejsce (ADR-0106): ręcznie, chyba że zmienia się tekst całego odnośnika („w odnośniku nr 2
            // wyrazy „X” zastępuje się …") albo odnośnik jest przedmiotem komendy (cytowany odnośnik, ADR-0059).
            var footnoteInside = amendment.Content?.Footnote is null
                && !(ChangesTextOnly(amendment) && WholeFootnoteTextChange.IsMatch(text));

            foreach (var target in amendment.Targets.Concat(amendment.TextChanges.SelectMany(change => change.Targets)))
            {
                var structure = target.Structure;
                // Część wspólna jednostki załącznika — miejsce w treści załącznika (ADR-0080, ADR-0100): adresem zostaje
                // jednostka, instrukcja jest ręczna. Eksport nie zna części wspólnej z eId pod jednostką załącznika (ADR-0127).
                var annexCommonPart = structure is { Annex.Length: > 0, CommonPart: true };
                var unaddressable = annexCommonPart
                    || (structure.Footnote is not null && footnoteInside)
                    || structure.ActTitle
                    || structure is { Annex.Length: > 0, InAnnexContent: true }
                    || (IsAnnexItself(structure) && !wholeAnnexText && !annexRepealed);
                if (!inflected && !unaddressable)
                    continue;

                target.IsManual = true;
                if (annexCommonPart)
                    target.Structure = structure.HostUnit();

                // Adres zastępczy (najgłębsza adresowalna jednostka) nie ma końca przedziału. Cel odmiany wyrazów ma własny
                // adres, więc przedział zostaje („w art. 75a w ust. 1–3, w różnym przypadku, wyrazy …" — M.P. 2026 poz. 1014).
                if (unaddressable)
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
