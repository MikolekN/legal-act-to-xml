using Saga.Core.Helpers;
using Saga.Model;
using Serilog;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Dane pojedynczego akapitu zebranego jako tresc nowelizacji.
    /// </summary>
    public sealed record CollectedAmendmentParagraph(
        string Text,
        string? StyleId,
        AmendmentStyleInfo? StyleInfo)
    {
        public string Text { get; set; } = Text;

        /// <summary>Tabela stojąca w tym miejscu treści cytowanej; pozycja z tabelą nie niesie tekstu.</summary>
        public ContentTable? Table { get; init; }

        /// <summary>MathML akapitu będącego wzorem; tekst pozycji to jego zapis liniowy.</summary>
        public string? FormulaMathMl { get; init; }

        /// <summary>Równanie otwierające akapit (symbol wiersza legendy wzoru).</summary>
        public Ingest.BlockSymbol? LeadingSymbol { get; init; }

        /// <summary>Wcięcie lewe akapitu (twips); null, gdy źródło nie niesie układu.</summary>
        public int? LeftIndentTwips { get; init; }
    }

    /// <summary>
    /// Bufor akapitow nowelizacji. Zbiera akapity od momentu wejscia w tryb nowelizacji
    /// az do powrotu do stylu ustawy matki. Przechowuje rowniez metadane:
    /// encje-wlasciciela (trigger source) i wykryty cel nowelizacji.
    ///
    /// Cykl zycia:
    /// 1. Orkiestrator wykrywa trigger ("otrzymuje brzmienie:") → ustawia Owner
    /// 2. Kolejne akapity (styl Z/... lub bezstylowe) → AddParagraph()
    /// 3. Powrot do stylu matki → orkiestrator wywoluje Flush() (iteracja 2)
    /// 4. Reset() → gotowy do nastepnej nowelizacji
    /// </summary>
    public sealed class AmendmentCollector
    {
        private readonly List<CollectedAmendmentParagraph> _paragraphs = new();

        /// <summary>
        /// Zebrane akapity tresci nowelizacji (kolejnosc zachowana).
        /// </summary>
        public IReadOnlyList<CollectedAmendmentParagraph> Paragraphs => _paragraphs;

        /// <summary>
        /// Encja, na ktore wykryto trigger nowelizacji (np. ustep z "otrzymuje brzmienie:").
        /// To jest wlasciciel przyszlego obiektu Amendment.
        /// </summary>
        public BaseEntity? Owner { get; private set; }

        /// <summary>
        /// Wykryty cel nowelizacji (referencja strukturalna z tresci triggera).
        /// </summary>
        public IReadOnlyList<StructuralAmendmentReference> Targets { get; private set; } = [];

        /// <summary>
        /// Bilans cudzysłowów zbieranej treści (granice nowelizacji bez stylów Z/*, § 94 ZTP).
        /// Uzbrajany przy pierwszym bezstylowym bloku zaczynającym się od „; resetowany z kolektorem.
        /// </summary>
        public QuoteBalanceTracker QuoteTracker { get; } = new();

        /// <summary>
        /// Co w otwartym cytacie wygląda na dalszy ciąg aktu zmieniającego — do wskazania granicy cytatu,
        /// który nie domknął się do końca dokumentu (ADR-0049); resetowany z kolektorem.
        /// </summary>
        public OpenQuotationTrail OpenQuotation { get; } = new();

        /// <summary>
        /// Czy kolektor jest w trybie aktywnego zbierania
        /// (ma wlasciciela lub zebrane akapity).
        /// </summary>
        public bool IsCollecting => Owner != null || _paragraphs.Count > 0;

        /// <summary>
        /// Liczba zebranych akapitow.
        /// </summary>
        public int Count => _paragraphs.Count;

        /// <summary>
        /// Rozpoczyna zbieranie nowelizacji — ustawia encje-wlasciciela.
        /// Wywolywane gdy orkiestrator wykryje trigger nowelizacji.
        /// </summary>
        /// <param name="owner">Encja zawierajaca zwrot nowelizacyjny</param>
        /// <param name="targets">Opcjonalne cele wykryte z referencji strukturalnej</param>
        public void Begin(BaseEntity owner, IReadOnlyList<StructuralAmendmentReference>? targets = null)
        {
            if (IsCollecting)
            {
                Log.Warning(
                    "AmendmentCollector.Begin() wywolany podczas aktywnego zbierania " +
                    "(owner={PrevOwner}, paragraphs={Count}). Poprzednia nowelizacja zostanie utracona.",
                    Owner?.Id, _paragraphs.Count);
                Reset();
            }

            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Targets = targets ?? [];

            Log.Debug("AmendmentCollector: rozpoczeto zbieranie nowelizacji dla {OwnerType} [{OwnerId}]",
                owner.UnitType, owner.Id);
        }

        /// <summary>
        /// Czy zebrana treść zaczyna się cudzysłowem, którego dotąd nie domknięto — bez względu na style akapitów
        /// (<see cref="QuoteTracker"/> liczy bilans tylko przy zbieraniu bezstylowym).
        /// </summary>
        public bool LeavesQuoteOpen
        {
            get
            {
                if (_paragraphs.Count == 0 || _paragraphs[0].Table is not null || !QuoteBalanceTracker.OpensQuote(_paragraphs[0].Text))
                    return false;

                var depth = 0;
                foreach (var paragraph in _paragraphs)
                {
                    foreach (var character in paragraph.Table is { } table ? TextOf(table) : paragraph.Text)
                    {
                        if (character == QuotationMarks.Opening)
                            depth++;
                        else if (QuotationMarks.IsClosing(character) && depth > 0)
                            depth--;
                    }
                }

                return depth > 0;
            }
        }

        /// <summary>
        /// Dodaje akapit do bufora nowelizacji.
        /// Dekoduje metadane stylu przez <see cref="AmendmentStyleDecoder"/>.
        /// </summary>
        /// <param name="formulaMathMl">MathML akapitu, gdy jest wzorem.</param>
        /// <param name="leadingSymbol">Równanie otwierające akapit, gdy za nim stoi tekst.</param>
        /// <param name="leftIndentTwips">Wcięcie lewe akapitu, gdy źródło niesie układ.</param>
        public void AddParagraph(
            string text, string? styleId, string? formulaMathMl = null, Ingest.BlockSymbol? leadingSymbol = null, int? leftIndentTwips = null)
        {
            var styleInfo = AmendmentStyleDecoder.DecodeByStyleId(styleId);
            _paragraphs.Add(new CollectedAmendmentParagraph(text, styleId, styleInfo)
            {
                FormulaMathMl = formulaMathMl,
                LeadingSymbol = leadingSymbol,
                LeftIndentTwips = leftIndentTwips,
            });

            // Bilans cudzysłowów — tylko dla zbierania bezstylowego. Rozbraja wyłącznie styl ZNACZĄCY
            // (nowelizacyjny Z/* lub rozpoznany styl ustawy matki) — domyślny styl edytora
            // („Normalny"/„Standard") nie niesie semantyki i nie może wyłączać granic cudzysłowowych.
            // Styl jednostki systematyzacyjnej w cytowanej treści (tytuł, komórka tabeli) nie jest stylem ustawy matki.
            var hasMeaningfulStyle = styleInfo != null
                || (Services.Classify.ParagraphClassifier.GetStyleType(styleId) is { } styleType
                    && !Services.Classify.ParagraphClassifier.IsSystematizingStyle(styleType));
            QuoteTracker.Observe(text, hasStyle: hasMeaningfulStyle);

            Log.Debug(
                "AmendmentCollector: dodano akapit #{Index} (styl={StyleId}, instrument={Instrument}, cel={Target})",
                _paragraphs.Count,
                styleId,
                styleInfo?.Instrument.ToString() ?? "brak",
                styleInfo?.TargetKind.ToString() ?? "brak");
        }

        /// <summary>
        /// Dodaje tabelę do bufora nowelizacji, w miejscu, w którym stoi w treści cytowanej. Cudzysłowy w jej
        /// komórkach liczą się w bilansie jak w akapicie: cytowaną tabelę otwiera się i zamyka także w niej
        /// samej — cudzysłowem w pierwszej komórce albo w ostatniej, razem z interpunkcją komendy („…",).
        /// Dla bilansu tabela jest jednym blokiem.
        /// </summary>
        public void AddTable(ContentTable table)
        {
            _paragraphs.Add(new CollectedAmendmentParagraph(string.Empty, null, null) { Table = table });
            QuoteTracker.Observe(TextOf(table), hasStyle: false);
            Log.Debug("AmendmentCollector: dodano tabelę #{Index} ({Rows} wierszy)", _paragraphs.Count, table.Rows.Count);
        }

        private static string TextOf(ContentTable table) => string.Join(
            "\n",
            table.Rows.SelectMany(row => row.Cells).SelectMany(cell => cell.Paragraphs)
                .Where(text => !string.IsNullOrWhiteSpace(text)));

        /// <summary>
        /// Resetuje kolektor do stanu poczatkowego.
        /// Wywolywane po przetworzeniu zebranych akapitow (lub w razie bledu).
        /// </summary>
        public void Reset()
        {
            var count = _paragraphs.Count;
            var ownerId = Owner?.Id;

            _paragraphs.Clear();
            Owner = null;
            Targets = [];
            QuoteTracker.Reset();
            OpenQuotation.Reset();

            if (count > 0)
            {
                Log.Debug("AmendmentCollector: zresetowano (zebrano {Count} akapitow dla {OwnerId})",
                    count, ownerId);
            }
        }
    }
}
