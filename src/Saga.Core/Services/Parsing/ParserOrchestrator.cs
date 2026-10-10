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
            // Kontrakt adapterów: bloki spoza głównego biegu treści NIE są treścią jednostek
            // redakcyjnych. Czytają je osobni konsumenci, z pełnej listy bloków:
            // klasyfikator dokumentu (sygnały TJ) oraz JournalHeaderParser (metadane publikatora).
            if (block.Role is BlockRole.FootnoteText or BlockRole.JournalHeader)
                return;

            // Załącznik stoi za ostatnim artykułem i ma własną treść (tabele, wykazy, nawet „Rozdział 1") —
            // nie jest częścią struktury aktu. Zbierany jest w całości, z pustymi komórkami tabel.
            if (context.InAnnex)
            {
                CollectAnnexBlock(block, context);
                return;
            }

            // Tabela nie składa się z jednostek: jej komórki nie przechodzą przez rozpoznawanie, tylko w całości
            // stają się dalszą treścią jednostki, w której tabela stoi, albo treścią cytowaną nowelizacji.
            if (block.TableCell is { } cell && context.CurrentArticle != null)
            {
                if (context.TableBlocks.Count > 0 && context.TableBlocks[0].TableCell!.Table != cell.Table)
                    FlushTable(context);

                // Tabela należy w całości tam, gdzie się zaczęła.
                if (context.TableBlocks.Count == 0)
                    context.TableInAmendment = context.InsideAmendment || context.AmendmentTriggerDetected
                        || context.AmendmentCollector.IsCollecting;

                context.TableBlocks.Add(block);
                RegisterFootnotes(block, context);
                return;
            }

            FlushTable(context);

            if (block.IsEmpty)
                return;

            var text = block.Text.CollapseWhitespace().Trim();
            var styleId = block.StyleId;

            var hint = BuildNumberingHint(context);
            var classification = _classifier.Classify(new ClassificationInput(text, styleId)
            {
                NumberingHint = hint,
            });

            if (IsAnnexHeader(context, classification, text))
            {
                context.InAnnex = true;
                CollectAnnexBlock(block, context);
                return;
            }

            RegisterFootnotes(block, context);

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
            && AnnexBuilder.AttachedTo.IsMatch(text);

        /// <summary>
        /// Zamyka zbieraną tabelę: w treści przepisu staje się dalszą treścią jednostki, w której stoi,
        /// a w treści nowelizacji — treścią cytowaną.
        /// </summary>
        private void FlushTable(ParsingContext context)
        {
            if (context.TableBlocks.Count == 0)
                return;

            var table = ContentTables.FromBlocks(context.TableBlocks);
            context.TableBlocks.Clear();

            if (!table.Rows.Any(row => row.Cells.Any(cell => cell.Paragraphs.Count > 0)))
                return;

            if (context.TableInAmendment)
                _amendmentManager.CollectTable(context, table);
            else
                StructureProcessor.DeepestUnit(context)?.Continuation.Add(new Saga.Model.ContentBlock { Table = table });
        }

        private static void CollectAnnexBlock(DocumentBlock block, ParsingContext context)
        {
            context.AnnexBlocks.Add(block);
            RegisterFootnotes(block, context);
        }

        // Treść odnośników leży poza treścią aktu; znacznik odwołania zostaje w tekście bloku.
        private static void RegisterFootnotes(DocumentBlock block, ParsingContext context)
        {
            foreach (var footnote in block.Footnotes)
                context.Document.Footnotes.Add(new Saga.Model.Footnote { Symbol = footnote.Symbol, Paragraphs = [.. footnote.Paragraphs] });
        }

        /// <summary>
        /// Finalizuje przetwarzanie — wypróżnia bufor nowelizacji jeśli dokument
        /// kończy się wewnątrz treści nowelizacji.
        /// </summary>
        public void Finalize(ParsingContext context)
        {
            FlushTable(context);

            if (context.InsideAmendment || context.AmendmentCollector.IsCollecting)
            {
                _amendmentManager.Flush(context);
                context.InsideAmendment = false;
            }

            // Zapisz metadane aktu zebrane ze strefy tytułowej (rodzaj/data/przedmiot → Title/ActDate).
            context.Metadata.ApplyTo(context.Document);

            context.Document.Annexes = AnnexBuilder.Build(context.AnnexBlocks);

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
            // Cytat zamknięty przecinkiem („…",): komenda mnoga ciągnie go kolejnym cytatem, a w wyliczeniu
            // zmian po przecinku stoi już następna jednostka aktu — rozstrzyga początek tego bloku.
            if (context.InsideAmendment && context.AmendmentCollector.QuoteTracker.ClosesBefore(text))
            {
                _amendmentManager.Flush(context);
                context.InsideAmendment = false;
            }

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
