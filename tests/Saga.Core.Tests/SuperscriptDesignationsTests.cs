using System.Text;
using Saga.Core.Ingest;
using Saga.Core.Services;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Indeks górny oznaczenia w TXT zapisany zwykłymi cyframi („Art. 292.") — odtwarzany z ciągu numeracji (ADR-0089).
    /// </summary>
    public class SuperscriptDesignationsTests
    {
        private static IReadOnlyList<string> Read(params string[] lines)
        {
            using var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(string.Join("\n", lines)));
            return new PlainTextBlockReader().ReadBlocks(stream).Select(block => block.Text).ToList();
        }

        [Fact]
        public void Article_ContinuingThePreviousDesignation_HasItsRestInSuperscript()
        {
            var text = Read(
                "Art. 29. Pierwszy.",
                "Art. 291. Dodany.",
                "Art. 292. Drugi dodany.",
                "Art. 30. Następny.");

            Assert.Equal(["Art. 29. Pierwszy.", "Art. 29[1]. Dodany.", "Art. 29[2]. Drugi dodany.", "Art. 30. Następny."], text);
        }

        [Fact]
        public void RegularSequence_StaysAsItIs()
        {
            var lines = new[] { "Art. 1. A.", "Art. 2. B.", "Art. 9. C.", "Art. 10. D.", "Art. 11. E.", "Art. 12. F." };

            Assert.Equal(lines, Read(lines));
        }

        [Fact]
        public void SuperscriptWithALetter_AndAfterALetteredArticle_IsRestored()
        {
            var text = Read(
                "Art. 130. A.",
                "Art. 1301. B.",
                "Art. 1301a. C.",
                "Art. 130a. D.",
                "Art. 130a1. E.");

            Assert.Equal(["Art. 130. A.", "Art. 130[1]. B.", "Art. 130[1a]. C.", "Art. 130a. D.", "Art. 130a[1]. E."], text);
        }

        [Fact]
        public void CodeParagraphs_PassagesAndPoints_FollowTheirOwnSequences()
        {
            var text = Read(
                "Art. 36. § 1. Pierwszy paragraf.",
                "§ 11. Dodany paragraf.",
                "§ 2. Drugi paragraf.",
                "Art. 37. 1. Pierwszy ustęp:",
                "1) punkt;",
                "11) dodany punkt;",
                "2) punkt.",
                "11. Dodany ustęp.",
                "2. Drugi ustęp.");

            Assert.Equal(
                ["Art. 36. § 1. Pierwszy paragraf.", "§ 1[1]. Dodany paragraf.", "§ 2. Drugi paragraf.",
                 "Art. 37. 1. Pierwszy ustęp:", "1) punkt;", "1[1]) dodany punkt;", "2) punkt.", "1[1]. Dodany ustęp.", "2. Drugi ustęp."],
                text);
        }

        [Fact]
        public void QuotedProvisions_AreLeftAsTheyAre()
        {
            // Treść cytowana przytacza jednostki innego aktu — jej numeracja nie musi być ciągła i nie zmienia ciągów aktu.
            var lines = new[]
            {
                "Art. 1. W ustawie wprowadza się zmiany:",
                "1) po art. 4 dodaje się art. 4a w brzmieniu:",
                "„Art. 4a. 1. Treść.",
                "11. Inna treść.",
                "12) punkt cytowany.”;",
                "2) art. 5 otrzymuje brzmienie:",
                "„Art. 5. Treść.”.",
                "Art. 2. Ustawa wchodzi w życie.",
            };

            Assert.Equal(lines, Read(lines));
        }

        [Fact]
        public void RestoredArticle_IsExportedWithTheSuperscriptDesignation()
        {
            var lines = new[] { "USTAWA", "z dnia 1 marca 2024 r.", "o sprawach", "Art. 1. A.", "Art. 11. B.", "Art. 2. C." };
            using var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(string.Join("\n", lines)));
            var document = LegalDocumentParser.Parse(stream, "akt.txt", new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

            var eIds = LegalDocumentConverter.Convert(document).Descendants()
                .Where(e => e.Name.LocalName == "artykul")
                .Select(e => (string?)e.Attribute("eId"))
                .ToList();

            Assert.Equal(["art_1", "art_1[1]", "art_2"], eIds.Select(eId => eId![(eId!.IndexOf("art_", StringComparison.Ordinal))..]));
        }
    }
}
