using Saga.Core.Services.Parsing;
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

			// Klauzula główna
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

			var exceptions = BuildExceptionMap(document, info);
			EffectiveDatePropagator.Propagate(document, info.ResolvedDate, exceptions);

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
			var generalPart = CutAtExceptions(main.Tail);
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
			// Wyjątki w treści klauzuli głównej („…, z wyjątkiem art. 2, który wchodzi w życie …").
			foreach (var (units, tail) in SplitInlineExceptions(main.Tail))
				info.Partials.Add(BuildPartial(units, tail, announcementDate, main.Owner, info));

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

				info.Partials.Add(BuildPartial(units, item.Tail, announcementDate, item.Owner, info));
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

				// Kolejny wyjątek w tym samym zdaniu — odetnij formułę na jego znaczniku.
				var next = EntryIntoForcePatterns.ExceptionMarker.Match(formulaTail);
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
			EntryIntoForceInfo info)
		{
			var (withoutRetro, retroactive, _) = EntryIntoForceFormulaParser.SplitRetroactivity(formulaTail);
			var formula = EntryIntoForceFormulaParser.Parse(withoutRetro);
			var enumeration = UnitEnumerationParser.Parse(unitsText);

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

				var date = partial.ResolvedDate;
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
						MarkScope(host, target, date, map, missed);
						continue;
					}

					Mark(map, host, target.Structure, date);
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
			BaseEntity host, ScopedUnitReference target, DateTime? date,
			EntryIntoForceExceptionMap map, List<string> missed)
		{
			// Uchylenie nie cytuje treści — „w zakresie uchylanego pkt 20" wskazuje jednostkę aktu
			// zmienianego, której ten dokument nigdzie nie przytacza. Nie ma czego datować.
			var hasQuotedContent = host is IHasAmendments { Amendment.Content: not null };
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

				Mark(map, scoped, scope, date);
			}
		}

		private static void Mark(
			EntryIntoForceExceptionMap map, BaseEntity unit, StructuralReference reference, DateTime? date)
		{
			if (EntryIntoForceTargetResolver.ResolveSegment(unit, reference) is { } segment)
			{
				map.MarkSegment(segment, date);
				return;
			}

			map.MarkUnit(unit, date);
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

		private static string CutAtExceptions(string tail)
		{
			var marker = EntryIntoForcePatterns.ExceptionMarker.Match(tail);
			return marker.Success ? tail[..marker.Index].Trim() : tail.Trim();
		}

		private static string Preview(string text) =>
			text.Length > 120 ? text[..120] + "…" : text;
	}
}
