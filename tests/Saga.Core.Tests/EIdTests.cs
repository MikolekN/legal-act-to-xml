using Saga.Model;
using Saga.Model.EditorialUnits;
using Saga.Model.SystematizingUnits;
using Xunit;

namespace Saga.Core.Tests
{
    public class EIdTests
    {
        [Fact]
        public void EId_SkipsImplicitSystematizingUnits()
        {
            var part = new Part { IsImplicit = true };
            var book = new Book { IsImplicit = true, Parent = part };
            var title = new Title { IsImplicit = true, Parent = book };
            var division = new Division { IsImplicit = true, Parent = title };
            var chapter = new Chapter { IsImplicit = true, Parent = division };
            var subchapter = new Subchapter { IsImplicit = true, Parent = chapter };

            var article = new Article
            {
                Parent = subchapter,
                Number = new EntityNumber { Value = "5", NumericPart = 5 }
            };

            var paragraph = new Paragraph
            {
                Parent = article,
                Article = article,
                Number = new EntityNumber { Value = "1", NumericPart = 1 }
            };

            Assert.Equal("art_5__ust_1", paragraph.Id);
        }

        [Fact]
        public void EId_NestedTiret_UsesLevelTypePerDepth()
        {
            // Każdy stopień tiretu ma WŁASNY levelType: tir / dwtir / trtir / cztir / pitir.
            // Wspólne „tir" na każdej głębokości sklejałoby podwójny tiret z pojedynczym
            // i eId przestawałoby jednoznacznie wskazywać jednostkę.
            var article = new Article { Number = new EntityNumber { Value = "1" } };
            var paragraph = new Paragraph { Parent = article, Article = article, IsImplicit = true };
            var point = new Point { Parent = paragraph, Number = new EntityNumber { Value = "2" } };
            var letter = new Letter { Parent = point, Number = new EntityNumber { Value = "a" } };

            var tiret = new Tiret { Parent = letter, Number = new EntityNumber { Value = "1" } };
            var doubleTiret = new Tiret { Parent = tiret, Number = new EntityNumber { Value = "2" } };
            var tripleTiret = new Tiret { Parent = doubleTiret, Number = new EntityNumber { Value = "3" } };

            Assert.Equal("art_1__pkt_2__lit_a__tir_1", tiret.Id);
            Assert.Equal("art_1__pkt_2__lit_a__tir_1__dwtir_2", doubleTiret.Id);
            Assert.Equal("art_1__pkt_2__lit_a__tir_1__dwtir_2__trtir_3", tripleTiret.Id);
        }

        [Fact]
        public void ReferencePath_AndEntityEId_UseTheSameLevelTypes()
        {
            // Powołanie („w zakresie … tiret pierwsze podwójne tiret drugie") i eId encji muszą
            // renderować się identycznie — inaczej diagnostyka wskazywałaby inną jednostkę niż wyjście.
            var reference = new StructuralReference
            {
                Article = "1", Point = "2", Letter = "a", Tiret = "1.2",
            };

            var article = new Article { Number = new EntityNumber { Value = "1" } };
            var paragraph = new Paragraph { Parent = article, Article = article, IsImplicit = true };
            var point = new Point { Parent = paragraph, Number = new EntityNumber { Value = "2" } };
            var letter = new Letter { Parent = point, Number = new EntityNumber { Value = "a" } };
            var tiret = new Tiret { Parent = letter, Number = new EntityNumber { Value = "1" } };
            var doubleTiret = new Tiret { Parent = tiret, Number = new EntityNumber { Value = "2" } };

            Assert.Equal(doubleTiret.Id, reference.ToString());
        }
    }
}