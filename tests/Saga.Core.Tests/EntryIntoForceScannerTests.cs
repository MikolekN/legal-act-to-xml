using Saga.Core.Services.EntryIntoForce;
using Saga.Model;
using Saga.Model.EditorialUnits;
using Saga.Model.EntryIntoForce;

namespace Saga.Core.Tests
{
    /// <summary>
    /// Skaner klauzul: obchód drzewa i filtry negatywne. Filtry są tu ważniejsze niż trafienia —
    /// fałszywie rozpoznana klauzula nadpisze datę wejścia w życie całego aktu.
    /// </summary>
    public class EntryIntoForceClauseScannerTests
    {
        /// <summary>Buduje dokument z artykułów podanych jako pary (numer, treść ustępu niejawnego).</summary>
        private static LegalDocument DocumentWith(params (string Number, string Text)[] articles)
        {
            var document = new LegalDocument();
            var subchapter = document.RootPart.Books[0].Titles[0].Divisions[0].Chapters[0].Subchapters[0];

            foreach (var (number, text) in articles)
            {
                var article = new Article
                {
                    Number = new EntityNumber { Value = number },
                    ContentText = $"Art. {number}. {text}",
                    Parent = subchapter,
                };
                article.Paragraphs.Add(new Paragraph
                {
                    IsImplicit = true,
                    ContentText = text,
                    Parent = article,
                    Article = article,
                });
                subchapter.Articles.Add(article);
            }

            return document;
        }

        private static ClauseCandidate[] Scan(LegalDocument document) =>
            new EntryIntoForceClauseScanner().Scan(document).ToArray();

        [Fact]
        public void MainClause_IsFound()
        {
            var candidates = Scan(DocumentWith(
                ("1", "Wprowadza się zmiany."),
                ("2", "Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.")));

            var main = Assert.Single(candidates, c => c.Role == ClauseRole.Main);
            Assert.Equal("po upływie 14 dni od dnia ogłoszenia.", main.Tail);
        }

        [Fact]
        public void TransitionalNounPhrase_ProducesNoCandidate()
        {
            // „przed dniem wejścia w życie" — dopełniacz rzeczownika, nigdy czasownik osobowy.
            var candidates = Scan(DocumentWith(
                ("1", "Do spraw wszczętych przed dniem wejścia w życie niniejszej ustawy stosuje się przepisy dotychczasowe.")));

            Assert.Empty(candidates);
        }

        [Theory]
        [InlineData("Przepisy stosuje się od dnia wejścia w życie ustawy.")]
        [InlineData("Umowy zawarte po dniu wejścia w życie ustawy zachowują moc.")]
        [InlineData("Z dniem wejścia w życie ustawy znosi się dotychczasowy organ.")]
        public void NounPhraseVariants_ProduceNoCandidate(string text)
        {
            Assert.Empty(Scan(DocumentWith(("1", text))));
        }

        [Fact]
        public void ForeignActSubject_IsRejectedAsMain()
        {
            var candidates = Scan(DocumentWith(
                ("1", "Ustawa z dnia 15 maja 2024 r. o zmianie ustawy o pomocy obywatelom Ukrainy " +
                      "wchodzi w życie z dniem 1 stycznia 2025 r.")));

            var candidate = Assert.Single(candidates);
            Assert.Equal(ClauseRole.Stray, candidate.Role);
            Assert.Equal("podmiotem klauzuli jest inny akt", candidate.RejectionReason);
        }

        [Fact]
        public void ClauseInsideQuote_IsRejected()
        {
            var candidates = Scan(DocumentWith(
                ("1", "W ustawie dodaje się art. 40 w brzmieniu: „Art. 40. Ustawa wchodzi w życie " +
                      "z dniem 1 stycznia 2025 r.")));

            var candidate = Assert.Single(candidates);
            Assert.Equal(ClauseRole.Stray, candidate.Role);
            Assert.Equal("klauzula wewnątrz cytatu", candidate.RejectionReason);
        }

