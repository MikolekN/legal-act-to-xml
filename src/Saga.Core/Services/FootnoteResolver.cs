using System.Globalization;
using System.Text.RegularExpressions;
using Saga.Model;

namespace Saga.Core.Services
{
    /// <summary>
    /// Rozstrzyga, do którego odnośnika należy znacznik „[N)]" spotkany w tekście aktu czytanym w kolejności
    /// dokumentu (ADR-0061). Odwołanie Worda stoi w tekście pod numerem jednoznacznym w dokumencie
    /// (<see cref="Footnote.Marker"/>) i zawsze jest pierwszym wystąpieniem swojego odnośnika, więc numery
    /// pierwszych wystąpień rosną. Powtórzenie znacznika jest w źródle zwykłym tekstem i niesie oznaczenie
    /// drukowane (<see cref="Footnote.Symbol"/>), które powtarza się między ciągami numeracji.
    /// </summary>
    internal sealed class FootnoteResolver
    {
        private static readonly Regex Number = new(@"\d+", RegexOptions.Compiled);

        private readonly Dictionary<string, Footnote> _byMarker = new(StringComparer.Ordinal);
        private readonly Dictionary<(int Run, string Symbol), Footnote> _met = new();
        private int _last;
        private int _run;

        internal FootnoteResolver(IEnumerable<Footnote> footnotes)
        {
            foreach (var footnote in footnotes)
                _byMarker.TryAdd(footnote.Marker, footnote);
        }

        /// <summary>Odnośnik, którego pierwszym wystąpieniem jest znacznik o tym numerze; null, gdy to nie ono.</summary>
        internal Footnote? First(string number)
        {
            if (!int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                || value <= _last
                || !_byMarker.TryGetValue(number, out var footnote))
            {
                return null;
            }

            _last = value;

            // Odnośnik z własnym znakiem stoi poza numeracją Worda: nie przełącza ciągu, w którym szuka się
            // powtórzeń, i sam powtarza się w ciągu bieżącym (ADR-0095).
            if (!footnote.IsCustomMark)
                _run = footnote.Run;
            _met[(footnote.IsCustomMark ? _run : footnote.Run, footnote.Symbol)] = footnote;
            return footnote;
        }

        /// <summary>
        /// Odnośnik spotkany wcześniej, do którego wraca powtórzony znacznik: oznaczenie drukowane w ciągu
        /// numeracji, w którym stoi ostatnie pierwsze wystąpienie. Null, gdy takiego odnośnika nie było.
        /// </summary>
        internal Footnote? Repeated(string number) => _met.GetValueOrDefault((_run, number));

        /// <summary>
        /// Znacznik zapisany tak, jak stoi w akcie: numer odwołania z dalszego ciągu numeracji zastąpiony
        /// oznaczeniem odnośnika. Numery, pod którymi nie stoi odwołanie, zostają bez zmian.
        /// </summary>
        internal string AsPrinted(string marker) => Number.Replace(marker, match =>
            _byMarker.TryGetValue(match.Value, out var footnote) && footnote.Run > 0 ? footnote.Symbol : match.Value);
    }
}
