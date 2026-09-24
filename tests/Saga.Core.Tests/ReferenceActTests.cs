namespace Saga.Core.Tests
{
    public class ReferenceActTests
    {
        private static string GetTemporaryCopyPath(string fileName)
            => TestFiles.CreateTemporaryCopy(TestFiles.GetReferenceDocPath(fileName));

        // AlwaysParse: testy referencyjne parsera niezależne od progów klasyfikatora dokumentu.
        private static Saga.Model.LegalDocument ParseDocument(string tempPath)
            => LegalDocumentParser.Parse(tempPath, new ParseOptions { Policy = ParsePolicy.AlwaysParse }).Document!;

        [DocRepoFact(TestFiles.ReferenceActFileName)]
        public void ReferenceAct_DefaultPolicy_IsRecognizedAsLegalAct()
        {
            // Strażnik fałszywego negatywu na REALNYM akcie: zmiana punktacji/progów klasyfikatora,
            // która odrzuciłaby akt referencyjny przy domyślnej polityce (Document=null, CLI exit 2),
            // musi być widoczna w siatce — testy syntetyczne tego nie łapią.
            var tempPath = GetTemporaryCopyPath(TestFiles.ReferenceActFileName);
            try
            {
                var result = LegalDocumentParser.Parse(tempPath);

                Assert.True(result.Classification.IsLegalAct,
                    $"Akt referencyjny nierozpoznany jako akt: {result.Classification.Justification}");
                Assert.NotNull(result.Document);
            }
            finally
            {
                File.Delete(tempPath);
            }
        }

        [DocRepoFact(TestFiles.ReferenceActFileName)]
        public void ReferenceAct_ContainsAtLeastOneArticle()
        {
            var tempPath = GetTemporaryCopyPath(TestFiles.ReferenceActFileName);
            try
            {
                var document = ParseDocument(tempPath);

                Assert.True(document.Articles.Any());
            }
            finally
            {
                File.Delete(tempPath);
            }
        }

        [DocRepoFact(TestFiles.ReferenceActFileName)]
        public void ReferenceAct_Article3_Paragraph1_Point8_IsNumbered()
        {
            var tempPath = GetTemporaryCopyPath(TestFiles.ReferenceActFileName);
            try
            {
                var document = ParseDocument(tempPath);

                var article = document.Articles.FirstOrDefault(a => a.Number?.Value == "3");
                Assert.NotNull(article);

                var paragraph = article!.Paragraphs.FirstOrDefault(p => p.Number?.Value == "1");
                Assert.True(paragraph != null,
                    $"Brak ust. 1 w art. 3. Treść artykułu: '{article.ContentText}'");
                Assert.False(paragraph!.IsImplicit, "Art. 3 ma kilka ustępów — ust. 1 nie może być niejawny.");

                var point = paragraph.Points.FirstOrDefault(p => p.Number?.Value == "8");
                Assert.True(point != null,
                    $"Brak pkt 8 w art. 3 ust. 1. Treść ustępu: '{paragraph.ContentText}'");
                Assert.False(string.IsNullOrWhiteSpace(point!.ContentText),
                    "Pkt 8 w art. 3 ust. 1 nie ma treści.");
            }
            finally
            {
                File.Delete(tempPath);
            }
        }
    }
}
