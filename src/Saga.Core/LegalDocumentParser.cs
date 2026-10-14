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
            // Cytat nowelizacji, którego nie domknięto, trwa do końca dokumentu i wchłania resztę aktu
            // zmieniającego. Widać to dopiero na końcu, więc dokument buduje się wtedy ponownie, z granicą
            // cytatu wskazaną z góry (ADR-0049). Każdy przebieg dokłada jedną nową granicę albo jest ostatni.
            var quotationBreaks = new HashSet<DocumentBlock>(ReferenceEqualityComparer.Instance);
            LegalDocument document;
            DocumentBlock? unclosedBreak;
            var publication = options?.Publication;
            do
            {
                document = BuildDocument(blocks, classification, publication, quotationBreaks, observeBlock: null, out unclosedBreak);
            }
            while (unclosedBreak is not null && quotationBreaks.Add(unclosedBreak));

            // Obserwator widzi wyłącznie przebieg, z którego powstaje wynik.
            if (observeBlock is not null)
                document = BuildDocument(blocks, classification, publication, quotationBreaks, observeBlock, out _);

            publication?.FillJournal(document);
            return document;
        }

        /// <summary>
        /// Jeden przebieg budowy modelu. <paramref name="unclosedQuotationBreak"/> to akapit, przed którym
        /// powinien był się skończyć cytat otwarty do końca dokumentu; null, gdy przebieg jest ostateczny.
        /// </summary>
        private static LegalDocument BuildDocument(
            IReadOnlyList<DocumentBlock> blocks,
            DocumentClassificationResult? classification,
            PublicationData? publication,
            IReadOnlySet<DocumentBlock> quotationBreaks,
            Action<DocumentBlock, ParsingContext>? observeBlock,
            out DocumentBlock? unclosedQuotationBreak)
        {
            var document = new LegalDocument { Classification = classification };

            JournalHeaderParser.Apply(blocks, document);
            publication?.FillAnnouncementDate(document);

            var subchapter = GetDefaultSubchapter(document);

            // Tekst jednolity: bloki sprzed wiersza „Załącznik do obwieszczenia …" są treścią obwieszczenia,
            // nie aktu, który ono ogłasza. Obwieszczenie, które aktu nie ogłasza, jest obwieszczeniem w całości:
            // za tym wierszem stoi jego załącznik.
            var layout = AnnouncementBoundary.Describe(blocks);
            var actStart = layout.IsPlainAnnouncement ? -1 : layout.AnnexStart;

            var context = new ParsingContext(document, subchapter)
            {
                // O jednostce podstawowej ogłaszanego aktu mówi też jego własny nagłówek („ROZPORZĄDZENIE"):
                // klasyfikator dokumentu waży liczbę jednostek, a krótki akt ma ich za mało.
                // Obwieszczenie zbudowane z paragrafów („§ 1. …") ma je za jednostki podstawowe z samej treści.
                SectionIsBasicUnit = Services.UnitReferences.SectionSignRole.IsBasicUnit(classification)
                    || Services.UnitReferences.SectionSignRole.IsSectionBased(layout.AnnouncedKind)
                    || (layout.IsPlainAnnouncement && layout.HasSections),
                IsPlainAnnouncement = layout.IsPlainAnnouncement,
                QuotationBreaks = quotationBreaks,
            };
            var orchestrator = new ParserOrchestrator();

            for (var i = 0; i < blocks.Count; i++)
            {
                // Parytet z klasyfikatorem (Normalize): null-element listy jest pomijany,
                // a nie wysadza potoku — publiczne Parse(blocks) przyjmuje listy budowane ręcznie.
                if (blocks[i] is not { } block)
                    continue;

                if (layout.IsPlainAnnouncement)
                {
                    // Przepisy obwieszczenia zbudowanego z paragrafów albo artykułów buduje potok aktu;
                    // treść bez nich jest treścią obwieszczenia.
                    if (layout.AnnexStart >= 0 && i >= layout.AnnexStart)
                        orchestrator.CollectAnnouncementAnnexBlock(block, context);
                    else if (layout.HasProvisions)
                        orchestrator.ProcessBlock(block, context);
                    else
                        orchestrator.CollectAnnouncementBlock(block, context);
                }
                else if (i < actStart)
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

            unclosedQuotationBreak = context.UnclosedQuotationBreak;
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
