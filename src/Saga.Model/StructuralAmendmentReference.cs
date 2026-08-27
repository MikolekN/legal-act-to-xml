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
        /// Surowy tekst źródłowy powołania, przydatny dla walidacji
        /// lub celów diagnostycznych parsera.
        /// </summary>
        public string? RawText { get; set; }

        public override string ToString() => Structure.ToString();
    }

    /// <summary>
    /// Strukturalna pozycja w hierarchii jednostek redakcyjnych (art. → ust. → pkt → lit. → tiret → fragment zdania).
    /// Mutacja dozwolona wyłącznie wewnętrznie i przez serwisy parsujące.
    /// Do nawigacji po hierarchii używaj metod With*, które zwracają nową instancję
    /// z wyzerowanymi poziomami podrzędnymi.
    /// </summary>
    public class StructuralReference
    {
        public string? Article { get; set; }
        public string? Paragraph { get; set; }
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

        /// <summary>Ustawia punkt i zeruje wszystkie poziomy podrzędne.</summary>
        public StructuralReference WithPoint(string value) =>
            new() { Article = Article, Paragraph = Paragraph, Point = value };

        /// <summary>Ustawia literę i zeruje wszystkie poziomy podrzędne.</summary>
        public StructuralReference WithLetter(string value) =>
            new() { Article = Article, Paragraph = Paragraph, Point = Point, Letter = value };

        /// <summary>Ustawia tiret i zeruje zdanie.</summary>
        public StructuralReference WithTiret(string value) =>
            new() { Article = Article, Paragraph = Paragraph, Point = Point, Letter = Letter, Tiret = value };

        /// <summary>Ustawia zdanie, zachowując wszystkie poziomy nadrzędne.</summary>
        public StructuralReference WithSentence(string value) =>
            new() { Article = Article, Paragraph = Paragraph, Point = Point, Letter = Letter, Tiret = Tiret, Sentence = value };

        public StructuralReference Clone() =>
            new() { Article = Article, Paragraph = Paragraph, Point = Point, Letter = Letter, Tiret = Tiret, Sentence = Sentence };

        public override string ToString()
        {
            var parts = new List<string>();

            if (Article != null) parts.Add($"art_{Article}");
            if (Paragraph != null) parts.Add($"ust_{Paragraph}");
            if (Point != null) parts.Add($"pkt_{Point}");
            if (Letter != null) parts.Add($"lit_{Letter}");
            if (Tiret != null) parts.AddRange(TiretSegments(Tiret));
            if (Sentence != null) parts.Add($"zdn_{Sentence}");

            return string.Join("__", parts);
        }

        private static IEnumerable<string> TiretSegments(string dottedPath)
        {
            var segments = dottedPath.Split('.', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < segments.Length; i++)
                yield return $"{EditorialUnits.Tiret.PrefixForDepth(i + 1)}_{segments[i]}";
        }
    }
}
