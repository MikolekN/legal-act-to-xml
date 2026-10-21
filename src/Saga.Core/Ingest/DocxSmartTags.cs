using DocumentFormat.OpenXml;
using Word = DocumentFormat.OpenXml.Wordprocessing;

#nullable enable

namespace Saga.Core.Ingest
{
    /// <summary>
    /// Inteligentne tagi Worda 2003 (<c>w:smartTag</c>, np. „metricconverter" wokół „1 m²") nie należą do schematu, który zna
    /// OpenXml SDK: tag i wszystko w nim to elementy nieznane, więc przebiegi w środku nie są przebiegami, a ich tekst ginie.
    /// Tag nic do treści nie wnosi — zostawia się jego przebiegi, odczytane na nowo jako zwykłe.
    /// </summary>
    internal static class DocxSmartTags
    {
        private const string WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        /// <summary>Zastępuje każdy tag jego zawartością (bez właściwości tagu); tagi zagnieżdżone — od najgłębszego.</summary>
        internal static void Unwrap(OpenXmlElement? root)
        {
            if (root is null)
                return;

            var tags = root.Descendants<OpenXmlUnknownElement>()
                .Where(e => e.LocalName == "smartTag" && e.NamespaceUri == WordNamespace)
                .Reverse()
                .ToList();

            foreach (var tag in tags)
            {
                foreach (var child in tag.ChildElements.ToList())
                {
                    if (child.LocalName == "smartTagPr")
                        continue;
                    tag.InsertBeforeSelf(child is OpenXmlUnknownElement { LocalName: "r" } run
                        ? new Word.Run(run.OuterXml)
                        : child.CloneNode(true));
                }

                tag.Remove();
            }
        }
    }
}
