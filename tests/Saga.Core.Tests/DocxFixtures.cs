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

        /// <summary>
        /// Jak <see cref="WithBodyXml"/>, z częścią stylów o podanych definicjach (<c>&lt;w:style&gt;…</c>) — do
        /// formatowania dziedziczonego ze stylu akapitu albo znaku.
        /// </summary>
        internal static byte[] WithBodyXmlAndStyles(string bodyInnerXml, string stylesInnerXml)
        {
            using var stream = new MemoryStream();
            using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
            {
                var main = document.AddMainDocumentPart();
                main.Document = new Word.Document(
                    $@"<w:document xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main""><w:body>{bodyInnerXml}</w:body></w:document>");
                main.AddNewPart<StyleDefinitionsPart>().Styles = new Word.Styles(
                    $@"<w:styles xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">{stylesInnerXml}</w:styles>");
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
        /// przypisu (z nawiasem w indeksie górnym, jak w aktach), „{n:znak}" — odwołanie z własnym znakiem
        /// (<c>w:customMarkFollows</c>) w indeksie górnym, „{n;znak}" — w zwykłym przebiegu, a „{tekst}" — sam tekst
        /// w indeksie górnym („{, }" między dwoma odwołaniami). Identyfikatory <c>w:id</c> zaczynają się od 2:
        /// wcześniejsze Word oddaje separatorom, więc id NIE jest numerem przypisu.
        /// </summary>
        internal static byte[] WithFootnotes(IReadOnlyList<string> footnotes, params string[] bodyLines) =>
            WithFootnotes(footnotes, new Word.Body(bodyLines.Select(FootnotedParagraph).ToArray<OpenXmlElement>()));

        /// <summary>
        /// Sekcja dokumentu z własnymi ustawieniami przypisów: czy wznawia numerację i jakim zapisem ją prowadzi
        /// (null = liczby). Wiersze jak w <see cref="WithFootnotes(IReadOnlyList{string}, string[])"/>.
        /// </summary>
        internal sealed record FootnoteSection(bool Restart, Word.NumberFormatValues? Format, params string[] Lines);

        /// <summary>
        /// Dokument z przypisami w kilku sekcjach. Sekcję kończy jej ostatni akapit (niesie <c>w:sectPr</c>),
        /// a ostatnią — <c>w:sectPr</c> treści, tak jak zapisuje to Word.
        /// </summary>
        internal static byte[] WithFootnoteSections(IReadOnlyList<string> footnotes, params FootnoteSection[] sections)
        {
            var body = new Word.Body();
            for (var i = 0; i < sections.Length; i++)
            {
                var paragraphs = sections[i].Lines.Select(FootnotedParagraph).ToList();
                var end = new Word.SectionProperties(FootnoteSettings(sections[i]));
                if (i < sections.Length - 1)
                    paragraphs[^1].PrependChild(new Word.ParagraphProperties(end));

                body.Append(paragraphs);
                if (i == sections.Length - 1)
                    body.AppendChild(end);
            }

            return WithFootnotes(footnotes, body);
        }

        private static Word.FootnoteProperties FootnoteSettings(FootnoteSection section)
        {
            var settings = new Word.FootnoteProperties();
            if (section.Format is { } format)
                settings.AppendChild(new Word.NumberingFormat { Val = format });
            if (section.Restart)
                settings.AppendChild(new Word.NumberingRestart { Val = Word.RestartNumberValues.EachSection });
            return settings;
        }

        /// <summary>
        /// Dokument z przypisami o treści podanej jako XML akapitów (<c>&lt;w:p&gt;…</c>, przestrzeń nazw jak w treści
        /// <see cref="WithBodyXml"/>) — do treści, której nie zapisze jeden wiersz: kilka akapitów, style akapitów
        /// przypisu, numer przypisu wpisany tekstem zamiast znaku odwołania. Wiersze treści aktu jak
        /// w <see cref="WithFootnotes(IReadOnlyList{string}, string[])"/>.
        /// </summary>
        internal static byte[] WithFootnoteXml(IReadOnlyList<string> footnoteParagraphsXml, params string[] bodyLines) =>
            WithFootnotes(
                footnoteParagraphsXml.Select(xml => new Word.Footnote($"<w:footnote {WordNamespace}>{xml}</w:footnote>")).ToList(),
                new Word.Body(bodyLines.Select(FootnotedParagraph).ToArray<OpenXmlElement>()));

        private static byte[] WithFootnotes(IReadOnlyList<string> footnotes, Word.Body body) =>
            WithFootnotes(footnotes.Select(text => new Word.Footnote(new Word.Paragraph(
                new Word.ParagraphProperties(new Word.ParagraphStyleId { Val = "ODNONIKtreodnonika" }),
                Superscript(new Word.FootnoteReferenceMark()),
                Superscript(new Word.Text(")")),
                new Word.Run(new Word.Text(text) { Space = SpaceProcessingModeValues.Preserve })))).ToList(), body);

        private static byte[] WithFootnotes(IReadOnlyList<Word.Footnote> footnotes, Word.Body body)
        {
            using var stream = new MemoryStream();
            using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
            {
                var main = document.AddMainDocumentPart();
                main.Document = new Word.Document(body);

                var part = main.AddNewPart<FootnotesPart>();
                part.Footnotes = new Word.Footnotes(
                    new Word.Footnote(new Word.Paragraph()) { Id = -1, Type = Word.FootnoteEndnoteValues.Separator },
                    new Word.Footnote(new Word.Paragraph()) { Id = 0, Type = Word.FootnoteEndnoteValues.ContinuationSeparator },
                    new Word.Footnote(new Word.Paragraph()) { Id = 1, Type = Word.FootnoteEndnoteValues.ContinuationNotice });

                for (var i = 0; i < footnotes.Count; i++)
                {
                    footnotes[i].Id = FootnoteId(i + 1);
                    part.Footnotes.AppendChild(footnotes[i]);
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
                else if (System.Text.RegularExpressions.Regex.Match(inner, @"^(?<number>\d+)(?<plain>[:;])(?<mark>.*)$") is { Success: true } custom)
                {
                    // „{n:znak}" — odwołanie z własnym znakiem w przebiegu indeksu górnego, „{n;znak}" — w zwykłym.
                    var reference = new Word.Run(
                        new Word.FootnoteReference { Id = FootnoteId(int.Parse(custom.Groups["number"].Value)), CustomMarkFollows = true },
                        new Word.Text(custom.Groups["mark"].Value) { Space = SpaceProcessingModeValues.Preserve });
                    if (custom.Groups["plain"].Value == ":")
                        reference.PrependChild(new Word.RunProperties(new Word.RunStyle { Val = SuperscriptStyle }));
                    paragraph.AppendChild(reference);
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
