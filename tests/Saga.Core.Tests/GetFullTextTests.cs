using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Saga.Core.Tests
{
    public class GetFullTextTests
    {
        [Fact]
        public void GetFullText_PlainText_ReturnsText()
        {
            var paragraph = new Paragraph(
                new Run(new Text("Tresc akapitu.")));

            var result = paragraph.GetFullText();

            Assert.Equal("Tresc akapitu.", result);
        }

        [Fact]
        public void GetFullText_SymbolCharF02D_ReturnsEnDash()
        {
            // Symulacja: <w:sym w:font="Symbol" w:char="F02D"/>
            var sym = new SymbolChar
            {
                Font = new StringValue("Symbol"),
                Char = new HexBinaryValue("F02D")
            };
            var paragraph = new Paragraph(
                new Run(sym, new Text(" tresc po polpauzie")));

            var result = paragraph.GetFullText();

            Assert.StartsWith("\u2013", result);
            Assert.Equal("\u2013 tresc po polpauzie", result);
        }

        private static Run Superscript(string text) => new(
            new RunProperties { RunStyle = new RunStyle { Val = "IGindeksgrny" } },
            new Text(text) { Space = SpaceProcessingModeValues.Preserve });

        [Fact]
        public void GetFullText_FootnoteInsertedAsAField_StaysInOneBracket()
        {
            // TJ 307519_2779687: odnośnik wstawiony polem NOTEREF — między „8" a „)" stoi przebieg końca pola
            // bez tekstu i bez indeksu górnego. Nie może rozciąć odnośnika na „[8][)]".
            var paragraph = new Paragraph(
                new Run(new Text("17a.")),
                new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
                new Run(new FieldCode(" NOTEREF _Ref1 \\h ")),
                new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }),
                new Run(new RunProperties { RunStyle = new RunStyle { Val = "Odwoanieprzypisudolnego" } }, new Text("8")),
                new Run(new FieldChar { FieldCharType = FieldCharValues.End }),
                Superscript(")"),
                new Run(new Text(" Wojskowy sąd okręgowy") { Space = SpaceProcessingModeValues.Preserve }));

            Assert.Equal("17a.[8)] Wojskowy sąd okręgowy", paragraph.GetFullText());
        }

        [Theory]
        // Odstęp sformatowany jako indeks górny to zwykły odstęp, nie pusty indeks.
        [InlineData(" ", "4. W sprawach")]
        // Odstęp na końcu indeksu wychodzi za nawias.
        [InlineData("7) ", "4.[7)] W sprawach")]
        public void GetFullText_WhitespaceStyledAsSuperscript_StaysOutsideTheBrackets(string superscript, string expected)
        {
            var paragraph = new Paragraph(
                new Run(new Text("4.")),
                Superscript(superscript),
                new Run(new Text("W sprawach")));

            Assert.Equal(expected, paragraph.GetFullText());
        }

        [Fact]
        public void GetFullText_NonBreakingHyphen_IsAHyphen()
        {
            // Ustawa o ochronie sygnalistów: „Służby Celno<w:noBreakHyphen/>Skarbowej".
            var paragraph = new Paragraph(
                new Run(new Text("Służby Celno")),
                new Run(new NoBreakHyphen(), new Text("Skarbowej, o której mowa w pkt 1")),
                new Run(new NoBreakHyphen(), new Text("3")));

            Assert.Equal("Służby Celno-Skarbowej, o której mowa w pkt 1-3", paragraph.GetFullText());
        }

        [Fact]
        public void GetFullText_LineBreakInsideAParagraph_SeparatesWords()
        {
            var paragraph = new Paragraph(
                new Run(new Text("o zmianie ustawy o usuwaniu skutków powodzi")),
                new Run(new Break(), new Text("oraz niektórych innych ustaw")));

            Assert.Equal("o zmianie ustawy o usuwaniu skutków powodzi oraz niektórych innych ustaw", paragraph.GetFullText());
        }

        [Fact]
        public void GetFullText_SoftHyphen_LeavesTheWordWhole()
        {
            // Łącznik opcjonalny to tylko miejsce możliwego przeniesienia wyrazu.
            var paragraph = new Paragraph(new Run(new Text("admini"), new SoftHyphen(), new Text("stracja")));

            Assert.Equal("administracja", paragraph.GetFullText());
        }

        [Fact]
        public void GetFullText_FootnoteWithSuperscript_WrappedInBrackets()
        {
            // Symulacja: <w:footnoteReference w:id="36"/> + <w:rStyle val="IGindeksgrny"/><w:t>)</w:t>
            // Oba runy sa w indeksie gornym → splaszczone do [36)]
            var fnRef = new FootnoteReference { Id = 36 };
            var paragraph = new Paragraph(
                new Run(new Text("Tekst")),
                new Run(
                    new RunProperties { RunStyle = new RunStyle { Val = "Odwoanieprzypisudolnego" } },
                    fnRef),
                new Run(
                    new RunProperties { RunStyle = new RunStyle { Val = "IGindeksgrny" } },
                    new Text(")")));

            var result = paragraph.GetFullText();

            Assert.Equal("Tekst[36)]", result);
        }

        [Fact]
        public void GetFullText_SuperscriptByVerticalAlign_WrappedInBrackets()
        {
            // Run z VerticalTextAlignment=Superscript (bez stylu)
            var paragraph = new Paragraph(
                new Run(new Text("E=mc")),
                new Run(
                    new RunProperties
                    {
                        VerticalTextAlignment = new VerticalTextAlignment
                        { Val = VerticalPositionValues.Superscript }
                    },
                    new Text("2")));

            var result = paragraph.GetFullText();

            Assert.Equal("E=mc[2]", result);
        }

        [Fact]
        public void GetFullText_SuperscriptFollowedByNormalText_BracketsClose()
        {
            // Indeks gorny w srodku zdania → nawias zamyka sie przed normalnym tekstem
            var paragraph = new Paragraph(
                new Run(new Text("przypis")),
                new Run(
                    new RunProperties { RunStyle = new RunStyle { Val = "IGindeksgrny" } },
                    new Text("1)")),
                new Run(new Text(" kontynuacja.")));

            var result = paragraph.GetFullText();

            Assert.Equal("przypis[1)] kontynuacja.", result);
        }

        [Theory]
        // Cztery style indeksu górnego szablonu; oznaczenie jednostki jest pogrubione, więc jego indeks
        // idzie stylem IGP (KPC, TJ 302049_2704309: „Art. 216¹." dawało „Art. 2161.").
        [InlineData("IGindeksgrny")]
        [InlineData("IGPindeksgrnyipogrubienie")]
        [InlineData("IGKindeksgrnyikursywa")]
        [InlineData("IGPKindeksgrnyipogrubieniekursywa")]
        public void GetFullText_TemplateSuperscriptStyles_WrappedInBrackets(string style)
        {
            var paragraph = new Paragraph(
                new Run(new Text("Art. 216")),
                new Run(new RunProperties(new RunStyle { Val = style }), new Text("1")),
                new Run(new Text(". Treść.")));

            Assert.Equal("Art. 216[1]. Treść.", paragraph.GetFullText());
        }

        [Fact]
        public void GetFullText_TabChar_EmitsTab()
        {
            var paragraph = new Paragraph(
                new Run(new Text("Art. 5."), new TabChar(), new Text("Tresc artykulu.")));

            var result = paragraph.GetFullText();

            Assert.Equal("Art. 5.\tTresc artykulu.", result);
        }

        [Fact]
        public void GetFullText_WrapUpWithSymbolChar_ReturnsEnDashPrefix()
        {
            // Czesc wspolna zaczyna sie od <w:sym F02D/> zamiast literalnego en-dash
            var sym = new SymbolChar
            {
                Font = new StringValue("Symbol"),
                Char = new HexBinaryValue("F02D")
            };
            var paragraph = new Paragraph(
                new Run(sym),
                new Run(new Text(" zachowuja dopuszczenie.")));

            var result = paragraph.GetFullText();

            Assert.Equal("\u2013 zachowuja dopuszczenie.", result);
        }

        [Fact]
        public void GetFullText_MultipleRuns_ConcatenatesAll()
        {
            var paragraph = new Paragraph(
                new Run(new Text("Czesc ")),
                new Run(new Text("pierwsza i ")),
                new Run(new Text("druga.")));

            var result = paragraph.GetFullText();

            Assert.Equal("Czesc pierwsza i druga.", result);
        }

        [Fact]
        public void GetFullText_SymbolCharOtherFont_Ignored()
        {
            // SymbolChar z innym fontem niz Symbol - nie konwertujemy
            var sym = new SymbolChar
            {
                Font = new StringValue("Wingdings"),
                Char = new HexBinaryValue("F02D")
            };
            var paragraph = new Paragraph(
                new Run(sym, new Text("tekst")));

            var result = paragraph.GetFullText();

            Assert.Equal("tekst", result);
        }

        [Fact]
        public void GetFullText_SymbolCharOtherChar_Ignored()
        {
            // SymbolChar z font=Symbol ale innym kodem niz F02D - nie konwertujemy
            var sym = new SymbolChar
            {
                Font = new StringValue("Symbol"),
                Char = new HexBinaryValue("F0B7")
            };
            var paragraph = new Paragraph(
                new Run(sym, new Text("tekst")));

            var result = paragraph.GetFullText();

            Assert.Equal("tekst", result);
        }

        [Fact]
        public void GetFullText_EmptyParagraph_ReturnsEmpty()
        {
            var paragraph = new Paragraph();

            var result = paragraph.GetFullText();

            Assert.Equal(string.Empty, result);
        }

        // ============================================================
        // Wersaliki
        // ============================================================

        [Fact]
        public void GetFullText_RunFormattedAsAllCaps_IsReadInCapitals()
        {
            // Przykład autorów schematu (DU/2026/252): tytuł załącznika zapisany „sektory ważne" z wersalikami
            // nadanymi wprost — w akcie i w przykładzie stoi „SEKTORY WAŻNE".
            var paragraph = new Paragraph(
                new Run(new RunProperties(new Caps()), new Text("sektory ważne")),
                new Run(new Text(" (wykaz)")));

            Assert.Equal("SEKTORY WAŻNE (wykaz)", paragraph.GetFullText());
        }

        [Fact]
        public void GetFullText_AllCapsSwitchedOff_KeepsTheText()
        {
            var paragraph = new Paragraph(
                new Run(new RunProperties(new Caps { Val = false }), new Text("sektory ważne")));

            Assert.Equal("sektory ważne", paragraph.GetFullText());
        }

        [Fact]
        public void GetFullText_SmallCaps_KeepTheLetters()
        {
            var paragraph = new Paragraph(
                new Run(new RunProperties(new SmallCaps()), new Text("Sektory ważne")));

            Assert.Equal("Sektory ważne", paragraph.GetFullText());
        }

        [Fact]
        public void GetFullText_AllCapsOfTheStyle_KeepTheTextAsTyped()
        {
            // Styl rodzaju aktu pisze wersalikami, ale tekstem jest „Ustawa" — tak też stoi w przykładach
            // autorów schematu (<rodzaj-aktu>Ustawa</rodzaj-aktu>).
            var paragraph = new Paragraph(
                new ParagraphProperties(new ParagraphStyleId { Val = "OZNRODZAKTUtznustawalubrozporzdzenieiorganwydajcy" }),
                new Run(new Text("Ustawa")));

            Assert.Equal("Ustawa", paragraph.GetFullText());
        }
    }
}
