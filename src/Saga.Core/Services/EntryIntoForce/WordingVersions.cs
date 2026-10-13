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

        /// <summary>
        /// Podmiot odnośnika o zdaniu: „Zdanie drugie w tym brzmieniu obowiązuje do …", „Zdanie trzecie i czwarte
        /// dodane przez …", a także „Wprowadzenie do wyliczenia …" — ono też jest zdaniem jednostki. Reszta
        /// odnośnika ma te same wzory co odnośnik o całej jednostce.
        /// </summary>
        private static readonly Regex SentenceSubject = new(
            @"^\s*(?:Zdani[ea]\s+\p{L}+(?:\s+(?:i|oraz)\s+\p{L}+)*|Wprowadzenie\s+do\s+wyliczenia)\s+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Podmiot odnośnika o części wspólnej wyliczenia: „Część wspólna w tym brzmieniu obowiązuje do …".</summary>
        private static readonly Regex CommonPartSubject = new(
            @"^\s*Część\s+wspólna\s+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Znacznik spotkany w tekście: gdzie stoi jego numer, gdzie kończy się nawias i który to odnośnik.</summary>
        private sealed record Met(int At, int End, Footnote? Footnote, string Number);

        /// <summary>
        /// Akt zmieniający nazwany odesłaniem (§ 106 ZTP): „przez art. 5 pkt 2 ustawy, o której mowa w odnośniku 3".
        /// </summary>
        private static readonly Regex ActNamedElsewhere = new(
            @"\b(?:ustawy|rozporządzenia)\s*,\s+o\s+któr(?:ej|ym)\s+mowa\s+w\s+odnośniku\s+(?<no>\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Wejście w życie aktu, nie pojedynczego przepisu: „ustawy z dnia … (Dz. U. poz. 818), która wejdzie
        /// w życie z dniem …". Zdanie po średniku („…; wejdzie w życie …") mówi o przepisie, przy którym stoi.
        /// </summary>
        private static readonly Regex ActEntersLater = new(
            @",\s+któr[aey]\s+wejd(?:zie|ą)\s+w\s+życie\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ActEntered = new(
            @",\s+któr[aey]\s+wesz(?:ła|ło|ły)\s+w\s+życie\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Akt zmieniający nazwany w pełni — datą i publikatorem: „ustawy z dnia 14 kwietnia 2023 r. o zmianie
        /// ustawy … (Dz. U. poz. 818)".
        /// </summary>
        private static readonly Regex ActNamedInFull = new(
            @"\b(?:ustawy|rozporządzenia)\b[^()]{0,400}?\bz\s+dnia\s+\d{1,2}\s+\p{L}+\s+\d{4}\s*r\.[^()]{0,400}?\((?:Dz\.\s*U\.|M\.\s?P\.)[^()]*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Przepis aktu zmieniającego: to, co stoi między „przez" a nazwą aktu („art. 2 pkt 1 lit. c").</summary>
        private static readonly Regex ChangingProvision = new(
            @"\bprzez\s+(?<provision>[^;()]+?)\s+(?:ustawy|rozporządzenia)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

        /// <summary>
        /// Termin wejścia w życie: dzień albo — gdy odnośnik dnia nie podaje — proza, może pusta; i odnośnik,
        /// w którym go zapisano.
        /// </summary>
        private sealed record Term(DateTime? Day, string? Prose, string Footnote);

        /// <summary>Odnośnik stojący przy jednostce i to, co mówi o jej wersji.</summary>
        private sealed class Marked
        {
            /// <summary>Oznaczenie odnośnika jak w akcie; numer ze znacznika, gdy odnośnika pod nim nie ma.</summary>
            internal required string Footnote { get; init; }
            internal required string Note { get; init; }

            /// <summary>
            /// Treść odnośnika, w którym nazwano akt zmieniający („ustawy, o której mowa w odnośniku 3"); null, gdy
            /// odnośnik nazywa akt sam albo wskazanego odnośnika nie ma.
            /// </summary>
            internal string? ActNote { get; init; }

            /// <summary>Oznaczenie odnośnika, w którym nazwano akt zmieniający.</summary>
            internal string? ActFootnote { get; init; }

            /// <summary>Akt zmieniający w zapisie adresu publikacyjnego; null, gdy nie nazwano go publikatorem.</summary>
            internal string? Act { get; init; }
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

            // Odnośnik powołuje inny oznaczeniem drukowanym, a to powtarza się między ciągami numeracji (ADR-0061).
            var printed = new Dictionary<(int Run, string Symbol), Footnote>();
            foreach (var footnote in document.Footnotes)
                printed.TryAdd((footnote.Run, footnote.Symbol), footnote);
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
                var met = MarkersIn(text);
                var leading = OwnMarker.Match(text);
                if (leading.Success && met.FirstOrDefault(marker => marker.At == leading.Groups["no"].Index) is { } own)
                    marked[unit] = Describe(Mark(own.Footnote, own.Number, Note(own.Footnote), leading.Groups["repealed"].Success));

                // Dalsza treść jednostki stoi za jej tekstem, a przed jednostkami podrzędnymi — w tej kolejności
                // padają jej znaczniki. Pierwszy akapit dalszej treści bywa drugim brzmieniem tego, co stoi
                // przed nim: zdania, wprowadzenia do wyliczenia albo części wspólnej (ADR-0065).
                var directlyAfter = true;
                foreach (var block in unit.Continuation.ToList())
                {
                    if (block is { Text: { Length: > 0 } more, Table: null, Quotation: null })
                    {
                        var metMore = MarkersIn(more);
                        if (directlyAfter && block.Formula is null)
                        {
                            if (unit is CommonPart { Type: CommonPartType.WrapUp } part)
                                TakeSecondCommonPart(part, text, met, block, more, metMore);
                            else if (unit is IHasTextSegments { TextSegments.Count: > 0 } holder)
                                text = TakeSecondWording(unit, holder.TextSegments, text, met, block, more, metMore);
                        }
                    }

                    directlyAfter = false;
                }

                if (unit is IHasTextSegments { TextSegments.Count: > 0 } sentences)
                    ReadSentences(sentences.TextSegments, text, met);
            }

            List<Met> MarkersIn(string text)
            {
                var met = new List<Met>();
                foreach (Match marker in FootnoteMarkers.Reference.Matches(text))
                {
                    foreach (Match number in MarkerNumber.Matches(marker.Value))
                    {
                        var symbol = number.Groups["no"].Value;
                        met.Add(new Met(
                            marker.Index + number.Groups["no"].Index,
                            marker.Index + marker.Length,
                            resolver.First(symbol) ?? resolver.Repeated(symbol),
                            symbol));
                    }
                }

                return met;
            }

            // Akapit bez oznaczenia, stojący zaraz za tekstem jednostki, jest drugim brzmieniem jej ostatniego
            // zdania, gdy oba kończą się odnośnikiem o zdaniu, a pierwszy mówi „obowiązuje do …". Jego zdania
            // dołączają wtedy do zdań jednostki; wersje nada im odczyt zdań. Zwraca tekst, w którym te zdania stoją.
            string TakeSecondWording(
                BaseEntity unit, List<TextSegment> sentences, string text, List<Met> met, ContentBlock block, string more, List<Met> metMore)
            {
                var last = sentences[^1];
                var ownNote = Note(met.LastOrDefault(marker => marker.End == text.TrimEnd().Length)?.Footnote);
                if (!SentenceSubject.IsMatch(ownNote) || !CurrentNote.IsMatch(SentenceSubject.Replace(ownNote, string.Empty, 1)))
                    return text;

                // Wprowadzenie do wyliczenia jest jednym zdaniem — i takie musi być jego drugie brzmienie.
                var added = ParsingFactories.SplitIntoSentences(more);
                if (added.Count == 0 || (last.Role is not null && added.Count != 1))
                    return text;

                var end = more.IndexOf(added[0].Text, StringComparison.Ordinal) + added[0].Text.Length;
                if (!SentenceSubject.IsMatch(Note(metMore.LastOrDefault(marker => marker.End == end)?.Footnote)))
                    return text;

                for (var k = 0; k < added.Count; k++)
                    added[k].Order = last.Order + 1 + k;
                added[^1].Role = last.Role;
                sentences.AddRange(added);

                var shift = text.Length + 1;
                met.AddRange(metMore.Select(marker => marker with { At = marker.At + shift, End = marker.End + shift }));
                unit.Continuation.Remove(block);
                return text + " " + more;
            }

            // Część wspólna jest jednostką z adresem, więc jej drugie brzmienie jest drugą częścią wspólną
            // tego samego wyliczenia — jak jednostka wydrukowana dwa razy (ADR-0060).
            void TakeSecondCommonPart(CommonPart part, string text, List<Met> met, ContentBlock block, string more, List<Met> metMore)
            {
                var own = met.LastOrDefault(marker => marker.End == text.TrimEnd().Length);
                var second = metMore.LastOrDefault(marker => marker.End == more.TrimEnd().Length);
                if (own is null || second is null || part.Parent is not IHasCommonParts host)
                    return;

                var (ownNote, secondNote) = (Note(own.Footnote), Note(second.Footnote));
                if (!CommonPartSubject.IsMatch(ownNote) || !CommonPartSubject.IsMatch(secondNote))
                    return;

                var current = Describe(Mark(own.Footnote, own.Number, CommonPartSubject.Replace(ownNote, string.Empty, 1), repealed: false));
                if (current.Version is not { Kind: WordingVersionKind.Current } closing)
                    return;

                var change = Describe(Mark(second.Footnote, second.Number, CommonPartSubject.Replace(secondNote, string.Empty, 1), repealed: false));
                change.Version ??= Future(change);
                if (change.Version is not { Kind: WordingVersionKind.Future } opening)
                    return;

                Close(closing, opening);
                var copy = new CommonPart(part.Type, part.ParentEId, more.Trim(), part.SourceSegmentOrder)
                {
                    Parent = part.Parent,
                    InterruptsAfter = part.InterruptsAfter,
                    ListItemType = part.ListItemType,
                };
                host.CommonParts.Insert(host.CommonParts.IndexOf(part) + 1, copy);
                part.Continuation.Remove(block);
                marked[part] = current;
                marked[copy] = change;
            }

            // Odnośnik o zdaniu stoi za zdaniem („… doręczenia.[26)]"), a zdanie w dwóch brzmieniach jest
            // wydrukowane dwa razy, jedno za drugim (ADR-0064).
            void ReadSentences(List<TextSegment> sentences, string text, List<Met> met)
            {
                var from = 0;
                var repeated = 0;
                Marked? previous = null;
                foreach (var sentence in sentences)
                {
                    var own = OfSentence(sentence);
                    if (own is not null && previous?.Version is { Kind: WordingVersionKind.Current } current)
                    {
                        own.Version ??= Future(own);
                        if (own.Version is { Kind: WordingVersionKind.Future } change)
                        {
                            Close(current, change);

                            // Drugie brzmienie tego samego zdania niesie jego numer; dalsze zdania liczą się dalej.
                            repeated++;
                        }
                    }

                    sentence.Order -= repeated;
                    sentence.Version = own?.Version;
                    previous = own;
                }

                Marked? OfSentence(TextSegment sentence)
                {
                    var at = text.IndexOf(sentence.Text, from, StringComparison.Ordinal);
                    if (at < 0)
                        return null;

                    from = at + sentence.Text.Length;
                    var closing = met.LastOrDefault(marker => marker.End == from);
                    var note = Note(closing?.Footnote);
                    return closing is not null && SentenceSubject.IsMatch(note)
                        ? Describe(Mark(closing.Footnote, closing.Number, SentenceSubject.Replace(note, string.Empty, 1), repealed: false))
                        : null;
                }
            }

            string Note(Footnote? footnote) => footnote is null ? string.Empty : string.Join(" ", footnote.Paragraphs);

            Marked Mark(Footnote? footnote, string number, string note, bool repealed)
            {
                var elsewhere = ActNamedElsewhere.Match(note);
                var act = footnote is not null
                    && elsewhere.Success
                    && printed.TryGetValue((footnote.Run, elsewhere.Groups["no"].Value), out var named)
                    && named != footnote
                        ? named
                        : null;
                var actNote = act is null ? null : Note(act);
                return new Marked
                {
                    Footnote = footnote?.Symbol ?? number,
                    Note = note,
                    ActNote = actNote,
                    ActFootnote = act?.Symbol,

                    // Akt nazwany odesłaniem czyta się tam, dokąd odesłanie prowadzi — nigdy z innych
                    // publikatorów tego samego odnośnika.
                    Act = elsewhere.Success ? ActNamedIn(actNote) : ActNamedIn(note),
                    Repealed = repealed,
                };
            }
        }

        /// <summary>Brzmienie obowiązujące kończy się zdarzeniem, które otwiera brzmienie przyszłe.</summary>
        private static void Close(WordingVersion current, WordingVersion change)
        {
            current.ChangeFootnote = change.ChangeFootnote;
            current.ChangeDate = change.ChangeDate;
            current.ChangeTerm = change.ChangeTerm;
            current.TermFootnote = change.TermFootnote;
            current.ChangeAct = change.ChangeAct;
            current.ChangeProvision = change.ChangeProvision;
            current.ChangeRepeals = change.ChangeRepeals;
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
                    Close(current, change);
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
            else if ((ChangeNote.IsMatch(own.Note) || OwnEntryNote.IsMatch(own.Note)) && TermOf(own) is not null)
                own.Version = Future(own);

            return own;
        }

        private static WordingVersion Future(Marked own)
        {
            var term = TermOf(own);
            return new WordingVersion
            {
                Kind = WordingVersionKind.Future,
                ChangeFootnote = own.Footnote,
                ChangeRepeals = own.Repealed,
                ChangeDate = term?.Day,
                ChangeTerm = term?.Day is null && term?.Prose is { Length: > 0 } prose ? prose : null,
                TermFootnote = term?.Footnote ?? string.Empty,
                ChangeAct = own.Act,
                ChangeProvision = ChangingProvision.Match(own.Note) is { Success: true } named
                    ? Whitespace.Replace(named.Groups["provision"].Value, " ").Trim()
                    : null,
            };
        }

        /// <summary>Adres publikacyjny aktu nazwanego w odnośniku datą i publikatorem; null, gdy takiego nie ma.</summary>
        private static string? ActNamedIn(string? note) =>
            note is not null && ActNamedInFull.Match(note) is { Success: true } named
                ? JournalReferenceService.ReadFirstAct(named.Value)?.GetELIStrings().FirstOrDefault()
                : null;

        /// <summary>
        /// Termin, w którym zmiana wejdzie w życie; null, gdy odnośnik o przyszłym wejściu w życie nie mówi.
        /// Odnośnik podaje go sam („…; wejdzie w życie z dniem …") albo zostawia aktowi zmieniającemu, nazwanemu
        /// w innym odnośniku razem z dniem, w którym cały ten akt wejdzie w życie.
        /// </summary>
        private static Term? TermOf(Marked own) =>
            TermAfter(NotYetInForce, own.Note, own.Footnote)
            ?? (own is { ActNote: { } act, ActFootnote: { } footnote } && !ActEntered.IsMatch(act) && ActEntersLater.Matches(act).Count == 1
                ? TermAfter(ActEntersLater, act, footnote)
                : null);

        /// <summary>Termin stojący w zdaniu za „wejdzie w życie": dzień albo proza przenoszona dosłownie.</summary>
        private static Term? TermAfter(Regex entersLater, string note, string footnote)
        {
            var sentence = ParsingFactories.SplitIntoSentences(note)
                .Select(part => part.Text)
                .FirstOrDefault(entersLater.IsMatch);
            if (sentence is null)
                return null;

            var match = entersLater.Match(sentence);
            var term = sentence[(match.Index + match.Length)..];
            return OnDay.Match(term) is { Success: true } day && PolishDates.TryParseLongDate(day.Groups["date"].Value, out var date)
                ? new Term(date, null, footnote)
                : new Term(null, term.Split(';')[0].Trim().TrimEnd('.').Trim(), footnote);
        }
    }
}
