using System;
using System.Linq;
using Saga.Core.Services.EntryIntoForce;
using Saga.Model;
using Saga.Model.EditorialUnits;
using Xunit;

namespace Saga.Core.Tests
{
	/// <summary>
	/// Odnajdywanie celu wyjątku — osobno w drzewie dokumentu i w cytowanej treści nowelizacji.
	///
	/// Testy są jednostkowe, z ręcznie zbudowaną treścią nowelizacji, a nie przez potok TXT:
	/// tiret zagnieżdżony w korzeniu cytatu jest trudny albo niemożliwy do wyprodukowania z tekstu,
	/// a testowany defekt jest defektem rezolwera, nie parsera.
	/// </summary>
	public class EntryIntoForceTargetResolverTests
	{
		// ============================================================
		// Budowanie drzewa
		// ============================================================

		private static Paragraph ParagraphWithSentences(string number, params string[] sentences)
		{
			var paragraph = new Paragraph { Number = new EntityNumber { Value = number } };
			for (var i = 0; i < sentences.Length; i++)
				paragraph.TextSegments.Add(new TextSegment { Order = i + 1, Text = sentences[i] });
			return paragraph;
		}

		/// <summary>Jednostka goszcząca z nowelizacją, której treść zbudował wywołujący.</summary>
		private static Point HostWithContent(AmendmentContent content) =>
			new()
			{
				Number = new EntityNumber { Value = "1" },
				Amendment = new Amendment { Content = content },
			};

		private static LegalDocument DocumentWithArticle(Article article)
		{
			var document = new LegalDocument();
			document.RootPart.Books[0].Titles[0].Divisions[0].Chapters[0].Subchapters[0].Articles.Add(article);
			return document;
		}

		/// <summary>Drzewo dokumentu: art. 1 (ustęp niejawny, 2 zdania) → pkt 3 → lit. a → tiret 1 → tiret 2.</summary>
		private static LegalDocument BuildTree()
		{
			var document = new LegalDocument();
			var subchapter = document.RootPart.Books[0].Titles[0].Divisions[0].Chapters[0].Subchapters[0];

			var article = new Article { Number = new EntityNumber { Value = "1" }, Parent = subchapter };
			var paragraph = ParagraphWithSentences("1", "Zdanie pierwsze.", "Zdanie drugie.");
			paragraph.Number = null;
			paragraph.IsImplicit = true;
			paragraph.Parent = article;
			paragraph.Article = article;

			var point = new Point { Number = new EntityNumber { Value = "3" }, Parent = paragraph, Article = article };
			var letter = new Letter { Number = new EntityNumber { Value = "a" }, Parent = point, Article = article };
			var tiret = new Tiret { Number = new EntityNumber { Value = "1" }, Parent = letter, Article = article };
			var nested = new Tiret { Number = new EntityNumber { Value = "2" }, Parent = tiret, Article = article };

			tiret.Tirets.Add(nested);
			letter.Tirets.Add(tiret);
			point.Letters.Add(letter);
			paragraph.Points.Add(point);
			article.Paragraphs.Add(paragraph);
			subchapter.Articles.Add(article);

			return document;
		}

		// ============================================================
		// Drzewo dokumentu
		// ============================================================

		[Fact]
		public void ResolvesArticle()
		{
			var entity = EntryIntoForceTargetResolver.Resolve(BuildTree(), new StructuralReference { Article = "1" });
			Assert.IsType<Article>(entity);
		}

		[Fact]
		public void ResolvesPointThroughImplicitParagraph()
		{
			var entity = EntryIntoForceTargetResolver.Resolve(
				BuildTree(), new StructuralReference { Article = "1", Point = "3" });

			Assert.Equal("3", Assert.IsType<Point>(entity).Number?.Value);
		}

		[Fact]
		public void ResolvesNestedTiretByDottedPath()
		{
			var entity = EntryIntoForceTargetResolver.Resolve(
				BuildTree(), new StructuralReference { Article = "1", Point = "3", Letter = "a", Tiret = "1.2" });

			Assert.Equal("2", Assert.IsType<Tiret>(entity).Number?.Value);
		}

		[Fact]
		public void MissingUnit_ResolvesToNull()
		{
			var document = BuildTree();
			Assert.Null(EntryIntoForceTargetResolver.Resolve(
				document, new StructuralReference { Article = "1", Point = "99" }));
			Assert.Null(EntryIntoForceTargetResolver.Resolve(
				document, new StructuralReference { Article = "42" }));
		}

		[Fact]
		public void SentenceTarget_ResolvesToTheSegment_NotTheWholeUnit()
		{
			var document = BuildTree();
			var reference = new StructuralReference { Article = "1", Sentence = "2" };

			var entity = EntryIntoForceTargetResolver.Resolve(document, reference);
			Assert.NotNull(entity);

			// Rezolwer wskazuje SEGMENT, a nie jednostkę — dzięki temu wyjątek dotyczący jednego
			// zdania nie zabiera daty całej jednostce (zapis należy do EffectiveDatePropagator).
			var segment = EntryIntoForceTargetResolver.ResolveSegment(entity!, reference);

			var paragraph = document.Articles.Single().Paragraphs[0];
			Assert.Same(paragraph.TextSegments[1], segment);
		}

		// ============================================================
		// Zdanie w cytacie — ścieżka dziś cicho nieaktywna
		// ============================================================

