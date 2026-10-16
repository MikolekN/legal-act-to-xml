using Saga.Core.Ingest;
using Saga.Core.Services.Classify;
using Saga.Model;
using Saga.Model.SystematizingUnits;
using DtoArticle = Saga.Model.EditorialUnits.Article;
using DtoLetter = Saga.Model.EditorialUnits.Letter;
using DtoParagraph = Saga.Model.EditorialUnits.Paragraph;
using DtoPoint = Saga.Model.EditorialUnits.Point;
using DtoTiret = Saga.Model.EditorialUnits.Tiret;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Kontekst parsowania przechowujacy aktualny stan drzewa encji
    /// oraz biezaca pozycje strukturalna w hierarchii jednostek redakcyjnych.
    /// </summary>
    public sealed class ParsingContext
    {
        public ParsingContext(LegalDocument document, Subchapter subchapter)
        {
            Document = document;
            Subchapter = subchapter;

            // Bieżąca ścieżka jednostek systematyzujących z kanonicznego (zawsze spójnego) drzewa dokumentu.
            // Dla realnego parsowania Subchapter pokrywa się z CurrentChapter.Subchapters[0].
            CurrentPart = document.RootPart;
            CurrentBook = CurrentPart.Books[0];
            CurrentTitle = CurrentBook.Titles[0];
            CurrentDivision = CurrentTitle.Divisions[0];
            CurrentChapter = CurrentDivision.Chapters[0];
        }

        public LegalDocument Document { get; }

        /// <summary>Bieżący oddział — miejsce dołączania artykułów. Aktualizowany przez SystematizingUnitBuilder.</summary>
        public Subchapter Subchapter { get; internal set; }

        // Bieżąca ścieżka jednostek systematyzujących (Część → … → Rozdział). Aktualizowana przy wejściu
        // w jawną jednostkę; służy do dołączania jednostek-rodzeństwa na właściwym poziomie.
        public Part CurrentPart { get; internal set; }
        public Book CurrentBook { get; internal set; }
        public Title CurrentTitle { get; internal set; }
        public Division CurrentDivision { get; internal set; }
        public Chapter CurrentChapter { get; internal set; }

        /// <summary>Jednostka systematyzacyjna oczekująca na tytuł (drugi wiersz wzorca dwuwierszowego, § 60).</summary>
        public ISystematizingUnit? PendingHeadingUnit { get; set; }

        /// <summary>Jednostka, której tytuł ze stylu właśnie przypisano — kolejny blok w stylu tytułu go dokleja.</summary>
        public ISystematizingUnit? HeadingContinuationUnit { get; set; }

        /// <summary>Za nagłówkiem załącznika aktu — dalsze bloki nie należą do struktury aktu.</summary>
        public bool InAnnex { get; set; }

        /// <summary>Bloki od nagłówka załącznika do końca dokumentu; załączniki składa z nich <c>AnnexBuilder</c>.</summary>
        public List<DocumentBlock> AnnexBlocks { get; } = new();

        /// <summary>
        /// Czy samodzielny „§" jest w tym dokumencie jednostką podstawową — z rodzaju aktu rozpoznanego przez
        /// klasyfikator dokumentu (rozporządzenie, uchwała, zarządzenie; także ogłaszane w tekście jednolitym).
        /// </summary>
        public bool SectionIsBasicUnit { get; init; }

        /// <summary>
        /// MathML bloku przetwarzanego w tej chwili, gdy blok jest wzorem (<see cref="Ingest.DocumentBlock.FormulaMathMl"/>);
        /// trafia do dalszej treści jednostki razem z zapisem liniowym.
        /// </summary>
        public string? CurrentFormula { get; set; }

        /// <summary>
        /// Akapit w stylu jednostki bez oznaczenia zostaje jednostką (ADR-0024), zamiast dalszą treścią poprzedniej
        /// (ADR-0039). Ustawia to budowa jednostek załącznika: tam jednostka bez oznaczenia powtarza adres i tym
        /// samym mówi, że treść załącznika nie jest w całości wyliczeniem — zostaje wtedy akapitami.
        /// </summary>
        public bool StyleOnlyUnitsStayUnits { get; init; }

        /// <summary>Równanie otwierające przetwarzany blok (symbol wiersza legendy wzoru); null, gdy go nie ma.</summary>
        public Ingest.BlockSymbol? CurrentSymbol { get; set; }

        /// <summary>
        /// Wcięcie lewe przetwarzanego bloku (twips) — idzie z akapitem do treści cytowanej, gdzie bez stylu
        /// rozstrzyga część wspólną za tiretami (ADR-0091); null bez układu (TXT).
        /// </summary>
        public int? CurrentLeftIndent { get; set; }

        /// <summary>
        /// Akapity, przed którymi cytat nowelizacji kończy się mimo otwartego bilansu cudzysłowów — wskazane
        /// przez przebieg, w którym cytat nie domknął się do końca dokumentu (ADR-0049).
        /// </summary>
        public IReadOnlySet<DocumentBlock> QuotationBreaks { get; init; } = NoQuotationBreaks;

        /// <summary>
        /// Akapit, przed którym powinien był się skończyć cytat otwarty do końca dokumentu; null, gdy takiego
        /// cytatu nie ma albo nic w nim nie wygląda na dalszy ciąg aktu zmieniającego.
        /// </summary>
        public DocumentBlock? UnclosedQuotationBreak { get; set; }

        private static readonly HashSet<DocumentBlock> NoQuotationBreaks = [];

        /// <summary>Bloki treści obwieszczenia tekstu jednolitego — sprzed aktu ogłaszanego w załączniku.</summary>
        public List<DocumentBlock> AnnouncementBlocks { get; } = new();

        /// <summary>
        /// Bloki tabeli stojącej w treści aktu, zbierane do jej końca; tabela trafia wtedy do dalszej treści
        /// jednostki, w której stoi.
        /// </summary>
        public List<DocumentBlock> TableBlocks { get; } = new();

        /// <summary>
        /// Czy zbierana tabela zaczęła się w treści nowelizacji (po komendzie albo w cytacie). Tabela należy
        /// w całości tam, gdzie się zaczęła — nie dzieli się jej między przepis i cytat.
        /// </summary>
        public bool TableInAmendment { get; set; }

        /// <summary>Liczba jednostek nazwanych bez numeru danego rodzaju — źródło tymczasowego eId (TMP1, TMP2…).</summary>
        public Dictionary<ParagraphKind, int> NamedUnitCounts { get; } = new();

        public DtoArticle? CurrentArticle { get; set; }
        public DtoParagraph? CurrentParagraph { get; set; }
        public DtoPoint? CurrentPoint { get; set; }
        public DtoLetter? CurrentLetter { get; set; }

        /// <summary>
        /// Stos aktywnych tiretow wg glebokosci (0 = brak, 1 = TIR, 2 = 2TIR, 3 = 3TIR).
        /// Ostatni element to biezacy wlasciciel nowelizacji dla poziomu tiret.
        /// Czyszczony przy kazdym wejsciu na poziom Letter/Point/Paragraph/Article.
        /// </summary>
        public List<DtoTiret> TiretStack { get; } = new();

        /// <summary>Skrot: najglebszy aktywny tiret (lub null).</summary>
        public DtoTiret? CurrentTiret => TiretStack.Count > 0 ? TiretStack[^1] : null;

        /// <summary>
        /// Wciecia lewe (twips) tiretow otwartych na stosie <see cref="TiretStack"/> — lista rownolegla,
        /// zawsze tej samej dlugosci. null gdy blok nie niosl ukladu (np. TXT). Zasila wnioskowanie
        /// glebokosci tiretu z wciecia (§ 58 ZTP), gdy styl 2TIR/3TIR nie rozstrzyga. Utrzymywana wylacznie
        /// przez <see cref="PushTiret"/>/<see cref="PopTiretsToDepth"/>/<see cref="ClearTiretStack"/>.
        /// </summary>
        public List<int?> OpenTiretIndents { get; } = new();

        /// <summary>
        /// Wcięcie lewe (twips) bloku bieżącej litery; null, gdy blok nie niósł układu. Odróżnia część wspólną liter
        /// od części wspólnej tiretów w dokumencie bez stylów (<see cref="StructureProcessor"/>).
        /// </summary>
        public int? CurrentLetterIndent { get; set; }

        /// <summary>Dodaje tiret na stos wraz z jego wcieciem lewym (utrzymuje synchronizacje obu list).</summary>
        public void PushTiret(DtoTiret tiret, int? leftIndentTwips)
        {
            TiretStack.Add(tiret);
            OpenTiretIndents.Add(leftIndentTwips);
        }

        /// <summary>Zdejmuje ze stosu tirety o glebokosci >= depth (pozostawia depth-1 poziomow).</summary>
        public void PopTiretsToDepth(int depth)
        {
            while (TiretStack.Count >= depth && TiretStack.Count > 0)
            {
                TiretStack.RemoveAt(TiretStack.Count - 1);
                OpenTiretIndents.RemoveAt(OpenTiretIndents.Count - 1);
            }
        }

        /// <summary>Czysci caly stos tiretow (wejscie na poziom Letter/Point/Paragraph/Article/jednostke systematyzacyjna).</summary>
        public void ClearTiretStack()
        {
            TiretStack.Clear();
            OpenTiretIndents.Clear();
        }

        /// <summary>Kolektor metadanych aktu (rodzaj/data/przedmiot) ze strefy tytulowej (§ 16-19 ZTP).</summary>
        public DocumentMetadataCollector Metadata { get; private set; } = new();

        /// <summary>Kolektor nagłówka obwieszczenia, odstawiony na granicy ogłaszanego aktu; null poza tekstem jednolitym.</summary>
        public DocumentMetadataCollector? AnnouncementMetadata { get; private set; }

        /// <summary>
        /// Dokument jest obwieszczeniem, które nie ogłasza aktu: cała treść jest treścią obwieszczenia,
        /// a załączniki — jego załącznikami.
        /// </summary>
        public bool IsPlainAnnouncement { get; init; }

        /// <summary>Wiersz „Załącznik do obwieszczenia …" otwierający ogłaszany akt.</summary>
        public string AnnouncedActAttachedTo { get; private set; } = string.Empty;

        /// <summary>
        /// Granica tekstu jednolitego: nagłówek obwieszczenia jest zebrany, a strefa tytułowa ogłaszanego aktu
        /// („USTAWA", data, przedmiot) zbiera się od nowa.
        /// </summary>
        public void BeginAnnouncedAct(string attachedTo)
        {
            AnnouncedActAttachedTo = attachedTo;
            AnnouncementMetadata = Metadata;
            Metadata = new DocumentMetadataCollector { ExpectKindFirst = true };
        }

        /// <summary>
        /// Serwis do budowania i aktualizacji referencji strukturalnych
        /// w kontekscie nowelizacji.
        /// </summary>
        public LegalReferenceService ReferenceService { get; } = new();

        /// <summary>
        /// Biezaca pozycja strukturalna w hierarchii jednostek redakcyjnych
        /// (art. -> ust. -> pkt -> lit. -> tiret). Aktualizowana przez orkiestrator
        /// po kazdym zbudowaniu encji.
        /// </summary>
        public StructuralReference CurrentStructuralReference { get; set; } = new();

        /// <summary>
        /// Wykryte cele nowelizacji w tresci jednostek redakcyjnych.
        /// Klucz: Guid encji, Wartosc: wykryte cele (referencje strukturalne z RawText).
        /// Wypelniane przez orkiestrator podczas parsowania encji IHasAmendments.
        /// </summary>
        public Dictionary<Guid, IReadOnlyList<StructuralAmendmentReference>> DetectedAmendmentTargets { get; } = new();

        /// <summary>
        /// Zmiany w tekście zapisane w tym samym zdaniu co zmiana jednostki („w pkt 20 kropkę zastępuje się
        /// średnikiem i dodaje się pkt 21 w brzmieniu:") — czekają na nowelizację tego przepisu, do której
        /// dołączy je <see cref="AmendmentFinalizer"/>. Klucz: Guid przepisu zmieniającego.
        /// </summary>
        public Dictionary<Guid, List<TextChange>> PendingTextChanges { get; } = new();

        /// <summary>
        /// Obudowanie dotychczasowej treści jednostki zapisane w przepisie zmieniającym („dotychczasową treść
        /// oznacza się jako ust. 1 i dodaje się ust. 2 w brzmieniu:") — czeka na nowelizację tego przepisu.
        /// Klucz: Guid przepisu zmieniającego.
        /// </summary>
        public Dictionary<Guid, (ContentWrap As, StructuralAmendmentReference? Unit)> PendingContentWraps { get; } = new();

        /// <summary>
        /// Zmiany załączników zapisane odesłaniem do załączników aktu zmieniającego — czekają na nowelizację
        /// przepisu, który je zapisał. Klucz: Guid przepisu zmieniającego.
        /// </summary>
        public Dictionary<Guid, IReadOnlyList<AnnexInstruction>> PendingAnnexInstructions { get; } = new();

        /// <summary>
        /// Czy aktualnie przetwarza akapity bedace trescia nowelizacji.
        /// Ustawiane na true gdy:
        /// - napotkano akapit ze stylem Z/... (Z/UST, Z/ART, Z/PKT itd.)
        /// - po triggerze ("otrzymuje brzmienie:") napotkano akapit bez stylu ustawy matki
        /// Resetowane gdy napotkano akapit z rozpoznanym stylem ustawy matki (ART, UST, PKT, LIT, TIR).
        /// </summary>
        public bool InsideAmendment { get; set; }

        /// <summary>
        /// Czy przetworzony wlasnie akapit zawieral zwrot rozpoczynajacy nowelizacje
        /// ("otrzymuje brzmienie:", "w brzmieniu:"). Ustawiane PO przetworzeniu
        /// akapitu, sprawdzane PRZED przetworzeniem nastepnego.
        /// </summary>
        public bool AmendmentTriggerDetected { get; set; }

        /// <summary>
        /// Głębokość cudzysłowu przytoczenia otwartego akapitem pod przepisem, który niczego nie zmienia
        /// („1) oświadczenie następującej treści:" i „„Oświadczam, że:"). Dopóki jest większa od zera, kolejne akapity —
        /// w dowolnym stylu — należą do przytoczenia (ADR-0076).
        /// </summary>
        public int ProvisionQuoteDepth { get; set; }

        /// <summary>Czy zbierana treść to przytoczenie otwarte pod przepisem (<see cref="ProvisionQuoteDepth"/>).</summary>
        public bool InProvisionQuote { get; set; }

        /// <summary>
        /// Bufor akapitow biezacej nowelizacji. Zbiera akapity od wejscia w tryb
        /// nowelizacji az do powrotu do stylu ustawy matki, po czym deleguje
        /// do AmendmentBuilder (iteracja 2).
        /// </summary>
        public AmendmentCollector AmendmentCollector { get; } = new();

        /// <summary>
        /// Encja, na ktorej wykryto ostatni trigger nowelizacji.
        /// Przechowywana tymczasowo do momentu rozpoczecia zbierania
        /// (Begin) w AmendmentCollector.
        /// </summary>
        public BaseEntity? AmendmentOwner { get; set; }

    }
}
