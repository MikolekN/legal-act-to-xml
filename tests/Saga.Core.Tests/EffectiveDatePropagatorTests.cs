using Saga.Core.Services.EntryIntoForce;
using Saga.Model;
using Saga.Model.EditorialUnits;

namespace Saga.Core.Tests
{
    /// <summary>
	/// Kolorowanie drzewa parą dat (wejście w życie + moc wsteczna).
    /// Podział ról: <c>EntryIntoForceParseIntegrationTests</c> pilnuje, czy klauzula prowadzi do
    /// właściwej mapy wyjątków; ten plik — co propagator robi z mapą już gotową. Dzięki temu da się
    /// tu ustawić kształty, których nie sposób wyprodukować z tekstu (zatrucie, kolizja wyjątków).
    /// </summary>
    public class EffectiveDatePropagatorTests
    {
        private static readonly DateTime General = new(2026, 1, 1);
        private static readonly DateTime Exception = new(2027, 1, 1);
        private static readonly DateTime Retroactive = new(2025, 7, 1);
        private static readonly DateTime ExceptionRetroactive = new(2024, 3, 1);

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

            EffectiveDatePropagator.Propagate(document, new EntryDates(General, null), new EntryIntoForceExceptionMap());

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
            map.MarkUnit(paragraph, new EntryDates(Exception, null));

            EffectiveDatePropagator.Propagate(document, new EntryDates(General, null), map);

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
            map.MarkUnit(paragraph, new EntryDates(null, null));

            EffectiveDatePropagator.Propagate(document, new EntryDates(General, null), map);

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
            map.MarkUnit(paragraph, new EntryDates(null, null));
            map.MarkUnit(point, new EntryDates(Exception, null));

            EffectiveDatePropagator.Propagate(document, new EntryDates(General, null), map);

            Assert.Equal(default, paragraph.EffectiveDate);
            Assert.Equal(Exception, point.EffectiveDate);
        }

        [Fact]
        public void SentenceException_DoesNotTouchItsUnit()
        {
            var document = BuildTree();
            var paragraph = document.Articles.Single().Paragraphs.Single();

            var map = new EntryIntoForceExceptionMap();
            map.MarkSegment(paragraph.TextSegments[1], new EntryDates(Exception, null));

            EffectiveDatePropagator.Propagate(document, new EntryDates(General, null), map);

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

            EffectiveDatePropagator.Propagate(document, new EntryDates(General, null), new EntryIntoForceExceptionMap());

            // Treść cytatu jest poddrzewem odpiętym od RootPart — bez wejścia w nią zostałaby bez dat.
            Assert.Equal(General, quoted.EffectiveDate);
            Assert.Equal(General, quotedParagraph.EffectiveDate);
            Assert.Equal(General, quotedParagraph.TextSegments.Single().EffectiveDate);

            // Nowelizacja wchodzi w życie razem ze zdaniem, które ją przytacza (ADR-0121).
            Assert.Equal(General, point.Amendment.EffectiveDate);
        }

        private static (Paragraph Host, Article Quoted) WithQuotedArticle(LegalDocument document)
        {
            var paragraph = document.Articles.Single().Paragraphs.Single();
            var quoted = new Article { Number = new EntityNumber { Value = "85a" } };
            quoted.Paragraphs.Add(new Paragraph { Number = new EntityNumber { Value = "1" } });

            var content = new AmendmentContent();
            content.Articles.Add(quoted);
            paragraph.Amendment = new Amendment { Content = content };
            return (paragraph, quoted);
        }

        [Fact]
        public void ExceptionOnTheQuotingSentence_ReachesTheAmendmentAndItsQuotedContent_NotTheUnit()
        {
            var document = BuildTree();
            var (paragraph, quoted) = WithQuotedArticle(document);

            // Cytat stoi w ostatnim zdaniu jednostki — tam dopisuje go eksport.
            var map = new EntryIntoForceExceptionMap();
            map.MarkSegment(paragraph.TextSegments[1], new EntryDates(Exception, ExceptionRetroactive));

            EffectiveDatePropagator.Propagate(document, new EntryDates(General, null), map);

            Assert.Equal(General, paragraph.EffectiveDate);
            Assert.Equal(General, paragraph.Points.Single().EffectiveDate);
            Assert.Equal(Exception, paragraph.Amendment!.EffectiveDate);
            Assert.Equal(ExceptionRetroactive, paragraph.Amendment.RetroactiveFrom);
            Assert.Equal(Exception, quoted.EffectiveDate);
            Assert.Equal(Exception, quoted.Paragraphs.Single().EffectiveDate);
            Assert.Equal(ExceptionRetroactive, quoted.Paragraphs.Single().RetroactiveFrom);
        }

