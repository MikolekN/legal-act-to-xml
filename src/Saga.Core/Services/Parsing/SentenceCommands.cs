using Saga.Model;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Kilka komend w osobnych zdaniach jednej jednostki aktu zmieniającego: „2) art. 127 uchyla się. W art. 128 zdanie
    /// drugie otrzymuje brzmienie:". ZTP rozdziela zmiany na jednostki wyliczenia (§ 94 ust. 2), ale takie brzmienie jest
    /// poprawnym zapisem dwóch zmian — każde zdanie to osobna komenda z celami ze swojego tekstu (ADR-0122). Komendy przed
    /// ostatnią muszą kończyć się w swoim zdaniu (uchylenie, zmiana w tekście): treść cytowana stoi tylko za ostatnią.
    /// </summary>
    internal static class SentenceCommands
    {
        /// <summary>
        /// Komenda w zdaniu jednostki: tekst zdania, jego numer (<see cref="TextSegment.Order"/>) i cele odczytane z niego.
        /// </summary>
        internal sealed record Command(string Text, int Sentence)
        {
            internal IReadOnlyList<StructuralAmendmentReference> Targets { get; init; } = [];
        }

        /// <summary>
        /// Komendy zdań jednostki, gdy jest ich kilka, a każda przed ostatnią kończy się w swoim zdaniu; null w każdym innym
        /// przypadku — jednostka jest wtedy jedną komendą.
        /// </summary>
        internal static IReadOnlyList<Command>? Read(BaseEntity unit)
        {
            if (string.IsNullOrWhiteSpace(unit.ContentText))
                return null;

            var commands = Sentences(unit.ContentText).Where(sentence => HasCommand(sentence.Text)).ToList();
            return commands.Count > 1 && commands.SkipLast(1).All(command => IsComplete(unit, command.Text)) ? commands : null;
        }

        /// <summary>
        /// Zdania tekstu w podziale eksportu (<see cref="ParsingFactories.SplitIntoSentences"/>), z numerem segmentu.
        /// Granica wewnątrz cudzysłowu („wyrazy „Art. 5. Opłatę …” zastępuje się …") zdania nie kończy — części łączą się
        /// w jedno zdanie z numerem ostatniej.
        /// </summary>
        private static IEnumerable<Command> Sentences(string text)
        {
            var parts = new List<string>();
            var open = 0;
            var straight = 0;
            var last = 0;
            foreach (var segment in ParsingFactories.SplitIntoSentences(text))
            {
                parts.Add(segment.Text);
                last = segment.Order;
                foreach (var character in segment.Text)
                {
                    if (character == QuotationMarks.Opening)
                        open++;
                    else if (QuotationMarks.IsClosing(character) && open > 0)
                        open--;
                    else if (character == QuotationMarks.Straight)
                        straight++;
                }

                if (open > 0 || straight % 2 == 1)
                    continue;

                yield return new Command(string.Join(' ', parts), last);
                parts.Clear();
            }

            if (parts.Count > 0)
                yield return new Command(string.Join(' ', parts), last);
        }

        /// <summary>Zdanie niesie komendę: czasownik zmiany albo uchylenia poza tekstem w cudzysłowie.</summary>
        private static bool HasCommand(string sentence) =>
            AmendmentCommandParser.HasChangeVerb(UnitReferences.UnitAddressReader.WithoutQuotedText(sentence));

        /// <summary>
        /// Komenda kończy się w swoim zdaniu: zmiana w tekście albo uchylenie — rozpoznane tak samo jak w całej jednostce
        /// (<see cref="AmendmentStateManager.IsRepealCommand"/>) — bez zwrotu zapowiadającego brzmienie.
        /// </summary>
        private static bool IsComplete(BaseEntity unit, string sentence)
        {
            var textChanges = AmendmentCommandParser.TextChangeCommands(sentence);
            var structural = AmendmentCommandParser.WithoutTextChanges(sentence, textChanges);
            return !AmendmentFinalizer.ModificationPattern.IsMatch(structural)
                && (textChanges.Count > 0 || AmendmentStateManager.IsRepealCommand(unit, structural));
        }
    }
}
