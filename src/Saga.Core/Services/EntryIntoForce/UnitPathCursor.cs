using Saga.Core.Helpers;
using Saga.Model;

namespace Saga.Core.Services.EntryIntoForce
{
    /// <summary>Skutek kroku kursora.</summary>
    internal enum UnitStepStatus
    {
        /// <summary>Ścieżka przesunięta — jest nowy kandydat na cel.</summary>
        Applied,

        /// <summary>Oznaczenie nie jest liczebnikiem („zdanie ostatnie") albo tiret trafił w niedostępną głębokość.</summary>
        UnresolvableDesignation,
    }

    /// <summary>Co zrobić z kandydatem zbudowanym w poprzednim kroku.</summary>
    internal enum PendingDisposition
    {
        /// <summary>Poprzedni był rodzeństwem — wydać go.</summary>
        Flush,

        /// <summary>Poprzedni był wyłącznie prefiksem nowego — porzucić.</summary>
        Drop,
    }

    /// <summary>
    /// Zamrożony kontekst jednego słowa kluczowego. Przedział („85c–85j") rozwija się w wiele
    /// oznaczeń z jednym słowem kluczowym, więc kontekst ustala się raz i podaje do każdego
    /// oznaczenia osobno.
    /// </summary>
    internal readonly record struct UnitStepContext(UnitLevel Level, bool ExplicitKeyword, int TiretDepth);

    /// <summary>Wynik kroku: czy się udał i co zrobić z poprzednim kandydatem.</summary>
    internal readonly record struct UnitStep(UnitStepStatus Status, PendingDisposition Previous)
    {
        internal bool IsApplied => Status == UnitStepStatus.Applied;
    }

    /// <summary>Kursor po ścieżce jednostek redakcyjnych.</summary>
    internal sealed class UnitPathCursor
    {
        private readonly List<string> _tiretPath = new();
        private UnitLevel _pendingLevel = UnitLevel.None;
        private int _pendingTiretDepth = 1;
        internal StructuralReference Path { get; private set; } = new();

        /// <summary>Poziom ostatnio zastosowanego oznaczenia.</summary>
        internal UnitLevel Level { get; private set; } = UnitLevel.None;

        /// <summary>Słowo kluczowe zapowiada poziom kolejnego oznaczenia.</summary>
        internal void Announce(UnitToken keyword)
        {
            // „§" po artykule to paragraf kodeksu — podjednostka, nie jednostka główna.
            _pendingLevel = keyword.IsSectionSign && Path.Article is not null
                ? UnitLevel.Paragraph
                : keyword.Level;
            _pendingTiretDepth = keyword.TiretDepth;
        }

        /// <summary>Ustala poziom kolejnego oznaczenia.</summary>
        internal UnitStepContext? BeginStep()
        {
            var level = _pendingLevel != UnitLevel.None ? _pendingLevel : Level;
            if (level == UnitLevel.None)
                return null;

            var context = new UnitStepContext(level, _pendingLevel != UnitLevel.None, _pendingTiretDepth);
            _pendingLevel = UnitLevel.None;
            _pendingTiretDepth = 1;
            return context;
        }

        /// <summary>Przesuwa ścieżkę o jedno oznaczenie w zamrożonym kontekście.</summary>
        internal UnitStep Step(in UnitStepContext context, string designation)
        {
            var isTiretDescent =
                context.Level == UnitLevel.Tiret && context.ExplicitKeyword && context.TiretDepth > 1 &&
                _tiretPath.Count == context.TiretDepth - 1;

            var previous = context.Level > Level || isTiretDescent
                ? PendingDisposition.Drop
                : PendingDisposition.Flush;

            if (context.Level == UnitLevel.Tiret)
            {
                if (!TryApplyTiret(designation, context.ExplicitKeyword, context.TiretDepth))
                    return new UnitStep(UnitStepStatus.UnresolvableDesignation, previous);
            }
            else if (context.Level == UnitLevel.Sentence)
            {
                if (NormalizeOrdinal(designation) is not { } sentenceNumber)
                    return new UnitStep(UnitStepStatus.UnresolvableDesignation, previous);

                _tiretPath.Clear();
                Path = Path.WithSentence(sentenceNumber);
            }
            else
            {
                _tiretPath.Clear();
                Path = context.Level switch
                {
                    UnitLevel.Article => Path.WithArticle(designation),
                    UnitLevel.Paragraph => Path.WithParagraph(designation),
                    UnitLevel.Point => Path.WithPoint(designation),
                    UnitLevel.Letter => Path.WithLetter(designation),
                    _ => Path,
                };
            }

            Level = context.Level;
            return new UnitStep(UnitStepStatus.Applied, previous);
        }

        /// <summary>Niezależna kopia bieżącej ścieżki.</summary>
        internal StructuralReference Snapshot() => Path.Clone();

        /// <summary>
        /// Tiret: głębokość 1 zaczyna nową ścieżkę, głębokość > 1 dokłada kolejny segment
        /// („tiret pierwsze podwójne tiret drugie" → „1.2"). Kolejne tiret na tej samej głębokości
        /// zastępuje ostatni segment („tiret drugie i trzecie").
        /// </summary>
        private bool TryApplyTiret(string designation, bool explicitKeyword, int depth)
        {
            var value = NormalizeOrdinal(designation);
            if (value is null)
                return false;

            if (!explicitKeyword || depth == 1)
            {
                if (_tiretPath.Count == 0)
                    _tiretPath.Add(value);
                else
                    _tiretPath[^1] = value;
            }
            else
            {
                while (_tiretPath.Count >= depth)
                    _tiretPath.RemoveAt(_tiretPath.Count - 1);
                if (_tiretPath.Count != depth - 1)
                    return false;
                _tiretPath.Add(value);
            }

            Path = Path.WithTiret(string.Join('.', _tiretPath));
            return true;
        }

        /// <summary>Liczebnik porządkowy słowny („pierwsze") przetworzony na liczbę; cyfry przepuszcza.</summary>
        private static string? NormalizeOrdinal(string designation)
        {
            if (designation.Length > 0 && char.IsDigit(designation[0]))
                return designation;

            return PolishNumerals.TryParseOrdinal(designation, out var value)
                ? value.ToString()
                : null;
        }
    }
}