        [Fact]
        public void ExceptionOnAnEarlierSentence_LeavesTheQuotedContentWithTheUnit()
        {
            var document = BuildTree();
            var (paragraph, quoted) = WithQuotedArticle(document);

            var map = new EntryIntoForceExceptionMap();
            map.MarkSegment(paragraph.TextSegments[0], new EntryDates(Exception, null));

            EffectiveDatePropagator.Propagate(document, new EntryDates(General, Retroactive), map);

            Assert.Equal(General, paragraph.Amendment!.EffectiveDate);
            Assert.Equal(Retroactive, paragraph.Amendment.RetroactiveFrom);
            Assert.Equal(General, quoted.Paragraphs.Single().EffectiveDate);
            Assert.Equal(Retroactive, quoted.Paragraphs.Single().RetroactiveFrom);
        }

        [Fact]
        public void UnresolvedGeneralDate_LeavesEverythingBlank()
        {
            var document = BuildTree();

            EffectiveDatePropagator.Propagate(document, new EntryDates(null, null), new EntryIntoForceExceptionMap());

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

            EffectiveDatePropagator.Propagate(document, new EntryDates(Exception, null), new EntryIntoForceExceptionMap());
            EffectiveDatePropagator.Propagate(document, new EntryDates(General, null), new EntryIntoForceExceptionMap());

            Assert.Equal(General, paragraph.EffectiveDate);
            Assert.All(paragraph.TextSegments, s => Assert.Equal(General, s.EffectiveDate));

            EffectiveDatePropagator.Propagate(document, new EntryDates(null, null), new EntryIntoForceExceptionMap());
            Assert.Equal(default, paragraph.EffectiveDate);
        }

        [Fact]
        public void ConflictingExceptions_KeepTheFirst_AndAreReported()
        {
            var document = BuildTree();
            var paragraph = document.Articles.Single().Paragraphs.Single();

            var map = new EntryIntoForceExceptionMap();
            map.MarkUnit(paragraph, new EntryDates(Exception, null));
            map.MarkUnit(paragraph, new EntryDates(new DateTime(2028, 1, 1), null));

            EffectiveDatePropagator.Propagate(document, new EntryDates(General, null), map);

            Assert.Equal(Exception, paragraph.EffectiveDate);
            Assert.Single(map.Conflicts);
        }
        // ============================================================
        // Moc wsteczna
        // ============================================================

        [Fact]
        public void GeneralRetroactivity_ReachesEveryUnitAndEverySentence()
        {
            var document = BuildTree();

            EffectiveDatePropagator.Propagate(
                document, new EntryDates(General, Retroactive), new EntryIntoForceExceptionMap());

            var paragraph = document.Articles.Single().Paragraphs.Single();

            // Moc wsteczna jedzie tą samą drogą co data wejścia w życie i NIE podmienia jej w modelu.
            Assert.Equal(General, paragraph.EffectiveDate);
            Assert.Equal(Retroactive, document.RootPart.RetroactiveFrom);
            Assert.Equal(Retroactive, document.Articles.Single().RetroactiveFrom);
            Assert.Equal(Retroactive, paragraph.RetroactiveFrom);
            Assert.Equal(Retroactive, OnlyPoint(document).RetroactiveFrom);
            Assert.Equal(Retroactive, paragraph.CommonParts.Single().RetroactiveFrom);
            Assert.All(paragraph.TextSegments, s => Assert.Equal(Retroactive, s.RetroactiveFrom));
        }

