using System.Text;
using Saga.Core.Ingest;

namespace Saga.Core.Tests
{
    /// <summary>Ręczne przejście do nowego wiersza w akapicie DOCX, które eksport tekstu zapisał końcem wiersza.</summary>
    public class PlainTextLineBreaksTests
    {
        private static IReadOnlyList<string> Read(params string[] lines)
        {
            using var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(string.Join("\n", lines)));
            return new PlainTextBlockReader().ReadBlocks(stream).Select(block => block.Text).ToList();
        }

        [Fact]
        public void ProvisionBrokenInTheMiddleOfAWord_IsOneParagraph()
        {
            Assert.Equal(
                ["§ 1. W rozporządzeniu Ministra Finansów w sprawie szcze- gółowego zakresu danych (Dz. U. z 2024 r. poz. 13) wprowadza się następujące zmiany:",
                 "1) w § 1 pkt 1 otrzymuje brzmienie:"],
                Read("§ 1. W rozporządzeniu Ministra Finansów w sprawie szcze-",
                    "gółowego zakresu danych (Dz. U. z 2024 r. poz. 13) wprowadza się następujące zmiany:",
                    "1) w § 1 pkt 1 otrzymuje brzmienie:"));
        }

        [Fact]
        public void ProvisionBrokenMidSentence_IsOneParagraph()
        {
            Assert.Equal(
                ["2. Skierowanie wydaje się za zgodą dyrektora placówki, po zasięgnięciu opinii."],
                Read("2. Skierowanie wydaje się za zgodą ", "dyrektora placówki, po zasięgnięciu opinii."));
        }

        [Fact]
        public void LowercaseLineAfterAnEnumerationItem_StaysSeparate()
        {
            // Za pozycją wyliczenia akapit małą literą bywa częścią wspólną bez myślnika (ADR-0046).
            var lines = new[] { "Art. 5. Wynagrodzenie, w przypadku:", "1) urlopu bezpłatnego,", "2) aresztowania", "ulega proporcjonalnemu obniżeniu." };

            Assert.Equal(lines, Read(lines));
        }

        [Fact]
        public void ProvisionEndingWithPunctuation_OrFollowedByADesignation_StaysSeparate()
        {
            var lines = new[] { "Art. 5. Przepis stosuje się do:", "a) spraw wszczętych;", "Art. 6. Przepis kończy zdanie.", "ustawa stosuje się odpowiednio." };

            Assert.Equal(lines, Read(lines));
        }
    }
}
