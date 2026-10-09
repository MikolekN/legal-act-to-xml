using Saga.Core.Ingest;
using Saga.Model.SystematizingUnits;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Obserwator bloków w <c>LegalDocumentParser.ParseBlocks</c> — kontrakt, na którym stoi raport
    /// <c>CorpusValidator --systematizing</c>: wywołanie raz na blok, PO jego przetworzeniu, bez wpływu
    /// na zbudowany model. Gdyby obserwator dostawał stan sprzed bloku, raport przypisywałby jednostkę
    /// poprzedniemu oznaczeniu.
    /// </summary>
    public class ParseBlocksObserverTests
    {
        private static IReadOnlyList<DocumentBlock> Blocks(params string[] lines) =>
            lines.Select(line => new DocumentBlock { Text = line }).ToList();

        [Fact]
        public void Observer_SeesEachBlockOnce_WithContextAfterProcessing()
        {
            var blocks = Blocks("Rozdział 1", "Przepisy ogólne", "Art. 1. Treść.");
            var seen = new List<(string Text, ISystematizingUnit? Pending)>();

            LegalDocumentParser.ParseBlocks(blocks,
                observeBlock: (block, context) => seen.Add((block.Text, context.PendingHeadingUnit)));

            Assert.Equal(blocks.Select(b => b.Text), seen.Select(s => s.Text));

            // Po oznaczeniu rozdział czeka na tytuł; po tytule już nie.
            var chapter = Assert.IsType<Chapter>(seen[0].Pending);
            Assert.Equal("1", chapter.Number?.Value);
            Assert.Null(seen[1].Pending);
        }

        [Fact]
        public void Observer_DoesNotChangeTheModel()
        {
            var blocks = Blocks("Rozdział 1", "Przepisy ogólne", "Art. 1. Treść.", "Art. 2. Treść.");

            var plain = LegalDocumentParser.ParseBlocks(blocks);
            var observed = LegalDocumentParser.ParseBlocks(blocks, observeBlock: (_, _) => { });

            Assert.Equal(
                ModelEquivalenceComparer.Describe(plain),
                ModelEquivalenceComparer.Describe(observed));
        }
    }
}
