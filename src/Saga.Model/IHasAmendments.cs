#nullable enable

using System.Collections.Generic;

namespace Saga.Model
{
    /// <summary>
    /// Kontrakt dla jednostek, ktore zawieraja nowelizacje.
    /// </summary>
    public interface IHasAmendments
    {
        /// <summary>
        /// Nowelizacja przypisana do jednostki (0 lub 1) — komenda jej ostatniego zdania, ta, po której może stać
        /// treść cytowana.
        /// </summary>
        Amendment? Amendment { get; set; }

        /// <summary>
        /// Nowelizacje komend z wcześniejszych zdań jednostki („art. 127 uchyla się. W art. 128 zdanie drugie otrzymuje
        /// brzmienie:"), w kolejności z tekstu. Każda zna zdanie, w którym stoi (<see cref="Model.Amendment.CommandSentence"/>);
        /// zwykle pusta (ADR-0122).
        /// </summary>
        List<Amendment> EarlierSentenceAmendments { get; }
    }
}
