using System.Text.RegularExpressions;
using System.Xml.Linq;
using Saga.Model;
using static Saga.Core.XLinqWrappers;

namespace Saga.Core.Services.Converters
{
    /// <summary>
    /// Tabela treści w eksporcie: <c>tabela</c> → <c>tbody</c> → <c>tr</c> → <c>td</c>, a w komórce akapity
    /// jako elementy typowane (<c>tytul-kolumny</c>, <c>liczba</c>, <c>wyliczenie-N</c>, <c>tekst-zwykly</c>).
    /// </summary>
    internal static class TableXmlConverter
    {
        // Sama liczba w komórce: „7", „1 000", „12,50".
        private static readonly Regex Number = new(@"^-?\d+(?:[  .]\d{3})*(?:,\d+)?$", RegexOptions.Compiled);

        // Pozycja wyliczenia w komórce — rodzaj znacznika: punkt („1)"), litera („a)"), tiret („–").
        private static readonly Regex[] EnumerationMarkers =
        [
            new(@"^\d+[a-z]*\)\s", RegexOptions.Compiled),
            new(@"^[a-z]{1,2}\)\s", RegexOptions.Compiled),
            new($@"^[{Dashes.Any}]\s", RegexOptions.Compiled),
        ];

        private static readonly string[] EnumerationLevels =
        [
            XmlConverterConstants.Table.Enumeration1,
            XmlConverterConstants.Table.Enumeration2,
            XmlConverterConstants.Table.Enumeration3,
            XmlConverterConstants.Table.Enumeration4,
        ];

        /// <summary>Element <c>tabela</c> albo null, gdy tabela nie ma ani jednej komórki.</summary>
        internal static XElement? ToXml(ContentTable table)
        {
            // Wiersz złożony z samych dalszych ciągów komórek scalonych nie ma własnych komórek, a „tr" ich wymaga.
            var rows = table.Rows.Where(row => row.Cells.Count > 0).Select(ToRow).ToList();
            if (rows.Count == 0)
                return null;

            var element = Element(XmlConverterConstants.Table.Element, Guid(),
                Element(XmlConverterConstants.Table.Body, rows));

            // Schemat ma na objaśnienia jeden element, więc akapity legendy stoją w nim jeden za drugim.
            if (table.Description.Count > 0)
                element.Add(Element(XmlConverterConstants.Table.Description, string.Join(" ", table.Description)));

            return element;
        }

        private static XElement ToRow(ContentTableRow row)
        {
            var element = Element(XmlConverterConstants.Table.Row);
            if (row.IsHeader)
                element.Add(new XAttribute(XmlConverterConstants.Table.HeaderAttribute, XmlConverterConstants.Table.HeaderValue));
            element.Add(Guid());
            element.Add(row.Cells.Select(cell => ToCell(cell, row.IsHeader)));
            return element;
        }

        private static XElement ToCell(ContentTableCell cell, bool isHeader)
        {
            var element = Element(XmlConverterConstants.Table.Cell);
            if (cell.ColumnSpan > 1)
                element.Add(new XAttribute(XmlConverterConstants.Table.ColspanAttribute, cell.ColumnSpan));
            if (cell.RowSpan > 1)
                element.Add(new XAttribute(XmlConverterConstants.Table.RowspanAttribute, cell.RowSpan));
            element.Add(Guid());

            // Pusta komórka niesie pusty akapit — jak w przykładach autorów schematu.
            if (cell.Paragraphs.Count == 0)
                element.Add(Element(XmlConverterConstants.Table.PlainText, Guid()));

            // Poziom wyliczenia liczy się w komórce: pierwszy rodzaj znacznika to poziom 1, kolejny nowy — 2.
            var markersSeen = new List<Regex>();
            foreach (var paragraph in cell.Paragraphs)
                element.Add(Element(ContentElement(paragraph, isHeader, markersSeen), Guid(), paragraph));

            return element;
        }

        private static string ContentElement(string paragraph, bool isHeader, List<Regex> markersSeen)
        {
            if (isHeader)
                return XmlConverterConstants.Table.ColumnTitle;

            if (Number.IsMatch(paragraph))
                return XmlConverterConstants.Table.Number;

            if (EnumerationMarkers.FirstOrDefault(marker => marker.IsMatch(paragraph)) is not { } found)
                return XmlConverterConstants.Table.PlainText;

            if (!markersSeen.Contains(found))
                markersSeen.Add(found);

            return EnumerationLevels[Math.Min(markersSeen.IndexOf(found), EnumerationLevels.Length - 1)];
        }

        private static XAttribute Guid() => new(XmlConverterConstants.Contents.GUID, System.Guid.NewGuid().ToString());
    }
}
