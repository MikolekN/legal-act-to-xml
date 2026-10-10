using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Saga.Core.Ingest;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Testy adaptera DOCX → IR (DocxBlockReader.ToBlock), w szczególności ekstrakcji
    /// metadanych układu. Layout konsumuje dziś wnioskowanie głębokości tiretu
    /// (StructureProcessor.GetTiretDepth, gdy styl 2TIR/3TIR nie rozstrzyga); tu weryfikujemy sam odczyt.
    /// </summary>
    public class DocxBlockReaderTests
    {
        private static DocumentBlock ToBlock(Paragraph paragraph)
            => DocxBlockReader.ToBlock(paragraph, blockIndex: 7);

        /// <summary>Akapit z surowego XML — do wartości spoza schematu (obce generatory, ręczna edycja).</summary>
        private static Paragraph FromXml(string bodyXml)
            => new Paragraph(
                $@"<w:p xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">{bodyXml}</w:p>");

        /// <summary>Run z tekstem i opcjonalnym formatowaniem znakowym.</summary>
        private static Run Formatted(string text, bool? bold = null, bool? italic = null, string? fontSize = null)
        {
            var runProperties = new RunProperties();
            if (bold == true) runProperties.Bold = new Bold();
            else if (bold == false) runProperties.Bold = new Bold { Val = false };
            if (italic == true) runProperties.Italic = new Italic();
            else if (italic == false) runProperties.Italic = new Italic { Val = false };
            if (fontSize != null) runProperties.FontSize = new FontSize { Val = fontSize };
            return new Run(runProperties, new Text(text));
        }

        // ============================================================
        // Parytet pól bloku (Text / StyleId / Source)
        // ============================================================

        [Fact]
        public void ToBlock_PreservesTextStyleAndIndex()
        {
            var paragraph = new Paragraph(
                new ParagraphProperties(new ParagraphStyleId { Val = "ART" }),
                new Run(new Text("Art. 5. Treść.")));

            var block = ToBlock(paragraph);

            Assert.Equal("Art. 5. Treść.", block.Text);
            Assert.Equal("ART", block.StyleId);
            Assert.Equal(7, block.Source.BlockIndex);
        }

        [Fact]
        public void ToBlock_BareParagraph_HasNullLayout()
        {
            var block = ToBlock(new Paragraph(new Run(new Text("Zwykły tekst."))));

            Assert.Null(block.Layout);
        }

        // ============================================================
        // Wcięcia
        // ============================================================

        [Fact]
        public void ToBlock_Indentation_ExtractedAsTwips()
        {
            var paragraph = new Paragraph(
                new ParagraphProperties(
                    new Indentation { Left = "708", FirstLine = "360", Hanging = "284" }),
                new Run(new Text("Treść.")));

            var layout = ToBlock(paragraph).Layout;

            Assert.NotNull(layout);
            Assert.Equal(708, layout!.LeftIndentTwips);
            Assert.Equal(360, layout.FirstLineIndentTwips);
            Assert.Equal(284, layout.HangingIndentTwips);
        }

        [Fact]
        public void ToBlock_NegativeLeftIndent_Parsed()
        {
            var paragraph = new Paragraph(
                new ParagraphProperties(new Indentation { Left = "-142" }),
                new Run(new Text("Treść.")));

            Assert.Equal(-142, ToBlock(paragraph).Layout!.LeftIndentTwips);
        }

        [Fact]
        public void ToBlock_NonIntegerTwips_Ignored()
        {
            // Miara uniwersalna (np. "0.5in") nie jest całkowitym twips — pomijamy bez błędu.
            var paragraph = new Paragraph(
                new ParagraphProperties(new Indentation { Left = "0.5in" }),
                new Run(new Text("Treść.")));

            var block = ToBlock(paragraph);

            Assert.Null(block.Layout); // brak innych sygnałów → cały Layout null
        }

        // ============================================================
        // Wyrównanie
        // ============================================================

        private static BlockAlignment? AlignmentOf(JustificationValues value)
        {
            var paragraph = new Paragraph(
                new ParagraphProperties(new Justification { Val = value }),
                new Run(new Text("Treść.")));
            return ToBlock(paragraph).Layout?.Alignment;
        }

        [Fact]
        public void ToBlock_Alignment_MappedForAllValues()
        {
            Assert.Equal(BlockAlignment.Left, AlignmentOf(JustificationValues.Left));
            Assert.Equal(BlockAlignment.Left, AlignmentOf(JustificationValues.Start));
            Assert.Equal(BlockAlignment.Center, AlignmentOf(JustificationValues.Center));
            Assert.Equal(BlockAlignment.Right, AlignmentOf(JustificationValues.Right));
            Assert.Equal(BlockAlignment.Right, AlignmentOf(JustificationValues.End));
            Assert.Equal(BlockAlignment.Justify, AlignmentOf(JustificationValues.Both));
            Assert.Equal(BlockAlignment.Justify, AlignmentOf(JustificationValues.Distribute));
        }

        // ============================================================
        // Pogrubienie / kursywa — dominanta ważona liczbą znaków
        // ============================================================

        [Fact]
        public void ToBlock_BoldRun_PresentWithoutVal_IsTrue()
        {
            var paragraph = new Paragraph(Formatted("Nowe brzmienie", bold: true));

            Assert.True(ToBlock(paragraph).Layout!.IsBold);
        }

        [Fact]
        public void ToBlock_BoldValFalse_IsFalse()
        {
            var paragraph = new Paragraph(Formatted("Zwykły", bold: false));

            Assert.False(ToBlock(paragraph).Layout!.IsBold);
        }

        [Fact]
        public void ToBlock_MixedBold_DominantByCharacterWeight()
        {
            // 14 znaków pogrubionych vs 6 niepogrubionych → dominanta = true
            var paragraph = new Paragraph(
                Formatted("Nowe brzmienie", bold: true),
                Formatted("zwykłe", bold: false));

            Assert.True(ToBlock(paragraph).Layout!.IsBold);
        }

        [Fact]
        public void ToBlock_ItalicRun_IsTrue()
        {
            var paragraph = new Paragraph(Formatted("utracił moc", italic: true));

            Assert.True(ToBlock(paragraph).Layout!.IsItalic);
        }

        [Fact]
        public void ToBlock_NoBoldSpecified_IsNull()
        {
            var paragraph = new Paragraph(new Run(new Text("Treść bez formatowania runu.")));

            Assert.Null(ToBlock(paragraph).Layout);
        }

        [Fact]
        public void ToBlock_WhitespaceOnlyBoldRun_DoesNotVote()
        {
            // Run pogrubiony zawierający tylko spacje nie głosuje (waga 0);
            // pozostały run nie deklaruje pogrubienia → dominanta null (nie true).
            var paragraph = new Paragraph(
                Formatted("   ", bold: true),
                new Run(new Text("Treść.")));

            Assert.Null(ToBlock(paragraph).Layout?.IsBold);
        }

        // ============================================================
        // Rozmiar czcionki — dominanta ważona liczbą znaków
        // ============================================================

        [Fact]
        public void ToBlock_SingleFontSize_Extracted()
        {
            var paragraph = new Paragraph(Formatted("Treść.", fontSize: "24"));

            Assert.Equal(24d, ToBlock(paragraph).Layout!.FontSizeHalfPoints);
        }

        [Fact]
        public void ToBlock_MixedFontSize_DominantByCharacterWeight()
        {
            // "Treść." (5 znaków bez kropki? liczymy niebiałe: 6) rozmiar 20;
            // "xx" rozmiar 28 → dominanta 20.
            var paragraph = new Paragraph(
                Formatted("Treść.", fontSize: "20"),
                Formatted("xx", fontSize: "28"));

            Assert.Equal(20d, ToBlock(paragraph).Layout!.FontSizeHalfPoints);
        }

        // ============================================================
        // Kombinacja sygnałów
        // ============================================================

        [Fact]
        public void ToBlock_CombinedLayout_AllFieldsPopulated()
        {
            var paragraph = new Paragraph(
                new ParagraphProperties(
                    new Justification { Val = JustificationValues.Center },
                    new Indentation { Left = "1416" }),
                Formatted("USTAWA", bold: true, fontSize: "28"));

            var layout = ToBlock(paragraph).Layout;

            Assert.NotNull(layout);
            Assert.Equal(1416, layout!.LeftIndentTwips);
            Assert.Equal(BlockAlignment.Center, layout.Alignment);
            Assert.True(layout.IsBold);
            Assert.Equal(28d, layout.FontSizeHalfPoints);
        }

        // ============================================================
        // Regresje z przeglądu adwersaryjnego Etapu 3
        // ============================================================

        [Fact]
        public void ToBlock_StartIndent_MappedToLeft()
        {
            // ISO strict / LibreOffice zapisują lewe wcięcie jako w:start zamiast w:left.
            var paragraph = new Paragraph(
                new ParagraphProperties(new Indentation { Start = "708" }),
                new Run(new Text("Treść.")));

            Assert.Equal(708, ToBlock(paragraph).Layout!.LeftIndentTwips);
        }

        [Fact]
        public void ToBlock_BoldSymbolTiretRun_Votes()
        {
            // Tiret zapisany symbolem (w:sym F02D) w pogrubionym runie musi głosować za bold —
            // wcześniej dostawał wagę 0 i nie liczył się do dominanty.
            var symbol = new SymbolChar { Font = new StringValue("Symbol"), Char = new HexBinaryValue("F02D") };
            var paragraph = new Paragraph(
                new Run(new RunProperties { Bold = new Bold() }, symbol),
                new Run(new Text(" przepis stosuje się.")));

            Assert.True(ToBlock(paragraph).Layout!.IsBold);
        }

        [Fact]
        public void ToBlock_JustificationWithoutVal_AlignmentNull()
        {
            var paragraph = new Paragraph(
                new ParagraphProperties(new Justification()),
                new Run(new Text("Treść.")));

            Assert.Null(ToBlock(paragraph).Layout?.Alignment);
        }

        [Fact]
        public void ToBlock_UnmappedButValidJustification_AlignmentNull()
        {
            // Wartość poprawna schematowo, lecz bez odpowiednika w BlockAlignment → null (bez wyjątku).
            Assert.Null(AlignmentOf(JustificationValues.ThaiDistribute));
        }

        [Fact]
        public void ToBlock_OutOfSchemaJustification_DoesNotThrow()
        {
            // w:jc z wartością spoza ST_Jc: odczyt .Value rzuciłby FormatException i przerwał
            // odczyt całego dokumentu. HasValue chroni — Layout bez wyrównania, brak wyjątku.
            var paragraph = FromXml(
                @"<w:pPr><w:jc w:val=""garbageValue""/></w:pPr><w:r><w:t>Treść.</w:t></w:r>");

            var block = ToBlock(paragraph);

            Assert.Null(block.Layout?.Alignment);
        }

        [Fact]
        public void ToBlock_OutOfSchemaBoldToken_DoesNotThrow_TreatedAsBold()
        {
            // w:b z tokenem spoza {true,false,on,off,0,1}: .Value rzuciłby FormatException.
            // Element obecny z nierozpoznanym val traktujemy jak obecność flagi (true).
            var paragraph = FromXml(
                @"<w:r><w:rPr><w:b w:val=""maybe""/></w:rPr><w:t>Treść.</w:t></w:r>");

            Assert.True(ToBlock(paragraph).Layout!.IsBold);
        }

        [Fact]
        public void ToBlock_NonFiniteFontSize_Ignored()
        {
            // w:sz val="NaN": double.TryParse przyjąłby NaN i zatruł słownik dominanty.
            // Guard IsFinite odrzuca → rozmiar pominięty (brak innego sygnału → Layout null).
            var paragraph = FromXml(
                @"<w:r><w:rPr><w:sz w:val=""NaN""/></w:rPr><w:t>Treść.</w:t></w:r>");

            Assert.Null(ToBlock(paragraph).Layout?.FontSizeHalfPoints);
        }

        [Fact]
        public void ToBlock_NegativeFontSize_Ignored()
        {
            var paragraph = FromXml(
                @"<w:r><w:rPr><w:sz w:val=""-24""/></w:rPr><w:t>Treść.</w:t></w:r>");

            Assert.Null(ToBlock(paragraph).Layout?.FontSizeHalfPoints);
        }

        // ============================================================
        // Nagłówek strony — osobna część pakietu (metadane publikatora)
        // ============================================================

        private static IReadOnlyList<DocumentBlock> ReadPackage(byte[] docx)
        {
            using var stream = new MemoryStream(docx);
            return new DocxBlockReader().ReadBlocks(stream);
        }

        [Fact]
        public void ReadBlocks_FirstPageHeader_EmittedWithPageHeaderRole()
        {
            var blocks = ReadPackage(DocxFixtures.WithFirstPageHeader(
                new[] { "DZIENNIK USTAW", "Poz. 26" }, "Art. 1. Treść."));

            var header = blocks.Where(b => b.Role == BlockRole.JournalHeader).ToList();

            Assert.Equal(new[] { "DZIENNIK USTAW", "Poz. 26" }, header.Select(b => b.Text));
        }

        [Fact]
        public void ReadBlocks_HeaderPrecedesBody_ButBodyIndexingIsUnchanged()
        {
            var blocks = ReadPackage(DocxFixtures.WithFirstPageHeader(
                new[] { "DZIENNIK USTAW" }, "Art. 1. Treść.", "Art. 2. Treść."));

            // Nagłówek idzie przed treścią, bo tak leży na stronie…
            Assert.Equal(BlockRole.JournalHeader, blocks[0].Role);
            Assert.All(blocks.Where(b => b.Role == BlockRole.JournalHeader),
                b => Assert.Null(b.Source.BlockIndex));

            // …ale numeracja bloków treści zaczyna się od zera tak samo jak w dokumencie bez
            // nagłówka. Bez tego każdy DocumentSignal.BlockIndex i każdy golden przesunąłby się
            // o długość nagłówka.
            var body = blocks.Where(b => b.Role == BlockRole.Body).ToList();
            Assert.Equal(new int?[] { 0, 1 }, body.Select(b => b.Source.BlockIndex));
        }

        [Fact]
        public void ReadBlocks_WithoutHeaderPart_ProducesOnlyBodyBlocks()
        {
            // Projekt ustawy / akt nieogłoszony — brak części nagłówkowej jest normalnym stanem.
            var blocks = ReadPackage(DocxFixtures.WithoutHeader("Art. 1. Treść."));

            Assert.All(blocks, b => Assert.Equal(BlockRole.Body, b.Role));
            Assert.Equal(0, blocks[0].Source.BlockIndex);
        }

        [Fact]
        public void ReadBlocks_EmptyHeaderParagraphs_Skipped()
        {
            var blocks = ReadPackage(DocxFixtures.WithFirstPageHeader(
                new[] { "", "   ", "DZIENNIK USTAW" }, "Art. 1. Treść."));

            var header = blocks.Where(b => b.Role == BlockRole.JournalHeader).ToList();

            Assert.Equal("DZIENNIK USTAW", Assert.Single(header).Text);
        }

        // ============================================================
        // Przypisy
        // ============================================================

        private static IReadOnlyList<DocumentBlock> Read(byte[] docx)
        {
            using var stream = new MemoryStream(docx);
            return DocumentBlockReaderFactory.Create(SourceFormat.Docx).ReadBlocks(stream);
        }

        [Fact]
        public void FootnoteReference_CarriesTheNumberPrintedInTheAct_NotTheWordId()
        {
            // Ustawa o ochronie sygnalistów: tytuł ma odnośniki 1) i 2), a w:id tych przypisów to 2 i 3.
            var blocks = Read(DocxFixtures.WithFootnotes(
                ["Niniejsza ustawa wdraża dyrektywę.", "Niniejszą ustawą zmienia się ustawy.", "Zmiany zostały ogłoszone."],
                "o ochronie sygnalistów{1}{, }{2}",
                "Art. 1. Ustawa (Dz. U. poz. 1, z późn. zm.{3}) określa zasady."));

            Assert.Equal("o ochronie sygnalistów[1), 2)]", blocks[0].Text);
            Assert.Equal("Art. 1. Ustawa (Dz. U. poz. 1, z późn. zm.[3)]) określa zasady.", blocks[1].Text);
        }

        [Fact]
        public void Block_CarriesTheFootnotesItRefersTo()
        {
            var blocks = Read(DocxFixtures.WithFootnotes(
                ["Niniejsza ustawa wdraża dyrektywę.", "Niniejszą ustawą zmienia się ustawy.", "Zmiany zostały ogłoszone."],
                "o ochronie sygnalistów{1}{, }{2}",
                "Art. 1. Ustawa określa zasady.",
                "Art. 2. Ustawa (Dz. U. poz. 1, z późn. zm.{3}) wchodzi w życie po upływie 14 dni od dnia ogłoszenia."));

            // Treść przypisu bez własnego znacznika („1)" na początku) — numer niesie Symbol.
            Assert.Equal(new[] { "1", "2" }, blocks[0].Footnotes.Select(f => f.Symbol));
            Assert.Equal("Niniejsza ustawa wdraża dyrektywę.", Assert.Single(blocks[0].Footnotes[0].Paragraphs));
            Assert.Equal("Niniejszą ustawą zmienia się ustawy.", Assert.Single(blocks[0].Footnotes[1].Paragraphs));

            Assert.Empty(blocks[1].Footnotes);
            Assert.Equal("Zmiany zostały ogłoszone.", Assert.Single(Assert.Single(blocks[2].Footnotes).Paragraphs));
        }

        [Fact]
        public void DocumentWithoutFootnotes_HasNoneOnItsBlocks()
        {
            var blocks = Read(DocxFixtures.WithoutHeader("Art. 1. Ustawa określa zasady."));

            Assert.All(blocks, block => Assert.Empty(block.Footnotes));
        }

        // ============================================================
        // Miejsce bloku w tabeli
        // ============================================================

        private static string P(string text) => $"<w:p><w:r><w:t>{text}</w:t></w:r></w:p>";

        private static string Cell(string text, string properties = "") =>
            $"<w:tc><w:tcPr>{properties}</w:tcPr>{P(text)}</w:tc>";

        private static string Row(params string[] cells) => $"<w:tr>{string.Concat(cells)}</w:tr>";

        private static string Table(params string[] rows) => $"<w:tbl>{string.Concat(rows)}</w:tbl>";

        /// <summary>Komórka tabeli każdego niepustego bloku, po jego tekście.</summary>
        private static Dictionary<string, BlockTableCell?> CellsByText(string bodyXml)
        {
            using var stream = new MemoryStream(DocxFixtures.WithBodyXml(bodyXml));
            return new DocxBlockReader().ReadBlocks(stream)
                .Where(block => !block.IsEmpty)
                .ToDictionary(block => block.Text, block => block.TableCell);
        }

        [Fact]
        public void BlockInATable_CarriesItsTableRowAndColumn()
        {
            var cells = CellsByText(
                P("Art. 1. Wykaz określa tabela.") +
                Table(Row(Cell("A1"), Cell("B1")), Row(Cell("A2"), Cell("B2"))) +
                P("Art. 2. Przepis końcowy."));

            // Akapit poza tabelą nie ma komórki.
            Assert.Null(cells["Art. 1. Wykaz określa tabela."]);
            Assert.Null(cells["Art. 2. Przepis końcowy."]);

            Assert.Equal((0, 0, 0), (cells["A1"]!.Table, cells["A1"]!.Row, cells["A1"]!.Column));
            Assert.Equal((0, 0, 1), (cells["B1"]!.Table, cells["B1"]!.Row, cells["B1"]!.Column));
            Assert.Equal((0, 1, 0), (cells["A2"]!.Table, cells["A2"]!.Row, cells["A2"]!.Column));
            Assert.Equal((0, 1, 1), (cells["B2"]!.Table, cells["B2"]!.Row, cells["B2"]!.Column));
            Assert.All(cells.Values.Where(c => c is not null), c => Assert.Equal((1, 1), (c!.ColumnSpan, c.RowSpan)));
        }

        [Fact]
        public void CellSpanningColumns_ShiftsTheColumnsAfterIt()
        {
            var cells = CellsByText(Table(
                Row(Cell("szeroka", @"<w:gridSpan w:val=""2""/>"), Cell("C1")),
                Row(Cell("A2"), Cell("B2"), Cell("C2"))));

            Assert.Equal((0, 2), (cells["szeroka"]!.Column, cells["szeroka"]!.ColumnSpan));
            // Kolumna siatki, nie numer komórki w wierszu: „C1" stoi nad „C2".
            Assert.Equal(2, cells["C1"]!.Column);
            Assert.Equal(cells["C2"]!.Column, cells["C1"]!.Column);
        }

        [Fact]
        public void CellMergedVertically_CountsItsRows_AndMarksTheContinuations()
        {
            // Word zapisuje komórkę scaloną w każdym wierszu: pierwsza zaczyna scalenie, kolejne je kontynuują.
            var cells = CellsByText(Table(
                Row(Cell("Energia", @"<w:vMerge w:val=""restart""/>"), Cell("gaz")),
                Row(Cell("ciąg 1", "<w:vMerge/>"), Cell("ropa")),
                Row(Cell("ciąg 2", @"<w:vMerge w:val=""continue""/>"), Cell("węgiel")),
                Row(Cell("Transport"), Cell("kolej"))));

            Assert.Equal(3, cells["Energia"]!.RowSpan);
            Assert.False(cells["Energia"]!.ContinuesCellAbove);
            Assert.True(cells["ciąg 1"]!.ContinuesCellAbove);
            Assert.True(cells["ciąg 2"]!.ContinuesCellAbove);
            Assert.Equal(1, cells["Transport"]!.RowSpan);
            Assert.False(cells["Transport"]!.ContinuesCellAbove);
        }

        [Fact]
        public void HeaderRow_IsMarkedOnItsCells()
        {
            var cells = CellsByText(Table(
                $"<w:tr><w:trPr><w:tblHeader/></w:trPr>{Cell("Sektor")}{Cell("Podsektor")}</w:tr>",
                Row(Cell("Energia"), Cell("gaz"))));

            Assert.True(cells["Sektor"]!.IsHeaderRow);
            Assert.True(cells["Podsektor"]!.IsHeaderRow);
            Assert.False(cells["Energia"]!.IsHeaderRow);
        }

        [Fact]
        public void NestedAndFollowingTables_HaveTheirOwnNumbers()
        {
            var inner = Table(Row(Cell("wewnętrzna")));
            var cells = CellsByText(
                Table(Row($"<w:tc>{P("zewnętrzna")}{inner}{P("")}</w:tc>", Cell("obok"))) +
                P("między tabelami") +
                Table(Row(Cell("druga"))));

            // Akapit należy do najbliższej tabeli; tabele liczone w kolejności wystąpienia.
            Assert.Equal(0, cells["zewnętrzna"]!.Table);
            Assert.Equal((1, 0, 0), (cells["wewnętrzna"]!.Table, cells["wewnętrzna"]!.Row, cells["wewnętrzna"]!.Column));
            Assert.Equal((0, 0, 1), (cells["obok"]!.Table, cells["obok"]!.Row, cells["obok"]!.Column));
            Assert.Null(cells["między tabelami"]);
            Assert.Equal(2, cells["druga"]!.Table);
        }
    }
}
