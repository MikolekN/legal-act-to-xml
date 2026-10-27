using System;
using System.Collections.Generic;
using System.Text;

#nullable enable

namespace Saga.Model.EditorialUnits
{
    /// <summary>
    /// Model ustepu artykulu (Paragraph) - definicja struktury bez logiki parsowania.
    /// Paragraph zawiera co najmniej jeden punkt lub tekst bezposrednio.
    /// Czesci wspolne dla listy punktow wystepuja na tym samym poziomie co punkty.
    /// </summary>
    public class Paragraph : BaseEntity, IHasCommonParts, IHasTextSegments, IHasAmendments
    {
        public bool? IsImplicitComputed => Article == null ? null : Article.Paragraphs.Count <= 1;

        /// <summary>
        /// Czesci wspolne na poziomie ustepu (np. intro/wrapUp wobec listy punktow).
        /// Wystepuja jako rodzenstwo punktow.
        /// </summary>
        public List<CommonPart> CommonParts { get; set; } = new();

        /// <summary>
        /// Segmenty tekstu (np. zdania) w obrebie ustepu.
        /// Umozliwiaja kotwiczenie nowelizacji do konkretnego zdania.
        /// </summary>
        public List<TextSegment> TextSegments { get; set; } = new();

        public List<Point> Points { get; set; } = new();
        public Amendment? Amendment { get; set; }

        public List<Amendment> EarlierSentenceAmendments { get; } = new();

        /// <summary>
        /// Rola paragrafu kodeksu (§) — jednostka mapowana na element paragraf-kodeksu
        /// </summary>
        public const string CodeParagraphRole = "par";

        /// <summary>
        /// Role moze okreslac specjalne mapowania/nazewnictwo (np. "par" dla paragrafu w kodeksie).
        /// </summary>
        public string? Role { get; set; }

        /// <summary>
        /// Paragraf kodeksu, którego oznaczenie wydrukowano bez „§" („2." w artykule z „§ 1.") — jednostka spoza ZTP.
        /// Numer zostaje taki, jak w akcie; rola i prefiks eId są paragrafu kodeksu, bo artykuł nie miesza ustępów
        /// z paragrafami.
        /// </summary>
        public bool CodeParagraphWithoutSign { get; set; }

        public Paragraph()
        {
            UnitType = UnitType.Paragraph;
            EIdPrefix = "ust";
            DisplayLabel = "ust.";
        }

        public override string ToString()
        {
            const string indent = "    ";
            var builder = new StringBuilder();
            builder.Append($"{indent}[{Id}] {ContentText.Substring(0, Math.Min(48, ContentText.Length))}");

            foreach (var point in Points)
            {
                builder.AppendLine();
                var pointStr = point.ToString();
                if (pointStr != null)
                {
                    builder.Append(pointStr);
                }
            }

            return builder.ToString();
        }
    }
}
