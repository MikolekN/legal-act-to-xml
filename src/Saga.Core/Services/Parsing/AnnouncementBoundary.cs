using System.Text.RegularExpressions;
using Saga.Core.Ingest;
using Saga.Core.Services.Classify.Document;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Granica między obwieszczeniem a aktem, który ogłasza ono w załączniku (tekst jednolity, § 102 ZTP).
    /// Dokument zaczyna się nagłówkiem obwieszczenia, a akt — wierszem „Załącznik do obwieszczenia …";
    /// wszystko przed tym wierszem jest treścią obwieszczenia, nie aktu.
    /// </summary>
    internal static class AnnouncementBoundary
    {
        /// <summary>Wiersz otwierający akt ogłaszany w załączniku do obwieszczenia.</summary>
        internal static readonly Regex AnnexHeader = new(@"^Załącznik\s+do\s+obwieszczenia\b", RegexOptions.Compiled);

        /// <summary>
        /// Układ dokumentu zaczynającego się nagłówkiem obwieszczenia.
        /// </summary>
        /// <param name="AnnexStart">Indeks wiersza „Załącznik do obwieszczenia …"; -1, gdy go nie ma.</param>
        /// <param name="AnnouncesAct">Załącznik jest aktem (tekst jednolity), a nie załącznikiem obwieszczenia.</param>
        /// <param name="HasProvisions">W treści obwieszczenia stoją artykuły albo paragrafy.</param>
        internal readonly record struct Layout(bool OpensWithAnnouncement, int AnnexStart, bool AnnouncesAct, bool HasProvisions)
        {
            /// <summary>
            /// Obwieszczenie, które nie ogłasza aktu i samo nie jest zbudowane z artykułów: cały dokument jest
            /// obwieszczeniem, a jego załączniki — załącznikami obwieszczenia.
            /// </summary>
            internal bool IsPlainAnnouncement => OpensWithAnnouncement && !AnnouncesAct && !HasProvisions;
        }

        /// <summary>
        /// Rozpoznaje układ obwieszczenia. Akt w załączniku poznaje się po dwóch sygnałach, z których wystarcza
        /// jeden: przedmiot obwieszczenia („w sprawie ogłoszenia jednolitego tekstu …") albo nagłówek rodzaju
        /// aktu tuż za wierszem „Załącznik do obwieszczenia …" („USTAWA", „ROZPORZĄDZENIE …").
        /// </summary>
        internal static Layout Describe(IReadOnlyList<DocumentBlock> blocks)
        {
            var annexStart = Find(blocks);
            var opens = annexStart >= 0;
            var consolidated = false;
            var provisions = false;
            var afterAnnex = (string?)null;

            for (var i = 0; i < blocks.Count; i++)
            {
                if (blocks[i] is not { Role: BlockRole.Body, IsEmpty: false, TableCell: null } block)
                    continue;

                var text = FootnoteMarkers.Remove(block.Text.CollapseWhitespace().Trim());
                if (!opens)
                {
                    if (!ZtpPatterns.AnnouncementHeaderPattern.IsMatch(text))
                        return new Layout(false, -1, false, false);

                    opens = true;
                    continue;
                }

                if (annexStart >= 0 && i > annexStart)
                {
                    afterAnnex = text;
                    break;
                }

                if (i == annexStart)
                    continue;

                consolidated |= ZtpPatterns.ConsolidatedTextTitlePattern.IsMatch(text);
                provisions |= Classify.ParagraphClassifier.IsArticleByText(text) || SectionDesignation.IsMatch(text);
            }

            var announcesAct = annexStart >= 0
                && (consolidated || (afterAnnex is not null && ZtpPatterns.KindOfHeading(afterAnnex) is not null));
            return new Layout(opens, annexStart, announcesAct, provisions);
        }

        private static readonly Regex SectionDesignation = new(@"^§\s*\d+[a-z]*\.", RegexOptions.Compiled);

        /// <summary>
        /// Indeks bloku „Załącznik do obwieszczenia …" w dokumencie zaczynającym się nagłówkiem obwieszczenia;
        /// -1, gdy dokument nie jest obwieszczeniem z aktem w załączniku.
        /// </summary>
        internal static int Find(IReadOnlyList<DocumentBlock> blocks)
        {
            var opensWithAnnouncement = false;
            for (var i = 0; i < blocks.Count; i++)
            {
                if (blocks[i] is not { Role: BlockRole.Body, IsEmpty: false, TableCell: null } block)
                    continue;

                var text = block.Text.CollapseWhitespace().Trim();
                if (!opensWithAnnouncement)
                {
                    if (!ZtpPatterns.AnnouncementHeaderPattern.IsMatch(FootnoteMarkers.Remove(text)))
                        return -1;

                    opensWithAnnouncement = true;
                    continue;
                }

                if (AnnexHeader.IsMatch(text))
                    return i;
            }

            return -1;
        }
    }
}
