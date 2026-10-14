using System.Text.RegularExpressions;
using Saga.Core.Ingest;
using Saga.Model;

namespace Saga.Core.Services.Parsing
{
    /// <summary>
    /// Składa załączniki z bloków stojących za nagłówkiem „Załącznik(i) do ustawy z dnia …": wskazanie aktu,
    /// oznaczenie („Załącznik nr 1"), tytuł i treść — akapity i tabele w kolejności dokumentu.
    /// Treść będąca w całości wyliczeniem staje się jednostkami redakcyjnymi (<see cref="AnnexUnits"/>).
    /// </summary>
    internal static class AnnexBuilder
    {
        // „Załącznik do ustawy z dnia…", „Załączniki do ustawy…", „Załącznik nr 2 do ustawy…" — nie
        // „Załącznik do obwieszczenia", który w tekście jednolitym otwiera sam akt. Akt wykonawczy ma między
        // rodzajem a datą organ, a uchwała i zarządzenie także numer („Załącznik do rozporządzenia Ministra
        // Finansów z dnia…", „Załącznik do uchwały nr 113 Rady Ministrów z dnia…"); wskazanie nie sięga za nawias.
        internal static readonly Regex AttachedTo = new(
            @"^Załącznik(?<plural>i)?(?:\s+nr\s+(?<number>\d+[a-z]*))?\s+do\s+(?:ustawy|rozporządzenia|uchwały|zarządzenia)\b[^()]{0,120}?\bz\s+dnia\b",
            RegexOptions.Compiled);

        // Załącznik obwieszczenia, które nie ogłasza aktu („Załącznik do obwieszczenia Ministra Klimatu
        // i Środowiska z dnia …", „Załącznik nr 2 do obwieszczenia …") — tylko tam, gdzie granicę rozstrzygnięto
        // już na rzecz obwieszczenia (<see cref="AnnouncementBoundary.Layout"/>).
        private static readonly Regex AttachedToAnnouncement = new(
            @"^Załącznik(?<plural>i)?(?:\s+nr\s+(?<number>\d+[a-z]*))?\s+do\s+obwieszczenia\b[^()]{0,160}?\bz\s+dnia\b",
            RegexOptions.Compiled);

        // „Załącznik nr 4a" w osobnym wierszu; za numerem najwyżej znacznik odnośnika („Załącznik nr 2[5)]")
        // i dopisek w nawiasie („Załącznik nr 3 (uchylony)") — dopisek jest już treścią załącznika.
        // Zdanie zaczynające się tak samo to treść.
        private static readonly Regex Designation = new(
            @"^(?<heading>Załącznik\s+nr\s+(?<number>\d+[a-z]*)(?:\s*\[[^\[\]]*\])*)(?:\s*(?<note>\([^()]*\)(?:\s*\[[^\[\]]*\])*))?$",
            RegexOptions.Compiled);

        // „(Dz. U. poz. 252)" w wierszu pod wskazaniem aktu. Rozstrzyga treść, nie styl: w tym samym stylu
        // stoi też „(pominięty)" — a to już treść załącznika.
        private static readonly Regex JournalLine = new(@"^\(Dz\.\s*U\.", RegexOptions.Compiled);

        private static readonly string[] TitleStyles = ["TYTZAL", "TYTTABELI"];

        // Objaśnienie gwiazdki („*) Zgodnie z § 11 …", „*[)] Dotyczy położnych.", „**) …") — przypis wpisany akapitem,
        // nie pozycja wyliczenia. Gwiazdka bywa z nawiasem w indeksie górnym.
        private static readonly Regex AsteriskNote = new(
            @"^\s*(?:\[\s*\*+\s*\)?\s*\]|\*+\s*(?:\[\s*\)\s*\]|\))?)\s*\S", RegexOptions.Compiled);

