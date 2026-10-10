using Saga.Core.Ingest;
using Saga.Core.Services.Classify;
using Saga.Core.Services.EntryIntoForce;
using Saga.Core.Services.Parsing;
using Saga.Core.Services.UnitReferences;
using Saga.Model;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Oznaczenia jednostek, których ZTP nie przewiduje, a akty wielokrotnie nowelizowane używają:
    /// indeks górny z literą („Art. 130[1a].") i polska litera („Art. 48ł.", „ł)"). Oznaczenie ma trafić
    /// do numeru w całości — inaczej art. 130[1a] staje się drugim art. 130, a art. 48ł drugim art. 48.
    /// </summary>
    public class NonZtpDesignationTests
    {
        private static LegalDocument Parse(params string[] lines) =>
            LegalDocumentParser.ParseBlocks(lines.Select(text => new DocumentBlock { Text = text }).ToList());

        [Fact]
        public void LetteredSuperscript_IsPartOfTheArticleNumber()
        {
            // Kodeks postępowania cywilnego (TJ 302049_2704309): art. 130[1a] stoi obok art. 130 i 130[1].
            var document = Parse(
                "Art. 130. § 1. Jeżeli pismo procesowe nie może otrzymać prawidłowego biegu, przewodniczący wzywa stronę.",
                "§ 2. Po bezskutecznym upływie terminu przewodniczący zwraca pismo stronie.",
                "Art. 130[1a]. § 1. Jeżeli pismo procesowe wniesione przez adwokata nie może otrzymać biegu, pismo zwraca się.",
                "§ 2[1a]. W zarządzeniu o zwrocie pisma wskazuje się braki.");

            Assert.Equal(new[] { "art_130", "art_130[1a]" }, document.Articles.Select(a => a.Id));

            var added = document.Articles.Last();
            Assert.Equal("130", added.Number!.Value);
            Assert.Equal("1a", added.Number.Superscript);
            Assert.Equal(new[] { "art_130[1a]__par_1", "art_130[1a]__par_2[1a]" }, added.Paragraphs.Select(p => p.Id));
            Assert.StartsWith("Jeżeli pismo procesowe wniesione", added.Paragraphs[0].ContentText);
        }

        [Theory]
        // TJ 352078_3158919 i 320264_2848048.
        [InlineData("8d[2a]. Minister właściwy do spraw gospodarki określi wzór wniosku.", ParagraphKind.Paragraph, "8d[2a]")]
        [InlineData("3[1a]) podmiot prowadzący działalność,", ParagraphKind.Point, "3[1a]")]
        public void LetteredSuperscript_IsPartOfTheNumber_OnLowerLevels(string text, ParagraphKind kind, string designation)
        {
            Assert.Equal(kind, new ParagraphClassifier().Classify(new ClassificationInput(text, null)).Kind);

            var number = kind == ParagraphKind.Paragraph
                ? ParsingFactories.ParseParagraphNumber(text)
                : ParsingFactories.ParsePointNumber(text);
            Assert.Equal(designation, number!.Designation);
        }

        [Fact]
        public void PolishLetter_IsPartOfTheNumber()
        {
            // Prawo bankowe (TJ 300754_2723313): art. 48ł stoi między art. 48l a art. 48m, lit. ł między l a m.
            // Z samego tekstu „ł)" nie jest literą (§ 56 ZTP) — w aktach niesie ją styl litery.
            var document = LegalDocumentParser.ParseBlocks(
            [
                new() { Text = "Art. 48l. Instytucja kredytowa prowadzi działalność przez oddział." },
                new() { Text = "Art. 48ł. 1. Instytucja kredytowa może rozpocząć działalność transgraniczną:" },
                new() { Text = "1) po otrzymaniu zawiadomienia:" },
                new() { Text = "l) sądu," },
                new() { Text = "ł) komornika sądowego w zakresie rachunków bankowych,", StyleId = "LIT" },
                new() { Text = "m) organu egzekucyjnego;" },
                new() { Text = "4ł) po upływie terminu." },
                new() { Text = "1ł. W przypadku odpłatnego zbycia udziałów stosuje się ust. 1." },
                new() { Text = "Art. 48m. Przepisy stosuje się odpowiednio." },
            ]);

            Assert.Equal(new[] { "48l", "48ł", "48m" }, document.Articles.Select(a => a.Number!.Value));

            var article = document.Articles.First(a => a.Number!.Value == "48ł");
            Assert.Equal(new[] { "1", "1ł" }, article.Paragraphs.Select(p => p.Number!.Value));
            Assert.StartsWith("Instytucja kredytowa może rozpocząć", article.Paragraphs[0].ContentText);

            var points = article.Paragraphs[0].Points;
            Assert.Equal(new[] { "1", "4ł" }, points.Select(p => p.Number!.Value));

            var letters = points[0].Letters;
            Assert.Equal(new[] { "l", "ł", "m" }, letters.Select(l => l.Number!.Value));
            Assert.Equal("komornika sądowego w zakresie rachunków bankowych,", letters[1].ContentText);
        }

        [Fact]
        public void PolishLetter_WithSuperscript_KeepsBoth()
        {
            // TJ 317038_2843903: „ł[1]) centrum integracji społecznej…".
            var number = ParsingFactories.ParseLetterNumber("ł[1]) centrum integracji społecznej,");

            Assert.Equal("ł", number!.Value);
            Assert.Equal("1", number.Superscript);
        }

        [Fact]
        public void PolishLetter_FollowsItsBaseLetter_InNumberingContinuity()
        {
            static EntityNumber Letter(string value) => new() { Value = value, LexicalPart = value };
            static NumberingHint After(string value) => new() { ExpectedKind = ParagraphKind.Letter, ExpectedNumber = Letter(value) };

            Assert.True(After("l").IsContinuous(Letter("ł")));
            Assert.True(After("ł").IsContinuous(Letter("m")));
            Assert.False(After("ł").IsContinuous(Letter("o")));
        }

        [Theory]
        [InlineData("Letter", "ł", true)]
        [InlineData("Letter", "Ł", false)]
        [InlineData("Article", "48ł", true)]
        public void Reference_AcceptsPolishLetterDesignations(string level, string designation, bool fits)
        {
            Assert.Equal(fits, UnitPathCursor.FitsLevel(Enum.Parse<UnitLevel>(level), designation));
        }

        [Fact]
        public void Reference_ReadsAPolishLetterDesignation()
        {
            var result = UnitEnumerationParser.Parse("art. 48ł ust. 1 pkt 1 lit. ł");

            Assert.Equal("art_48ł__ust_1__pkt_1__lit_ł", Assert.Single(result.Targets).Structure.ToString());
        }

        [Fact]
        public void FootnoteMarker_IsStillNotASuperscript()
        {
            // „[1a)]" zamyka nawias — to odnośnik, nie część oznaczenia.
            Assert.Equal("5", ParsingFactories.ParseArticleNumber("Art. 5[1a)]. Przepis z odnośnikiem.")!.Designation);
        }
    }
}
