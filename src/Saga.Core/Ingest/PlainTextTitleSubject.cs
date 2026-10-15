using System.Text.RegularExpressions;

namespace Saga.Core.Ingest
{
    /// <summary>
    /// Przedmiot aktu (§ 18–19 ZTP) rozbity w TXT na kilka wierszy. W DOCX przedmiot jest jednym akapitem, a ręczne
    /// przejście do nowego wiersza (<c>w:br</c>) w jego środku czytnik DOCX oddaje odstępem; eksport tekstu pisze w tym
    /// miejscu koniec wiersza, więc dalsza część przedmiotu stawała się osobnym akapitem, a tytuł aktu kończył się w pół
    /// zdania („… rejestracji pojazdów," / „blankietu profesjonalnego dowodu …").
    ///
    /// Za datą aktu („z dnia 28 października 2025 r."), która stoi pod nagłówkiem rodzaju i organem, wiersz przedmiotu
    /// przyłącza kolejne wiersze zaczynające się małą literą. Treść za przedmiotem zaczyna się wielką literą, cyfrą
    /// albo znakiem („Na podstawie …", preambuła, „1. …", „§ 1.", „Art. 1.", „Rozdział 1"), więc na niej doklejanie staje.
    /// </summary>
    internal static class PlainTextTitleSubject
    {
        private static readonly Regex KindHeader = new(
            @"^(?:ustawa|rozporządzenie|obwieszczenie|uchwała|zarządzenie)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ActDate = new(
            @"^z\s+dnia\s+\d{1,2}\s+\p{L}+\s+\d{4}\s*r\.(?:\s*\[[^\]]*\])?\s*$", RegexOptions.Compiled);

        private static readonly Regex NamedSubject = new(@"^(?:Kodeks|Prawo|Ordynacja|Przepisy)\b", RegexOptions.Compiled);

        /// <summary>Najwięcej wierszy między nagłówkiem rodzaju a datą (organ wydający, czasem w dwóch wierszach).</summary>
        private const int MaxIssuerLines = 3;

        internal static IReadOnlyList<DocumentBlock> Join(IReadOnlyList<DocumentBlock> blocks)
        {
            var result = new List<DocumentBlock>(blocks.Count);
            for (var i = 0; i < blocks.Count; i++)
            {
                result.Add(blocks[i]);
                if (!ActDate.IsMatch(blocks[i].Text.Trim()) || !FollowsKindHeader(blocks, i) || i + 1 >= blocks.Count)
                    continue;

                if (!IsSubject(blocks[i + 1].Text))
                    continue;

                var subject = blocks[++i];
                var text = subject.Text.TrimEnd();
                while (i + 1 < blocks.Count && StartsLowercase(blocks[i + 1].Text))
                    text += " " + blocks[++i].Text.Trim();

                result.Add(subject with { Text = text });
            }

            return result;
        }

        private static bool FollowsKindHeader(IReadOnlyList<DocumentBlock> blocks, int dateIndex)
        {
            for (var j = dateIndex - 1; j >= Math.Max(0, dateIndex - 1 - MaxIssuerLines); j--)
            {
                if (KindHeader.IsMatch(blocks[j].Text.Trim()))
                    return true;
            }

            return false;
        }

        /// <summary>Przedmiot: zaczyna się małą literą („o …", „w sprawie …", „zmieniające …") albo nazwą kodeksu i prawa.</summary>
        private static bool IsSubject(string text) => StartsLowercase(text) || NamedSubject.IsMatch(text.TrimStart());

        private static bool StartsLowercase(string text) =>
            text.TrimStart() is { Length: > 0 } trimmed && char.IsLower(trimmed[0]);
    }
}
