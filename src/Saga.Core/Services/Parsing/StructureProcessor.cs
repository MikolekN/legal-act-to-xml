using System.Text.RegularExpressions;
using Saga.Core.Helpers;
using Saga.Core.Ingest;
using Saga.Core.Services.Classify;
using Saga.Core.Services.Classify.Document;
using Saga.Core.Services.Parsing.Builders;
using Saga.Core.Services.UnitReferences;
using Saga.Model;
using Saga.Model.SystematizingUnits;
using Serilog;
using DtoParagraph = Saga.Model.EditorialUnits.Paragraph;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Buduje encje domenowe (Article, Paragraph, Point, Letter, Tiret)
    /// na podstawie wyników klasyfikacji akapitu. Odpowiada za:
    /// - budowanie encji i resetowanie podpoziomów w kontekście,
    /// - transfer kar numeracyjnych z ClassificationResult jako ValidationMessage,
    /// - aktualizację pozycji strukturalnej (CurrentStructuralReference),
    /// - wykrywanie celów nowelizacji w treści encji,
    /// - obsługę akapitów wrap-up (CZ_WSP_*).
    /// </summary>
    internal sealed class StructureProcessor
    {
        private readonly ArticleBuilder _articleBuilder = new();
        private readonly ParagraphBuilder _paragraphBuilder = new();
        private readonly PointBuilder _pointBuilder = new();
        private readonly LetterBuilder _letterBuilder = new();
        private readonly TiretBuilder _tiretBuilder = new();
        private readonly SystematizingUnitBuilder _systematizingUnitBuilder = new();
        private readonly JournalReferenceService _journalReferenceService = new();

        /// <summary>
        /// Obsługuje akapit wrap-up (zamknięcie listy przez półpauzę przed pozycją nadrzędną).
        /// Wywoływana z Process() gdy classification.Kind == WrapUp.
        /// </summary>
        private static bool TryHandleWrapUp(ParsingContext context, string text, string? styleId)
        {
            if (!TryGetWrapUpTarget(styleId, out var targetKind))
                return false;

            if (!IsWrapUpByText(text))
            {
                Log.Warning(
                    "WrapUp pominiety: styl={StyleId}, brak polpauzy w tekscie: {Text}",
                    styleId,
                    text);
                return true;
            }

            BaseEntity? parent = targetKind switch
            {
                AmendmentTargetKind.Point => context.CurrentParagraph,
                AmendmentTargetKind.Letter => context.CurrentPoint,
                AmendmentTargetKind.Tiret => context.CurrentLetter,
                _ => null
            };

            if (parent == null)
            {
                Log.Warning(
                    "WrapUp pominiety: brak rodzica dla {TargetKind} (styl={StyleId})",
                    targetKind,
                    styleId);
                return true;
            }

            var hasListItems = targetKind switch
            {
                AmendmentTargetKind.Point => context.CurrentParagraph?.Points.Count > 0,
                AmendmentTargetKind.Letter => context.CurrentPoint?.Letters.Count > 0,
                AmendmentTargetKind.Tiret => context.CurrentLetter?.Tirets.Count > 0,
                _ => false
            };

            if (!hasListItems)
            {
                Log.Warning(
                    "WrapUp pominiety: brak elementow listy dla {TargetKind} (styl={StyleId})",
                    targetKind,
                    styleId);
                return true;
            }

            var attached = ParsingFactories.AttachWrapUpCommonPart(parent, text);
            Log.Debug(attached
                ? "WrapUp dodany dla {ParentId}"
                : "WrapUp pominiety (duplikat lub pusty tekst) dla {ParentId}",
                parent.Id);

            return true;
        }

        /// <summary>
        /// Część wspólna bez stylu CZ_WSP_*: „– …" sklasyfikowane z samego tekstu jako tiret.
        /// Wyliczenie tiretów otwiera zawsze wprowadzenie zakończone dwukropkiem (§ 56 ZTP), a ostatni
        /// element przed częścią wspólną nie ma żadnego znaku końcowego (§ 57 ust. 3). Myślnik tuż po
        /// punkcie albo literze BEZ znaku końcowego zamyka więc wyliczenie jako część wspólna.
        ///
        /// Warunek „bez znaku", a nie tylko „bez dwukropka", jest celowy: po elemencie zakończonym
        /// przecinkiem lub średnikiem myślnik bywa w korpusie legendą wzoru albo pozycją tabeli, nie
        /// częścią wspólną — tam zostaje dotychczasowe zachowanie. Myślnik po tirecie zostaje tiretem:
        /// kontekst nie rozstrzyga (potrzebne wcięcie).
        /// </summary>
        private static bool TryHandleStylelessWrapUp(
            ParsingContext context, ClassificationResult classification, string text, string? styleId)
        {
            if (classification.IsAmendmentContent
                || ParagraphClassifier.GetStyleType(styleId) == "TIR"
                || !IsWrapUpByText(text))
                return false;

            // Lista liter (część wspólna w punkcie) albo punktów (część wspólna w ustępie).
            BaseEntity? lastItem;
            BaseEntity? parent;
            if (context.CurrentLetter != null)
            {
                if (context.CurrentLetter.Tirets.Count > 0)
                    return false;
                lastItem = context.CurrentLetter;
                parent = context.CurrentPoint;
            }
            else if (context.CurrentPoint != null && context.CurrentPoint.Letters.Count == 0)
            {
                lastItem = context.CurrentPoint;
                parent = context.CurrentParagraph;
            }
            else
            {
                return false;
            }

            if (parent == null || !CommonPartPlacement.LeavesEnumerationOpen(lastItem.ContentText))
                return false;

            if (!ParsingFactories.AttachWrapUpCommonPart(parent, text))
                return false;

            Log.Warning("Myślnik bez stylu uznany za część wspólną (poprzednia jednostka bez znaku końcowego): {Text}", text);
            ValidationReporter.AddValidationMessage(parent, ValidationLevel.Info,
                "Część wspólna rozpoznana bez stylu: ostatni element wyliczenia nie ma znaku końcowego (§ 57 ust. 3 ZTP).");
            return true;
        }

        /// <summary>Początek sankcji karnej: „podlega karze…", także po myślniku.</summary>
        private static readonly Regex SanctionStart = new(
            @"^(?:[–-]\s*)?podlega\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private const string SanctionStyleName = "S_KARN";

        /// <summary>
        /// Sankcja karna zapisana w osobnym akapicie (styl S_KARN): „Kto …, || podlega grzywnie." Jest dalszym
        /// ciągiem zdania przepisu, a po wyliczeniu — jego częścią wspólną. Rozpoznają ją dwa zgodne sygnały
        /// z trzech: styl sankcji, początek „podlega" i niedokończone zdanie przepisu przed nią.
        /// </summary>
        private static bool TryHandleSanction(ParsingContext context, string text, string? styleId)
        {
            // W wyliczeniu tiretów „– podlega …" jest kolejnym tiretem.
            if (context.CurrentTiret != null || context.CurrentParagraph is not { } paragraph)
                return false;

            var deepest = (BaseEntity?)context.CurrentLetter ?? (BaseEntity?)context.CurrentPoint ?? paragraph;
            var closing = paragraph.CommonParts.LastOrDefault(CommonPartPlacement.IsClosing);
            var before = (closing?.ContentText ?? deepest.ContentText).TrimEnd();

            var styled = StyleLibraryMapper.TryGetStyleInfo(styleId, out var info)
                && info!.DisplayName.StartsWith(SanctionStyleName, StringComparison.Ordinal);
            var worded = SanctionStart.IsMatch(text);
            var unfinished = before.Length > 0 && before[^1] is not ('.' or ';' or ':');
            if ((styled ? 1 : 0) + (worded ? 1 : 0) + (unfinished ? 1 : 0) < 2)
                return false;

            var sanction = text.Trim();
            if (closing is not null)
            {
                // Sankcja za częścią wspólną wyliczenia ciągnie jej zdanie.
                closing.ContentText = $"{closing.ContentText.TrimEnd()} {sanction}";
            }
            else if (context.CurrentPoint is not null)
            {
                // Po wyliczeniu sankcja jest jego częścią wspólną — odnosi się do wszystkich punktów.
                ParsingFactories.AttachWrapUpCommonPart(paragraph, sanction);
            }
            else
            {
                ParsingFactories.SetContentAndSegments(paragraph, $"{paragraph.ContentText.TrimEnd()} {sanction}");
            }

            return true;
        }

        /// <summary>
        /// Jednostka, której treść otwiera wyliczenie: punkt i litera wstawione za pominięty poziom nie mają
        /// treści, więc wprowadzeniem jest zdanie najbliższej jednostki nad nimi (litery wprost w ustępie).
        /// </summary>
        private static BaseEntity ExplicitOwner(BaseEntity unit) =>
            unit is { UnitType: UnitType.Point or UnitType.Letter, IsImplicit: true, Parent: { } parent }
                ? ExplicitOwner(parent)
                : unit;

        /// <summary>
        /// Przetwarza akapit: buduje encję domenową na podstawie klasyfikacji,
        /// aktualizuje pozycję strukturalną i wykrywa cele nowelizacji.
        /// Zwraca true jeśli encja została zbudowana (wykrywanie triggera powinno nastąpić po powrocie).
        /// </summary>
        public bool Process(ParsingContext context, ClassificationResult classification, string text,
            string? sourceStyleId = null, BlockLayoutInfo? layout = null)
        {
            // Tytuł złamany na kilka bloków w stylu tytułu — dalszy ciąg dokleja się do przypisanego.
            // Tytuł zaczyna się wielką literą (§ 62 ust. 2 ZTP), więc blok od wielkiej litery to osobny
            // nagłówek (np. nienumerowany „Przepis wstępny"), nie ciąg dalszy.
            if (classification.Kind == ParagraphKind.UnitHeading && context.HeadingContinuationUnit is { } titled
                && text.TrimStart() is [var first, ..] && char.IsLower(first))
            {
                titled.Heading = $"{titled.Heading} {text.Trim()}";
                return true;
            }
            context.HeadingContinuationUnit = null;

            // Tytuł jednostki systematyzacyjnej (drugi wiersz wzorca dwuwierszowego, § 62 ust. 2):
            // pierwszy akapit po oznaczeniu, jeśli sam nie jest jednostką. Ze stylu tytułu — bez
            // heurystyki; bez stylu — gdy wygląda na tytuł.
            if (context.PendingHeadingUnit is { } pendingUnit)
            {
                context.PendingHeadingUnit = null;
                if (IsStyledHeading(classification, pendingUnit, text)
                    || (IsUnknownOrHeading(classification.Kind) && (IsHeadingCandidate(text) || RepealedUnitMarker.IsMatch(text))))
                {
                    pendingUnit.Heading = text.Trim();
                    if (classification.Kind == ParagraphKind.UnitHeading)
                        context.HeadingContinuationUnit = pendingUnit;
                    return true;
                }
                // brak tytułu — kontynuuj normalną obsługę bieżącego akapitu
            }

            // Jednostki systematyzacyjne (§ 60-62) — mogą wystąpić przed pierwszym artykułem.
            if (IsSystematizing(classification.Kind))
            {
                HandleSystematizingUnit(context, classification, text, sourceStyleId);
                return true;
            }

            if (classification.Kind == ParagraphKind.Article)
            {
                // Pierwszy artykuł zamyka strefę tytułową — metadane dalej nie są zbierane.
                context.Metadata.Seal();

                // Podpis zamyka akt: formuła, po której stoi kolejny artykuł, nim nie jest
                // (podpis pod obwieszczeniem przed tekstem jednolitym).
                context.Document.Signature = null;

                var result = _articleBuilder.Build(new ArticleBuildInput(context.Subchapter, text));
                ValidationReporter.AddClassificationWarning(result.Article, classification, "ART");
                context.CurrentArticle = result.Article;
                context.CurrentParagraph = result.Paragraph;
                context.CurrentPoint = null;
                context.CurrentLetter = null;
                context.ClearTiretStack();

                UpdateStructuralReference(context, result.Article);
                if (result.Paragraph != null)
                {
                    if (!result.Paragraph.IsImplicit)
                        UpdateStructuralReference(context, result.Paragraph);
                    DetectAmendmentTargets(context, result.Paragraph);
                }
                _journalReferenceService.ParseJournalReferences(result.Article);
                return true;
            }

            // Sankcja karna w osobnym akapicie („podlega grzywnie…") domyka zdanie przepisu.
            if (context.CurrentArticle != null && TryHandleSanction(context, text, sourceStyleId))
                return true;

            // Nierozpoznany — inference-first, potem diagnostyka. Tytuł jednostki bez oczekującej
            // jednostki idzie tą samą drogą.
            if (IsUnknownOrHeading(classification.Kind))
            {
                HandleUnknown(context, text, sourceStyleId, layout);
                return true; // zawsze "skonsumowany"
            }

            if (context.CurrentArticle == null)
            {
                context.Metadata.Seal();

                Log.Warning("Akapit {Kind} poza kontekstem artykułu (metadane lub pre-art.) — pominięty. Styl: {StyleId}",
                    classification.Kind, sourceStyleId);
                return false;
            }

            switch (classification.Kind)
            {
                case ParagraphKind.Paragraph:
                    context.CurrentParagraph = _paragraphBuilder.Build(
                        new ParagraphBuildInput(context.CurrentArticle, context.CurrentParagraph, text));
                    ValidationReporter.AddClassificationWarning(context.CurrentParagraph, classification, "UST");
                    context.CurrentPoint = null;
                    context.CurrentLetter = null;
                    context.ClearTiretStack();

                    UpdateStructuralReference(context, context.CurrentParagraph);
                    DetectAmendmentTargets(context, context.CurrentParagraph);
                    return true;

                case ParagraphKind.Point:
                    var ensuredParagraph = _paragraphBuilder.EnsureForPoint(context.CurrentArticle, context.CurrentParagraph);
                    context.CurrentParagraph = ensuredParagraph.Paragraph;

                    // Wiąż intro z segmentem rodzica przed dodaniem pierwszego punktu
                    if (context.CurrentParagraph.Points.Count == 0)
                        ParsingFactories.AttachIntroCommonPart(context.CurrentParagraph);

                    CommonPartPlacement.BeforeNextItem(context.CurrentParagraph, context.CurrentParagraph.Points);

                    context.CurrentPoint = _pointBuilder.Build(
                        new PointBuildInput(context.CurrentParagraph, context.CurrentArticle, text));
                    ValidationReporter.AddClassificationWarning(context.CurrentPoint, classification, "PKT");
                    context.CurrentLetter = null;
                    context.ClearTiretStack();

                    UpdateStructuralReference(context, context.CurrentPoint);
                    DetectAmendmentTargets(context, context.CurrentPoint);
                    return true;

                case ParagraphKind.Letter:
                    var ensuredPoint = _pointBuilder.EnsureForLetter(context.CurrentParagraph, context.CurrentArticle, context.CurrentPoint);
                    context.CurrentPoint = ensuredPoint.Point;
                    if (ensuredPoint.CreatedImplicit)
                    {
                        ValidationReporter.AddValidationMessage(context.CurrentPoint, ValidationLevel.Warning,
                            "Brak jawnego punktu; utworzono niejawny punkt na podstawie struktury.");
                    }

                    // Wiąż intro z segmentem rodzica przed dodaniem pierwszej litery
                    if (context.CurrentPoint.Letters.Count == 0)
                        ParsingFactories.AttachIntroCommonPart(ExplicitOwner(context.CurrentPoint));

                    CommonPartPlacement.BeforeNextItem(context.CurrentPoint, context.CurrentPoint.Letters);

                    context.CurrentLetter = _letterBuilder.Build(
                        new LetterBuildInput(context.CurrentPoint, context.CurrentParagraph, context.CurrentArticle, text));
                    ValidationReporter.AddClassificationWarning(context.CurrentLetter, classification, "LIT");
                    context.ClearTiretStack();

                    UpdateStructuralReference(context, context.CurrentLetter);
                    DetectAmendmentTargets(context, context.CurrentLetter);
                    return true;

                case ParagraphKind.Tiret:
                    if (TryHandleStylelessWrapUp(context, classification, text, sourceStyleId))
                        return true;

                    var ensuredPointForTiret = _pointBuilder.EnsureForLetter(context.CurrentParagraph, context.CurrentArticle, context.CurrentPoint);
                    context.CurrentPoint = ensuredPointForTiret.Point;
                    if (ensuredPointForTiret.CreatedImplicit)
                    {
                        ValidationReporter.AddValidationMessage(context.CurrentPoint, ValidationLevel.Warning,
                            "Brak jawnego punktu; utworzono niejawny punkt na podstawie struktury.");
                    }

                    var ensuredLetter = _letterBuilder.EnsureForTiret(
                        context.CurrentPoint, context.CurrentParagraph, context.CurrentArticle, context.CurrentLetter);
                    context.CurrentLetter = ensuredLetter.Letter;
                    if (ensuredLetter.CreatedImplicit)
                    {
                        ValidationReporter.AddValidationMessage(context.CurrentLetter, ValidationLevel.Warning,
                            "Brak jawnej litery; utworzono niejawna litere na podstawie struktury.");
                    }

                    // Wiąż intro z segmentem rodzica przed dodaniem pierwszego tiretu
                    if (context.CurrentLetter.Tirets.Count == 0)
                        ParsingFactories.AttachIntroCommonPart(ExplicitOwner(context.CurrentLetter));

                    var writtenDepth = TiretMarker.Depth(text);
                    var tiretDepth = GetTiretDepth(writtenDepth, sourceStyleId, layout, context);

                    // Skroc stos do glebokosci depth-1 (usun tirety glebsze lub rowne)
                    context.PopTiretsToDepth(tiretDepth);

                    // Straznik: przy glebokosci > 1 rodzic musi istniec; inaczej degradacja do poziomu 1
                    // (np. 2TIR/wciecie bez poprzedzajacego tiretu — zamiast wyjatku).
                    var parentTiret = tiretDepth > 1 && context.TiretStack.Count > 0 ? context.TiretStack[^1] : null;

                    // Intro wspolna - tylko dla pierwszego dziecka na danym poziomie
                    if (parentTiret != null && parentTiret.Tirets.Count == 0)
                        ParsingFactories.AttachIntroCommonPart(parentTiret);

                    var tiretIndex = parentTiret != null
                        ? parentTiret.Tirets.Count + 1
                        : context.CurrentLetter.Tirets.Count + 1;

                    var tiret = _tiretBuilder.Build(new TiretBuildInput(
                        context.CurrentLetter, context.CurrentPoint, context.CurrentParagraph,
                        context.CurrentArticle, text, tiretIndex, parentTiret));
                    ValidationReporter.AddClassificationWarning(tiret, classification, "TIR");

                    // Gdy zagniezdzenie wywnioskowano z wciecia (a nie ze stylu 2TIR/3TIR) — audyt decyzji (§ 58 ZTP).
                    if (parentTiret != null && writtenDepth < 2 && !StyleEncodesTiretDepth(sourceStyleId))
                        ValidationReporter.AddValidationMessage(tiret, ValidationLevel.Info,
                            $"Glebokosc tiretu ({tiretDepth}) ustalona z wciecia (§ 58 ZTP).");
                    // Degradacja: sklasyfikowano na poziom > 1, ale brak tiretu nadrzednego — slad diagnostyczny.
                    else if (tiretDepth > 1 && parentTiret == null)
                        ValidationReporter.AddValidationMessage(tiret, ValidationLevel.Warning,
                            $"Tiret sklasyfikowany na poziom {tiretDepth}, ale brak tiretu nadrzednego — umieszczono na poziomie 1.");

                    // Tirety o glebokosci ustalonej ze STYLU nie wnosza wciecia jako punktu odniesienia dla
                    // wnioskowania (ich wciecie moze byc null lub sprzeczne z narzucona stylem glebokoscia) —
                    // inaczej mieszanie trybu stylowego i wcieciowego w obrebie litery falszowaloby zagniezdzenie.
                    context.PushTiret(tiret, writtenDepth > 1 || StyleDecidesTiretDepth(sourceStyleId) ? null : layout?.LeftIndentTwips);

                    UpdateStructuralReference(context, tiret);
                    DetectAmendmentTargets(context, tiret);
                    return true;

                case ParagraphKind.WrapUp:
                    TryHandleWrapUp(context, text, sourceStyleId);
                    return true;

                default:
                    Log.Warning("Nieobsługiwany ParagraphKind {Kind} w switch — pominięty (styl={StyleId})",
                        classification.Kind, sourceStyleId);
                    return false;
            }
        }

        // ============================================================
        // Metody pomocnicze
        // ============================================================

        /// <summary>
        /// Obsługuje akapit Unknown: najpierw próbuje wywnioskować rodzaj z treści,
        /// a gdy brak wzorca — dołącza ValidationMessage do najgłębszej aktywnej encji.
        /// </summary>
        /// <summary>Początek komunikatu walidacji o akapicie, którego potok nie rozpoznał i nie wbudował.</summary>
        internal const string SkippedParagraphMessage = "Pominięty akapit nierozpoznany";

        private void HandleUnknown(ParsingContext context, string text, string? sourceStyleId, BlockLayoutInfo? layout)
        {
            // Krok 1: wywnioskuj z treści (ochrona przed edge cases LayeredClassifier)
            if (context.CurrentArticle != null)
            {
                var inferredKind = TryInferKindFromText(text);
                if (inferredKind != ParagraphKind.Unknown)
                {
                    Log.Warning("Akapit Unknown — wywnioskowany jako {Kind} z wzorca tekstowego " +
                        "(styl: {StyleId}): {Text}",
                        inferredKind, sourceStyleId,
                        text.Length > 60 ? text[..60] + "…" : text);

                    var rescued = new ClassificationResult
                    {
                        Kind = inferredKind,
                        Confidence = 50,
                        Penalties =
                        [
                            new ClassificationPenalty
                            {
                                Reason = "Wywnioskowano z wzorca tekstowego po braku klasyfikacji",
                                Value  = 50,
                            },
                        ],
                    };
                    Process(context, rescued, text, sourceStyleId, layout);
                    return;
                }
            }

            // Krok 2: brak wzorca — diagnostyka bez encji
            if (context.CurrentArticle == null)
            {
                // Strefa tytułowa (§ 16-19): spróbuj wychwycić metadane aktu (rodzaj/data/przedmiot).
                if (context.Metadata.Observe(text))
                    return;

                // Podstawa prawna aktu wykonawczego (§ 121) stoi między tytułem a pierwszym paragrafem.
                if (context.Document.LegalBasis.Length == 0 && ZtpPatterns.LegalBasisPattern.IsMatch(text))
                {
                    context.Document.LegalBasis = text;
                    return;
                }

                Log.Warning("Akapit Unknown przed pierwszym artykułem (metadane) — pominięty. " +
                    "Styl: {StyleId}, Tekst: {Text}",
                    sourceStyleId, text.Length > 80 ? text[..80] + "…" : text);
                return;
            }

            // Formuła podpisu za ostatnim przepisem („Prezydent Rzeczypospolitej Polskiej: A. Duda").
            if (ActSignatureParser.TryParse(text) is { } signature)
            {
                context.Document.Signature = signature;
                return;
            }

            var deepest = DeepestUnit(context);
            var preview = text.Length > 80 ? text[..80] + "…" : text;

            // Akapit bez oznaczenia należy do jednostki, za którą stoi: zostaje jej dalszą treścią, nie ginie.
            if (deepest is not null && !IsActHeadingLine(text, sourceStyleId))
            {
                deepest.Continuation.Add(new ContentBlock { Text = text });
                deepest.ValidationMessages.Add(new ValidationMessage(
                    ValidationLevel.Info,
                    $"Akapit bez oznaczenia dołączony jako dalsza treść jednostki (styl: '{sourceStyleId ?? "brak"}'). Tekst: '{preview}'"));
                return;
            }

            deepest?.ValidationMessages.Add(new ValidationMessage(
                ValidationLevel.Warning,
                $"{SkippedParagraphMessage} (styl: '{sourceStyleId ?? "brak"}')." +
                $" Tekst: '{preview}'"));

            Log.Warning("Akapit Unknown pominięty — brak dopasowania do wzorca. " +
                "Styl: {StyleId}, Tekst: {Text}",
                sourceStyleId, preview);
        }

        /// <summary>Jednostka, w której treści stoi bieżące miejsce dokumentu — najgłębsza otwarta.</summary>
        internal static BaseEntity? DeepestUnit(ParsingContext context) =>
            (BaseEntity?)context.CurrentTiret
            ?? (BaseEntity?)context.CurrentLetter
            ?? (BaseEntity?)context.CurrentPoint
            ?? (BaseEntity?)context.CurrentParagraph
            ?? context.CurrentArticle;

        // Style strefy tytułowej aktu i wskazania załącznika.
        private static readonly string[] ActHeadingStyles = ["OZNRODZAKTU", "DATAAKTU", "TYTUAKTU", "TEKSTZa"];

        /// <summary>
        /// Wiersz strefy tytułowej aktu w środku dokumentu: tekst jednolity zaczyna się za przepisami
        /// obwieszczenia („Załącznik do obwieszczenia…", „USTAWA", „z dnia …", tytuł). To początek innego aktu,
        /// nie dalsza treść ostatniego przepisu.
        /// </summary>
        private static bool IsActHeadingLine(string text, string? styleId) =>
            (styleId is not null && ActHeadingStyles.Any(prefix => styleId.StartsWith(prefix, StringComparison.Ordinal)))
            || AnnouncementBoundary.AnnexHeader.IsMatch(text)
            || ZtpPatterns.StatuteHeaderPattern.IsMatch(text)
            || ZtpPatterns.RegulationHeaderPattern.IsMatch(text)
            || ZtpPatterns.AnnouncementHeaderPattern.IsMatch(text)
            || ZtpPatterns.ActDateLinePattern.IsMatch(text);

        private static ParagraphKind TryInferKindFromText(string text)
        {
            if (ParagraphClassifier.IsParagraphByText(text)) return ParagraphKind.Paragraph;
            if (ParagraphClassifier.IsPointByText(text)) return ParagraphKind.Point;
            if (ParagraphClassifier.IsLetterByText(text)) return ParagraphKind.Letter;
            if (ParagraphClassifier.IsTiretByText(text)) return ParagraphKind.Tiret;
            return ParagraphKind.Unknown;
        }

        // ============================================================
        // Jednostki systematyzacyjne (§ 60-62 ZTP)
        // ============================================================

        private static bool IsUnknownOrHeading(ParagraphKind kind) =>
            kind is ParagraphKind.Unknown or ParagraphKind.UnitHeading;

        /// <summary>Tytuł bez stylu dłuższy niż 99% tytułów w korpusie tekstów jednolitych (p99 = 183 znaki).</summary>
        private const int MaxUnstyledHeadingLength = 200;

        /// <summary>„(uchylony)" w miejscu tytułu jednostki uchylonej w tekście jednolitym (§ 106 ZTP).</summary>
        private static readonly Regex RepealedUnitMarker = new(
            @"^\(\s*(?:uchylon[yae]|pominięt[yae])\s*\)(?:\[[^\]]*\])?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Tytuł ze stylu szablonu: styl tytułu jednostki albo — dla części i księgi — ten sam styl
        /// CZĘŚĆ(KSIĘGA), którym zapisano oznaczenie, bez oznaczenia w treści.
        /// </summary>
        private static bool IsStyledHeading(ClassificationResult classification, ISystematizingUnit pendingUnit, string text) =>
            classification.Kind == ParagraphKind.UnitHeading
            || (classification.Kind == ParagraphKind.Unknown
                && classification.StyleType == ParagraphClassifier.PartOrBookStyle
                && pendingUnit is Part or Book)
            // Część nazwana zaraz po oznaczeniu niższej jednostki to jej tytuł: „KSIĘGA PIERWSZA" /
            // „CZĘŚĆ OGÓLNA" (KC), „DZIAŁ I" / „Część ogólna" (KKS).
            || (classification.Kind == ParagraphKind.PartUnit && pendingUnit is not Part
                && ParagraphClassifier.ParseSystematizingNumber(ParagraphKind.PartUnit, text)?.Value
                    == ParagraphClassifier.NamedUnitValue);

        internal static bool IsSystematizing(ParagraphKind kind) => kind is
            ParagraphKind.PartUnit or ParagraphKind.BookUnit or ParagraphKind.TitleUnit or
            ParagraphKind.DivisionUnit or ParagraphKind.ChapterUnit or ParagraphKind.SubchapterUnit;

        private void HandleSystematizingUnit(ParsingContext context, ClassificationResult classification,
            string text, string? sourceStyleId)
        {
            var number = ParagraphClassifier.ParseSystematizingNumber(classification.Kind, text);
            if (number == null)
            {
                // MatchSystematizingRegex waliduje numer, więc tu nie powinniśmy trafić — defensywnie.
                Log.Warning("Jednostka systematyzacyjna {Kind} bez rozpoznanego numeru (styl={StyleId}): {Text}",
                    classification.Kind, sourceStyleId, text);
                return;
            }

            if (number.Value == ParagraphClassifier.NamedUnitValue)
            {
                var ordinal = context.NamedUnitCounts.GetValueOrDefault(classification.Kind) + 1;
                context.NamedUnitCounts[classification.Kind] = ordinal;
                number.Value = $"{ParagraphClassifier.NamedUnitValue}{ordinal}";
            }

            _systematizingUnitBuilder.Enter(context, classification.Kind, number);
            Log.Debug("Wejście w jednostkę systematyzacyjną {Kind} {Number}", classification.Kind, number.Value);

            if (context.PendingHeadingUnit is BaseEntity named
                && number.Value.StartsWith(ParagraphClassifier.NamedUnitValue, StringComparison.Ordinal))
                ValidationReporter.AddValidationMessage(named, ValidationLevel.Warning,
                    $"Jednostka nazwana bez numeru („{number.RawValue}”) — eId tymczasowy {named.Id}.");

            if (context.PendingHeadingUnit is BaseEntity entered
                && ParagraphClassifier.ToleratedDesignationReason(classification.Kind, text, number) is { } reason)
            {
                ValidationReporter.AddValidationMessage(entered, ValidationLevel.Info, $"Odczyt tolerancyjny: {reason}");
                Log.Information("Odczyt tolerancyjny oznaczenia „{Text}\": {Reason}", text, reason);
            }
        }

        /// <summary>
        /// Czy akapit to prawdopodobny tytuł jednostki (§ 60): krótki, rozpoczyna się wielką literą
        /// i NIE kończy się kropką (tytuł jednostki nie ma kropki końcowej; zdanie treści ma) — chroni
        /// przed pochłonięciem realnej treści jako tytułu jednostki bez tytułu.
        /// </summary>
        private static bool IsHeadingCandidate(string text)
        {
            var t = text.Trim();
            if (t.Length == 0 || t.Length > MaxUnstyledHeadingLength)
                return false;
            if (!char.IsLetter(t[0]) || !char.IsUpper(t[0]))
                return false;
            return t[^1] != '.';
        }

        /// <summary>
        /// Aktualizuje bieżącą pozycję strukturalną w kontekście na podstawie
        /// numeru zbudowanej encji. Ustawienie poziomu resetuje podrzędne
        /// (np. WithArticle zeruje ust/pkt/lit/tir), aby zachować spójne eId.
        /// </summary>
        private static void UpdateStructuralReference(ParsingContext context, BaseEntity entity)
        {
            var numberValue = entity.Number?.Value;
            if (string.IsNullOrEmpty(numberValue))
                return;

            context.CurrentStructuralReference = entity.UnitType switch
            {
                UnitType.Article => context.CurrentStructuralReference.WithArticle(numberValue),
                UnitType.Paragraph => entity is DtoParagraph { Role: DtoParagraph.CodeParagraphRole }
                    ? context.CurrentStructuralReference.WithCodeParagraph(numberValue)
                    : context.CurrentStructuralReference.WithParagraph(numberValue),
                UnitType.Point => context.CurrentStructuralReference.WithPoint(numberValue),
                UnitType.Letter => context.CurrentStructuralReference.WithLetter(numberValue),
                UnitType.Tiret => context.CurrentStructuralReference.WithTiret(numberValue),
                _ => context.CurrentStructuralReference
            };
        }

        /// <summary>
        /// Wykrywa cele nowelizacji w treści encji implementującej IHasAmendments.
        /// Parsuje wzorce takie jak "w art. 5", "po ust. 2", "ust. 2 i 4" itp.
        /// Kontekst jest dziedziczony z encji nadrzędnych (kumulacja referencji rozproszonych po poziomach).
        /// </summary>
        private static void DetectAmendmentTargets(ParsingContext context, BaseEntity entity)
        {
            if (entity is not IHasAmendments)
                return;

            if (string.IsNullOrWhiteSpace(entity.ContentText))
                return;

            // Zacznij od kontekstu rodzica (jeśli istnieje wykryty cel w encji nadrzędnej)
            var parentTargets = FindParentAmendmentTargets(context, entity);
            var targets = context.ReferenceService.ReadAmendmentTargets(
                parentTargets?[0].Structure, entity.ContentText,
                SectionSignRole.IsBasicUnit(context.Document.Classification));

            if (targets.Count == 0 && parentTargets is not null)
            {
                targets = parentTargets
                    .Select(parent => new StructuralAmendmentReference
                    {
                        Structure = parent.Structure.Clone(),
                        RangeEnd = parent.RangeEnd?.Clone(),
                        RawText = entity.ContentText,
                    })
                    .ToList();
            }

            if (targets.Count == 0)
                return;

            context.DetectedAmendmentTargets[entity.Guid] = targets;

            Log.Debug("Wykryto cele nowelizacji w {UnitType} [{EntityId}]: {AmendmentTargets}",
                entity.UnitType, entity.Id, string.Join(", ", targets));
        }

        /// <summary>
        /// Przeszukuje encje nadrzędne w hierarchii (Parent), szukając wcześniej
        /// wykrytych celów nowelizacji, których kontekst może być odziedziczony.
        /// </summary>
        private static IReadOnlyList<StructuralAmendmentReference>? FindParentAmendmentTargets(ParsingContext context, BaseEntity entity)
        {
            var current = entity.Parent;
            while (current != null)
            {
                if (context.DetectedAmendmentTargets.TryGetValue(current.Guid, out var parentTargets))
                    return parentTargets;
                current = current.Parent;
            }
            return null;
        }

        /// <summary>Tolerancja wciecia (twips, ~6 pt) przy porownywaniu poziomow tiretow (§ 58 ZTP).</summary>
        private const int TiretIndentTolerance = 120;

        /// <summary>Maks. glebokosc tiretu odwzorowana w modelu/stylach (TIR/2TIR/3TIR).</summary>
        private const int MaxTiretDepth = 3;

        /// <summary>
        /// Wyznacza glebokosc tiretu. Priorytet:
        /// 1) znacznik zapisany w tresci („– –", „– – –", <see cref="TiretMarker"/>) — zagniezdzenie stoi w samym tekscie;
        /// 2) styl jawnie kodujacy glebokosc (2TIR/3TIR/TIR);
        /// 3) wciecie lewe bloku wzgledem tiretow otwartych na stosie (§ 58 ZTP) — dla dokumentow bezstylowych z ukladem;
        /// 4) brak sygnalu — poziom 1.
        /// </summary>
        private static int GetTiretDepth(int writtenDepth, string? styleId, BlockLayoutInfo? layout, ParsingContext context)
        {
            // Zagnieżdżenie zapisane znacznikiem („– –") stoi w treści — rozstrzyga przed stylem i wcięciem.
            if (writtenDepth > 1)
                return System.Math.Min(writtenDepth, MaxTiretDepth);

            if (!string.IsNullOrEmpty(styleId))
            {
                if (styleId.StartsWith("3TIR", StringComparison.OrdinalIgnoreCase)) return 3;
                if (styleId.StartsWith("2TIR", StringComparison.OrdinalIgnoreCase)) return 2;
                if (styleId.StartsWith("TIR", StringComparison.OrdinalIgnoreCase)) return 1;
            }

            if (layout?.LeftIndentTwips is int curIndent)
            {
                var inferred = InferTiretDepthFromIndent(curIndent, context.OpenTiretIndents);
                if (inferred != null) return inferred.Value;
            }

            return 1;
        }

        /// <summary>Czy styl jawnie koduje NIEZEROWA glebokosc zagniezdzenia tiretu (2TIR/3TIR).</summary>
        private static bool StyleEncodesTiretDepth(string? styleId) =>
            !string.IsNullOrEmpty(styleId) &&
            (styleId.StartsWith("2TIR", StringComparison.OrdinalIgnoreCase) ||
             styleId.StartsWith("3TIR", StringComparison.OrdinalIgnoreCase));

        /// <summary>Czy to glebokosc tiretu rozstrzyga STYL (TIR/2TIR/3TIR), a nie wciecie.</summary>
        private static bool StyleDecidesTiretDepth(string? styleId) =>
            (!string.IsNullOrEmpty(styleId) && styleId.StartsWith("TIR", StringComparison.OrdinalIgnoreCase))
            || StyleEncodesTiretDepth(styleId);

        /// <summary>
        /// Wnioskuje glebokosc tiretu z wciecia lewego wzgledem wciec tiretow otwartych na stosie.
        /// Wieksze wciecie (o > tolerancje) niz najglebszy ZNANY punkt odniesienia = dziecko biezacego szczytu
        /// stosu; wciecie w tolerancji = rodzenstwo tego poziomu; mniejsze = rodzenstwo plytszego poziomu.
        /// Zwraca null, gdy brak punktu odniesienia (stos pusty lub same wciecia null) — wtedy wywolujacy
        /// przyjmuje poziom 1. UWAGA (ograniczenie ekstrakcji ukladu): LeftIndentTwips pochodzi wylacznie
        /// z bezposredniego w:left akapitu (DocxBlockReader), nie z wciec dziedziczonych ze stylu/numeracji;
        /// tiret z wcieciem dziedziczonym trafia tu jako null i domyslnie ląduje na poziomie 1 (Etap 9/PDF: geometria).
        /// </summary>
        private static int? InferTiretDepthFromIndent(int curIndent, IReadOnlyList<int?> openIndents)
        {
            int deepest = openIndents.Count - 1;
            while (deepest >= 0 && openIndents[deepest] == null) deepest--;
            if (deepest < 0) return null;

            int refIndent = openIndents[deepest]!.Value;

            // Glebiej niz najglebszy ZNANY punkt odniesienia → dziecko RZECZYWISTEGO szczytu stosu.
            // Glebokosc liczymy od pelnej wysokosci stosu (openIndents.Count), nie od poziomu odniesienia,
            // bo miedzy nim a szczytem moga stac tirety o wcieciu null (styl 2TIR/3TIR) — inaczej zaniżalibysmy.
            if (curIndent > refIndent + TiretIndentTolerance)
                return System.Math.Min(openIndents.Count + 1, MaxTiretDepth);

            // W przeciwnym razie znajdz najglebszy poziom, ktorego wciecie nie jest wieksze od biezacego
            // (w tolerancji) — to poziom-rodzenstwo biezacego tiretu.
            for (int d = deepest + 1; d >= 1; d--)
            {
                var ind = openIndents[d - 1];
                if (ind == null) continue;
                if (curIndent >= ind.Value - TiretIndentTolerance)
                    return d;
            }
            return 1;
        }

        private static bool IsWrapUpByText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var trimmed = text.TrimStart();
            if (trimmed.Length < 2)
                return false;

            var first = trimmed[0];
            if (first != '\u2013' && first != '-')
                return false;

            return char.IsWhiteSpace(trimmed[1]);
        }

        private static bool TryGetWrapUpTarget(string? styleId, out AmendmentTargetKind targetKind)
        {
            targetKind = AmendmentTargetKind.Unknown;
            if (!StyleLibraryMapper.TryGetStyleInfo(styleId, out var info) || info == null)
                return false;

            var name = info.DisplayName;
            if (name.StartsWith("CZ_WSP_PKT \u2013", StringComparison.OrdinalIgnoreCase))
            {
                targetKind = AmendmentTargetKind.Point;
                return true;
            }

            if (name.StartsWith("CZ_WSP_LIT \u2013", StringComparison.OrdinalIgnoreCase))
            {
                targetKind = AmendmentTargetKind.Letter;
                return true;
            }

            if (name.StartsWith("CZ_WSP_TIR \u2013", StringComparison.OrdinalIgnoreCase))
            {
                targetKind = AmendmentTargetKind.Tiret;
                return true;
            }

            return false;
        }
    }
}
