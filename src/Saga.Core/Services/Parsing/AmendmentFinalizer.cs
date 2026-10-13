using System.Text.RegularExpressions;
using Saga.Core.Helpers;
using Saga.Model;
using Saga.Model.EditorialUnits;
using Serilog;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Dane wejściowe dla finalizatora nowelizacji.
    /// Zawiera wynik budowania (AmendmentContent), kolektor z metadanymi
    /// oraz kontekst parsowania do linkowania z JournalInfo i walidacji.
    /// </summary>
    public sealed record AmendmentFinalizerInput(
        AmendmentContent Content,
        AmendmentCollector Collector,
        ParsingContext Context,
        AmendmentOperationType? ForcedOperationType = null,
        IReadOnlyList<TextChange>? TextChanges = null)
    {
        /// <summary>
        /// Cele ustalone przez wywołującego zamiast odczytanych z adresu jednostek — gdy przedmiotem komendy
        /// nie jest jednostka (odnośnik). Pusta lista: cel nieznany.
        /// </summary>
        public IReadOnlyList<StructuralAmendmentReference>? Targets { get; init; }
    }

    /// <summary>
    /// Serwis finalizujący nowelizację (Faza 3).
    ///
    /// Odpowiada za:
    /// 1. Detekcję typu operacji (Modification/Insertion/Repeal) na podstawie treści triggera
    /// 2. Tworzenie obiektu Amendment z wynikami AmendmentBuilder
    /// 3. Łączenie z JournalInfo (TargetLegalAct z artykułu nadrzędnego)
    /// 4. Przypisanie nowelizacji do encji-właściciela (IHasAmendments)
    /// 5. Walidację i raportowanie diagnostyczne
    ///
    /// Wywoływany przez orkiestrator po zamknięciu nowelizacji
    /// (powrót do stylu ustawy matki lub koniec dokumentu).
    /// </summary>
    public sealed class AmendmentFinalizer
    {
        // ============================================================
        // Wzorce tekstowe do rozpoznawania typu operacji
        // ============================================================

        private static readonly Regex InsertionPattern = new(
            @"dodaje\s+się",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // „skreśla się" — historyczny czasownik uchylenia (akty sprzed 2009 r., wciąż liczne w korpusie);
        // DetectOperationType od zawsze DEKLAROWAŁ jego obsługę — wzorzec dorównany do dokumentacji.
        internal static readonly Regex RepealPattern = new(
            @"(?:uchyla|skreśla)\s+się",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        internal static readonly Regex ModificationPattern = new(
            @"(?:otrzymuje\s+brzmienie:|otrzymują\s+brzmienie:|w\s+brzmieniu:|zastępuje\s+się:)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Zwrot zmiany brzmienia zamykający przepis („… dodaje się pkt 4 w brzmieniu:") — po nim stoi treść
        /// cytowana, cokolwiek przepis mówił wcześniej.
        /// </summary>
        internal static readonly Regex ModificationAtEndPattern = new(
            @"(?:otrzymuj[eą]\s+brzmienie|w\s+brzmieniu):\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Czy zwrot zmiany brzmienia zostawia brzmienie otwarte na kolejne akapity. Komenda kończy się
        /// dwukropkiem, a nowe brzmienie stoi za nią w cudzysłowie (§ 85–89 ZTP): zwrot na końcu akapitu
        /// („… otrzymuje brzmienie:") albo cytat otwarty za nim i niedomknięty w tym akapicie zapowiadają treść
        /// w kolejnych akapitach. Zwrot, po którym brzmienie mieści się w tym samym akapicie („… posługuje się
        /// nazwą w brzmieniu: Polish Air Navigation Services Agency", „… rotę ślubowania w brzmieniu: „Ślubuję
        /// …"."), niczego za sobą nie otwiera — bez tego rozróżnienia zwykły przepis z takim zwrotem połykał
        /// dalszą część aktu jako treść cytowaną.
        /// </summary>
        internal static bool LeavesWordingOpen(string text)
        {
            var phrases = ModificationPattern.Matches(text);
            if (phrases.Count == 0)
                return false;

            var rest = text[(phrases[^1].Index + phrases[^1].Length)..];
            if (string.IsNullOrWhiteSpace(rest))
                return true;

            var open = 0;
            var straight = 0;
            foreach (var character in rest)
            {
                if (character == QuotationMarks.Opening)
                    open++;
                else if (QuotationMarks.IsClosing(character) && open > 0)
                    open--;
                else if (character == QuotationMarks.Straight)
                    straight++;
            }

            return open > 0 || straight % 2 == 1;
        }

        /// <summary>
        /// Zwrot zmiany brzmienia na końcu przepisu, ale bez dwukropka („ust. 1 otrzymuje brzmienie") —
        /// usterka zapisu; komendą czyni go dopiero cudzysłów otwierający następnego bloku.
        /// </summary>
        /// <summary>
        /// Komenda, której przedmiotem jest zdanie („zdanie pierwsze otrzymuje brzmienie:", „dodaje się zdanie
        /// drugie w brzmieniu:") — treść cytowana to wtedy całe zdania (ADR-0057). Miejscownik („w zdaniu
        /// pierwszym …") przedmiotu nie nazywa. Wprowadzenie do wyliczenia zdaniem nie jest: kończy je dwukropek,
        /// a domykają dopiero pozycje wyliczenia.
        /// </summary>
        internal static readonly Regex SentenceObjectPattern = new(
            $@"\bzdani[ea]\b[^:{QuotationMarks.Any}]{{0,60}}?(?:otrzymuj[eą]\s+brzmienie|w\s+brzmieniu)\s*:\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        internal static readonly Regex ModificationWithoutColonPattern = new(
            @"(?:otrzymuj[eą]\s+brzmienie|w\s+brzmieniu)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // ============================================================
        // Główna metoda finalizacji
        // ============================================================

        /// <summary>
        /// Finalizuje nowelizację: tworzy obiekt Amendment, wykrywa typ operacji,
        /// łączy z JournalInfo, przypisuje do właściciela i waliduje wynik.
        /// Zwraca null jeśli brak danych wejściowych (pusty kolektor).
        /// </summary>
        public Amendment? Finalize(AmendmentFinalizerInput input)
        {
            var collector = input.Collector;
            var content = input.Content;
            var context = input.Context;

            if (collector.Owner == null)
            {
                Log.Warning("AmendmentFinalizer: brak właściciela nowelizacji — pomijam finalizację");
                return null;
            }

            // 1. Typ operacji: wymuszony przez wywołującego (np. ReplaceWords — czasowniki wewnątrz
            //    cytowanych wyrazów nie mogą przeinaczyć operacji) albo wykryty z treści triggera.
            //    Zmiana w tekście zapisana w tym samym zdaniu („skreśla się średnik i dodaje się … w brzmieniu:")
            //    o operacji na jednostce nie rozstrzyga — jej czasownik nie jest uchyleniem (ADR-0068).
            var operationType = input.ForcedOperationType
                ?? DetectOperationType(AmendmentCommandParser.WithoutTextChanges(collector.Owner.ContentText));

            // 2. Zbuduj obiekt Amendment
            var amendment = new Amendment
            {
                OperationType = operationType,
                Content = operationType == AmendmentOperationType.Repeal ? null : content
            };
            if (amendment.Content is { PlainText.Length: > 0 } quoted)
                quoted.PlainTextIsSentences = SentenceObjectPattern.IsMatch(collector.Owner.ContentText.TrimEnd());

            amendment.TextChanges.AddRange(input.TextChanges ?? []);
            if (context.PendingTextChanges.Remove(collector.Owner.Guid, out var pendingTextChanges))
                amendment.TextChanges.AddRange(pendingTextChanges);
            if (context.PendingContentWraps.Remove(collector.Owner.Guid, out var pendingWrap))
                (amendment.WrapsContentAs, amendment.WrappedUnit) = pendingWrap;
            if (context.PendingAnnexInstructions.Remove(collector.Owner.Guid, out var pendingAnnexes))
                amendment.AnnexInstructions.AddRange(pendingAnnexes);

            // 3. Połącz z celem nowelizacji (StructuralAmendmentReference)
            if (input.Targets is { } givenTargets)
                amendment.Targets.AddRange(givenTargets);
            else
                LinkTargets(amendment, collector, context);

            // 3a. Uzupełnij cel z komendy tekstowej — źródło o niższym priorytecie niż mapa stylów;
            //     dokłada tylko to, czego adres celu nie wskazał (tiret), i ostrzega o fragmencie zdania.
            EnrichTargetsFromCommand(amendment, collector);
            AmendmentTargetPairing.AnchorContentUnits(amendment);

            // 4. Połącz z JournalInfo (TargetLegalAct)
            var journal = ResolveTargetJournal(collector.Owner, context);
            if (journal != null)
            {
                amendment.TargetLegalAct = journal;
            }

            // 5. Przypisz nowelizację do właściciela
            var assigned = AssignToOwner(amendment, collector.Owner);

            // 6. Walidacja i raportowanie
            ValidateAmendment(amendment, collector.Owner);
            ReportStylelessLimitations(collector);

            Log.Information(
                "AmendmentFinalizer: {OperationType} przypisana do {UnitType} [{EntityId}] " +
                "(cele: {TargetCount}, treść: {ContentSummary})",
                amendment.OperationType,
                collector.Owner.UnitType,
                collector.Owner.Id,
                amendment.Targets.Count,
                content?.ToString() ?? "brak");

            if (!assigned)
            {
                Log.Warning(
                    "AmendmentFinalizer: właściciel {UnitType} [{EntityId}] nie implementuje IHasAmendments — " +
                    "nowelizacja utworzona, ale nie przypisana",
                    collector.Owner.UnitType,
                    collector.Owner.Id);
            }

            return amendment;
        }

        // ============================================================
        // Detekcja typu operacji
        // ============================================================

        /// <summary>
        /// Rozpoznaje typ operacji nowelizacyjnej na podstawie treści triggera.
        /// Priorytet: Repeal > Insertion > Modification (domyślny).
        ///
        /// Wzorce:
        /// - "uchyla się", "skreśla się", "traci moc" → Repeal
        /// - "dodaje się" → Insertion
        /// - "otrzymuje brzmienie:", "zastępuje się" → Modification
        /// - Brak wzorca → Modification (domyślny)
        /// </summary>
        internal static AmendmentOperationType DetectOperationType(string? triggerText)
        {
            if (string.IsNullOrWhiteSpace(triggerText))
                return AmendmentOperationType.Modification;

            // Repeal ma najwyższy priorytet — jeśli jest "uchyla się", to uchylenie
            if (RepealPattern.IsMatch(triggerText))
                return AmendmentOperationType.Repeal;

            // Insertion: "dodaje się ... w brzmieniu:" lub samo "dodaje się"
            if (InsertionPattern.IsMatch(triggerText))
                return AmendmentOperationType.Insertion;

            // Modification: "otrzymuje brzmienie:", "zastępuje się"
            if (ModificationPattern.IsMatch(triggerText))
                return AmendmentOperationType.Modification;

            // Domyślnie — Modification
            return AmendmentOperationType.Modification;
        }

        // ============================================================
        // Łączenie z celami nowelizacji
        // ============================================================

        /// <summary>
        /// Łączy nowelizację z wykrytymi celami (StructuralAmendmentReference).
        /// Źródła (w kolejności priorytetu):
        /// 1. Targets z kolektora (bezpośrednio z Begin())
        /// 2. DetectedAmendmentTargets z kontekstu (po Guid właściciela)
        /// </summary>
        private static void LinkTargets(Amendment amendment, AmendmentCollector collector, ParsingContext context)
        {
            // Priorytet 1: cele z kolektora
            if (collector.Targets.Count > 0)
            {
                amendment.Targets.AddRange(collector.Targets);
                return;
            }

            // Priorytet 2: cele z mapy DetectedAmendmentTargets
            if (collector.Owner != null &&
                context.DetectedAmendmentTargets.TryGetValue(collector.Owner.Guid, out var detectedTargets))
            {
                amendment.Targets.AddRange(detectedTargets);
            }
        }

        /// <summary>
        /// Uzupełnia referencje celów o to, czego adres celu (LegalReferenceService) nie wskazał,
        /// na podstawie komendy tekstowej z treści właściciela (niższy priorytet niż mapa stylów):
        /// - „tiret 2" / „tiret drugie" → Structure.Tiret (liczebnik porządkowy to forma KANONICZNA
        ///   powołania tiretu — § 57 ust. 6 ZTP — mapowany na wartość liczbową),
        /// - cel typu fragment („zdanie") → Warning o częściowej referencji (tylko zbieranie bezstylowe,
        ///   by nie dodawać nowej diagnostyki na niezmienionej ścieżce stylowej).
        /// </summary>
        private static void EnrichTargetsFromCommand(Amendment amendment, AmendmentCollector collector)
        {
            var owner = collector.Owner;
            if (owner == null)
                return;

            var command = AmendmentCommandParser.Parse(owner.ContentText);
            if (command == null)
                return;

            // Tiret dodawane („dodaje się tiret piąte") nie jest celem — celem jest jednostka, w której się dodaje.
            if (command.Kind != AmendmentCommandKind.Add
                && command.TargetKind == AmendmentTargetKind.Tiret && command.TargetOrdinalValue is { } tiretNo)
            {
                foreach (var target in amendment.Targets)
                {
                    if (target.Structure.Tiret == null)
                        target.Structure.Tiret = tiretNo.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            if (command.TargetKind == AmendmentTargetKind.Fragment && IsStylelessCollection(collector))
            {
                ValidationReporter.AddValidationMessage(owner, ValidationLevel.Warning,
                    "Cel nowelizacji typu fragment (zdanie) — referencja strukturalna jest częściowa " +
                    "(wskazuje jednostkę nadrzędną, nie fragment).");
            }
        }

        /// <summary>Czy zebrane akapity nowelizacji są w całości bezstylowe (Etap 8 — ścieżka tekstowa).</summary>
        private static bool IsStylelessCollection(AmendmentCollector collector)
        {
            foreach (var para in collector.Paragraphs)
            {
                if (para.StyleInfo != null || !string.IsNullOrEmpty(para.StyleId))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Raportuje ograniczenia semantyki nieosiągalnej bez stylów Z/* (Etap 8):
        /// - komenda nowelizacyjna wewnątrz cytatu (zagnieżdżona ZZ) — nierozkładalna, zachowana jako treść,
        /// - tirety w cytowanej treści bez stylów — głębokość i część wspólna nieustalane (poziom 1).
        /// </summary>
        private static void ReportStylelessLimitations(AmendmentCollector collector)
        {
            var owner = collector.Owner;
            if (owner == null)
                return;

            if (collector.QuoteTracker.NestedTriggerSuppressed)
            {
                ValidationReporter.AddValidationMessage(owner, ValidationLevel.Warning,
                    "Treść nowelizacji zawiera zagnieżdżoną komendę nowelizacyjną (ZZ) — " +
                    "bez stylów nierozkładalna; zachowano jako treść cytatu.");
            }

            // Finalizacja przy OTWARTYM cytacie (brak „" zamykającego do końca zbierania — literówka/OCR):
            // granica nowelizacji jest niepewna, kolejne akapity mogły zostać wchłonięte do treści.
            if (collector.QuoteTracker.IsInsideQuote)
            {
                ValidationReporter.AddValidationMessage(owner, ValidationLevel.Warning,
                    "Cytat treści nowelizacji niedomknięty do końca zbierania — granica nowelizacji " +
                    "niepewna; kolejne akapity mogły zostać błędnie wchłonięte do treści.");
            }

            if (collector.QuoteTracker.IsArmed && collector.Count > 0)
            {
                var hasTirets = false;
                foreach (var para in collector.Paragraphs)
                {
                    if (para.StyleInfo == null && Classify.ParagraphClassifier.IsTiretByText(para.Text))
                    {
                        hasTirets = true;
                        break;
                    }
                }
                if (hasTirets)
                {
                    ValidationReporter.AddValidationMessage(owner, ValidationLevel.Warning,
                        "Tirety w cytowanej treści nowelizacji bez stylów — głębokość zagnieżdżenia " +
                        "i część wspólna nieustalane (przyjęto poziom 1).");
                }
            }
        }

        // ============================================================
        // Łączenie z JournalInfo
        // ============================================================

        /// <summary>
        /// Rozpoznaje publikator (JournalInfo) aktu zmienianego.
        /// Szuka w hierarchii encji nadrzędnych aż do Article,
        /// która ma sparsowane Journals (z JournalReferenceService).
        /// Fallback: Document.SourceJournal.
        /// </summary>
        internal static JournalInfo? ResolveTargetJournal(BaseEntity owner, ParsingContext context)
        {
            // Szukaj artykułu nadrzędnego z publikatorem
            var article = FindParentArticle(owner);
            if (article is { Journals.Count: > 0 })
            {
                return article.Journals[0];
            }

            // Fallback: publikator z dokumentu (jeśli dokument zawiera jeden akt zmieniany)
            var sourceJournal = context.Document.SourceJournal;
            if (sourceJournal.Year > 0 && sourceJournal.Positions.Count > 0)
            {
                return sourceJournal;
            }

            return null;
        }

        /// <summary>
        /// Nawiguje w górę hierarchii encji, szukając artykułu nadrzędnego.
        /// </summary>
        private static Article? FindParentArticle(BaseEntity? entity)
        {
            var current = entity;
            while (current != null)
            {
                if (current is Article article)
                    return article;
                current = current.Article ?? current.Parent;
            }
            return null;
        }

        // ============================================================
        // Przypisanie do właściciela
        // ============================================================

        /// <summary>
        /// Przypisuje nowelizację do encji-właściciela (IHasAmendments).
        /// Zwraca true jeśli przypisanie się powiodło.
        /// </summary>
        private static bool AssignToOwner(Amendment amendment, BaseEntity owner)
        {
            if (owner is not IHasAmendments hasAmendments)
                return false;

            // Jednostka niesie jedną nowelizację. Druga treść cytowana pod tą samą jednostką (akapity w stylu
            // zmiany rozdzielone dalszą treścią jednostki) dołącza do pierwszej — nie może jej zastąpić.
            if (hasAmendments.Amendment is { Content: { } existing } && amendment.Content is { } added)
            {
                Append(existing, added);
                hasAmendments.Amendment.TextChanges.AddRange(amendment.TextChanges);
                if (amendment.WrapsContentAs != ContentWrap.None)
                    (hasAmendments.Amendment.WrapsContentAs, hasAmendments.Amendment.WrappedUnit) = (amendment.WrapsContentAs, amendment.WrappedUnit);
                ValidationReporter.AddValidationMessage(owner, ValidationLevel.Warning,
                    "Druga treść cytowana pod tą samą jednostką — dołączona do pierwszej nowelizacji.");
                return true;
            }

            hasAmendments.Amendment = amendment;
            return true;
        }

        private static void Append(AmendmentContent existing, AmendmentContent added)
        {
            if (!string.IsNullOrEmpty(added.PlainText))
            {
                existing.PlainText = string.IsNullOrEmpty(existing.PlainText)
                    ? added.PlainText
                    : existing.PlainText + Environment.NewLine + added.PlainText;
            }

            existing.PlainTextIsSentences |= added.PlainTextIsSentences;
            existing.Footnote ??= added.Footnote;
            existing.SystematizingUnits.AddRange(added.SystematizingUnits);
            existing.Articles.AddRange(added.Articles);
            existing.Paragraphs.AddRange(added.Paragraphs);
            existing.Points.AddRange(added.Points);
            existing.Letters.AddRange(added.Letters);
            existing.Tirets.AddRange(added.Tirets);
            existing.CommonParts.AddRange(added.CommonParts);
            existing.Tables.AddRange(added.Tables);
        }

        // ============================================================
        // Walidacja i raportowanie
        // ============================================================

        /// <summary>
        /// Waliduje zbudowaną nowelizację i dodaje komunikaty diagnostyczne
        /// do encji-właściciela.
        /// </summary>
        internal static void ValidateAmendment(Amendment amendment, BaseEntity owner)
        {
            // Brak celów nowelizacji; załącznik zmieniany odesłaniem ma cel w instrukcji, nie w adresie jednostki.
            if (amendment.Targets.Count == 0 && amendment.AnnexInstructions.Count == 0)
            {
                ValidationReporter.AddValidationMessage(owner, ValidationLevel.Warning,
                    "Nowelizacja bez wykrytego celu strukturalnego (brak referencji art./ust./pkt/lit.).");
            }

            if (amendment.Targets.Count > 1 &&
                AmendmentTargetPairing.ContentUnits(amendment.Content) is { Count: > 0 } units &&
                AmendmentTargetPairing.Pair(amendment.Targets, units) is null)
            {
                ValidationReporter.AddValidationMessage(owner, ValidationLevel.Warning,
                    $"Treści nowelizacji nie da się rozdzielić między wskazane cele ({string.Join(", ", amendment.Targets)}) " +
                    "— eksport wskazuje tylko pierwszy cel.");
            }

            // Repeal nie powinien mieć treści
            if (amendment.OperationType == AmendmentOperationType.Repeal && amendment.Content != null)
            {
                ValidationReporter.AddValidationMessage(owner, ValidationLevel.Info,
                    "Uchylenie zawiera treść — może to wskazywać na błędną klasyfikację operacji.");
            }

            // Modification/Insertion powinny mieć treść
            if (amendment.OperationType != AmendmentOperationType.Repeal && amendment.Content == null)
            {
                ValidationReporter.AddValidationMessage(owner, ValidationLevel.Warning,
                    $"{amendment.OperationType} bez treści — brak akapitów nowelizacji.");
            }

            // Treść bez żadnych jednostek redakcyjnych (oprócz PlainText); zmiana wyrazów treści cytowanej nie ma.
            if (amendment.Content != null && amendment.TextChanges.Count == 0 && amendment.AnnexInstructions.Count == 0
                && amendment.WrapsContentAs == ContentWrap.None && IsContentEmpty(amendment.Content))
            {
                ValidationReporter.AddValidationMessage(owner, ValidationLevel.Info,
                    "Treść nowelizacji nie zawiera jednostek redakcyjnych (tylko tekst/brak treści).");
            }

            // Brak JournalInfo
            if (amendment.TargetLegalAct.Year == 0 || amendment.TargetLegalAct.Positions.Count == 0)
            {
                ValidationReporter.AddValidationMessage(owner, ValidationLevel.Info,
                    "Nie wykryto publikatora (Dz.U.) aktu zmienianego dla tej nowelizacji.");
            }
        }

        /// <summary>
        /// Sprawdza czy AmendmentContent jest pusta (nie zawiera żadnych jednostek redakcyjnych).
        /// </summary>
        private static bool IsContentEmpty(AmendmentContent content)
        {
            return content.SystematizingUnits.Count == 0
                && content.Articles.Count == 0
                && content.Paragraphs.Count == 0
                && content.Points.Count == 0
                && content.Letters.Count == 0
                && content.Tirets.Count == 0
                && content.CommonParts.Count == 0
                && content.Tables.Count == 0
                && content.Footnote is null
                && string.IsNullOrEmpty(content.PlainText);
        }
    }
}
