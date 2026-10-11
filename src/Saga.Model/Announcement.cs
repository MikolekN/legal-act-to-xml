using System.Collections.Generic;

#nullable enable

namespace Saga.Model
{
    /// <summary>
    /// Obwieszczenie w sprawie ogłoszenia tekstu jednolitego: to, co stoi przed aktem ogłaszanym w załączniku —
    /// punkty obwieszczenia, przytoczone przepisy, podpis. Nie należy do struktury ogłaszanego aktu.
    /// Nagłówek obwieszczenia (rodzaj, organ, data, przedmiot) niesie <see cref="LegalDocument"/>;
    /// strefę tytułową ogłaszanego aktu — to obwieszczenie.
    /// </summary>
    public class Announcement
    {
        /// <summary>Wiersz otwierający ogłaszany akt („Załącznik do obwieszczenia … (Dz. U. poz. 1094)").</summary>
        public string AttachedTo { get; set; } = string.Empty;

        /// <summary>Wiersz rodzaju ogłaszanego aktu („USTAWA"); pusty, gdy nierozpoznany.</summary>
        public string ActKindLine { get; set; } = string.Empty;

        /// <summary>Wiersz daty ogłaszanego aktu („z dnia 11 sierpnia 2001 r."); pusty, gdy nierozpoznany.</summary>
        public string ActDateLine { get; set; } = string.Empty;

        /// <summary>Przedmiot ogłaszanego aktu („o odbudowie obiektów budowlanych"); pusty, gdy nierozpoznany.</summary>
        public string ActSubject { get; set; } = string.Empty;

        /// <summary>Treść obwieszczenia w kolejności dokumentu: akapity i tabele.</summary>
        public List<ContentBlock> Content { get; set; } = new();
    }
}
