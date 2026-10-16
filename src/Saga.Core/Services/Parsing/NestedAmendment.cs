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
                    paragraphs, null, AmendmentOperationType.Modification, sectionIsBasicUnit));
            AmendmentBuilder.DemoteLoneCommonParts(content, command);
            if (content is { PlainText.Length: > 0 })
                content.PlainTextIsSentences = AmendmentFinalizer.SentenceObjectPattern.IsMatch(command.TrimEnd());

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

            AmendmentTargetPairing.AnchorContentUnits(amendment);
            return amendment;
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
                return Complete(new Amendment { OperationType = AmendmentOperationType.Repeal }, owner, sectionIsBasicUnit);

            if (commands.Count == 0 || !CommandLocation.IsMatch(text) || AmendmentFinalizer.RepealPattern.IsMatch(structural))
                return null;

            var amendment = new Amendment { OperationType = AmendmentOperationType.Modification };
            amendment.TextChanges.AddRange(commands.Select(command => new TextChange
            {
                OldText = TextChangeWording.AsInAmendedAct(command.OldWording ?? string.Empty).Trim(),
                NewText = TextChangeWording.AsInAmendedAct(command.NewWording ?? string.Empty).Trim(),
                Occurrence = command.Kind == AmendmentCommandKind.ReplaceSign ? TextOccurrence.Last : TextOccurrence.All,
            }));
            return Complete(amendment, owner, sectionIsBasicUnit);
        }

        private static Amendment Complete(Amendment amendment, BaseEntity owner, bool sectionIsBasicUnit)
        {
            amendment.Targets.AddRange(Targets(owner, sectionIsBasicUnit));
            if (AmendedAct(owner) is { } act)
                amendment.TargetLegalAct = act;
            return amendment;
        }

        /// <summary>
        /// Cel komendy z tekstu przepisu, czytany w kontekście jego cytowanych przodków („w art. 34:" → „a) ust. 2 otrzymuje
        /// brzmienie:"); przepis bez własnego celu dziedziczy cel przodka.
        /// </summary>
        private static IReadOnlyList<StructuralAmendmentReference> Targets(BaseEntity owner, bool sectionIsBasicUnit)
        {
            var chain = new List<BaseEntity>();
            for (var unit = owner; unit is not null; unit = unit.Parent)
                chain.Insert(0, unit);

            var service = new LegalReferenceService();
            StructuralReference? context = null;
            IReadOnlyList<StructuralAmendmentReference> targets = [];
            foreach (var unit in chain)
            {
                var read = service.ReadAmendmentTargets(context, unit.ContentText, sectionIsBasicUnit);
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
