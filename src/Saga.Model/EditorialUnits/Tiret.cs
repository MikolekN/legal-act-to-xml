using System;
using System.Collections.Generic;
using System.Text;

#nullable enable

namespace Saga.Model.EditorialUnits
{
    /// <summary>
    /// Model tiret - zawiera definicje struktury bez logiki parsowania.
    /// Tiret to wyliczeniowa jednostka w hierarchii.
    /// Czesci wspolne wystepuja jako rodzenstwo tiretow na poziomie litery.
    /// </summary>
    public class Tiret : BaseEntity, IHasTextSegments, IHasAmendments
    {
        /// <summary>
        /// Segmenty tekstu (np. zdania) w obrebie tiretu.
        /// Umozliwiaja kotwiczenie nowelizacji do konkretnego zdania.
        /// </summary>
        public List<TextSegment> TextSegments { get; set; } = new();

        /// <summary>
        /// Tirety wchodzace w skład tiret. Podwójne tirety.
        /// </summary>
        public List<Tiret> Tirets { get; set; } = new();
        public Amendment? Amendment { get; set; }

        public Tiret()
        {
            UnitType = UnitType.Tiret;
            EIdPrefix = PrefixForDepth(1);
            DisplayLabel = "tiret";
        }

        /// <summary>
        /// Prefiks eId (levelType) tiretu wg głębokości zagnieżdżenia:
        /// 1 → „tir", 2 → „dwtir", 3 → „trtir", 4 → „cztir", 5 → „pitir".
        /// </summary>
        public static string PrefixForDepth(int depth) => depth switch
        {
            <= 1 => "tir",
            2 => "dwtir",
            3 => "trtir",
            4 => "cztir",
            _ => "pitir",
        };

        /// <summary>Głębokość zagnieżdżenia liczona po łańcuchu rodziców (tiret najwyższy = 1).</summary>
        private int Depth()
        {
            var depth = 1;
            for (var parent = Parent; parent is Tiret; parent = parent.Parent)
                depth++;
            return depth;
        }

        protected override string GetLocalIdSegment()
        {
            var prefix = PrefixForDepth(Depth());
            return string.IsNullOrEmpty(Number?.Value) ? prefix : $"{prefix}_{Number.Value}";
        }

        public override string ToString()
        {
            const string indent = "          ";
            var builder = new StringBuilder();
            builder.Append($"{indent}[{Id}] {ContentText.Substring(0, Math.Min(48, ContentText.Length))}");

            foreach (var tiret in Tirets)
            {
                builder.AppendLine();
                var tiretStr = tiret.ToString();
                if (tiretStr != null)
                {
                    builder.Append(tiretStr);
                }
            }

            return builder.ToString();
        }
    }
}
