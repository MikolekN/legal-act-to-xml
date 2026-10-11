using DocumentFormat.OpenXml.Packaging;
using Saga.Core.Ingest;
using Saga.Core.Services.Classify.Document;
using Saga.Core.Services.Parsing;
using Saga.Model;
using Saga.Model.SystematizingUnits;

namespace Saga.Core
{
    public static class LegalDocumentParser
    {
        /// <summary>
        /// Kanoniczny punkt wejścia (Etap 10): dowolny dokument tekstowy (DOCX/PDF/TXT) ze strumienia.
        /// Format wykrywany sygnaturowo (albo wymuszony w opcjach), dokument klasyfikowany wg ZTP,
        /// model budowany zgodnie z polityką. Strumień nieseekowalny jest buforowany w pamięci;
        /// parser czyta wyłącznie (read-only) i nie przejmuje własności strumienia.
        /// </summary>
        /// <param name="fileNameHint">Nazwa pliku źródłowego — rozszerzenie rozstrzyga wyłącznie
        /// niekonkluzywne przypadki detekcji formatu; może być null.</param>
        public static ParseResult Parse(Stream stream, string? fileNameHint = null, ParseOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(stream);
            options ??= ParseOptions.Default;

            if (!stream.CanSeek)
            {
                using var buffered = new MemoryStream();
                stream.CopyTo(buffered);
                buffered.Position = 0;
                return Parse(buffered, fileNameHint, options);
            }

            // Unknown w wymuszeniu = „brak wymuszenia" (naturalne przy propagacji SourceFormat
            // z wcześniejszego ParseResult zbudowanego z bloków) — wraca do detekcji sygnaturowej.
            var format = options.ForcedFormat is { } forced && forced != SourceFormat.Unknown
                ? forced
                : SourceFormatDetector.Detect(stream, fileNameHint);
            var reader = DocumentBlockReaderFactory.Create(format);
            var blocks = reader.ReadBlocks(stream);

            return Parse(blocks, options, format);
        }

        /// <summary>Wygoda: parsowanie pliku z dysku (otwierany read-only, bez modyfikacji).</summary>
        public static ParseResult Parse(string filePath, ParseOptions? options = null)
        {
            using var stream = File.OpenRead(filePath);
            return Parse(stream, Path.GetFileName(filePath), options);
        }

        /// <summary>
        /// Parsowanie z gotowych bloków reprezentacji pośredniej (dla wywołujących, którzy
        /// sami przeprowadzili odczyt adapterem). Format źródłowy w kopercie = Unknown.
        /// </summary>
        public static ParseResult Parse(IReadOnlyList<DocumentBlock> blocks, ParseOptions? options = null)
            => Parse(blocks, options ?? ParseOptions.Default, SourceFormat.Unknown);

        [Obsolete("Użyj Parse(Stream, ...) — ta metoda pomija klasyfikację dokumentu i kopertę ParseResult.")]
        public static LegalDocument Parse(WordprocessingDocument wordDocument)
        {
            var blocks = new DocxBlockReader().ReadBlocks(wordDocument);
            return ParseBlocks(blocks, ParseOptions.Default);
        }

        private static ParseResult Parse(IReadOnlyList<DocumentBlock> blocks, ParseOptions options, SourceFormat format)
        {
            ArgumentNullException.ThrowIfNull(blocks);

            var classification = new DocumentClassifier().Classify(blocks);

            bool buildModel = options.Policy switch
            {
                ParsePolicy.AlwaysParse => true,
                ParsePolicy.ClassifyOnly => false,
                _ => classification.IsLegalAct,
            };

            LegalDocument? document = null;
            if (buildModel)
                document = ParseBlocks(blocks, options, classification);

            return new ParseResult
            {
                Classification = classification,
                Document = document,
                SourceFormat = format,
                BlockCount = blocks.Count,
            };
        }

        /// <summary>
        /// Rdzeń parsowania: buduje model z bloków reprezentacji pośredniej niezależnie od formatu
        /// źródłowego (DOCX/TXT/PDF), bez klasyfikacji dokumentu i koperty wyniku.
        ///
        /// Opcjonalny obserwator dostaje każdy blok razem z kontekstem PO jego przetworzeniu —
        /// wyłącznie dla narzędzi diagnostycznych (CorpusValidator), które muszą przypisać wynik
        /// budowy konkretnemu blokowi. Nie wpływa na wynik; dzięki niemu narzędzie mierzy ten sam
        /// potok, a nie jego kopię.
        /// </summary>
        internal static LegalDocument ParseBlocks(
            IReadOnlyList<DocumentBlock> blocks,
            ParseOptions? options = null,
            DocumentClassificationResult? classification = null,
            Action<DocumentBlock, ParsingContext>? observeBlock = null)
        {
            var document = new LegalDocument { Classification = classification };

            JournalHeaderParser.Apply(blocks, document);

            var subchapter = GetDefaultSubchapter(document);

            var context = new ParsingContext(document, subchapter)
            {
                SectionIsBasicUnit = Services.UnitReferences.SectionSignRole.IsBasicUnit(classification),
            };
            var orchestrator = new ParserOrchestrator();

            // Tekst jednolity: bloki sprzed wiersza „Załącznik do obwieszczenia …" są treścią obwieszczenia,
            // nie aktu, który ono ogłasza.
            var actStart = AnnouncementBoundary.Find(blocks);

            for (var i = 0; i < blocks.Count; i++)
            {
                // Parytet z klasyfikatorem (Normalize): null-element listy jest pomijany,
                // a nie wysadza potoku — publiczne Parse(blocks) przyjmuje listy budowane ręcznie.
                if (blocks[i] is not { } block)
                    continue;

                if (i < actStart)
                    orchestrator.CollectAnnouncementBlock(block, context);
                else if (i == actStart)
                    orchestrator.BeginAnnouncedAct(block, context);
                else
                    orchestrator.ProcessBlock(block, context);

                observeBlock?.Invoke(block, context);
            }

            // Finalizacja — wypróżnienie bufora nowelizacji jeśli dokument
            // kończy się wewnątrz treści nowelizacji
            orchestrator.Finalize(context);

            return document;
        }

        private static Subchapter GetDefaultSubchapter(LegalDocument document)
        {
            var part = document.RootPart;
            var book = part.Books.First();
            var title = book.Titles.First();
            var division = title.Divisions.First();
            var chapter = division.Chapters.First();
            return chapter.Subchapters.First();
        }

    }
}
