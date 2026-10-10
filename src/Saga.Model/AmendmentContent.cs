using System.Collections.Generic;
using System.Linq;
using Saga.Model.EditorialUnits;

#nullable enable

namespace Saga.Model
{
    /// <summary>
    /// Model treści nowelizacji - może zawierać dowolny fragment hierarchicznej struktury aktu prawnego.
    /// </summary>
    public class AmendmentContent
    {
        /// <summary>
        /// Typ obiektu będącego treścią nowelizacji (Artykuł, Ustęp, Punkt, Litera, Tiret, CommonPart).
        /// Możliwa redundancja z StructuralAmendmentReference. Do weryfikacji, czy potrzebna.
        /// </summary>
        public AmendmentObjectType ObjectType { get; set; }

        /// <summary>
        /// Jednostki systematyzacyjne najwyższego poziomu w treści nowelizacji („dodaje się rozdział 9a
        /// w brzmieniu:") — wraz z artykułami w ich wnętrzu.
        /// </summary>
        public List<BaseEntity> SystematizingUnits { get; set; } = new();

        /// <summary>
        /// Artykuły będące treścią nowelizacji (w przypadku dodania/zmiany pełnych artykułów).
        /// </summary>
        public List<Article> Articles { get; set; } = new();

        /// <summary>
        /// Ustępy będące treścią nowelizacji (w przypadku dodania/zmiany ustępów).
        /// </summary>
        public List<Paragraph> Paragraphs { get; set; } = new();

        /// <summary>
        /// Punkty będące treścią nowelizacji (w przypadku dodania/zmiany punktów).
        /// </summary>
        public List<Point> Points { get; set; } = new();

        /// <summary>
        /// Litery będące treścią nowelizacji (w przypadku dodania/zmiany liter).
        /// </summary>
        public List<Letter> Letters { get; set; } = new();

        /// <summary>
        /// Tirety będące treścią nowelizacji (w przypadku dodania/zmiany tiretów).
        /// </summary>
        public List<Tiret> Tirets { get; set; } = new();

        /// <summary>
        /// Części wspólne (intro/wrapUp) będące treścią nowelizacji 
        /// (w przypadku zmiany tekstu przed lub po liście).
        /// </summary>
        public List<CommonPart> CommonParts { get; set; } = new();

        /// <summary>
        /// Prosty tekst treści (dla przypadków, gdy nowelizacja nie ma struktury hierarchicznej).
        /// </summary>
        public string? PlainText { get; set; }

        /// <summary>
        /// Tabele cytowane wprost — treść nowelizacji, która jest tabelą albo jej wierszami („po lp. 2a dodaje
        /// się lp. 2b w brzmieniu:" i wiersz tabeli). Tabela stojąca w cytowanej jednostce należy do jej dalszej
        /// treści (<see cref="BaseEntity.Continuation"/>), nie tutaj.
        /// </summary>
        public List<ContentTable> Tables { get; set; } = new();

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(PlainText))
                return PlainText;

            var counts = new List<string>();
            if (SystematizingUnits.Any()) counts.Add($"{SystematizingUnits.Count} jedn. syst.");
            if (Articles.Any()) counts.Add($"{Articles.Count} art.");
            if (Paragraphs.Any()) counts.Add($"{Paragraphs.Count} ust.");
            if (Points.Any()) counts.Add($"{Points.Count} pkt.");
            if (Letters.Any()) counts.Add($"{Letters.Count} lit.");
            if (Tirets.Any()) counts.Add($"{Tirets.Count} tir.");
            if (CommonParts.Any()) counts.Add($"{CommonParts.Count} cz. wspólne");
            if (Tables.Any()) counts.Add($"{Tables.Count} tab.");

            return counts.Any() ? string.Join(", ", counts) : "brak treści";
        }
    }
}
