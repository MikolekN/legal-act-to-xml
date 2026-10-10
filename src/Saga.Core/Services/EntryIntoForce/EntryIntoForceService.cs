using Saga.Core.Services.Parsing;
using Saga.Core.Services.UnitReferences;
using Saga.Model;
using Saga.Model.EntryIntoForce;
using Serilog;

namespace Saga.Core.Services.EntryIntoForce
{
    /// <summary>
    /// Wyszukuje w zbudowanym dokumencie klauzulę wejścia w życie, rozkłada ją na strukturę
    /// i zapisuje wyliczone daty do modelu.
    /// </summary>
    public sealed class EntryIntoForceService
    {
        private readonly EntryIntoForceClauseScanner _scanner = new();

        /// <summary>Analizuje dokument i wypełnia daty wejścia w życie.</summary>
        public void Apply(LegalDocument document)
        {
            if (document is null)
                return;

            var candidates = _scanner.Scan(document);
            if (candidates.Count == 0)
            {
                Log.Warning("Nie znaleziono klauzuli wejścia w życie w żadnej jednostce dokumentu");
                return;
            }

            var main = candidates.LastOrDefault(c => c.Role == ClauseRole.Main);
            if (main is null)
            {
                var amendingOnly = candidates.LastOrDefault(c =>
                    c.Role == ClauseRole.Stray && c.RejectionReason == "klauzula w artykule nowelizującym");

                if (amendingOnly is null)
                {
                    Log.Warning("Kandydaci na klauzulę wejścia w życie odrzuceni przez filtry: {Reasons}",
                        string.Join("; ", candidates.Select(c => c.RejectionReason ?? c.Role.ToString())));
                    return;
                }

                main = amendingOnly with { Role = ClauseRole.Main };
            }

            var announcementDate = document.AnnouncementDate;

            if (announcementDate is null)
                Log.Warning("Brak daty ogłoszenia — daty względem dnia ogłoszenia pozostaną nierozstrzygnięte");

            var info = BuildInfo(main, announcementDate);

            CollectPartials(document, candidates, main, announcementDate, info);
            RecordStrays(candidates, main, info);

            document.EntryIntoForce = info;

            if (info.ResolvedDate is { } resolved)
                document.EffectiveDate = resolved;

            if (info.RetroactiveFrom is { } retroactive)
                document.RetroactiveFrom = retroactive;

            var exceptions = BuildExceptionMap(document, info);
            EffectiveDatePropagator.Propagate(
                document, new EntryDates(info.ResolvedDate, info.RetroactiveFrom), exceptions);

            foreach (var conflict in exceptions.Conflicts)
            {
                info.UnresolvedFragments.Add($"sprzeczne wyjątki dla tej samej jednostki — {conflict}");
                Log.Warning("Sprzeczne wyjątki wejścia w życie: {Conflict}", conflict);
            }

            Log.Information("Klauzula wejścia w życie ({Unit}): {Info}", info.SourceUnitEId ?? "?", info);
        }

        // ============================================================
        // Reguła ogólna
        // ============================================================

        private static EntryIntoForceInfo BuildInfo(ClauseCandidate main, DateTime? announcementDate)
        {
            // Reguła ogólna kończy się w miejscu pierwszego wyjątku.
            var generalPart = CutAtFirstMarker(main.Tail);
            var (withoutRetro, retroactive, retroRaw) = EntryIntoForceFormulaParser.SplitRetroactivity(generalPart);

            var formula = EntryIntoForceFormulaParser.Parse(withoutRetro);
            var info = new EntryIntoForceInfo
            {
                RawText = main.Text,
                Formula = formula,
                ResolvedDate = EntryIntoForceDateResolver.Resolve(formula, announcementDate),
                RetroactiveFrom = retroactive,
                RetroactiveFromRawText = retroRaw,
                SourceUnitEId = main.Owner.Id,
            };

            if (formula.Kind == EntryIntoForceFormulaKind.Unrecognized)
            {
                info.UnresolvedFragments.Add(withoutRetro);
                ValidationReporter.AddValidationMessage(main.Owner, ValidationLevel.Warning,
                    $"Nierozpoznana formuła wejścia w życie: „{Preview(withoutRetro)}\".");
            }
            else if (info.ResolvedDate is null)
            {
                ValidationReporter.AddValidationMessage(main.Owner, ValidationLevel.Info,
                    "Klauzula wejścia w życie rozpoznana, data nierozstrzygnięta.");
            }

            return info;
        }

        // ============================================================
        // Wyjątki
        // ============================================================

