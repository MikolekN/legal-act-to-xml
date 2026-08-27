using System.Text;
using Saga.Model;
using Saga.Model.EntryIntoForce;

namespace Saga.Core.Services.EntryIntoForce
{
    /// <summary>Wynik rozbioru powołania jednostek.</summary>
    internal sealed record UnitEnumerationResult(
        IReadOnlyList<ScopedUnitReference> Targets,
        IReadOnlyList<string> Unresolved)
    {
        /// <summary>Czy całe powołanie zostało rozłożone bez reszty.</summary>
        internal bool IsComplete => Unresolved.Count == 0 && Targets.All(t => t.IsApplicable);
    }

    /// <summary>
    /// Rozkłada powołanie jednostek z wyjątku („art. 1 pkt 3 i 9")
    /// na listę pojedynczych, rozwiniętych celów.
    /// </summary>
    internal static class UnitEnumerationParser
    {
        /// <summary>Imiesłowy kwalifikujące jednostkę w zakresie („w zakresie dodawanego art. 26e").</summary>
        private static readonly string[] ScopeQualifiers =
        {
            "dodawanego", "dodawanej", "dodawanych", "dodawanym",
            "uchylanego", "uchylanej", "uchylanych", "uchylanym",
            "zmienianego", "zmienianej", "zmienianych", "zmienianym",
        };

        private static string? MatchScopeQualifier(string text) =>
            ScopeQualifiers.FirstOrDefault(q => text.Contains(q, StringComparison.OrdinalIgnoreCase));

        private static bool IsScopeQualifier(UnitToken token) => MatchScopeQualifier(token.Text) is not null;

        /// <summary>Rozkłada powołanie jednostek na pojedyncze cele.</summary>
        internal static UnitEnumerationResult Parse(string? unitsText)
        {
            var targets = new List<ScopedUnitReference>();
            var unresolved = new List<string>();

            var tokens = UnitReferenceTokenizer.Tokenize(unitsText);
            if (tokens.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(unitsText))
                    unresolved.Add(unitsText.Trim());
                return new UnitEnumerationResult(targets, unresolved);
            }

            var state = new FoldState();

            for (var i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];

                switch (token.Kind)
                {
                    case UnitTokenKind.UnitKeyword:
                        state.Cursor.Announce(token);
                        break;

                    case UnitTokenKind.Designation:
                        ApplyDesignation(state, targets, token.Text, unresolved);
                        break;

                    case UnitTokenKind.Range:
                        ApplyRange(state, targets, token, unresolved);
                        break;

                    case UnitTokenKind.Separator:
                        if (token.Text == "," || token.Text == ";")
                            state.GroupStart = targets.Count;
                        break;

                    case UnitTokenKind.Dash:
                        state.DashPending = true;
                        break;

                    case UnitTokenKind.ScopeMarker:
                        i = ApplyScope(state, targets, tokens, i, unresolved);
                        break;

                    case UnitTokenKind.ProseScope:
                        MarkGroupProse(state, targets, token.Text);
                        unresolved.Add(token.Text);
                        break;

                    case UnitTokenKind.Prose:
                        unresolved.Add(token.Text);
                        break;
                }
            }

            state.FlushPending(targets);

