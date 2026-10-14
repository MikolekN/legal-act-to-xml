using System.Collections.Generic;
using Saga.Model.EditorialUnits;

#nullable enable

namespace Saga.Model
{
    /// <summary>
    /// Załącznik do aktu: wskazanie aktu, do którego należy, oznaczenie, tytuł i treść w kolejności dokumentu.
    /// </summary>
    public class Annex
    {
        /// <summary>Skrót jednostki w eId (ADR-0014).</summary>
        public const string EIdPrefix = "zal";

        /// <summary>
        /// Wiersze wskazujące akt („Załączniki do ustawy z dnia 23 stycznia 2026 r.", „(Dz. U. poz. 252)").
        /// Przy kilku załącznikach stoją w akcie raz, przed pierwszym — i tylko on je niesie.
        /// </summary>
        public List<string> AttachedTo { get; set; } = new();

        /// <summary>
        /// Oznaczenie w osobnym wierszu, jak w akcie („Załącznik nr 1"); null, gdy załącznik go nie ma
        /// (jedyny załącznik aktu albo numer podany we wskazaniu aktu).
        /// </summary>
        public string? Heading { get; set; }

        /// <summary>Numer załącznika („1", „4a"); null dla załącznika nienumerowanego.</summary>
        public string? Number { get; set; }

        public string? Title { get; set; }

        /// <summary>Treść: akapity, tabele i jednostki redakcyjne w kolejności dokumentu.</summary>
        public List<AnnexBlock> Content { get; set; } = new();

        /// <summary>Adres załącznika w akcie („zal_1"); pusty, gdy załącznik nie ma numeru (ADR-0024).</summary>
        public string Id => string.IsNullOrEmpty(Number) ? string.Empty : EIdSegment.Of(EIdPrefix, Number);
    }

    /// <summary>
    /// Element treści załącznika: akapit tekstu, tabela albo jednostka redakcyjna — zawsze jedno z trojga.
    /// </summary>
    public class AnnexBlock : ContentBlock
    {
        /// <summary>
        /// Jednostka najwyższego poziomu wyliczenia w załączniku („I. …" z punktami i literami pod sobą).
        /// Jej adres zaczyna się od adresu załącznika (<c>zal_4__ust_I</c>).
        /// </summary>
        public Paragraph? Unit { get; set; }

        /// <summary>Czy element stoi na stronie poziomej; null, gdy źródło układu strony nie podaje.</summary>
        public bool? IsLandscape { get; set; }
    }
}
