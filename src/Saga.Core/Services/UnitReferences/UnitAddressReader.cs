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
            "„[^„”“\"]*[”“\"]|\"[^„”“\"]*\"", RegexOptions.Compiled);

        /// <summary>Cytat niedomknięty do końca tekstu („otrzymuje brzmienie: „Art. 5 …").</summary>
        private static readonly Regex UnclosedQuote = new("[„\"].*$", RegexOptions.Compiled | RegexOptions.Singleline);

        /// <summary>
        /// Czasownik komendy. Jednostka za nim to przedmiot komendy („po pkt 6 dodaje się przecinek i pkt 7"),
        /// nie kolejny cel — chyba że adres dopiero się przy nim zaczyna („w art. 5 uchyla się ust. 2 i 3").
        /// </summary>
        private static readonly Regex CommandVerb = new(
            @"\b(?:dodaje|uchyla|skreśla|zastępuje)\s+się\b|\botrzymuj[eą]\s+brzmienie",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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

            foreach (var token in UnitReferenceTokenizer.Tokenize(WithoutQuotedText(text)))
            {
                switch (token.Kind)
                {
                    case UnitTokenKind.UnitKeyword:
                        cursor.Announce(token);
                        inRun = true;
                        continue;

                    case UnitTokenKind.Separator when IsEnumerationSeparator(token.Text):
                        join = token.Text == "," && join != EnumerationJoin.Conjunction
                            ? EnumerationJoin.Comma
                            : EnumerationJoin.Conjunction;
                        continue;

                    case UnitTokenKind.Designation or UnitTokenKind.Range when inRun:
                        break;

                    case UnitTokenKind.Prose when UnitReferenceTokenizer.IsPreposition(token.Text):
                        inRun = false;
                        continue;

                    default:
                        inRun = false;
                        join = EnumerationJoin.None;
                        commandVerb |= token.Kind == UnitTokenKind.Prose && CommandVerb.IsMatch(token.Text);
                        continue;
                }

                if (cursor.BeginStep() is not { } stepContext ||
                    Step(cursor, stepContext, token) is not { } step)
                {
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
