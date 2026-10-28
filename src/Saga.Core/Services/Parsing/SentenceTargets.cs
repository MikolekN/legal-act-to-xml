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

        /// <summary>Czy tekst nazywa zdanie — także bez numeru („w zdaniu ostatnim:", „zdanie:").</summary>
        internal static bool NamesASentence(string? text) =>
            SentenceWord.IsMatch(UnitReferences.UnitAddressReader.WithoutQuotedText((text ?? string.Empty).Replace(SoftHyphen, string.Empty)));

        /// <summary>Miękki dywiz („sprawo&#173;zdania") — granica słowa przed nim nie jest początkiem „zdania".</summary>
        private const string SoftHyphen = "\u00AD";

        internal static void Adjust(Amendment amendment, string? ownText)
        {
            var text = UnitReferences.UnitAddressReader.WithoutQuotedText(ownText ?? string.Empty);
            if (amendment.Targets.Count > 0 && SentenceWord.IsMatch(text))
                AdjustNamedSentence(amendment, text);
            else
                MergeReplacedRun(amendment);   // numery zdań podał przepis nadrzędny („w zdaniu:" → „a) pierwsze i drugie …")

            SentenceInEveryUnitOfARange(amendment.Targets);
        }

        private static void AdjustNamedSentence(Amendment amendment, string text)
        {
            var targets = amendment.Targets;
            if (AddedSentence.Match(text) is { Success: true } added)
            {
                AnchorAddedSentence(targets, added);
                return;
            }

            // Zdanie przedmiotem komendy („w art. 7 i 8 zdanie pierwsze otrzymuje brzmienie:") stoi w każdym miejscu przed
            // nim; zdanie-miejsce („w ust. 10 i w ust. 11 w zdaniu pierwszym wyrazy …") zostaje przy swojej jednostce (ADR-0126).
            if (SentenceIsTheObject(amendment, text))
                ShareNamedSentences(targets);

            if (MergeReplacedRun(amendment))
                return;

            // Zdanie bez numeru („w zdaniu ostatnim wyrazy …") — adres zdania nieznany: instrukcja ręczna na jednostce.
            if (targets.All(target => target.Structure.Sentence is null))
                foreach (var target in targets.Where(NamesAUnit))
                    target.IsManual = true;
        }

        /// <summary>
        /// Zdanie jest przedmiotem komendy, nie miejscem zmiany: treść cytowana to same zdania (ADR-0124), a nie wyrazy ani
        /// jednostki, albo czasownik uchylenia ma za przedmiot zdanie i żadnej innej jednostki.
        /// </summary>
        private static bool SentenceIsTheObject(Amendment amendment, string text) => amendment.OperationType switch
        {
            AmendmentOperationType.Repeal =>
                AmendmentCommandParser.RepealsSentence(text) || !AmendmentCommandParser.RepealsNamedUnit(text),
            AmendmentOperationType.Modification =>
                amendment.Content is { PlainTextIsSentences: true, PlainTextIsIntroduction: false } content
                && AmendmentTargetPairing.ContentUnits(content).Count == 0 && amendment.TextChanges.Count == 0,
            _ => false,
        };

        /// <summary>
        /// Miejsca wymienione przed zdaniem-przedmiotem („w art. 7 i 8 zdanie pierwsze") dostają to zdanie: odczyt adresu
        /// przypisuje je tylko ostatniej jednostce wyliczenia (§ 59 ust. 2 ZTP), a art. 7 zostałby celem w całości.
        /// Kilka zdań („zdania pierwsze i drugie") — każde w każdym miejscu.
        /// </summary>
        private static void ShareNamedSentences(List<StructuralAmendmentReference> targets)
        {
            var first = targets.FindIndex(target => target.Structure.Sentence is not null);
            if (first <= 0 || targets.Take(first).Any(target => !NamesAUnit(target) || target.RangeEnd is not null))
                return;

            var unit = UnitOf(targets[first].Structure);
            var sentences = targets.Skip(first)
                .TakeWhile(target => target.Structure.Sentence is not null && target.RangeEnd is null && UnitOf(target.Structure) == unit)
                .Select(target => target.Structure.Sentence!)
                .ToList();
            var shared = targets.Take(first)
                .SelectMany(place => sentences.Select(sentence => new StructuralAmendmentReference
                {
                    Structure = place.Structure.WithSentence(sentence),
                    RawText = place.RawText,
                    IsManual = place.IsManual,
                }))
                .ToList();

            targets.RemoveRange(0, first);
            targets.InsertRange(0, shared);
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
        /// każdym zdaniu; w kilku jednostkach — zakres w każdej (ADR-0126). Zdania niesąsiednie zostają osobnymi celami.
        /// </summary>
        private static bool MergeReplacedRun(Amendment amendment)
        {
            var targets = amendment.Targets;
            if (targets.Count < 2 || amendment.OperationType != AmendmentOperationType.Modification
                || amendment.Content is not { PlainText.Length: > 0 } || amendment.TextChanges.Count > 0
                || targets.Any(target => target.RangeEnd is not null))
                return false;

            var runs = targets.GroupBy(target => UnitOf(target.Structure)).Select(run => run.ToList()).ToList();
            if (runs.All(run => run.Count == 1))
                return false;

            foreach (var run in runs)
            {
                var numbers = run.Select(target => int.TryParse(target.Structure.Sentence, out var n) ? n : 0).ToList();
                if (numbers.Any(n => n == 0) || numbers.Zip(numbers.Skip(1)).Any(pair => pair.Second != pair.First + 1))
                    return false;
            }

            foreach (var run in runs.Where(run => run.Count > 1))
                run[0].RangeEnd = run[^1].Structure.Clone();

            var merged = runs.Select(run => run[0]).ToList();
            targets.Clear();
            targets.AddRange(merged);
            return true;
        }

        /// <summary>
        /// Zdanie w każdej jednostce przedziału („w art. 7–9 zdanie pierwsze", „w ust. 2–4 w zdaniu pierwszym wyrazy …"):
        /// para eId na zdaniach objęłaby tekst między nimi, a rozwinięcie przedziału wymaga jednostek, których tekst nie
        /// nazywa (ADR-0016) — instrukcja ręczna na przedziale jednostek (ADR-0100, ADR-0126).
        /// </summary>
        private static void SentenceInEveryUnitOfARange(List<StructuralAmendmentReference> targets)
        {
            foreach (var target in targets)
            {
                if (target.RangeEnd is not { } end || (target.Structure.Sentence ?? end.Sentence) is null
                    || UnitOf(target.Structure) == UnitOf(end))
                    continue;

                target.Structure = Unit(target.Structure);
                target.RangeEnd = Unit(end);
                target.IsManual = true;
            }
        }

        private static StructuralReference Unit(StructuralReference reference)
        {
            var unit = reference.Clone();
            unit.Sentence = null;
            return unit;
        }

        private static string UnitOf(StructuralReference reference) => Unit(reference).ToString();

        private static bool NamesAUnit(StructuralAmendmentReference target) => UnitOf(target.Structure).Length > 0;
    }
}