        private static void CollectPartials(
            LegalDocument document,
            IReadOnlyList<ClauseCandidate> candidates,
            ClauseCandidate main,
            DateTime? announcementDate,
            EntryIntoForceInfo info)
        {
            var sectionSignIsBasicUnit = SectionSignRole.IsBasicUnit(document.Classification);

            // Wyjątki w treści klauzuli głównej („…, z wyjątkiem art. 2, który wchodzi w życie …").
            foreach (var (units, tail) in SplitInlineExceptions(main.Tail))
                info.Partials.Add(BuildPartial(units, tail, announcementDate, main.Owner, info, sectionSignIsBasicUnit));

            CollectProvisos(main, announcementDate, info, sectionSignIsBasicUnit);

            // Wyjątki jako osobne pozycje listy pod klauzulą kończącą się „z wyjątkiem:".
            if (!main.OpensExceptionList)
                return;

            foreach (var item in candidates.Where(c => c.Role == ClauseRole.ExceptionListItem))
            {
                var units = UnitsBeforeRelativePronoun(item.Text);
                if (units is null)
                {
                    info.UnresolvedFragments.Add(item.Text);
                    continue;
                }

                info.Partials.Add(BuildPartial(units, item.Tail, announcementDate, item.Owner, info, sectionSignIsBasicUnit));
            }
        }

        /// <summary>
        /// Rozbija ogon klauzuli głównej na pary (powołanie jednostek, formuła) dla wyjątków
        /// zapisanych w tym samym zdaniu.
        /// </summary>
        private static IEnumerable<(string Units, string Tail)> SplitInlineExceptions(string tail)
        {
            var marker = EntryIntoForcePatterns.ExceptionMarker.Match(tail);
            if (!marker.Success || marker.Groups["colon"].Success)
                yield break;

            var rest = tail[(marker.Index + marker.Length)..];

            while (rest.Length > 0)
            {
                var relative = EntryIntoForcePatterns.RelativeClause.Match(rest);
                if (!relative.Success)
                    yield break;

                var units = UnitsBefore(rest, relative.Index);
                var formulaTail = relative.Groups["tail"].Value;

                var proviso = EntryIntoForcePatterns.ProvisoMarker.Match(formulaTail);
                var next = EntryIntoForcePatterns.ExceptionMarker.Match(formulaTail);

                if (proviso.Success && (!next.Success || proviso.Index < next.Index))
                {
                    yield return (units, formulaTail[..proviso.Index].Trim());
                    yield break;
                }

                if (next.Success)
                {
                    yield return (units, formulaTail[..next.Index].Trim());
                    rest = formulaTail[(next.Index + next.Length)..];
                    continue;
                }

                yield return (units, formulaTail.Trim());
                yield break;
            }
        }

        /// <summary>
        /// Zbiera człony „z tym że …". Człon niosący czasownik klauzuli („z tym że art. 5 wchodzi
        /// w życie z dniem …") jest zwykłym wyjątkiem daty, tyle że zapisanym bez zaimka względnego.
        /// </summary>
        private static void CollectProvisos(
            ClauseCandidate main, DateTime? announcementDate, EntryIntoForceInfo info, bool sectionSignIsBasicUnit)
        {
            foreach (var chunk in SplitProvisos(main.Tail))
            {
                if (chunk.Length == 0)
                    continue;

                if (EntryIntoForcePatterns.AnyClauseVerb.Match(chunk) is { Success: true } verb)
                {
                    info.Partials.Add(BuildPartial(
                        UnitsBefore(chunk, verb.Index),
                        chunk[(verb.Index + verb.Length)..].Trim(),
                        announcementDate, main.Owner, info, sectionSignIsBasicUnit));
                    continue;
                }

                var items = EntryIntoForceFormulaParser.SplitScopedRetroactivity(chunk).ToList();
                if (items.Count == 0)
                {
                    info.UnresolvedFragments.Add(chunk);
                    ValidationReporter.AddValidationMessage(main.Owner, ValidationLevel.Warning,
                        $"Nierozpoznany człon klauzuli po „z tym że\": „{Preview(chunk)}\".");
                    continue;
                }

                foreach (var (units, retroactive, rawText) in items)
                    info.Partials.Add(BuildRetroactivityPartial(units, retroactive, rawText, main.Owner, info, sectionSignIsBasicUnit));
            }
        }

        /// <summary>
        /// Wycina człony stojące po „z tym że". Człon kończy się na kolejnym znaczniku — swoim
        /// albo znaczniku wyjątku.
        /// </summary>
        private static IEnumerable<string> SplitProvisos(string tail)
        {
            var rest = tail;

            while (EntryIntoForcePatterns.ProvisoMarker.Match(rest) is { Success: true } marker)
            {
                var chunk = rest[(marker.Index + marker.Length)..];
                var end = NextMarkerIndex(chunk);

                yield return (end is { } cut ? chunk[..cut] : chunk).Trim();

                if (end is not { } next)
                    yield break;

                rest = chunk[next..];
            }
        }

