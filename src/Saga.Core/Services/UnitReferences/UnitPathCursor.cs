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
    internal readonly record struct UnitStepContext(
        UnitLevel Level, bool ExplicitKeyword, int TiretDepth, bool CodeParagraph, bool SectionUnit = false);

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
        private bool _pendingSectionUnit;

        // Jednostkę główną ścieżki wprowadził „§" (akt oparty na paragrafach) — kolejny „§" to jej rodzeństwo.
        private bool _articleIsSection;
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
        /// W akcie opartym na paragrafach odziedziczona jednostka główna jest paragrafem („W § 5 wprowadza się
        /// następujące zmiany:"), więc „§" pod nią wskazuje kolejny paragraf, nie paragraf kodeksu.
        /// </summary>
        internal static UnitPathCursor StartingAt(StructuralReference context, bool sectionSignIsBasicUnit = false)
        {
            var cursor = new UnitPathCursor { Path = context.Clone(), SectionSignIsBasicUnit = sectionSignIsBasicUnit };
            cursor._articleIsSection = sectionSignIsBasicUnit && context.Article is not null;
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
            // samodzielny „§" jest jednostką główną tylko w akcie opartym na paragrafach, a „§" po takiej
            // jednostce („§ 6 i § 7", „po § 3 dodaje się § 3a") — kolejną jednostką główną (ADR-0034).
            var underArticle = Path.Article is not null && !_articleIsSection;
            _pendingCodeParagraph = keyword.IsSectionSign && (underArticle || !SectionSignIsBasicUnit);
            _pendingSectionUnit = keyword.IsSectionSign && !_pendingCodeParagraph;
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
            var sectionUnit = explicitKeyword ? _pendingSectionUnit : _articleIsSection;

            var context = new UnitStepContext(level, explicitKeyword, _pendingTiretDepth, codeParagraph, sectionUnit);
            _pendingLevel = UnitLevel.None;
            _pendingTiretDepth = 1;
            _pendingCodeParagraph = false;
            _pendingSectionUnit = false;
            return context;
        }

        /// <summary>
        /// Miejsce w treści załącznika poza jego jednostkami („w tabeli nr 1", „w części II", „w pkt 1.4.1"): adresem
        /// zostaje najgłębsza znana jednostka załącznika, a kolejne oznaczenia jednostek już go nie wydłużają — tylko
        /// odróżniają cele od siebie (ADR-0080). Poza załącznikiem nic nie robi.
        /// </summary>
        internal bool EnterAnnexContent()
        {
            if (Path.Annex is null || Path.InAnnexContent)
                return false;

            Path = Path.Clone();
            Path.AnnexContainer = Path.ToString();
            return true;
        }

        /// <summary>
        /// Część wspólna jako miejsce (ADR-0127, § 56 ZTP). Wprost za jednostką — część wspólna wyliczenia tej jednostki
        /// (jednostka przestaje być kandydatem); dołączona spójnikiem albo przecinkiem do pozycji wyliczenia („w pkt 2
        /// i w części wspólnej") — część wspólna wyliczenia, do którego ta pozycja należy (oba są celami). Bez gospodarza
        /// niczego nie wskazuje.
        /// </summary>
        internal UnitStep StepCommonPart(bool besideTheLastItem)
        {
            var previous = besideTheLastItem ? PendingDisposition.Flush : PendingDisposition.Drop;
            var host = besideTheLastItem ? EnumerationHolding(Level) : Path.HostUnit();
            if (host is null || (host.Article ?? host.Paragraph ?? host.Point ?? host.Letter ?? host.Tiret ?? host.Annex) is null)
                return new UnitStep(UnitStepStatus.UnresolvableDesignation, previous);

            if (besideTheLastItem && Level == UnitLevel.Tiret && _tiretPath.Count > 0)
                _tiretPath.RemoveAt(_tiretPath.Count - 1);
            Path = host.WithCommonPart();
            Level = UnitLevel.CommonPart;
            return new UnitStep(UnitStepStatus.Applied, previous);
        }

        /// <summary>Jednostka, w której stoi wyliczenie pozycji danego poziomu; ustęp i artykuł pozycjami wyliczenia nie są.</summary>
        private StructuralReference? EnumerationHolding(UnitLevel itemLevel)
        {
            var host = Path.HostUnit();
            switch (itemLevel)
            {
                case UnitLevel.Point:
                    host.Point = host.Letter = host.Tiret = null;
                    return host;
                case UnitLevel.Letter:
                    host.Letter = host.Tiret = null;
                    return host;
                case UnitLevel.Tiret:
                    host.Tiret = _tiretPath.Count > 1 ? string.Join('.', _tiretPath.SkipLast(1)) : null;
                    return host;
                default:
                    return null;
            }
        }

        /// <summary>Przesuwa ścieżkę o jedno oznaczenie w zamrożonym kontekście.</summary>
        internal UnitStep Step(in UnitStepContext context, string designation)
        {
            // Część wspólna nie ma oznaczenia — gołe oznaczenie po niej niczego nie wskazuje.
            if (context.Level == UnitLevel.CommonPart)
                return new UnitStep(UnitStepStatus.UnresolvableDesignation, PendingDisposition.Flush);

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
            else if (context.Level == UnitLevel.Annex)
            {
                if (!FitsLevel(context.Level, designation))
                    return new UnitStep(UnitStepStatus.UnresolvableDesignation, previous);

                // Załącznik zaczyna ścieżkę od nowa: jednostki pod nim są jednostkami załącznika (ADR-0080).
                _tiretPath.Clear();
                _systematizingPath.Clear();
                _articleIsSection = false;
                Path = new StructuralReference { Annex = designation };
            }
            else if (IsSystematizing(context.Level))
            {
                // „w części 1" pod załącznikiem to podział treści załącznika, nie część aktu.
                if (Path.Annex is not null || SystematizingSegment(context.Level, designation) is not { } segment)
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

                // Jednostki załącznika zaczynają się od ustępu (ADR-0028): „§ 10" w załączniku to miejsce w jego treści —
                // odróżnia cele („§ 107 i § 108"), ale adresu nie wydłuża (ADR-0080).
                if (context.Level == UnitLevel.Article && Path.Annex is not null)
                    EnterAnnexContent();

                if (context.Level == UnitLevel.Paragraph && context.CodeParagraph && Path.Article is null)
                    return new UnitStep(UnitStepStatus.UnresolvableDesignation, previous);

                _tiretPath.Clear();
                if (context.Level == UnitLevel.Article)
                {
                    _systematizingPath.Clear();
                    _articleIsSection = context.SectionUnit;
                }

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
            var type = TypeOf(level);
            if (ParagraphClassifier.ParseSystematizingDesignation(type, designation) is not { } number)
                return null;

            return EIdSegment.Of(SystematizingHierarchy.EIdPrefix(type), number.Designation);
        }

        // Poziomy systematyzacyjne UnitLevel stoją w kolejności SystematizingHierarchy.Levels.
        private static UnitType TypeOf(UnitLevel level) => SystematizingHierarchy.Levels[level - UnitLevel.Part];

        private static UnitLevel LevelOfSegment(string segment) =>
            UnitLevel.Part + SystematizingHierarchy.Rank(SystematizingHierarchy.LevelOfPrefix(segment.Split('_')[0])
                ?? throw new ArgumentException($"Segment spoza jednostek systematyzacyjnych: {segment}", nameof(segment)));

        /// <summary>Niezależna kopia bieżącej ścieżki.</summary>
        internal StructuralReference Snapshot() => Path.Clone();

        internal static bool FitsLevel(UnitLevel level, string designation) => level switch
        {
            UnitLevel.Article or UnitLevel.Paragraph or UnitLevel.Point =>
                designation.Length > 0 && char.IsDigit(designation[0]),
            UnitLevel.Letter => designation.Length is 1 or 2 && designation.All(DesignationAlphabet.IsLowerLetter),
            // Numer załącznika („nr 1", „nr 2a"); załącznik bez numeru czytający dostaje jako „1" (ADR-0099).
            UnitLevel.Annex => designation.Length == 0 || char.IsDigit(designation[0]),
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
