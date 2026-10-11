#nullable enable

using System.Collections.Generic;

namespace Saga.Model
{
    /// <summary>
    /// Wzór matematyczny stojący w osobnym akapicie: wyrażenie w MathML prezentacyjnym (element <c>math</c>
    /// z przestrzenią nazw MathML). Zapis liniowy tego samego wzoru niesie <see cref="ContentBlock.Text"/>.
    /// Legenda wzoru — wprowadzenie i opisy symboli — należy do wzoru, nie do dalszej treści jednostki.
    /// </summary>
    public sealed class ContentFormula
    {
        public string MathMl { get; set; } = string.Empty;

        /// <summary>Wprowadzenie do opisów symboli („gdzie poszczególne symbole oznaczają:"); null, gdy go nie ma.</summary>
        public string? LegendIntroduction { get; set; }

        /// <summary>Opisy symboli użytych we wzorze, w kolejności z aktu.</summary>
        public List<FormulaSymbol> Legend { get; set; } = new();
    }

    /// <summary>Opis jednego symbolu wzoru: „I_e – ilość energii elektrycznej …".</summary>
    public sealed class FormulaSymbol
    {
        /// <summary>Symbol tak, jak stoi w tekście (zapis liniowy, gdy w źródle jest równaniem).</summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>Symbol w MathML, gdy w źródle jest równaniem; null dla symbolu zapisanego tekstem.</summary>
        public string? SymbolMathMl { get; set; }

        /// <summary>Objaśnienie symbolu — tekst za myślnikiem.</summary>
        public string Explanation { get; set; } = string.Empty;
    }
}
