using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace Saga.Model
{
    /// <summary>
    /// Model nowelizacji - pełna reprezentacja zmiany w akcie prawnym.
    /// Nowelizacja może dotyczyć uchylenia, dodania lub zmiany brzmienia jednostek redakcyjnych.
    /// </summary>
    public class Amendment : IEntersIntoForce
    {
        /// <summary>
        /// Unikalny identyfikator nowelizacji.
        /// </summary>
        public Guid Guid { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Rodzaj operacji nowelizacyjnej (uchylenie, dodanie, zmiana brzmienia).
        /// </summary>
        public AmendmentOperationType OperationType { get; set; }

        /// <summary>
        /// Treść nowelizacji w przypadku dodania lub zmiany brzmienia.
        /// Może zawierać hierarchiczną strukturę (artykuł z ustępami, punktami, literami, tiretami).
        /// Null w przypadku uchylenia.
        /// </summary>
        public AmendmentContent? Content { get; set; }

        /// <summary>
        /// Zmiany w tekście jednostek wskazanych celem, bez przytoczenia ich brzmienia („wyrazy „X" zastępuje się
        /// wyrazami „Y""). Każda dotyczy wszystkich celów nowelizacji; treści cytowanej zmiana wyrazów nie ma.
        /// </summary>
        public List<TextChange> TextChanges { get; set; } = new();

        /// <summary>
        /// Czy nowelizacja obudowuje dotychczasową treść jednostki nowym poziomem struktury („dotychczasową treść
        /// oznacza się jako ust. 1") i jakim. Jednostkę wskazuje <see cref="WrappedUnit"/>.
        /// </summary>
        public ContentWrap WrapsContentAs { get; set; }

        /// <summary>Jednostka, której dotychczasowa treść staje się ustępem albo paragrafem 1.</summary>
        public StructuralAmendmentReference? WrappedUnit { get; set; }

        /// <summary>
        /// Załączniki zastępowane albo dodawane odesłaniem do załączników aktu zmieniającego („otrzymuje brzmienie
        /// określone w załączniku do niniejszej ustawy").
        /// </summary>
        public List<AnnexInstruction> AnnexInstructions { get; set; } = new();

        /// <summary>
        /// Informacja o ustawie, której dotyczy nowelizacja (rok i lista pozycji publikatora).
        /// </summary>
        public JournalInfo TargetLegalAct { get; set; } = new();

        /// <summary>
        /// Cel nowelizacji - jednostki redakcyjne których dotyczy zmiana.
        /// Może być lista celów np. punkty 1-4 ustępu 2 artykułu 5.
        /// </summary>
        public List<StructuralAmendmentReference> Targets { get; set; } = new();

        /// <summary>
        /// Data wejścia w życie nowelizacji.
        /// </summary>
        public DateTime EffectiveDate { get; set; }

        /// <summary>
        /// Data mocy wstecznej nowelizacji.
        /// </summary>
        public DateTime RetroactiveFrom { get; set; }

        /// <summary>
        /// Interpunkcja następująca po cytacie w zdaniu nowelizującym
        /// (np. ',' w 'pkt 5 otrzymuje brzmienie: „…”,').
        /// </summary>
        public string? TrailingPunctuation { get; set; }

        /// <summary>Czy treść nowelizacji otwierał cudzysłów.</summary>
        public bool HasOpeningQuote { get; set; }

        /// <summary>Czy treść nowelizacji zamykał cudzysłów.</summary>
        public bool HasClosingQuote { get; set; }

        public override string ToString()
        {
            var targetStr = string.Join(", ", Targets.Select(t => t.ToString()));
            return $"{OperationType}: {targetStr} (wejście w życie: {EffectiveDate.ToShortDateString() ?? "nieokreślone"})";
        }
    }
}
