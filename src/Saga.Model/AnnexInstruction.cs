#nullable enable

namespace Saga.Model
{
    /// <summary>
    /// Zmiana załącznika zapisana odesłaniem do załącznika aktu zmieniającego: „załącznik nr 1 do ustawy otrzymuje
    /// brzmienie określone w załączniku nr 2 do niniejszej ustawy" albo „dodaje się załącznik nr 4 do ustawy
    /// w brzmieniu określonym w załączniku do niniejszej ustawy". Treść stoi w załączniku aktu zmieniającego, więc
    /// przepis nie ma cytatu; w eksporcie instrukcja <c>now:zastap</c> albo <c>now:dodaj-po</c>.
    /// </summary>
    public class AnnexInstruction
    {
        /// <summary>Czy załączniki są dodawane (po <see cref="Target"/>), a nie zastępowane.</summary>
        public bool Adds { get; set; }

        /// <summary>
        /// Numer załącznika aktu zmienianego: zastępowanego albo — przy dodaniu — tego, po którym się dodaje
        /// (przy zmianie części — tego, w którym ona stoi, <see cref="Part"/>).
        /// Null, gdy przepis go nie podaje i nie da się go wyprowadzić.
        /// </summary>
        public string? Target { get; set; }

        /// <summary>Numer załącznika aktu zmieniającego z nowym brzmieniem; null, gdy załącznik nie ma numeru.</summary>
        public string? Source { get; set; }

        /// <summary>Koniec przedziału załączników aktu zmieniającego przy dodaniu kilku naraz.</summary>
        public string? SourceTo { get; set; }

        /// <summary>
        /// Część załącznika zmieniana albo dodawana odesłaniem, tak jak ją nazywa przepis („tabela C", „lp. 73") —
        /// <see cref="Target"/> to wtedy załącznik, w którym stoi. Części nie zaadresuje <c>eId</c>, więc instrukcja
        /// jest ręczna, z adresem załącznika (ADR-0108).
        /// </summary>
        public string? Part { get; set; }
    }
}
