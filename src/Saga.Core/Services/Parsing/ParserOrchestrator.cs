using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Wordprocessing;
using Saga.Core.Ingest;
using Saga.Core.Services.Classify;
using Saga.Core.Services.EntryIntoForce;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Koordynuje jednoprzebiegowe parsowanie dokumentu prawnego.
    /// Deleguje logikę nowelizacji do <see cref="AmendmentStateManager"/>
    /// oraz budowanie encji do <see cref="StructureProcessor"/>.
    /// </summary>
    public sealed class ParserOrchestrator
    {
        private readonly IParagraphClassifier _classifier;
        private readonly AmendmentStateManager _amendmentManager = new();
        private readonly StructureProcessor _structureProcessor = new();
        private readonly EntryIntoForceService _entryIntoForce = new();

        // „Załącznik do ustawy z dnia…", „Załączniki do ustawy…", „Załącznik nr 2 do ustawy…" — nie
        // „Załącznik do obwieszczenia", który w tekście jednolitym otwiera sam akt.
        private static readonly Regex AnnexHeader = new(
            @"^Załącznik(?:i)?(?:\s+nr\s+\S+)?\s+do\s+(?:ustawy|rozporządzenia)\s+z\s+dnia\b", RegexOptions.Compiled);

        /// <summary>
        /// Konstruktor domyślny — używa ParagraphClassifier.
        /// </summary>
        public ParserOrchestrator(IParagraphClassifier? classifier = null)
        {
            _classifier = classifier ?? new ParagraphClassifier();
        }

        /// <summary>
        /// Przetwarza pojedynczy akapit OpenXml — cienki adapter na ProcessBlock,
        /// zachowany dla wstecznej kompatybilności (testy i dotychczasowi wywołujący).
        /// </summary>
        public void ProcessParagraph(Paragraph paragraph, ParsingContext context)
            => ProcessBlock(DocxBlockReader.ToBlock(paragraph, blockIndex: null), context);

        /// <summary>
        /// Przetwarza pojedynczy blok reprezentacji pośredniej i aktualizuje stan kontekstu.
        /// Flow:
        /// 1. Obliczenie NumberingHint na podstawie bieżącego stanu kontekstu.
        /// 2. Klasyfikacja akapitu (Kind + Confidence).
        /// 3. Aktualizacja stanu nowelizacji (AmendmentStateManager).
        /// 4. Budowanie encji lub dołączanie treści nowelizacji (StructureProcessor).
        /// 5. Wykrywanie triggerów nowelizacji po zbudowaniu encji.
        /// </summary>
        public void ProcessBlock(DocumentBlock block, ParsingContext context)
        {
            if (block.IsEmpty)
                return;

            // Kontrakt adapterów: bloki spoza głównego biegu treści NIE są treścią jednostek
            // redakcyjnych. Czytają je osobni konsumenci, z pełnej listy bloków:
            // klasyfikator dokumentu (sygnały TJ) oraz JournalHeaderParser (metadane publikatora).
            if (block.Role is BlockRole.FootnoteText or BlockRole.JournalHeader)
                return;

            var text = block.Text.CollapseWhitespace().Trim();
            var styleId = block.StyleId;

            var hint = BuildNumberingHint(context);
            var classification = _classifier.Classify(new ClassificationInput(text, styleId)
            {
                NumberingHint = hint,
            });

            // Załącznik stoi za ostatnim artykułem i ma własną treść (tabele, wykazy, nawet „Rozdział 1") —
            // nie jest częścią struktury aktu.
            if (context.InAnnex) return;
            if (IsAnnexHeader(context, classification, text))
            {
                context.InAnnex = true;
                return;
            }

            // Treść odnośników leży poza treścią aktu; znacznik odwołania zostaje w tekście bloku.
            foreach (var footnote in block.Footnotes)
                context.Document.Footnotes.Add(new Saga.Model.Footnote { Symbol = footnote.Symbol, Paragraphs = [.. footnote.Paragraphs] });

            if (IsMisstyledActUnit(context, classification, text))
                classification = AsActContent(classification);

            if (HandleAmendmentFlow(context, classification, text, styleId)) return;

            if (_structureProcessor.Process(context, classification, text, styleId, block.Layout))
                _amendmentManager.DetectTrigger(context, text);
        }

        /// <summary>
        /// Tekst jednolity składa jednostki dodane nowelizacją stylami zmian (Z…): oznaczenie i tytuł
        /// jednostki w stylu Z bez komendy nowelizacyjnej przed nimi i bez cudzysłowu to jednostka aktu.
        /// </summary>
        private static bool IsMisstyledActUnit(ParsingContext context, ClassificationResult classification, string text) =>
            classification.IsAmendmentContent
            && !context.InsideAmendment && !context.AmendmentTriggerDetected && !context.AmendmentCollector.IsCollecting
            && text is not ['„' or '"', ..]
            && (StructureProcessor.IsSystematizing(classification.Kind)
                || (context.PendingHeadingUnit != null && classification.Kind is ParagraphKind.Unknown or ParagraphKind.UnitHeading));

        private static ClassificationResult AsActContent(ClassificationResult classification) => new()
        {
            Kind = classification.Kind,
            Confidence = classification.Confidence,
            StyleType = classification.StyleType,
            Penalties = classification.Penalties,
        };

        private static bool IsAnnexHeader(ParsingContext context, ClassificationResult classification, string text) =>
            context.CurrentArticle != null
            && !classification.IsAmendmentContent
            && !context.InsideAmendment && !context.AmendmentCollector.IsCollecting
            && AnnexHeader.IsMatch(text);

        /// <summary>
        /// Finalizuje przetwarzanie — wypróżnia bufor nowelizacji jeśli dokument
        /// kończy się wewnątrz treści nowelizacji.
        /// </summary>
        public void Finalize(ParsingContext context)
        {
            if (context.InsideAmendment || context.AmendmentCollector.IsCollecting)
            {
                _amendmentManager.Flush(context);
                context.InsideAmendment = false;
            }

            // Zapisz metadane aktu zebrane ze strefy tytułowej (rodzaj/data/przedmiot → Title/ActDate).
            context.Metadata.ApplyTo(context.Document);

            SystematizingUnitValidator.Validate(context.Document);

            // Przebieg po gotowym drzewie: klauzula wejścia w życie.
            // Wymaga kompletnego drzewa, wraz z nowelizacjami.
            _entryIntoForce.Apply(context.Document);
        }

        // ============================================================
        // Obliczanie NumberingHint
        // ============================================================

        /// <summary>
        /// Oblicza podpowiedź numeracyjną na podstawie aktualnego stanu kontekstu.
        /// Zwraca hint dla najbardziej szczegółowego aktywnego poziomu hierarchii.
        /// </summary>
        private static NumberingHint? BuildNumberingHint(ParsingContext context)
        {
            // Poziom: litera (jeśli jest aktywna litera z numerem)
            if (context.CurrentLetter?.Number != null)
                return new NumberingHint
                {
                    ExpectedKind = ParagraphKind.Letter,
                    ExpectedNumber = context.CurrentLetter.Number,
                };

            // Poziom: punkt
            if (context.CurrentPoint?.Number != null)
                return new NumberingHint
                {
                    ExpectedKind = ParagraphKind.Point,
                    ExpectedNumber = context.CurrentPoint.Number,
                };

            // Poziom: ustęp (pomijamy ustępy niejawne)
            if (context.CurrentParagraph?.Number != null &&
                context.CurrentParagraph is { IsImplicit: false })
                return new NumberingHint
                {
                    ExpectedKind = ParagraphKind.Paragraph,
                    ExpectedNumber = context.CurrentParagraph.Number,
                };

            // Poziom: artykuł
            if (context.CurrentArticle?.Number != null)
                return new NumberingHint
                {
                    ExpectedKind = ParagraphKind.Article,
                    ExpectedNumber = context.CurrentArticle.Number,
                };

            return null;
        }

        // ============================================================
        // Obsługa cyklu stanu nowelizacji
        // ============================================================

        /// <summary>
        /// Hermetyzuje cały cykl stanu nowelizacji. Zwraca true jeśli akapit został skonsumowany.
        /// </summary>
        private bool HandleAmendmentFlow(
            ParsingContext context,
            ClassificationResult classification,
            string text,
            string? styleId)
        {
            bool wasInsideAmendment = context.InsideAmendment;
            _amendmentManager.UpdateState(context, classification, text);

            if (wasInsideAmendment && !context.InsideAmendment)
                _amendmentManager.Flush(context);

            // Guard: ShouldExitForNewParentLawTrigger jest sensowne tylko gdy bylismy JUZ w nowelizacji
            // przed wywolaniem UpdateState. Jesli wlasnie weszlismy (wasInsideAmendment=false),
            // to jestesmy przy pierwszym akapicie tresci — nie wolno go natychmiast wyrzucic,
            // nawet jesli jego tekst zawiera zwrot nowelizacyjny (np. "w brzmieniu").
            var quoteTracker = context.AmendmentCollector.QuoteTracker;
            if (wasInsideAmendment && _amendmentManager.ShouldExitForNewParentLawTrigger(context, classification, text))
            {
                if (quoteTracker.IsInsideQuote)
                {
                    // Komenda nowelizacyjna WEWNATRZ otwartego cytatu to nowelizacja zagniezdzona (ZZ) —
                    // cytat trwa; bez stylow nie da sie jej rozlozyc (Warning przy finalizacji).
                    quoteTracker.MarkNestedTrigger();
                }
                else
                {
                    _amendmentManager.Flush(context);
                    context.InsideAmendment = false;
                }
            }

            if (classification.IsAmendmentContent || context.InsideAmendment)
            {
                _amendmentManager.Collect(context, text, styleId);

                // Bilans cudzyslowow domknal cytowana tresc („…") — koniec nowelizacji bez stylow (§ 94 ZTP).
                if (quoteTracker.ClosureReached)
                {
                    _amendmentManager.Flush(context);
                    context.InsideAmendment = false;
                }
                return true;
            }
            return false;
        }
    }
}
