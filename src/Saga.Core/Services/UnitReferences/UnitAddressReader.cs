using System.Text.RegularExpressions;
using Saga.Model;

namespace Saga.Core.Services.UnitReferences
{
    internal sealed record UnitAddress(StructuralReference Start, StructuralReference? End = null);

    /// <summary>
    /// Czyta adres jednostek wskazywanych z tekstu komendy („w art. 5 § 2 pkt 3 wyrazy „…” zastępuje się …").
    ///
    /// Komenda wskazuje jeden cel, przedział albo wyliczenie: „art. 1" → jeden cel,
    /// „art. 1–3" → jeden cel z przedziałem, „art. 1, 2 i 3" → trzy cele.
    /// </summary>
    internal static class UnitAddressReader
    {
        /// <summary>Domknięty fragment w cudzysłowie — najgłębszy, bez cudzysłowów w środku.</summary>
        private static readonly Regex InnermostQuoted = new(
            $"{QuotationMarks.Opening}[^{QuotationMarks.Any}]*[{QuotationMarks.ClosingOrStraight}]|\"[^{QuotationMarks.Any}]*\"",
            RegexOptions.Compiled);

        /// <summary>Cytat niedomknięty do końca tekstu („otrzymuje brzmienie: „Art. 5 …").</summary>
        private static readonly Regex UnclosedQuote = new(
            $"[{QuotationMarks.OpeningOrStraight}].*$", RegexOptions.Compiled | RegexOptions.Singleline);

