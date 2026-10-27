using System.Text.RegularExpressions;
using Saga.Core.Ingest;
using Saga.Core.Services.Parsing.Builders;
using Saga.Model;
using Saga.Model.EditorialUnits;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Jednostki obwieszczenia tekstu jednolitego (§ 102–104 ZTP): punkty („1. Na podstawie …"), podpunkty
    /// („1) ustawą …") i część wspólna podpunktów. Przytoczone przepisy („…, który stanowi: „Art. 57. …"")
    /// i tabele są dalszą treścią jednostki, za którą stoją; podpis trafia do dokumentu.
    /// Potok aktu tu nie pracuje — przytoczony artykuł nie jest artykułem żadnego z dwóch aktów dokumentu.
    /// Przytoczenie złożone z całych artykułów (paragrafów aktu wykonawczego) albo z ustępów artykułu, którego
    /// oznaczenie stoi przed cudzysłowem, buduje się jak treść cytowaną w nowelizacji (<see cref="AmendmentBuilder"/>)
    /// i trafia do jednostki jako <see cref="Quotation"/>; tak samo
    /// przytoczony odnośnik, który treść jednostki wymienia („nie obejmuje odnośnika nr 2 …"). Każde inne przytoczenie
    /// zostaje akapitami tekstu.
    /// </summary>
    internal static class AnnouncementUnits
    {
        private static readonly Regex PointDesignation = new(@"^\d+[a-z]*\.\s", RegexOptions.Compiled);

        private static readonly Regex SubPointDesignation = new(@"^\d+[a-z]*\)\s", RegexOptions.Compiled);

        /// <summary>
        /// Cudzysłów otwarty za oznaczeniem artykułu („Art. 78. „1. Uznaje się …") — przytoczony jest ustęp, a oznaczenie
        /// jego artykułu stoi przed cudzysłowem. Przytoczenie zaczyna się od tego akapitu (ADR-0075), a oznaczenie
        /// zostaje poza nim (ADR-0113).
        /// </summary>
        private static readonly Regex QuoteAfterArticleDesignation = new(
            $@"^(?<designation>(?:Art\.|§)\s*\d+{DesignationAlphabet.LowerLetter}*(?:\[[^\]]*\])?\.)\s*{QuotationMarks.Opening}",
            RegexOptions.Compiled);

        /// <param name="sectionIsBasicUnit">
        /// Ogłaszany akt jest oparty na paragrafach — przytoczone „§ 2." to wtedy jednostka podstawowa (ADR-0034).
        /// </param>
        internal static Announcement Build(
            IReadOnlyList<DocumentBlock> blocks, LegalDocument document, bool sectionIsBasicUnit = false)
        {
            var announcement = new Announcement();
            Paragraph? point = null;
            Point? subPoint = null;

            // Głębokość otwartego przytoczenia; ciągnie się przez kolejne bloki do domknięcia cudzysłowu.
            var quoteDepth = 0;
            var quotedSinceSubPoint = false;
            var quoted = new List<CollectedAmendmentParagraph>();

            // Oznaczenie artykułu przed cudzysłowem otwartego przytoczenia („Art. 9. „2. …"); null — cudzysłów stoi przed nim.
            string? designationBeforeQuote = null;

            for (var i = 0; i < blocks.Count; i++)
            {
                if (blocks[i].TableCell is { } cell)
                {
                    var run = blocks.Skip(i).TakeWhile(b => b.TableCell?.Table == cell.Table).ToList();
                    i += run.Count - 1;
                    var table = ContentTables.FromBlocks(run);

                    // Tabela w przytoczeniu jest jego treścią — dalszą treścią jednostki, za którą stoi, jak w treści
                    // cytowanej nowelizacji (ADR-0029). Cudzysłowy w jej komórkach liczą się do bilansu przytoczenia.
                    if (quoteDepth > 0)
                    {
                        quoted.Add(new CollectedAmendmentParagraph(string.Empty, null, null) { Table = table });
                        foreach (var cellBlock in run)
                            quoteDepth = Balance(quoteDepth, cellBlock.Text);
                        if (quoteDepth == 0)
                            AttachQuotation(quoted);
                        continue;
                    }

                    Attach(new ContentBlock { Table = table });
                    continue;
                }

                if (blocks[i].IsEmpty)
                    continue;

                var text = blocks[i].Text.CollapseWhitespace().Trim();

                // „Art. 78. „1. …" buduje się jak „„Art. 78. 1. …": ustęp należy do artykułu i od niego ma adres.
                if (quoteDepth == 0 && QuoteAfterArticleDesignation.Match(text) is { Success: true } cited)
                {
                    designationBeforeQuote = cited.Groups["designation"].Value;
                    text = QuotationMarks.Opening + designationBeforeQuote + " " + text[cited.Length..];
                }

                if (quoteDepth > 0 || QuoteBalanceTracker.OpensQuote(text))
                {
                    quoted.Add(new CollectedAmendmentParagraph(text, null, null));
                    quoteDepth = Balance(quoteDepth, text);
                    quotedSinceSubPoint = true;
                    if (quoteDepth == 0)
                        AttachQuotation(quoted);
                    continue;
                }

                if (PointDesignation.IsMatch(text))
                {
                    point = new Paragraph
                    {
                        EIdPrefix = Announcement.PointEIdPrefix,
                        Number = ParsingFactories.ParseParagraphNumber(text),
                    };
                    ParsingFactories.SetContentAndSegments(point, ParsingFactories.StripParagraphPrefix(text));
                    announcement.Points.Add(point);
                    subPoint = null;
                    continue;
                }

                if (point is not null && SubPointDesignation.IsMatch(text))
                {
                    if (point.Points.Count == 0)
                        ParsingFactories.AttachIntroCommonPart(point);

                    subPoint = new Point
                    {
                        EIdPrefix = Announcement.SubPointEIdPrefix,
                        Parent = point,
                        Paragraph = point,
                        Number = ParsingFactories.ParsePointNumber(text),
                    };
                    ParsingFactories.SetContentAndSegments(subPoint, ParsingFactories.StripPointPrefix(text));
                    point.Points.Add(subPoint);
                    quotedSinceSubPoint = false;
                    continue;
                }

                // Formuła podpisu; obwieszczenie wydane wspólnie ma ich kilka.
                if (ActSignatureParser.TryParse(text, blocks[i].Layout?.ItalicText) is { } signature)
                {
                    if (document.Signature is null)
                        document.Signature = signature;
                    else
                        document.CoSignatures.Add(signature);
                    continue;
                }

                // Akapit tuż za ostatnim podpunktem bez znaku końcowego zamyka wyliczenie (§ 57 ust. 3 ZTP).
                if (point is not null && subPoint is not null && !quotedSinceSubPoint
                    && CommonPartPlacement.LeavesEnumerationOpen(subPoint.ContentText)
                    && ParsingFactories.AttachWrapUpCommonPart(point, text))
                {
                    subPoint = null;
                    continue;
                }

                Attach(new ContentBlock { Text = text });
            }

            // Przytoczenie niedomknięte do końca obwieszczenia zostaje tekstem.
            AttachAsText(quoted);
            return announcement;

            // Treść bez oznaczenia należy do najgłębszej otwartej jednostki; przed pierwszym punktem — do obwieszczenia.
            void Attach(ContentBlock block)
            {
                var unit = (BaseEntity?)subPoint ?? point;
                if (unit is not null)
                    unit.Continuation.Add(block);
                else
                    announcement.Content.Add(block);
            }

            void AttachAsText(List<CollectedAmendmentParagraph> paragraphs)
            {
                foreach (var paragraph in paragraphs)
                    Attach(paragraph.Table is { } table ? new ContentBlock { Table = table } : new ContentBlock { Text = paragraph.Text });
                paragraphs.Clear();
            }

            // Domknięte przytoczenie: jednostki, gdy da się je zbudować w całości; inaczej akapity tekstu i tabele.
            void AttachQuotation(List<CollectedAmendmentParagraph> paragraphs)
            {
                var designation = designationBeforeQuote;
                designationBeforeQuote = null;

                var owner = (BaseEntity?)subPoint ?? point;
                if (owner is not null && !string.IsNullOrWhiteSpace(owner.ContentText)
                    && BuildQuotation(paragraphs, owner.ContentText, sectionIsBasicUnit, designation) is { } quotation)
                {
                    Attach(new ContentBlock { Quotation = quotation });
                    paragraphs.Clear();
                    return;
                }

                AttachAsText(paragraphs);
            }
        }

        /// <summary>
        /// Podstawa prawna obwieszczenia, które nie ogłasza aktu: pierwszy akapit jego treści („Na podstawie
        /// art. … ogłasza się …") — jak w akcie wykonawczym. Akapit otwierający wyliczenie („… ogłasza się
        /// następujące stawki:" i „1) …") jest wprowadzeniem do tego wyliczenia i zostaje z nim.
        /// </summary>
        internal static void TakeLegalBasis(Announcement announcement, LegalDocument document)
        {
            if (document.LegalBasis.Length > 0
                || announcement.Content.FirstOrDefault()?.Text is not { } first
                || !Classify.Document.ZtpPatterns.LegalBasisPattern.IsMatch(first))
                return;

            if (announcement.Content.Count > 1
                && announcement.Content[1].Text is { } next
                && Classify.UnitDesignation.LooksDesignated(next))
                return;

            document.LegalBasis = first;
            announcement.Content.RemoveAt(0);
        }

        /// <summary>
        /// Przytoczenie jako jednostki — tylko gdy składa się z całych artykułów (paragrafów aktu wykonawczego)
        /// i żaden akapit nie został poza nimi. Przytoczony ustęp albo punkt („„3. W przypadku …"") wymagałby
        /// adresu z treści jednostki („art. 5 ust. 3 ustawy …"), więc zostaje tekstem. Przytoczony przepis zmieniający
        /// z własnym cytatem („Art. 94. W ustawie … 1) w art. 10f ust. 5 otrzymuje brzmienie: „5. …"") ma ten cytat jako
        /// swoją nowelizację, jak w nowelizacji nowelizacji (ADR-0082) — o ile cytat stoi za komendą przepisu; cytat
        /// bez komendy przed sobą nie jest jednostką przytaczanego artykułu, więc przytoczenie zostaje tekstem.
        /// Tabela w środku przytoczenia jest dalszą treścią jednostki, za którą stoi. Przytoczony odnośnik
        /// (<see cref="QuotedFootnoteReader.Cited"/>) jest odnośnikiem pod adresem <c>odn_N</c> aktu przytaczanego.
        /// Oznaczenie artykułu przed cudzysłowem (<paramref name="designationBeforeQuote"/>) zostaje przy przytoczeniu,
        /// gdy za cudzysłowem stoi oznaczony ustęp — przytoczone są wtedy ustępy, nie artykuł (ADR-0113).
        /// </summary>
        private static Quotation? BuildQuotation(
            IReadOnlyList<CollectedAmendmentParagraph> paragraphs, string ownerText, bool sectionIsBasicUnit,
            string? designationBeforeQuote = null)
        {
            // Tabela na granicy przytoczenia niesie jego cudzysłów w komórce; jednostką nie zaczyna się ani nie kończy.
            if (paragraphs[0].Table is not null || paragraphs[^1].Table is not null)
                return null;

            // Granice obcina się na kopii — przytoczenie, którego nie da się zbudować, zostaje tekstem z cudzysłowami.
            var collected = paragraphs.Select(paragraph => paragraph with { }).ToList();
            var quotes = AmendmentStateManager.TrimQuoteBoundaries(collected);

            // Przytoczony odnośnik jest odnośnikiem, nie jednostkami (ADR-0059); jego znacznik bywa złożony indeksem
            // górnym razem z cudzysłowem otwierającym przytoczenie.
            if (QuotedFootnoteReader.Cited(ownerText, collected, out var quoteAtTheMarker) is { } footnote)
            {
                return (quotes.HasOpening || quoteAtTheMarker) && quotes.HasClosing
                    ? new Quotation
                    {
                        Content = new AmendmentContent { ObjectType = AmendmentObjectType.None, Footnote = footnote },
                        SourceAct = JournalReferenceService.ReadSingleAct(ownerText),
                        TrailingPunctuation = quotes.Punctuation,
                    }
                    : null;
            }

            if (!quotes.HasOpening || !quotes.HasClosing)
                return null;

            if (NestedQuotes.Split(collected).Any(item => QuoteBalanceTracker.OpensQuote(item.Paragraph.Text)))
                return null;

            var content = new AmendmentBuilder().Build(new AmendmentBuildInput(
                collected, null, AmendmentOperationType.Modification, sectionIsBasicUnit));

            var wholeProvisions = content.Articles.Count > 0
                && content.SystematizingUnits.Count == 0
                && content.Paragraphs.Count == 0
                && content.Points.Count == 0
                && content.Letters.Count == 0
                && content.Tirets.Count == 0
                && content.CommonParts.Count == 0
                && content.Tables.Count == 0
                && string.IsNullOrEmpty(content.PlainText);
            if (!wholeProvisions)
                return null;

            return new Quotation
            {
                Content = content,
                SourceAct = JournalReferenceService.ReadSingleAct(ownerText),
                TrailingPunctuation = quotes.Punctuation,
                ArticleDesignation = content.Articles[0].Paragraphs is [{ IsImplicit: false }, ..] ? designationBeforeQuote : null,
            };
        }

        private static int Balance(int depth, string text)
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
    }
}
