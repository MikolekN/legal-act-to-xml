using System;
using System.Collections.Generic;

#nullable enable

namespace Saga.Model
{
    /// <summary>
    /// Odnośnik (przypis) aktu: oznaczenie, pod którym stoi w akcie, i treść. Miejsce odwołania niesie tekst
    /// jednostki — znacznik „[N)]" z numerem <see cref="Marker"/>.
    /// </summary>
    public sealed class Footnote
    {
        private string? _marker;

        public Guid Guid { get; set; } = Guid.NewGuid();

        /// <summary>Oznaczenie odnośnika jak w akcie („1", „12", „a").</summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Numer, pod którym znacznik odnośnika stoi w tekście jednostki. Jednoznaczny w dokumencie, inaczej niż
        /// oznaczenie: sekcja dokumentu może wznawiać numerację odnośników („1)" w obwieszczeniu i znów „1)"
        /// w ogłaszanym akcie), a znacznik drugiego ciągu numeracji niesie wtedy numer spoza zakresu numerów
        /// drukowanych. Gdy nie podano — równy oznaczeniu.
        /// </summary>
        public string Marker
        {
            get => _marker ?? Symbol;
            set => _marker = value;
        }

        /// <summary>Ciąg numeracji, w którym odnośnik stoi; oznaczenia powtarzają się między ciągami.</summary>
        public int Run { get; set; }

        /// <summary>
        /// Czy odwołanie ma własny znak („3)", „*") zamiast numeru z licznika Worda. Taki odnośnik stoi poza
        /// numeracją, więc nie mówi, który ciąg numeracji jest bieżący.
        /// </summary>
        public bool IsCustomMark { get; set; }

        /// <summary>Akapity treści odnośnika, bez własnego znacznika na początku.</summary>
        public List<string> Paragraphs { get; set; } = new();
    }
}