        [Fact]
        public void Retroactivity_WithoutEntryDate_IsStillPropagated()
        {
            // Kształt z korpusu: „wchodzi w życie z dniem ogłoszenia, z mocą od dnia …" bez znanej
            // daty ogłoszenia. Data wejścia w życie nierozstrzygnięta, moc wsteczna jawna.
            var document = BuildTree();

            EffectiveDatePropagator.Propagate(
                document, new EntryDates(null, Retroactive), new EntryIntoForceExceptionMap());

            var paragraph = document.Articles.Single().Paragraphs.Single();
            Assert.Equal(default, paragraph.EffectiveDate);
            Assert.Equal(Retroactive, paragraph.RetroactiveFrom);
        }

        [Fact]
        public void Exception_WithoutRetroactivity_ClearsTheInheritedOne()
        {
            // Sedno niezależności składników: jednostka wyjęta z reguły ogólnej nie może zabrać
            // ze sobą mocy wstecznej TEJ reguły. Gdyby moc wsteczna dziedziczyła się osobno,
            // art. 1 ust. 1 dostałby datę wsteczną, której nikt mu nie nadał.
            var document = BuildTree();
            var paragraph = document.Articles.Single().Paragraphs.Single();

            var map = new EntryIntoForceExceptionMap();
            map.MarkUnit(paragraph, new EntryDates(Exception, null));

            EffectiveDatePropagator.Propagate(document, new EntryDates(General, Retroactive), map);

            Assert.Equal(Retroactive, document.Articles.Single().RetroactiveFrom);
            Assert.Equal(Exception, paragraph.EffectiveDate);
            Assert.Equal(default, paragraph.RetroactiveFrom);
            Assert.Equal(default, OnlyPoint(document).RetroactiveFrom);
            Assert.All(paragraph.TextSegments, s => Assert.Equal(default, s.RetroactiveFrom));
        }

        [Fact]
        public void Exception_CarriesItsOwnRetroactivity_IntoTheSubtree()
        {
            var document = BuildTree();
            var paragraph = document.Articles.Single().Paragraphs.Single();

            var map = new EntryIntoForceExceptionMap();
            map.MarkUnit(paragraph, new EntryDates(Exception, ExceptionRetroactive));

            EffectiveDatePropagator.Propagate(document, new EntryDates(General, Retroactive), map);

            Assert.Equal(Retroactive, document.Articles.Single().RetroactiveFrom);
            Assert.Equal(ExceptionRetroactive, paragraph.RetroactiveFrom);
            Assert.Equal(ExceptionRetroactive, OnlyPoint(document).RetroactiveFrom);
            Assert.All(paragraph.TextSegments, s => Assert.Equal(ExceptionRetroactive, s.RetroactiveFrom));
        }

        [Fact]
        public void SentenceException_CarriesBothDates()
        {
            var document = BuildTree();
            var paragraph = document.Articles.Single().Paragraphs.Single();

            var map = new EntryIntoForceExceptionMap();
            map.MarkSegment(paragraph.TextSegments[1], new EntryDates(Exception, ExceptionRetroactive));

            EffectiveDatePropagator.Propagate(document, new EntryDates(General, Retroactive), map);

            Assert.Equal(Retroactive, paragraph.RetroactiveFrom);
            Assert.Equal(Retroactive, paragraph.TextSegments[0].RetroactiveFrom);
            Assert.Equal(ExceptionRetroactive, paragraph.TextSegments[1].RetroactiveFrom);
            Assert.Equal(Exception, paragraph.TextSegments[1].EffectiveDate);
        }

        [Fact]
        public void Retroactivity_ReachesTheAmendmentAndItsQuotedContent()
        {
            var document = BuildTree();
            var point = OnlyPoint(document);

            var quoted = new Article { Number = new EntityNumber { Value = "85a" } };
            quoted.Paragraphs.Add(new Paragraph { Number = new EntityNumber { Value = "1" } });

            var content = new AmendmentContent();
            content.Articles.Add(quoted);
            point.Amendment = new Amendment { Content = content };

            EffectiveDatePropagator.Propagate(
                document, new EntryDates(General, Retroactive), new EntryIntoForceExceptionMap());

            Assert.Equal(Retroactive, point.Amendment.RetroactiveFrom);
            Assert.Equal(Retroactive, quoted.RetroactiveFrom);
            Assert.Equal(Retroactive, quoted.Paragraphs.Single().RetroactiveFrom);
        }

