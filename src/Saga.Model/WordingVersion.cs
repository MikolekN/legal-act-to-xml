using System;

#nullable enable

namespace Saga.Model
{
    /// <summary>Którą z wersji jednostki drukowanych w tekście jednolitym jest jednostka (§ 106a ZTP).</summary>
    public enum WordingVersionKind
    {
        /// <summary>Brzmienie obowiązujące do wejścia w życie zmiany („W tym brzmieniu obowiązuje do …").</summary>
        Current,

        /// <summary>Brzmienie przyszłe („…; wejdzie w życie z dniem …"), także jednostka dopiero dodawana.</summary>
        Future,
    }

    /// <summary>
    /// Wersja czasowa jednostki w tekście jednolitym. Tekst jednolity drukuje jednostkę dwa razy, gdy jej zmiana nie
    /// weszła jeszcze w życie; obie wersje mają to samo oznaczenie i eId, a rozróżnia je czas. Koniec wersji
    /// obowiązującej i początek przyszłej to to samo zdarzenie — wejście w życie zmiany.
    /// </summary>
    public class WordingVersion
    {
        public WordingVersionKind Kind { get; set; }

        /// <summary>
        /// Numer odnośnika, który opisuje zmianę (stoi przy wersji przyszłej) — wspólny klucz obu wersji. Pusty,
        /// gdy wersji obowiązującej nie udało się powiązać z jej zmianą.
        /// </summary>
        public string ChangeFootnote { get; set; } = string.Empty;

        /// <summary>Dzień wejścia w życie zmiany; null, gdy odnośnik go nie podaje.</summary>
        public DateTime? ChangeDate { get; set; }

        /// <summary>
        /// Termin wejścia w życie zmiany zapisany prozą zamiast dnia („z dniem wdrożenia rozwiązań technicznych …")
        /// — dosłownie, jak w odnośniku. Null, gdy odnośnik podaje dzień albo terminu nie podaje wcale.
        /// </summary>
        public string? ChangeTerm { get; set; }

        /// <summary>
        /// Numer odnośnika, w którym zapisano termin wejścia w życie zmiany. Zwykle to <see cref="ChangeFootnote"/>;
        /// inny, gdy odnośnik zmiany nazywa akt zmieniający odesłaniem („ustawy, o której mowa w odnośniku 3"),
        /// a termin stoi tam, gdzie akt nazwano w pełni. Pusty, gdy terminu nie podano.
        /// </summary>
        public string TermFootnote { get; set; } = string.Empty;

        /// <summary>
        /// Czy zmiana uchyla jednostkę: wersją przyszłą jest wtedy „(uchylony)", a obowiązująca kończy się
        /// uchyleniem.
        /// </summary>
        public bool ChangeRepeals { get; set; }
    }
}