        /// <param name="ofAnnouncement">Załączniki obwieszczenia: wskazaniem jest „Załącznik do obwieszczenia …".</param>
        internal static List<Annex> Build(IReadOnlyList<DocumentBlock> blocks, bool ofAnnouncement = false)
        {
            var header = ofAnnouncement ? AttachedToAnnouncement : AttachedTo;
            var annexes = new List<Annex>();
            var pendingAttachedTo = new List<string>();
            Annex? current = null;

            // Bloki akapitów treści każdego załącznika — z nich powstają jednostki, jeśli treść jest wyliczeniem.
            var paragraphs = new Dictionary<Annex, List<DocumentBlock>>();

            // Wiersz „(Dz. U. …)" domyka wskazanie aktu; tytuł stoi tuż za oznaczeniem, przed treścią.
            var attachedToOpen = false;
            var titleOpen = false;

            for (var i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];

                if (block.TableCell is { } cell)
                {
                    var run = blocks.Skip(i).TakeWhile(b => b.TableCell?.Table == cell.Table).ToList();
                    i += run.Count - 1;

                    Current().Content.Add(new AnnexBlock { Table = ContentTables.FromBlocks(run), IsLandscape = run[0].IsLandscape });
                    attachedToOpen = titleOpen = false;
                    continue;
                }

                if (block.IsEmpty)
                    continue;

                var text = block.Text.CollapseWhitespace().Trim();

                if (header.Match(text) is { Success: true } attached)
                {
                    current = null;
                    pendingAttachedTo.Add(text);
                    attachedToOpen = true;

                    // Liczba pojedyncza wskazuje jeden załącznik — zaczyna się od razu, z numerem albo bez.
                    if (!attached.Groups["plural"].Success)
                    {
                        Start(heading: null, attached.Groups["number"] is { Success: true } number ? number.Value : null);
                        titleOpen = true;
                    }

                    continue;
                }

                if (attachedToOpen && JournalLine.IsMatch(text))
                {
                    (current?.AttachedTo ?? pendingAttachedTo).Add(text);
                    continue;
                }

                attachedToOpen = false;

                if (Designation.Match(text) is { Success: true } designation)
                {
                    var annex = Start(designation.Groups["heading"].Value, designation.Groups["number"].Value);
                    titleOpen = true;

                    if (designation.Groups["note"] is { Success: true } note)
                        annex.Content.Add(new AnnexBlock { Text = note.Value, IsLandscape = block.IsLandscape });

                    continue;
                }

                if (titleOpen && IsTitle(block, isFirstLine: Current().Title is null))
                {
                    var annex = Current();
                    annex.Title = annex.Title is null ? text : annex.Title + " " + text;
                    continue;
                }

                titleOpen = false;
                Current().Content.Add(new AnnexBlock { Text = text, IsLandscape = block.IsLandscape });

                if (!paragraphs.TryGetValue(current!, out var collected))
                    paragraphs[current!] = collected = [];
                collected.Add(block);
            }

            foreach (var (annex, blocksOfAnnex) in paragraphs)
                StructureContent(annex, blocksOfAnnex);

            return annexes;

            // Treść przed pierwszym oznaczeniem należy do załącznika bez numeru.
            Annex Current() => current ?? Start(heading: null, number: null);

            Annex Start(string? heading, string? number)
            {
                current = new Annex { Heading = heading, Number = number, AttachedTo = [.. pendingAttachedTo] };
                pendingAttachedTo.Clear();
                annexes.Add(current);
                return current;
            }
        }

        /// <summary>
        /// Załącznik, którego cała treść to akapity wyliczenia, dostaje zamiast nich jednostki. Objaśnienia gwiazdek na
        /// końcu treści („*) Zgodnie z § 11 …") do wyliczenia nie należą — zostają akapitami za jednostkami. Treść
        /// z tabelą, dopiskiem albo choć jednym innym akapitem spoza wyliczenia zostaje, jaka była.
        /// </summary>
        private static void StructureContent(Annex annex, List<DocumentBlock> blocks)
        {
            if (annex.Content.Count != blocks.Count || annex.Content.Any(block => block.Text is null))
                return;

            var notes = 0;
            while (notes < blocks.Count && AsteriskNote.IsMatch(blocks[blocks.Count - 1 - notes].Text.CollapseWhitespace()))
                notes++;
            var enumeration = blocks.Take(blocks.Count - notes).ToList();
            if (enumeration.Count == 0)
                return;

            // Jednostki zastępują akapity wyliczenia — stoją na stronie pierwszego z nich.
            if (AnnexUnits.TryBuild(enumeration, annex.Id) is { } units)
            {
                annex.Content = units.Select(unit => new AnnexBlock { Unit = unit, IsLandscape = blocks[0].IsLandscape })
                    .Concat(annex.Content.Skip(enumeration.Count))
                    .ToList();
            }
        }

        // Tytuł ma własny styl; bez niego tytułem jest pierwszy wiersz, jeśli jest wyśrodkowany (styl bywa
        // pomylony — tytuł w stylu artykułu — układ zostaje) albo ma budowę tytułu i nie jest pozycją wyliczenia
        // (dokument bez szablonu nie niesie ani stylu, ani wyśrodkowania). Dalsze wyśrodkowane wiersze to już
        // treść: podpisy rysunków, nagłówki zestawień.
        private static bool IsTitle(DocumentBlock block, bool isFirstLine)
        {
            if (TitleStyles.Any(style => HasStyle(block, style)))
                return true;
            if (!isFirstLine)
                return false;

            var text = block.Text.CollapseWhitespace().Trim();
            return block.Layout?.Alignment == BlockAlignment.Center
                || (TitleForm.Matches(text) && !Classify.UnitDesignation.LooksDesignated(text));
        }

        private static bool HasStyle(DocumentBlock block, string prefix) =>
            block.StyleId?.StartsWith(prefix, StringComparison.Ordinal) == true;
    }
}
