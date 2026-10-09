using System;
using System.Collections.Generic;

#nullable enable

namespace Saga.Model
{
    /// <summary>
    /// Reprezentuje pojedynczy cel nowelizacji.
    /// Bez żadnych powiązań z XML/ELI.
    /// </summary>
    public class StructuralAmendmentReference
    {
        /// <summary>
        /// Strukturalna ścieżka jednostki redakcyjnej będącej celem zmiany:
        /// art. → ust. → pkt → lit. → tiret
        /// </summary>
        public StructuralReference Structure { get; set; } = new();

        /// <summary>
        /// Ostatnia jednostka przedziału („art. 1–3" → art. 3); null, gdy cel to jedna jednostka.
        /// </summary>
        public StructuralReference? RangeEnd { get; set; }

        /// <summary>
        /// Surowy tekst źródłowy powołania, przydatny dla walidacji
        /// lub celów diagnostycznych parsera.
        /// </summary>
        public string? RawText { get; set; }

        public override string ToString() =>
            RangeEnd is null ? Structure.ToString() : $"{Structure}–{RangeEnd}";
    }

    /// <summary>
    /// Strukturalna pozycja w hierarchii jednostek redakcyjnych (art. → ust. → pkt → lit. → tiret → fragment zdania).
    /// Mutacja dozwolona wyłącznie wewnętrznie i przez serwisy parsujące.
    /// Do nawigacji po hierarchii używaj metod With*, które zwracają nową instancję
    /// z wyzerowanymi poziomami podrzędnymi.
    /// </summary>
    public class StructuralReference
    {
        /// <summary>
        /// Ścieżka jednostek systematyzacyjnych w postaci eId („dzial_2__rozdz_9") — tylko w adresie bez
        /// artykułu; artykuł ją zeruje, bo eId jednostki redakcyjnej jej nie niesie (ADR-0017).
        /// </summary>
        public string? Systematizing { get; set; }

        public string? Article { get; set; }
        public string? Paragraph { get; set; }

        public bool ParagraphIsCodeParagraph { get; set; }

        public string? Point { get; set; }
        public string? Letter { get; set; }
        public string? Tiret { get; set; }
        public string? Sentence { get; set; }

        /// <summary>Ustawia artykuł i zeruje wszystkie poziomy podrzędne.</summary>
        public StructuralReference WithArticle(string value) =>
            new() { Article = value };

        /// <summary>Ustawia ustęp i zeruje wszystkie poziomy podrzędne.</summary>
        public StructuralReference WithParagraph(string value) =>
            new() { Article = Article, Paragraph = value };

        /// <summary>Ustawia paragraf kodeksu i zeruje wszystkie poziomy podrzędne.</summary>
        public StructuralReference WithCodeParagraph(string value) =>
            new() { Article = Article, Paragraph = value, ParagraphIsCodeParagraph = true };

        /// <summary>Ustawia punkt i zeruje wszystkie poziomy podrzędne.</summary>
        public StructuralReference WithPoint(string value) =>
            new() { Article = Article, Paragraph = Paragraph, ParagraphIsCodeParagraph = ParagraphIsCodeParagraph, Point = value };

        /// <summary>Ustawia literę i zeruje wszystkie poziomy podrzędne.</summary>
        public StructuralReference WithLetter(string value) =>
            new() { Article = Article, Paragraph = Paragraph, ParagraphIsCodeParagraph = ParagraphIsCodeParagraph, Point = Point, Letter = value };

        /// <summary>Ustawia tiret i zeruje zdanie.</summary>
        public StructuralReference WithTiret(string value) =>
            new() { Article = Article, Paragraph = Paragraph, ParagraphIsCodeParagraph = ParagraphIsCodeParagraph, Point = Point, Letter = Letter, Tiret = value };

        /// <summary>Ustawia zdanie, zachowując wszystkie poziomy nadrzędne.</summary>
        public StructuralReference WithSentence(string value) =>
            new() { Article = Article, Paragraph = Paragraph, ParagraphIsCodeParagraph = ParagraphIsCodeParagraph, Point = Point, Letter = Letter, Tiret = Tiret, Sentence = value };

        public StructuralReference Clone() =>
            new() { Systematizing = Systematizing, Article = Article, Paragraph = Paragraph, ParagraphIsCodeParagraph = ParagraphIsCodeParagraph, Point = Point, Letter = Letter, Tiret = Tiret, Sentence = Sentence };

        public override string ToString()
        {
            var parts = new List<string>();

            if (Systematizing != null) parts.Add(Systematizing);
            if (Article != null) parts.Add(EIdSegment.Of("art", Article));
            if (Paragraph != null)
                parts.Add(EIdSegment.Of(ParagraphIsCodeParagraph ? EditorialUnits.Paragraph.CodeParagraphRole : "ust", Paragraph));
            if (Point != null) parts.Add(EIdSegment.Of("pkt", Point));
            if (Letter != null) parts.Add(EIdSegment.Of("lit", Letter));
            if (Tiret != null) parts.AddRange(TiretSegments(Tiret));
            if (Sentence != null) parts.Add(EIdSegment.Of("zdn", Sentence));

            return EIdSegment.Join(parts);
        }

        private static IEnumerable<string> TiretSegments(string dottedPath)
        {
            var segments = dottedPath.Split('.', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < segments.Length; i++)
                yield return EIdSegment.Of(EditorialUnits.Tiret.PrefixForDepth(i + 1), segments[i]);
        }
    }
}