        /// <summary>
        /// Czasownik komendy. Jednostka za nim to przedmiot komendy („po pkt 6 dodaje się przecinek i pkt 7"),
        /// nie kolejny cel — chyba że adres dopiero się przy nim zaczyna („w art. 5 uchyla się ust. 2 i 3").
        /// </summary>
        private static readonly Regex CommandVerb = new(
            @"\b(?:dodaje|uchyla|skreśla|zastępuje)\s+się\b|\botrzymuj[eą]\s+brzmienie",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// „Dodaje się" — jednostki stojące za nim są dodawane, nie wskazywane. Miejsce dodania stoi przed
        /// czasownikiem: jednostka, po której się dodaje („po ust. 5 dodaje się ust. 5a"), albo jednostka, w której
        /// się dodaje („w art. 13 dodaje się ust. 6") (ADR-0055).
        /// </summary>
        private static readonly Regex AdditionVerb = new(
            @"\bdodaje\s+się\b", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>Cele wskazane w tekście komendy.</summary>
        internal static IReadOnlyList<UnitAddress> Read(
            string? text, StructuralReference? context = null, bool sectionSignIsBasicUnit = false)
        {
            if (string.IsNullOrWhiteSpace(text))
                return [];

            var cursor = context is null
                ? new UnitPathCursor { SectionSignIsBasicUnit = sectionSignIsBasicUnit }
                : UnitPathCursor.StartingAt(context, sectionSignIsBasicUnit);
            var targets = new List<EnumeratedTarget>();
            EnumeratedTarget? pending = null;
            var inRun = false;

            var join = EnumerationJoin.None;
            var commandVerb = false;
            var addition = false;

            var tokens = UnitReferenceTokenizer.Tokenize(WithoutQuotedText(text), systematizing: true, annexes: true);
            for (var i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                switch (token.Kind)
                {
                    case UnitTokenKind.UnitKeyword when token.Level == UnitLevel.Annex && !IsAnnexLocation(tokens, i):
                        inRun = false;
                        join = EnumerationJoin.None;
                        continue;

                    case UnitTokenKind.UnitKeyword when token.Level == UnitLevel.Annex:
                        cursor.Announce(token);
                        inRun = true;
                        if (token.Text.EndsWith("nr", StringComparison.OrdinalIgnoreCase)
                            && i + 1 < tokens.Count && tokens[i + 1].Kind == UnitTokenKind.Designation)
                        {
                            continue;
                        }

                        // Załącznik bez numeru: komenda zakłada, że jest jedyny, więc i pierwszy — adres zal_1 (ADR-0099);
                        // dalszy wyraz („do rozporządzenia") oznaczeniem nie jest (ADR-0080).
                        inRun = false;
                        token = new UnitToken(UnitTokenKind.Designation, Annex.OnlyAnnexNumber);
                        break;

                    case UnitTokenKind.UnitKeyword:
                        cursor.Announce(token);
                        inRun = true;

                        // Słowo kluczowe bez oznaczenia w załączniku („w pkt 1.4.1", „w części Rachunek zysków") — miejsce
                        // w treści załącznika poza jego jednostkami (ADR-0080).
                        if (i + 1 >= tokens.Count || tokens[i + 1].Kind is not (UnitTokenKind.Designation or UnitTokenKind.Range))
                            pending = InAnnexContent(cursor, pending);
                        continue;

                    case UnitTokenKind.Designation when IsLocationByNoun(tokens, i) && cursor.Path.Annex is not null:
                        // „w tabeli nr 1", „po rysunku 9.5.53", „w lp. 5" — miejsce nazwane rzeczownikiem, nie jednostką.
                        pending = InAnnexContent(cursor, pending);
                        inRun = false;
                        continue;

                    case UnitTokenKind.Separator when IsEnumerationSeparator(token.Text):
                        join = token.Text == "," && join != EnumerationJoin.Conjunction
                            ? EnumerationJoin.Comma
                            : EnumerationJoin.Conjunction;
                        continue;

                    case UnitTokenKind.Designation or UnitTokenKind.Range when inRun && addition:
                        // Jednostka dodawana kończy adres: wskazane jest tylko to, co stało przed „dodaje się".
                        return pending is null ? [] : Completed(targets, pending);

                    case UnitTokenKind.Designation or UnitTokenKind.Range when inRun:
                        break;

                    case UnitTokenKind.Prose when UnitReferenceTokenizer.IsPreposition(token.Text):
                        inRun = false;
                        continue;

                    default:
                        inRun = false;
                        join = EnumerationJoin.None;
                        commandVerb |= token.Kind == UnitTokenKind.Prose && CommandVerb.IsMatch(token.Text);
                        addition |= token.Kind == UnitTokenKind.Prose && AdditionVerb.IsMatch(token.Text);
                        continue;
                }

                var begun = cursor.BeginStep();
                if (begun is not { } stepContext ||
                    Step(cursor, stepContext, token) is not { } step)
                {
                    // Jednostka aktu, której w załączniku nie ma („w § 10", „w części II") — miejsce w treści załącznika.
                    if (begun is { ExplicitKeyword: true })
                        pending = InAnnexContent(cursor, pending);
                    inRun = false;
                    join = EnumerationJoin.None;
                    continue;
                }

                if (pending is not null && step.Previous == PendingDisposition.Flush)
                {
                    if (join == EnumerationJoin.None || commandVerb)
                        return Completed(targets, pending);

                    targets.Add(pending);
                    pending = new EnumeratedTarget(step.Target, join == EnumerationJoin.Comma);
                }
                else
                {
                    pending = new EnumeratedTarget(step.Target, pending?.JoinedByCommaOnly ?? false);
                }

                join = EnumerationJoin.None;
                commandVerb = false;
            }

            return pending is null ? [] : Completed(targets, pending);
        }

        /// <summary>Przyimki miejsca: „w tabeli", „po rysunku", „pod tabelą", „przed objaśnieniami", „na końcu".</summary>
        private static readonly HashSet<string> LocationPrepositions = new(StringComparer.OrdinalIgnoreCase)
        {
            "w", "we", "po", "pod", "przed", "na", "nad",
        };

        /// <summary>
        /// Przyimek miejsca przed rzeczownikiem, który jednostką nie jest — tokenizer zostawił go oznaczeniem, bo za nim
        /// nie stoi słowo kluczowe. Litera „w" tuż po „lit." jest oznaczeniem litery.
        /// </summary>
        private static bool IsLocationByNoun(IReadOnlyList<UnitToken> tokens, int index) =>
            LocationPrepositions.Contains(tokens[index].Text)
            && (index == 0 || tokens[index - 1].Kind != UnitTokenKind.UnitKeyword);

        /// <summary>
        /// Wejście w treść załącznika poza jego jednostkami: kandydat na cel staje się najgłębszą znaną jednostką
        /// załącznika — także wtedy, gdy przepis żadnej jednostki nie nazwał („w tabeli nr 1:"), żeby przepisy pod nim
        /// odziedziczyły to miejsce (ADR-0080).
        /// </summary>
        private static EnumeratedTarget? InAnnexContent(UnitPathCursor cursor, EnumeratedTarget? pending)
        {
            if (!cursor.EnterAnnexContent())
                return pending;

            var address = new UnitAddress(cursor.Snapshot());
            return pending is null ? new EnumeratedTarget(address, false) : pending with { Address = address };
        }

        /// <summary>
        /// Wzmianka o załączniku, nie miejsce w nim: „o których mowa w załączniku nr 1", „określone w załączniku …".
        /// </summary>
        private static readonly Regex AnnexMention = new(
            @"(?:\bmowa|\b(?:określ|wymien|zawar|wskaz|poda|przewidzia|ustal|zamieszcz|stanowi|wyszczególni|opisa)\p{L}*)\s+we?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>Załącznik aktu zmieniającego, nie zmienianego („w załączniku do niniejszej ustawy").</summary>
        private static readonly Regex OwnAct = new(
            @"^do\s+niniejsz", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Czy „załączniku" stoi jako miejsce komendy w akcie zmienianym: tuż po przyimku „w", nie we wzmiance
        /// („o których mowa w załączniku …") i nie przy załączniku aktu zmieniającego („… do niniejszej ustawy") (ADR-0080).
        /// </summary>
        private static bool IsAnnexLocation(IReadOnlyList<UnitToken> tokens, int index)
        {
            if (index == 0 || tokens[index - 1] is not { Kind: UnitTokenKind.Prose } previous
                || previous.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1].ToLowerInvariant() is not ("w" or "we"))
            {
                return false;
            }

            var before = string.Join(' ', tokens.Take(index).TakeLast(2)
                .Where(token => token.Kind == UnitTokenKind.Prose).Select(token => token.Text));
            if (AnnexMention.IsMatch(before))
                return false;

            var next = index + 1 < tokens.Count && tokens[index + 1].Kind == UnitTokenKind.Designation ? index + 2 : index + 1;
            return next >= tokens.Count || tokens[next] is not { Kind: UnitTokenKind.Prose } after || !OwnAct.IsMatch(after.Text);
        }

        /// <summary>Dokłada ostatni cel i odcina końcowe jednostki dołączone samym przecinkiem.</summary>
        private static List<UnitAddress> Completed(List<EnumeratedTarget> targets, EnumeratedTarget last)
        {
            targets.Add(last);
            while (targets.Count > 1 && targets[^1].JoinedByCommaOnly)
                targets.RemoveAt(targets.Count - 1);
            return targets.Select(target => target.Address).ToList();
        }

        /// <summary>Cel wyliczenia i to, czy dołączył go sam przecinek.</summary>
        private sealed record EnumeratedTarget(UnitAddress Address, bool JoinedByCommaOnly);

        /// <summary>Czym tekst łączy kolejną jednostkę z poprzednią.</summary>
        private enum EnumerationJoin
        {
            /// <summary>Niczym albo prozą.</summary>
            None,

            /// <summary>Samym przecinkiem.</summary>
            Comma,

            /// <summary>Spójnikiem („i", „oraz", „lub", „albo").</summary>
            Conjunction,
        }

        /// <summary>
        /// Krok kursora o jedno oznaczenie; przedział („ust. 2–4") to dwa kroki w tym samym kontekście —
        /// na początek i na koniec.
        /// </summary>
        private static (UnitAddress Target, PendingDisposition Previous)? Step(
            UnitPathCursor cursor, in UnitStepContext stepContext, UnitToken token)
        {
            var isRange = token is { Kind: UnitTokenKind.Range, RangeFrom: not null, RangeTo: not null };
            var first = cursor.Step(stepContext, isRange ? token.RangeFrom! : token.Text);
            if (!first.IsApplied)
                return null;

            var start = cursor.Snapshot();
            var end = isRange && cursor.Step(stepContext, token.RangeTo!).IsApplied ? cursor.Snapshot() : null;
            return (new UnitAddress(start, end), first.Previous);
        }

        /// <summary>
        /// Tekst komendy bez fragmentów w cudzysłowie. „wyrazy „§ 3” zastępuje się …" cytuje jednostkę,
        /// ale jej nie wskazuje.
        /// </summary>
        internal static string WithoutQuotedText(string text)
        {
            string previous;
            do
            {
                previous = text;
                text = InnermostQuoted.Replace(text, " ");
            }
            while (text != previous);

            return UnclosedQuote.Replace(text, " ");
        }

        /// <summary>Separator wyliczenia oznaczeń („§ 2 i 3"); dwukropek i średnik nim nie są.</summary>
        private static bool IsEnumerationSeparator(string separator) =>
            separator.ToLowerInvariant() is "," or "i" or "oraz" or "lub" or "albo";
    }
}
