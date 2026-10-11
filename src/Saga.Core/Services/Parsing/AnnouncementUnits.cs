using System.Text.RegularExpressions;
using Saga.Core.Ingest;
using Saga.Model;
using Saga.Model.EditorialUnits;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Jednostki obwieszczenia tekstu jednolitego (§ 102–104 ZTP): punkty („1. Na podstawie …"), podpunkty
    /// („1) ustawą …") i część wspólna podpunktów. Przytoczone przepisy („…, który stanowi: „Art. 57. …"")
    /// i tabele są dalszą treścią jednostki, za którą stoją; podpis trafia do dokumentu.
    /// Potok aktu tu nie pracuje — przytoczony artykuł nie jest artykułem żadnego z dwóch aktów dokumentu.
    /// </summary>
    internal static class AnnouncementUnits
    {
        private static readonly Regex PointDesignation = new(@"^\d+[a-z]*\.\s", RegexOptions.Compiled);

        private static readonly Regex SubPointDesignation = new(@"^\d+[a-z]*\)\s", RegexOptions.Compiled);

        internal static Announcement Build(IReadOnlyList<DocumentBlock> blocks, LegalDocument document)
        {
            var announcement = new Announcement();
            Paragraph? point = null;
            Point? subPoint = null;

            // Głębokość otwartego przytoczenia; ciągnie się przez kolejne bloki do domknięcia cudzysłowu.
            var quoteDepth = 0;
            var quotedSinceSubPoint = false;

            for (var i = 0; i < blocks.Count; i++)
            {
                if (blocks[i].TableCell is { } cell)
                {
                    var run = blocks.Skip(i).TakeWhile(b => b.TableCell?.Table == cell.Table).ToList();
                    i += run.Count - 1;
                    Attach(new ContentBlock { Table = ContentTables.FromBlocks(run) });
                    continue;
                }

                if (blocks[i].IsEmpty)
                    continue;

                var text = blocks[i].Text.CollapseWhitespace().Trim();

                if (quoteDepth > 0 || QuoteBalanceTracker.OpensQuote(text))
                {
                    Attach(new ContentBlock { Text = text });
                    quoteDepth = Balance(quoteDepth, text);
                    quotedSinceSubPoint = true;
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

                if (document.Signature is null && ActSignatureParser.TryParse(text) is { } signature)
                {
                    document.Signature = signature;
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
