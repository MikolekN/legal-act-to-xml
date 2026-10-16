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

            context.CurrentFormula = block.FormulaMathMl;
            context.CurrentSymbol = block.LeadingSymbol;
            context.CurrentLeftIndent = block.Layout?.LeftIndentTwips;

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
                SectionIsBasicUnit = context.SectionIsBasicUnit,
            });

            if (IsAnnexHeader(context, classification, text))
            {
                context.InAnnex = true;
                CollectAnnexBlock(block, context);
                return;
            }

            RegisterFootnotes(block, context);

            // Wzór nie ma oznaczenia, więc jednostką nie jest — także gdy złożono go stylem jednostki.
            if (block.FormulaMathMl is not null && classification.Kind != ParagraphKind.Unknown)
                classification = WithoutUnitKind(classification);


            if (IsMisstyledActUnit(context, classification, text))
                classification = AsActContent(classification);

            if (HandleAmendmentFlow(context, classification, block, text, styleId)) return;

            // Akapit w stylu jednostki bez żadnego oznaczenia w treści jest dalszą treścią jednostki, za którą
            // stoi (ADR-0039). Granice nowelizacji rozstrzygnęła już klasyfikacja ze stylu — zmienia się tylko to,
            // co z akapitu powstaje w strukturze aktu.
            if (!context.StyleOnlyUnitsStayUnits && IsUndesignatedUnit(classification.Kind, text) && HasUnitToContinue(context))
                classification = WithoutUnitKind(classification);

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
            && text is not [QuotationMarks.Opening or QuotationMarks.Straight, ..]
            && (StructureProcessor.IsSystematizing(classification.Kind)
                || (context.PendingHeadingUnit != null && classification.Kind is ParagraphKind.Unknown or ParagraphKind.UnitHeading));

        /// <summary>
        /// Jednostka rozpoznana z samego stylu: treść nie niesie jej oznaczenia ani niczego, co na oznaczenie
        /// wygląda. Artykuł ma własną regułę w klasyfikatorze (bez oznaczenia nie jest rozpoznawany).
        /// </summary>
        private static bool IsUndesignatedUnit(ParagraphKind kind, string text)
        {
            var designated = kind switch
            {
                ParagraphKind.Paragraph => ParagraphClassifier.IsParagraphByText(text),
                ParagraphKind.Point => ParagraphClassifier.IsPointByText(text),
                ParagraphKind.Letter => ParagraphClassifier.IsLetterByText(text),
                ParagraphKind.Tiret => ParagraphClassifier.IsTiretByText(text),
                _ => true,
            };

            return !designated && !UnitDesignation.LooksDesignated(text);
        }

        /// <summary>
        /// Jest jednostka, której dalszą treścią akapit może zostać. Pusty ustęp niejawny („Art. 5." w osobnym
        /// wierszu) czeka na swoją treść — akapit w stylu ustępu staje się nim, nie jego dalszą treścią.
        /// </summary>
        private static bool HasUnitToContinue(ParsingContext context) =>
            StructureProcessor.DeepestUnit(context) is { } unit
            && unit is not Saga.Model.EditorialUnits.Article
            && !(unit is Saga.Model.EditorialUnits.Paragraph { IsImplicit: true } paragraph
                 && string.IsNullOrWhiteSpace(paragraph.ContentText));

        private static ClassificationResult WithoutUnitKind(ClassificationResult classification) => new()
        {
            Kind = ParagraphKind.Unknown,
            Confidence = classification.Confidence,
            IsAmendmentContent = classification.IsAmendmentContent,
            StyleType = classification.StyleType,
            Penalties = classification.Penalties,
        };

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
        internal void FlushTable(ParsingContext context)
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

        /// <summary>
        /// Blok obwieszczenia tekstu jednolitego, sprzed aktu ogłaszanego w załączniku. Wiersze nagłówka (rodzaj,
        /// organ, data, przedmiot) czyta kolektor metadanych; reszta jest treścią obwieszczenia i nie przechodzi
        /// przez rozpoznawanie jednostek aktu — przytoczony tam „Art. 57." nie jest artykułem ogłaszanego aktu.
        /// </summary>
        public void CollectAnnouncementBlock(DocumentBlock block, ParsingContext context)
        {
            if (block.Role is BlockRole.FootnoteText or BlockRole.JournalHeader)
                return;

            RegisterFootnotes(block, context);

            if (block.TableCell is null
                && (block.IsEmpty || context.Metadata.Observe(block.Text.CollapseWhitespace().Trim(), block.StyleId)))
                return;

            context.AnnouncementBlocks.Add(block);
        }

        /// <summary>
        /// Blok za wierszem „Załącznik do obwieszczenia …" w obwieszczeniu, które aktu nie ogłasza: treść
        /// załącznika samego obwieszczenia.
        /// </summary>
        public void CollectAnnouncementAnnexBlock(DocumentBlock block, ParsingContext context)
        {
            if (block.Role is BlockRole.FootnoteText or BlockRole.JournalHeader)
                return;

            CollectAnnexBlock(block, context);
        }

        /// <summary>
        /// Wiersz „Załącznik do obwieszczenia …": koniec obwieszczenia, początek aktu, który ono ogłasza.
        /// </summary>
        public void BeginAnnouncedAct(DocumentBlock block, ParsingContext context)
        {
            RegisterFootnotes(block, context);
            context.BeginAnnouncedAct(block.Text.CollapseWhitespace().Trim());
        }

        // Treść odnośników leży poza treścią aktu; znacznik odwołania zostaje w tekście bloku.
        private static void RegisterFootnotes(DocumentBlock block, ParsingContext context)
        {
            foreach (var footnote in block.Footnotes)
            {
                context.Document.Footnotes.Add(new Saga.Model.Footnote
                {
                    Symbol = footnote.Symbol,
                    Marker = footnote.Marker,
                    Run = footnote.Run,
                    IsCustomMark = footnote.IsCustomMark,
                    Paragraphs = [.. footnote.Paragraphs],
                });
            }
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
                // Dokument skończył się w otwartym cytacie: cudzysłowu zamykającego zabrakło i cytat wchłonął
                // resztę aktu. Ślad wskazuje akapit, przed którym powinien był się skończyć (ADR-0049).
                var collector = context.AmendmentCollector;
                if (collector.QuoteTracker.IsInsideQuote && context.CurrentArticle?.Number is { } owner)
                    context.UnclosedQuotationBreak = collector.OpenQuotation.FindBreak(owner);

                _amendmentManager.Flush(context);
                context.InsideAmendment = false;
            }

            // Zapisz metadane aktu zebrane ze strefy tytułowej (rodzaj/data/przedmiot → Title/ActDate).
            // W tekście jednolitym nagłówkiem dokumentu jest nagłówek obwieszczenia, a strefa tytułowa
            // zebrana za granicą należy do ogłaszanego aktu.
            (context.AnnouncementMetadata ?? context.Metadata).ApplyTo(context.Document);

            context.Document.Annexes = AnnexBuilder.Build(context.AnnexBlocks, ofAnnouncement: context.IsPlainAnnouncement);

            if (context.IsPlainAnnouncement)
            {
                // Treść bez numerowanych punktów jest jednym przepisem bez oznaczenia; z punktami („1. …")
                // albo gdy przepisu nie da się złożyć — jednostkami obwieszczenia, jak w tekście jednolitym.
                var announcement = AnnouncementBody.TryBuild(context.AnnouncementBlocks, context.Document);
                if (announcement is null)
                {
                    announcement = AnnouncementUnits.Build(context.AnnouncementBlocks, context.Document, context.SectionIsBasicUnit);
                    announcement.AnnouncesAct = false;
                    AnnouncementUnits.TakeLegalBasis(announcement, context.Document);
                }

                context.Document.Announcement = announcement;
            }
            else if (context.AnnouncementMetadata is not null)
            {
                var announcement = AnnouncementUnits.Build(context.AnnouncementBlocks, context.Document, context.SectionIsBasicUnit);
                announcement.AttachedTo = context.AnnouncedActAttachedTo;
                announcement.IsConsolidatedText =
                    Classify.Document.ZtpPatterns.ConsolidatedTextTitlePattern.IsMatch(context.Document.Subject)
                    || context.Document.Classification is { IsConsolidatedText: true };
                context.Metadata.ApplyTo(announcement);
                context.Document.Announcement = announcement;
            }

            SystematizingUnitValidator.Validate(context.Document);

            // Wyrazy zmian w tekście bez znaczników odnośników tego aktu — rozstrzyga pełny rejestr odnośników.
            TextChangeWording.DropOwnFootnoteMarkers(context.Document);

            // Przebieg po gotowym drzewie: klauzula wejścia w życie.
            // Wymaga kompletnego drzewa, wraz z nowelizacjami.
            // Tekst jednolity jej nie ma: „dzień ogłoszenia" w jego przepisie końcowym to ogłoszenie ustawy,
            // nie obwieszczenia, z którego pochodzi data dokumentu (ADR-0033).
            if (context.Document.Announcement is not { IsConsolidatedText: true })
                _entryIntoForce.Apply(context.Document);

            // Tekst jednolity drukuje jednostkę dwa razy, gdy jej zmiana nie weszła jeszcze w życie (ADR-0060).
            WordingVersions.Assign(context.Document);

            if (context.Document.Announcement is { IsConsolidatedText: true } consolidated)
                consolidated.LegalStateDate = ConsolidatedTextState.Read(consolidated);

            JournalHeaderParser.ApplyFromAttachment(context.Document);
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
            DocumentBlock block,
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

            // Cytat, który w poprzednim przebiegu nie domknął się do końca dokumentu, kończy się przed akapitem
            // wskazanym po tamtym przebiegu (ADR-0049).
            if (wasInsideAmendment && context.InsideAmendment && context.QuotationBreaks.Contains(block))
            {
                _amendmentManager.Flush(context);
                context.InsideAmendment = false;
            }

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

            if (wasInsideAmendment && context.InsideAmendment && quoteTracker.IsInsideQuote)
                NoteWhatMayContinueTheAct(context, classification, block, text);

            // Treść cytowana bez stylu i bez cudzysłowu otwierającego nie ma granicy, którą dałoby się śledzić.
            // Kończy ją to, co do aktu zmienianego należeć nie może (ADR-0048).
            var untracked = wasInsideAmendment && context.InsideAmendment && !classification.IsAmendmentContent
                && string.IsNullOrEmpty(styleId) && !quoteTracker.IsArmed;
            if (untracked && EndsUntrackedQuotation(context, classification, text))
            {
                _amendmentManager.Flush(context);
                context.InsideAmendment = false;
            }

            if (classification.IsAmendmentContent || context.InsideAmendment)
            {
                _amendmentManager.Collect(context, text, styleId);

                // Bilans cudzyslowow domknal cytowana tresc („…") — koniec nowelizacji bez stylow (§ 94 ZTP).
                // Cudzysłów zamykający bez otwarcia domyka treść, której otwarcia zabrakło w źródle.
                var untrackedContent = string.IsNullOrEmpty(styleId) && !classification.IsAmendmentContent && !quoteTracker.IsArmed;
                if (quoteTracker.ClosureReached || (untrackedContent && QuoteBalanceTracker.ClosesUnopenedQuote(text))
                    || (context.InProvisionQuote && context.ProvisionQuoteDepth == 0))
                {
                    _amendmentManager.Flush(context);
                    context.InsideAmendment = false;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Czy akapit kończy treść cytowaną, której cudzysłowu nie da się śledzić: jest kolejnym artykułem aktu
        /// zmieniającego (numer o jeden większy od artykułu z komendą, bez cudzysłowu otwierającego) albo formułą
        /// podpisu. Artykuł cytowany ma numer z aktu zmienianego; zbieżność z kolejnym numerem aktu
        /// zmieniającego jest możliwa, ale bez cudzysłowów i stylów nic jej nie rozstrzyga — a alternatywą jest
        /// połknięcie reszty aktu.
        /// </summary>
        private static bool EndsUntrackedQuotation(ParsingContext context, ClassificationResult classification, string text)
        {
            if (QuoteBalanceTracker.OpensQuote(text))
                return false;

            if (ActSignatureParser.TryParse(text) is not null)
                return true;

            if (classification.Kind != ParagraphKind.Article || context.CurrentArticle?.Number is not { } current)
                return false;

            return ParsingFactories.ParseArticleNumber(text) is { } next && OpenQuotationTrail.Follows(current, next);
        }

        /// <summary>
        /// W otwartym cytacie odnotowuje akapity, które mogą być dalszym ciągiem aktu zmieniającego: oznaczone
        /// jak artykuł i bez własnego cudzysłowu otwierającego albo formułę podpisu. Czy cytat rzeczywiście
        /// wchłonął resztę aktu, okaże się na końcu dokumentu (ADR-0049).
        /// </summary>
        private static void NoteWhatMayContinueTheAct(
            ParsingContext context, ClassificationResult classification, DocumentBlock block, string text)
        {
            if (QuoteBalanceTracker.OpensQuote(text))
                return;

            var collector = context.AmendmentCollector;
            if (ActSignatureParser.TryParse(text) is not null)
                collector.OpenQuotation.NoteSignature(block);
            else if (classification.Kind == ParagraphKind.Article && ParsingFactories.ParseArticleNumber(text) is { } number)
                collector.OpenQuotation.NoteArticle(block, number, collector.QuoteTracker.Depth);
        }
    }
}
