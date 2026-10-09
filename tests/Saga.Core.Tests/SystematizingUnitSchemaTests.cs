using Saga.Core.Ingest;
using Saga.Core.Services;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Eksport jednostek systematyzacyjnych wobec schemaPL.xsd — wszystkie sześć poziomów, jednostki
    /// niejawne pod jawnymi („-n"), tytuł na poziomie aktu i w księdze, oznaczenia z indeksem górnym
    /// i tymczasowe eId jednostek nazwanych.
    /// </summary>
    public class SystematizingUnitSchemaTests
    {
        private static IReadOnlyList<string> Problems(params string[] lines)
        {
            var xml = LegalDocumentConverter.Convert(LegalDocumentParser.ParseBlocks(
                lines.Select(text => new DocumentBlock { Text = text }).ToList()));
            var act = xml.Descendants().First(e => e.Name.LocalName == "ustawa");

            return SchemaPl.Validator.ValidateActBody(act);
        }

        [SchemaPlFact]
        public void ActBodyValidation_ReportsAnArticleStandingDirectlyInAChapter()
        {
            // Walidacja częściowa nie może przechodzić „z pustego": rozdział nie trzyma artykułów.
            var xml = LegalDocumentConverter.Convert(LegalDocumentParser.ParseBlocks(
                new[] { "Rozdział 1", "Przepisy ogólne", "Art. 1. Treść." }.Select(text => new DocumentBlock { Text = text }).ToList()));
            var act = xml.Elements().First();
            var subchapter = act.Descendants().Single(e => e.Name.LocalName == "oddzial-n");
            subchapter.ReplaceWith(subchapter.Elements());

            Assert.Contains(SchemaPl.Validator.ValidateActBody(act), problem => problem.Contains("'artykul'"));
        }

        [SchemaPlFact]
        public void AllSixLevels_WithImplicitLevelsBetween_AreSchemaValid()
        {
            var problems = Problems(
                "CZĘŚĆ I", "Przepisy ogólne",
                "KSIĘGA PIERWSZA", "Zasady",
                "TYTUŁ I", "Wstęp",
                "DZIAŁ I", "Podstawy",
                "Rozdział 1", "Zakres",
                "Oddział 1", "Definicje", "Art. 1. Treść.",
                "Oddział 2", "Pojęcia", "Art. 2. 1. Treść.", "2. Treść.",
                "Rozdział 2[1]", "Dodany", "Art. 3. Treść.",
                "DZIAŁ II", "Dalsze", "Art. 4. Treść.",
                "TYTUŁ II", "Inny", "Art. 5. Treść.",
                "CZĘŚĆ II", "Przepisy szczególne", "Rozdział 3", "Końcowe", "Art. 6. Treść.");

            Assert.Empty(problems);
        }

        [SchemaPlFact]
        public void ArticlesBeforeTheFirstChapter_AndActLevelTitle_AreSchemaValid()
        {
            var problems = Problems(
                "Art. 1. Przed rozdziałem.",
                "Rozdział 1", "Przepisy ogólne", "Art. 2. Treść.",
                "TYTUŁ II", "Tytuł bez księgi", "Art. 3. Treść.");

            Assert.Empty(problems);
        }

        [SchemaPlFact]
        public void NamedParts_WithTemporaryEIds_AreSchemaValid()
        {
            var problems = Problems("CZĘŚĆ OGÓLNA", "Rozdział I", "Zasady", "Art. 1. Treść.",
                "CZĘŚĆ SZCZEGÓLNA", "Rozdział XVI", "Przestępstwa", "Art. 117. Treść.");

            Assert.Empty(problems);
        }
    }
}
