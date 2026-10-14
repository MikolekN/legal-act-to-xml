using System.Xml.Linq;
using Saga.Core.Services.Parsing;
using static Saga.Core.XLinqWrappers;
using Odnosnik = Saga.Core.Services.Converters.XmlConverterConstants.Contents.Odnosnik;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Treść odnośnika — własnego (<c>odnosnik</c>) i cytowanego (<c>odnosnik-cyt</c>): pierwszy akapit jako <c>p</c>,
    /// a za nim jednostki odnośnika (<see cref="FootnoteContent"/>, ADR-0078). Wprowadzenie do wyliczenia i jego
    /// jednostki to fragmenty jednego zdania, jak w treści aktu.
    /// </summary>
    internal static class FootnoteContentXml
    {
        private const string ListIntro = "ListIntro";

        /// <summary>
        /// Wypełnia element odnośnika treścią i zwraca jego pierwszy <c>p</c>. Treść, której jednostkami zapisać się
        /// nie da, zostaje jednym <c>p</c> — kolejne akapity dołączają do niego zdaniami.
        /// </summary>
        internal static XElement Fill(XElement footnote, IReadOnlyList<string> paragraphs)
        {
            if (FootnoteContent.Read(paragraphs) is not { } units)
            {
                var paragraph = Element(XmlConverterConstants.Contents.TextSection);
                var order = 0;
                foreach (var sentence in paragraphs.SelectMany(ParsingFactories.SplitIntoSentences))
                    paragraph.Add(SentenceContext.ToXml(text: sentence.Text, number: ++order));
                if (!paragraph.HasElements)
                    paragraph.Add(SentenceContext.ToXml(text: string.Empty));
                footnote.Add(paragraph);
                return paragraph;
            }

            var (first, context) = Text(paragraphs[0], null, opensEnumeration: units[0].Kind == FootnoteContent.Kind.Point);
            footnote.Add(first);
            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                switch (unit.Kind)
                {
                    case FootnoteContent.Kind.Point:
                        footnote.Add(Point(unit, context));
                        break;
                    case FootnoteContent.Kind.CommonPart:
                        footnote.Add(CommonPart(Odnosnik.PointsCommonPartElement, unit, context));
                        context = null;
                        break;
                    default:
                        var opens = i + 1 < units.Count && units[i + 1].Kind == FootnoteContent.Kind.Point;
                        var (text, next) = Text(unit.Text, null, opens);
                        footnote.Add(Element(Odnosnik.FragmentElement, text));
                        context = next;
                        break;
                }
            }

            return first;
        }

        private static XElement Point(FootnoteContent.Unit point, SentenceContext? context)
        {
            var (text, inner) = Text(point.Text, context, opensEnumeration: point.Units.FirstOrDefault()?.Kind == FootnoteContent.Kind.Letter);
            var element = Element(Odnosnik.PointElement, Element(XmlConverterConstants.Contents.Num, point.Number!), text);
            foreach (var unit in point.Units)
            {
                element.Add(unit.Kind == FootnoteContent.Kind.Letter
                    ? Letter(unit, inner)
                    : CommonPart(Odnosnik.LettersCommonPartElement, unit, inner));
            }

            return element;
        }

        private static XElement Letter(FootnoteContent.Unit letter, SentenceContext? context)
        {
            var (text, inner) = Text(letter.Text, context, opensEnumeration: letter.Units.FirstOrDefault()?.Kind == FootnoteContent.Kind.Tiret);
            var element = Element(Odnosnik.LetterElement, Element(XmlConverterConstants.Contents.Num, letter.Number!), text);
            foreach (var unit in letter.Units)
            {
                element.Add(unit.Kind == FootnoteContent.Kind.Tiret
                    ? Element(Odnosnik.TiretElement, Text(unit.Text, inner, opensEnumeration: false).Paragraph)
                    : CommonPart(Odnosnik.TiretsCommonPartElement, unit, inner));
            }

            return element;
        }

        // Część wspólna jest kolejnym fragmentem zdania otwartego wprowadzeniem do wyliczenia.
        private static XElement CommonPart(string name, FootnoteContent.Unit commonPart, SentenceContext? context)
        {
            var paragraph = Element(XmlConverterConstants.Contents.TextSection);
            if (context is not null)
            {
                context.CurrentFragment += 1;
                paragraph.Add(SentenceContext.ToXml(context, commonPart.Text));
            }
            else
            {
                paragraph.Add(SentenceContext.ToXml(text: commonPart.Text));
            }

            return Element(name, paragraph);
        }

        /// <summary>
        /// Akapit jednostki: zdania, z których pierwsze ciągnie zdanie wprowadzenia (<paramref name="context"/>), a ostatnie
        /// — gdy pod jednostką stoi wyliczenie — samo je wprowadza. Zwraca kontekst zdania dla jednostek niższego poziomu.
        /// </summary>
        private static (XElement Paragraph, SentenceContext? Context) Text(string text, SentenceContext? context, bool opensEnumeration)
        {
            var segments = ParsingFactories.SplitIntoSentences(text);
            if (opensEnumeration && segments.Count > 0)
                segments[^1].Role = ListIntro;

            var paragraph = Element(XmlConverterConstants.Contents.TextSection);
            var inner = SentenceProcessor.Process(segments, context, paragraph);
            if (!paragraph.HasElements)
                paragraph.Add(SentenceContext.ToXml(text: string.Empty));
            return (paragraph, inner);
        }
    }
}
