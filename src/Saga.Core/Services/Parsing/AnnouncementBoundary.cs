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
        /// <summary>
        /// Wiersz otwierający to, co obwieszczenie niesie w załączniku: akt (tekst jednolity) albo własne
        /// załączniki — także w liczbie mnogiej i z numerem („Załączniki do obwieszczenia …", „Załącznik nr 2
        /// do obwieszczenia …").
        /// </summary>
        internal static readonly Regex AnnexHeader = new(
            @"^Załączniki?(?:\s+nr\s+\d+[a-z]*)?\s+do\s+obwieszczenia\b", RegexOptions.Compiled);

        /// <summary>
        /// Układ dokumentu zaczynającego się nagłówkiem obwieszczenia.
        /// </summary>
        /// <param name="AnnexStart">Indeks wiersza „Załącznik do obwieszczenia …"; -1, gdy go nie ma.</param>
        /// <param name="AnnouncesAct">Załącznik jest aktem (tekst jednolity), a nie załącznikiem obwieszczenia.</param>
        /// <param name="HasProvisions">W treści obwieszczenia stoją artykuły albo paragrafy („§ 1.").</param>
        /// <param name="AnnouncedKind">Rodzaj aktu nazwany jego nagłówkiem tuż za wierszem załącznika; null, gdy go nie ma.</param>
        /// <param name="HasSections">Przepisy obwieszczenia są paragrafami („§ 1."), nie artykułami.</param>
        internal readonly record struct Layout(
            bool OpensWithAnnouncement, int AnnexStart, bool AnnouncesAct, bool HasProvisions,
            Saga.Model.LegalActType? AnnouncedKind = null, bool HasSections = false)
        {
            /// <summary>
            /// Obwieszczenie, które nie ogłasza aktu: cały dokument jest obwieszczeniem, a jego załączniki —
            /// załącznikami obwieszczenia. Gdy samo jest zbudowane z paragrafów albo artykułów
            /// (<see cref="HasProvisions"/>), jego przepisy buduje potok aktu.
            /// </summary>
            internal bool IsPlainAnnouncement => OpensWithAnnouncement && !AnnouncesAct;
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
            var articles = false;
            var sections = false;
            var afterAnnex = (string?)null;

            for (var i = 0; i < blocks.Count; i++)
            {
                if (blocks[i] is not { Role: BlockRole.Body, IsEmpty: false, TableCell: null } block)
                    continue;

                var text = FootnoteMarkers.Remove(block.Text.CollapseWhitespace().Trim());
                if (!opens)
                {
                    if (!IsAnnouncementHeading(text))
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
                sections |= SectionDesignation.IsMatch(text);
                articles |= !SectionDesignation.IsMatch(text) && Classify.ParagraphClassifier.IsArticleByText(text);
            }

            var announcedKind = afterAnnex is null ? null : ZtpPatterns.KindOfHeading(afterAnnex);
            var announcesAct = annexStart >= 0 && (consolidated || announcedKind is not null);
            return new Layout(
                opens, annexStart, announcesAct, articles || sections,
                announcesAct ? announcedKind : null,
                HasSections: sections && !articles);
        }

        // Wiersz dłuższy niż nagłówek z nazwą organu jest już zdaniem.
        private const int LongestHeading = 160;

        /// <summary>
        /// Nagłówek obwieszczenia w pierwszym wierszu treści dokumentu. Wzorcowa pisownia wystarcza sama;
        /// pisownię spoza wzorca („Obwieszczenie marszałka Sejmu Rzeczypospolitej Polskiej" — organ małą literą)
        /// potwierdza miejsce: to pierwszy wiersz dokumentu, zaczyna się słowem rodzaju i nie jest zdaniem.
        /// </summary>
        private static bool IsAnnouncementHeading(string firstLine) =>
            ZtpPatterns.AnnouncementHeaderPattern.IsMatch(firstLine)
            || (ZtpPatterns.KindOfHeading(firstLine) == Saga.Model.LegalActType.Announcement
                && firstLine.Length <= LongestHeading
                && !firstLine.EndsWith('.'));

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
                    if (!IsAnnouncementHeading(FootnoteMarkers.Remove(text)))
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
