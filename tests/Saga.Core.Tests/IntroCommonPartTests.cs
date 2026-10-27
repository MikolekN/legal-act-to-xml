using Saga.Core.Ingest;
using Saga.Core.Services;
using Saga.Core.Services.Parsing;
using Saga.Model;
using Saga.Model.EditorialUnits;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Testy wiazania CommonPart Intro z segmentem tekstu rodzica
    /// (logika AttachIntroCommonPart).
    /// </summary>
    public class IntroCommonPartTests
    {
        [Fact]
        public void AttachIntro_SingleSegment_WholeSegmentBecomesIntro()
        {
            // Arrange: ustep z jednym zdaniem
            var paragraph = new Paragraph
            {
                ContentText = "W ustawie wprowadza się następujące zmiany:"
            };
            ParsingFactories.SetContentAndSegments(paragraph, paragraph.ContentText);

            // Act
            ParsingFactories.AttachIntroCommonPart(paragraph);

            // Assert
            Assert.Single(paragraph.CommonParts);
            var intro = paragraph.CommonParts[0];
            Assert.Equal(CommonPartType.Intro, intro.Type);
            Assert.Equal(1, intro.SourceSegmentOrder);
            Assert.Equal(paragraph.ContentText, intro.ContentText);
            Assert.Equal("ListIntro", paragraph.TextSegments[0].Role);
            Assert.Same(paragraph, intro.Parent);
        }

        [Fact]
        public void AttachIntro_MultipleSegments_LastSegmentBecomesIntro()
        {
            // Arrange: punkt z dwoma zdaniami
            var point = new Point
            {
                ContentText = "Zmienia się regulamin. W regulaminie wprowadza się zmiany:"
            };
            ParsingFactories.SetContentAndSegments(point, point.ContentText);

            // Pre-check: powinny byc 2 segmenty
            Assert.Equal(2, point.TextSegments.Count);

            // Act
            ParsingFactories.AttachIntroCommonPart(point);

            // Assert
            Assert.Single(point.CommonParts);
            var intro = point.CommonParts[0];
            Assert.Equal(CommonPartType.Intro, intro.Type);
            Assert.Equal(2, intro.SourceSegmentOrder);
            Assert.Equal(point.TextSegments[1].Text, intro.ContentText);
            Assert.Null(point.TextSegments[0].Role); // pierwsze zdanie nie jest intro
            Assert.Equal("ListIntro", point.TextSegments[1].Role);
        }

        [Fact]
        public void AttachIntro_NoSegments_DoesNothing()
        {
            // Arrange: ustep bez tekstu (implicit)
            var paragraph = new Paragraph { IsImplicit = true, ContentText = string.Empty };

            // Act
            ParsingFactories.AttachIntroCommonPart(paragraph);

            // Assert
            Assert.Empty(paragraph.CommonParts);
        }

        [Fact]
        public void AttachIntro_CalledTwice_DoesNotDuplicate()
        {
            // Arrange
            var paragraph = new Paragraph
            {
                ContentText = "Wprowadza się zmiany:"
            };
            ParsingFactories.SetContentAndSegments(paragraph, paragraph.ContentText);

            // Act
            ParsingFactories.AttachIntroCommonPart(paragraph);
            ParsingFactories.AttachIntroCommonPart(paragraph);

            // Assert: tylko jeden CommonPart
            Assert.Single(paragraph.CommonParts);
        }

        [Fact]
        public void AttachIntro_Letter_WorksForTiretParent()
        {
            // Arrange: litera z jednym zdaniem jako intro dla tiretow
            var letter = new Letter
            {
                ContentText = "obejmuje następujące elementy:"
            };
            ParsingFactories.SetContentAndSegments(letter, letter.ContentText);

            // Act
            ParsingFactories.AttachIntroCommonPart(letter);

            // Assert
            Assert.Single(letter.CommonParts);
            var intro = letter.CommonParts[0];
            Assert.Equal(CommonPartType.Intro, intro.Type);
            Assert.Same(letter, intro.Parent);
        }

        [Fact]
        public void AttachIntro_TiretParent_MarksItsIntroWithoutACommonPart()
        {
            // Tiret wprowadzający tirety podwójne nie ma części wspólnych, ale jego wprowadzenie ciągnie łańcuch
            // fragmentów zdania tak jak wprowadzenie litery (odpowiedź autorów schematu na pytanie 31).
            var tiret = new Tiret { ContentText = "w pkt 10:" };
            ParsingFactories.SetContentAndSegments(tiret, tiret.ContentText);

            ParsingFactories.AttachIntroCommonPart(tiret);

            Assert.Equal("ListIntro", tiret.TextSegments[0].Role);
        }

        [Fact]
        public void DoubleTirets_ContinueTheSentenceFragmentChain()
        {
            // Przykład DU20260000252 w repozytorium schematu zaczynał przy tiretach podwójnych nowe zdania — to błąd
            // przykładu: tirety podwójne i dalsze ciągną numerację fragmentów zdania wprowadzenia.
            string[] lines =
            [
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o zmianie ustawy o krajowym systemie cyberbezpieczeństwa",
                "Art. 1. W ustawie z dnia 5 lipca 2018 r. o krajowym systemie cyberbezpieczeństwa (Dz. U. poz. 20) wprowadza się następujące zmiany:",
                "1) w art. 26:",
                "a) w ust. 3:",
                "– w pkt 10:",
                "– – wyrazy „sektorowymi zespołami” zastępuje się wyrazami „CSIRT sektorowymi”,",
                "– – wyrazy „zagrożeniom” zastępuje się wyrazem „cyberzagrożeniom”,",
                "– w pkt 11 wyraz „zespołów” zastępuje się wyrazem „CSIRT”,",
                "b) uchyla się ust. 4;",
                "2) w art. 27 uchyla się ust. 1.",
                "Art. 2. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.",
            ];
            var xml = LegalDocumentConverter.Convert(LegalDocumentParser.Parse(
                lines.Select(line => new DocumentBlock { Text = line }).ToList(),
                new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!);

            var article = xml.Descendants().First(e => e.Name.LocalName == "artykul");
            var sentences = article.Descendants().Where(e => e.Name.LocalName == "zdanie").ToList();
            Assert.Contains(article.Descendants(), e => e.Name.LocalName == "podw-tiret");
            Assert.Single(sentences.Select(s => s.Attributes().Single(a => a.Name.LocalName == "zdGUID").Value).Distinct());
            Assert.Equal(Enumerable.Range(1, sentences.Count).Select(n => n.ToString()), sentences.Select(s => s.Attribute("fragment")!.Value));
        }

        [Fact]
        public void AttachIntro_SetsCorrectParentEId()
        {
            // Arrange: ustep z ustawionym rodzicem (artykulem) dla poprawnego eId
            var article = new Article { Number = new EntityNumber { Value = "10" } };
            var paragraph = new Paragraph
            {
                Parent = article,
                Article = article,
                Number = new EntityNumber { Value = "2" },
                ContentText = "W ustawie wprowadza się zmiany:"
            };
            ParsingFactories.SetContentAndSegments(paragraph, paragraph.ContentText);

            // Act
            ParsingFactories.AttachIntroCommonPart(paragraph);

            // Assert
            var intro = paragraph.CommonParts[0];
            Assert.Equal("art_10__ust_2", intro.ParentEId);
            // eId intro powinien byc art_10__ust_2__intro
            Assert.Equal("art_10__ust_2__intro", intro.Id);
        }

        [Fact]
        public void AttachWrapUp_KeepsTheDashAndAddsCommonPart()
        {
            // Arrange: ustep z rodzicem dla poprawnego eId
            var article = new Article { Number = new EntityNumber { Value = "293" } };
            var paragraph = new Paragraph
            {
                Parent = article,
                Article = article,
                Number = new EntityNumber { Value = "1" }
            };

            // Act
            var added = ParsingFactories.AttachWrapUpCommonPart(paragraph, "- zachowuja dopuszczenie.");

            // Assert
            Assert.True(added);
            Assert.Single(paragraph.CommonParts);
            var wrapUp = paragraph.CommonParts[0];
            Assert.Equal(CommonPartType.WrapUp, wrapUp.Type);
            // Myślnik należy do treści części wspólnej — tak zapisują ją przykłady autorów schematu.
            Assert.Equal("- zachowuja dopuszczenie.", wrapUp.ContentText);
            Assert.Equal("art_293__ust_1__czw", wrapUp.Id);
        }
    }
}
