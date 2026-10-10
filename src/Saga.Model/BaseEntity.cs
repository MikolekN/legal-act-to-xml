using System;
using System.Collections.Generic;
using Saga.Model.EditorialUnits;
using Saga.Model.SystematizingUnits;

#nullable enable

namespace Saga.Model
{
    /// <summary>
    /// Bazowy DTO dla wszystkich encji modelu (artykuł, ustęp, punkt, litera, tiret, jednostki systematyzujące).
    /// Zawiera dane opisujące strukturę i zależności bez logiki parsowania.
    /// Wprowadza: UnitType, DisplayLabel, EIdPrefix oraz centralne budowanie stabilnego eId.
    /// </summary>
    public enum UnitType
    {
        Unknown,
        // editorial units
        Article,
        Paragraph, // ustęp
        Point,
        Letter,
        Tiret,
        CommonPart, // część wspólna (intro/wrapUp) - wirtualny byt redakcyjny
        // systematizing units
        Part,
        Book,
        Title,
        Division,
        Chapter,
        Subchapter
    }

    public abstract class BaseEntity : IEntersIntoForce
    {
        public Guid Guid { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Typ jednostki semantycznej (np. Article/Paragraph/Point/Letter/Tiret/...)
        /// </summary>
        public UnitType UnitType { get; set; } = UnitType.Unknown;

        /// <summary>
        /// Etykieta do wyświetlenia (np. "art.", "ust.", "§")
        /// </summary>
        public string DisplayLabel { get; set; } = string.Empty;

        /// <summary>
        /// Prefiks używany przy generowaniu eId (np. "art"/"ust"/"pkt"/"lit"/"tir"/"czesc"/"ksi" ...).
        /// Można go nadpisać w klasach pochodnych jeżeli konieczne.
        /// </summary>
        public string EIdPrefix { get; set; } = string.Empty;

        /// <summary>
        /// Numer encji (np. 10 dla artykułu, 2 dla ustępu, f dla litery).
        /// Zawiera rozbicie na komponenty: część liczbowa, tekstowa i indeks górny.
        /// </summary>
        public EntityNumber? Number { get; set; }

        /// <summary>
        /// Jednostka niejawna: nie ma oznaczenia w tekście, model wstawia ją dla spójności hierarchii
        /// (jedyny ustęp artykułu, rozdział aktu bez rozdziałów). Nie wchodzi do eId.
        /// </summary>
        public bool IsImplicit { get; set; }

        //TODO: ContentText to tekst jednostki redakcyjnej. Tymczasowo jest to string, ale trzeba uwzgędnić:
        // - możliwość występowania elementów osadzonych, jak tabele, grafiki, wzory matematyczne itp.
        // - obecnie konwertujemy wszystko do tekstu płaskiego, ale w przyszłości będzie potrzebny model drzewa,
        //  dzieląc na poszczególne akapity (zdania)
        // Do rozważenia - Content trzymać w formie HTML z formatowaniem?
        public string ContentText { get; set; } = string.Empty;

        /// <summary>
        /// Data wejścia w życie danej jednostki redakcyjnej.
        /// </summary>
        public DateTime EffectiveDate { get; set; }

        /// <summary>
        /// Data mocy wstecznej danej jednostki redakcyjnej.
        /// </summary>
        public DateTime RetroactiveFrom { get; set; }

        /// <summary>
        /// Komunikaty walidacji/diagnostyki zestawiane podczas parsowania
        /// (błędy, ostrzeżenia, informacje o jakości dokumentu).
        /// </summary>
        public List<ValidationMessage> ValidationMessages { get; set; } = new();

        /// <summary>
        /// Dalsza treść akapitu jednostki, w kolejności z aktu: akapity bez własnego oznaczenia i tabele stojące
        /// za jej zdaniami, a przed jednostkami podrzędnymi (schemat: <c>kontynuacja-akapitu</c>).
        /// </summary>
        public List<ContentBlock> Continuation { get; set; } = new();

        /// <summary>
        /// Adres w akcie zmienianym, pod którym stoi jednostka cytowana w nowelizacji — początek jej eId
        /// („art_68__ust_1" dla punktu dodawanego w art. 68 ust. 1). Pusty poza treścią nowelizacji.
        /// </summary>
        public string AddressAnchor { get; set; } = string.Empty;

        // Referencje do encji nadrzędnych (ułatwiają nawigację w modelu)
        public BaseEntity? Parent { get; set; }
        public Article? Article { get; set; }
        public Paragraph? Paragraph { get; set; }
        public Point? Point { get; set; }
        public Letter? Letter { get; set; }
        public Tiret? Tiret { get; set; }

        /// <summary>
        /// Lokalny segment identyfikatora (np. "art_10" albo "lit_a").
        /// Jednostka bez odczytanego oznaczenia nie wnosi segmentu: jej adresem jest najbliższy oznaczony
        /// przodek, a jednostki pod nią adresują się względem niego (ADR-0024).
        /// Można nadpisać w klasach pochodnych, gdy format różni się.
        /// </summary>
        protected virtual string GetLocalIdSegment() =>
            string.IsNullOrEmpty(EIdPrefix) || string.IsNullOrEmpty(Number?.Designation)
                ? string.Empty
                : EIdSegment.Of(EIdPrefix, Number.Designation);

        /// <summary>
        /// Hierarchiczne, stabilne i parsowalne Id (eId) budowane od korzenia w dół,
        /// używa separatora "__" zgodnie z założeniem (np. "art_10__ust_2__pkt_3").
        /// eId jednostki redakcyjnej kończy się na artykule — ścieżka jednostek systematyzacyjnych
        /// do niego nie wchodzi (ADR-0017); jednostka systematyzacyjna niesie ścieżkę swoich
        /// jawnych przodków systematyzacyjnych. Jednostki niejawne (<see cref="IsImplicit"/>) i jednostki
        /// bez odczytanego oznaczenia są pomijane.
        /// Jednostka cytowana w nowelizacji zaczyna się od adresu celu w akcie zmienianym
        /// (<see cref="AddressAnchor"/> korzenia cytatu).
        /// </summary>
        public virtual string Id
        {
            get
            {
                var parts = new List<string>();
                var isEditorial = this is not ISystematizingUnit;
                BaseEntity? current = this;
                BaseEntity root = this;
                while (current != null)
                {
                    root = current;
                    if (current is ISystematizingUnit && isEditorial)
                        break;

                    if (!current.IsImplicit)
                    {
                        var seg = current.GetLocalIdSegment();
                        if (!string.IsNullOrEmpty(seg)) parts.Add(seg);
                    }
                    current = current.Parent;
                }
                if (current == null && root.AddressAnchor.Length > 0)
                    parts.Add(root.AddressAnchor);
                parts.Reverse();
                return EIdSegment.Join(parts);
            }
        }
    }
}
