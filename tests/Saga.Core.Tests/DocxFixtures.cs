using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Word = DocumentFormat.OpenXml.Wordprocessing;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Buduje w pamięci pakiety DOCX dla testów, które muszą przejść przez PRAWDZIWY adapter
    /// OpenXml. Nagłówek strony leży w osobnej części pakietu (word/headerN.xml), więc — inaczej
    /// niż treść — nie da się go podstawić ręcznie zbudowanym <c>DocumentBlock</c>.
    /// </summary>
    internal static class DocxFixtures
    {
        /// <summary>Jedna część nagłówkowa pakietu: typ odsyłacza + akapity.</summary>
        internal readonly record struct HeaderSpec(Word.HeaderFooterValues Type, IReadOnlyList<string> Lines)
        {
            /// <summary>Wiersze podane jako surowy XML akapitu (<c>&lt;w:p …&gt;</c>), nie jako tekst.</summary>
            internal bool IsRawXml { get; init; }
        }

        internal static HeaderSpec Header(Word.HeaderFooterValues type, params string[] lines)
            => new(type, lines);

        internal static HeaderSpec RawHeader(Word.HeaderFooterValues type, params string[] paragraphXml)
            => new(type, paragraphXml) { IsRawXml = true };

        /// <summary>Dokument z nagłówkiem pierwszej strony (typ First — układ plików Dz.U.).</summary>
        internal static byte[] WithFirstPageHeader(IEnumerable<string> headerLines, params string[] bodyLines)
            => Build(new[] { Header(Word.HeaderFooterValues.First, headerLines.ToArray()) }, bodyLines);

        /// <summary>
        /// Dokument z nagłówkiem domyślnym — Word zapisuje nagłówek tak, gdy w sekcji nie
        /// włączono „innej strony tytułowej".
        /// </summary>
        internal static byte[] WithDefaultHeader(IEnumerable<string> headerLines, params string[] bodyLines)
            => Build(new[] { Header(Word.HeaderFooterValues.Default, headerLines.ToArray()) }, bodyLines);

        /// <summary>Dokument bez części nagłówkowej — projekt ustawy, akt nieogłoszony.</summary>
        internal static byte[] WithoutHeader(params string[] bodyLines)
            => Build(Array.Empty<HeaderSpec>(), bodyLines);

        /// <summary>Dokument z dowolnym zestawem części nagłówkowych.</summary>
        internal static byte[] WithHeaders(IEnumerable<HeaderSpec> headers, params string[] bodyLines)
            => Build(headers.ToArray(), bodyLines);

        /// <summary>
        /// Nagłówek w układzie ZAOBSERWOWANYM w korpusie RCL (lokalny katalog <c>documents/</c>,
        /// 240 z 241 plików). Trzy pułapki naraz, wszystkie z prawdziwych plików:
        ///
        /// 1. trzy części nagłówkowe — pusta dla stron parzystych, żywa pagina jako domyślna
        ///    i dopiero TRZECIA z winietą, podpięta jako <c>first</c>;
        /// 2. wartości daty ogłoszenia i pozycji zawinięte w kontrolki zawartości (<c>w:sdt</c>),
        ///    więc tekst akapitu powstaje ze sklejenia runów z RÓŻNYCH poziomów drzewa —
        ///    „Warszawa, dnia " + „12 stycznia 2026" + „ r.";
        /// 3. akapit sygnatury zawierający same spacje (<c>xml:space="preserve"</c>).
        /// </summary>
        internal static byte[] WithRclJournalHeader(params string[] bodyLines)
            => Build(new[]
            {
                Header(Word.HeaderFooterValues.Even),
                Header(Word.HeaderFooterValues.Default, "Dziennik Ustaw\t–\t1\t–\tPoz.\t26"),
                RawHeader(Word.HeaderFooterValues.First,
                    RclParagraph("RCLSygnatura", @"<w:r><w:t xml:space=""preserve"">     </w:t></w:r>"),
                    RclParagraph("RCLTytuDU1", @"<w:r><w:t>DZIENNIK USTAW</w:t></w:r>"),
                    RclParagraph("RCLTytuDU2", @"<w:r><w:t>RZECZYPOSPOLITEJ POLSKIEJ</w:t></w:r>"),
                    RclParagraph("RCLDataogoszeniaaktu",
                        @"<w:r><w:t xml:space=""preserve"">Warszawa, dnia </w:t></w:r>" +
                        Sdt("Data ogłoszenia", "12 stycznia 2026") +
                        @"<w:r><w:t xml:space=""preserve""> r.</w:t></w:r>"),
                    RclParagraph("RCLPozycjaaktu",
                        @"<w:r><w:t xml:space=""preserve"">Poz. </w:t></w:r>" +
                        Sdt("Pozycja", "26"))),
            }, bodyLines);

        private const string WordNamespace =
            @"xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main""";

        private static string RclParagraph(string styleId, string content)
            => $@"<w:p {WordNamespace}><w:pPr><w:pStyle w:val=""{styleId}""/></w:pPr>{content}</w:p>";

        /// <summary>Kontrolka zawartości (content control) — tak RCL osadza wartości w szablonie.</summary>
        private static string Sdt(string alias, string value)
            => $@"<w:sdt><w:sdtPr><w:alias w:val=""{alias}""/><w:text/></w:sdtPr>" +
               $@"<w:sdtContent><w:r><w:t>{value}</w:t></w:r></w:sdtContent></w:sdt>";

        private static byte[] Build(IReadOnlyList<HeaderSpec> headers, string[] bodyLines)
        {
            using var stream = new MemoryStream();
            using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
            {
                var main = document.AddMainDocumentPart();
                var body = new Word.Body(bodyLines.Select(TextParagraph).ToArray<OpenXmlElement>());

                if (headers.Count > 0)
                {
                    var section = new Word.SectionProperties();

                    foreach (var spec in headers)
                    {
                        var headerPart = main.AddNewPart<HeaderPart>();
                        headerPart.Header = new Word.Header(spec.Lines
                            .Select(line => spec.IsRawXml ? new Word.Paragraph(line) : TextParagraph(line))
                            .ToArray<OpenXmlElement>());

                        section.AppendChild(new Word.HeaderReference
                        {
                            Type = spec.Type,
                            Id = main.GetIdOfPart(headerPart),
                        });
                    }

                    body.AppendChild(section);
                }

                main.Document = new Word.Document(body);
            }

            return stream.ToArray();
        }

        private static Word.Paragraph TextParagraph(string text)
            => new(new Word.Run(new Word.Text(text) { Space = SpaceProcessingModeValues.Preserve }));

        // ============================================================
        // Treść z surowego XML
        // ============================================================

        /// <summary>
        /// Dokument o treści podanej jako XML wnętrza <c>w:body</c> — do konstrukcji, których nie da się
        /// zapisać wierszami tekstu (tabele, scalenia komórek).
        /// </summary>
        internal static byte[] WithBodyXml(string bodyInnerXml)
        {
            using var stream = new MemoryStream();
            using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
            {
                var main = document.AddMainDocumentPart();
                main.Document = new Word.Document(
                    $@"<w:document xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main""><w:body>{bodyInnerXml}</w:body></w:document>");
            }

            return stream.ToArray();
        }

        // ============================================================
        // Przypisy
        // ============================================================

        /// <summary>Styl znakowy indeksu górnego z szablonu aktu.</summary>
        private const string SuperscriptStyle = "IGindeksgrny";

        /// <summary>
        /// Dokument z przypisami w układzie plików Dz.U. W wierszu treści „{n}" to odwołanie do n-tego
        /// przypisu (z nawiasem w indeksie górnym, jak w aktach), a „{tekst}" — sam tekst w indeksie
        /// górnym („{, }" między dwoma odwołaniami). Identyfikatory <c>w:id</c> zaczynają się od 2:
        /// wcześniejsze Word oddaje separatorom, więc id NIE jest numerem przypisu.
        /// </summary>
        internal static byte[] WithFootnotes(IReadOnlyList<string> footnotes, params string[] bodyLines)
        {
            using var stream = new MemoryStream();
            using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
            {
                var main = document.AddMainDocumentPart();
                main.Document = new Word.Document(new Word.Body(bodyLines.Select(FootnotedParagraph).ToArray<OpenXmlElement>()));

                var part = main.AddNewPart<FootnotesPart>();
                part.Footnotes = new Word.Footnotes(
                    new Word.Footnote(new Word.Paragraph()) { Id = -1, Type = Word.FootnoteEndnoteValues.Separator },
                    new Word.Footnote(new Word.Paragraph()) { Id = 0, Type = Word.FootnoteEndnoteValues.ContinuationSeparator },
                    new Word.Footnote(new Word.Paragraph()) { Id = 1, Type = Word.FootnoteEndnoteValues.ContinuationNotice });

                for (var i = 0; i < footnotes.Count; i++)
                {
                    part.Footnotes.AppendChild(new Word.Footnote(new Word.Paragraph(
                        new Word.ParagraphProperties(new Word.ParagraphStyleId { Val = "ODNONIKtreodnonika" }),
                        Superscript(new Word.FootnoteReferenceMark()),
                        Superscript(new Word.Text(")")),
                        new Word.Run(new Word.Text(footnotes[i]) { Space = SpaceProcessingModeValues.Preserve })))
                    {
                        Id = FootnoteId(i + 1),
                    });
                }
            }

            return stream.ToArray();
        }

        private static long FootnoteId(int number) => number + 1;

        private static Word.Run Superscript(OpenXmlElement content)
            => new(new Word.RunProperties(new Word.RunStyle { Val = SuperscriptStyle }), content);

        private static Word.Paragraph FootnotedParagraph(string line)
        {
            var paragraph = new Word.Paragraph();
            foreach (var part in System.Text.RegularExpressions.Regex.Split(line, @"(\{[^}]*\})"))
            {
                if (part.Length == 0)
                    continue;

                if (!part.StartsWith('{'))
                {
                    paragraph.AppendChild(new Word.Run(new Word.Text(part) { Space = SpaceProcessingModeValues.Preserve }));
                    continue;
                }

                var inner = part[1..^1];
                if (int.TryParse(inner, out var number))
                {
                    paragraph.AppendChild(Superscript(new Word.FootnoteReference { Id = FootnoteId(number) }));
                    paragraph.AppendChild(Superscript(new Word.Text(")")));
                }
                else
                {
                    paragraph.AppendChild(Superscript(new Word.Text(inner) { Space = SpaceProcessingModeValues.Preserve }));
                }
            }

            return paragraph;
        }
    }
}
