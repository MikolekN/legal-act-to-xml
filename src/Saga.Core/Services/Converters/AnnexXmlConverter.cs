using System.Xml.Linq;
using Saga.Model;
using Saga.Model.EditorialUnits;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Załącznik w eksporcie: <c>zalacznik</c> ze wskazaniem aktu (<c>zalacznik-do</c>), numerem
    /// (<c>zalacznik-nr</c>), tytułem i treścią w <c>segment-zalacznika</c> — tabele i akapity w kolejności
    /// dokumentu. Jedyny załącznik bez numeru ma adres <c>zal_1</c> (ADR-0099); załącznik bez numeru obok innych
    /// adresu nie ma i mówi dlaczego (ADR-0024).
    /// </summary>
    internal static class AnnexXmlConverter
    {
        internal static XElement ToXml(Annex annex)
        {
            var element = Element(XmlConverterConstants.Attachments.Element,
                annex.Id.Length > 0
                    ? new XAttribute(XmlConverterConstants.Contents.eId, annex.Id)
                    : new XAttribute(XmlConverterConstants.Contents.MissingEId, XmlConverterConstants.Contents.UnmarkedUnit),
                Guid());

            foreach (var line in annex.AttachedTo)
                element.Add(Element(XmlConverterConstants.Attachments.AttachedTo, Guid(), line));

            if (annex.Number is { Length: > 0 } number)
            {
                // Odnośnik przy oznaczeniu („Załącznik nr 2⁵⁾") stoi za numerem — znacznik zostaje tu tekstem,
                // odnośnikiem czyni go FootnoteXmlConverter (ADR-0094).
                var designation = Element(XmlConverterConstants.Attachments.NumberElement, Guid(),
                    Element(XmlConverterConstants.Contents.Num, number));
                if (FootnoteMarkers.Only(annex.Heading ?? string.Empty) is { Length: > 0 } markers)
                    designation.Add(new XText(markers));
                element.Add(designation);
            }

            if (annex.Title is { Length: > 0 } title)
                element.Add(Element(XmlConverterConstants.Attachments.TitleElement, Guid(), title));

            // Segment załącznika to ciąg treści na stronach jednej orientacji — zmiana orientacji zaczyna nowy segment.
            foreach (var run in Runs(annex.Content))
            {
                var content = run.SelectMany(ToContent).ToList();
                if (content.Count == 0)
                    continue;

                var segment = Element(XmlConverterConstants.Attachments.Segment, Guid(), content);
                if (run[0].IsLandscape is { } landscape)
                    segment.Add(new XAttribute(XmlConverterConstants.Attachments.OrientationAttribute,
                        landscape ? XmlConverterConstants.Attachments.OrientationLandscape : XmlConverterConstants.Attachments.OrientationPortrait));
                element.Add(segment);
            }

            return element;
        }

        private static IEnumerable<List<AnnexBlock>> Runs(IReadOnlyList<AnnexBlock> blocks)
        {
            var run = new List<AnnexBlock>();
            foreach (var block in blocks)
            {
                if (run.Count > 0 && run[0].IsLandscape != block.IsLandscape)
                {
                    yield return run;
                    run = [];
                }

                run.Add(block);
            }

            if (run.Count > 0)
                yield return run;
        }

        private static IEnumerable<XElement> ToContent(AnnexBlock block)
        {
            if (block.Table is { } table)
                return TableXmlConverter.ToXml(table) is { } element ? [element] : [];

            if (block.Unit is { } unit)
                return ToUnit(unit);

            return [Element(XmlConverterConstants.Table.PlainText, Guid(), block.Text ?? string.Empty)];
        }

        // Wyliczenie zaczęte od punktów nie ma ustępu — punkty stoją wtedy wprost w treści załącznika.
        private static IEnumerable<XElement> ToUnit(Paragraph unit)
        {
            if (ParagraphXmlConverter.ToXml(unit) is not { } element)
                return [];

            return unit.IsImplicit ? element.Elements().ToList() : [element];
        }

        private static XAttribute Guid() => new(XmlConverterConstants.Contents.GUID, System.Guid.NewGuid().ToString());
    }
}
