using Saga.Core.Services.Classify;
using Saga.Core.Services.UnitReferences;
using Saga.Model;
using Saga.Model.EditorialUnits;
using Saga.Model.SystematizingUnits;

namespace Saga.Core.Tests
{
    /// <summary>Jedno źródło kształtu hierarchii jednostek systematyzacyjnych.</summary>
    public class SystematizingHierarchyTests
    {
        [Fact]
        public void EveryLevel_CreatesItsUnit_WithTheTablePrefix_AndRank()
        {
            for (var rank = 0; rank < SystematizingHierarchy.Levels.Count; rank++)
            {
                var level = SystematizingHierarchy.Levels[rank];
                var unit = SystematizingHierarchy.Create(level);

                Assert.Equal(level, unit.UnitType);
                Assert.True(((ISystematizingUnit)unit).IsImplicit);
                Assert.Equal(SystematizingHierarchy.EIdPrefix(level), unit.EIdPrefix);
                Assert.Equal(level, SystematizingHierarchy.LevelOfPrefix(unit.EIdPrefix));
                Assert.Equal(rank, SystematizingHierarchy.Rank(unit));
            }

            Assert.Null(SystematizingHierarchy.LevelOfPrefix("art"));
            Assert.False(SystematizingHierarchy.IsSystematizing(UnitType.Article));
        }

        [Fact]
        public void AddChild_GoesOneLevelDown_AndSubchapterHoldsArticles()
        {
            var chapter = new Chapter();
            var subchapter = new Subchapter();
            var article = new Article();

            SystematizingHierarchy.AddChild(chapter, subchapter);
            SystematizingHierarchy.AddChild(subchapter, article);

            Assert.Same(chapter, subchapter.Parent);
            Assert.Same(subchapter, article.Parent);
            Assert.Equal(new BaseEntity[] { subchapter }, SystematizingHierarchy.Children(chapter));
            Assert.Equal(new BaseEntity[] { article }, SystematizingHierarchy.Children(subchapter));
            Assert.Empty(SystematizingHierarchy.Children(article));
        }

        [Fact]
        public void SystematizingKinds_AndReferenceLevels_FollowTheHierarchyOrder()
        {
            // Kursor powołań przelicza UnitLevel na poziom hierarchii po kolejności — musi się zgadzać.
            var referenceLevels = Enum.GetValues<UnitLevel>().Where(l => l is >= UnitLevel.Part and <= UnitLevel.Subchapter);
            Assert.Equal(SystematizingHierarchy.Levels.Select(l => l.ToString()), referenceLevels.Select(l => l.ToString()));

            var kinds = new[] { ParagraphKind.PartUnit, ParagraphKind.BookUnit, ParagraphKind.TitleUnit,
                ParagraphKind.DivisionUnit, ParagraphKind.ChapterUnit, ParagraphKind.SubchapterUnit };
            Assert.Equal(SystematizingHierarchy.Levels, kinds.Select(k => ParagraphClassifier.LevelOf(k)!.Value));
        }

        [Theory]
        [InlineData(UnitType.Division, "VIa", "6a")]
        [InlineData(UnitType.Chapter, "9a", "9a")]
        [InlineData(UnitType.Title, "XIII", "13")]
        public void DesignationFromACitation_ReadsLikeTheDesignationInTheAct(UnitType level, string designation, string value)
        {
            Assert.Equal(value, ParagraphClassifier.ParseSystematizingDesignation(level, designation)?.Value);
        }
    }
}
