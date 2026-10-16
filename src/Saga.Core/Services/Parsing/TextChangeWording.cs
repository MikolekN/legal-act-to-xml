using System.Text.RegularExpressions;
using Saga.Core.Services.EntryIntoForce;
using Saga.Model;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Wyrazy zmiany w tekście tak, jak stoją w akcie zmienianym (<see cref="TextChange.OldText"/>,
    /// <see cref="TextChange.NewText"/>). Instrukcja wskazuje tekst do odszukania, więc nie może nieść tego, czego
    /// w akcie zmienianym nie ma.
    /// </summary>
    internal static class TextChangeWording
    {
        // Miękki dywiz Worda (U+00AD) — wskazówka dzielenia wyrazu, nie znak tekstu („jesienno­­-zimowym").
        private const char SoftHyphen = (char)0x00AD;

        private static readonly Regex MarkerNumber = new(@"\d+(?=\))", RegexOptions.Compiled);

        /// <summary>Wyrazy bez miękkiego dywizu.</summary>
        internal static string AsInAmendedAct(string wording) =>
            wording.Replace(SoftHyphen.ToString(), string.Empty, StringComparison.Ordinal);

        /// <summary>
        /// Zdejmuje z wyrazów zmian w tekście znaczniki odnośników tego dokumentu: „(Dz. U. poz. 1270, z późn.
        /// zm.[8)])" niesie odnośnik aktu zmieniającego o zmianach ustawy; treść odnośnika stoi w zdaniu komendy,
        /// a akt zmieniany znacznika nie ma. Rozstrzyga pełny rejestr odnośników, więc przebieg idzie po gotowym
        /// dokumencie: znacznik wpisany tekstem bywa powtórzeniem odnośnika, którego odwołanie stoi dalej. Znacznik,
        /// którego numer nie jest odnośnikiem tego dokumentu, zostaje. Przebieg obejmuje też przepisy przytoczone
        /// w obwieszczeniu tekstu jednolitego — ich odnośniki obwieszczenie powtarza jako własne.
        /// </summary>
        internal static void DropOwnFootnoteMarkers(LegalDocument document)
        {
            if (document.Footnotes.Count == 0)
                return;

            var markers = document.Footnotes.Select(footnote => footnote.Marker).ToHashSet(StringComparer.Ordinal);
            string Without(string text) => FootnoteMarkers.Reference.Replace(text, marker =>
                MarkerNumber.Matches(marker.Value).All(number => markers.Contains(number.Value)) ? string.Empty : marker.Value);

            foreach (var unit in Units(document))
            {
                if (unit is not IHasAmendments { Amendment.TextChanges: { Count: > 0 } changes })
                    continue;

                foreach (var change in changes)
                {
                    change.OldText = Without(change.OldText);
                    change.NewText = Without(change.NewText);
                }
            }
        }

        /// <summary>
        /// Jednostki dokumentu: aktu i obwieszczenia, z treścią cytowaną nowelizacji i z przytoczeniami w dalszej
        /// treści jednostek — każda raz.
        /// </summary>
        private static IEnumerable<BaseEntity> Units(LegalDocument document)
        {
            var roots = new List<BaseEntity>(document.Articles);
            if (document.Announcement is { } announcement)
            {
                roots.AddRange(announcement.Points);
                if (announcement.Body is { } body)
                    roots.Add(body);
            }

            var seen = new HashSet<BaseEntity>(ReferenceEqualityComparer.Instance);
            var pending = new Stack<BaseEntity>(roots.AsEnumerable().Reverse());
            while (pending.Count > 0)
            {
                var root = pending.Pop();
                foreach (var unit in EditorialUnitWalker.SelfAndDescendants(root, includeAmendmentContent: true))
                {
                    if (!seen.Add(unit))
                        continue;

                    yield return unit;
                    foreach (var content in unit.Continuation.Select(block => block.Quotation?.Content).OfType<AmendmentContent>())
                    {
                        foreach (var quoted in QuotedUnits(content))
                            pending.Push(quoted);
                    }
                }
            }
        }

        private static IEnumerable<BaseEntity> QuotedUnits(AmendmentContent content) =>
            content.SystematizingUnits.Cast<BaseEntity>()
                .Concat(content.Articles).Concat(content.Paragraphs).Concat(content.Points)
                .Concat(content.Letters).Concat(content.Tirets).Concat(content.CommonParts);
    }
}
