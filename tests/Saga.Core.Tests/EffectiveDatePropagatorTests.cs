using Saga.Core.Services.EntryIntoForce;
using Saga.Model;
using Saga.Model.EditorialUnits;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Kolorowanie drzewa datą wejścia w życie — jednostkowo, na drzewie budowanym ręcznie.
    /// Podział ról: <c>EntryIntoForceParseIntegrationTests</c> pilnuje, czy klauzula prowadzi do
    /// właściwej mapy wyjątków; ten plik — co propagator robi z mapą już gotową. Dzięki temu da się
    /// tu ustawić kształty, których nie sposób wyprodukować z tekstu (zatrucie, kolizja wyjątków).
    /// </summary>
    public class EffectiveDatePropagatorTests
    {
        private static readonly DateTime General = new(2026, 1, 1);
        private static readonly DateTime Exception = new(2027, 1, 1);

        private static LegalDocument BuildTree()
        {
            var document = new LegalDocument();
            var subchapter = document.RootPart.Books[0].Titles[0].Divisions[0].Chapters[0].Subchapters[0];

            var article = new Article { Parent = subchapter, Number = new EntityNumber { Value = "1" } };
            subchapter.Articles.Add(article);

            var paragraph = new Paragraph
            {
                Parent = article, Article = article, Number = new EntityNumber { Value = "1" },
            };
            paragraph.TextSegments.Add(new TextSegment { Order = 1, Text = "Zdanie pierwsze." });
            paragraph.TextSegments.Add(new TextSegment { Order = 2, Text = "Zdanie drugie." });
            article.Paragraphs.Add(paragraph);

            var point = new Point { Parent = paragraph, Number = new EntityNumber { Value = "1" } };
            point.TextSegments.Add(new TextSegment { Order = 1, Text = "Treść punktu." });
            paragraph.Points.Add(point);
            paragraph.CommonParts.Add(new CommonPart
            {
                Parent = paragraph, Type = CommonPartType.WrapUp, ContentText = "– przy czym…",
            });

            return document;
        }

        private static Point OnlyPoint(LegalDocument document) =>
            document.Articles.Single().Paragraphs.Single().Points.Single();

        [Fact]
        public void GeneralDate_ReachesEveryUnitAndEverySentence()
        {
            var document = BuildTree();

            EffectiveDatePropagator.Propagate(document, General, new EntryIntoForceExceptionMap());

            var paragraph = document.Articles.Single().Paragraphs.Single();
            Assert.Equal(General, document.RootPart.EffectiveDate);
            Assert.Equal(General, document.Articles.Single().EffectiveDate);
            Assert.Equal(General, paragraph.EffectiveDate);
            Assert.Equal(General, OnlyPoint(document).EffectiveDate);
            Assert.Equal(General, paragraph.CommonParts.Single().EffectiveDate);
            Assert.All(paragraph.TextSegments, s => Assert.Equal(General, s.EffectiveDate));
        }

        [Fact]
        public void ResolvedException_OverridesTheUnitAndItsSubtree()
        {
            var document = BuildTree();
            var paragraph = document.Articles.Single().Paragraphs.Single();

            var map = new EntryIntoForceExceptionMap();
            map.MarkUnit(paragraph, Exception);

            EffectiveDatePropagator.Propagate(document, General, map);

            Assert.Equal(General, document.Articles.Single().EffectiveDate);
            Assert.Equal(Exception, paragraph.EffectiveDate);
            Assert.Equal(Exception, OnlyPoint(document).EffectiveDate);
            Assert.All(paragraph.TextSegments, s => Assert.Equal(Exception, s.EffectiveDate));
        }

        [Fact]
        public void UnresolvedException_PoisonsTheSubtree()
        {
            var document = BuildTree();
            var paragraph = document.Articles.Single().Paragraphs.Single();

            var map = new EntryIntoForceExceptionMap();
            map.MarkUnit(paragraph, null);

            EffectiveDatePropagator.Propagate(document, General, map);

            // Rodzic zachowuje datę ogólną, poddrzewo traci ją w całości.
            Assert.Equal(General, document.Articles.Single().EffectiveDate);
            Assert.Equal(default, paragraph.EffectiveDate);
            Assert.Equal(default, OnlyPoint(document).EffectiveDate);
            Assert.Equal(default, paragraph.CommonParts.Single().EffectiveDate);
            Assert.All(paragraph.TextSegments, s => Assert.Equal(default, s.EffectiveDate));
        }

        [Fact]
        public void DeeperException_CuresThePoisonedSubtree()
        {
            var document = BuildTree();
            var paragraph = document.Articles.Single().Paragraphs.Single();
            var point = OnlyPoint(document);

            var map = new EntryIntoForceExceptionMap();
            map.MarkUnit(paragraph, null);
            map.MarkUnit(point, Exception);

            EffectiveDatePropagator.Propagate(document, General, map);

            Assert.Equal(default, paragraph.EffectiveDate);
            Assert.Equal(Exception, point.EffectiveDate);
        }

        [Fact]
        public void SentenceException_DoesNotTouchItsUnit()
        {
            var document = BuildTree();
            var paragraph = document.Articles.Single().Paragraphs.Single();

            var map = new EntryIntoForceExceptionMap();
            map.MarkSegment(paragraph.TextSegments[1], Exception);

            EffectiveDatePropagator.Propagate(document, General, map);

            Assert.Equal(General, paragraph.EffectiveDate);
            Assert.Equal(General, paragraph.TextSegments[0].EffectiveDate);
            Assert.Equal(Exception, paragraph.TextSegments[1].EffectiveDate);
        }

        [Fact]
        public void QuotedAmendmentContent_InheritsFromItsHost()
        {
            var document = BuildTree();
            var point = OnlyPoint(document);

            var quoted = new Article { Number = new EntityNumber { Value = "85a" } };
            var quotedParagraph = new Paragraph { Number = new EntityNumber { Value = "1" } };
            quotedParagraph.TextSegments.Add(new TextSegment { Order = 1, Text = "Treść." });
            quoted.Paragraphs.Add(quotedParagraph);

            var content = new AmendmentContent();
            content.Articles.Add(quoted);
            point.Amendment = new Amendment { Content = content };

            EffectiveDatePropagator.Propagate(document, General, new EntryIntoForceExceptionMap());

            // Treść cytatu jest poddrzewem odpiętym od RootPart — bez wejścia w nią zostałaby bez dat.
            Assert.Equal(General, quoted.EffectiveDate);
            Assert.Equal(General, quotedParagraph.EffectiveDate);
            Assert.Equal(General, quotedParagraph.TextSegments.Single().EffectiveDate);

            // Nowelizacja wchodzi w życie razem z jednostką, która ją zarządza.
            Assert.Equal(General, point.Amendment.EffectiveDate);
        }

        [Fact]
        public void UnresolvedGeneralDate_LeavesEverythingBlank()
        {
            var document = BuildTree();

            EffectiveDatePropagator.Propagate(document, null, new EntryIntoForceExceptionMap());

            Assert.Equal(default, document.Articles.Single().EffectiveDate);
            Assert.All(document.Articles.Single().Paragraphs.SelectMany(p => p.TextSegments),
                s => Assert.Equal(default, s.EffectiveDate));
        }

        [Fact]
        public void Propagation_IsIdempotent_AndTotal()
        {
            // Zapis jest TOTALNY, nie „tylko gdy jest data": drugi przebieg z inną datą musi w całości
            // zastąpić wynik pierwszego, a nie dołożyć się do niego.
            var document = BuildTree();
            var paragraph = document.Articles.Single().Paragraphs.Single();

            EffectiveDatePropagator.Propagate(document, Exception, new EntryIntoForceExceptionMap());
            EffectiveDatePropagator.Propagate(document, General, new EntryIntoForceExceptionMap());

            Assert.Equal(General, paragraph.EffectiveDate);
            Assert.All(paragraph.TextSegments, s => Assert.Equal(General, s.EffectiveDate));

            EffectiveDatePropagator.Propagate(document, null, new EntryIntoForceExceptionMap());
            Assert.Equal(default, paragraph.EffectiveDate);
        }

        [Fact]
        public void ConflictingExceptions_KeepTheFirst_AndAreReported()
        {
            var document = BuildTree();
            var paragraph = document.Articles.Single().Paragraphs.Single();

            var map = new EntryIntoForceExceptionMap();
            map.MarkUnit(paragraph, Exception);
            map.MarkUnit(paragraph, new DateTime(2028, 1, 1));

            EffectiveDatePropagator.Propagate(document, General, map);

            Assert.Equal(Exception, paragraph.EffectiveDate);
            Assert.Single(map.Conflicts);
        }
    }
}
