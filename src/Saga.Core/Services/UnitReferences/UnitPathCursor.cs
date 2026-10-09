using Saga.Core.Helpers;
using Saga.Core.Services.Classify;
using Saga.Model;
using Saga.Model.SystematizingUnits;

namespace Saga.Core.Services.UnitReferences
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
    internal readonly record struct UnitStepContext(UnitLevel Level, bool ExplicitKeyword, int TiretDepth, bool CodeParagraph);

    /// <summary>Wynik kroku: czy się udał i co zrobić z poprzednim kandydatem.</summary>
    internal readonly record struct UnitStep(UnitStepStatus Status, PendingDisposition Previous)
    {
        internal bool IsApplied => Status == UnitStepStatus.Applied;
    }

    /// <summary>Kursor po ścieżce jednostek redakcyjnych.</summary>
    internal sealed class UnitPathCursor
    {
        private readonly List<string> _tiretPath = new();
        private readonly List<(UnitLevel Level, string Segment)> _systematizingPath = new();
        private UnitLevel _pendingLevel = UnitLevel.None;
        private int _pendingTiretDepth = 1;
        private bool _pendingCodeParagraph;
        internal StructuralReference Path { get; private set; } = new();

        /// <summary>Poziom ostatnio zastosowanego oznaczenia.</summary>
        internal UnitLevel Level { get; private set; } = UnitLevel.None;

        /// <summary>
        /// Czy samodzielny „§" (bez artykułu w ścieżce) jest jednostką główną — rozstrzyga rodzaj aktu.
        /// </summary>
        internal bool SectionSignIsBasicUnit { get; init; }

        /// <summary>
        /// Kursor ustawiony na znanej ścieżce — np. artykule z komendy nadrzędnej („W art. 5 wprowadza się
        /// następujące zmiany: 1) w § 2 …"). Dzięki temu „§" pod odziedziczonym artykułem to paragraf kodeksu,
        /// a „podwójne tiret" schodzi pod odziedziczony tiret. Poziomu kontekst nie ustala: pierwsze
        /// oznaczenie potrzebuje słowa kluczowego, więc gołe „4" z daty nie staje się jednostką kontekstu.
        /// </summary>
        internal static UnitPathCursor StartingAt(StructuralReference context, bool sectionSignIsBasicUnit = false)
        {
            var cursor = new UnitPathCursor { Path = context.Clone(), SectionSignIsBasicUnit = sectionSignIsBasicUnit };
            if (context.Tiret is { } tiret)
                cursor._tiretPath.AddRange(tiret.Split('.', StringSplitOptions.RemoveEmptyEntries));
            if (context.Systematizing is { } systematizing)
                foreach (var segment in systematizing.Split(EIdSegment.Separator, StringSplitOptions.RemoveEmptyEntries))
                    cursor._systematizingPath.Add((LevelOfSegment(segment), segment));
            return cursor;
        }

        /// <summary>Słowo kluczowe zapowiada poziom kolejnego oznaczenia.</summary>
        internal void Announce(UnitToken keyword)
        {
            // „§" po artykule to zawsze paragraf kodeksu — podjednostka, nie jednostka główna;
            // samodzielny „§" jest jednostką główną tylko w akcie opartym na paragrafach.
            _pendingCodeParagraph = keyword.IsSectionSign && (Path.Article is not null || !SectionSignIsBasicUnit);
            _pendingLevel = _pendingCodeParagraph ? UnitLevel.Paragraph : keyword.Level;
            _pendingTiretDepth = keyword.TiretDepth;
        }

        /// <summary>Ustala poziom kolejnego oznaczenia.</summary>
        internal UnitStepContext? BeginStep()
        {
            var level = _pendingLevel != UnitLevel.None ? _pendingLevel : Level;
            if (level == UnitLevel.None)
                return null;

            var explicitKeyword = _pendingLevel != UnitLevel.None;
            var codeParagraph = explicitKeyword ? _pendingCodeParagraph : Path.ParagraphIsCodeParagraph;

            var context = new UnitStepContext(level, explicitKeyword, _pendingTiretDepth, codeParagraph);
            _pendingLevel = UnitLevel.None;
            _pendingTiretDepth = 1;
            _pendingCodeParagraph = false;
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
            else if (IsSystematizing(context.Level))
            {
                if (SystematizingSegment(context.Level, designation) is not { } segment)
                    return new UnitStep(UnitStepStatus.UnresolvableDesignation, previous);

                var level = context.Level;
                _tiretPath.Clear();
                _systematizingPath.RemoveAll(entry => entry.Level >= level);
                _systematizingPath.Add((level, segment));
                Path = new StructuralReference
                {
                    Systematizing = EIdSegment.Join(_systematizingPath.Select(entry => entry.Segment)),
                };
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
                if (!FitsLevel(context.Level, designation))
                    return new UnitStep(UnitStepStatus.UnresolvableDesignation, previous);

                if (context.Level == UnitLevel.Paragraph && context.CodeParagraph && Path.Article is null)
                    return new UnitStep(UnitStepStatus.UnresolvableDesignation, previous);

                _tiretPath.Clear();
                if (context.Level == UnitLevel.Article)
                    _systematizingPath.Clear();
                Path = context.Level switch
                {
                    UnitLevel.Article => Path.WithArticle(designation),
                    UnitLevel.Paragraph => context.CodeParagraph
                        ? Path.WithCodeParagraph(designation)
                        : Path.WithParagraph(designation),
                    UnitLevel.Point => Path.WithPoint(designation),
                    UnitLevel.Letter => Path.WithLetter(designation),
                    _ => Path,
                };
            }

            Level = context.Level;
            return new UnitStep(UnitStepStatus.Applied, previous);
        }

        private static bool IsSystematizing(UnitLevel level) => level is >= UnitLevel.Part and <= UnitLevel.Subchapter;

        /// <summary>Segment eId jednostki systematyzacyjnej z oznaczenia („VIa" w dziale → „dzial_6a", ADR-0018).</summary>
        private static string? SystematizingSegment(UnitLevel level, string designation)
        {
            var (kind, keyword, unit) = level switch
            {
                UnitLevel.Part => (ParagraphKind.PartUnit, "Część", (BaseEntity)new Part()),
                UnitLevel.Book => (ParagraphKind.BookUnit, "Księga", new Book()),
                UnitLevel.Title => (ParagraphKind.TitleUnit, "Tytuł", new Title()),
                UnitLevel.Division => (ParagraphKind.DivisionUnit, "Dział", new Division()),
                UnitLevel.Chapter => (ParagraphKind.ChapterUnit, "Rozdział", new Chapter()),
                _ => (ParagraphKind.SubchapterUnit, "Oddział", new Subchapter()),
            };

            if (ParagraphClassifier.ParseSystematizingNumber(kind, $"{keyword} {designation}") is not { } number
                || number.Value.StartsWith(ParagraphClassifier.NamedUnitValue, StringComparison.Ordinal))
                return null;

            unit.Number = number;
            return EIdSegment.Of(unit.EIdPrefix, number.Designation);
        }

        private static UnitLevel LevelOfSegment(string segment) => segment.Split('_')[0] switch
        {
            "czesc" => UnitLevel.Part,
            "ksi" => UnitLevel.Book,
            "tyt" => UnitLevel.Title,
            "dzial" => UnitLevel.Division,
            "rozdz" => UnitLevel.Chapter,
            _ => UnitLevel.Subchapter,
        };

        /// <summary>Niezależna kopia bieżącej ścieżki.</summary>
        internal StructuralReference Snapshot() => Path.Clone();

        internal static bool FitsLevel(UnitLevel level, string designation) => level switch
        {
            UnitLevel.Article or UnitLevel.Paragraph or UnitLevel.Point =>
                designation.Length > 0 && char.IsDigit(designation[0]),
            UnitLevel.Letter => designation.Length is 1 or 2 && designation.All(c => c is >= 'a' and <= 'z'),
            _ => true,
        };

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