        [Fact]
        public void RetroactivityWrite_IsTotal_LikeTheEntryDate()
        {
            // Przebieg bez mocy wstecznej musi ZDJĄĆ tę zapisaną poprzednim przebiegiem.
            var document = BuildTree();
            var paragraph = document.Articles.Single().Paragraphs.Single();

            EffectiveDatePropagator.Propagate(
                document, new EntryDates(General, Retroactive), new EntryIntoForceExceptionMap());
            Assert.Equal(Retroactive, paragraph.RetroactiveFrom);

            EffectiveDatePropagator.Propagate(
                document, new EntryDates(General, null), new EntryIntoForceExceptionMap());

            Assert.Equal(default, paragraph.RetroactiveFrom);
            Assert.All(paragraph.TextSegments, s => Assert.Equal(default, s.RetroactiveFrom));
        }

        [Fact]
        public void RetroactivityOnlyException_LeavesTheEntryDateInherited()
        {
            // Kształt „z tym że art. 2 z mocą od dnia …": nadpisany jest WYŁĄCZNIE kanał mocy
            // wstecznej, więc jednostka dalej bierze datę wejścia w życie z reguły ogólnej.
            var document = BuildTree();
            var paragraph = document.Articles.Single().Paragraphs.Single();

            var map = new EntryIntoForceExceptionMap();
            map.MarkUnitRetroactivity(paragraph, ExceptionRetroactive);

            EffectiveDatePropagator.Propagate(document, new EntryDates(General, Retroactive), map);

            Assert.Equal(General, paragraph.EffectiveDate);
            Assert.Equal(ExceptionRetroactive, paragraph.RetroactiveFrom);

            // Poddrzewo dziedziczy oba składniki tak, jak je zastało na rodzicu.
            Assert.Equal(General, OnlyPoint(document).EffectiveDate);
            Assert.Equal(ExceptionRetroactive, OnlyPoint(document).RetroactiveFrom);

            // Rodzeństwo nietknięte.
            Assert.Equal(Retroactive, document.Articles.Single().RetroactiveFrom);
        }

        [Fact]
        public void RetroactivityOnlyException_OnASentence_LeavesTheUnitAlone()
        {
            var document = BuildTree();
            var paragraph = document.Articles.Single().Paragraphs.Single();

            var map = new EntryIntoForceExceptionMap();
            map.MarkSegmentRetroactivity(paragraph.TextSegments[1], ExceptionRetroactive);

            EffectiveDatePropagator.Propagate(document, new EntryDates(General, Retroactive), map);

            Assert.Equal(Retroactive, paragraph.RetroactiveFrom);
            Assert.Equal(Retroactive, paragraph.TextSegments[0].RetroactiveFrom);
            Assert.Equal(ExceptionRetroactive, paragraph.TextSegments[1].RetroactiveFrom);
            Assert.Equal(General, paragraph.TextSegments[1].EffectiveDate);
        }

        [Fact]
        public void ConflictingRetroactivity_IsReportedSeparatelyFromTheEntryDate()
        {
            // Wyjątki zgodne co do daty wejścia w życie, sprzeczne co do mocy wstecznej: sprzeczność
            // dotyczy jednego składnika, więc zgłoszenie ma być jedno, nie dwa.
            var document = BuildTree();
            var paragraph = document.Articles.Single().Paragraphs.Single();

            var map = new EntryIntoForceExceptionMap();
            map.MarkUnit(paragraph, new EntryDates(Exception, ExceptionRetroactive));
            map.MarkUnit(paragraph, new EntryDates(Exception, Retroactive));

            EffectiveDatePropagator.Propagate(document, new EntryDates(General, null), map);

            Assert.Equal(ExceptionRetroactive, paragraph.RetroactiveFrom);
            var conflict = Assert.Single(map.Conflicts);
            Assert.Contains("moc wsteczna", conflict);
        }
    }
}
