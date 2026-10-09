using Saga.Core.Ingest;
using Saga.Model;
using Saga.Model.SystematizingUnits;

namespace Saga.Core.Tests
{
    /// <summary>Komunikaty o numeracji i hierarchii jednostek systematyzacyjnych na gotowym drzewie.</summary>
    public class SystematizingUnitValidatorTests
    {
        private static LegalDocument Parse(params string[] lines) =>
            LegalDocumentParser.ParseBlocks(lines.Select(text => new DocumentBlock { Text = text }).ToList());

        private static List<BaseEntity> Units(LegalDocument document)
        {
            var units = new List<BaseEntity>();
            void Walk(BaseEntity unit)
            {
                if (unit is ISystematizingUnit { IsImplicit: false })
                    units.Add(unit);
                IEnumerable<BaseEntity> children = unit switch
                {
                    Part part => part.Books,
                    Book book => book.Titles,
                    Title title => title.Divisions,
                    Division division => division.Chapters,
                    Chapter chapter => chapter.Subchapters,
                    _ => [],
                };
                foreach (var child in children)
                    Walk(child);
            }

            foreach (var part in document.Parts)
                Walk(part);
            return units;
        }

        private static string[] Messages(BaseEntity unit) => unit.ValidationMessages.Select(m => m.Message).ToArray();

        [Fact]
        public void ConsecutiveAndInsertedUnits_HaveNoNumberingMessages()
        {
            var units = Units(Parse("Rozdział 1", "Ogólne", "Art. 1. Treść.", "Rozdział 2", "Szczególne", "Art. 2. Treść.",
                "Rozdział 2a", "Dodane", "Art. 2a. Treść.", "Rozdział 3", "Końcowe", "Art. 3. Treść."));

            Assert.All(units, unit => Assert.Empty(unit.ValidationMessages));
        }

        [Fact]
        public void RepeatedDesignation_IsWarned()
        {
            // TJ 317145_2832571: „Rozdział 6" dwa razy z rzędu.
            var units = Units(Parse("Rozdział 6", "Finansowanie przewozów", "Rozdział 6", "Finansowanie transportu", "Art. 1. Treść."));

            Assert.Empty(units[0].ValidationMessages);
            Assert.Contains(Messages(units[1]), m => m.StartsWith("Powtórzone oznaczenie"));
        }

        [Fact]
        public void GapAndDecrease_AreWarned()
        {
            var units = Units(Parse("Rozdział 1", "A", "Art. 1. Treść.", "Rozdział 3", "B", "Art. 2. Treść.",
                "Rozdział 2", "C", "Art. 3. Treść."));

            Assert.Contains(Messages(units[1]), m => m.StartsWith("Luka w numeracji"));
            Assert.Contains(Messages(units[2]), m => m.StartsWith("Numeracja maleje"));
        }

        [Fact]
        public void NumberingRestartsUnderEachParent()
        {
            var units = Units(Parse("DZIAŁ I", "Ogólne", "Rozdział 1", "A", "Art. 1. Treść.", "Rozdział 2", "B", "Art. 2. Treść.",
                "DZIAŁ II", "Szczególne", "Rozdział 1", "C", "Art. 3. Treść."));

            Assert.All(units, unit => Assert.Empty(unit.ValidationMessages));
        }

        [Fact]
        public void UnitWithoutTitle_GetsInfo_ButNamedPartDoesNot()
        {
            var units = Units(Parse("CZĘŚĆ OGÓLNA", "Rozdział 1", "Art. 1. Treść."));

            Assert.DoesNotContain(units.OfType<Part>().Single().ValidationMessages,
                m => m.Message.StartsWith("Jednostka bez tytułu"));
            var chapter = units.OfType<Chapter>().Single();
            Assert.Contains(chapter.ValidationMessages, m => m.Level == ValidationLevel.Info && m.Message.StartsWith("Jednostka bez tytułu"));
        }

        [Fact]
        public void SubchapterOutsideChapter_IsWarned()
        {
            var subchapter = Units(Parse("Oddział 1", "Zasady", "Art. 1. Treść.")).OfType<Subchapter>().Single();

            Assert.Contains(subchapter.ValidationMessages, m => m.Level == ValidationLevel.Warning && m.Message.StartsWith("Oddział poza rozdziałem"));
        }
    }
}
