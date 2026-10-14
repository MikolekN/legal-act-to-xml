using Saga.Core.Services.Classify;
using Saga.Core.Services.Parsing.Builders;
using Saga.Model;
using Serilog;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Zarządza cyklem życia nowelizacji w trakcie parsowania:
    /// aktualizuje flagi stanu (InsideAmendment, AmendmentTriggerDetected),
    /// zbiera akapity nowelizacji i finalizuje je do obiektów Amendment.
    /// </summary>
    internal sealed class AmendmentStateManager
    {
        private readonly AmendmentBuilder _amendmentBuilder = new();
        private readonly AmendmentFinalizer _amendmentFinalizer = new();

        /// <summary>
        /// Komendy wyrazowe § 87 ust. 3 pkt 2-3 ZTP: „skreśla się wyrazy „X"" / „dodaje się wyrazy „Y""
        /// (po wskazanych wyrazach). Operują na wyrazach, nie jednostkach — bez tego strażnika
        /// „skreśla się wyrazy" tworzyłoby FAŁSZYWE uchylenie całej jednostki (RepealPattern zna „skreśla się").
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex WordLevelCommandPattern = new(
            @"(?:uchyla|skreśla|dodaje)\s+się\s+wyraz",
            System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        /// <summary>
        /// Aktualizuje stan nowelizacji w kontekście na podstawie wyniku klasyfikacji bieżącego akapitu.
        /// Logika oparta na stylach:
        /// - Styl Z/... → zawsze nowelizacja
        /// - Rozpoznany styl ustawy matki (ART/UST/PKT/LIT/TIR) → wyjście z nowelizacji,
        ///   CHYBA ŻE aktywny trigger + akapit nie zawiera własnego triggera → treść nowelizacji
        /// - Brak stylu + trigger → wejście w nowelizację
        /// - Brak stylu + już w nowelizacji → pozostaje w nowelizacji
        /// - Brak stylu + cudzysłów otwierający po komendzie bez dwukropka → wejście w nowelizację
        /// </summary>
        public void UpdateState(ParsingContext context, ClassificationResult classification, string text)
        {
            // 1. Styl Z/... → zawsze nowelizacja
            // 0. Przytoczenie otwarte pod przepisem, który niczego nie zmienia: trwa do domknięcia cudzysłowu, bez względu
            //    na styl akapitów — kończy je najwyżej kolejny artykuł, bo cytat nie przechodzi przez artykuły (ADR-0076).
            if (context.InProvisionQuote)
            {
                if (classification.Kind == ParagraphKind.Article && !QuoteBalanceTracker.OpensQuote(text))
                {
                    EndProvisionQuote(context);
                    context.InsideAmendment = false;
                    return;
                }

                context.ProvisionQuoteDepth = QuoteDepthAfter(context.ProvisionQuoteDepth, text);
                return;
            }

            if (OpensProvisionQuote(context, text))
            {
                context.InsideAmendment = true;
                context.InProvisionQuote = true;
                context.ProvisionQuoteDepth = QuoteDepthAfter(0, text);
                return;
            }

            if (classification.IsAmendmentContent)
            {
                context.InsideAmendment = true;
                context.AmendmentTriggerDetected = false;
                return;
            }

            // 2. Rozpoznany styl ustawy matki
            if (classification.HasParentLawStyle)
            {
                // 2a. Aktywny trigger + akapit BEZ własnego zwrotu nowelizacyjnego
                //     → akapit jest treścią nowelizacji (np. "1) nowe brzmienie pkt 1" z stylem PKT).
                //     Bez tej reguły styl ustawy matki błędnie czyściłby trigger i gubił treść.
                if (context.AmendmentTriggerDetected)
                {
                    bool startsNewAmendment = AmendmentFinalizer.ModificationPattern.IsMatch(text)
                        || AmendmentFinalizer.RepealPattern.IsMatch(text);

                    if (!startsNewAmendment)
                    {
                        context.InsideAmendment = true;
                        context.AmendmentTriggerDetected = false;
                        Log.Debug("Wejscie w nowelizacje po triggerze (styl ustawy matki {Style} bez wlasnego triggera)",
                            classification.StyleType);
                        return;
                    }
                    // else: akapit zawiera własny trigger → traktuj jak nowy element ustawy matki (fall-through)
                }

                if (context.InsideAmendment)
                {
                    // 2b. Cudzysłów cytatu wciąż otwarty: akapit w stylu ustawy matki jest treścią cytowaną — styl bywa
                    //     pomylony („„Wariant D" i „1. Wykształcenie …" w stylu punktu), a o granicy cytatu mówi
                    //     cudzysłów. Cytat, który literówka zostawiła otwarty, kończy jak dotąd akapit z komendą albo
                    //     kolejna jednostka aktu zmieniającego.
                    if (context.AmendmentCollector.LeavesQuoteOpen
                        && !StartsCommand(text)
                        && !ContinuesTheAct(context, classification, text))
                    {
                        Log.Debug("Cytat otwarty — akapit w stylu ustawy matki {Style} jest treścią cytowaną", classification.StyleType);
                        return;
                    }

                    Log.Debug("Zamknieto nowelizacje (styl ustawy matki: {Style})", classification.StyleType);
                    context.InsideAmendment = false;
                }
                // Trigger czyszczony — nowy zostanie ustawiony PO przetworzeniu tego akapitu, jeśli zawiera zwrot.
                context.AmendmentTriggerDetected = false;
                context.AmendmentOwner = null;
                return;
            }

            // 3. Brak rozpoznanego stylu ustawy matki
            if (context.AmendmentTriggerDetected)
            {
                // Po triggerze napotkano akapit bez stylu → to treść nowelizacji
                context.InsideAmendment = true;
                context.AmendmentTriggerDetected = false;
                Log.Debug("Wejscie w nowelizacje po triggerze (brak stylu ustawy matki)");
                return;
            }

            // 4. Brak stylu + już w nowelizacji → pozostaje w nowelizacji

            // 5. Komenda bez dwukropka („ust. 1 otrzymuje brzmienie") i blok w cudzysłowie otwierającym:
            //    to treść cytowana, a nie jednostka aktu — przepis aktu nie zaczyna się od „. Bez tej reguły
            //    „„1. …" stawało się ustępem artykułu zmieniającego. Sam cudzysłów nie wystarcza: obwieszczenie
            //    tekstu jednolitego przytacza całe artykuły („…, które stanowią:") i nie jest to nowelizacja.
            if (!context.InsideAmendment
                && QuoteBalanceTracker.OpensQuote(text)
                && GetOwner(context) is { } owner
                && AmendmentFinalizer.ModificationWithoutColonPattern.IsMatch(owner.ContentText))
            {
                context.InsideAmendment = true;
                Log.Warning("Komenda nowelizacyjna bez dwukropka — blok w cudzysłowie przyjęty jako treść cytowana: {Text}", text);
                ValidationReporter.AddValidationMessage(owner, ValidationLevel.Warning,
                    "Komenda nowelizacyjna bez dwukropka — treść w cudzysłowie przyjęta jako cytowana.");
                return;
            }

            // 6. Brak stylu + normalny tryb → przetwarzane normalnie (z fallback warning)
        }

        /// <summary>
        /// Akapit jest kolejną jednostką aktu zmieniającego: numer następuje po otwartej jednostce tego samego rodzaju
        /// („3)" po punkcie 2), tiret stoi przy otwartym tirecie, a artykuł — zawsze, bo cytat nie przechodzi przez artykuły.
        /// </summary>
        private static bool ContinuesTheAct(ParsingContext context, ClassificationResult classification, string text)
        {
            if (classification.Kind == ParagraphKind.Article)
                return true;
            if (classification.Kind == ParagraphKind.Tiret)
                return context.CurrentTiret is not null;

            var open = classification.Kind switch
            {
                ParagraphKind.Paragraph => context.CurrentParagraph?.Number,
                ParagraphKind.Point => context.CurrentPoint?.Number,
                ParagraphKind.Letter => context.CurrentLetter?.Number,
                _ => null,
            };

            return open is not null
                && ParagraphClassifier.ParseNumberForKind(classification.Kind, text) is { } number
                && new NumberingHint { ExpectedKind = classification.Kind, ExpectedNumber = open }.IsContinuous(number);
        }

        /// <summary>Akapit zawiera komendę — zmianę brzmienia, uchylenie albo zmianę wyrazów.</summary>
        private static bool StartsCommand(string text) =>
            AmendmentFinalizer.ModificationPattern.IsMatch(text) ||
            AmendmentFinalizer.RepealPattern.IsMatch(text) ||
            AmendmentCommandParser.ReplaceWordsPattern.IsMatch(text) ||
            AmendmentCommandParser.AddWordsPattern.IsMatch(text);

        /// <summary>
        /// Zwraca true, gdy podczas nowelizacji pojawia się akapit z triggerem
        /// nowego punktu/ustępu bez stylu ustawy matki — sygnał zamknięcia bieżącej nowelizacji.
        /// </summary>
        public bool ShouldExitForNewParentLawTrigger(
            ParsingContext context,
            ClassificationResult classification,
            string text)
        {
            if (!context.InsideAmendment)
                return false;

            if (classification.IsAmendmentContent || classification.HasParentLawStyle)
                return false;

            if (classification.Kind == ParagraphKind.Unknown)
                return false;

            // ReplaceWords („wyrazy … zastępuje się wyrazami …") to od Etapu 8 pełnoprawna komenda —
            // nowy punkt ustawy matki z taką komendą również zamyka bieżącą nowelizację (symetria z DetectTrigger).
            return StartsCommand(text);
        }

        /// <summary>
        /// Zbiera akapit nowelizacji do bufora. Jeśli kolektor nie jest jeszcze uruchomiony,
        /// rozpoczyna zbieranie z odpowiednim właścicielem i celem.
        /// </summary>
        public void Collect(ParsingContext context, string text, string? styleId)
        {
            if (!context.AmendmentCollector.IsCollecting)
            {
                var owner = context.AmendmentOwner ?? GetOwner(context);
                if (owner != null)
                {
                    var targets = context.DetectedAmendmentTargets.GetValueOrDefault(owner.Guid);
                    context.AmendmentCollector.Begin(owner, targets);
                }
            }

            context.AmendmentCollector.AddParagraph(text, styleId, context.CurrentFormula, context.CurrentSymbol);
        }

        /// <summary>
        /// Zbiera tabelę stojącą w treści nowelizacji. Tabela tuż po komendzie („…w brzmieniu:") jest całą
        /// treścią cytowaną i nowelizację zamyka jej koniec — chyba że sama otwiera cudzysłów i go nie zamyka:
        /// cytat biegnie wtedy dalej (objaśnienia pod tabelą) aż do cudzysłowu zamykającego.
        /// Tabela w środku treści cytowanej należy do jednostki, za którą stoi — zbieranie trwa, dopóki
        /// cudzysłów w jej ostatniej komórce nie domknie cytatu.
        /// </summary>
        public void CollectTable(ParsingContext context, ContentTable table)
        {
            var collector = context.AmendmentCollector;
            var opensQuotedContent = collector.Count == 0;

            if (!collector.IsCollecting)
            {
                var owner = context.AmendmentOwner ?? GetOwner(context);
                if (owner != null)
                    collector.Begin(owner, context.DetectedAmendmentTargets.GetValueOrDefault(owner.Guid));
            }

            collector.AddTable(table);
            context.AmendmentTriggerDetected = false;

            var quotes = collector.QuoteTracker;
            if (quotes.ClosureReached || (opensQuotedContent && !quotes.IsInsideQuote))
            {
                Flush(context);
                context.InsideAmendment = false;
            }
            else
            {
                context.InsideAmendment = true;
            }
        }

        /// <summary>
        /// Buduje nowelizację z zebranych akapitów i deleguje finalizację
        /// do AmendmentFinalizer (Faza 3). Wywołuje się po zamknięciu nowelizacji
        /// (powrót do stylu ustawy matki) lub na końcu dokumentu.
        /// </summary>
        public void Flush(ParsingContext context)
        {
            EndProvisionQuote(context);
            var collector = context.AmendmentCollector;
            if (!collector.IsCollecting || collector.Count == 0)
            {
                collector.Reset();
                context.AmendmentOwner = null;
                return;
            }

            var closingText = collector.Paragraphs[^1] is { Table: null } closingParagraph ? closingParagraph.Text : null;
            var quotes = TrimQuoteBoundaries(collector);

            // Faza 2: budowanie treści nowelizacji
            var buildInput = new AmendmentBuildInput(
                collector.Paragraphs,
                collector.Targets.FirstOrDefault(),
                AmendmentOperationType.Modification,
                context.SectionIsBasicUnit);

            // Odnośnik jako przedmiot komendy: jego treść nie przechodzi przez rozpoznawanie jednostek (ADR-0059).
            // Zdanie albo akapit jako przedmiot komendy: treść cytowana jest tekstem, bez względu na style (ADR-0079).
            var footnote = QuotedFootnoteReader.Read(collector.Owner?.ContentText);
            var quoteAtTheMarker = false;
            var content = footnote is not null
                ? new AmendmentContent
                {
                    ObjectType = AmendmentObjectType.None,
                    Footnote = QuotedFootnoteReader.Build(footnote, collector.Paragraphs, out quoteAtTheMarker),
                }
                : QuotedTextSubject.Read(collector.Owner?.ContentText, collector.Paragraphs) ?? _amendmentBuilder.Build(buildInput);

            // Tekst pod przepisem, który niczego nie zmienia („oświadczenie o następującej treści:", „według wzoru:"),
            // jest treścią tego przepisu, nie nowelizacją (ADR-0071).
            if (footnote is null && collector.Owner is { } provision && !AmendsAnything(context, provision)
                && TryKeepAsContentOfTheProvision(provision, collector, content, quotes, closingText))
            {
                Log.Debug("Treść pod przepisem bez komendy — treść przepisu, nie nowelizacja (owner={OwnerId})", provision.Id);

                collector.Reset();
                context.AmendmentOwner = null;
                return;
            }

            // Faza 3: finalizacja — detekcja operacji, linkowanie celów/JournalInfo,
            // przypisanie do właściciela, walidacja
            var finalizerInput = new AmendmentFinalizerInput(content, collector, context)
            {
                Targets = footnote is null ? null : QuotedFootnoteReader.Target(footnote, collector.Owner?.ContentText) is { } target ? [target] : [],
            };
            var amendment = _amendmentFinalizer.Finalize(finalizerInput);

            if (amendment != null)
            {
                amendment.TrailingPunctuation = quotes.Punctuation;
                amendment.HasOpeningQuote = quotes.HasOpening || quoteAtTheMarker;
                amendment.HasClosingQuote = quotes.HasClosing;
            }

            collector.Reset();
            context.AmendmentOwner = null;
        }

        /// <summary>
        /// Czy przepis, pod którym zebrano treść cytowaną, cokolwiek zmienia: stoi w artykule zmieniającym inny akt
        /// (<see cref="Article.IsAmending"/>), czekają na niego zmiany odczytane z jego tekstu albo on sam lub
        /// przepis nad nim niesie czasownik komendy (<see cref="AmendmentCommandParser.HasChangeVerb"/>).
        /// </summary>
        private static bool AmendsAnything(ParsingContext context, BaseEntity owner)
        {
            if (AmendmentFinalizer.FindParentArticle(owner) is { IsAmending: true })
                return true;

            if (context.PendingTextChanges.ContainsKey(owner.Guid) || context.PendingContentWraps.ContainsKey(owner.Guid)
                || context.PendingAnnexInstructions.ContainsKey(owner.Guid))
            {
                return true;
            }

            for (var unit = owner; unit is not null; unit = unit is Saga.Model.EditorialUnits.Article ? null : unit.Parent)
            {
                if (AmendmentCommandParser.HasChangeVerb(unit.ContentText))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Treść zebrana pod przepisem, który niczego nie zmienia: tekst w cudzysłowie jest przytoczeniem
        /// (<see cref="Quotation"/>), tekst bez cudzysłowu i bez jednostek — dalszą treścią przepisu. Jednostki bez
        /// cudzysłowu zostają tym, czym były: tu nie wiadomo jeszcze, czy to jednostki aktu złożone stylem zmiany.
        /// </summary>
        private static bool TryKeepAsContentOfTheProvision(
            BaseEntity provision, AmendmentCollector collector, AmendmentContent content, QuoteBoundaries quotes, string? closingText)
        {
            var opens = quotes.HasOpening;
            var closes = quotes.HasClosing;
            List<ContentBlock> opening = [];

            if (closes && !opens)
            {
                if (closingText is not null && !ClosesAQuoteOpenedEarlier(closingText))
                {
                    // Cudzysłów na końcu, otwarty w tym samym akapicie („… do działania „n","), niczego nie zamyka.
                    collector.Paragraphs[^1].Text = closingText;
                    closes = false;
                }
                else
                {
                    // Cytat otwarty akapit wcześniej („„Oświadczam, że:" w stylu cytatu) — ten akapit do niego należy.
                    opening = LinesOpeningTheQuote(provision);
                    opens = opening.Count > 0;
                }
            }

            if (!opens && !closes)
            {
                if (!IsTextOnly(content))
                    return false;

                AttachAsFurtherContent(provision, collector.Paragraphs);
                return true;
            }

            if (opening.Count > 0)
            {
                provision.Continuation.RemoveRange(provision.Continuation.Count - opening.Count, opening.Count);
                var lines = opening.Select(block => block.Text!).ToList();
                lines[0] = OpeningQuotePattern.Replace(lines[0], "", 1);
                if (!string.IsNullOrEmpty(content.PlainText))
                    lines.Add(content.PlainText);
                content.PlainText = string.Join(Environment.NewLine, lines);
            }

            provision.Continuation.Add(new ContentBlock
            {
                Quotation = new Quotation
                {
                    Content = content,
                    TrailingPunctuation = closes ? quotes.Punctuation : null,
                    HasOpeningQuote = opens,
                    HasClosingQuote = closes,
                },
            });
            return true;
        }

        /// <summary>Czy akapit zamyka więcej cudzysłowów, niż sam otwiera — domyka cytat zaczęty wcześniej.</summary>
        private static bool ClosesAQuoteOpenedEarlier(string text)
        {
            var open = 0;
            var straight = 0;
            foreach (var character in text)
            {
                if (character == QuotationMarks.Opening)
                    open++;
                else if (QuotationMarks.IsClosing(character))
                    open--;
                else if (character == QuotationMarks.Straight)
                    straight++;
            }

            return open < 0 || straight % 2 == 1;
        }

        /// <summary>
        /// Końcowe akapity dalszej treści przepisu, z których pierwszy otwiera cudzysłów niedomknięty do ich końca.
        /// </summary>
        private static List<ContentBlock> LinesOpeningTheQuote(BaseEntity provision)
        {
            var blocks = provision.Continuation;
            var start = blocks.Count;
            while (start > 0 && blocks[start - 1] is { Text.Length: > 0, Formula: null, Table: null, Quotation: null })
                start--;

            for (var i = start; i < blocks.Count; i++)
            {
                if (!QuoteBalanceTracker.OpensQuote(blocks[i].Text!))
                    continue;

                var lines = blocks.GetRange(i, blocks.Count - i);
                var open = lines.Sum(line => line.Text!.Count(c => c == QuotationMarks.Opening) - line.Text!.Count(QuotationMarks.IsClosing));
                return open > 0 ? lines : [];
            }

            return [];
        }

        private static bool IsTextOnly(AmendmentContent content) =>
            AmendmentTargetPairing.ContentUnits(content).Count == 0 && content.Tables.Count == 0 && content.CommonParts.Count == 0;

        /// <summary>
        /// Tekst bez cudzysłowu i bez jednostek pod przepisem, który niczego nie zmienia, to dalsza treść tego
        /// przepisu — wzór, wiersz legendy, akapit — w kolejności z aktu, jak akapit bez stylu zmiany (ADR-0029).
        /// </summary>
        private static void AttachAsFurtherContent(BaseEntity unit, IReadOnlyList<CollectedAmendmentParagraph> paragraphs)
        {
            foreach (var paragraph in paragraphs)
            {
                var text = paragraph.Text.Trim();
                if (paragraph.FormulaMathMl is { } mathMl)
                {
                    unit.Continuation.Add(new ContentBlock { Text = text, Formula = new ContentFormula { MathMl = mathMl } });
                    continue;
                }

                var leading = paragraph.LeadingSymbol is { } symbol
                    ? new FormulaSymbol { Symbol = symbol.Text, SymbolMathMl = symbol.MathMl }
                    : null;
                if (text.Length > 0 && !FormulaLegend.TryAbsorb(unit, text, leading))
                    unit.Continuation.Add(new ContentBlock { Text = text });
            }
        }

        /// <summary>
        /// Akapit otwiera przytoczenie pod przepisem: zaczyna się cudzysłowem, którego nie domyka, a przepis nad nim kończy
        /// dwukropek i niczego nie zmienia („1) oświadczenie następującej treści:" i „„Oświadczam, że:") (ADR-0076). Tak samo
        /// akapit, który w całości jest cytatem („ślubowanie według następującej roty:" i „„Ślubuję uroczyście … .”") —
        /// przytoczenie kończy się wtedy na nim samym.
        /// </summary>
        private static bool OpensProvisionQuote(ParsingContext context, string text) =>
            !context.InsideAmendment && !context.AmendmentTriggerDetected && !context.AmendmentCollector.IsCollecting
            && QuoteBalanceTracker.OpensQuote(text) && (QuoteDepthAfter(0, text) > 0 || IsWholeQuotation(text))
            && GetOwner(context) is { } provision && provision.ContentText.TrimEnd().EndsWith(':')
            && !AmendsAnything(context, provision);

        private static readonly System.Text.RegularExpressions.Regex QuotationEnd = new(
            $@"[{QuotationMarks.Closing}]\s*[.,;]?\s*$", System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>
        /// Akapit w całości jest cytatem: cudzysłów otwierający z jego początku domyka się dopiero na końcu, przed
        /// interpunkcją. Wyrażenie w cudzysłowie na początku („„Ustawa" oznacza …”) domyka się wcześniej i cytatem nie jest.
        /// </summary>
        private static bool IsWholeQuotation(string text)
        {
            var end = QuotationEnd.Match(text);
            var start = text.IndexOf(QuotationMarks.Opening);
            if (!end.Success || start < 0)
                return false;

            var depth = 0;
            for (var i = start; i < end.Index; i++)
            {
                if (text[i] == QuotationMarks.Opening)
                    depth++;
                else if (QuotationMarks.IsClosing(text[i]) && --depth == 0)
                    return false;
            }

            return depth == 1;
        }

        /// <summary>Głębokość cudzysłowu po akapicie: otwarcia minus zamknięcia, nie mniej niż zero.</summary>
        private static int QuoteDepthAfter(int depth, string text)
        {
            foreach (var character in text)
            {
                if (character == QuotationMarks.Opening)
                    depth++;
                else if (QuotationMarks.IsClosing(character) && depth > 0)
                    depth--;
            }

            return depth;
        }

        /// <summary>Kończy przytoczenie otwarte pod przepisem.</summary>
        internal static void EndProvisionQuote(ParsingContext context)
        {
            context.InProvisionQuote = false;
            context.ProvisionQuoteDepth = 0;
        }

        /// <summary>
        /// Elementy cytatu zdjęte z nowelizacji: obecność cudzysłowu otwierającego
        /// i zamykającego oraz interpunkcja następująca po cytacie.
        /// </summary>
        internal readonly record struct QuoteBoundaries(bool HasOpening, bool HasClosing, string? Punctuation);

        /// <summary>
        /// Zdejmuje graniczne cudzysłowy cytatu.
        /// Zwraca zdjętą interpunkcję (np. ',' z '…”,').
        /// </summary>
        internal static QuoteBoundaries TrimQuoteBoundaries(AmendmentCollector collector) =>
            TrimQuoteBoundaries(collector.Paragraphs);

        /// <summary>
        /// Zdejmuje graniczne cudzysłowy z pierwszego i ostatniego akapitu cytowanej treści, a gdy treść zaczyna
        /// się albo kończy tabelą — z jej pierwszej i ostatniej komórki (<see cref="QuotedTableBoundaries"/>).
        /// </summary>
        internal static QuoteBoundaries TrimQuoteBoundaries(IReadOnlyList<CollectedAmendmentParagraph> paragraphs)
        {
            if (paragraphs.Count == 0)
                return default;

            var first = paragraphs[0];
            bool hasOpening;
            if (first.Table is { } openingTable)
            {
                hasOpening = QuotedTableBoundaries.TakeOpening(openingTable);
            }
            else
            {
                var trimmedText = OpeningQuotePattern.Replace(first.Text, "", 1);
                hasOpening = trimmedText.Length != first.Text.Length;
                first.Text = trimmedText;
            }

            var last = paragraphs[^1];
            if (last.Table is { } closingTable)
            {
                return QuotedTableBoundaries.TakeClosing(closingTable) is { } afterTable
                    ? new QuoteBoundaries(hasOpening, true, afterTable.Length > 0 ? afterTable : null)
                    : new QuoteBoundaries(hasOpening, false, null);
            }

            var match = ClosingQuotePattern.Match(last.Text);
            if (!match.Success)
                return new QuoteBoundaries(hasOpening, false, null);

            last.Text = last.Text[..match.Index].TrimEnd();
            var punctuation = match.Groups["punct"].Value;
            return new QuoteBoundaries(hasOpening, true, punctuation.Length > 0 ? punctuation : null);
        }

        /// <summary>Otwierający cudzysłów cytatu na początku pierwszego akapitu.</summary>
        private static readonly System.Text.RegularExpressions.Regex OpeningQuotePattern = new(
            $@"^\s*[{QuotationMarks.OpeningOrStraight}]",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>
        /// Zamykający cudzysłów cytatu na końcu ostatniego akapitu z opcjonalną interpunkcją.
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex ClosingQuotePattern = new(
            $@"[{QuotationMarks.ClosingOrStraight}]\s*(?<punct>[,;.])?\s*$",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>
        /// Sprawdza czy przetworzony akapit zawiera zwrot rozpoczynający nowelizację.
        /// Wywołuje się PO przetworzeniu akapitu (po budowaniu encji).
        ///
        /// Obsługuje dwa scenariusze:
        /// 1. Uchylenie ("uchyla się") — natychmiastowe utworzenie nowelizacji Repeal bez treści.
        /// 2. Zmiana brzmienia / dodanie ("otrzymuje brzmienie:", "w brzmieniu:") —
        ///    ustawienie triggera dla kolejnych akapitów.
        /// </summary>
        public void DetectTrigger(ParsingContext context, string text)
        {
            NoteContentWrap(context, text);
            NoteAnnexInstructions(context, text);
            DetectCommands(context, text);

            // Obudowanie treści bez żadnej innej zmiany w tym przepisie („…oznacza się jako ust. 1 i:" przed
            // wyliczeniem zmian) jest nowelizacją samo w sobie; tak samo zmiana załącznika odesłaniem.
            if (GetOwner(context) is { } owner && !context.AmendmentTriggerDetected
                && (context.PendingContentWraps.ContainsKey(owner.Guid) || context.PendingAnnexInstructions.ContainsKey(owner.Guid)))
            {
                FinalizeWithoutContent(context, owner);
            }
        }

        /// <summary>
        /// „Dotychczasową treść oznacza się jako ust. 1" (w kodeksie: „jako § 1") — dotychczasowa treść jednostki
        /// staje się jej pierwszym ustępem albo paragrafem (ADR-0056).
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex ContentWrapPattern = new(
            @"dotychczasow(?:ą|e)\s+treść\s+oznacza\s+się\s+jako\s+(?<level>ust\.|§)\s*1\b",
            System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        /// <summary>
        /// Odnotowuje obudowanie dotychczasowej treści: dotyczy jednostki, w której powstaje ustęp 1 — celu przepisu
        /// bez poziomu ustępu. Dołączy je nowelizacja tego przepisu (<see cref="AmendmentFinalizer"/>).
        /// </summary>
        private static void NoteContentWrap(ParsingContext context, string text)
        {
            var match = ContentWrapPattern.Match(text);
            if (!match.Success || GetOwner(context) is not { } owner)
                return;

            StructuralAmendmentReference? unit = null;
            if (context.DetectedAmendmentTargets.GetValueOrDefault(owner.Guid) is [var target, ..] && target.Structure.Article is not null)
            {
                unit = new StructuralAmendmentReference
                {
                    Structure = new StructuralReference { Article = target.Structure.Article, Systematizing = target.Structure.Systematizing },
                    RawText = target.RawText,
                };
            }

            var level = match.Groups["level"].Value == "§" ? ContentWrap.CodeParagraph : ContentWrap.Paragraph;
            context.PendingContentWraps[owner.Guid] = (level, unit);
        }

        /// <summary>
        /// Odnotowuje zmianę załączników zapisaną odesłaniem do załączników aktu zmieniającego
        /// (<see cref="AnnexInstructionReader"/>) — dołączy ją nowelizacja tego przepisu.
        /// </summary>
        private static void NoteAnnexInstructions(ParsingContext context, string text)
        {
            if (GetOwner(context) is { } owner && AnnexInstructionReader.Read(text) is { Count: > 0 } instructions)
                context.PendingAnnexInstructions[owner.Guid] = instructions;
        }

        /// <summary>Nowelizacja bez treści cytowanej i bez zmian w tekście — niesie to, co na nią czekało.</summary>
        private void FinalizeWithoutContent(ParsingContext context, BaseEntity owner)
        {
            var collector = context.AmendmentCollector;
            if (collector.IsCollecting && collector.Count > 0)
                Flush(context);

            collector.Begin(owner, context.DetectedAmendmentTargets.GetValueOrDefault(owner.Guid));
            _amendmentFinalizer.Finalize(new AmendmentFinalizerInput(
                new AmendmentContent { ObjectType = AmendmentObjectType.None }, collector, context, AmendmentOperationType.Modification));
            collector.Reset();
        }

        private void DetectCommands(ParsingContext context, string text)
        {
            // Zamiana wyrazów (§ 87-88 ZTP) analizowana PRZED uchyleniem: fraza „uchyla się" WEWNĄTRZ
            // cytowanych wyrazów komendy zamiany („wyrazy „uchyla się" zastępuje się…") nie może
            // przeinaczyć operacji na Repeal. Komendę zamiany wycina się z tekstu, a POZOSTAŁOŚĆ bada
            // na obecność innych komend (prawdziwa komenda złożona) — te wygrywają jako operacja
            // strukturalna (model ma jedną nowelizację na encję), z Warningiem dla audytu.
            var textChanges = AmendmentCommandParser.TextChangeCommands(text);
            var structuralText = AmendmentCommandParser.WithoutTextChanges(text, textChanges);

            if (textChanges.Count > 0)
            {
                bool hasStructuralCommand = IsRepealCommand(GetOwner(context), structuralText)
                    || AmendmentFinalizer.ModificationPattern.IsMatch(structuralText);

                if (!hasStructuralCommand)
                {
                    HandleTextChanges(context, textChanges);

                    // Reszta zdania bywa zmianą w tekście, której zapisać się nie da („…i dodaje się od nowego
                    // wiersza wyrazy „Y"") — rozpoznane zmiany zostają, a ślad zostawia ostrzeżenie.
                    if (WordLevelCommandPattern.IsMatch(structuralText) && GetOwner(context) is { } partlyMapped)
                    {
                        ValidationReporter.AddValidationMessage(partlyMapped, ValidationLevel.Warning,
                            "Komenda wyrazowa (skreślenie/dodanie wyrazów) — nieodwzorowana jako obiekt nowelizacji; " +
                            "treść komendy zachowana werbatim.");
                    }

                    return;
                }

                var compoundOwner = GetOwner(context);
                if (compoundOwner != null)
                {
                    // Zamiana znaku kończącego jednostkę towarzyszy zmianie jednostki po niej — zostaje przy
                    // nowelizacji tego przepisu. Zmiana wyrazów w komendzie złożonej nie ma jeszcze własnego celu.
                    var signs = textChanges.Where(command => command.Kind == AmendmentCommandKind.ReplaceSign).ToList();
                    if (signs.Count > 0)
                        KeepSignChanges(context, compoundOwner, text, signs);

                    if (signs.Count < textChanges.Count)
                    {
                        ValidationReporter.AddValidationMessage(compoundOwner, ValidationLevel.Warning,
                            "Komenda złożona: zamiana wyrazów połączona z inną komendą nowelizacyjną — " +
                            "model reprezentuje tylko operację strukturalną; zamiana wyrazów pozostaje w treści komendy.");
                    }
                }
            }

            // Komendy WYRAZOWE § 87 ust. 3 pkt 2-3 („skreśla się wyrazy „X"", „dodaje się wyrazy „Y"") —
            // operują na wyrazach, nie jednostkach: NIE wolno ich odwzorować jako Repeal/Insertion jednostki
            // (fałszywe uchylenie art.!). Model nie ma dla nich reprezentacji — tekst pozostaje werbatim
            // w treści właściciela, a ślad zostawia Warning.
            if (WordLevelCommandPattern.IsMatch(structuralText))
            {
                var wordCmdOwner = GetOwner(context);
                if (wordCmdOwner != null)
                {
                    ValidationReporter.AddValidationMessage(wordCmdOwner, ValidationLevel.Warning,
                        "Komenda wyrazowa (skreślenie/dodanie wyrazów) — nieodwzorowana jako obiekt nowelizacji; " +
                        "treść komendy zachowana werbatim.");
                }

                // Komenda złożona („…dodaje się wyraz „lub" oraz dodaje się pkt 4 w brzmieniu:"): po dwukropku
                // stoi treść cytowana — komenda wyrazowa zostaje w treści przepisu, a zwrot końcowy obowiązuje.
                if (AmendmentFinalizer.ModificationAtEndPattern.IsMatch(text))
                    ArmTrigger(context, text);
                return;
            }

            // Uchylenie — natychmiastowa nowelizacja bez treści (badane na tekście bez komendy zamiany).
            if (IsRepealCommand(GetOwner(context), structuralText))
            {
                var owner = GetOwner(context);
                if (owner == null)
                {
                    Log.Warning("Uchylenie: brak właściciela");
                    return;
                }

                var collector = context.AmendmentCollector;

                // Zapobieganie utracie poprzedniej nowelizacji gdy kolektor jest aktywny
                if (collector.IsCollecting && collector.Count > 0)
                {
                    Log.Warning("Uchylenie wykryte podczas aktywnego zbierania nowelizacji; " +
                        "finalizacja poprzedniej przed uchyleniem (owner={OwnerId})",
                        collector.Owner?.Id ?? "brak");
                    Flush(context);
                }

                var targets = context.DetectedAmendmentTargets.GetValueOrDefault(owner.Guid);
                collector.Begin(owner, targets);

                var content = new AmendmentContent { ObjectType = AmendmentObjectType.None };
                var finalizerInput = new AmendmentFinalizerInput(content, collector, context);
                _amendmentFinalizer.Finalize(finalizerInput);

                collector.Reset();
                return;
            }

            // Zmiana brzmienia / dodanie — trigger dla kolejnych akapitów, o ile zwrot zostawia brzmienie
            // otwarte: przepis, który podaje brzmienie w tym samym akapicie, niczego za sobą nie zapowiada.
            if (AmendmentFinalizer.LeavesWordingOpen(text))
                ArmTrigger(context, text);
        }

        /// <summary>
        /// Czy czasownik uchylenia jest w tym przepisie komendą. „Uchyla się" i „skreśla się" należą też do języka
        /// przepisów merytorycznych („uchyla się od wykonania obowiązku", „decyzję uchyla się, jeżeli…", „skreśla się
        /// z listy studentów"), więc sam czasownik nie wystarcza. Komendą jest w artykule zmieniającym inny akt
        /// (<see cref="Article.IsAmending"/>) albo wtedy, gdy zaraz za nim stoi uchylana jednostka
        /// („uchyla się ust. 3") (ADR-0069).
        /// </summary>
        private static bool IsRepealCommand(BaseEntity? owner, string structuralText) =>
            AmendmentCommandParser.HasRepealVerb(structuralText)
            && (AmendmentFinalizer.FindParentArticle(owner) is { IsAmending: true }
                || AmendmentCommandParser.RepealsNamedUnit(structuralText));

        private static void ArmTrigger(ParsingContext context, string text)
        {
            context.AmendmentTriggerDetected = true;
            context.AmendmentOwner = GetOwner(context);
            Log.Debug("Wykryto zwrot nowelizacyjny (owner={OwnerId}): {Text}",
                context.AmendmentOwner?.Id ?? "brak",
                text.Length > 80 ? text.Substring(0, 80) + "..." : text);
        }

        /// <summary>
        /// Zamiana znaku kończącego jednostkę w zdaniu, które zmienia też jednostkę po niej („w pkt 4 średnik
        /// zastępuje się kropką i uchyla się pkt 5", „w pkt 20 kropkę zastępuje się średnikiem i dodaje się pkt 21
        /// w brzmieniu:"). Znak zmienia się w jednostce wskazanej przed komendą, więc zmiana bierze ją za własny
        /// cel i czeka na nowelizację tego przepisu. Uchylenie stojące za zamianą znaku dotyczy jednostki nazwanej
        /// po niej — cele przepisu czyta się wtedy z dalszej części zdania, a nie z tej sprzed znaku.
        /// </summary>
        private static void KeepSignChanges(ParsingContext context, BaseEntity owner, string text, List<AmendmentCommand> signs)
        {
            var unitOfTheSign = context.DetectedAmendmentTargets.GetValueOrDefault(owner.Guid) ?? [];

            context.PendingTextChanges[owner.Guid] = signs
                .Select(command => new TextChange
                {
                    OldText = command.OldWording ?? string.Empty,
                    NewText = command.NewWording ?? string.Empty,
                    Occurrence = TextOccurrence.Last,
                    Targets = unitOfTheSign
                        .Select(target => new StructuralAmendmentReference
                        {
                            Structure = target.Structure.Clone(),
                            RangeEnd = target.RangeEnd?.Clone(),
                            RawText = target.RawText,
                        })
                        .ToList(),
                })
                .ToList();

            if (unitOfTheSign.Count == 0 || signs[^1].MatchedCommandText is not { } matched)
                return;

            var afterTheSign = text[(text.IndexOf(matched, StringComparison.Ordinal) + matched.Length)..];
            if (!AmendmentFinalizer.RepealPattern.IsMatch(afterTheSign) || AmendmentFinalizer.ModificationPattern.IsMatch(afterTheSign))
                return;

            var repealed = context.ReferenceService.ReadAmendmentTargets(
                unitOfTheSign[0].Structure, afterTheSign,
                Services.UnitReferences.SectionSignRole.IsBasicUnit(context.Document.Classification));
            if (repealed.Count > 0)
                context.DetectedAmendmentTargets[owner.Guid] = repealed;
        }

        /// <summary>
        /// Zmiany w tekście (§ 87-88 ZTP: zamiana, dodanie, skreślenie wyrazów): komendy kompletne w jednym
        /// akapicie (bez cytowanego bloku po nich) → natychmiastowa nowelizacja ze zmianami w tekście
        /// (<see cref="TextChange"/>: wyrazy zastępowane i zastępujące, werbatim), w kolejności zapisu, bez treści
        /// cytowanej (ADR-0052).
        /// Operacja WYMUSZONA na Modification — DetectOperationType na tekście właściciela mógłby
        /// zostać przeinaczony przez czasowniki wewnątrz cytowanych wyrazów („dodaje się"/„uchyla się").
        /// </summary>
        private void HandleTextChanges(ParsingContext context, IReadOnlyList<AmendmentCommand> commands)
        {
            var owner = GetOwner(context);
            if (owner == null)
            {
                Log.Warning("Zamiana wyrazów: brak właściciela");
                return;
            }

            var collector = context.AmendmentCollector;
            if (collector.IsCollecting && collector.Count > 0)
            {
                Log.Warning("Zamiana wyrazów wykryta podczas aktywnego zbierania nowelizacji; " +
                    "finalizacja poprzedniej (owner={OwnerId})", collector.Owner?.Id ?? "brak");
                Flush(context);
            }

            var targets = context.DetectedAmendmentTargets.GetValueOrDefault(owner.Guid);
            collector.Begin(owner, targets);

            var content = new AmendmentContent { ObjectType = AmendmentObjectType.None };
            // Odstęp tuż za cudzysłowem otwierającym („„ , o którym mowa"") jest zabiegiem składu, nie tekstem.
            var changes = commands
                .Select(command => new TextChange
                {
                    OldText = (command.OldWording ?? string.Empty).Trim(),
                    NewText = (command.NewWording ?? string.Empty).Trim(),
                    Occurrence = command.Kind == AmendmentCommandKind.ReplaceSign ? TextOccurrence.Last : TextOccurrence.All,
                })
                .ToList();
            _amendmentFinalizer.Finalize(new AmendmentFinalizerInput(content, collector, context,
                AmendmentOperationType.Modification, changes));

            collector.Reset();
        }

        /// <summary>
        /// Zwraca najgłębszą encję w bieżącym kontekście implementującą IHasAmendments.
        /// Używane do ustalenia właściciela nowelizacji.
        /// </summary>
        public static BaseEntity? GetOwner(ParsingContext context)
        {
            // Tiret sprawdzany pierwszy — kazdy tiret moze byc wlascicielem wlasnej nowelizacji,
            // a wielu tiretow w tej samej literze musi miec osobne Amendment (single-property).
            if (context.CurrentTiret is IHasAmendments) return context.CurrentTiret;
            if (context.CurrentLetter is IHasAmendments) return context.CurrentLetter;
            if (context.CurrentPoint is IHasAmendments) return context.CurrentPoint;
            if (context.CurrentParagraph is IHasAmendments) return context.CurrentParagraph;
            return null;
        }
    }
}