        [Fact]
        public void AmendmentContent_IsNeverVisited()
        {
            // Treść nowelizacji to poddrzewo odpięte od RootPart — obchód nie ma jak tam wejść.
            var document = DocumentWith(("1", "Wprowadza się zmiany."));
            var article = document.Articles.Single();

            article.Paragraphs[0].Amendment = new Amendment
            {
                Content = new AmendmentContent
                {
                    Articles =
                    {
                        new Article
                        {
                            ContentText = "Art. 99. Ustawa wchodzi w życie z dniem 1 stycznia 2030 r.",
                        },
                    },
                },
            };

            Assert.Empty(Scan(document));
        }

        [Fact]
        public void AmendingArticleClause_IsPromoted_WhenNoOtherMainExists()
        {
            // Skaner odrzuca klauzulę w artykule nowelizującym, bo zwykle jest cytatem. W ustawie,
            // której JEDYNA klauzula siedzi w takim artykule, ten filtr zjadłby wszystko — serwis
            // awansuje wtedy ostatniego odrzuconego z powrotem na Main.
            //
            // Powiązanie idzie przez LITERAŁ RejectionReason porównywany w EntryIntoForceService,
            // więc kompilator rozjazdu nie zauważy, a skutkiem byłby akt bez daty wejścia w życie.
            var document = DocumentWith(
                ("1", "Ustawa wchodzi w życie z dniem 1 stycznia 2026 r."));

            var article = document.Articles.Single();
            article.Journals.Add(new JournalInfo { Year = 2024, Positions = { 1289 } });
            Assert.True(article.IsAmending, "artykuł z publikatorem musi być nowelizujący");

            // Skaner sam odrzuca — i podaje powód, na którym opiera się furtka.
            var candidate = Assert.Single(Scan(document));
            Assert.Equal(ClauseRole.Stray, candidate.Role);
            Assert.Equal("klauzula w artykule nowelizującym", candidate.RejectionReason);

            // …a serwis mimo to wyznacza datę.
            new EntryIntoForceService().Apply(document);

            var info = Assert.IsType<EntryIntoForceInfo>(document.EntryIntoForce);
            Assert.Equal(new DateTime(2026, 1, 1), info.ResolvedDate);
            Assert.Equal(new DateTime(2026, 1, 1), document.EffectiveDate);
        }

        [Fact]
        public void MultipleSelfReferentialClauses_LastWins_OthersRecorded()
        {
            var document = DocumentWith(
                ("1", "Ustawa wchodzi w życie z dniem 1 stycznia 2025 r."),
                ("2", "Ustawa wchodzi w życie z dniem 1 marca 2026 r."));

            var main = Scan(document).Where(c => c.Role == ClauseRole.Main).ToArray();
            Assert.Equal(2, main.Length);

            // Wybór „ostatnia wygrywa" należy do serwisu; skaner ma oddać obie.
            new EntryIntoForceService().Apply(document);

            var info = Assert.IsType<EntryIntoForceInfo>(document.EntryIntoForce);
            Assert.Equal(new DateTime(2026, 3, 1), info.ResolvedDate);
        }

        [Fact]
        public void ExceptionListItem_IsRecognizedByRelativePronoun()
        {
            var document = new LegalDocument();
            var subchapter = document.RootPart.Books[0].Titles[0].Divisions[0].Chapters[0].Subchapters[0];

            var article = new Article { Number = new EntityNumber { Value = "5" }, Parent = subchapter };
            var paragraph = new Paragraph
            {
                IsImplicit = true,
                ContentText = "Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia, z wyjątkiem:",
                Parent = article,
                Article = article,
            };
            paragraph.Points.Add(new Point
            {
                Number = new EntityNumber { Value = "1" },
                ContentText = "art. 3, który wchodzi w życie z dniem 1 stycznia 2026 r.;",
                Parent = paragraph,
                Article = article,
            });
            article.Paragraphs.Add(paragraph);
            subchapter.Articles.Add(article);

            var candidates = Scan(document);

            Assert.Contains(candidates, c => c.Role == ClauseRole.Main && c.OpensExceptionList);
            Assert.Contains(candidates, c => c.Role == ClauseRole.ExceptionListItem);
        }
    }
}
