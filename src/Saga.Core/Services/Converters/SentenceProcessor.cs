using System.Xml.Linq;
using Saga.Model;
using Serilog;

namespace Saga.Core.Services.Converters
{
    internal static class SentenceProcessor
    {
        internal static SentenceContext? Process(List<TextSegment> textSegments, SentenceContext? previousSentenceContext, XElement textSectionElement)
        {
            SentenceContext? contextToPass = null;
            Func<TextSegment, XElement>? likePrevious = null;

            for (int i = 0; i < textSegments.Count; i++)
            {
                // Drugie brzmienie tego samego zdania (ADR-0064) wychodzi tak jak pierwsze: ten sam numer,
                // ten sam fragment i to samo zdanie — także gdy otwiera wyliczenie.
                if (i > 0 && likePrevious is not null && textSegments[i].Order == textSegments[i - 1].Order)
                {
                    textSectionElement.Add(likePrevious(textSegments[i]));
                    continue;
                }

                /// <summary>
                /// Processes text segments using boolean logic.
                /// Inputs:
                ///     A if there is a context from previous sentence (if we are inside an enumeration),
                ///     B if the sentence is first on it's level,
                ///     C if the sentence is last on it's level,
                ///     D if the sentence is opening an enumeration.
                /// Outputs:
                ///     E if we continue an enumeration,
                ///     F if we start a new enumeration (create a new context),
                ///     G if we pass previous context further.
                ///</summary>
                var textSegment = textSegments[i];
                var isContextExist = previousSentenceContext != null; // A
                var isFirst = i == 0; // B
                var isLast = textSegments.Skip(i + 1).All(later => later.Order == textSegment.Order); // C
                var isIntro = textSegment.Role == "ListIntro"; // D

                if (isContextExist && isFirst && isLast && isIntro) // E!FG
                {
                    var context = previousSentenceContext!;
                    context.CurrentFragment += 1;
                    likePrevious = segment => SentenceContext.ToXml(context: context, text: segment.Text, dated: segment);
                    textSectionElement.Add(likePrevious(textSegment));
                    contextToPass = context;
                }
                else if (isContextExist && isFirst && !isIntro) // E!F!G
                {
                    var context = previousSentenceContext!;
                    context.CurrentFragment += 1;
                    likePrevious = segment => SentenceContext.ToXml(context: context, text: segment.Text, dated: segment);
                    textSectionElement.Add(likePrevious(textSegment));
                    contextToPass = null;
                }
                else if (!isIntro) // !E!F!G
                {
                    var guid = Guid.NewGuid().ToString();
                    likePrevious = segment => SentenceContext.ToXml(number: segment.Order, text: segment.Text, guid: guid, dated: segment);
                    textSectionElement.Add(likePrevious(textSegment));
                    contextToPass = null;
                }
                else if (isLast) // !EF!G
                {
                    var nextSentenceContext = new SentenceContext(
                        currentFragment: 1,
                        referenceSentenceNumber: textSegment.Order,
                        referenceSentenceGUID: Guid.NewGuid().ToString());
                    likePrevious = segment => SentenceContext.ToXml(context: nextSentenceContext, text: segment.Text, dated: segment);
                    textSectionElement.Add(likePrevious(textSegment));
                    contextToPass = nextSentenceContext;
                }
                else // a sentence that isn't last but is intro
                {
                    Log.Warning("Podczas przetwarzania fragmentów zdań wykryto fragment, który nie jest ostatni na danym poziomie, ale otwiera nowe wyliczenie.\nTextSegment(Type={type}, Text={text}, Order={order}, Role={role})", textSegment.Type.ToString(), textSegment.Text, textSegment.Order.ToString(), textSegment.Role);
                }
            }
            return contextToPass;
        }
    }
}
