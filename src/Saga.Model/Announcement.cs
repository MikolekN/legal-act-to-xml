using System.Collections.Generic;

#nullable enable

namespace Saga.Model
{
    /// <summary>
    /// Obwieszczenie w sprawie ogłoszenia tekstu jednolitego: to, co stoi przed aktem ogłaszanym w załączniku —
    /// punkty obwieszczenia, przytoczone przepisy, podpis. Nie należy do struktury ogłaszanego aktu.
    /// Nagłówek obwieszczenia (rodzaj, organ, data, przedmiot) niesie <see cref="LegalDocument"/>.
    /// </summary>
    public class Announcement
    {
        /// <summary>Treść obwieszczenia w kolejności dokumentu: akapity i tabele.</summary>
        public List<ContentBlock> Content { get; set; } = new();
    }
}