		[Fact]
		public void Amendment_SentencePath_ResolvesToTheOwningUnit_NotTheWholeArticle()
		{
			var article = new Article { Number = new EntityNumber { Value = "35" } };
			article.Paragraphs.Add(ParagraphWithSentences("3e", "Zdanie pierwsze.", "Zdanie drugie."));

			var content = new AmendmentContent();
			content.Articles.Add(article);
			var host = HostWithContent(content);

			var scope = new StructuralReference { Article = "35", Paragraph = "3e", Sentence = "2" };
			var resolved = EntryIntoForceTargetResolver.ResolveInAmendment(host, scope);

			// Nie artykuł — jednostka, która NAPRAWDĘ trzyma zdanie.
			var paragraph = Assert.IsType<Paragraph>(resolved);
			Assert.Equal("3e", paragraph.Number?.Value);

			Assert.Same(
				paragraph.TextSegments[1],
				EntryIntoForceTargetResolver.ResolveSegment(paragraph, scope));
		}

		[Fact]
		public void Amendment_SentenceWithoutMatchingSegment_ReturnsNull()
		{
			// Najważniejszy test w tym pliku. Dziś ta ścieżka jest cicho nieaktywna: rezolwer zwraca
			// jednostkę nadrzędną, ApplyDate iteruje po pustym zbiorze i NIC nie robi, a wyjątek
			// wygląda na zastosowany. Musi być nietrafieniem, żeby trafił do UnresolvedReason.
			var article = new Article { Number = new EntityNumber { Value = "35" } };
			article.Paragraphs.Add(ParagraphWithSentences("3e", "Jedyne zdanie."));

			var content = new AmendmentContent();
			content.Articles.Add(article);

			var resolved = EntryIntoForceTargetResolver.ResolveInAmendment(
				HostWithContent(content),
				new StructuralReference { Article = "35", Paragraph = "3e", Sentence = "7" });

			Assert.Null(resolved);
		}

		// ============================================================
		// Tiret ze ścieżką kropkową
		// ============================================================

		[Fact]
		public void Amendment_DottedTiretPath_UnderLetter_Resolves()
		{
			var outer = new Tiret { Number = new EntityNumber { Value = "1" } };
			outer.Tirets.Add(new Tiret { Number = new EntityNumber { Value = "2" } });

			var letter = new Letter { Number = new EntityNumber { Value = "a" } };
			letter.Tirets.Add(outer);

			var point = new Point { Number = new EntityNumber { Value = "3" } };
			point.Letters.Add(letter);

			var paragraph = new Paragraph { IsImplicit = true };
			paragraph.Points.Add(point);

			var article = new Article { Number = new EntityNumber { Value = "5" } };
			article.Paragraphs.Add(paragraph);

			var content = new AmendmentContent();
			content.Articles.Add(article);

			var resolved = EntryIntoForceTargetResolver.ResolveInAmendment(
				HostWithContent(content),
				new StructuralReference { Article = "5", Point = "3", Letter = "a", Tiret = "1.2" });

			Assert.Equal("2", Assert.IsType<Tiret>(resolved).Number?.Value);
		}

		[Fact]
		public void Amendment_DottedTiretPath_AtContentRoot_Resolves()
		{
			// Gałąź content.Tirets — dziś nietrafialna dla ścieżki „1.2", bo omija rozbicie po kropce.
			var outer = new Tiret { Number = new EntityNumber { Value = "1" } };
			outer.Tirets.Add(new Tiret { Number = new EntityNumber { Value = "2" } });

			var content = new AmendmentContent();
			content.Tirets.Add(outer);

			var resolved = EntryIntoForceTargetResolver.ResolveInAmendment(
				HostWithContent(content),
				new StructuralReference { Tiret = "1.2" });

			Assert.Equal("2", Assert.IsType<Tiret>(resolved).Number?.Value);
		}

		// ============================================================
		// Parytet obu zejściówek
		// ============================================================

		[Fact]
		public void ImplicitParagraphFallback_BehavesIdentically_InDocumentAndInAmendment()
		{
			// Strażnik linii, która była skopiowana co do znaku w obu rezolwerach:
			// powołanie „art. 5 pkt 3" pomija ustęp, bo artykuł ma tylko jeden, niejawny.
			static Article BuildArticle()
			{
				var paragraph = new Paragraph { IsImplicit = true };
				paragraph.Points.Add(new Point { Number = new EntityNumber { Value = "3" } });

				var article = new Article { Number = new EntityNumber { Value = "5" } };
				article.Paragraphs.Add(paragraph);
				return article;
			}

			var reference = new StructuralReference { Article = "5", Point = "3" };

			var fromDocument = EntryIntoForceTargetResolver.Resolve(DocumentWithArticle(BuildArticle()), reference);

			var content = new AmendmentContent();
			content.Articles.Add(BuildArticle());
			var fromAmendment = EntryIntoForceTargetResolver.ResolveInAmendment(HostWithContent(content), reference);

			Assert.Equal("3", Assert.IsType<Point>(fromDocument).Number?.Value);
			Assert.Equal("3", Assert.IsType<Point>(fromAmendment).Number?.Value);
		}

		[Fact]
		public void MissingUnitInAmendment_ReturnsNull()
		{
			var content = new AmendmentContent();
			content.Articles.Add(new Article { Number = new EntityNumber { Value = "35" } });

			Assert.Null(EntryIntoForceTargetResolver.ResolveInAmendment(
				HostWithContent(content), new StructuralReference { Article = "99" }));
		}

		[Fact]
		public void HostWithoutQuotedContent_ReturnsNull()
		{
			// Uchylenie nie cytuje treści — nie ma czego datować.
			var host = new Point
			{
				Number = new EntityNumber { Value = "1" },
				Amendment = new Amendment { Content = null },
			};

			Assert.Null(EntryIntoForceTargetResolver.ResolveInAmendment(
				host, new StructuralReference { Article = "35" }));
		}
	}
}
