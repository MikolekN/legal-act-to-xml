using System.Text.RegularExpressions;
using Saga.Core.Helpers;
using Saga.Core.Services.Classify;
using Saga.Core.Services.Parsing;
using Saga.Model;
using Saga.Model.EditorialUnits;

namespace Saga.Core.Services.EntryIntoForce
{
    /// <summary>
    /// Wersje czasowe jednostek tekstu jednolitego (§ 106a ZTP, ADR-0060). Jednostkę, której zmiana nie weszła jeszcze
    /// w życie, tekst jednolity drukuje dwa razy: w brzmieniu obowiązującym, z odnośnikiem „W tym brzmieniu obowiązuje
    /// do wejścia w życie zmiany, o której mowa w odnośniku N", i w brzmieniu przyszłym, z odnośnikiem o zmianie.
    /// Znacznik odnośnika stoi zaraz za oznaczeniem jednostki, a kolejne jednostki tej samej zmiany go powtarzają.
    /// </summary>
    internal static class WordingVersions
    {
        /// <summary>Znacznik zaraz za oznaczeniem: „[7)] Treść …", a przy uchyleniu — „(uchylony)[7)]".</summary>
        private static readonly Regex OwnMarker = new(
            @"^\s*(?<repealed>\(uchylon[yae]\)\s*)?\[(?<no>\d+)\)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// § 106a ust. 2 ZTP: „W tym brzmieniu obowiązuje do wejścia w życie zmiany …", przed uchyleniem —
        /// „Obowiązuje do …". Zdanie bez podmiotu mówi o jednostce; z podmiotem („Zdanie drugie w tym brzmieniu
        /// obowiązuje …") — o jej fragmencie, którego wersji tu nie rozróżniamy.
        /// </summary>
        private static readonly Regex CurrentNote = new(
            @"(?:^|;)\s*(?:w\s+tym\s+brzmieniu\s+)?obowiązuj[eą]\s+do\s+(?:dnia\s+)?wejścia\s+w\s+życie\s+zmian",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>§ 106 pkt 2–5 ZTP: wzory odnośników o zmianie całej jednostki.</summary>
        private static readonly Regex ChangeNote = new(
            @"^\s*(?:W\s+brzmieniu\s+ustalonym\s+przez|Ze\s+zmian(?:ą\s+wprowadzoną|ami\s+wprowadzonymi)\s+przez|Dodan[yae]\s+przez|Przez)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Odnośnik jednostki samego aktu, która jeszcze nie obowiązuje: „Wejdzie w życie z dniem …".</summary>
        private static readonly Regex OwnEntryNote = new(
            @"^\s*Wejd(?:zie|ą)\s+w\s+życie\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Czas przyszły: zmiana, która weszła już w życie („która weszła w życie z dniem …"), wersji nie tworzy.
        /// </summary>
        private static readonly Regex NotYetInForce = new(
            @"\bwejd(?:zie|ą)\s+w\s+życie\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex OnDay = new(
            @"^\s*z\s+dniem\s+(?<date>\d{1,2}\s+\p{L}+\s+\d{4})\s*r\.", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex MarkerNumber = new(@"(?<no>\d+)\)", RegexOptions.Compiled);

        /// <summary>Odnośnik stojący przy jednostce i to, co mówi o jej wersji.</summary>
        private sealed class Marked
        {
            /// <summary>Oznaczenie odnośnika jak w akcie; numer ze znacznika, gdy odnośnika pod nim nie ma.</summary>
            internal required string Footnote { get; init; }
            internal required string Note { get; init; }
            internal required bool Repealed { get; init; }
            internal WordingVersion? Version { get; set; }
        }

        /// <summary>Nadaje wersje jednostkom tekstu jednolitego; w innych dokumentach nic nie robi.</summary>
        internal static void Assign(LegalDocument document)
        {
            if (document.Announcement is not { IsConsolidatedText: true } || document.Footnotes.Count == 0)
                return;

            AssignAmong(document.Articles.Cast<BaseEntity>().ToList(), ReadMarkers(document), inherited: null);
        }

        /// <summary>
        /// Odnośniki stojące zaraz za oznaczeniami jednostek. Znaczniki czyta się w kolejności dokumentu, bo dopiero
        /// ona mówi, który odnośnik stoi za numerem (<see cref="FootnoteResolver"/>).
        /// </summary>
        private static Dictionary<BaseEntity, Marked> ReadMarkers(LegalDocument document)
        {
            var resolver = new FootnoteResolver(document.Footnotes);
            var marked = new Dictionary<BaseEntity, Marked>();
            foreach (var article in document.Articles)
                Walk(article);
            return marked;

            void Walk(BaseEntity unit)
            {
                // Kolejność dokumentu: część wspólna zamykająca wyliczenie stoi po jego pozycjach.
                var children = EditorialUnitWalker.Children(unit, includeAmendmentContent: false)
                    .OrderBy(child => child is CommonPart { Type: CommonPartType.WrapUp })
                    .ToList();
                if (unit is not Article)
                {
                    Read(unit, unit.ContentText ?? string.Empty);
                    children.ForEach(Walk);
                    return;
                }

                // Treść artykułu powtarza treść jego pierwszego ustępu razem ze znacznikami, więc czyta się je raz
                // — przy ustępie. Znacznik stojący zaraz za oznaczeniem artykułu („Art. 5.[7)] 1. …") mówi
                // o artykule, choć otwiera też tekst ustępu.
                children.ForEach(Walk);
                var text = unit.ContentText ?? string.Empty;
                var tail = ParagraphClassifier.ArticleTailCapture.Match(text.Trim()) is { Success: true } match ? match.Groups[1].Value : text;
                if (!OwnMarker.IsMatch(tail))
                    return;

                if (children.FirstOrDefault() is { } opening && marked.Remove(opening, out var own))
                    marked[unit] = own;
            }

            void Read(BaseEntity unit, string text)
            {
                var leading = OwnMarker.Match(text);
                foreach (Match marker in FootnoteMarkers.Reference.Matches(text))
                {
                    foreach (Match number in MarkerNumber.Matches(marker.Value))
                    {
                        var footnote = resolver.First(number.Groups["no"].Value);
                        var isLeading = leading.Success && marker.Index + number.Groups["no"].Index == leading.Groups["no"].Index;
                        if (!isLeading)
                            continue;

                        footnote ??= resolver.Repeated(number.Groups["no"].Value);
                        marked[unit] = Describe(new Marked
                        {
                            Footnote = footnote?.Symbol ?? number.Groups["no"].Value,
                            Note = footnote is null ? string.Empty : string.Join(" ", footnote.Paragraphs),
                            Repealed = leading.Groups["repealed"].Success,
                        });
                    }
                }
            }
        }

        private static void AssignAmong(List<BaseEntity> siblings, Dictionary<BaseEntity, Marked> marked, WordingVersion? inherited)
        {
            // Jednostka bez własnego odnośnika dzieli wersję jednostki nadrzędnej.
            foreach (var unit in siblings)
                unit.Version = marked.GetValueOrDefault(unit)?.Version ?? inherited;

            // Druga jednostka o tym samym oznaczeniu, z własnym odnośnikiem, jest brzmieniem przyszłym — także gdy
            // jej odnośnik nie mówi o wejściu w życie („W brzmieniu ustalonym przez art. 2 ustawy, o której mowa
            // w odnośniku 8") albo opisuje samą różnicę („Zdanie drugie uchylone przez …"). Wersja obowiązująca
            // kończy się zdarzeniem, które tę przyszłą otwiera.
            for (var i = 0; i < siblings.Count; i++)
            {
                if (!marked.TryGetValue(siblings[i], out var first) || first.Version is not { Kind: WordingVersionKind.Current } current)
                    continue;

                var next = siblings.Skip(i + 1).FirstOrDefault(other => marked.ContainsKey(other) && SameDesignation(other, siblings[i]));
                if (next is null)
                    continue;

                var second = marked[next];
                if (second.Version is null)
                    next.Version = second.Version = Future(second);

                if (second.Version is { Kind: WordingVersionKind.Future } change)
                {
                    current.ChangeFootnote = change.ChangeFootnote;
                    current.ChangeDate = change.ChangeDate;
                    current.ChangeTerm = change.ChangeTerm;
                    current.ChangeRepeals = change.ChangeRepeals;
                }
            }

            foreach (var unit in siblings)
            {
                var children = EditorialUnitWalker.Children(unit, includeAmendmentContent: false).ToList();
                if (children.Count > 0)
                    AssignAmong(children, marked, unit.Version);
            }
        }

        private static bool SameDesignation(BaseEntity left, BaseEntity right) =>
            left.GetType() == right.GetType()
            && left.Number is { Designation.Length: > 0 } number
            && number.Designation == right.Number?.Designation;

        /// <summary>Wersja, którą odnośnik sam zapisuje; druga kopia jednostki dostaje ją z układu.</summary>
        private static Marked Describe(Marked own)
        {
            if (CurrentNote.IsMatch(own.Note))
                own.Version = new WordingVersion { Kind = WordingVersionKind.Current };
            else if ((ChangeNote.IsMatch(own.Note) || OwnEntryNote.IsMatch(own.Note)) && NotYetInForce.IsMatch(own.Note))
                own.Version = Future(own);

            return own;
        }

        private static WordingVersion Future(Marked own)
        {
            var version = new WordingVersion
            {
                Kind = WordingVersionKind.Future,
                ChangeFootnote = own.Footnote,
                ChangeRepeals = own.Repealed,
            };

            // Termin stoi w zdaniu za „wejdzie w życie": dzień albo proza, którą przenosimy dosłownie.
            var sentence = ParsingFactories.SplitIntoSentences(own.Note)
                .Select(part => part.Text)
                .FirstOrDefault(NotYetInForce.IsMatch);
            if (sentence is null)
                return version;

            var term = sentence[(NotYetInForce.Match(sentence).Index + NotYetInForce.Match(sentence).Length)..];
            if (OnDay.Match(term) is { Success: true } day && PolishDates.TryParseLongDate(day.Groups["date"].Value, out var date))
            {
                version.ChangeDate = date;
            }
            else
            {
                var prose = term.Split(';')[0].Trim().TrimEnd('.').Trim();
                version.ChangeTerm = prose.Length > 0 ? prose : null;
            }

            return version;
        }
    }
}
