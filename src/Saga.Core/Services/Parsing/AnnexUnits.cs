using System.Text.RegularExpressions;
using Saga.Core.Ingest;
using Saga.Core.Services.Classify;
using Saga.Core.Services.EntryIntoForce;
using Saga.Model;
using Saga.Model.EditorialUnits;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Wyliczenie w treści załącznika jako jednostki redakcyjne („I. … 1) … a) …") adresowane od załącznika
    /// (<c>zal_4__ust_I__pkt_1</c>). Akapity przechodzą przez ten sam potok co treść aktu, pod artykułem
    /// zastępczym, który sam do adresu nie wchodzi.
    /// </summary>
    internal static class AnnexUnits
    {
        // Wyliczenie najwyższego poziomu w załączniku bywa oznaczone liczbą rzymską („I. System…") — spoza ZTP.
        // W treści aktu takiego oznaczenia NIE rozpoznajemy: wielka litera z kropką to tam częściej inicjał.
        private static readonly Regex RomanDesignation = new(
            @"^(?<number>[IVXLCDM]+)\.\s+(?<text>\S.*)$", RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly ParagraphClassifier Classifier = new();
        private static readonly EntityNumberService Numbers = new();

        /// <summary>
        /// Jednostki najwyższego poziomu zbudowane z akapitów załącznika albo null, gdy treść nie jest w całości
        /// wyliczeniem — wtedy zostaje akapitami, żeby nic z niej nie zginęło. Załącznik bez numeru nie ma
        /// adresu, od którego adresy jednostek mogłyby się zacząć („pkt_1" wyglądałby jak punkt samego aktu),
        /// więc jego treść też zostaje akapitami.
        /// </summary>
        internal static List<Paragraph>? TryBuild(IReadOnlyList<DocumentBlock> blocks, string annexId)
        {
            if (annexId.Length == 0)
                return null;

            var texts = blocks.Select(block => block.Text.CollapseWhitespace().Trim()).ToList();
            if (texts.Count == 0 || !blocks.Zip(texts).All(pair => IsUnit(pair.First, pair.Second)))
                return null;

            // Artykuł zastępczy: niejawny, więc bez segmentu w eId, a adres jednostek zaczyna od załącznika.
            var document = new LegalDocument();
            var context = new ParsingContext(document, document.RootPart.Books[0].Titles[0].Divisions[0].Chapters[0].Subchapters[0])
            {
                StyleOnlyUnitsStayUnits = true,
            };
            var container = new Article { IsImplicit = true, AddressAnchor = annexId };
            context.CurrentArticle = container;

            var orchestrator = new ParserOrchestrator();
            for (var i = 0; i < blocks.Count; i++)
            {
                if (RomanDesignation.Match(texts[i]) is { Success: true } roman)
                    StartRomanParagraph(context, container, roman);
                else
                    orchestrator.ProcessBlock(blocks[i], context);
            }

            return IsSound(context, container) ? Detached(container) : null;
        }

        private static bool IsUnit(DocumentBlock block, string text)
        {
            if (RomanDesignation.IsMatch(text))
                return true;

            var classification = Classifier.Classify(new ClassificationInput(text, block.StyleId));
            return !classification.IsAmendmentContent
                && classification.Kind is ParagraphKind.Paragraph or ParagraphKind.Point or ParagraphKind.Letter
                    or ParagraphKind.Tiret or ParagraphKind.WrapUp;
        }

        private static void StartRomanParagraph(ParsingContext context, Article container, Match roman)
        {
            var paragraph = new Paragraph
            {
                Parent = container,
                Article = container,
                Number = Numbers.Parse(roman.Groups["number"].Value),
                IsImplicit = false,
            };
            ParsingFactories.SetContentAndSegments(paragraph, roman.Groups["text"].Value);
            container.Paragraphs.Add(paragraph);

            context.CurrentParagraph = paragraph;
            context.CurrentPoint = null;
            context.CurrentLetter = null;
            context.ClearTiretStack();
        }

        /// <summary>
        /// Wynik nadaje się na jednostki, gdy potok niczego nie pominął, nie wszedł w nowelizację (przepis
        /// załącznika, który brzmi jak komenda) i każdy adres jest w załączniku jeden.
        /// </summary>
        private static bool IsSound(ParsingContext context, Article container)
        {
            if (context.InsideAmendment || context.AmendmentCollector.IsCollecting || container.Paragraphs.Count == 0)
                return false;

            var units = EditorialUnitWalker.SelfAndDescendants(container, includeAmendmentContent: true).ToList();
            if (units.Any(unit => unit is IHasAmendments { Amendment: not null }
                    || unit.ValidationMessages.Any(m => m.Message.StartsWith(StructureProcessor.SkippedParagraphMessage, StringComparison.Ordinal))))
                return false;

            var addresses = units.Where(unit => unit != container && !unit.IsImplicit).Select(unit => unit.Id).ToList();
            return addresses.Count == addresses.Distinct(StringComparer.Ordinal).Count();
        }

        // Jedyny ustęp artykułu uchodzi w modelu za niejawny; jednostka załącznika taka nie jest, więc nie
        // wskazuje artykułu zastępczego jako swojego — zostaje on tylko rodzicem niosącym adres załącznika.
        private static List<Paragraph> Detached(Article container)
        {
            foreach (var paragraph in container.Paragraphs)
                paragraph.Article = null;

            return container.Paragraphs;
        }
    }
}
