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
