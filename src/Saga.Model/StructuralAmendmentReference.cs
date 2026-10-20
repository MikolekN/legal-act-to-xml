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

        /// <summary>
        /// Cel, którego <c>eId</c> nie zaadresuje (wiersz tabeli, kolumna, część załącznika): <see cref="Structure"/>
        /// wskazuje najgłębszą adresowalną jednostkę, a instrukcja jest ręczna — <c>*-recznie</c> (ADR-0100).
        /// </summary>
        public bool IsManual { get; set; }

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

        /// <summary>
        /// Numer załącznika, gdy adres wskazuje miejsce w załączniku aktu („w załączniku nr 1 … w ust. 2"); jednostki pod
        /// nim są jednostkami załącznika (ADR-0028); załącznik bez numeru to „1" (ADR-0099). Pusty napis — miejsce
        /// w załączniku bez adresu: taki adres, z jednostkami pod nim, niczego nie wskazuje (ADR-0080).
        /// </summary>
        public string? Annex { get; set; }

        /// <summary>
        /// Adres najgłębszej znanej jednostki załącznika, gdy wskazane miejsce leży w treści załącznika poza jego
        /// jednostkami — w tabeli, w części, pod numeracją wielopoziomową („w tabeli nr 1", „w pkt 1.4.1"). Jednostki
        /// pod tym miejscem nie są jednostkami załącznika: odróżniają cele od siebie, ale adresem jest ta jednostka
        /// (ADR-0080).
        /// </summary>
        public string? AnnexContainer { get; set; }

        /// <summary>Czy adres wskazuje miejsce w treści załącznika poza jego jednostkami.</summary>
        public bool InAnnexContent => AnnexContainer is not null;

        public string? Article { get; set; }
        public string? Paragraph { get; set; }

        public bool ParagraphIsCodeParagraph { get; set; }

        public string? Point { get; set; }
        public string? Letter { get; set; }
        public string? Tiret { get; set; }
        public string? Sentence { get; set; }

        /// <summary>Numer odnośnika (przypisu), gdy adres wskazuje odnośnik aktu, a nie jednostkę („odn_1").</summary>
        public string? Footnote { get; set; }

        /// <summary>
        /// Miejscem jest tytuł aktu zmienianego („tytuł ustawy otrzymuje brzmienie:", „w tytule ustawy …"). Tytułu nie
        /// adresuje <c>eId</c> — instrukcja jest ręczna, z adresem samego aktu (ADR-0101).
        /// </summary>
        public bool ActTitle { get; set; }

        /// <summary>Ustawia artykuł i zeruje wszystkie poziomy podrzędne.</summary>
        public StructuralReference WithArticle(string value) =>
            new() { Annex = Annex, AnnexContainer = AnnexContainer, Article = value };

        /// <summary>Ustawia ustęp i zeruje wszystkie poziomy podrzędne.</summary>
        public StructuralReference WithParagraph(string value) =>
            new() { Annex = Annex, AnnexContainer = AnnexContainer, Article = Article, Paragraph = value };

        /// <summary>Ustawia paragraf kodeksu i zeruje wszystkie poziomy podrzędne.</summary>
        public StructuralReference WithCodeParagraph(string value) =>
            new() { Annex = Annex, AnnexContainer = AnnexContainer, Article = Article, Paragraph = value, ParagraphIsCodeParagraph = true };

        /// <summary>Ustawia punkt i zeruje wszystkie poziomy podrzędne.</summary>
        public StructuralReference WithPoint(string value) =>
            new() { Annex = Annex, AnnexContainer = AnnexContainer, Article = Article, Paragraph = Paragraph, ParagraphIsCodeParagraph = ParagraphIsCodeParagraph, Point = value };

        /// <summary>Ustawia literę i zeruje wszystkie poziomy podrzędne.</summary>
        public StructuralReference WithLetter(string value) =>
            new() { Annex = Annex, AnnexContainer = AnnexContainer, Article = Article, Paragraph = Paragraph, ParagraphIsCodeParagraph = ParagraphIsCodeParagraph, Point = Point, Letter = value };

        /// <summary>Ustawia tiret i zeruje zdanie.</summary>
        public StructuralReference WithTiret(string value) =>
            new() { Annex = Annex, AnnexContainer = AnnexContainer, Article = Article, Paragraph = Paragraph, ParagraphIsCodeParagraph = ParagraphIsCodeParagraph, Point = Point, Letter = Letter, Tiret = value };

        /// <summary>Ustawia zdanie, zachowując wszystkie poziomy nadrzędne.</summary>
        public StructuralReference WithSentence(string value) =>
            new() { Annex = Annex, AnnexContainer = AnnexContainer, Article = Article, Paragraph = Paragraph, ParagraphIsCodeParagraph = ParagraphIsCodeParagraph, Point = Point, Letter = Letter, Tiret = Tiret, Sentence = value };

        /// <summary>Skrót odnośnika w adresie (<c>odn_1</c>).</summary>
        public const string FootnotePrefix = "odn";

        public StructuralReference Clone() =>
            new() { Systematizing = Systematizing, Annex = Annex, AnnexContainer = AnnexContainer, Article = Article, Paragraph = Paragraph, ParagraphIsCodeParagraph = ParagraphIsCodeParagraph, Point = Point, Letter = Letter, Tiret = Tiret, Sentence = Sentence, Footnote = Footnote, ActTitle = ActTitle };

        public override string ToString()
        {
            // Miejsce w załączniku bez numeru nie ma adresu — ani ono, ani jednostki pod nim.
            if (Annex is { Length: 0 })
                return string.Empty;
            if (AnnexContainer is not null)
                return AnnexContainer;

            var parts = new List<string>();

            if (Systematizing != null) parts.Add(Systematizing);
            if (Annex != null) parts.Add(EIdSegment.Of(Saga.Model.Annex.EIdPrefix, Annex));
            if (Article != null) parts.Add(EIdSegment.Of("art", Article));
            if (Paragraph != null)
                parts.Add(EIdSegment.Of(ParagraphIsCodeParagraph ? EditorialUnits.Paragraph.CodeParagraphRole : "ust", Paragraph));
            if (Point != null) parts.Add(EIdSegment.Of("pkt", Point));
            if (Letter != null) parts.Add(EIdSegment.Of("lit", Letter));
            if (Tiret != null) parts.AddRange(TiretSegments(Tiret));
            if (Sentence != null) parts.Add(EIdSegment.Of("zdn", Sentence));
            if (Footnote != null) parts.Add(EIdSegment.Of(FootnotePrefix, Footnote));

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