        /// <summary>Powołanie jednostek stojące przed zaimkiem względnym („art. 8, który wchodzi…").</summary>
        private static string? UnitsBeforeRelativePronoun(string text)
        {
            var relative = EntryIntoForcePatterns.RelativeClause.Match(text);
            if (!relative.Success || relative.Index == 0)
                return null;

            var units = UnitsBefore(text, relative.Index);
            return units.Length > 0 ? units : null;
        }

        private static string UnitsBefore(string text, int relativeIndex) =>
            text[..relativeIndex].Trim().TrimEnd(',', ' ');

        private static PartialEntryIntoForce BuildPartial(
            string unitsText,
            string formulaTail,
            DateTime? announcementDate,
            BaseEntity owner,
            EntryIntoForceInfo info,
            bool sectionSignIsBasicUnit)
        {
            var (withoutRetro, retroactive, _) = EntryIntoForceFormulaParser.SplitRetroactivity(formulaTail);
            var formula = EntryIntoForceFormulaParser.Parse(withoutRetro);
            var enumeration = UnitEnumerationParser.Parse(unitsText, sectionSignIsBasicUnit);

            var partial = new PartialEntryIntoForce
            {
                Targets = enumeration.Targets.ToList(),
                Formula = formula,
                ResolvedDate = EntryIntoForceDateResolver.Resolve(formula, announcementDate),
                RetroactiveFrom = retroactive,
                RawText = $"{unitsText}, {formulaTail}".Trim(),
            };

            foreach (var fragment in enumeration.Unresolved)
                info.UnresolvedFragments.Add(fragment);

            if (partial.Targets.Count == 0)
            {
                partial.IsApplicable = false;
                partial.UnresolvedReason = "nie rozpoznano żadnej jednostki będącej celem wyjątku";
            }
            else if (!formula.IsResolvable)
            {
                partial.IsApplicable = false;
                partial.UnresolvedReason = formula.Kind switch
                {
                    EntryIntoForceFormulaKind.DeterminedByCommunication =>
                        "data wskazana w komunikacie — brak daty kalendarzowej",
                    EntryIntoForceFormulaKind.DeterminedByIntroducingAct =>
                        "termin określony w odrębnym akcie — brak daty kalendarzowej",
                    _ => $"nierozpoznana formuła: „{Preview(withoutRetro)}\"",
                };
            }

            if (!partial.IsApplicable)
                ValidationReporter.AddValidationMessage(owner, ValidationLevel.Warning,
                    $"Wyjątek wejścia w życie nieprzypisany: {partial.UnresolvedReason}.");

            return partial;
        }

        /// <summary>
        /// Wyjątek nadający wskazanym jednostkom samą moc wsteczną.
        /// </summary>
        private static PartialEntryIntoForce BuildRetroactivityPartial(
            string unitsText,
            DateTime retroactive,
            string rawText,
            BaseEntity owner,
            EntryIntoForceInfo info,
            bool sectionSignIsBasicUnit)
        {
            var enumeration = UnitEnumerationParser.Parse(unitsText, sectionSignIsBasicUnit);

            var partial = new PartialEntryIntoForce
            {
                Kind = PartialEntryIntoForceKind.RetroactivityOnly,
                Targets = enumeration.Targets.ToList(),
                RetroactiveFrom = retroactive,
                RawText = $"{unitsText} {rawText}".Trim(),
            };

            foreach (var fragment in enumeration.Unresolved)
                info.UnresolvedFragments.Add(fragment);

            if (partial.Targets.Count == 0)
            {
                partial.IsApplicable = false;
                partial.UnresolvedReason = "nie rozpoznano jednostki, której nadano moc wsteczną";

                ValidationReporter.AddValidationMessage(owner, ValidationLevel.Warning,
                    $"Moc wsteczna nieprzypisana: {partial.UnresolvedReason}.");
            }

            return partial;
        }

        // ============================================================
        // Propagacja na jednostki
        // ============================================================

