using System.Text;
using Saga.Core.Ingest;

namespace Saga.Core.Tests
{
    /// <summary>Przedmiot aktu rozbity w TXT na kilka wierszy (ręczne przejście do nowego wiersza w DOCX).</summary>
    public class PlainTextTitleSubjectTests
    {
        private static IReadOnlyList<string> Read(params string[] lines)
        {
            using var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(string.Join("\n", lines)));
            return new PlainTextBlockReader().ReadBlocks(stream).Select(block => block.Text).ToList();
        }

        [Fact]
        public void SubjectContinuedOnLowercaseLines_IsOneBlock()
        {
            var text = Read(
                "OBWIESZCZENIE",
                "Ministra Infrastruktury",
                "z dnia 28 października 2025 r.",
                "w sprawie wysokości stawek opłat za wydanie decyzji o profesjonalnej rejestracji pojazdów, ",
                "blankietu profesjonalnego dowodu rejestracyjnego",
                "oraz znaku legalizacyjnego obowiązujących od dnia 1 stycznia 2026 r.",
                "Na podstawie art. 80zd ust. 3 ustawy ogłasza się, co następuje:",
                "§ 1. Stawka opłaty wynosi 10 zł.");

            Assert.Equal(
                "w sprawie wysokości stawek opłat za wydanie decyzji o profesjonalnej rejestracji pojazdów, blankietu profesjonalnego dowodu rejestracyjnego oraz znaku legalizacyjnego obowiązujących od dnia 1 stycznia 2026 r.",
                text[3]);
            Assert.StartsWith("Na podstawie", text[4]);
        }

        [Fact]
        public void TextAfterTheSubject_StartingUppercase_StaysSeparate()
        {
            // Preambuła ustawy i pierwszy przepis zaczynają się wielką literą albo oznaczeniem.
            var lines = new[]
            {
                "USTAWA", "z dnia 14 czerwca 2024 r.", "o ochronie sygnalistów",
                "Uznając znaczenie ochrony osób zgłaszających naruszenia prawa,", "Art. 1. Ustawa określa zasady.",
            };

            Assert.Equal(lines, Read(lines));
        }

        [Fact]
        public void DateLineWithoutAKindHeader_DoesNotJoinAnything()
        {
            // „z dnia …" poza strefą tytułową (np. w wykazie) nie otwiera przedmiotu.
            var lines = new[] { "Art. 1. Wykaz decyzji:", "z dnia 5 marca 2025 r.", "w sprawie pierwszej,", "w sprawie drugiej." };

            Assert.Equal(lines, Read(lines));
        }
    }
}
