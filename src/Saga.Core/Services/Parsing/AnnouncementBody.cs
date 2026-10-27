using System.Text.RegularExpressions;
using Saga.Core.Ingest;
using Saga.Core.Services.Classify;
using Saga.Core.Services.Classify.Document;
using Saga.Core.Services.EntryIntoForce;
using Saga.Model;
using Saga.Model.EditorialUnits;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Treść obwieszczenia, które nie ogłasza aktu i nie ma numerowanych punktów: podstawa prawna, podpisy
    /// i — gdy jest coś więcej — jeden przepis bez oznaczenia z wyliczeniem („1) … a) … – …"), dalszymi
    /// akapitami i tabelami. Przepis buduje ten sam potok co treść aktu, pod artykułem zastępczym, więc
    /// punkt, litera, tiret, część wspólna i dalsza treść jednostki znaczą tu to samo co w akcie (ADR-0044).
    /// </summary>
    internal static class AnnouncementBody
    {
        private static readonly Regex NumberedPoint = new(@"^\d+[a-z]*\.\s", RegexOptions.Compiled);

        /// <summary>
        /// Obwieszczenie zbudowane z tych bloków albo null, gdy treść ma numerowane punkty („1. …" — buduje je
        /// <see cref="AnnouncementUnits"/>) lub potok nie złożył z niej jednego przepisu; dokumentu wtedy nie zmienia.
        /// </summary>
        internal static Announcement? TryBuild(IReadOnlyList<DocumentBlock> blocks, LegalDocument document)
        {
            var content = new List<DocumentBlock>();
            var signatures = new List<ActSignature>();
            foreach (var block in blocks)
            {
                if (block.TableCell is null && ActSignatureParser.TryParse(TextOf(block), block.Layout?.ItalicText) is { } signature)
                    signatures.Add(signature);
                else
                    content.Add(block);
            }

            if (content.Any(block => block.TableCell is null && NumberedPoint.IsMatch(TextOf(block))))
                return null;

            // Podstawa prawna, gdy za nią nie stoi nic albo zwykły akapit; przed wyliczeniem i tabelą jest
            // wprowadzeniem do nich i zostaje tekstem przepisu.
            string? legalBasis = null;
            if (document.LegalBasis.Length == 0 && content.Count > 0 && IsText(content[0])
                && ZtpPatterns.LegalBasisPattern.IsMatch(TextOf(content[0]))
                && (content.Count == 1 || (IsText(content[1]) && !UnitDesignation.LooksDesignated(TextOf(content[1])))))
            {
                legalBasis = TextOf(content[0]);
                content.RemoveAt(0);
            }

            Paragraph? body = null;
            if (content.Count > 0)
            {
                body = BuildProvision(content);
                if (body is null)
                    return null;
            }

            if (legalBasis is not null)
                document.LegalBasis = legalBasis;

            foreach (var signature in signatures)
            {
                if (document.Signature is null)
                    document.Signature = signature;
                else
                    document.CoSignatures.Add(signature);
            }

            return new Announcement { AnnouncesAct = false, Body = body };
        }

        /// <summary>
        /// Przepis bez oznaczenia: pierwszy akapit jest jego tekstem, reszta przechodzi przez potok aktu.
        /// Null, gdy treść nie zaczyna się akapitem tekstu albo potok coś pominął, wszedł w nowelizację
        /// lub zbudował więcej niż jeden przepis.
        /// </summary>
        private static Paragraph? BuildProvision(IReadOnlyList<DocumentBlock> content)
        {
            if (!IsText(content[0]) || UnitDesignation.LooksDesignated(TextOf(content[0])))
                return null;

            // Artykuł zastępczy: niejawny, więc bez segmentu w eId — punkty adresują się od obwieszczenia.
            var holder = new LegalDocument();
            var context = new ParsingContext(holder, holder.RootPart.Books[0].Titles[0].Divisions[0].Chapters[0].Subchapters[0]);
            var container = new Article { IsImplicit = true };
            var body = new Paragraph { Parent = container, Article = container, IsImplicit = true };
            ParsingFactories.SetContentAndSegments(body, TextOf(content[0]));
            container.Paragraphs.Add(body);
            context.CurrentArticle = container;
            context.CurrentParagraph = body;

            var orchestrator = new ParserOrchestrator();
            foreach (var block in content.Skip(1))
                orchestrator.ProcessBlock(block, context);
            orchestrator.FlushTable(context);

            if (context.InsideAmendment || context.AmendmentCollector.IsCollecting
                || container.Paragraphs.Count != 1 || context.CurrentArticle != container)
                return null;

            var skipped = EditorialUnitWalker.SelfAndDescendants(container, includeAmendmentContent: true)
                .Any(unit => unit is IHasAmendments { Amendment: not null } or IHasAmendments { EarlierSentenceAmendments.Count: > 0 }
                    || unit.ValidationMessages.Any(message =>
                        message.Message.StartsWith(StructureProcessor.SkippedParagraphMessage, StringComparison.Ordinal)));
            return skipped ? null : body;
        }

        private static bool IsText(DocumentBlock block) => block.TableCell is null && !block.IsEmpty;

        private static string TextOf(DocumentBlock block) => block.Text.CollapseWhitespace().Trim();
    }
}