        /// <summary>
        /// Rozpisuje cele wyjątków na konkretne encje i oznacza je w mapie.
        /// </summary>
        private static EntryIntoForceExceptionMap BuildExceptionMap(LegalDocument document, EntryIntoForceInfo info)
        {
            var map = new EntryIntoForceExceptionMap();

            foreach (var partial in info.Partials)
            {
                var targets = partial.Targets.Where(t => t.IsApplicable).ToList();
                if (targets.Count == 0)
                    continue;

                var missed = new List<string>();

                foreach (var target in targets)
                {
                    var host = EntryIntoForceTargetResolver.Resolve(document, target.Structure);
                    if (host is null)
                    {
                        missed.Add(target.Structure.ToString());
                        continue;
                    }

                    if (target.ScopeKind == ScopeKind.Structural && target.ScopeStructures.Count > 0)
                    {
                        MarkScope(host, target, partial, map, missed);
                        continue;
                    }

                    Mark(map, host, target.Structure, partial);
                }

                if (missed.Count == 0)
                    continue;

                partial.UnresolvedReason = $"jednostki nieodnalezione w akcie: {string.Join(", ", missed)}";
                info.UnresolvedFragments.Add(partial.UnresolvedReason);
                Log.Warning("Wyjątek wejścia w życie wskazuje jednostki spoza aktu: {Units}",
                    string.Join(", ", missed));
            }

            return map;
        }

        /// <summary>Oznacza jednostki wskazane zakresem, szukając ich w cytowanej treści.</summary>
        private static void MarkScope(
            BaseEntity host, ScopedUnitReference target, PartialEntryIntoForce partial,
            EntryIntoForceExceptionMap map, List<string> missed)
        {
            // Uchylenie nie cytuje treści — „w zakresie uchylanego pkt 20" wskazuje jednostkę aktu
            // zmienianego, której ten dokument nigdzie nie przytacza. Nie ma czego datować.
            var hasQuotedContent = EntryIntoForceTargetResolver.AmendmentOf(host)?.Content is not null;
            if (!hasQuotedContent)
            {
                missed.Add($"{target.Structure} w zakresie {string.Join(", ", target.ScopeStructures)} " +
                           "(jednostka nie cytuje treści — np. uchylenie)");
                return;
            }

            foreach (var scope in target.ScopeStructures)
            {
                var scoped = EntryIntoForceTargetResolver.ResolveInAmendment(host, scope);
                if (scoped is null)
                {
                    missed.Add($"{target.Structure} w zakresie {scope}");
                    continue;
                }

                Mark(map, scoped, scope, partial);
            }
        }

        private static void Mark(
            EntryIntoForceExceptionMap map, BaseEntity unit, StructuralReference reference,
            PartialEntryIntoForce partial)
        {
            var segment = EntryIntoForceTargetResolver.ResolveSegment(unit, reference);

            // Jednostka nie jest wyjęta z reguły ogólnej, więc nadpisujemy sam kanał
            // mocy wstecznej i zostawiamy datę wejścia w życie.
            if (partial.Kind == PartialEntryIntoForceKind.RetroactivityOnly)
            {
                if (segment is not null)
                    map.MarkSegmentRetroactivity(segment, partial.RetroactiveFrom);
                else
                    map.MarkUnitRetroactivity(unit, partial.RetroactiveFrom);

                return;
            }

            // Wyjątek daty wyłącza jednostkę z reguły ogólnej w całości.
            var dates = new EntryDates(partial.ResolvedDate, partial.RetroactiveFrom);

            if (segment is not null)
            {
                map.MarkSegment(segment, dates);
                return;
            }

            map.MarkUnit(unit, dates);
        }

        // ============================================================
        // Pomocnicze
        // ============================================================

        private static void RecordStrays(
            IReadOnlyList<ClauseCandidate> candidates, ClauseCandidate main, EntryIntoForceInfo info)
        {
            foreach (var candidate in candidates)
            {
                if (ReferenceEquals(candidate, main) || candidate.Role != ClauseRole.Stray)
                    continue;

                info.UnresolvedFragments.Add(candidate.Text);
                ValidationReporter.AddValidationMessage(candidate.Owner, ValidationLevel.Info,
                    $"Pominięte wystąpienie klauzuli wejścia w życie ({candidate.RejectionReason}).");
            }
        }

        /// <summary>
        /// Ucina ogon klauzuli na pierwszym znaczniku zmieniającym adresata: wyjątku
        /// („z wyjątkiem", „z zastrzeżeniem") albo członu „z tym że".
        /// </summary>
        private static string CutAtFirstMarker(string tail) =>
            NextMarkerIndex(tail) is { } index ? tail[..index].Trim() : tail.Trim();

        /// <summary>Pozycja najbliższego znacznika wyjątku.</summary>
        private static int? NextMarkerIndex(string text)
        {
            int? result = null;

            if (EntryIntoForcePatterns.ExceptionMarker.Match(text) is { Success: true } exception)
                result = exception.Index;

            if (EntryIntoForcePatterns.ProvisoMarker.Match(text) is { Success: true } proviso)
                result = result is { } current ? Math.Min(current, proviso.Index) : proviso.Index;

            return result;
        }

        private static string Preview(string text) =>
            text.Length > 120 ? text[..120] + "…" : text;
    }
}
