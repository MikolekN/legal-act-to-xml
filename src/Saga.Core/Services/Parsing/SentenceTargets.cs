using System.Globalization;
using System.Text.RegularExpressions;
using Saga.Core.Helpers;
using Saga.Model;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Zdanie jako cel komendy (ADR-0102, ustalenie z 2026-10-19 do pytania 7): komenda, która podaje numer zdania,
    /// wskazuje to zdanie (<c>…__zdn_N</c>), a nie całą jednostkę — zmiana wyrazów wskazana jednostką zmieniłaby też
    /// zdania, których komenda nie wymienia (<c>wystapienie</c> domyślnie <c>wszystkie</c>). Odczyt adresu zostawia zdanie
    /// w celu (<see cref="LegalReferenceService"/>); tu rozstrzyga się to, czego sam adres nie mówi.
    /// </summary>
    internal static class SentenceTargets
    {
        /// <summary>
        /// Zdanie dodawane: „dodaje się zdanie trzecie", „dodaje się zdania drugie i trzecie", „dodaje się zdanie" (bez
        /// numeru). Numer pierwszego dodawanego wskazuje zdanie, po którym nowe staje.
        /// </summary>
        private static readonly Regex AddedSentence = new(
            $@"\bdodaje\s+się\s+zdani[ea](?:\s+(?<ordinal>{PolishNumerals.OrdinalAlternation}))?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>Słowo „zdanie" w tekście komendy — także bez numeru („w zdaniu ostatnim", „ostatnie zdanie").</summary>
        private static readonly Regex SentenceWord = new(
            @"\bzda(?:nie|nia|niu|niem|niach)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        internal static void Adjust(Amendment amendment, string? ownText)
        {
            var text = UnitReferences.UnitAddressReader.WithoutQuotedText(ownText ?? string.Empty);
            var targets = amendment.Targets;
            if (targets.Count == 0 || !SentenceWord.IsMatch(text))
                return;

            if (AddedSentence.Match(text) is { Success: true } added)
            {
                AnchorAddedSentence(targets, added);
                return;
            }

            if (MergeReplacedRun(amendment))
                return;

            // Zdanie bez numeru („w zdaniu ostatnim wyrazy …") — adres zdania nieznany: instrukcja ręczna na jednostce.
            if (targets.All(target => target.Structure.Sentence is null))
                foreach (var target in targets.Where(NamesAUnit))
                    target.IsManual = true;
        }

        /// <summary>
        /// Zdanie dodawane staje po zdaniu poprzednim: „w ust. 7 dodaje się zdanie trzecie" → <c>dodaj-po</c> na
        /// <c>…__ust_7__zdn_2</c>. Pierwsze albo bez numeru nie ma poprzednika, który da się wskazać — instrukcja ręczna
        /// na jednostce. Miejsce podane wprost („po zdaniu pierwszym dodaje się zdanie") niesie już sam adres.
        /// </summary>
        private static void AnchorAddedSentence(List<StructuralAmendmentReference> targets, Match added)
        {
            var number = added.Groups["ordinal"].Success && PolishNumerals.TryParseOrdinal(added.Groups["ordinal"].Value, out var ordinal)
                ? ordinal
                : 0;

            foreach (var target in targets.Where(target => NamesAUnit(target) && target.Structure.Sentence is null))
            {
                if (number >= 2)
                    target.Structure.Sentence = (number - 1).ToString(CultureInfo.InvariantCulture);
                else
                    target.IsManual = true;
            }
        }

        /// <summary>
        /// Kilka sąsiednich zdań jednej jednostki zastąpionych jednym cytatem („w § 1 zdanie drugie i trzecie otrzymują
        /// brzmienie:") — jedna instrukcja z zakresem <c>element-eId</c>/<c>element-do-eId</c>, nie cytat powtórzony przy
        /// każdym zdaniu. Zdania niesąsiednie zostają osobnymi celami.
        /// </summary>
        private static bool MergeReplacedRun(Amendment amendment)
        {
            var targets = amendment.Targets;
            if (targets.Count < 2 || amendment.OperationType != AmendmentOperationType.Modification
                || amendment.Content is not { PlainText.Length: > 0 } || amendment.TextChanges.Count > 0)
                return false;

            var numbers = targets.Select(target => int.TryParse(target.Structure.Sentence, out var n) ? n : 0).ToList();
            var unit = UnitOf(targets[0].Structure);
            if (numbers.Any(n => n == 0) || targets.Any(target => UnitOf(target.Structure) != unit)
                || numbers.Zip(numbers.Skip(1)).Any(pair => pair.Second != pair.First + 1))
                return false;

            var first = targets[0];
            first.RangeEnd = targets[^1].Structure.Clone();
            targets.RemoveRange(1, targets.Count - 1);
            return true;
        }

        private static string UnitOf(StructuralReference reference)
        {
            var unit = reference.Clone();
            unit.Sentence = null;
            return unit.ToString();
        }

        private static bool NamesAUnit(StructuralAmendmentReference target) => UnitOf(target.Structure).Length > 0;
    }
}
