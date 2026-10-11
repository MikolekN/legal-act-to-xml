#nullable enable

namespace Saga.Model
{
    /// <summary>
    /// Wzór matematyczny stojący w osobnym akapicie: wyrażenie w MathML prezentacyjnym (element <c>math</c>
    /// z przestrzenią nazw MathML). Zapis liniowy tego samego wzoru niesie <see cref="ContentBlock.Text"/>.
    /// </summary>
    public sealed class ContentFormula
    {
        public string MathMl { get; set; } = string.Empty;
    }
}