            return new UnitEnumerationResult(targets, unresolved);
        }

        // ============================================================
        // Oznaczenia i przedziały
        // ============================================================

        private static void ApplyDesignation(
            FoldState state, List<ScopedUnitReference> targets, string designation, List<string> unresolved)
        {
            if (state.Cursor.BeginStep() is not { } context)
            {
                unresolved.Add(designation);
                return;
            }

            TakeStep(state, targets, context, designation, unresolved);
        }

        private static void ApplyRange(
            FoldState state, List<ScopedUnitReference> targets, UnitToken token, List<string> unresolved)
        {
            if (state.Cursor.BeginStep() is not { } context)
            {
                unresolved.Add(token.Text);
                return;
            }

            var expanded = DesignationRange.Expand(token.RangeFrom, token.RangeTo);
            if (expanded is null)
            {
                TakeStep(state, targets, context, token.Text, unresolved);
                state.MarkPendingUnapplicable($"nierozwijalny przedział „{token.Text}\"");
                unresolved.Add(token.Text);
                return;
            }

            foreach (var value in expanded)
                TakeStep(state, targets, context, value, unresolved);
        }

        /// <summary>
        /// Przesuwa kursor o jedno oznaczenie i rozstrzyga los poprzedniego kandydata.
        /// </summary>
        private static void TakeStep(
            FoldState state,
            List<ScopedUnitReference> targets,
            in UnitStepContext context,
            string designation,
            List<string> unresolved)
        {
            var step = state.Cursor.Step(context, designation);

            if (state.Pending is not null)
            {
                if (step.Previous == PendingDisposition.Drop)
                    state.DropPending();
                else
                    state.FlushPending(targets);
            }

            if (!step.IsApplied)
            {
                unresolved.Add(designation);
                return;
            }

            state.Pending = new ScopedUnitReference
            {
                Structure = state.Cursor.Snapshot(),
                RawText = state.Cursor.Path.ToString(),
            };
        }

        // ============================================================
        // Zakres („w zakresie …")
        // ============================================================

        /// <summary>
        /// Zbiera treść zakresu i powiązuje ją z ostatnio zbudowanym celem.
        /// </summary>
        private static int ApplyScope(
            FoldState state,
            List<ScopedUnitReference> targets,
            IReadOnlyList<UnitToken> tokens,
            int markerIndex,
            List<string> unresolved)
        {
            var anchorLevel = state.Cursor.Level;
            var scopeTokens = new List<UnitToken>();
            var raw = new StringBuilder(tokens[markerIndex].Text);
            var i = markerIndex + 1;

            // Poziom, na którym zaczyna się wyliczenie w zakresie — ustalany przy pierwszym słowie
            // kluczowym, bo dopiero on mówi, dokąd zakres sięga.
            var scopeLevel = UnitLevel.None;

            for (; i < tokens.Count; i++)
            {
                var token = tokens[i];

                if (token.Kind == UnitTokenKind.Separator)
                {
                    if (token.Text is "," or ";")
                        break;

                    if (token.Text.Equals("oraz", StringComparison.OrdinalIgnoreCase))
                        break;

                    if (i + 1 < tokens.Count && tokens[i + 1] is { Kind: UnitTokenKind.UnitKeyword } next &&
                        (next.Level == anchorLevel || ClimbsAbove(next.Level, scopeLevel)))
                        break;

                    scopeTokens.Add(token);
                    raw.Append(' ').Append(token.Text);
                    continue;
                }

                if (token.Kind is UnitTokenKind.Dash or UnitTokenKind.ScopeMarker or UnitTokenKind.ProseScope)
                    break;

                if (scopeLevel == UnitLevel.None && token.Kind == UnitTokenKind.UnitKeyword)
                    scopeLevel = token.Level;

                scopeTokens.Add(token);
                raw.Append(' ').Append(token.Text);
            }

            var scopeText = raw.ToString();
            var qualifier = scopeTokens
                .Where(t => t.Kind == UnitTokenKind.Prose)
                .Select(t => MatchScopeQualifier(t.Text))
                .FirstOrDefault(q => q is not null);

            var hasForeignProse = scopeTokens.Any(t => t.Kind == UnitTokenKind.Prose && !IsScopeQualifier(t));

            var structural = hasForeignProse ? null : BuildScopeStructures(scopeTokens);

            if (structural is null or { Count: 0 })
            {
                state.ApplyScopeToGroup(targets, ScopeKind.Prose, null, qualifier, scopeText);
                state.MarkPendingUnapplicable($"zakres nierozkładalny: „{scopeText}\"");
                unresolved.Add(scopeText);
            }
            else
            {
                state.ApplyScopeToGroup(targets, ScopeKind.Structural, structural, qualifier, scopeText);
            }

            state.DashPending = false;
            return i - 1;
        }

        /// <summary>
        /// Czy powołanie wraca ponad poziom, na którym zaczął się zakres. Wewnątrz zakresu wolno
        /// zostać na poziomie („w zakresie art. 85a ust. 1 i art. 85c") albo zejść głębiej
        /// („…art. 85a ust. 1 i ust. 3"); powrót W GÓRĘ znaczy, że zakres się skończył i wróciliśmy
        /// do wyliczenia głównego („…w zakresie uchylanego pkt 5 i art. 5").
        /// </summary>
        private static bool ClimbsAbove(UnitLevel level, UnitLevel scopeLevel) =>
            scopeLevel != UnitLevel.None && level != UnitLevel.None && level < scopeLevel;

        /// <summary>Rozkłada treść zakresu na wskazane jednostki.</summary>
        private static List<StructuralReference>? BuildScopeStructures(IReadOnlyList<UnitToken> tokens)
        {
            var results = new List<StructuralReference>();
            var cursor = new UnitPathCursor();
            StructuralReference? candidate = null;

            bool Take(in UnitStepContext context, string designation)
            {
                var step = cursor.Step(context, designation);
                if (!step.IsApplied)
                    return false;

                if (candidate is not null && step.Previous == PendingDisposition.Flush)
                    results.Add(candidate);

                candidate = cursor.Snapshot();
                return true;
            }

            foreach (var token in tokens)
            {
                switch (token.Kind)
                {
                    case UnitTokenKind.UnitKeyword:
                        cursor.Announce(token);
                        break;

                    case UnitTokenKind.Designation:
                        {
                            if (cursor.BeginStep() is not { } context || !Take(context, token.Text))
                                return null;
                            break;
                        }

                    case UnitTokenKind.Range:
                        {
                            if (cursor.BeginStep() is not { } context)
                                return null;

                            var expanded = DesignationRange.Expand(token.RangeFrom, token.RangeTo);
                            if (expanded is null)
                                return null;

                            foreach (var value in expanded)
                                if (!Take(context, value))
                                    return null;
                            break;
                        }

                    case UnitTokenKind.Separator:
                        break;

                    case UnitTokenKind.Prose:
                        if (!IsScopeQualifier(token))
                            return null;
                        break;

                    default:
                        return null;
                }
            }

            if (candidate is not null)
                results.Add(candidate);

            return results;
        }

        private static void MarkGroupProse(FoldState state, List<ScopedUnitReference> targets, string rawText)
        {
            state.ApplyScopeToGroup(targets, ScopeKind.Prose, null, null, rawText);
            state.MarkPendingUnapplicable($"zakres opisany prozą: „{rawText}\"");
        }

        // ============================================================
        // Stan foldu
        // ============================================================

        private sealed class FoldState
        {
            internal UnitPathCursor Cursor { get; } = new();

            /// <summary>Cel zbudowany, ale oczekujący.</summary>
            internal ScopedUnitReference? Pending { get; set; }

            /// <summary>Indeks w liście celów, od którego zaczyna się grupa objęta półpauzą.</summary>
            internal int GroupStart { get; set; }

            /// <summary>Czy napotkano półpauzę wprowadzającą zakres dla całej grupy.</summary>
            internal bool DashPending { get; set; }

            internal void FlushPending(List<ScopedUnitReference> targets)
            {
                if (Pending is null)
                    return;
                targets.Add(Pending);
                Pending = null;
            }

            internal void DropPending() => Pending = null;

            internal void MarkPendingUnapplicable(string reason)
            {
                if (Pending is null)
                    return;
                Pending.IsApplicable = false;
                Pending.ScopeRawText ??= reason;
            }

            /// <summary>Dopisuje zakres do ostatnio budowanego celu.</summary>
            internal void ApplyScopeToGroup(
                List<ScopedUnitReference> targets,
                ScopeKind kind,
                IReadOnlyList<StructuralReference>? structures,
                string? qualifier,
                string rawText)
            {
                if (DashPending)
                {
                    for (var i = GroupStart; i < targets.Count; i++)
                        Assign(targets[i]);
                    if (Pending is not null)
                        Assign(Pending);
                    return;
                }

                if (Pending is not null)
                    Assign(Pending);
                else if (targets.Count > 0)
                    Assign(targets[^1]);

                void Assign(ScopedUnitReference reference)
                {
                    reference.ScopeKind = kind;
                    reference.ScopeStructures = structures is null
                        ? new List<StructuralReference>()
                        : structures.Select(s => s.Clone()).ToList();
                    reference.ScopeQualifier = qualifier;
                    reference.ScopeRawText = rawText;
                    if (kind == ScopeKind.Prose)
                        reference.IsApplicable = false;
                }
            }
        }
    }
}
