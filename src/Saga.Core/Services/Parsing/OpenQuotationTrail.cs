using Saga.Core.Ingest;
using Saga.Model;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Ślad po tym, co w otwartym cytacie nowelizacji wygląda na dalszy ciąg aktu zmieniającego. Cytat, którego
    /// bilans cudzysłowów się nie domyka (zgubiony cudzysłów zamykający), trwa do końca dokumentu i wchłania
    /// resztę aktu; że tak się stało, widać dopiero na końcu. Ślad wskazuje wtedy akapit, przed którym cytat
    /// powinien był się skończyć (ADR-0049).
    ///
    /// W biegu rozstrzygnąć się tego nie da: artykuł o numerze kolejnym względem artykułu z komendą bywa
    /// prawidłową treścią cytowaną („art. 1 i 2 otrzymują brzmienie:").
    /// </summary>
    public sealed class OpenQuotationTrail
    {
        private readonly record struct ArticleMark(DocumentBlock Block, EntityNumber Number, int Depth);

        private readonly List<ArticleMark> _articles = [];
        private DocumentBlock? _signature;

        /// <summary>
        /// Czy numer jest kolejnym po poprzednim w numeracji aktu zmieniającego: o jeden większy, bez litery
        /// i bez indeksu — akt uchwalany numeruje artykuły po kolei, litery i indeksy mają jednostki dodane
        /// do aktu zmienianego.
        /// </summary>
        public static bool Follows(EntityNumber previous, EntityNumber next) =>
            next.LexicalPart.Length == 0 && next.Superscript.Length == 0
            && next.NumericPart == previous.NumericPart + 1;

        /// <summary>
        /// Odnotowuje akapit oznaczony jak artykuł, stojący w otwartym cytacie bez własnego cudzysłowu
        /// otwierającego. <paramref name="depth"/> to głębokość cudzysłowów przed tym akapitem.
        /// </summary>
        public void NoteArticle(DocumentBlock block, EntityNumber number, int depth) =>
            _articles.Add(new ArticleMark(block, number, depth));

        /// <summary>Odnotowuje formułę podpisu stojącą w otwartym cytacie — treścią cytowaną nie bywa.</summary>
        public void NoteSignature(DocumentBlock block) => _signature ??= block;

        /// <summary>
        /// Akapit, przed którym cytat powinien był się skończyć: artykuł o numerze kolejnym względem artykułu
        /// z komendą, po którym numeracja aktu zmieniającego biegnie dalej. Taki sam numer w cytacie kilku
        /// artykułów tego nie spełnia — po nim stoi jeszcze raz ten sam numer albo numer dalszy niż kolejny.
        /// Gdy żaden kandydat tego nie spełnia, bierze się pierwszego; bez kandydatów — formułę podpisu.
        /// </summary>
        public DocumentBlock? FindBreak(EntityNumber owner)
        {
            DocumentBlock? first = null;
            for (var i = 0; i < _articles.Count; i++)
            {
                if (!Follows(owner, _articles[i].Number))
                    continue;

                first ??= _articles[i].Block;
                if (NumberingGoesOnAfter(i))
                    return _articles[i].Block;
            }

            return first ?? _signature;
        }

        private bool NumberingGoesOnAfter(int index)
        {
            var candidate = _articles[index];
            for (var i = index + 1; i < _articles.Count; i++)
            {
                // Artykuł głębiej w cudzysłowach stoi w cytacie otwartym już za kandydatem.
                if (_articles[i].Depth > candidate.Depth)
                    continue;

                return Follows(candidate.Number, _articles[i].Number);
            }

            return true;
        }

        /// <summary>Przywraca stan początkowy (nowe zbieranie nowelizacji).</summary>
        public void Reset()
        {
            _articles.Clear();
            _signature = null;
        }
    }
}
