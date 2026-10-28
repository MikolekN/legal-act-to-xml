using System.Text.RegularExpressions;
using Saga.Core.Services.Parsing.Builders;
using Saga.Model;
using Saga.Model.EditorialUnits;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Nowelizacja, którą wprowadza przepis cytowany w treści zmiany (ADR-0082): cytowany punkt „4) po art. 28a dodaje się
    /// art. 28b i art. 28c w brzmieniu:" ma własny cytat. Rodzaj zmiany, cel i akt zmieniany czyta się z tego przepisu
    /// i jego cytowanych przodków — tak jak dla przepisu aktu, tylko w obrębie cytatu.
    /// </summary>
    internal static class NestedAmendment
    {
        /// <param name="owner">Cytowany przepis, który jest komendą.</param>
        /// <param name="paragraphs">Akapity jego cytatu — z cudzysłowami granicznymi, które zdejmuje się tutaj.</param>
        internal static Amendment Build(BaseEntity owner, List<CollectedAmendmentParagraph> paragraphs, bool sectionIsBasicUnit)
        {
            var quotes = AmendmentStateManager.TrimQuoteBoundaries(paragraphs);
            var command = owner.ContentText;

            var content = QuotedTextSubject.Read(command, paragraphs)
                ?? new AmendmentBuilder().Build(new AmendmentBuildInput(
                    paragraphs, null, AmendmentOperationType.Modification, sectionIsBasicUnit,
                    QuotesCommonPart: AmendmentCommandParser.QuotesCommonPartPattern.IsMatch(command)));
            AmendmentBuilder.DemoteLoneCommonParts(content, command);
            AmendmentFinalizer.MarkTextObject(content, command);

            var amendment = new Amendment
            {
                OperationType = AmendmentFinalizer.DetectOperationType(AmendmentCommandParser.WithoutTextChanges(command)),
                Content = content,
                HasOpeningQuote = quotes.HasOpening,
                HasClosingQuote = quotes.HasClosing,
                TrailingPunctuation = quotes.Punctuation,
            };
            if (amendment.OperationType == AmendmentOperationType.Repeal)
                amendment.OperationType = AmendmentOperationType.Modification;

            amendment.Targets.AddRange(Targets(owner, sectionIsBasicUnit));
            if (AmendedAct(owner) is { } act)
                amendment.TargetLegalAct = act;
            amendment.TextChanges.AddRange(FinalSignChanges(owner, sectionIsBasicUnit));

            SentenceTargets.Adjust(amendment, command);
            ManualTargets.Mark(amendment, command);
            AmendmentTargetPairing.AnchorContentUnits(amendment);
            return amendment;
        }

        /// <summary>
        /// Zmiana znaku kończącego jednostkę w komendzie złożonej („w pkt 157 kropkę zastępuje się średnikiem i dodaje się
        /// pkt 158", „w pkt 6 średnik zastępuje się kropką i uchyla się pkt 7", „w pkt 4 dodaje się przecinek i …") — jak
        /// w przepisie aktu (<c>AmendmentStateManager.KeepSignChanges</c>): znak zmienia się w jednostce nazwanej przed
        /// komendą, więc zmiana ma własny cel, czytany z tekstu przed nią.
        /// </summary>
        private static IEnumerable<TextChange> FinalSignChanges(BaseEntity owner, bool sectionIsBasicUnit)
        {
            var text = owner.ContentText;
            var signs = AmendmentCommandParser.TextChangeCommands(text).Where(command => command.ChangesFinalSign).ToList();
            if (signs.Count == 0 || signs[0].MatchedCommandText is not { } matched)
                return [];

            var at = text.IndexOf(matched, StringComparison.Ordinal);
            if (at < 0)
                return [];

            var unitOfTheSign = Targets(owner, sectionIsBasicUnit, text[..at]);
            return signs.Select(command => new TextChange
            {
                OldText = TextChangeWording.AsInAmendedAct(command.OldWording ?? string.Empty).Trim(),
                NewText = TextChangeWording.AsInAmendedAct(command.NewWording ?? string.Empty).Trim(),
                Occurrence = TextOccurrence.Last,
                Targets = unitOfTheSign.Select(target => new StructuralAmendmentReference
                {
                    Structure = target.Structure.Clone(),
                    RangeEnd = target.RangeEnd?.Clone(),
                    RawText = target.RawText,
                }).ToList(),
            });
        }

        /// <summary>
        /// Cytowany przepis, który zaczyna się miejscem zmiany („w art. 12 w ust. 4 …", „w pkt 2 po wyrazach …", „użyte
        /// w art. 5 …"): przepis aktu zmieniającego, a nie treść zwykłego aktu.
        /// </summary>
        private static readonly Regex CommandLocation = new(
            $@"^(?:użyt[eya]\s+w\b|(?:w|po|przed)\s+(?:{UnitReferences.UnitReferenceTokenizer.UnitKeywordPattern}|części\s+wspólnej|wprowadzeniu\s+do\s+wyliczenia|tytule|odnośniku))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Komenda bez cytatu, którą jest cytowany przepis (ADR-0082): zmiany w tekście („w art. 12 wyrazy „art. 40”
        /// zastępuje się wyrazami „art. 41”") albo uchylenie nazwanej jednostki („uchyla się art. 11a"). Null, gdy przepis
        /// nie jest taką komendą — także gdy obok zmiany w tekście zmienia jednostkę, bo ta wymaga cytatu za nim.
        /// </summary>
        internal static Amendment? Command(BaseEntity owner, bool sectionIsBasicUnit)
        {
            var text = owner.ContentText.Trim();
            var commands = AmendmentCommandParser.TextChangeCommands(text);
            var structural = AmendmentCommandParser.WithoutTextChanges(text, commands);
            if (AmendmentFinalizer.ModificationPattern.IsMatch(structural))
                return null;

            // Uchylenie jest komendą, gdy tuż za czasownikiem stoi uchylana jednostka (ADR-0069).
            if (AmendmentCommandParser.RepealsNamedUnit(structural))
            {
                var repeal = new Amendment { OperationType = AmendmentOperationType.Repeal };
                repeal.TextChanges.AddRange(FinalSignChanges(owner, sectionIsBasicUnit));
                return Complete(repeal, owner, sectionIsBasicUnit, RepealedAfterTheSign(repeal, text, sectionIsBasicUnit));
            }

            if (commands.Count == 0 || !CommandLocation.IsMatch(text) || AmendmentFinalizer.RepealPattern.IsMatch(structural))
                return null;

            var amendment = new Amendment { OperationType = AmendmentOperationType.Modification };
            amendment.TextChanges.AddRange(commands.Select(command => new TextChange
            {
                OldText = TextChangeWording.AsInAmendedAct(command.OldWording ?? string.Empty).Trim(),
                NewText = TextChangeWording.AsInAmendedAct(command.NewWording ?? string.Empty).Trim(),
                Occurrence = command.ChangesFinalSign ? TextOccurrence.Last : TextOccurrence.All,
            }));
            return Complete(amendment, owner, sectionIsBasicUnit);
        }

        /// <summary>
        /// Uchylenie za zamianą znaku („w pkt 6 średnik zastępuje się kropką i uchyla się pkt 7") dotyczy jednostki nazwanej
        /// po niej — czytanej w kontekście jednostki znaku; null, gdy przepis nie ma takiej budowy.
        /// </summary>
        private static IReadOnlyList<StructuralAmendmentReference>? RepealedAfterTheSign(Amendment repeal, string text, bool sectionIsBasicUnit)
        {
            if (repeal.TextChanges.FirstOrDefault()?.Targets.FirstOrDefault() is not { } unitOfTheSign
                || AmendmentCommandParser.TextChangeCommands(text).LastOrDefault(command => command.ChangesFinalSign)?.MatchedCommandText is not { } sign)
                return null;

            var at = text.IndexOf(sign, StringComparison.Ordinal);
            if (at < 0)
                return null;

            var repealed = new LegalReferenceService().ReadAmendmentTargets(unitOfTheSign.Structure, text[(at + sign.Length)..], sectionIsBasicUnit);
            return repealed.Count > 0 ? repealed : null;
        }

        private static Amendment Complete(Amendment amendment, BaseEntity owner, bool sectionIsBasicUnit,
            IReadOnlyList<StructuralAmendmentReference>? targets = null)
        {
            amendment.Targets.AddRange(targets ?? Targets(owner, sectionIsBasicUnit));
            if (AmendedAct(owner) is { } act)
                amendment.TargetLegalAct = act;
            SentenceTargets.Adjust(amendment, owner.ContentText);
            ManualTargets.Mark(amendment, owner.ContentText);
            return amendment;
        }

        /// <summary>
        /// Cel komendy z tekstu przepisu, czytany w kontekście jego cytowanych przodków („w art. 34:" → „a) ust. 2 otrzymuje
        /// brzmienie:"); przepis bez własnego celu dziedziczy cel przodka.
        /// </summary>
        /// <param name="ownText">Tekst samego przepisu, gdy cel czyta się tylko z jego części (przed zmianą znaku).</param>
        private static IReadOnlyList<StructuralAmendmentReference> Targets(BaseEntity owner, bool sectionIsBasicUnit, string? ownText = null)
        {
            var chain = new List<BaseEntity>();
            for (var unit = owner; unit is not null; unit = unit.Parent)
                chain.Insert(0, unit);

            var service = new LegalReferenceService();
            StructuralReference? context = null;
            IReadOnlyList<StructuralAmendmentReference> targets = [];
            foreach (var unit in chain)
            {
                var read = service.ReadAmendmentTargets(context, unit == owner ? ownText ?? unit.ContentText : unit.ContentText, sectionIsBasicUnit);
                if (read.Count == 0)
                    continue;

                targets = read;
                context = read[0].Structure;
            }

            return targets;
        }

        /// <summary>
        /// Akt zmieniany nazwany w formule cytowanego artykułu („Art. 6. W ustawie z dnia … (Dz. U. z 2025 r. poz. 1483)
        /// wprowadza się następujące zmiany:"); null, gdy cytat go nie nazywa.
        /// </summary>
        private static JournalInfo? AmendedAct(BaseEntity owner)
        {
            for (var unit = owner; unit is not null; unit = unit.Parent)
            {
                if (unit is not Article article)
                    continue;

                if (article.Journals.Count == 0)
                    new JournalReferenceService().ParseJournalReferences(article);
                if (article.Journals.Count > 0)
                    return article.Journals[0];
            }

            return null;
        }
    }
}
