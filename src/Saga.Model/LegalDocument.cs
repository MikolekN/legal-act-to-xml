using System;
using System.Collections.Generic;
using System.Text;
using Saga.Model.EditorialUnits;
using Saga.Model.EntryIntoForce;
using Saga.Model.SystematizingUnits;

#nullable enable

namespace Saga.Model
{
    /// <summary>
    /// Model aktu prawnego - wrapper całej struktury dokumentu legislacyjnego.
    ///
    /// Zawiera metadane dotyczące całego aktu oraz hierarchię jego struktury.
    /// W konstruktorze tworzona jest minimalna pełna hierarchia od części do oddziału,
    /// a jednostki nieobecne w tekście pozostają jako IsImplicit = true.
    ///
    /// Przykładowa pełna hierarchia:
    /// Część (Part) → Księga (Book) → Tytuł (Title) → Dział (Division) →
    /// Rozdział (Chapter) → Oddział (Subchapter) → Artykuł (Article)
    ///
    /// Struktura w LegalDocument:
    /// LegalDocument
    ///   ├─ Type: Statute/Regulation/Code (determinuje nazewnictwo jednostek)
    ///   ├─ Title: "Ustawa o ochronie pracy"
    ///   ├─ SourceJournal: DU 2024, poz. 123
    ///   └─ RootSystematizingUnits[]: zawsze zawiera Part (domyślną lub jawną)
    ///       └─ Book → Title → Division → Chapter → Subchapter
    ///           └─ Articles[]: artykuły w oddziale
    ///               └─ Subsections: ustępy
    /// </summary>
    public class LegalDocument : IEntersIntoForce
    {
        public LegalDocument()
        {
            var part = new Part { IsImplicit = true };
            var book = new Book { IsImplicit = true, Parent = part };
            var title = new Title { IsImplicit = true, Parent = book };
            var division = new Division { IsImplicit = true, Parent = title };
            var chapter = new Chapter { IsImplicit = true, Parent = division };
            var subchapter = new Subchapter { IsImplicit = true, Parent = chapter };

            chapter.Subchapters.Add(subchapter);
            division.Chapters.Add(chapter);
            title.Divisions.Add(division);
            book.Titles.Add(title);
            part.Books.Add(book);

            Parts.Add(part);
        }
        /// <summary>
        /// Unikalny identyfikator dokumentu.
        /// </summary>
        public Guid Guid { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Typ aktu prawnego (ustawa, rozporządzenie, kodeks).
        /// Określa konwencje nazewnictwa i struktury jednostek redakcyjnych.
        /// </summary>
        public LegalActType Type { get; set; } = LegalActType.Statute;

        /// <summary>
        /// Pełny tytuł aktu prawnego.
        /// Przykłady: "Kodeks cywilny", "Ustawa z dnia 1 marca 2024 r. o ochronie konkurencji"
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Wiersz rodzaju aktu ze strefy tytułowej, jak w akcie („USTAWA"); pusty, gdy nierozpoznany.</summary>
        public string KindLine { get; set; } = string.Empty;

        /// <summary>
        /// Wiersz organu wydającego ze strefy tytułowej, jak w akcie („Marszałka Sejmu Rzeczypospolitej Polskiej",
        /// „MINISTRA FINANSÓW"); pusty, gdy akt go nie ma (ustawa) albo nierozpoznany.
        /// </summary>
        public string IssuerLine { get; set; } = string.Empty;

        /// <summary>Wiersz daty aktu („z dnia 14 czerwca 2024 r."); pusty, gdy nierozpoznany.</summary>
        public string DateLine { get; set; } = string.Empty;

        /// <summary>Przedmiot aktu („o ochronie sygnalistów") — sam, bez rodzaju i daty; pusty, gdy nierozpoznany.</summary>
        public string Subject { get; set; } = string.Empty;

        /// <summary>
        /// Data uchwalenia/wydania aktu (§ 17 ZTP), wydobyta ze strefy tytułowej ("z dnia D miesiąca RRRR r.").
        /// Null gdy nierozpoznana (np. czysty tekst bez wiersza daty).
        /// </summary>
        public DateTime? ActDate { get; set; }

        /// <summary>
        /// Data ogłoszenia aktu w dzienniku urzędowym.
        /// </summary>
        public DateTime? AnnouncementDate { get; set; }

        /// <summary>
        /// Data wejścia w życie aktu.
        /// </summary>
        public DateTime EffectiveDate { get; set; }

        /// <summary>
        /// Data mocy wstecznej aktu.
        /// </summary>
        public DateTime RetroactiveFrom { get; set; }

        /// <summary>
        /// Klauzula wejścia w życie.
        /// </summary>
        public EntryIntoForceInfo? EntryIntoForce { get; set; }

        /// <summary>
        /// Informacja o źródle publikacji (Dziennik Ustaw, rok, numer pozycji).
        /// Zawiera metadane potrzebne do odwołań w systemie prawnym.
        /// </summary>
        public JournalInfo SourceJournal { get; set; } = new();

        /// <summary>
        /// Raport klasyfikacji dokumentu (rodzaj aktu wg ZTP, pewność, sygnały) — ustawiany
        /// przez kopertę Parse, gdy klasyfikacja została wykonana. Null, gdy model zbudowano
        /// z pominięciem klasyfikacji. Nie wpływa na strukturę ani serializację kanoniczną.
        /// </summary>
        public DocumentClassificationResult? Classification { get; set; }

        /// <summary>
        /// Formuła podpisu zamykająca akt („Prezydent Rzeczypospolitej Polskiej: A. Duda"); null, gdy jej nie ma.
        /// </summary>
        public ActSignature? Signature { get; set; }

        /// <summary>
        /// Odnośniki aktu w kolejności odwołań. Miejsce odwołania niesie tekst jednostki (znacznik „[N)]").
        /// </summary>
        public List<Footnote> Footnotes { get; } = new();

        /// <summary>Załączniki aktu w kolejności dokumentu.</summary>
        public List<Annex> Annexes { get; set; } = new();

        /// <summary>
        /// Treść obwieszczenia, gdy dokument jest obwieszczeniem ogłaszającym akt w załączniku (tekst jednolity);
        /// null dla aktu ogłoszonego wprost.
        /// </summary>
        public Announcement? Announcement { get; set; }

        /// <summary>
        /// Części aktu w kolejności (kodeksy mają ich kilka). Pierwsza zawsze istnieje i zawiera minimalną
        /// pełną hierarchię od Części do Oddziału; jednostki obecne w tekście mają IsImplicit = false.
        /// </summary>
        public List<Part> Parts { get; } = new();

        /// <summary>Pierwsza część — początek drzewa (część niejawna, gdy akt nie ma części).</summary>
        public Part RootPart => Parts[0];

        /// <summary>
        /// Skrót do wszystkich artykułów w dokumencie (niezależnie od hierarchii).
        /// Przydatne do nawigacji i przeglądania treści.
        /// </summary>
        public IEnumerable<Article> Articles
        {
            get
            {
                foreach (var part in Parts)
                    foreach (var article in EnumerateArticlesFromUnit(part))
                        yield return article;
            }
        }

        /// <summary>
        /// Data utworzenia dokumentu w systemie.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Data ostatniej modyfikacji dokumentu.
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Zwraca etykietę wyświetlającą dla głównej jednostki redakcyjnej (artykułu).
        /// Zależy od typu aktu.
        /// </summary>
        public string GetMainUnitLabel() => Type.GetMainUnitLabel();

        /// <summary>
        /// Zwraca etykietę wyświetlającą dla podjednostki (ustępu/paragrafu).
        /// Zależy od typu aktu.
        /// </summary>
        public string GetSubUnitLabel() => Type.GetSubUnitLabel();

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append($"{Type.ToFriendlyString().ToUpper()}: {Title} ({SourceJournal})");

            foreach (var article in Articles)
            {
                builder.AppendLine();
                builder.Append(article.ToString());
            }

            return builder.ToString();
        }

        private static IEnumerable<Article> EnumerateArticlesFromUnit(BaseEntity unit)
        {
            foreach (var child in SystematizingHierarchy.Children(unit))
            {
                if (child is Article article)
                {
                    yield return article;
                    continue;
                }

                foreach (var nested in EnumerateArticlesFromUnit(child))
                    yield return nested;
            }
        }
    }
}
