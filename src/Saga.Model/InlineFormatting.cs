using System;
using System.Collections.Generic;

#nullable enable

namespace Saga.Model
{
    /// <summary>Formatowanie znakowe zakresu tekstu (ADR-0117).</summary>
    [Flags]
    public enum InlineFormat
    {
        None = 0,
        Italic = 1,
        Bold = 2,
        Subscript = 4,

        /// <summary>Grupa indeksu górnego kanału „[x]" — z nawiasami, jak stoi w tekście bloku.</summary>
        Superscript = 8,
    }

    /// <summary>
    /// Zakres tekstu bloku źródła złożony formatowaniem znakowym. <see cref="Format"/> — jak wydrukowano;
    /// <see cref="FromParagraphStyle"/> — ta część formatu, którą ma cały akapit ze swojego stylu (typografia rodzaju
    /// akapitu: tytuł, nagłówek), a nie przebieg (styl znaku, formatowanie wprost). Tekst zostaje bez zmian —
    /// formatowanie idzie obok niego, więc wzorce rozpoznawania go nie widzą.
    /// </summary>
    public sealed record InlineSpan(int Start, int Length, InlineFormat Format, InlineFormat FromParagraphStyle = InlineFormat.None)
    {
        public int End => Start + Length;
    }

    /// <summary>
    /// Tekst bloku źródła z zakresami formatowania, w kolejności dokumentu — rejestr, z którego eksport odtwarza
    /// formatowanie w treści (ADR-0117). Nie jest treścią aktu i nie wchodzi do modelu kanonicznego.
    /// </summary>
    public sealed record FormattedSource(string Text, IReadOnlyList<InlineSpan> Spans);
}
